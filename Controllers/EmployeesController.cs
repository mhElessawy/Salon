using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Employees.Where(e => e.IsActive);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.FullName.Contains(search) || (e.Phone != null && e.Phone.Contains(search)));

            var employees = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
            ViewBag.Search = search;
            return View(employees);
        }

        public IActionResult Create() => View(new Employee());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Employees.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل بيانات الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Attendances)
                .Include(e => e.Salaries)
                .Include(e => e.Advances)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                employee.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الموظف بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Licenses()
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive && e.ResidencyExpiry != null)
                .OrderBy(e => e.ResidencyExpiry)
                .ToListAsync();
            return View(employees);
        }
    }
}
