using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // ===== قائمة المستخدمين =====
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            var result = new List<(ApplicationUser User, string Role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add((user, roles.FirstOrDefault() ?? "-"));
            }

            ViewBag.UserRoles = result;
            return View(users);
        }

        // ===== إضافة مستخدم =====
        public async Task<IActionResult> Create()
        {
            await PopulateUserDropdowns();
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return View(new CreateUserViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UserDepartment = model.UserDepartment,
                LinkedEmployeeId = model.LinkedEmployeeId
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                TempData["Success"] = $"تم إنشاء المستخدم {model.FullName} بنجاح";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await PopulateUserDropdowns();
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return View(model);
        }

        // ===== تعديل مستخدم =====
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "Cashier",
                IsActive = user.IsActive,
                UserDepartment = user.UserDepartment,
                LinkedEmployeeId = user.LinkedEmployeeId
            };

            await PopulateUserDropdowns();
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateUserDropdowns();
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.IsActive = model.IsActive;
            user.UserDepartment = model.UserDepartment;
            user.LinkedEmployeeId = model.LinkedEmployeeId;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var e in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                return View(model);
            }

            // Update role
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, model.Role);

            TempData["Success"] = "تم تعديل بيانات المستخدم بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // ===== تغيير كلمة المرور (من قِبَل المدير) =====
        public async Task<IActionResult> ChangePassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(new AdminChangePasswordViewModel
            {
                UserId = user.Id,
                UserName = user.FullName
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("ChangePassword")]
        public async Task<IActionResult> ChangePasswordPost(AdminChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = $"تم تغيير كلمة مرور {user.FullName} بنجاح";
                return RedirectToAction(nameof(Index));
            }

            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);

            return View(model);
        }

        // ===== إدارة الصلاحيات =====
        public async Task<IActionResult> Permissions(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = roles.Contains("Admin");

            var userPerms = await _context.UserPermissions
                .Where(p => p.UserId == id)
                .ToListAsync();

            // إذا لم تكن هناك صلاحيات محددة → الافتراضي: وصول كامل
            var permSet = userPerms.Any()
                ? userPerms.ToDictionary(p => p.Module, p => p.CanAccess)
                : AppModules.All.ToDictionary(m => m.Key, _ => true);

            ViewBag.UserId = id;
            ViewBag.UserFullName = user.FullName;
            ViewBag.UserRole = roles.FirstOrDefault() ?? "-";
            ViewBag.IsAdmin = isAdmin;
            ViewBag.PermSet = permSet;
            ViewBag.Modules = AppModules.All;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("Permissions")]
        public async Task<IActionResult> PermissionsPost(string userId, string[]? allowedModules)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // حذف الصلاحيات القديمة
            var old = _context.UserPermissions.Where(p => p.UserId == userId);
            _context.UserPermissions.RemoveRange(old);

            // إضافة الصلاحيات الجديدة
            var allowed = allowedModules?.ToHashSet() ?? new HashSet<string>();
            foreach (var module in AppModules.All)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    Module = module.Key,
                    CanAccess = allowed.Contains(module.Key)
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم حفظ صلاحيات {user.FullName} بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // ===== نشاط المستخدمين =====
        public async Task<IActionResult> Activity()
        {
            var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();

            // Group by UserId (for properly-logged entries)
            var statsByUserId = await _context.AuditLogs
                .Where(l => l.UserId != null && l.UserId != "")
                .GroupBy(l => l.UserId)
                .Select(g => new { Key = g.Key, Count = g.Count(), LastAt = g.Max(l => l.CreatedAt) })
                .ToDictionaryAsync(x => x.Key, x => (x.Count, x.LastAt));

            // Group by UserName for entries without UserId (older logs)
            var statsByName = await _context.AuditLogs
                .Where(l => l.UserId == null || l.UserId == "")
                .GroupBy(l => l.UserName)
                .Select(g => new { Key = g.Key, Count = g.Count(), LastAt = g.Max(l => l.CreatedAt) })
                .ToDictionaryAsync(x => x.Key, x => (x.Count, x.LastAt));

            var result = new List<UserActivityViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                int count = 0;
                DateTime? lastAt = null;

                if (statsByUserId.TryGetValue(user.Id, out var byId))
                {
                    count += byId.Count;
                    lastAt = byId.LastAt;
                }
                if (statsByName.TryGetValue(user.FullName, out var byName))
                {
                    count += byName.Count;
                    if (!lastAt.HasValue || byName.LastAt > lastAt.Value)
                        lastAt = byName.LastAt;
                }

                result.Add(new UserActivityViewModel
                {
                    User = user,
                    Role = roles.FirstOrDefault() ?? "-",
                    ActivityCount = count,
                    LastActivityAt = lastAt
                });
            }

            return View(result);
        }

        // ===== تعطيل مستخدم =====
        private async Task PopulateUserDropdowns()
        {
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = user.IsActive ? "تم تفعيل المستخدم" : "تم تعطيل المستخدم";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
