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
        private readonly IPermissionService _perms;

        public CustomersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit, IPermissionService perms)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
            _perms = perms;
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

            // صلاحية "عملائي فقط": الموظف يشوف عملاءه المعينين له فقط
            if (user?.LinkedEmployeeId.HasValue == true && await _perms.HasAccessAsync("CustomersMyOnly"))
                query = query.Where(c => c.AssignedEmployeeId == user.LinkedEmployeeId);

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
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment, user?.LinkedEmployeeId);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!string.IsNullOrEmpty(model.Phone))
            {
                var phoneExists = await _context.Customers.AnyAsync(c =>
                    c.Phone == model.Phone && c.Department == model.Department && c.IsActive);
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
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment, user?.LinkedEmployeeId);
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
                var phoneExists = await _context.Customers.AnyAsync(c =>
                    c.Phone == phone && c.Department == department && c.IsActive);
                if (phoneExists)
                    return Json(new { success = false, message = "رقم الهاتف مستخدم مسبقاً لعميل آخر في نفس القسم" });
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
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment, user?.LinkedEmployeeId);
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
            ViewBag.Employees = await GetEmployeesForUserAsync(user?.UserDepartment, user?.LinkedEmployeeId);
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Appointments)
                .Include(c => c.Sales)
                .Include(c => c.CustomerPackages).ThenInclude(cp => cp.ServicePackage)
                .Include(c => c.CustomerPackages).ThenInclude(cp => cp.Transactions).ThenInclude(t => t.Employee)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // ─── إضافة رصيد افتتاحي لباقة قديمة (مدير/أدمن فقط) ───────────
        // باقات ما قبل تشغيل النظام: العميل دفع قيمتها مسبقاً، فلا تُنشأ فاتورة ولا حركة
        // كاش/كي نت/بنك ولا إيراد ولا عمولة — مجرد تسجيل رصيد داخل محفظة العميل.
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AddOpeningBalancePackage(
            int customerId, string packageName, decimal remainingBalance,
            int totalSessions, int usedSessions, DateTime? purchaseDate, string? notes)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null) return NotFound();

            if (string.IsNullOrWhiteSpace(packageName) || totalSessions < 1 ||
                usedSessions < 0 || usedSessions > totalSessions || remainingBalance < 0)
            {
                TempData["Error"] = "الرجاء إدخال بيانات صحيحة للباقة (اسم الباقة، عدد الجلسات، الرصيد)";
                return RedirectToAction(nameof(Details), new { id = customerId });
            }

            packageName = packageName.Trim();
            var servicePackage = await _context.ServicePackages
                .FirstOrDefaultAsync(p => p.NameAr == packageName);
            if (servicePackage == null)
            {
                servicePackage = new ServicePackage
                {
                    NameAr = packageName,
                    SessionCount = totalSessions,
                    Price = 0,
                    // رصيد افتتاحي فقط — لا تُعرض هذه الباقة كباقة قابلة للبيع من جديد
                    IsActive = false
                };
                _context.ServicePackages.Add(servicePackage);
                await _context.SaveChangesAsync();
            }

            var remainingSessions = totalSessions - usedSessions;
            var customerPkg = new CustomerPackage
            {
                CustomerId = customerId,
                ServicePackageId = servicePackage.Id,
                PurchaseDate = purchaseDate ?? DateTime.Today,
                TotalSessions = totalSessions,
                RemainingSessions = remainingSessions,
                PricePaid = 0,
                CurrentBalance = remainingBalance,
                RegistrationType = CustomerPackage.RegistrationTypes.OpeningBalance,
                Notes = notes,
                IsActive = remainingSessions > 0
            };
            _context.CustomerPackages.Add(customerPkg);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Add", "Customers",
                $"رصيد افتتاحي لباقة: {packageName} للعميل ID {customerId} — الرصيد: {remainingBalance:F3} د.ك",
                customerPkg.Id);
            TempData["Success"] = "تم إضافة الرصيد الافتتاحي للباقة بنجاح";
            return RedirectToAction(nameof(Details), new { id = customerId });
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
                query = query.Where(c => c.Department == effectiveDept || c.Department == "الكل");

            // صلاحية "عملائي فقط": الموظف يشوف عملاءه المعينين له فقط، إلا لو كانت صلاحيته "جميع العملاء"
            if (user?.LinkedEmployeeId.HasValue == true && await _perms.HasAccessAsync("CustomersMyOnly"))
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

        private async Task<List<Employee>> GetEmployeesForUserAsync(string? userDepartment, int? linkedEmployeeId = null)
        {
            var query = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            // موظف مربوط بحساب مستخدم: يشوف اسمه بس في قائمة "الموظف المسؤول"
            if (linkedEmployeeId.HasValue)
                query = query.Where(e => e.Id == linkedEmployeeId.Value);
            else if (userDepartment == "حلاقة" || userDepartment == "مساج")
                query = query.Where(e => e.DepartmentNav != null && e.DepartmentNav.Name == userDepartment);

            return await query.OrderBy(e => e.FullName).ToListAsync();
        }
    }
}