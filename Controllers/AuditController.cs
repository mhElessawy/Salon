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
            string? actionFilter,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1)
        {
            int pageSize = 50;

            var allUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            ViewBag.AllUsers = allUsers;

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
            {
                // جمع كل الأسماء الممكنة للمستخدم (FullName + Email + UserName)
                var targetUser = allUsers.FirstOrDefault(u => u.Id == userId);
                var nameVariants = new List<string>();
                if (!string.IsNullOrEmpty(targetUser?.FullName)) nameVariants.Add(targetUser.FullName);
                if (!string.IsNullOrEmpty(targetUser?.Email)) nameVariants.Add(targetUser.Email);
                if (!string.IsNullOrEmpty(targetUser?.UserName)) nameVariants.Add(targetUser.UserName);

                query = nameVariants.Any()
                    ? query.Where(l => l.UserId == userId || nameVariants.Contains(l.UserName))
                    : query.Where(l => l.UserId == userId);

                ViewBag.SelectedUserName = targetUser?.FullName;
            }
            else if (!string.IsNullOrEmpty(userName))
            {
                query = query.Where(l => l.UserName.Contains(userName));
                ViewBag.SelectedUserName = userName;
            }

            if (!string.IsNullOrEmpty(module))
                query = query.Where(l => l.Module == module);

            if (!string.IsNullOrEmpty(actionFilter))
                query = query.Where(l => l.Action == actionFilter);

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

            ViewBag.UserId = userId;
            ViewBag.UserName = ViewBag.SelectedUserName ?? userName;
            ViewBag.Module = module;
            ViewBag.Action = actionFilter;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            var knownModules = new List<string>
            {
                "Departments", "Packages", "Attendance", "Services", "Salaries",
                "Advances", "Shifts", "Customers", "Suppliers", "Employees",
                "المبيعات", "Inventory", "Users", "Expenses", "Appointments",
                "System", "Service Categories"
            };
            var dbModules = await _context.AuditLogs.Select(l => l.Module).Distinct().ToListAsync();
            ViewBag.Modules = knownModules.Union(dbModules).OrderBy(m => m).ToList();

            var knownActions = new List<string>
            {
                "Add", "Close", "Cancel", "Use", "Consumption", "Receiving",
                "Check-Out", "Depreciation", "Sale", "Login", "Edit", "Assign",
                "Delete", "Check-In", "خصم", "Pay", "Return", "Open", "Approve"
            };
            var dbActions = await _context.AuditLogs.Select(l => l.Action).Distinct().ToListAsync();
            ViewBag.Actions = knownActions.Union(dbActions).OrderBy(a => a).ToList();

            return View(logs);
        }
    }
}