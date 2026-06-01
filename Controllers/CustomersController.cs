using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public CustomersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? search, string? dept)
        {
            var user = await _userManager.GetUserAsync(User);
            var userDept = user?.UserDepartment;

            var query = _context.Customers.Where(c => c.IsActive);

            // Department-restricted users see only their department's customers
            if (userDept == "مساج")
                query = query.Where(c => c.Department == "مساج");
            else if (userDept == "حلاقة")
                query = query.Where(c => c.Department == "حلاقة");
            else if (!string.IsNullOrEmpty(dept))
                query = query.Where(c => c.Department == dept);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c =>
                    c.FullName.Contains(search) ||
                    (c.FullNameEn != null && c.FullNameEn.Contains(search)) ||
                    (c.Phone != null && c.Phone.Contains(search)));

            var customers = await query
                .Include(c => c.Sales)
                .OrderByDescending(c => c.CreatedAt).ToListAsync();
            ViewBag.Search = search;
            ViewBag.Dept = dept;
            ViewBag.UserDepartment = userDept;
            return View(customers);
        }

        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var model = new Customer();
            if (!string.IsNullOrEmpty(user?.UserDepartment))
                model.Department = user.UserDepartment;
            ViewBag.UserDepartment = user?.UserDepartment;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!string.IsNullOrEmpty(model.Phone))
            {
                var phoneExists = await _context.Customers.AnyAsync(c => c.Phone == model.Phone);
                if (phoneExists)
                    ModelState.AddModelError("Phone", "رقم الهاتف مستخدم بالفعل لعميل آخر");
            }

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Customers.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("إضافة", "العملاء", $"عميل جديد: {model.FullName}", model.Id);
                TempData["Success"] = "تم إضافة العميل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer model)
        {
            if (id != model.Id) return NotFound();

            if (!string.IsNullOrEmpty(model.Phone))
            {
                var phoneExists = await _context.Customers.AnyAsync(c => c.Phone == model.Phone && c.Id != id);
                if (phoneExists)
                    ModelState.AddModelError("Phone", "رقم الهاتف مستخدم بالفعل لعميل آخر");
            }

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("تعديل", "العملاء", $"تعديل بيانات العميل: {model.FullName}", model.Id);
                TempData["Success"] = "تم تعديل بيانات العميل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Appointments)
                .Include(c => c.Sales)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                customer.IsActive = false;
                await _context.SaveChangesAsync();
                await _audit.LogAsync("حذف", "العملاء", $"حذف العميل: {customer.FullName}", customer.Id);
                TempData["Success"] = "تم حذف العميل بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
