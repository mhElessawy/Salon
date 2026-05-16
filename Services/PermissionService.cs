using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    public interface IPermissionService
    {
        Task<HashSet<string>> GetAccessibleModulesAsync();
        Task<bool> HasAccessAsync(string module);
    }

    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task<HashSet<string>> GetAccessibleModulesAsync()
        {
            const string cacheKey = "_PermSvc_AccessibleModules";
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return new HashSet<string>();

            // Return cached value for this request
            if (ctx.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> hit)
                return hit;

            var user = ctx.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                ctx.Items[cacheKey] = new HashSet<string>();
                return new HashSet<string>();
            }

            // Admins always have full access
            if (user.IsInRole("Admin"))
            {
                var all = AppModules.All.Select(m => m.Key).ToHashSet();
                ctx.Items[cacheKey] = all;
                return all;
            }

            var userId = _userManager.GetUserId(user);
            var perms = await _context.UserPermissions
                .Where(p => p.UserId == userId && p.CanAccess)
                .Select(p => p.Module)
                .ToListAsync();

            // No permissions defined = full access (default for existing users)
            HashSet<string> result = perms.Any()
                ? perms.ToHashSet()
                : AppModules.All.Select(m => m.Key).ToHashSet();

            // Apply department-based filtering: each department sees only its own invoice type
            var appUser = await _userManager.FindByIdAsync(userId!);
            if (appUser?.UserDepartment == "„”«Ã")
                result.Remove("BarberInvoice");
            else if (appUser?.UserDepartment == "Õ·«ﬁ…")
                result.Remove("MassageInvoice");

            // Employees cannot create product invoices
            var userClaims = _httpContextAccessor.HttpContext!.User;
            if (userClaims.IsInRole("Employee"))
                result.Remove("ProductInvoice");

            ctx.Items[cacheKey] = result;
            return result;
        }

        public async Task<bool> HasAccessAsync(string module)
        {
            var modules = await GetAccessibleModulesAsync();
            return modules.Contains(module);
        }
    }
}