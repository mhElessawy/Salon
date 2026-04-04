using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class ServiceCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServiceCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.ServiceCategories
                .Include(c => c.Services)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        public IActionResult Create() => View(new ServiceCategory());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceCategory model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.ServiceCategories.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الفئة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cat = await _context.ServiceCategories.FindAsync(id);
            if (cat == null) return NotFound();
            return View(cat);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServiceCategory model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل الفئة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _context.ServiceCategories.FindAsync(id);
            if (cat != null)
            {
                cat.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الفئة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}