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
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                model.CreatedAt = DateTime.Now;
                _context.Employees.Add(model);
                await _context.SaveChangesAsync();
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
                _context.Update(model);
                await _context.SaveChangesAsync();
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
                TempData["Success"] = "تم حذف الموظف بنجاح";
            }
            return RedirectToAction(nameof(Index));
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