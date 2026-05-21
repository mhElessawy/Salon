using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            string? userId,
            string? userName,
            string? module,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1)
        {
            int pageSize = 50;

            var query = _context.AuditLogs.AsQueryable();

            // Filter by userId (preferred, exact match)
            if (!string.IsNullOrEmpty(userId))
                query = query.Where(l => l.UserId == userId);
            // Fallback: filter by userName text (partial match)
            else if (!string.IsNullOrEmpty(userName))
                query = query.Where(l => l.UserName.Contains(userName));

            if (!string.IsNullOrEmpty(module))
                query = query.Where(l => l.Module == module);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(l => l.Action == action);

            if (dateFrom.HasValue)
                query = query.Where(l => l.CreatedAt >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(l => l.CreatedAt < dateTo.Value.AddDays(1));

            int total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // All users for dropdown
            var allUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();

            // Resolve selected user name for display
            string? selectedUserName = userName;
            if (!string.IsNullOrEmpty(userId))
            {
                var selectedUser = allUsers.FirstOrDefault(u => u.Id == userId);
                selectedUserName = selectedUser?.FullName;
            }

            ViewBag.UserId = userId;
            ViewBag.UserName = selectedUserName;
            ViewBag.Module = module;
            ViewBag.Action = action;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            ViewBag.AllUsers = allUsers;

            ViewBag.Modules = await _context.AuditLogs
                .Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync();

            ViewBag.Actions = await _context.AuditLogs
                .Select(l => l.Action).Distinct().OrderBy(a => a).ToListAsync();

            return View(logs);
        }
    }
}