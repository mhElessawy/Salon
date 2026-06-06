using Microsoft.AspNetCore.Identity;
using Salon.Data;
using Salon.Models;

namespace Salon.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string module, string? description = null, int? entityId = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditService(ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task LogAsync(string action, string module, string? description = null, int? entityId = null)
        {
            var ctx = _httpContextAccessor.HttpContext;
            var user = ctx?.User;

            string userId = string.Empty;
            string userName = "Unknown";

            if (user?.Identity?.IsAuthenticated == true)
            {
                userId = _userManager.GetUserId(user) ?? string.Empty;
                var appUser = await _userManager.GetUserAsync(user);
                userName = appUser?.FullName ?? appUser?.UserName ?? user.Identity.Name ?? "Unknown";
            }

            string? ip = ctx?.Connection?.RemoteIpAddress?.ToString();

            var log = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                Module = module,
                Description = description,
                EntityId = entityId,
                IpAddress = ip,
                CreatedAt = DateTime.Now
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}