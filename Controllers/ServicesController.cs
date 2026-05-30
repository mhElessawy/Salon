using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, string? filter)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Services.Include(s => s.ServiceCategory).Where(s => s.IsActive);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(s => s.ServiceCategory!.Department == userDept);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.Name.Contains(search));

            if (!string.IsNullOrEmpty(filter) && filter != "all")
                query = query.Where(s => s.ServiceCategory != null && s.ServiceCategory.Department == filter);

            var services = await query.OrderBy(s => s.ServiceCategoryId).ThenBy(s => s.Name).ToListAsync();
            ViewBag.Search = search;
            ViewBag.Filter = filter ?? "all";
            return View(services);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCategories();
            return View(new Service());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Service model)
        {
            if (ModelState.IsValid)
            {
                _context.Services.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة الخدمة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateCategories(model.ServiceCategoryId);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();
            await PopulateCategories(service.ServiceCategoryId);
            return View(service);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Service model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل الخدمة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateCategories(model.ServiceCategoryId);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الخدمة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCategories(int? selectedId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var catQuery = _context.ServiceCategories.Where(c => c.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                catQuery = catQuery.Where(c => c.Department == userDept);
            ViewBag.Categories = new SelectList(
                await catQuery.OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", selectedId);
        }
    }
}