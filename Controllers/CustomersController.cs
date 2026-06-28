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
                .Include(c => c.Sales).ThenInclude(s => s.Employee)
                .Include(c => c.CustomerPackages).ThenInclude(cp => cp.ServicePackage)
                .Include(c => c.AssignedEmployee)
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
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!string.IsNullOrEmpty(model.Phone))
            {
                var phoneExists = await _context.Customers.AnyAsync(c => c.Phone == model.Phone);
                if (phoneExists)
                    ModelState.AddModelError("Phone", "Phone number already used by another customer");
            }

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Customers.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Customers", $"New customer: {model.FullName}", model.Id);
                TempData["Success"] = "Customer added created successfully";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = user?.UserDepartment;
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuick(
            string fullName, string? fullNameEn, string? phone, string? email,
            string? birthDate, string? gender, string? assignedEmployeeIdStr,
            string? notes, string? department)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return Json(new { success = false, message = "الاسم الكامل مطلوب" });

            if (!string.IsNullOrEmpty(phone))
            {
                var phoneExists = await _context.Customers.AnyAsync(c => c.Phone == phone);
                if (phoneExists)
                    return Json(new { success = false, message = "رقم الهاتف مستخدم مسبقاً لعميل آخر" });
            }

            int? assignedEmpId = null;
            if (int.TryParse(assignedEmployeeIdStr, out var eid) && eid > 0)
                assignedEmpId = eid;

            var customer = new Customer
            {
                FullName = fullName.Trim(),
                FullNameEn = string.IsNullOrWhiteSpace(fullNameEn) ? null : fullNameEn.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                BirthDate = DateTime.TryParse(birthDate, out var bd) ? bd : null,
                Gender = string.IsNullOrWhiteSpace(gender) ? null : gender,
                AssignedEmployeeId = assignedEmpId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Department = department,
                CreatedAt = DateTime.Now,
                IsActive = true
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Add", "Customers", $"عميل جديد (سريع): {customer.FullName}", customer.Id);

            var displayName = string.IsNullOrEmpty(customer.Phone)
                ? customer.FullName
                : $"{customer.FullName} — {customer.Phone}";

            return Json(new
            {
                success = true,
                id = customer.Id,
                fullName = customer.FullName,
                displayName = displayName,
                phone = customer.Phone ?? ""
            });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = user?.UserDepartment;
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment);
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
                    ModelState.AddModelError("Phone", "Phone number already used by another customer");
            }

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Customers", $"Edit customer: {model.FullName}", model.Id);
                TempData["Success"] = "Customer data updated created successfully";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = user?.UserDepartment;
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment);
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
                await _audit.LogAsync("Delete", "Customers", $"Delete customer: {customer.FullName}", customer.Id);
                TempData["Success"] = "Customer deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SearchJson(string? q, string? dept)
        {
            var user = await _userManager.GetUserAsync(User);
            var userDept = user?.UserDepartment;

            var query = _context.Customers.Where(c => c.IsActive);

            // استخدم dept القادم من الطلب إذا كان صحيحاً، وإلا استخدم قسم المستخدم
            var effectiveDept = (dept == "مساج" || dept == "حلاقة") ? dept
                              : (!string.IsNullOrEmpty(userDept) ? userDept : null);
            if (effectiveDept == "مساج" || effectiveDept == "حلاقة")
                query = query.Where(c => c.Department == effectiveDept);

            if (user?.LinkedEmployeeId.HasValue == true)
                query = query.Where(c => c.AssignedEmployeeId == user.LinkedEmployeeId);

            if (!string.IsNullOrEmpty(q))
                query = query.Where(c =>
                    c.FullName.Contains(q) ||
                    (c.FullNameEn != null && c.FullNameEn.Contains(q)) ||
                    (c.Phone != null && c.Phone.Contains(q)));

            var raw = await query
                .OrderBy(c => c.FullName)
                .Take(30)
                .Select(c => new {
                    id = c.Id,
                    fullName = c.FullName,
                    phone = c.Phone ?? "",
                    displayName = string.IsNullOrEmpty(c.Phone) ? c.FullName : c.FullName + " — " + c.Phone,
                    visitCount = c.Sales.Count(),
                    lastVisit = c.Sales.Any() ? (DateTime?)c.Sales.Max(s => s.SaleDate) : null
                })
                .ToListAsync();

            var results = raw.Select(c => new {
                c.id,
                c.fullName,
                c.phone,
                c.displayName,
                c.visitCount,
                lastVisit = c.lastVisit.HasValue ? c.lastVisit.Value.ToString("dd/MM/yyyy") : (string?)null
            });

            return Json(results);
        }

        private async Task<List<Employee>> GetEmployeesForUserAsync(string? userDepartment)
        {
            var query = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (userDepartment == "حلاقة" || userDepartment == "مساج")
                query = query.Where(e => e.DepartmentNav != null && e.DepartmentNav.Name == userDepartment);

            return await query.OrderBy(e => e.FullName).ToListAsync();
        }
    }
}