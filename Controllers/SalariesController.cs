using Microsoft.AspNetCore.Authorization;
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

        public SalariesController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(int? year, int? month)
        {
            int y = year ?? DateTime.Today.Year;
            int m = month ?? DateTime.Today.Month;

            var salaries = await _context.Salaries
                .Include(s => s.Employee)
                .Where(s => s.Year == y && s.Month == m)
                .ToListAsync();

            ViewBag.Year = y;
            ViewBag.Month = m;
            return View(salaries);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(new Salary { Year = DateTime.Today.Year, Month = DateTime.Today.Month });
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeDetails(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            var pendingAdvances = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == id && a.Status == "موافق عليها" && a.PaidDate == null)
                .ToListAsync();
            var totalAdvances = pendingAdvances.Sum(a => a.Amount - a.DeductedAmount);

            return Json(new
            {
                salary = employee.BasicSalary,
                commission = employee.Commission,
                advances = totalAdvances
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
                    ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                model.NetSalary = model.BasicSalary + model.Allowances - model.Deductions - model.AdvanceDeducted;
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
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
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