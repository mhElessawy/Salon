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
    public class CustodyController : Controller
    {
        private static readonly string[] ArabicMonths = { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                                                            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDailyClosureService _closure;
        private readonly IPermissionService _perms;

        public CustodyController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager, IDailyClosureService closure, IPermissionService perms)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _closure = closure;
            _perms = perms;
        }

        private async Task<bool> IsManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager");
        }

        public async Task<IActionResult> Index(int? employeeId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.Custodies
                .Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(c => c.Allocations)
                .Include(c => c.InvoicePayments)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(c => (c.Employee!.RevenueDepartment ?? c.Employee!.DepartmentNav!.Name) == userDept);

            if (!isManager && linkedEmpId.HasValue)
                query = query.Where(c => c.EmployeeId == linkedEmpId.Value);

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            var custodies = await query.OrderByDescending(c => c.CustodyDate).ThenByDescending(c => c.CreatedAt).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => (e.RevenueDepartment ?? e.DepartmentNav!.Name) == userDept);
            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            // رصيد كل موظف موحَّد (انظر CustodyPoolCalculator) — الإيداعات المفتوحة فقط تُحسب ضمن
            // الرصيد المتاح، أما المسوَّاة (مرحَّلة/مقفلة) فتبقى في سجل الإيداعات تحت للمراجعة فقط
            var summaries = new List<EmployeeCustodySummary>();
            foreach (var group in custodies.GroupBy(c => c.EmployeeId))
            {
                var open = group.Where(c => c.SettlementType == null).ToList();
                var reserved = await CustodyPoolCalculator.GetReservedAsync(_context, group.Key);
                summaries.Add(new EmployeeCustodySummary
                {
                    EmployeeId = group.Key,
                    EmployeeName = group.First().Employee?.FullName ?? "-",
                    TotalDeposited = open.PoolDeposited(),
                    TotalSpent = open.PoolSpent(),
                    TotalReserved = reserved,
                    OpenDepositsCount = open.Count
                });
            }
            ViewBag.EmployeeSummaries = summaries.OrderBy(s => s.EmployeeName).ToList();

            ViewBag.EmployeeId = employeeId;
            ViewBag.IsManager = isManager;
            ViewBag.TotalCash = custodies.Where(c => c.PaymentMethod == "نقدي").Sum(c => c.Amount);
            ViewBag.TotalLink = custodies.Where(c => c.PaymentMethod == "لينك").Sum(c => c.Amount);
            ViewBag.Total = custodies.Sum(c => c.Amount);
            ViewBag.TotalSpent = summaries.Sum(s => s.TotalSpent);
            ViewBag.TotalReserved = summaries.Sum(s => s.TotalReserved);
            ViewBag.TotalRemaining = summaries.Sum(s => s.RemainingAmount);
            return View(custodies);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسليم عهدة";
                return RedirectToAction(nameof(Index));
            }

            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => (e.RevenueDepartment ?? e.DepartmentNav!.Name) == userDept);

            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(new Custody { CustodyDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Custody model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسليم عهدة";
                return RedirectToAction(nameof(Index));
            }

            if (model.PaymentMethod != "نقدي" && model.PaymentMethod != "لينك")
                model.PaymentMethod = "نقدي";

            if (ModelState.IsValid)
            {
                var emp = await _context.Employees.Include(e => e.DepartmentNav).FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

                // العهدة لا تُنشئ مصروفاً ولا تخصم من الصندوق — هي مبلغ منفصل تحت عهدة الموظف
                // فقط، يظهر في شاشة العهد وملخص BarberDaily كرقم معلوماتي. لو للموظف عهد مفتوحة
                // أخرى فرصيده الموحَّد يزيد بمجموعها كلها (انظر CustodyPoolCalculator).
                model.CreatedAt = DateTime.Now;
                _context.Custodies.Add(model);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Add", "Custody",
                    $"تسليم عهدة للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بمبلغ {model.Amount:N3} KD | طريقة التسليم: {model.PaymentMethod}",
                    model.Id);

                TempData["Success"] = "تم تسليم العهدة بنجاح";
                return RedirectToAction(nameof(Index));
            }

            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => (e.RevenueDepartment ?? e.DepartmentNav!.Name) == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف العهد";
                return RedirectToAction(nameof(Index));
            }

            var custody = await _context.Custodies
                .Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(c => c.Allocations)
                .Include(c => c.InvoicePayments)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (custody != null)
            {
                var custodyDept = custody.Employee?.RevenueDepartment ?? custody.Employee?.DepartmentNav?.Name;
                if (await _closure.IsDateLockedAsync(custody.CustodyDate, custodyDept))
                {
                    TempData["Error"] = "لا يمكن حذف عهدة تخص يومية معتمدة — استخدم صلاحية إعادة فتح اليومية";
                    return RedirectToAction(nameof(Index));
                }

                if (custody.Allocations.Any() || custody.InvoicePayments.Any())
                {
                    TempData["Error"] = "لا يمكن حذف عهدة لها مبالغ مصروفة بالفعل — راجع طلبات الشراء/دفعات الموردين المرتبطة أولاً";
                    return RedirectToAction(nameof(Index));
                }

                // حذف إيداع مفتوح لسه ملوش صرف — لازم نتأكد إن باقي إيداعات الموظف المفتوحة
                // لسه كافية لتغطية أي طلبات شراء قيد المعالجة (محجوزة) قبل ما نحذفه
                if (custody.SettlementType == null)
                {
                    var openCustodies = await CustodyPoolCalculator.GetOpenCustodiesAsync(_context, custody.EmployeeId);
                    var reserved = await CustodyPoolCalculator.GetReservedAsync(_context, custody.EmployeeId);
                    decimal remainingWithoutThis = openCustodies.Where(c => c.Id != custody.Id).PoolRemaining();
                    if (remainingWithoutThis < reserved - 0.0005m)
                    {
                        TempData["Error"] = "لا يمكن حذف هذا الإيداع — يوجد طلبات شراء قيد المعالجة لهذا الموظف تعتمد على رصيده";
                        return RedirectToAction(nameof(Index));
                    }
                }

                string empName = custody.Employee?.FullName ?? custody.EmployeeId.ToString();
                decimal amount = custody.Amount;

                if (custody.ExpenseId.HasValue)
                {
                    var linkedExpense = await _context.Expenses.FindAsync(custody.ExpenseId.Value);
                    if (linkedExpense != null)
                        _context.Expenses.Remove(linkedExpense);
                }

                _context.Custodies.Remove(custody);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "Custody",
                    $"حذف عهدة الموظف: {empName} بمبلغ {amount:N3} KD",
                    id);

                TempData["Success"] = "تم حذف العهدة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        // تسوية رصيد عهدة موظف — إما ترحيل المتبقي كرصيد افتتاحي لعهدة جديدة، أو إقفاله نهائياً.
        // في الحالتين لا يتأثر رصيد الصندوق إطلاقاً — العهدة أصلاً لم تُخصم منه (انظر Custody.cs)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settle(int employeeId, string settlementType)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسوية العهد";
                return RedirectToAction(nameof(Index));
            }

            if (settlementType != Custody.SettlementTypes.RolledOver && settlementType != Custody.SettlementTypes.Closed)
            {
                TempData["Error"] = "نوع التسوية غير صحيح";
                return RedirectToAction(nameof(Index));
            }

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                TempData["Error"] = "الموظف غير موجود";
                return RedirectToAction(nameof(Index));
            }

            var openCustodies = await CustodyPoolCalculator.GetOpenCustodiesAsync(_context, employeeId);
            if (openCustodies.Count == 0)
            {
                TempData["Error"] = "لا توجد عهدة مفتوحة لهذا الموظف لتسويتها";
                return RedirectToAction(nameof(Index));
            }

            decimal remaining = openCustodies.PoolRemaining();
            if (remaining <= 0.0005m)
            {
                TempData["Error"] = "لا يوجد رصيد متبقٍ لهذا الموظف لتسويته";
                return RedirectToAction(nameof(Index));
            }

            var reserved = await CustodyPoolCalculator.GetReservedAsync(_context, employeeId);
            if (reserved > 0.0005m)
            {
                TempData["Error"] = "يوجد طلبات شراء قيد المعالجة لهذا الموظف — يجب اعتمادها أو رفضها أولاً قبل تسوية العهدة";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            foreach (var c in openCustodies)
            {
                c.SettlementType = settlementType;
                c.SettledAt = now;
            }

            string message;
            if (settlementType == Custody.SettlementTypes.RolledOver)
            {
                var earliestDate = openCustodies.Min(c => c.CustodyDate);
                var fromLabel = $"{ArabicMonths[earliestDate.Month]} {earliestDate.Year}";
                var toLabel = $"{ArabicMonths[DateTime.Today.Month]} {DateTime.Today.Year}";

                var newCustody = new Custody
                {
                    EmployeeId = employeeId,
                    Amount = remaining,
                    CustodyDate = DateTime.Today,
                    PaymentMethod = "نقدي",
                    Reason = "رصيد مرحّل",
                    Notes = $"تم ترحيله من عهد {fromLabel} عند التسوية بتاريخ {now:yyyy/MM/dd}",
                    IsOpeningBalance = true,
                    CreatedAt = now
                };
                _context.Custodies.Add(newCustody);

                message = $"تم ترحيل رصيد عهدة {employee.FullName} بقيمة {remaining:N3} د.ك من {fromLabel} إلى {toLabel}";
            }
            else
            {
                message = $"تم إقفال رصيد عهدة الموظف {employee.FullName} بقيمة {remaining:N3} د.ك";
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Settle", "Custody", message, employeeId);

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        // كشف حساب عهدة موظف: كل إيداع وكل صرف بتاريخه ووقته مع الرصيد بعد كل حركة، بالإضافة
        // لتفاصيل أي الإيداعات المفتوحة لسه فيها رصيد متبقٍ حالياً
        public async Task<IActionResult> Ledger(int employeeId)
        {
            var employee = await _context.Employees.Include(e => e.DepartmentNav).FirstOrDefaultAsync(e => e.Id == employeeId);
            if (employee == null) return RedirectToAction(nameof(Index));

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;
            var empDept = employee.RevenueDepartment ?? employee.DepartmentNav?.Name;

            bool deptAllowed = (userDept != "حلاقة" && userDept != "مساج") || empDept == userDept;
            bool employeeAllowed = isManager || !linkedEmpId.HasValue || linkedEmpId.Value == employeeId;
            if (!deptAllowed || !employeeAllowed)
                return Forbid();

            var allCustodies = await _context.Custodies
                .Include(c => c.Allocations).ThenInclude(a => a.PurchaseRequest)
                .Include(c => c.InvoicePayments).ThenInclude(ip => ip.SupplierInvoice)
                .Where(c => c.EmployeeId == employeeId)
                .ToListAsync();

            var entries = new List<CustodyLedgerEntry>();

            foreach (var c in allCustodies)
            {
                entries.Add(new CustodyLedgerEntry
                {
                    Date = c.CreatedAt,
                    Type = c.IsOpeningBalance ? "رصيد افتتاحي مرحّل" : "إيداع عهدة",
                    Description = c.Reason ?? (c.IsOpeningBalance ? "رصيد مرحّل من عهد سابقة" : "تسليم عهدة"),
                    InAmount = c.Amount
                });

                foreach (var a in c.Allocations)
                {
                    entries.Add(new CustodyLedgerEntry
                    {
                        Date = a.PurchaseRequest?.ReviewedAt ?? a.CreatedAt,
                        Type = "شراء من العهدة",
                        Description = $"طلب شراء: {a.PurchaseRequest?.ItemsSummary ?? "-"} (فاتورة {a.PurchaseRequest?.InvoiceNumber ?? "-"})",
                        OutAmount = a.Amount
                    });
                }

                foreach (var ip in c.InvoicePayments)
                {
                    entries.Add(new CustodyLedgerEntry
                    {
                        Date = ip.PaymentDate,
                        Type = "دفعة فاتورة مورد",
                        Description = $"فاتورة {ip.SupplierInvoice?.InvoiceNumber ?? "-"}",
                        OutAmount = ip.Amount
                    });
                }

                if (c.SettlementType != null && c.SettledAt.HasValue)
                {
                    decimal settledRemaining = c.Amount - c.Allocations.Sum(a => a.Amount) - c.InvoicePayments.Sum(ip => ip.Amount);
                    if (settledRemaining > 0.0005m)
                    {
                        entries.Add(new CustodyLedgerEntry
                        {
                            Date = c.SettledAt.Value,
                            Type = c.SettlementType == Custody.SettlementTypes.RolledOver ? "ترحيل رصيد" : "إقفال رصيد",
                            Description = c.SettlementType == Custody.SettlementTypes.RolledOver
                                ? "ترحيل المتبقي كرصيد افتتاحي لعهدة جديدة"
                                : "إقفال المتبقي — لا يؤثر على الصندوق",
                            OutAmount = settledRemaining
                        });
                    }
                }
            }

            entries = entries.OrderBy(e => e.Date).ToList();
            decimal running = 0;
            foreach (var e in entries)
            {
                running += e.InAmount - e.OutAmount;
                e.Balance = running;
            }

            var openCustodies = allCustodies.Where(c => c.SettlementType == null).OrderBy(c => c.CustodyDate).ToList();
            var reserved = await CustodyPoolCalculator.GetReservedAsync(_context, employeeId);

            var model = new CustodyLedgerViewModel
            {
                Employee = employee,
                Entries = entries,
                OpenDeposits = openCustodies,
                TotalDeposited = openCustodies.PoolDeposited(),
                TotalSpent = openCustodies.PoolSpent(),
                TotalReserved = reserved
            };

            return View(model);
        }

        public async Task<IActionResult> Report(DateTime? dateFrom, DateTime? dateTo, int? employeeId, string? paymentMethod)
        {
            if (!await _perms.HasAccessAsync("ReportCustody"))
                return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.Custodies
                .Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(c => c.Allocations)
                .Include(c => c.InvoicePayments)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(c => (c.Employee!.RevenueDepartment ?? c.Employee!.DepartmentNav!.Name) == userDept);

            if (!isManager && linkedEmpId.HasValue)
            {
                query = query.Where(c => c.EmployeeId == linkedEmpId.Value);
                employeeId = linkedEmpId;
            }

            if (dateFrom.HasValue)
                query = query.Where(c => c.CustodyDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(c => c.CustodyDate <= dateTo.Value);

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(paymentMethod))
                query = query.Where(c => c.PaymentMethod == paymentMethod);

            var custodies = await query.OrderByDescending(c => c.CustodyDate).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => (e.RevenueDepartment ?? e.DepartmentNav!.Name) == userDept);
            if (!isManager && linkedEmpId.HasValue)
                empQuery = empQuery.Where(e => e.Id == linkedEmpId.Value);

            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId = employeeId;
            ViewBag.PaymentMethod = paymentMethod;

            // المحجوز مفهوم "حالي" (طلبات قيد المعالجة الآن) وليس مرتبطاً بفترة التقرير، فيُحسب
            // لكل موظف ظاهر في نتيجة التقرير على حدة بدل ما يُحسب من صفوف الإيداعات نفسها
            var reservedByEmployee = new Dictionary<int, decimal>();
            foreach (var empId in custodies.Select(c => c.EmployeeId).Distinct())
                reservedByEmployee[empId] = await CustodyPoolCalculator.GetReservedAsync(_context, empId);
            ViewBag.ReservedByEmployee = reservedByEmployee;

            return View(custodies);
        }
    }

    public class EmployeeCustodySummary
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "-";
        public decimal TotalDeposited { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalReserved { get; set; }
        public int OpenDepositsCount { get; set; }
        public decimal RemainingAmount => TotalDeposited - TotalSpent;
        public decimal AvailableForRequest => RemainingAmount - TotalReserved;
    }

    public class CustodyLedgerEntry
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal InAmount { get; set; }
        public decimal OutAmount { get; set; }
        public decimal Balance { get; set; }
    }

    public class CustodyLedgerViewModel
    {
        public Employee? Employee { get; set; }
        public List<CustodyLedgerEntry> Entries { get; set; } = new();
        public List<Custody> OpenDeposits { get; set; } = new();
        public decimal TotalDeposited { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalReserved { get; set; }
        public decimal RemainingAmount => TotalDeposited - TotalSpent;
        public decimal AvailableForRequest => RemainingAmount - TotalReserved;
    }
}