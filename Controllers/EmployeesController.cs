using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public EmployeesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(e => e.DepartmentNav!.Name == userDept);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.FullName.Contains(search) || (e.Phone != null && e.Phone.Contains(search)));

            var employees = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
            ViewBag.Search = search;
            return View(employees);
        }

        private async Task LoadDepartments(int? selectedId = null)
        {
            ViewBag.Departments = new SelectList(
                await _context.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(),
                "Id", "Name", selectedId);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDepartments();
            return View(new Employee());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee model)
        {
            if (ModelState.IsValid)
            {
                await CheckDuplicates(model, excludeId: null);
                if (!ModelState.IsValid)
                {
                    await LoadDepartments(model.DepartmentId);
                    return View(model);
                }

                model.CreatedAt = DateTime.Now;
                _context.Employees.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("إضافة", "الموظفين", $"موظف جديد: {model.FullName}", model.Id);
                TempData["Success"] = "تم إضافة الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await LoadDepartments(model.DepartmentId);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();
            await LoadDepartments(employee.DepartmentId);
            return View(employee);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                await CheckDuplicates(model, excludeId: id);
                if (!ModelState.IsValid)
                {
                    await LoadDepartments(model.DepartmentId);
                    return View(model);
                }

                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("تعديل", "الموظفين", $"تعديل بيانات الموظف: {model.FullName}", model.Id);
                TempData["Success"] = "تم تعديل بيانات الموظف بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await LoadDepartments(model.DepartmentId);
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.DepartmentNav)
                .Include(e => e.Attendances)
                .Include(e => e.Salaries)
                .Include(e => e.Advances)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            if ((userDept == "حلاقة" || userDept == "مساج") && employee.DepartmentNav?.Name != userDept)
                return Forbid();

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
                await _audit.LogAsync("حذف", "الموظفين", $"حذف الموظف: {employee.FullName}", employee.Id);
                TempData["Success"] = "تم حذف الموظف بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckDuplicates(Employee model, int? excludeId)
        {
            var query = _context.Employees.Where(e => e.IsActive);
            if (excludeId.HasValue)
                query = query.Where(e => e.Id != excludeId.Value);

            if (await query.AnyAsync(e => e.FullName == model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "يوجد موظف آخر بنفس الاسم");

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await query.AnyAsync(e => e.Phone == model.Phone))
                ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف مستخدم لدى موظف آخر");

            if (!string.IsNullOrWhiteSpace(model.IdNumber) &&
                await query.AnyAsync(e => e.IdNumber == model.IdNumber))
                ModelState.AddModelError(nameof(model.IdNumber), "رقم الإقامة / الهوية مستخدم لدى موظف آخر");
        }

        public async Task<IActionResult> Licenses()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive && e.ResidencyExpiry != null);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(e => e.DepartmentNav!.Name == userDept);

            var employees = await query.OrderBy(e => e.ResidencyExpiry).ToListAsync();
            return View(employees);
        }
    }
}