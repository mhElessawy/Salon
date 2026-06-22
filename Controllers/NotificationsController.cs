using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, IMemoryCache cache, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _cache = cache;
            _userManager = userManager;
        }

        public async Task<IActionResult> Panel()
        {
            var items = await BuildNotificationsAsync();
            return PartialView("_Panel", items);
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var items = await BuildNotificationsAsync();
            var userId = _userManager.GetUserId(User);
            var seenAt = _cache.TryGetValue($"notif_seen_{userId}", out DateTime seen) ? seen : DateTime.MinValue;

            var unseen = items.Count(n => n.Date > seenAt);
            var important = items.Count(n => n.Category == "مهمة" && n.Date > seenAt);
            return Json(new { total = unseen, important });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult MarkSeen()
        {
            var userId = _userManager.GetUserId(User);
            _cache.Set($"notif_seen_{userId}", DateTime.Now, TimeSpan.FromDays(1));
            return Ok();
        }

        private async Task<List<NotificationItem>> BuildNotificationsAsync()
        {
            var list = new List<NotificationItem>();
            var today = DateTime.Today;
            var weekAgo = today.AddDays(-7);

            // 1. Closed shifts — cash difference or normal close
            var closedShifts = await _context.Shifts
                .Where(s => s.Status == "مغلق" && s.ShiftDate >= weekAgo && s.ClosingBalance.HasValue)
                .OrderByDescending(s => s.ShiftDate).ThenByDescending(s => s.CreatedAt)
                .Take(10).ToListAsync();

            foreach (var shift in closedShifts)
            {
                var dayStart = shift.ShiftDate.Date;
                var dayEnd = dayStart.AddDays(1);

                var daySales = await _context.Sales
                    .Where(s => s.SaleDate >= dayStart && s.SaleDate < dayEnd && s.Status == "مكتمل")
                    .ToListAsync();

                var cashSales = daySales.Sum(s =>
                    s.CashAmount.HasValue ? s.CashAmount.Value :
                    (s.PaymentMethod == "نقدي" || s.PaymentMethod == "كاش" ? s.NetAmount : 0m));

                var cashExpenses = await _context.Expenses
                    .Where(e => e.ExpenseDate >= dayStart && e.ExpenseDate < dayEnd &&
                                (e.PaymentMethod == "نقدي" || e.PaymentMethod == "كاش"))
                    .SumAsync(e => e.Amount);

                var expected = shift.OpeningBalance + cashSales - cashExpenses;
                var diff = (shift.ClosingBalance ?? 0) - expected;
                var notifDate = shift.ShiftDate.Date.Add(shift.EndTime ?? TimeSpan.FromHours(23));

                if (Math.Abs(diff) >= 0.001m)
                {
                    list.Add(new NotificationItem
                    {
                        Type = "shift-diff",
                        Category = "مهمة",
                        Title = "فرق في الصندوق",
                        SubTitle = $"الكاشير: {shift.CashierName ?? "غير محدد"}",
                        Body = $"المتوقع: {expected:N3} | الموجود: {shift.ClosingBalance:N3} | الفرق: {diff:N3} د.ك",
                        IconClass = "fas fa-exclamation-triangle",
                        IconBg = "#dc3545",
                        Date = notifDate,
                        ActionUrl = Url.Action("Index", "Shifts"),
                        ActionText = "عرض التفاصيل"
                    });
                }
                else
                {
                    list.Add(new NotificationItem
                    {
                        Type = "shift-close",
                        Category = "تشغيلية",
                        Title = "تم إغلاق الشفت",
                        SubTitle = $"الشفت: {shift.Name}",
                        Body = $"الرصيد: {shift.ClosingBalance:N3} د.ك",
                        IconClass = "fas fa-check-circle",
                        IconBg = "#198754",
                        Date = notifDate,
                        ActionUrl = Url.Action("Reports", "Shifts"),
                        ActionText = "عرض التقرير"
                    });
                }
            }

            // 2. Products at or below minimum stock
            var lowStock = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity <= p.MinStockLevel)
                .OrderBy(p => p.StockQuantity).ToListAsync();

            foreach (var prod in lowStock)
            {
                list.Add(new NotificationItem
                {
                    Type = "low-stock",
                    Category = "تشغيلية",
                    Title = "منتج وصل الحد الأدنى",
                    SubTitle = $"المنتج: {prod.Name}",
                    Body = $"الكمية المتبقية: {prod.StockQuantity} قطعة | الحد الأدنى: {prod.MinStockLevel}",
                    IconClass = "fas fa-box",
                    IconBg = "#7c3aed",
                    Date = today,
                    ActionUrl = Url.Action("Index", "Inventory"),
                    ActionText = "عرض المنتج"
                });
            }

            // 3. Recent expenses (last 2 days)
            var recentExpenses = await _context.Expenses
                .Where(e => e.ExpenseDate >= today.AddDays(-2))
                .OrderByDescending(e => e.CreatedAt).Take(8).ToListAsync();

            foreach (var exp in recentExpenses)
            {
                list.Add(new NotificationItem
                {
                    Type = "expense",
                    Category = "مالية",
                    Title = "تم إدخال مصروف",
                    SubTitle = exp.Description,
                    Body = $"المبلغ: {exp.Amount:N3} د.ك | الفئة: {exp.Category ?? "عامة"}",
                    IconClass = "fas fa-file-invoice-dollar",
                    IconBg = "#0d6efd",
                    Date = exp.CreatedAt,
                    ActionUrl = Url.Action("Index", "Expenses"),
                    ActionText = "عرض المصروف"
                });
            }

            // 4. Appointments — overdue today, upcoming today, tomorrow
            var now = DateTime.Now;
            var tomorrow = today.AddDays(1);
            var dayAfterTomorrow = today.AddDays(2);

            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => a.AppointmentDate >= today && a.AppointmentDate < dayAfterTomorrow
                         && a.Status == "مجدول")
                .OrderBy(a => a.AppointmentDate)
                .Take(15)
                .ToListAsync();

            foreach (var appt in appointments)
            {
                bool isOverdue = appt.AppointmentDate < now;
                bool isTomorrow = appt.AppointmentDate.Date == tomorrow;

                list.Add(new NotificationItem
                {
                    Type = "appointment",
                    Category = isOverdue ? "مهمة" : "تشغيلية",
                    Title = isOverdue ? "موعد فائت" : (isTomorrow ? "موعد الغد" : "موعد اليوم"),
                    SubTitle = $"العميل: {appt.Customer?.FullName ?? "غير محدد"}",
                    Body = $"الوقت: {appt.AppointmentDate:hh:mm tt}" +
                           (appt.Employee != null ? $" | الموظف: {appt.Employee.FullName}" : ""),
                    IconClass = isOverdue ? "fas fa-calendar-times" : "fas fa-calendar-check",
                    IconBg = isOverdue ? "#dc3545" : (isTomorrow ? "#0d6efd" : "#F7941D"),
                    Date = appt.AppointmentDate,
                    ActionUrl = Url.Action("Index", "Appointments"),
                    ActionText = "عرض الموعد"
                });
            }

            return list.OrderByDescending(n => n.Date).ToList();
        }
    }
}
