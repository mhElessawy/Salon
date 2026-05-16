using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public SalesController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // ===== قائمة الفواتير =====
        public async Task<IActionResult> Index(string? date, string? type)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var user = await _userManager.GetUserAsync(User);
            var userDept = user?.UserDepartment;
            var roles = await _userManager.GetRolesAsync(user!);
            var isEmployee = roles.Contains("Employee");

            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= filterDate && s.SaleDate < nextDay);

            // Hide opposing department's invoices from restricted users
            if (userDept == "مساج")
                query = query.Where(s => s.SaleType != "حلاقة");
            else if (userDept == "حلاقة")
                query = query.Where(s => s.SaleType != "مساج");

            // Employees see only their own invoices
            if (isEmployee && user?.LinkedEmployeeId.HasValue == true)
                query = query.Where(s => s.EmployeeId == user.LinkedEmployeeId!.Value);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(s => s.SaleType == type);

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.FilterType = type;
            ViewBag.TotalSales = sales.Sum(s => s.NetAmount);
            ViewBag.UserDepartment = userDept;
            ViewBag.IsEmployee = isEmployee;
            return View(sales);
        }

        // ===== فاتورة حلاقة (PAR-) =====
        public async Task<IActionResult> CreateBarber()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            var role = roles.FirstOrDefault() ?? "";

            // مستخدمو قسم المساج لا يملكون صلاحية فواتير الحلاقة
            if (user!.UserDepartment == "مساج")
                return Forbid();

            await PopulateDeptDropdowns("حلاقة", user, role);
            var sale = new Sale
            {
                InvoiceNumber = await GenerateInvoiceNumber("PAR"),
                SaleDate = DateTime.Now,
                SaleType = "حلاقة"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateBarber")]
        public async Task<IActionResult> CreateBarberPost(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys)
        {
            model.SaleType = "حلاقة";
            return await SaveServiceInvoice(model, itemIds, itemNames, itemPrices, itemQtys, "حلاقة");
        }

        // ===== فاتورة مساج (MAS-) =====
        public async Task<IActionResult> CreateMassage()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            var role = roles.FirstOrDefault() ?? "";

            // مستخدمو قسم الحلاقة لا يملكون صلاحية فواتير المساج
            if (user!.UserDepartment == "حلاقة")
                return Forbid();

            await PopulateDeptDropdowns("مساج", user, role);
            var sale = new Sale
            {
                InvoiceNumber = await GenerateInvoiceNumber("MAS"),
                SaleDate = DateTime.Now,
                SaleType = "مساج"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateMassage")]
        public async Task<IActionResult> CreateMassagePost(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys)
        {
            model.SaleType = "مساج";
            return await SaveServiceInvoice(model, itemIds, itemNames, itemPrices, itemQtys, "مساج");
        }

        // ===== فاتورة مبيعات منتجات (PRD-) =====
        public async Task<IActionResult> CreateProduct()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            if (roles.Contains("Employee"))
                return Forbid();

            await PopulateProductDropdowns();
            var sale = new Sale
            {
                InvoiceNumber = await GenerateInvoiceNumber("PRD"),
                SaleDate = DateTime.Now,
                SaleType = "منتجات"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateProduct")]
        public async Task<IActionResult> CreateProductPost(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys,
            string? transactionType, int? employeeRecipientId)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            if (roles.Contains("Employee"))
                return Forbid();

            model.SaleType = "منتجات";

            // ===== استهلاك موظف =====
            if (transactionType == "استهلاك")
            {
                if (!employeeRecipientId.HasValue)
                {
                    TempData["Error"] = "يرجى اختيار الموظف";
                    await PopulateProductDropdowns();
                    return View(model);
                }
                if (itemNames == null || itemNames.Length == 0)
                {
                    TempData["Error"] = "يرجى اختيار منتج واحد على الأقل";
                    await PopulateProductDropdowns();
                    return View(model);
                }

                // التحقق من الكميات أولاً قبل الخصم
                for (int i = 0; i < itemNames.Length; i++)
                {
                    if (string.IsNullOrEmpty(itemNames[i])) continue;
                    var id = itemIds?[i] ?? 0;
                    var qty = itemQtys?[i] ?? 1;
                    if (id <= 0) continue;
                    var product = await _context.Products.FindAsync(id);
                    if (product == null || product.StockQuantity < qty)
                    {
                        var available = product?.StockQuantity ?? 0;
                        TempData["Error"] = $"لا يوجد مخزون كافٍ للمنتج «{itemNames[i]}». المتاح: {available}، المطلوب: {qty}";
                        await PopulateProductDropdowns();
                        return View(model);
                    }
                }

                for (int i = 0; i < itemNames.Length; i++)
                {
                    if (string.IsNullOrEmpty(itemNames[i])) continue;
                    var qty = itemQtys?[i] ?? 1;
                    var price = itemPrices?[i] ?? 0;
                    var id = itemIds?[i] ?? 0;
                    if (id <= 0) continue;

                    var product = await _context.Products.FindAsync(id);
                    if (product != null)
                    {
                        product.StockQuantity -= qty;
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = id,
                            MovementType = "استهلاك",
                            Quantity = qty,
                            UnitPrice = price,
                            EmployeeId = employeeRecipientId,
                            Notes = model.Notes,
                            MovementDate = DateTime.Today
                        });
                    }
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تسجيل الاستهلاك بنجاح";
                return RedirectToAction("Movements", "Inventory");
            }

            // ===== بيع لعميل =====
            if (ModelState.IsValid)
            {
                // التحقق من الكميات أولاً قبل إنشاء الفاتورة
                if (itemNames != null)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (string.IsNullOrEmpty(itemNames[i])) continue;
                        var id = itemIds?[i] ?? 0;
                        var qty = itemQtys?[i] ?? 1;
                        if (id <= 0) continue;
                        var product = await _context.Products.FindAsync(id);
                        if (product == null || product.StockQuantity < qty)
                        {
                            var available = product?.StockQuantity ?? 0;
                            TempData["Error"] = $"لا يوجد مخزون كافٍ للمنتج «{itemNames[i]}». المتاح: {available}، المطلوب: {qty}";
                            await PopulateProductDropdowns();
                            return View(model);
                        }
                    }
                }

                model.TotalAmount = 0;
                model.SaleDate = DateTime.Now;
                _context.Sales.Add(model);
                await _context.SaveChangesAsync();

                if (itemNames != null)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (string.IsNullOrEmpty(itemNames[i])) continue;
                        var qty = itemQtys?[i] ?? 1;
                        var price = itemPrices?[i] ?? 0;
                        var id = itemIds?[i] ?? 0;
                        var item = new SaleItem
                        {
                            SaleId = model.Id,
                            ItemName = itemNames[i],
                            Quantity = qty,
                            Price = price,
                            Total = qty * price
                        };
                        if (id > 0)
                        {
                            item.ProductId = id;
                            var product = await _context.Products.FindAsync(id);
                            if (product != null)
                                product.StockQuantity -= qty;
                        }
                        _context.SaleItems.Add(item);
                        model.TotalAmount += item.Total;
                    }
                }
                model.NetAmount = model.TotalAmount - model.Discount;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"تم إنشاء فاتورة المنتجات {model.InvoiceNumber} بنجاح";
                return RedirectToAction(nameof(PrintInvoice), new { id = model.Id });
            }
            await PopulateProductDropdowns();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems).ThenInclude(i => i.Service)
                .Include(s => s.SaleItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        public async Task<IActionResult> PrintInvoice(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.DebtEmployee)
                .Include(s => s.SaleItems).ThenInclude(i => i.Service)
                .Include(s => s.SaleItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _context.Sales.Include(s => s.SaleItems).FirstOrDefaultAsync(s => s.Id == id);
            if (sale != null)
            {
                foreach (var item in sale.SaleItems.Where(i => i.ProductId != null))
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                        product.StockQuantity += item.Quantity;
                }
                sale.Status = "ملغي";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إلغاء الفاتورة";
            }
            return RedirectToAction(nameof(Index));
        }

        // ===== Helpers =====

        private async Task<string> GenerateInvoiceNumber(string prefix)
        {
            var last = await _context.Sales
                .Where(s => s.InvoiceNumber.StartsWith(prefix + "-"))
                .OrderByDescending(s => s.Id)
                .Select(s => s.InvoiceNumber)
                .FirstOrDefaultAsync();

            int seq = 1;
            if (last != null)
            {
                var parts = last.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var n))
                    seq = n + 1;
            }
            return $"{prefix}-{seq:D4}";
        }

        private async Task PopulateDeptDropdowns(string dept, ApplicationUser? user, string role)
        {
            // العملاء
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");

            // الخدمات مصنّفة حسب القسم
            ViewBag.ServiceCategories = await _context.ServiceCategories
                .Include(c => c.Services.Where(s => s.IsActive))
                .Where(c => c.IsActive && c.Department == dept)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // الموظفون حسب الدور
            bool isEmployee = role == "Employee";
            ViewBag.IsEmployee = isEmployee;
            ViewBag.LinkedEmployeeId = user?.LinkedEmployeeId;

            var empQuery = _context.Employees.Where(e => e.IsActive && e.DepartmentNav!.Name == dept);

            if (isEmployee && user?.LinkedEmployeeId.HasValue == true)
                empQuery = empQuery.Where(e => e.Id == user.LinkedEmployeeId!.Value);

            var empList = await empQuery.ToListAsync();

            // Today's queue positions — join on Departments to avoid OPENJSON / '$' issue (EF Core 8 + SQL Server)
            var today = DateTime.Today;
            var todayQueue = await (
                from a in _context.Attendances
                join e in _context.Employees on a.EmployeeId equals e.Id
                join d in _context.Departments on e.DepartmentId equals d.Id
                where a.AttendanceDate == today && a.QueuePosition != null && d.Name == dept && e.IsActive
                select new { a.EmployeeId, QueuePos = (int)a.QueuePosition! }
            ).ToDictionaryAsync(x => x.EmployeeId, x => x.QueuePos);

            // Today's check-in times
            var checkInRows = await (
                from a in _context.Attendances
                join e in _context.Employees on a.EmployeeId equals e.Id
                join d in _context.Departments on e.DepartmentId equals d.Id
                where a.AttendanceDate == today && d.Name == dept && e.IsActive && a.CheckIn != null
                select new { a.EmployeeId, a.CheckIn }
            ).ToListAsync();
            ViewBag.EmployeeCheckInTimes = checkInRows
                .GroupBy(x => x.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First().CheckIn);

            // Sort by queue position (present employees first), then unqueued alphabetically
            var sortedEmployees = empList
                .OrderBy(e => todayQueue.ContainsKey(e.Id) ? 0 : 1)
                .ThenBy(e => todayQueue.TryGetValue(e.Id, out var q) ? q : int.MaxValue)
                .ThenBy(e => e.FullName)
                .ToList();

            ViewBag.Employees = sortedEmployees;
            ViewBag.EmployeeQueuePositions = todayQueue;

            ViewBag.AllEmployees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }

        private async Task PopulateProductDropdowns()
        {
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.AllEmployees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }

        private async Task<IActionResult> SaveServiceInvoice(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys, string dept)
        {
            if (ModelState.IsValid)
            {
                model.TotalAmount = 0;
                model.SaleDate = DateTime.Now;
                _context.Sales.Add(model);
                await _context.SaveChangesAsync();

                if (itemNames != null)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (string.IsNullOrEmpty(itemNames[i])) continue;
                        var qty = itemQtys?[i] ?? 1;
                        var price = itemPrices?[i] ?? 0;
                        var id = itemIds?[i] ?? 0;
                        var item = new SaleItem
                        {
                            SaleId = model.Id,
                            ItemName = itemNames[i],
                            Quantity = qty,
                            Price = price,
                            Total = qty * price
                        };
                        if (id > 0) item.ServiceId = id;
                        _context.SaleItems.Add(item);
                        model.TotalAmount += item.Total;
                    }
                }
                model.NetAmount = model.TotalAmount - model.Discount;
                await _context.SaveChangesAsync();

                // Move the employee to the end of today's queue after serving a customer
                if (model.EmployeeId.HasValue)
                {
                    var today2 = DateTime.Today;
                    var todayAttendance = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EmployeeId == model.EmployeeId.Value
                                               && a.AttendanceDate == today2);
                    if (todayAttendance != null)
                    {
                        var maxPos = await (
                            from a in _context.Attendances
                            join e in _context.Employees on a.EmployeeId equals e.Id
                            join d in _context.Departments on e.DepartmentId equals d.Id
                            where a.AttendanceDate == today2 && a.QueuePosition != null && d.Name == dept
                            select a.QueuePosition
                        ).MaxAsync();
                        todayAttendance.QueuePosition = (maxPos ?? 0) + 1;
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["Success"] = $"تم إنشاء الفاتورة {model.InvoiceNumber} بنجاح";

                // Send email notification (awaited but exception-safe)
                var currentUser = await _userManager.GetUserAsync(User);
                var cashierName = currentUser?.FullName ?? User.Identity?.Name ?? "—";
                var saleWithItems = await _context.Sales
                    .Include(s => s.Employee)
                    .Include(s => s.SaleItems)
                    .FirstAsync(s => s.Id == model.Id);
                await _emailService.SendInvoiceNotificationAsync(saleWithItems, cashierName);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, invoiceId = model.Id });

                return RedirectToAction(nameof(PrintInvoice), new { id = model.Id });
            }

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            await PopulateDeptDropdowns(dept, user, roles.FirstOrDefault() ?? "");

            return dept == "حلاقة" ? View("CreateBarber", model) : View("CreateMassage", model);
        }
    }
}