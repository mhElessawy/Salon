using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public SuppliersController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Suppliers.Where(s => s.IsActive).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.Name.Contains(search) || (s.Phone != null && s.Phone.Contains(search)));

            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Search = search;
            return View(suppliers);
        }

        public IActionResult Create() => View(new Supplier());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Suppliers.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("إضافة", "الموردين", $"مورد جديد: {model.Name}", model.Id);
                TempData["Success"] = "تم إضافة المورد بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("تعديل", "الموردين", $"تعديل بيانات المورد: {model.Name}", model.Id);
                TempData["Success"] = "تم تعديل بيانات المورد بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier != null)
            {
                supplier.IsActive = false;
                await _context.SaveChangesAsync();
                await _audit.LogAsync("حذف", "الموردين", $"حذف المورد: {supplier.Name}", supplier.Id);
                TempData["Success"] = "تم حذف المورد بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}