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

        public async Task<IActionResult> Index(int? year, int? month, int? employeeId)
        {
            int y = year ?? DateTime.Today.Year;

            var query = _context.Salaries
                .Include(s => s.Employee)
                .Where(s => s.Year == y);

            if (month.HasValue)
                query = query.Where(s => s.Month == month.Value);

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId.Value);

            var salaries = await query.OrderBy(s => s.Month).ThenBy(s => s.Employee!.FullName).ToListAsync();

            ViewBag.Year = y;
            ViewBag.Month = month;           // nullable — null = كل الأشهر
            ViewBag.EmployeeId = employeeId; // nullable — null = الكل
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");

            // كل السنوات الموجودة في قاعدة البيانات + السنة الحالية
            var dbYears = await _context.Salaries.Select(s => s.Year).Distinct().ToListAsync();
            if (!dbYears.Contains(DateTime.Today.Year))
                dbYears.Add(DateTime.Today.Year);
            ViewBag.Years = dbYears.OrderByDescending(yr => yr).ToList();

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

            // مجموع المتبقي من السلف غير المسددة
            var pendingAdvances = await _context.EmployeeAdvances
                .Where(a => a.EmployeeId == employeeId &&
                            (a.Status == "معلق" || a.Status == "موافق عليها" || a.Status == "مدفوعة جزئياً"))
                .SumAsync(a => (decimal?)(a.Amount - a.AmountPaid)) ?? 0;

            // التحقق من وجود راتب مسبق لنفس الشهر والسنة
            var alreadyPaid = await _context.Salaries.AnyAsync(s =>
                s.EmployeeId == employeeId &&
                s.Month == month &&
                s.Year == year);

            return Json(new
            {
                basicSalary = employee.BasicSalary,
                advanceDeducted = pendingAdvances,
                alreadyPaid
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

                // تسوية السلف المخصومة من الراتب
                if (salary.AdvanceDeducted > 0)
                {
                    var advances = await _context.EmployeeAdvances
                        .Where(a => a.EmployeeId == salary.EmployeeId &&
                                    (a.Status == "معلق" || a.Status == "موافق عليها"))
                        .OrderBy(a => a.AdvanceDate)
                        .ToListAsync();

                    decimal remaining = salary.AdvanceDeducted;
                    foreach (var adv in advances)
                    {
                        if (remaining <= 0) break;
                        decimal canPay = Math.Min(adv.Amount - adv.AmountPaid, remaining);
                        adv.AmountPaid += canPay;
                        remaining -= canPay;
                        if (adv.AmountPaid >= adv.Amount)
                        {
                            adv.Status = "مسددة";
                            adv.PaidDate = DateTime.Today;
                        }
                        else
                        {
                            adv.Status = "مدفوعة جزئياً";
                        }
                    }
                }

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
