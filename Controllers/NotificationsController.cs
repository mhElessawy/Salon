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

        public async Task<IActionResult> Panel([FromQuery] string lang = "ar")
        {
            var items = await BuildNotificationsAsync();
            var userId = _userManager.GetUserId(User);
            var readSet = _cache.TryGetValue($"notif_read_{userId}", out HashSet<string>? rk) ? rk : new HashSet<string>();
            foreach (var item in items)
                item.IsRead = readSet != null && readSet.Contains(item.Key);
            ViewBag.Lang = lang;
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

            // موظف مرتبط بحساب المستخدم الحالي (يشوف تنبيهاته الخاصة فقط) — الأدمن/المدير بدون ربط يشوف تنبيهات الجميع
            var currentUser = await _userManager.GetUserAsync(User);
            int? myEmployeeId = currentUser?.LinkedEmployeeId;
            var myRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
            bool viewerIsManager = myRoles.Contains("Admin") || myRoles.Contains("Manager");
            bool viewerIsCashier = viewerIsManager || myRoles.Contains("Cashier");

            // 1. Closed shifts — cash difference or normal close
            var closedShifts = await _context.Shifts
                .Where(s => !s.IsClosureRecord && s.Status == "مغلق" && s.ShiftDate >= weekAgo && s.ClosingBalance.HasValue)
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
                    (s.PaymentMethod == "نقدي" || s.PaymentMethod == "كاش" || s.PaymentMethod == "Cash" ? s.NetAmount : 0m));

                var cashExpenses = await _context.Expenses
                    .Where(e => e.ExpenseDate >= dayStart && e.ExpenseDate < dayEnd &&
                                (e.PaymentMethod == "نقدي" || e.PaymentMethod == "كاش" || e.PaymentMethod == "Cash"))
                    .SumAsync(e => e.Amount);

                var expected = shift.OpeningBalance + cashSales - cashExpenses;
                var diff = (shift.ClosingBalance ?? 0) - expected;
                var notifDate = shift.ShiftDate.Date.Add(shift.EndTime ?? TimeSpan.FromHours(23));

                if (Math.Abs(diff) >= 0.001m)
                {
                    var sub1 = $"الكاشير: {shift.CashierName ?? "غير محدد"}";
                    var sub1En = $"Cashier: {shift.CashierName ?? "Unknown"}";
                    list.Add(new NotificationItem
                    {
                        Type = "shift-diff",
                        Category = "مهمة",
                        Title = "فرق في الصندوق",
                        TitleEn = "Cash Register Difference",
                        SubTitle = sub1,
                        SubTitleEn = sub1En,
                        Body = $"المتوقع: {expected:N3} | الموجود: {shift.ClosingBalance:N3} | الفرق: {diff:N3} د.ك",
                        BodyEn = $"Expected: {expected:N3} | Found: {shift.ClosingBalance:N3} | Diff: {diff:N3} KD",
                        IconClass = "fas fa-exclamation-triangle",
                        IconBg = "#dc3545",
                        Date = notifDate,
                        ActionUrl = Url.Action("Index", "Shifts"),
                        ActionText = "عرض التفاصيل",
                        ActionTextEn = "View Details",
                        Key = NotifKey("shift-diff", notifDate, sub1)
                    });
                }
                else
                {
                    var sub2 = $"الشفت: {shift.Name}";
                    var sub2En = $"Shift: {shift.Name}";
                    list.Add(new NotificationItem
                    {
                        Type = "shift-close",
                        Category = "تشغيلية",
                        Title = "تم إغلاق الشفت",
                        TitleEn = "Shift Closed",
                        SubTitle = sub2,
                        SubTitleEn = sub2En,
                        Body = $"الرصيد: {shift.ClosingBalance:N3} د.ك",
                        BodyEn = $"Balance: {shift.ClosingBalance:N3} KD",
                        IconClass = "fas fa-check-circle",
                        IconBg = "#198754",
                        Date = notifDate,
                        ActionUrl = Url.Action("Reports", "Shifts"),
                        ActionText = "عرض التقرير",
                        ActionTextEn = "View Report",
                        Key = NotifKey("shift-close", notifDate, sub2)
                    });
                }
            }

            // 1b. نتيجة اعتماد/إغلاق اليومية — يظهر فقط للمدير والكاشير، ولا يظهر لأي صلاحية أخرى
            if (viewerIsCashier)
            {
                var closureUpdates = await _context.Shifts
                    .Where(s => s.IsClosureRecord && s.ShiftDate >= weekAgo && (
                        s.ApprovalStatus == Shift.ApprovalStatuses.Approved
                        || s.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy
                        || s.ApprovalStatus == Shift.ApprovalStatuses.AutoClosedUnapproved))
                    .OrderByDescending(s => s.ShiftDate)
                    .Take(10).ToListAsync();

                foreach (var shift in closureUpdates)
                {
                    var dept = shift.ClosureDepartment ?? Shift.ClosureDepartments.Shared;
                    if (shift.ApprovalStatus == Shift.ApprovalStatuses.AutoClosedUnapproved)
                    {
                        var notifDate = shift.AutoClosedAt ?? shift.ShiftDate;
                        var sub = $"يومية {dept} {shift.ShiftDate:yyyy/MM/dd}";
                        list.Add(new NotificationItem
                        {
                            Type = "closure-auto-closed",
                            Category = "مهمة",
                            Title = "لم يتم اعتماد اليومية",
                            TitleEn = "Daily Closure Not Approved",
                            SubTitle = sub,
                            SubTitleEn = $"Closure {dept} {shift.ShiftDate:yyyy/MM/dd}",
                            Body = "لم يتم اعتماد اليومية من الكاشير وتم إغلاقها آلياً، وهي بانتظار المراجعة.",
                            BodyEn = "The cashier did not approve the daily closure; it was auto-closed and awaits review.",
                            IconClass = "fas fa-exclamation-triangle",
                            IconBg = "#ffc107",
                            Date = notifDate,
                            ActionUrl = Url.Action("Review", "DailyClosure", new { date = shift.ShiftDate.ToString("yyyy-MM-dd"), dept }),
                            ActionText = "مراجعة اليومية",
                            ActionTextEn = "Review Closure",
                            Key = NotifKey("closure-auto-closed", notifDate, sub)
                        });
                    }
                    else
                    {
                        bool hasDiscrepancy = shift.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy;
                        var notifDate = shift.ApprovedAt ?? shift.ShiftDate;
                        var sub = $"{dept} — المعتمد: {shift.ApprovedByUserName ?? "غير معروف"}";
                        list.Add(new NotificationItem
                        {
                            Type = "closure-approved",
                            Category = hasDiscrepancy ? "مهمة" : "تشغيلية",
                            Title = "تم اعتماد وإغلاق اليومية بنجاح",
                            TitleEn = "Daily Closure Approved",
                            SubTitle = sub,
                            SubTitleEn = $"{dept} — Approved by: {shift.ApprovedByUserName ?? "Unknown"}",
                            Body = (hasDiscrepancy ? "🟠 توجد فروقات وتم تسجيلها. " : "🟢 اليومية سليمة. ")
                                + $"بتاريخ {shift.ShiftDate:yyyy/MM/dd} — {notifDate:HH:mm}",
                            BodyEn = (hasDiscrepancy ? "Discrepancies were found and recorded. " : "Closure is balanced. ")
                                + $"Date {shift.ShiftDate:yyyy/MM/dd} — {notifDate:HH:mm}",
                            IconClass = hasDiscrepancy ? "fas fa-exclamation-circle" : "fas fa-check-circle",
                            IconBg = hasDiscrepancy ? "#dc3545" : "#198754",
                            Date = notifDate,
                            ActionUrl = Url.Action("Review", "DailyClosure", new { date = shift.ShiftDate.ToString("yyyy-MM-dd"), dept }),
                            ActionText = "عرض التفاصيل",
                            ActionTextEn = "View Details",
                            Key = NotifKey("closure-approved", notifDate, sub)
                        });
                    }
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
                    TitleEn = "Product at Minimum Stock",
                    SubTitle = subProd,
                    SubTitleEn = $"Product: {prod.Name}",
                    Body = $"الكمية المتبقية: {prod.StockQuantity} قطعة | الحد الأدنى: {prod.MinStockLevel}",
                    BodyEn = $"Remaining: {prod.StockQuantity} pcs | Min Level: {prod.MinStockLevel}",
                    IconClass = "fas fa-box",
                    IconBg = "#7c3aed",
                    Date = today,
                    ActionUrl = Url.Action("Index", "Inventory"),
                    ActionText = "عرض المنتج",
                    ActionTextEn = "View Product",
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
                    TitleEn = "Expense Added",
                    SubTitle = exp.Description ?? "",
                    SubTitleEn = exp.Description ?? "",
                    Body = $"المبلغ: {exp.Amount:N3} د.ك | الفئة: {exp.Category ?? "عامة"}",
                    BodyEn = $"Amount: {exp.Amount:N3} KD | Category: {exp.Category ?? "General"}",
                    IconClass = "fas fa-file-invoice-dollar",
                    IconBg = "#0d6efd",
                    Date = exp.CreatedAt,
                    ActionUrl = Url.Action("Index", "Expenses"),
                    ActionText = "عرض المصروف",
                    ActionTextEn = "View Expense",
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
                    TitleEn = isOverdue ? "Missed Appointment" : (isTomorrow ? "Tomorrow's Appointment" : "Today's Appointment"),
                    SubTitle = $"العميل: {appt.Customer?.FullName ?? "غير محدد"}",
                    SubTitleEn = $"Customer: {appt.Customer?.FullName ?? "Unknown"}",
                    Body = $"الوقت: {appt.AppointmentDate:hh:mm tt}" +
                           (appt.Employee != null ? $" | الموظف: {appt.Employee.FullName}" : ""),
                    BodyEn = $"Time: {appt.AppointmentDate:hh:mm tt}" +
                             (appt.Employee != null ? $" | Employee: {appt.Employee.FullName}" : ""),
                    IconClass = isOverdue ? "fas fa-calendar-times" : "fas fa-calendar-check",
                    IconBg = isOverdue ? "#dc3545" : (isTomorrow ? "#0d6efd" : "#F7941D"),
                    Date = appt.AppointmentDate,
                    ActionUrl = Url.Action("Index", "Appointments"),
                    ActionText = "عرض الموعد",
                    ActionTextEn = "View Appointment",
                    Key = NotifKey("appointment", appt.AppointmentDate, appt.Customer?.FullName ?? "")
                });
            }

            // 5. طلبات السلف الجديدة (تنتظر موافقة المدير) — تصل للمدير فقط
            if (viewerIsManager)
            {
                var pendingRequests = await _context.EmployeeAdvances
                    .Include(a => a.Employee)
                    .Where(a => a.Status == EmployeeAdvance.Statuses.PendingApproval && a.CreatedAt >= today.AddDays(-7))
                    .OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync();

                foreach (var adv in pendingRequests)
                {
                    var subAdv = $"الموظف: {adv.Employee?.FullName ?? "غير محدد"}";
                    list.Add(new NotificationItem
                    {
                        Type = "advance-new",
                        Category = "مهمة",
                        Title = "طلب سلفة جديد",
                        TitleEn = "New Advance Request",
                        SubTitle = subAdv,
                        SubTitleEn = $"Employee: {adv.Employee?.FullName ?? "Unknown"}",
                        Body = $"المبلغ: {adv.Amount:N3} د.ك | السبب: {adv.Reason ?? "-"} | ينتظر الموافقة",
                        BodyEn = $"Amount: {adv.Amount:N3} KD | Awaiting Approval",
                        IconClass = "fas fa-hand-holding-usd",
                        IconBg = "#F7941D",
                        Date = adv.CreatedAt,
                        ActionUrl = Url.Action("Index", "Advances"),
                        ActionText = "مراجعة الطلب",
                        ActionTextEn = "Review Request",
                        Key = NotifKey("advance-new", adv.CreatedAt, subAdv)
                    });
                }
            }

            // 5a-i. سلف معتمدة بانتظار صرف الكاشير — تصل للكاشير (والمدير)
            if (viewerIsCashier)
            {
                var cashierQueue = await _context.EmployeeAdvances
                    .Include(a => a.Employee)
                    .Where(a => a.Status == EmployeeAdvance.Statuses.AwaitingCashierPayout && a.DecisionAt >= weekAgo)
                    .OrderByDescending(a => a.DecisionAt).Take(10).ToListAsync();

                foreach (var adv in cashierQueue)
                {
                    var empName = adv.Employee?.FullName ?? "غير محدد";
                    var subCq = $"الموظف: {empName}";
                    var decidedAt = adv.DecisionAt ?? adv.CreatedAt;
                    list.Add(new NotificationItem
                    {
                        Type = "advance-cashier-queue",
                        Category = "مهمة",
                        Title = "سلفة بانتظار الصرف",
                        TitleEn = "Advance Awaiting Cash Payout",
                        SubTitle = subCq,
                        SubTitleEn = $"Employee: {empName}",
                        Body = $"تمت الموافقة على صرف سلفة للموظف ({empName}) بقيمة ({adv.Amount:N3} د.ك)، يرجى صرف المبلغ وتأكيد عملية التسليم. المدير: {adv.ManagerName ?? "-"}",
                        BodyEn = $"Approved advance for ({empName}) of ({adv.Amount:N3} KD) — please pay out and confirm delivery. Manager: {adv.ManagerName ?? "-"}",
                        IconClass = "fas fa-money-bill-wave",
                        IconBg = "#198754",
                        Date = decidedAt,
                        ActionUrl = Url.Action("Index", "Advances"),
                        ActionText = "صرف السلفة",
                        ActionTextEn = "Pay Out Advance",
                        Key = NotifKey("advance-cashier-queue", decidedAt, subCq)
                    });
                }
            }

            // 5a-ii. سلف معتمدة بانتظار التحويل البنكي — تصل للمدير/المستخدم المخوَّل
            if (viewerIsManager)
            {
                var bankQueue = await _context.EmployeeAdvances
                    .Include(a => a.Employee)
                    .Where(a => a.Status == EmployeeAdvance.Statuses.AwaitingBankTransfer && a.DecisionAt >= weekAgo)
                    .OrderByDescending(a => a.DecisionAt).Take(10).ToListAsync();

                foreach (var adv in bankQueue)
                {
                    var empName = adv.Employee?.FullName ?? "غير محدد";
                    var subBq = $"الموظف: {empName}";
                    var decidedAt = adv.DecisionAt ?? adv.CreatedAt;
                    list.Add(new NotificationItem
                    {
                        Type = "advance-bank-queue",
                        Category = "مهمة",
                        Title = "سلفة بانتظار التحويل البنكي",
                        TitleEn = "Advance Awaiting Bank Transfer",
                        SubTitle = subBq,
                        SubTitleEn = $"Employee: {empName}",
                        Body = $"تمت الموافقة على تحويل سلفة للموظف ({empName}) بقيمة ({adv.Amount:N3} د.ك)، يرجى تنفيذ التحويل وتأكيده",
                        BodyEn = $"Approved bank-transfer advance for ({empName}) of ({adv.Amount:N3} KD) — please execute and confirm the transfer",
                        IconClass = "fas fa-university",
                        IconBg = "#0d6efd",
                        Date = decidedAt,
                        ActionUrl = Url.Action("Index", "Advances"),
                        ActionText = "تنفيذ التحويل",
                        ActionTextEn = "Execute Transfer",
                        Key = NotifKey("advance-bank-queue", decidedAt, subBq)
                    });
                }
            }

            // 5a-iii. نتيجة الطلب لصاحبه: رفض / صرف نقدي / تحويل بنكي
            {
                var decidedAdvancesQuery = _context.EmployeeAdvances
                    .Include(a => a.Employee)
                    .Where(a => (a.Status == EmployeeAdvance.Statuses.Rejected && a.DecisionAt >= weekAgo)
                             || (a.Status == EmployeeAdvance.Statuses.Disbursed && a.DisbursedAt >= weekAgo)
                             || (a.Status == EmployeeAdvance.Statuses.Transferred && a.DisbursedAt >= weekAgo));
                if (myEmployeeId.HasValue)
                    decidedAdvancesQuery = decidedAdvancesQuery.Where(a => a.EmployeeId == myEmployeeId.Value);
                var decidedAdvances = await decidedAdvancesQuery.OrderByDescending(a => a.DisbursedAt ?? a.DecisionAt).Take(10).ToListAsync();

                foreach (var adv in decidedAdvances)
                {
                    var empName = adv.Employee?.FullName ?? "غير محدد";
                    bool forEmployee = !myEmployeeId.HasValue || adv.EmployeeId == myEmployeeId.Value;
                    // تنبيه المدير بإتمام الصرف النقدي، وتنبيه الكاشير للعلم فقط بعد التحويل البنكي
                    bool forAudience = adv.Status switch
                    {
                        var s when s == EmployeeAdvance.Statuses.Disbursed => forEmployee || viewerIsManager,
                        var s when s == EmployeeAdvance.Statuses.Transferred => forEmployee || viewerIsCashier,
                        _ => forEmployee
                    };
                    if (!forAudience) continue;

                    var (type, title, titleEn, icon, iconBg, body, bodyEn) = adv.Status switch
                    {
                        var s when s == EmployeeAdvance.Statuses.Rejected => (
                            "advance-rejected", "تم رفض طلب السلفة", "Advance Request Rejected",
                            "fas fa-times-circle", "#dc3545",
                            $"طلب سلفة الموظف ({empName}) بقيمة ({adv.Amount:N3} د.ك) — السبب: {adv.RejectionReason ?? "-"}",
                            $"Advance request for ({empName}) of ({adv.Amount:N3} KD) rejected — Reason: {adv.RejectionReason ?? "-"}"),
                        var s when s == EmployeeAdvance.Statuses.Disbursed => (
                            "advance-disbursed", "تم صرف السلفة", "Advance Paid Out",
                            "fas fa-hand-holding-usd", "#198754",
                            $"تم تسليم مبلغ السلفة ({adv.Amount:N3} د.ك) للموظف ({empName}) نقداً بواسطة الكاشير: {adv.CashierName ?? "-"}",
                            $"Cash advance of ({adv.Amount:N3} KD) paid out to ({empName}) by cashier: {adv.CashierName ?? "-"}"),
                        _ => (
                            "advance-transferred", "تم تحويل السلفة بنكياً", "Advance Bank Transfer Completed",
                            "fas fa-university", "#0d6efd",
                            $"تم تحويل مبلغ السلفة ({adv.Amount:N3} د.ك) للموظف ({empName}) بنكياً | مرجع التحويل: {adv.TransferReference ?? "-"}",
                            $"Bank transfer of ({adv.Amount:N3} KD) completed for ({empName}) | Ref: {adv.TransferReference ?? "-"}")
                    };
                    var subDec = $"الموظف: {empName}";
                    var decDate = adv.DisbursedAt ?? adv.DecisionAt ?? adv.CreatedAt;
                    list.Add(new NotificationItem
                    {
                        Type = type,
                        Category = "مهمة",
                        Title = title,
                        TitleEn = titleEn,
                        SubTitle = subDec,
                        SubTitleEn = $"Employee: {empName}",
                        Body = body,
                        BodyEn = bodyEn,
                        IconClass = icon,
                        IconBg = iconBg,
                        Date = decDate,
                        ActionUrl = Url.Action("Index", "Advances"),
                        ActionText = "عرض السلفة",
                        ActionTextEn = "View Advance",
                        Key = NotifKey(type, decDate, subDec)
                    });
                }
            }

            // 5b. طلبات الشراء الجديدة المعلقة (تنتظر موافقة الأدمن)
            var pendingPurchaseRequests = await _context.PurchaseRequests
                .Include(p => p.Employee)
                .Where(p => p.Status == PurchaseRequest.Statuses.Pending && p.CreatedAt >= today.AddDays(-7))
                .OrderByDescending(p => p.CreatedAt).Take(10).ToListAsync();

            foreach (var pr in pendingPurchaseRequests)
            {
                var empName = pr.Employee?.FullName ?? "غير محدد";
                var subPr = $"الموظف: {empName}";
                list.Add(new NotificationItem
                {
                    Type = "purchase-request-new",
                    Category = "مهمة",
                    Title = "طلب شراء جديد",
                    TitleEn = "New Purchase Request",
                    SubTitle = subPr,
                    SubTitleEn = $"Employee: {empName}",
                    Body = $"قيمة تقديرية: {pr.EstimatedAmount:N3} د.ك | ينتظر الموافقة",
                    BodyEn = $"Estimated: {pr.EstimatedAmount:N3} KD | Awaiting Approval",
                    IconClass = "fas fa-shopping-cart",
                    IconBg = "#F7941D",
                    Date = pr.CreatedAt,
                    ActionUrl = Url.Action("Index", "PurchaseRequests"),
                    ActionText = "مراجعة الطلب",
                    ActionTextEn = "Review Request",
                    Key = NotifKey("purchase-request-new", pr.CreatedAt, subPr)
                });
            }

            // 5c. طلبات الشراء التي تمت الموافقة عليها أو رفضها حديثاً (لصاحب الطلب)
            var decidedPurchaseRequestsQuery = _context.PurchaseRequests
                .Include(p => p.Employee)
                .Where(p => (p.Status == PurchaseRequest.Statuses.Approved || p.Status == PurchaseRequest.Statuses.Rejected)
                         && p.CreatedAt >= weekAgo);
            if (myEmployeeId.HasValue)
                decidedPurchaseRequestsQuery = decidedPurchaseRequestsQuery.Where(p => p.EmployeeId == myEmployeeId.Value);
            var decidedPurchaseRequests = await decidedPurchaseRequestsQuery
                .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt).Take(10).ToListAsync();

            foreach (var pr in decidedPurchaseRequests)
            {
                bool approved = pr.Status == PurchaseRequest.Statuses.Approved;
                var decidedDate = pr.ApprovedAt ?? pr.CreatedAt;
                var subDecided = $"طلب شراء #{pr.Id}";
                list.Add(new NotificationItem
                {
                    Type = approved ? "purchase-request-approved" : "purchase-request-rejected",
                    Category = "مهمة",
                    Title = approved ? "تمت الموافقة على طلب الشراء" : "تم رفض طلب الشراء",
                    TitleEn = approved ? "Purchase Request Approved" : "Purchase Request Rejected",
                    SubTitle = subDecided,
                    SubTitleEn = subDecided,
                    Body = approved
                        ? $"بواسطة: {pr.ApprovedByName ?? "-"} | {decidedDate:HH:mm  yyyy/MM/dd}"
                        : $"السبب: {pr.RejectionReason ?? "-"}",
                    BodyEn = approved
                        ? $"By: {pr.ApprovedByName ?? "-"} | {decidedDate:HH:mm  yyyy/MM/dd}"
                        : $"Reason: {pr.RejectionReason ?? "-"}",
                    IconClass = approved ? "fas fa-check-circle" : "fas fa-times-circle",
                    IconBg = approved ? "#198754" : "#dc3545",
                    Date = decidedDate,
                    ActionUrl = Url.Action("Index", "PurchaseRequests"),
                    ActionText = "عرض الطلب",
                    ActionTextEn = "View Request",
                    Key = NotifKey(approved ? "purchase-request-approved" : "purchase-request-rejected", decidedDate, subDecided)
                });
            }

            // 6. Pending (unpaid) advances
            var pendingAdvances = await _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => (a.Status == EmployeeAdvance.Statuses.Disbursed || a.Status == EmployeeAdvance.Statuses.Transferred)
                         && a.Amount > a.DeductedAmount)
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
                    TitleEn = "Unpaid Advance",
                    SubTitle = subPend,
                    SubTitleEn = $"Employee: {adv.Employee?.FullName ?? "Unknown"}",
                    Body = $"المتبقي: {remaining:N3} د.ك | من أصل: {adv.Amount:N3} د.ك",
                    BodyEn = $"Remaining: {remaining:N3} KD | Of: {adv.Amount:N3} KD",
                    IconClass = "fas fa-exclamation-circle",
                    IconBg = "#f59e0b",
                    Date = adv.AdvanceDate,
                    ActionUrl = Url.Action("Index", "Advances"),
                    ActionText = "عرض السلف",
                    ActionTextEn = "View Advances",
                    Key = NotifKey("advance-pending", adv.AdvanceDate, subPend)
                });
            }

            // 7. عملاء لم يزوروا الصالون منذ أكثر من شهر (لكل موظف مسؤول عنهم)
            var inactiveThreshold = today.AddDays(-30);

            var visitSales = await _context.Sales
                .Where(s => s.CustomerId.HasValue && s.Status != "ملغي")
                .Select(s => new { CustomerId = s.CustomerId!.Value, s.SaleDate })
                .ToListAsync();
            var lastVisitMap = visitSales
                .GroupBy(s => s.CustomerId)
                .ToDictionary(g => g.Key, g => g.Max(s => s.SaleDate));

            var assignedCustomersQuery = _context.Customers
                .Include(c => c.AssignedEmployee)
                .Where(c => c.IsActive && c.AssignedEmployeeId.HasValue);
            if (myEmployeeId.HasValue)
                assignedCustomersQuery = assignedCustomersQuery.Where(c => c.AssignedEmployeeId == myEmployeeId.Value);
            var assignedCustomers = await assignedCustomersQuery.ToListAsync();

            var inactiveCustomers = assignedCustomers
                .Where(c => lastVisitMap.TryGetValue(c.Id, out var lv) && lv < inactiveThreshold)
                .OrderBy(c => lastVisitMap[c.Id])
                .Take(30);

            foreach (var cust in inactiveCustomers)
            {
                var lastVisit = lastVisitMap[cust.Id];
                var daysAgo = (today - lastVisit.Date).Days;
                var empName = cust.AssignedEmployee?.FullName ?? "غير محدد";
                var subInact = $"العميل: {cust.FullName}";
                list.Add(new NotificationItem
                {
                    Type = "inactive-customer",
                    Category = "مهمة",
                    Title = "عميل لم يزور الصالون",
                    TitleEn = "Customer Hasn't Visited",
                    SubTitle = subInact,
                    SubTitleEn = $"Customer: {cust.FullNameEn ?? cust.FullName}",
                    Body = $"آخر زيارة: {lastVisit:yyyy/MM/dd} ({daysAgo} يوم) | الموظف المسؤول: {empName}",
                    BodyEn = $"Last visit: {lastVisit:yyyy/MM/dd} ({daysAgo} days ago) | Employee: {empName}",
                    IconClass = "fas fa-user-clock",
                    IconBg = "#0dcaf0",
                    Date = today,
                    ActionUrl = Url.Action("Index", "Customers"),
                    ActionText = "عرض العميل",
                    ActionTextEn = "View Customer",
                    Key = NotifKey("inactive-customer", today, $"{cust.Id}")
                });
            }

            // 8. اقتراب الموظف من تحقيق التارجت الشهري (باقي أقل من 200 د.ك)
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthSales = await _context.Sales
                .Where(s => s.SaleDate >= monthStart && s.Status != "ملغي" && s.EmployeeId.HasValue)
                .Select(s => new { EmployeeId = s.EmployeeId!.Value, s.NetAmount })
                .ToListAsync();
            var revenueMap = monthSales
                .GroupBy(s => s.EmployeeId)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.NetAmount));

            var targetEmployeesQuery = _context.Employees.Where(e => e.IsActive && e.SalesTarget != null && e.SalesTarget > 0);
            if (myEmployeeId.HasValue)
                targetEmployeesQuery = targetEmployeesQuery.Where(e => e.Id == myEmployeeId.Value);
            var targetEmployees = await targetEmployeesQuery.ToListAsync();

            foreach (var emp in targetEmployees)
            {
                var revenue = revenueMap.TryGetValue(emp.Id, out var r) ? r : 0;
                var target = emp.SalesTarget ?? 0;
                var remaining = target - revenue;
                if (remaining > 0 && remaining <= 200)
                {
                    var subTarget = $"الموظف: {emp.FullName}";
                    list.Add(new NotificationItem
                    {
                        Type = "target-near",
                        Category = "مهمة",
                        Title = "اقتراب تحقيق التارجت",
                        TitleEn = "Target Almost Reached",
                        SubTitle = subTarget,
                        SubTitleEn = $"Employee: {emp.FullName}",
                        Body = $"باقي {remaining:N3} د.ك فقط للوصول للتارجت الشهري ({target:N3} د.ك)",
                        BodyEn = $"Only {remaining:N3} KD left to reach the monthly target ({target:N3} KD)",
                        IconClass = "fas fa-bullseye",
                        IconBg = "#6f42c1",
                        Date = today,
                        ActionUrl = Url.Action("EmployeeRevenue", "Reports"),
                        ActionText = "عرض التقرير",
                        ActionTextEn = "View Report",
                        Key = NotifKey("target-near", today, subTarget)
                    });
                }
            }

            // 9. فواتير موردين آجلة جديدة (آخر يومين) — تصل للكاشير/المدير
            if (viewerIsCashier)
            {
                var recentSupplierInvoices = await _context.SupplierInvoices
                    .Include(i => i.Supplier)
                    .Where(i => i.CreatedAt >= today.AddDays(-2))
                    .OrderByDescending(i => i.CreatedAt).Take(8).ToListAsync();

                foreach (var inv in recentSupplierInvoices)
                {
                    var supplierName = inv.Supplier?.Name ?? "غير محدد";
                    var subInv = $"المورد: {supplierName}";
                    list.Add(new NotificationItem
                    {
                        Type = "supplier-invoice-new",
                        Category = "مالية",
                        Title = "فاتورة مورد آجلة جديدة",
                        TitleEn = "New Deferred Supplier Invoice",
                        SubTitle = subInv,
                        SubTitleEn = $"Supplier: {supplierName}",
                        Body = $"رقم الفاتورة: {inv.InvoiceNumber} | القيمة: {inv.TotalAmount:N3} د.ك | أنشأها: {inv.CreatedByName ?? "-"}",
                        BodyEn = $"Invoice #: {inv.InvoiceNumber} | Amount: {inv.TotalAmount:N3} KD | By: {inv.CreatedByName ?? "-"}",
                        IconClass = "fas fa-file-invoice",
                        IconBg = "#fd7e14",
                        Date = inv.CreatedAt,
                        ActionUrl = Url.Action("Index", "SupplierInvoices"),
                        ActionText = "عرض الفاتورة",
                        ActionTextEn = "View Invoice",
                        Key = NotifKey("supplier-invoice-new", inv.CreatedAt, subInv)
                    });
                }
            }
            return list.OrderByDescending(n => n.Date).ToList();
        }
    }
}