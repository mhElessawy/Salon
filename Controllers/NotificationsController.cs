using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
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
            var important = items.Count(n => n.Category == "مهمة");
            return Json(new { total = items.Count, important });
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

            // 4. Late attendance today
            var todayAttendances = await _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.AttendanceDate == today && a.CheckIn.HasValue)
                .ToListAsync();

            var lateThreshold = new TimeSpan(9, 30, 0);
            foreach (var att in todayAttendances.Where(a => a.CheckIn > lateThreshold))
            {
                list.Add(new NotificationItem
                {
                    Type = "late",
                    Category = "تشغيلية",
                    Title = "تأخر تسجيل حضور",
                    SubTitle = $"الموظف: {att.Employee?.FullName ?? "غير محدد"}",
                    Body = $"وقت الحضور: {att.CheckIn:hh\\:mm}",
                    IconClass = "fas fa-clock",
                    IconBg = "#fd7e14",
                    Date = today.Add(att.CheckIn!.Value),
                    ActionUrl = Url.Action("Index", "Attendance"),
                    ActionText = "عرض الموظف"
                });
            }

            // 5. Absent employees (if attendance exists for today — shift is open)
            if (todayAttendances.Any())
            {
                var presentIds = todayAttendances.Select(a => a.EmployeeId).ToHashSet();
                var absentEmployees = await _context.Employees
                    .Where(e => e.IsActive && !presentIds.Contains(e.Id))
                    .ToListAsync();

                foreach (var emp in absentEmployees)
                {
                    list.Add(new NotificationItem
                    {
                        Type = "absent",
                        Category = "تشغيلية",
                        Title = "موظف غائب",
                        SubTitle = $"الموظف: {emp.FullName}",
                        Body = "لم يسجل حضوره اليوم",
                        IconClass = "fas fa-user-times",
                        IconBg = "#6c757d",
                        Date = today,
                        ActionUrl = Url.Action("Index", "Attendance"),
                        ActionText = "عرض الحضور"
                    });
                }
            }

            return list.OrderByDescending(n => n.Date).ToList();
        }
    }
}
