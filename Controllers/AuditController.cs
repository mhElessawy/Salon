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

            // When userId is given: match by UserId OR by UserName (covers logs saved before UserId was stored)
            if (!string.IsNullOrEmpty(userId))
            {
                var allUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
                var targetUser = allUsers.FirstOrDefault(u => u.Id == userId);
                var targetName = targetUser?.FullName ?? "";
                query = query.Where(l => l.UserId == userId || l.UserName == targetName);
                ViewBag.AllUsers = allUsers;
            }
            else
            {
                ViewBag.AllUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
                if (!string.IsNullOrEmpty(userName))
                    query = query.Where(l => l.UserName.Contains(userName));
            }

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

            // Resolve display name for selected user
            var allUsersForBag = ViewBag.AllUsers as List<ApplicationUser>
                ?? await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();

            string? selectedUserName = userName;
            if (!string.IsNullOrEmpty(userId))
            {
                var selectedUser = allUsersForBag.FirstOrDefault(u => u.Id == userId);
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

            if (ViewBag.AllUsers == null)
                ViewBag.AllUsers = allUsersForBag;

            ViewBag.Modules = await _context.AuditLogs
                .Select(l => l.Module).Distinct().OrderBy(m => m).ToListAsync();

            ViewBag.Actions = await _context.AuditLogs
                .Select(l => l.Action).Distinct().OrderBy(a => a).ToListAsync();

            return View(logs);
        }
    }
}