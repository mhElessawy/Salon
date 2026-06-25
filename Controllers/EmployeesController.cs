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

            var roles = await _userManager.GetRolesAsync(currentUser!);
            bool isManager = roles.Contains("Admin") || roles.Contains("Manager");
            bool isEmployee = !isManager && roles.Contains("Employee");
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(e => e.DepartmentNav!.Name == userDept);

            // Employees see only themselves
            if (isEmployee && linkedEmpId.HasValue)
                query = query.Where(e => e.Id == linkedEmpId.Value);

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
                await _audit.LogAsync("Add", "Employees", $"موظف جديد: {model.FullName}", model.Id);
                TempData["Success"] = "Employee added created successfully";
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
                await _audit.LogAsync("Edit", "Employees", $"تعديل بيانات Employee: {model.FullName}", model.Id);
                TempData["Success"] = "Employee data updated created successfully";
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
            var detailRoles = await _userManager.GetRolesAsync(currentUser!);
            bool detailIsManager = detailRoles.Contains("Admin") || detailRoles.Contains("Manager");
            bool detailIsEmployee = !detailIsManager && detailRoles.Contains("Employee");

            if ((userDept == "حلاقة" || userDept == "مساج") && employee.DepartmentNav?.Name != userDept)
                return Forbid();

            if (detailIsEmployee && currentUser?.LinkedEmployeeId.HasValue == true && employee.Id != currentUser.LinkedEmployeeId.Value)
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
                await _audit.LogAsync("Delete", "Employees", $"حذف Employee: {employee.FullName}", employee.Id);
                TempData["Success"] = "Employee deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckDuplicates(Employee model, int? excludeId)
        {
            var query = _context.Employees.Where(e => e.IsActive);
            if (excludeId.HasValue)
                query = query.Where(e => e.Id != excludeId.Value);

            if (await query.AnyAsync(e => e.FullName == model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Another employee has the same name");

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await query.AnyAsync(e => e.Phone == model.Phone))
                ModelState.AddModelError(nameof(model.Phone), "Phone number is used by another employee");

            if (!string.IsNullOrWhiteSpace(model.IdNumber) &&
                await query.AnyAsync(e => e.IdNumber == model.IdNumber))
                ModelState.AddModelError(nameof(model.IdNumber), "Residency / ID number is used by another employee");
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