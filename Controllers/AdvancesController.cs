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
    public class AdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdvancesController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            if (!string.IsNullOrEmpty(search))
                advancesQuery = advancesQuery.Where(a => a.Employee != null && a.Employee.FullName.Contains(search));

            var advances = await advancesQuery.OrderByDescending(a => a.AdvanceDate).ToListAsync();

            // subquery to avoid EF Core generating CTE (WITH) syntax error on SQL Server
            var employeeIdsSubquery = advancesQuery.Select(a => a.EmployeeId).Distinct();

            var salaryDeductions = await _context.Salaries
                .Where(s => employeeIdsSubquery.Contains(s.EmployeeId) && s.AdvanceDeducted > 0)
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .ToListAsync();

            var summaries = advances
                .GroupBy(a => a.Employee)
                .Select(g => new EmployeeAdvanceSummaryViewModel
                {
                    Employee = g.Key!,
                    Advances = g.OrderByDescending(a => a.AdvanceDate).ToList(),
                    SalaryDeductions = salaryDeductions
                        .Where(s => s.EmployeeId == g.Key!.Id)
                        .Select(s => new SalaryAdvanceDeduction
                        {
                            Month = s.Month,
                            Year = s.Year,
                            Amount = s.AdvanceDeducted,
                            PaidDate = s.PaidDate
                        }).ToList()
                })
                .OrderBy(s => s.Employee.FullName)
                .ToList();

            ViewBag.Search = search;
            return View(summaries);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(new EmployeeAdvance { AdvanceDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeAdvance model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.EmployeeAdvances.Add(model);
                await _context.SaveChangesAsync();

                var emp = await _context.Employees.FindAsync(model.EmployeeId);
                await _audit.LogAsync("إضافة", "السلف",
                    $"إضافة سلفة للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بمبلغ {model.Amount:N3} د.ك",
                    model.Id);

                TempData["Success"] = "تم إضافة السلفة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            var cu2 = await _userManager.GetUserAsync(User);
            var ud2 = cu2?.UserDepartment;
            var eq2 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (ud2 == "حلاقة" || ud2 == "مساج") eq2 = eq2.Where(e => e.DepartmentNav!.Name == ud2);
            ViewBag.Employees = new SelectList(await eq2.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        public async Task<IActionResult> Report(DateTime? dateFrom, DateTime? dateTo, int? employeeId, string? status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            if (dateFrom.HasValue)
                query = query.Where(a => a.AdvanceDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(a => a.AdvanceDate <= dateTo.Value);

            if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            var advances = await query.OrderByDescending(a => a.AdvanceDate).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);

            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId = employeeId;
            ViewBag.Status = status;

            return View(advances);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance != null)
            {
                advance.Status = "موافق عليها";
                await _context.SaveChangesAsync();

                await _audit.LogAsync("موافقة", "السلف",
                    $"الموافقة على سلفة الموظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} د.ك",
                    advance.Id);

                TempData["Success"] = "تم الموافقة على السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance != null)
            {
                string empName = advance.Employee?.FullName ?? advance.EmployeeId.ToString();
                decimal amount = advance.Amount;

                _context.EmployeeAdvances.Remove(advance);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("حذف", "السلف",
                    $"حذف سلفة الموظف: {empName} بمبلغ {amount:N3} د.ك",
                    id);

                TempData["Success"] = "تم حذف السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}