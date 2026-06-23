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
            var userId = _userManager.GetUserId(User);
            var readSet = _cache.TryGetValue($"notif_read_{userId}", out HashSet<string>? rk) ? rk : new HashSet<string>();
            foreach (var item in items)
                item.IsRead = readSet != null && readSet.Contains(item.Key);
            return PartialView("_Panel", items);
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var items = await BuildNotificationsAsync();
            var userId = _userManager.GetUserId(User);
            var readSet = _cache.TryGetValue($"notif_read_{userId}", out HashSet<string>? rk) ? rk : new HashSet<string>();
            var unread = items.Where(n => !(readSet?.Contains(n.Key) ?? false)).ToList();
            return Json(new { total = unread.Count, important = unread.Count(n => n.Category == "مهمة") });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkRead([FromForm] string key)
        {
            var userId = _userManager.GetUserId(User);
            var cacheKey = $"notif_read_{userId}";
            var readSet = _cache.GetOrCreate(cacheKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(3);
                return new HashSet<string>();
            }) ?? new HashSet<string>();
            readSet.Add(key);
            _cache.Set(cacheKey, readSet, TimeSpan.FromDays(3));

            var items = await BuildNotificationsAsync();
            var unread = items.Where(n => !readSet.Contains(n.Key)).ToList();
            return Json(new { total = unread.Count, important = unread.Count(n => n.Category == "مهمة") });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult MarkSeen()
        {
            var userId = _userManager.GetUserId(User);
            _cache.Set($"notif_seen_{userId}", DateTime.Now, TimeSpan.FromDays(1));
            return Ok();
        }

        private static string NotifKey(string type, DateTime date, string sub)
        {
            unchecked
            {
                long h = 17;
                foreach (char c in type) h = h * 31 + c;
                h = h * 31 + date.Year * 366 * 24 * 60 + date.DayOfYear * 24 * 60 + date.Hour * 60 + date.Minute;
                foreach (char c in sub) h = h * 31 + c;
                return ((ulong)h).ToString("x");
            }
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
                    var sub1 = $"الكاشير: {shift.CashierName ?? "غير محدد"}";
                    list.Add(new NotificationItem
                    {
                        Type = "shift-diff",
                        Category = "مهمة",
                        Title = "فرق في الصندوق",
                        SubTitle = sub1,
                        Body = $"المتوقع: {expected:N3} | الموجود: {shift.ClosingBalance:N3} | الفرق: {diff:N3} د.ك",
                        IconClass = "fas fa-exclamation-triangle",
                        IconBg = "#dc3545",
                        Date = notifDate,
                        ActionUrl = Url.Action("Index", "Shifts"),
                        ActionText = "عرض التفاصيل",
                        Key = NotifKey("shift-diff", notifDate, sub1)
                    });
                }
                else
                {
                    var sub2 = $"الشفت: {shift.Name}";
                    list.Add(new NotificationItem
                    {
                        Type = "shift-close",
                        Category = "تشغيلية",
                        Title = "تم إغلاق الشفت",
                        SubTitle = sub2,
                        Body = $"الرصيد: {shift.ClosingBalance:N3} د.ك",
                        IconClass = "fas fa-check-circle",
                        IconBg = "#198754",
                        Date = notifDate,
                        ActionUrl = Url.Action("Reports", "Shifts"),
                        ActionText = "عرض التقرير",
                        Key = NotifKey("shift-close", notifDate, sub2)
                    });
                }
            }

            // 2. Products at or below minimum stock
            var lowStock = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity <= p.MinStockLevel)
                .OrderBy(p => p.StockQuantity).ToListAsync();

            foreach (var prod in lowStock)
            {
                var subProd = $"المنتج: {prod.Name}";
                list.Add(new NotificationItem
                {
                    Type = "low-stock",
                    Category = "تشغيلية",
                    Title = "منتج وصل الحد الأدنى",
                    SubTitle = subProd,
                    Body = $"الكمية المتبقية: {prod.StockQuantity} قطعة | الحد الأدنى: {prod.MinStockLevel}",
                    IconClass = "fas fa-box",
                    IconBg = "#7c3aed",
                    Date = today,
                    ActionUrl = Url.Action("Index", "Inventory"),
                    ActionText = "عرض المنتج",
                    Key = NotifKey("low-stock", today, subProd)
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
                    ActionText = "عرض المصروف",
                    Key = NotifKey("expense", exp.CreatedAt, exp.Description ?? "")
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
                    ActionText = "عرض الموعد",
                    Key = NotifKey("appointment", appt.AppointmentDate, appt.Customer?.FullName ?? "")
                });
            }

            // 5. Recent advances (last 2 days)
            var recentAdvances = await _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => a.AdvanceDate >= today.AddDays(-2))
                .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync();

            foreach (var adv in recentAdvances)
            {
                var subAdv = $"الموظف: {adv.Employee?.FullName ?? "غير محدد"}";
                list.Add(new NotificationItem
                {
                    Type = "advance-new",
                    Category = "مالية",
                    Title = "سلفة جديدة",
                    SubTitle = subAdv,
                    Body = $"المبلغ: {adv.Amount:N3} د.ك | الحالة: {adv.Status}",
                    IconClass = "fas fa-hand-holding-usd",
                    IconBg = "#7c3aed",
                    Date = adv.CreatedAt,
                    ActionUrl = Url.Action("Index", "Advances"),
                    ActionText = "عرض السلف",
                    Key = NotifKey("advance-new", adv.CreatedAt, subAdv)
                });
            }

            // 6. Pending (unpaid) advances
            var pendingAdvances = await _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => (a.Status == "معلق" || a.Status == "موافق") && a.Amount > a.DeductedAmount)
                .OrderByDescending(a => a.AdvanceDate).Take(10).ToListAsync();

            foreach (var adv in pendingAdvances)
            {
                var remaining = adv.Amount - adv.DeductedAmount;
                var subPend = $"الموظف: {adv.Employee?.FullName ?? "غير محدد"}";
                list.Add(new NotificationItem
                {
                    Type = "advance-pending",
                    Category = "مهمة",
                    Title = "سلفة غير مسددة",
                    SubTitle = subPend,
                    Body = $"المتبقي: {remaining:N3} د.ك | من أصل: {adv.Amount:N3} د.ك",
                    IconClass = "fas fa-exclamation-circle",
                    IconBg = "#f59e0b",
                    Date = adv.AdvanceDate,
                    ActionUrl = Url.Action("Index", "Advances"),
                    ActionText = "عرض السلف",
                    Key = NotifKey("advance-pending", adv.AdvanceDate, subPend)
                });
            }

            return list.OrderByDescending(n => n.Date).ToList();
        }
    }
}