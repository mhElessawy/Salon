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
        public async Task<IActionResult> GetEmployeeInfo(int employeeId, int month, int year)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var pendingAdvances = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId
                         && (a.Status == EmployeeAdvance.Statuses.Disbursed || a.Status == EmployeeAdvance.Statuses.Transferred)
                         && a.PaidDate == null)
                .ToListAsync();
            var totalAdvances = pendingAdvances.Sum(a => a.Amount - a.DeductedAmount);

            bool alreadyPaid = await _context.Salaries
                .AnyAsync(s => s.EmployeeId == employeeId && s.Month == month && s.Year == year);

            var rangeStart = new DateTime(year, month, 1);
            var rangeEnd = rangeStart.AddMonths(1);

            var allSalesRaw = await _context.Sales
                .Where(s => s.EmployeeId == employeeId
                         && s.SaleDate >= rangeStart
                         && s.SaleDate < rangeEnd)
                .OrderBy(s => s.SaleDate)
                .Select(s => new
                {
                    invoiceNumber = s.InvoiceNumber,
                    saleDate = s.SaleDate.ToString("yyyy-MM-dd"),
                    netAmount = s.NetAmount,
                    status = s.Status,
                    paymentMethod = s.PaymentMethod,
                    cashAmount = s.CashAmount,
                    linkAmount = s.LinkAmount,
                    giftForEmployee = s.GiftForEmployee ?? 0
                })
                .ToListAsync();

            var sales = allSalesRaw.Where(s => s.status != "ملغي").ToList();
            var cancelledSales = allSalesRaw.Where(s => s.status == "ملغي").ToList();

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            var totalSalesAmount = sales.Sum(s => s.netAmount);
            bool targetReached = employee.SalesTarget.HasValue
                                 && employee.SalesTarget.Value > 0
                                 && totalSalesAmount >= employee.SalesTarget.Value;
            var effectiveCommission = (targetReached && employee.CommissionAfterTarget.HasValue)
                                      ? employee.CommissionAfterTarget.Value
                                      : employee.Commission;
            var commissionAmount = Math.Round(totalSalesAmount * effectiveCommission / 100, 3);

            var cashTotal = Math.Round(sales.Sum(s =>
                cashMethods.Contains(s.paymentMethod) ? s.netAmount :
                mixedMethods.Contains(s.paymentMethod) ? (s.cashAmount ?? 0) : 0), 3);
            var knetTotal = Math.Round(sales.Sum(s =>
                knetMethods.Contains(s.paymentMethod) ? s.netAmount :
                mixedMethods.Contains(s.paymentMethod) ? (s.linkAmount ?? 0) : 0), 3);
            var employeeDebtTotal = Math.Round(sales.Where(s => s.paymentMethod == "دين على الموظف").Sum(s => s.netAmount), 3);
            var customerDebtTotal = Math.Round(sales.Where(s => s.paymentMethod == "دين على العميل").Sum(s => s.netAmount), 3);

            var totalGifts = await _context.Sales
                .Where(s => s.EmployeeId == employeeId
                         && s.SaleDate >= rangeStart
                         && s.SaleDate < rangeEnd
                         && s.Status != "ملغي"
                         && s.EmployeeGift != null && s.EmployeeGift > 0)
                .Select(s => s.EmployeeGift!.Value)
                .SumAsync();

            var totalHadiya = await _context.Sales
                .Where(s => s.EmployeeId == employeeId
                         && s.SaleDate >= rangeStart
                         && s.SaleDate < rangeEnd
                         && s.Status != "ملغي"
                         && s.GiftForEmployee != null && s.GiftForEmployee > 0)
                .SumAsync(s => s.GiftForEmployee!.Value);

            return Json(new
            {
                basicSalary = employee.BasicSalary,
                commission = effectiveCommission,
                sales,
                cancelledSales,
                totalSalesAmount,
                commissionAmount,
                cashTotal,
                knetTotal,
                employeeDebtTotal,
                customerDebtTotal,
                advanceDeducted = totalAdvances,
                totalGifts,
                totalHadiya,
                alreadyPaid,
                salesTarget = employee.SalesTarget,
                targetReached,
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
                    ModelState.AddModelError("", "Salary already recorded for this employee this month");
                    var cu2 = await _userManager.GetUserAsync(User);
                    var ud2 = cu2?.UserDepartment;
                    var eq2 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
                    if (ud2 == "حلاقة" || ud2 == "مساج") eq2 = eq2.Where(e => e.DepartmentNav!.Name == ud2);
                    ViewBag.Employees = new SelectList(await eq2.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                model.NetSalary = model.BasicSalary + model.CommissionAmount + model.Allowances + (model.GiftAmount ?? 0) + (model.HadiyaAmount ?? 0) - model.Deductions - model.AdvanceDeducted - model.EmployeeDebtDeducted;
                model.CreatedAt = DateTime.Now;
                if (model.PaidDate.HasValue)
                {
                    model.Status = "مصروف";
                    await AdvanceReconciliationHelper.ReconcileAsync(_context, model.EmployeeId, model.AdvanceDeducted);
                }
                _context.Salaries.Add(model);
                await _context.SaveChangesAsync();

                var emp = await _context.Employees.FindAsync(model.EmployeeId);
                await _audit.LogAsync("Add", "Salaries",
                    $"إضافة راتب شهر {model.Month}/{model.Year} للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} صافي: {model.NetSalary:N3} KD",
                    model.Id);

                TempData["Success"] = "Employee salary added created successfully";
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
                salary.Status = "مصروف";
                salary.PaidDate = DateTime.Today;

                await AdvanceReconciliationHelper.ReconcileAsync(_context, salary.EmployeeId, salary.AdvanceDeducted);

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