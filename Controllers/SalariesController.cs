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
    public class SalariesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly string[] ArabicMonths = { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                               "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

        public SalariesController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        public async Task<IActionResult> Details(int id)
        {
            var salary = await _context.Salaries
                .Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (salary == null) return NotFound();

            var rangeStart = new DateTime(salary.Year, salary.Month, 1);
            var rangeEnd = rangeStart.AddMonths(1);

            var allSales = await _context.Sales
                .Where(s => s.EmployeeId == salary.EmployeeId
                         && s.SaleDate >= rangeStart && s.SaleDate < rangeEnd)
                .OrderBy(s => s.SaleDate)
                .ToListAsync();

            var activeSales = allSales.Where(s => s.Status != "ملغي").ToList();
            var cancelledSales = allSales.Where(s => s.Status == "ملغي").ToList();

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            ViewBag.ActiveSales = activeSales;
            ViewBag.CancelledSales = cancelledSales;
            ViewBag.TotalSales = activeSales.Sum(s => s.NetAmount);
            ViewBag.CashTotal = activeSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            ViewBag.KnetTotal = activeSales.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            ViewBag.EmployeeDebt = activeSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            ViewBag.CustomerDebt = activeSales.Where(s => s.PaymentMethod == "دين على العميل").Sum(s => s.NetAmount);
            ViewBag.OwnerDebt = activeSales.Where(s => s.PaymentMethod == "دين على الإدارة").Sum(s => s.NetAmount);

            return View(salary);
        }

        public async Task<IActionResult> Index(int? year, int? month, int? employeeId)
        {
            int y = year ?? DateTime.Today.Year;

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var salaryRoles = await _userManager.GetRolesAsync(currentUser!);
            bool salaryIsManager = salaryRoles.Contains("Admin") || salaryRoles.Contains("Manager");
            bool salaryIsEmployee = !salaryIsManager && salaryRoles.Contains("Employee");
            int? salaryLinkedEmpId = currentUser?.LinkedEmployeeId;

            // Scope to department-specific employees when user has a department restriction
            List<int>? deptEmpIds = null;
            if (userDept == "حلاقة" || userDept == "مساج")
            {
                deptEmpIds = await _context.Employees
                    .Include(e => e.DepartmentNav)
                    .Where(e => e.IsActive && e.DepartmentNav!.Name == userDept)
                    .Select(e => e.Id)
                    .ToListAsync();
            }

            // Employees see only their own salary record
            if (salaryIsEmployee && salaryLinkedEmpId.HasValue)
                deptEmpIds = new List<int> { salaryLinkedEmpId.Value };

            var query = _context.Salaries.Include(s => s.Employee).Where(s => s.Year == y);

            if (deptEmpIds != null)
                query = query.Where(s => deptEmpIds.Contains(s.EmployeeId));

            if (month.HasValue)
                query = query.Where(s => s.Month == month.Value);

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId.Value);

            var salaries = await query.OrderBy(s => s.Month).ThenBy(s => s.Employee!.FullName).ToListAsync();

            int minYear = await _context.Salaries.AnyAsync() ? await _context.Salaries.MinAsync(s => s.Year) : DateTime.Today.Year;
            var years = Enumerable.Range(minYear, DateTime.Today.Year - minYear + 2).ToList();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (deptEmpIds != null)
                empQuery = empQuery.Where(e => deptEmpIds.Contains(e.Id));

            ViewBag.Year = y;
            ViewBag.Month = month;
            ViewBag.EmployeeId = employeeId;
            ViewBag.Years = years;
            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName }).ToList();

            return View(salaries);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(new Salary { Year = DateTime.Today.Year, Month = DateTime.Today.Month });
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeInfo(int employeeId, int month, int year,
            decimal? basicSalary = null, decimal? allowances = null, decimal? deductions = null)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var existing = await _context.Salaries
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.Month == month && s.Year == year);

            string monthLabel = ArabicMonths[month];
            var result = await SalarySettlementCalculator.ComputeAsync(_context, employee, month, year,
                basicSalary ?? employee.BasicSalary, allowances ?? 0, deductions ?? 0, monthLabel);

            return Json(new
            {
                basicSalary = result.BasicSalary,
                commission = result.TargetReached ? result.CommissionAfterTargetRate : result.NormalCommissionRate,
                sales = result.ActiveSales.Select(s => new
                {
                    invoiceNumber = s.InvoiceNumber,
                    saleDate = s.SaleDate.ToString("yyyy-MM-dd"),
                    netAmount = s.NetAmount,
                    paymentMethod = s.PaymentMethod,
                    giftForEmployee = s.GiftForEmployee ?? 0
                }),
                cancelledSales = result.CancelledSales.Select(s => new
                {
                    invoiceNumber = s.InvoiceNumber,
                    saleDate = s.SaleDate.ToString("yyyy-MM-dd"),
                    netAmount = s.NetAmount,
                    paymentMethod = s.PaymentMethod
                }),
                totalSalesAmount = result.TotalSalesAmount,
                commissionAmount = result.CommissionAmount,
                cashTotal = result.CashTotal,
                knetTotal = result.KnetTotal,
                employeeDebtTotal = result.EmployeeDebtTotal,
                customerDebtTotal = result.CustomerDebtTotal,
                totalGifts = result.GiftAmount,
                totalHadiya = result.HadiyaAmount,
                totalEntitlements = result.TotalEntitlements,
                carriedAdvanceBalance = result.CarriedAdvanceBalance,
                newAdvancesAmount = result.NewAdvancesAmount,
                totalAdvanceDue = result.TotalAdvanceDue,
                availableForAdvanceRepayment = result.AvailableForAdvanceRepayment,
                advanceDeducted = result.AdvanceDeducted,
                remainingAdvanceCarried = result.RemainingAdvanceCarried,
                netSalary = result.NetSalary,
                autoNote = result.AutoNote,
                alreadyExists = existing != null,
                existingStatus = existing?.Status,
                salesTarget = employee.SalesTarget,
                targetReached = result.TargetReached,
                normalCommission = employee.Commission,
                commissionAfterTarget = employee.CommissionAfterTarget
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Salary model)
        {
            if (ModelState.IsValid)
            {
                bool alreadyExists = await _context.Salaries
                    .AnyAsync(s => s.EmployeeId == model.EmployeeId && s.Month == model.Month && s.Year == model.Year);

                if (alreadyExists)
                {
                    ModelState.AddModelError("", "تمت معالجة راتب هذا الموظف لهذا الشهر مسبقاً");
                    var cu2 = await _userManager.GetUserAsync(User);
                    var ud2 = cu2?.UserDepartment;
                    var eq2 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
                    if (ud2 == "حلاقة" || ud2 == "مساج") eq2 = eq2.Where(e => e.DepartmentNav!.Name == ud2);
                    ViewBag.Employees = new SelectList(await eq2.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                var employee = await _context.Employees.FindAsync(model.EmployeeId);
                if (employee == null)
                {
                    ModelState.AddModelError("", "الموظف غير موجود");
                    var eq0 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
                    ViewBag.Employees = new SelectList(await eq0.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                string monthLabel = ArabicMonths[model.Month];

                // كل الأرقام المرتبطة بمصادر أخرى (عمولة/دين/سلف) تُحسب هنا من سجلاتها الأصلية
                // مباشرة وقت الحفظ، ولا يُعتمد على أي قيمة مُرسَلة من الشاشة لهذه الحقول.
                var result = await SalarySettlementCalculator.ComputeAsync(_context, employee, model.Month, model.Year,
                    model.BasicSalary, model.Allowances, model.Deductions, monthLabel);

                if (result.NetSalary < 0)
                {
                    ModelState.AddModelError("", "لا يمكن حفظ الراتب لأن الخصومات والالتزامات أكبر من إجمالي الاستحقاقات");
                    var eqNeg = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
                    ViewBag.Employees = new SelectList(await eqNeg.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                model.CommissionAmount = result.CommissionAmount;
                model.GiftAmount = result.GiftAmount;
                model.HadiyaAmount = result.HadiyaAmount;
                model.EmployeeDebtDeducted = result.EmployeeDebtTotal;
                model.CarriedAdvanceBalance = result.CarriedAdvanceBalance;
                model.NewAdvancesAmount = result.NewAdvancesAmount;
                model.TotalAdvanceDue = result.TotalAdvanceDue;
                model.AvailableForAdvanceRepayment = result.AvailableForAdvanceRepayment;
                model.AdvanceDeducted = result.AdvanceDeducted;
                model.RemainingAdvanceCarried = result.RemainingAdvanceCarried;
                model.NetSalary = result.NetSalary;
                model.AutoNote = result.AutoNote;

                if (model.NetSalary == 0)
                {
                    model.Status = Salary.Statuses.SettledNoPayment;
                    model.PaidDate = null;
                    model.PaymentMethod = "-";
                }
                else if (model.PaidDate.HasValue)
                {
                    model.Status = Salary.Statuses.Paid;
                }
                else
                {
                    model.Status = Salary.Statuses.Pending;
                }

                // خصم رصيد السلف من سجلاتها الأصلية يتم فور اعتماد التسوية، بغض النظر عن تاريخ
                // صرف المبلغ المتبقي فعلياً للموظف — فرصيد السلف حقيقة محاسبية مستقلة عن توقيت الكاش.
                await AdvanceReconciliationHelper.ReconcileAsync(_context, model.EmployeeId, model.AdvanceDeducted);

                model.CreatedAt = DateTime.Now;
                _context.Salaries.Add(model);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Add", "Salaries",
                    $"تسوية راتب شهر {monthLabel}/{model.Year} للموظف: {employee.FullName} صافي: {model.NetSalary:N3} KD - {model.Status}",
                    model.Id);

                TempData["Success"] = "تمت تسوية راتب الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            var cu3 = await _userManager.GetUserAsync(User);
            var ud3 = cu3?.UserDepartment;
            var eq3 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (ud3 == "حلاقة" || ud3 == "مساج") eq3 = eq3.Where(e => e.DepartmentNav!.Name == ud3);
            ViewBag.Employees = new SelectList(await eq3.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            var salary = await _context.Salaries.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == id);
            if (salary != null)
            {
                if (salary.NetSalary <= 0)
                {
                    TempData["Error"] = "لا يوجد مبلغ مستحق الصرف لهذا الراتب";
                    return RedirectToAction(nameof(Index));
                }

                // خصم السلف تم بالفعل عند حفظ التسوية (Create) — هذا الإجراء يقتصر على تسجيل
                // تاريخ الصرف الفعلي لمبلغ الصافي المحسوب مسبقاً، دون إعادة حساب أي رصيد سلف.
                salary.Status = Salary.Statuses.Paid;
                salary.PaidDate = DateTime.Today;

                await _context.SaveChangesAsync();

                await _audit.LogAsync("Pay", "Salaries",
                    $"صرف راتب شهر {salary.Month}/{salary.Year} للموظف: {salary.Employee?.FullName ?? salary.EmployeeId.ToString()} بمبلغ {salary.NetSalary:N3} KD",
                    salary.Id);

                TempData["Success"] = "Salary paid created successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var salary = await _context.Salaries.Include(s => s.Employee).FirstOrDefaultAsync(s => s.Id == id);
            if (salary != null)
            {
                string empName = salary.Employee?.FullName ?? salary.EmployeeId.ToString();
                int month = salary.Month;
                int year = salary.Year;

                await AdvanceReconciliationHelper.UnreconcileAsync(_context, salary.EmployeeId, salary.AdvanceDeducted);

                _context.Salaries.Remove(salary);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "Salaries",
                    $"حذف راتب شهر {month}/{year} للموظف: {empName}",
                    id);

                TempData["Success"] = "Salary record deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}