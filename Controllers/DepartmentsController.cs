using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class DepartmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public DepartmentsController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
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
                var newDept = new Department
                {
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    CreatedAt = DateTime.Now
                };
                _context.Departments.Add(newDept);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Departments", $"New department: {newDept.Name}", newDept.Id);
                TempData["Success"] = "Department added created successfully";
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
                await _audit.LogAsync("Edit", "Departments", $"Edit department: {dept.Name}", dept.Id);
                TempData["Success"] = "Department updated created successfully";
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
                    TempData["Error"] = "Cannot delete department — employees are linked to it";
                    return RedirectToAction(nameof(Index));
                }
                _context.Departments.Remove(dept);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Departments", $"Delete department: {dept.Name}", id);
                TempData["Success"] = "Department deleted";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}