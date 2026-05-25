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
        // Kuwait is permanently UTC+3 with no DST
        private static readonly TimeZoneInfo _kuwaitTz =
            TimeZoneInfo.CreateCustomTimeZone("Kuwait Standard Time",
                TimeSpan.FromHours(3), "Kuwait Standard Time", "Kuwait Standard Time");

        private static DateTime KuwaitNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _kuwaitTz);
        private static DateTime KuwaitToday => KuwaitNow.Date;

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
            DateTime filterDate = string.IsNullOrEmpty(date) ? KuwaitToday : DateTime.Parse(date);
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

            // Department users see only their own department's invoices
            if (userDept == "مساج")
                query = query.Where(s => s.SaleType == "مساج");
            else if (userDept == "حلاقة")
                query = query.Where(s => s.SaleType == "حلاقة");

            // Employees see only their own invoices
            if (isEmployee && user?.LinkedEmployeeId.HasValue == true)
                query = query.Where(s => s.EmployeeId == user.LinkedEmployeeId!.Value);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(s => s.SaleType == type);

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();

            var activeSales = sales.Where(s => s.Status != "ملغي").ToList();
            var cancelledSales = sales.Where(s => s.Status == "ملغي").ToList();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.FilterType = type;
            ViewBag.TotalSales = activeSales.Sum(s => s.NetAmount);
            ViewBag.TotalCancelled = cancelledSales.Sum(s => s.NetAmount);

            ViewBag.TotalCash = activeSales
                .Where(s => s.PaymentMethod == "كاش")
                .Sum(s => s.NetAmount)
                + activeSales
                .Where(s => s.PaymentMethod == "كي نت و كاش")
                .Sum(s => s.CashAmount ?? 0);

            ViewBag.TotalKnet = activeSales
                .Where(s => s.PaymentMethod == "كي نت")
                .Sum(s => s.NetAmount)
                + activeSales
                .Where(s => s.PaymentMethod == "كي نت و كاش")
                .Sum(s => s.LinkAmount ?? 0);

            ViewBag.TotalEmployeeDebt = activeSales
                .Where(s => s.PaymentMethod == "دين على الموظف")
                .Sum(s => s.NetAmount);

            ViewBag.TotalOwnerDebt = activeSales
                .Where(s => s.PaymentMethod == "دين على صاحب المكان")
                .Sum(s => s.NetAmount);

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
                SaleDate = KuwaitNow,
                SaleType = "حلاقة"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateBarber")]
        public async Task<IActionResult> CreateBarberPost(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys, int? customerPackageId)
        {
            model.SaleType = "حلاقة";
            return await SaveServiceInvoice(model, itemIds, itemNames, itemPrices, itemQtys, "حلاقة", customerPackageId);
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
                SaleDate = KuwaitNow,
                SaleType = "مساج"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateMassage")]
        public async Task<IActionResult> CreateMassagePost(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys, int? customerPackageId)
        {
            model.SaleType = "مساج";
            return await SaveServiceInvoice(model, itemIds, itemNames, itemPrices, itemQtys, "مساج", customerPackageId);
        }

        // ===== فاتورة مبيعات منتجات (PRD-) =====
        public async Task<IActionResult> CreateProduct()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user!);
            if (roles.Contains("Employee") || !string.IsNullOrEmpty(user?.UserDepartment))
                return Forbid();

            await PopulateProductDropdowns();
            var sale = new Sale
            {
                InvoiceNumber = await GenerateInvoiceNumber("PRD"),
                SaleDate = KuwaitNow,
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
            if (roles.Contains("Employee") || !string.IsNullOrEmpty(user?.UserDepartment))
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
                            MovementDate = KuwaitToday
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
                model.SaleDate = KuwaitNow;
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
            // العملاء - فلتر حسب القسم
            var customersQuery = _context.Customers.Where(c => c.IsActive);
            if (dept == "مساج" || dept == "حلاقة")
                customersQuery = customersQuery.Where(c => c.Department == dept);
            ViewBag.Customers = new SelectList(
                await customersQuery.OrderBy(c => c.FullName).ToListAsync(),
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

            // Today's date for attendance checks
            var today = KuwaitToday;

            // Only employees who are currently present (checked in, NOT checked out)
            var presentEmpIds = await _context.Attendances
                .Where(a => a.AttendanceDate == today && a.CheckIn != null && a.CheckOut == null)
                .Select(a => a.EmployeeId)
                .ToListAsync();

            var empQuery = _context.Employees.Where(e => e.IsActive && e.DepartmentNav!.Name == dept
                                                         && presentEmpIds.Contains(e.Id));

            if (isEmployee && user?.LinkedEmployeeId.HasValue == true)
                empQuery = empQuery.Where(e => e.Id == user.LinkedEmployeeId!.Value);

            var empList = await empQuery.ToListAsync();

            // Today's queue positions — only for employees who haven't checked out
            var todayQueue = await (
                from a in _context.Attendances
                join e in _context.Employees on a.EmployeeId equals e.Id
                join d in _context.Departments on e.DepartmentId equals d.Id
                where a.AttendanceDate == today && a.QueuePosition != null && d.Name == dept
                      && e.IsActive && a.CheckOut == null
                select new { a.EmployeeId, QueuePos = (int)a.QueuePosition! }
            ).ToDictionaryAsync(x => x.EmployeeId, x => x.QueuePos);

            // Today's check-in times — only for employees who haven't checked out
            var checkInRows = await (
                from a in _context.Attendances
                join e in _context.Employees on a.EmployeeId equals e.Id
                join d in _context.Departments on e.DepartmentId equals d.Id
                where a.AttendanceDate == today && d.Name == dept && e.IsActive
                      && a.CheckIn != null && a.CheckOut == null
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

            // Feature permissions: Discount & CustomerDebt
            var userId = user?.Id ?? "";
            var featurePerms = await _context.UserPermissions
                .Where(p => p.UserId == userId)
                .ToListAsync();

            if (!featurePerms.Any())
            {
                ViewBag.CanDiscount = true;
                ViewBag.CanCustomerDebt = true;
            }
            else
            {
                var pd = featurePerms.ToDictionary(p => p.Module, p => p.CanAccess);
                ViewBag.CanDiscount = !pd.ContainsKey("Discount") || pd["Discount"];
                ViewBag.CanCustomerDebt = !pd.ContainsKey("CustomerDebt") || pd["CustomerDebt"];
            }
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

        // ===== API: باقات العميل النشطة للكاشير =====
        [HttpGet]
        public async Task<IActionResult> GetCustomerPackagesForSale(int customerId, string dept)
        {
            var pkgs = await _context.CustomerPackages
                .Include(cp => cp.ServicePackage)
                .Where(cp => cp.CustomerId == customerId
                          && cp.IsActive
                          && cp.RemainingSessions > 0
                          && (cp.ExpiryDate == null || cp.ExpiryDate >= DateTime.Today))
                .Select(cp => new
                {
                    id            = cp.Id,
                    name          = cp.ServicePackage!.NameAr,
                    remaining     = cp.RemainingSessions,
                    total         = cp.TotalSessions,
                    expiry        = cp.ExpiryDate.HasValue
                                        ? cp.ExpiryDate.Value.ToString("yyyy/MM/dd")
                                        : "—"
                })
                .ToListAsync();
            return Json(pkgs);
        }

        private async Task<IActionResult> SaveServiceInvoice(
            Sale model, int[]? itemIds, string[]? itemNames,
            decimal[]? itemPrices, int[]? itemQtys, string dept,
            int? customerPackageId = null)
        {
            if (ModelState.IsValid)
            {
                // Server-side permission enforcement for Discount and CustomerDebt
                var currentUser = await _userManager.GetUserAsync(User);
                var serverPerms = await _context.UserPermissions
                    .Where(p => p.UserId == currentUser!.Id)
                    .ToListAsync();
                if (serverPerms.Any())
                {
                    var pd = serverPerms.ToDictionary(p => p.Module, p => p.CanAccess);
                    if (pd.TryGetValue("Discount", out var canDisc) && !canDisc)
                        model.Discount = 0;
                    if (pd.TryGetValue("CustomerDebt", out var canDebt) && !canDebt && model.PaymentMethod == "دين على العميل")
                        model.PaymentMethod = "كاش";
                }

                model.TotalAmount = 0;
                model.SaleDate = KuwaitNow;
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
                    var today2 = KuwaitToday;
                    // Only move the employee in queue if they haven't checked out yet
                    var todayAttendance = await _context.Attendances
                        .FirstOrDefaultAsync(a => a.EmployeeId == model.EmployeeId.Value
                                               && a.AttendanceDate == today2
                                               && a.CheckOut == null);
                    if (todayAttendance != null)
                    {
                        // Calculate max position only among employees still present (not checked out)
                        var maxPos = await (
                            from a in _context.Attendances
                            join e in _context.Employees on a.EmployeeId equals e.Id
                            join d in _context.Departments on e.DepartmentId equals d.Id
                            where a.AttendanceDate == today2 && a.QueuePosition != null
                                  && d.Name == dept && a.CheckOut == null
                            select a.QueuePosition
                        ).MaxAsync();
                        todayAttendance.QueuePosition = (maxPos ?? 0) + 1;
                        await _context.SaveChangesAsync();
                    }
                }

                // ── خصم جلسة من باقة العميل ──
                if (customerPackageId.HasValue && customerPackageId.Value > 0)
                {
                    var cp = await _context.CustomerPackages
                        .FirstOrDefaultAsync(x => x.Id == customerPackageId.Value
                                               && x.IsActive && x.RemainingSessions > 0);
                    if (cp != null)
                    {
                        cp.RemainingSessions--;
                        if (cp.RemainingSessions == 0) cp.IsActive = false;

                        _context.CustomerPackageTransactions.Add(new CustomerPackageTransaction
                        {
                            CustomerPackageId = cp.Id,
                            UsedDate          = KuwaitNow,
                            EmployeeId        = model.EmployeeId,
                            Notes             = $"فاتورة رقم {model.InvoiceNumber}"
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["Success"] = $"تم إنشاء الفاتورة {model.InvoiceNumber} بنجاح";

                // Send email notification (fire-and-forget, never blocks the response)
                var currentUser1 = await _userManager.GetUserAsync(User);
                var cashierName = currentUser1?.FullName ?? User.Identity?.Name ?? "—";
                var saleWithItems = await _context.Sales
                    .Include(s => s.Employee)
                    .Include(s => s.SaleItems)
                    .FirstAsync(s => s.Id == model.Id);
                _ = Task.Run(() => _emailService.SendInvoiceNotificationAsync(saleWithItems, cashierName));

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