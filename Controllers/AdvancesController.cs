using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class AdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdvancesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.EmployeeAdvances.Include(a => a.Employee).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Employee != null && a.Employee.FullName.Contains(search));

            var advances = await query.OrderByDescending(a => a.AdvanceDate).ToListAsync();
            ViewBag.Search = search;
            return View(advances);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
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
                TempData["Success"] = "تم إضافة السلفة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var advance = await _context.EmployeeAdvances.FindAsync(id);
            if (advance != null)
            {
                advance.Status = "موافق عليها";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم الموافقة على السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var advance = await _context.EmployeeAdvances.FindAsync(id);
            if (advance != null)
            {
                _context.EmployeeAdvances.Remove(advance);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
