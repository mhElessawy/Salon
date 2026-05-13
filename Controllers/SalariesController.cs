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

        public async Task<IActionResult> Index(int? year, int? month, int? employeeId)
        {
            int y = year ?? DateTime.Today.Year;

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

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
                .Where(a => a.EmployeeId == employeeId && a.Status == "موافق عليها" && a.PaidDate == null)
                .ToListAsync();
            var totalAdvances = pendingAdvances.Sum(a => a.Amount - a.DeductedAmount);

            bool alreadyPaid = await _context.Salaries
                .AnyAsync(s => s.EmployeeId == employeeId && s.Month == month && s.Year == year);

            var rangeStart = new DateTime(year, month, 1);
            var rangeEnd = rangeStart.AddMonths(1);
            var totalGifts = await _context.Sales
                .Where(s => s.EmployeeId == employeeId
                         && s.SaleDate >= rangeStart
                         && s.SaleDate < rangeEnd
                         && s.EmployeeGift != null && s.EmployeeGift > 0)
                .Select(s => s.EmployeeGift!.Value)
                .SumAsync();

            return Json(new
            {
                basicSalary = employee.BasicSalary,
                advanceDeducted = totalAdvances,
                totalGifts,
                alreadyPaid
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
                    ModelState.AddModelError("", "تم تسجيل راتب هذا الموظف لهذا الشهر مسبقًا");
                    var cu2 = await _userManager.GetUserAsync(User);
                    var ud2 = cu2?.UserDepartment;
                    var eq2 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
                    if (ud2 == "حلاقة" || ud2 == "مساج") eq2 = eq2.Where(e => e.DepartmentNav!.Name == ud2);
                    ViewBag.Employees = new SelectList(await eq2.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                model.NetSalary = model.BasicSalary + model.Allowances + (model.GiftAmount ?? 0) - model.Deductions - model.AdvanceDeducted;
                model.CreatedAt = DateTime.Now;
                _context.Salaries.Add(model);
                await _context.SaveChangesAsync();

                var emp = await _context.Employees.FindAsync(model.EmployeeId);
                await _audit.LogAsync("إضافة", "الرواتب",
                    $"إضافة راتب شهر {model.Month}/{model.Year} للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} صافي: {model.NetSalary:N3} د.ك",
                    model.Id);

                TempData["Success"] = "تم إضافة راتب الموظف بنجاح";
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

                if (salary.AdvanceDeducted > 0)
                {
                    var advances = await _context.EmployeeAdvances
                        .Where(a => a.EmployeeId == salary.EmployeeId && a.Status == "موافق عليها" && a.PaidDate == null)
                        .OrderBy(a => a.AdvanceDate)
                        .ToListAsync();

                    decimal remaining = salary.AdvanceDeducted;
                    foreach (var advance in advances)
                    {
                        if (remaining <= 0) break;

                        decimal advanceRemaining = advance.Amount - advance.DeductedAmount;
                        if (advanceRemaining <= remaining)
                        {
                            remaining -= advanceRemaining;
                            advance.DeductedAmount = advance.Amount;
                            advance.PaidDate = DateTime.Today;
                            advance.Status = "مسددة";
                        }
                        else
                        {
                            advance.DeductedAmount += remaining;
                            remaining = 0;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                await _audit.LogAsync("صرف", "الرواتب",
                    $"صرف راتب شهر {salary.Month}/{salary.Year} للموظف: {salary.Employee?.FullName ?? salary.EmployeeId.ToString()} بمبلغ {salary.NetSalary:N3} د.ك",
                    salary.Id);

                TempData["Success"] = "تم صرف الراتب بنجاح";
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

                _context.Salaries.Remove(salary);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("حذف", "الرواتب",
                    $"حذف راتب شهر {month}/{year} للموظف: {empName}",
                    id);

                TempData["Success"] = "تم حذف سجل الراتب بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}