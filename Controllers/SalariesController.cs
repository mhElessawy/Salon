using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class SalariesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalariesController(ApplicationDbContext context)
        {
            _context = context;
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

        // يُستخدم من JavaScript لجلب راتب الموظف وسلفه المعلق
        public async Task<IActionResult> GetEmployeeInfo(int employeeId, int month, int year)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var pendingAdvances = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId && a.Status == "معلق")
                .SumAsync(a => (decimal?)a.Amount) ?? 0;

            return Json(new
            {
                basicSalary = employee.BasicSalary,
                advanceDeducted = pendingAdvances
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Salary model)
        {
            if (ModelState.IsValid)
            {
                // منع تكرار الراتب لنفس الموظف في نفس الشهر والسنة
                var exists = await _context.Salaries.AnyAsync(s =>
                    s.EmployeeId == model.EmployeeId &&
                    s.Month == model.Month &&
                    s.Year == model.Year);
                if (exists)
                {
                    ModelState.AddModelError("", "تم صرف راتب هذا الموظف لهذا الشهر مسبقاً");
                    ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
                    return View(model);
                }

                model.NetSalary = model.BasicSalary + model.Allowances - model.Deductions - model.AdvanceDeducted;
                model.CreatedAt = DateTime.Now;
                _context.Salaries.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة راتب الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            var salary = await _context.Salaries.FindAsync(id);
            if (salary != null)
            {
                salary.Status = "مصروف";
                salary.PaidDate = DateTime.Today;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم صرف الراتب بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var salary = await _context.Salaries.FindAsync(id);
            if (salary != null)
            {
                _context.Salaries.Remove(salary);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف سجل الراتب بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
