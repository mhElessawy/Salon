using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class DepartmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DepartmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();
            return View(departments);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, string? description)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _context.Departments.Add(new Department
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة القسم بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, string? description)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept != null && !string.IsNullOrWhiteSpace(name))
            {
                dept.Name = name.Trim();
                dept.Description = description?.Trim();
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل القسم بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var dept = await _context.Departments.FindAsync(id);
            if (dept != null)
            {
                var hasEmployees = await _context.Employees.AnyAsync(e => e.DepartmentId == id && e.IsActive);
                if (hasEmployees)
                {
                    TempData["Error"] = "لا يمكن حذف القسم — يوجد موظفون مرتبطون به";
                    return RedirectToAction(nameof(Index));
                }
                _context.Departments.Remove(dept);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف القسم";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}