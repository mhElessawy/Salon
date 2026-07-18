using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class DailyClosureController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDailyClosureService _closure;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public DailyClosureController(ApplicationDbContext context, IDailyClosureService closure,
            IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _closure = closure;
            _audit = audit;
            _userManager = userManager;
        }

        private static readonly string[] KnetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };

        public async Task<IActionResult> Review(string? date)
        {
            var day = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var shift = await _closure.GetOrCreateForDateAsync(day);
            var vm = await BuildViewModelAsync(shift, day);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int shiftId, decimal actualCashBalance, decimal deviceKnetTotal, string? knetSettlementNumber,
            string? cashDifferenceReason, string? knetDifferenceReason, string? approvalNotes,
            bool reviewedRevenue, bool reviewedCash, bool reviewedKnet, bool reviewedExpenses,
            bool reviewedWithdrawals, bool reviewedDeposits, bool reviewedAdvances,
            bool reviewedEmployeeDebts, bool reviewedCustody, bool confirmedNoDiscrepancies)
        {
            if (!User.IsInRole("Cashier") && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
                return Forbid();

            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null) return NotFound();

            var day = shift.ShiftDate.Date;
            var dateParam = day.ToString("yyyy-MM-dd");

            if (shift.ApprovalStatus == Shift.ApprovalStatuses.Approved
                || shift.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy)
            {
                TempData["Error"] = "هذه اليومية معتمدة بالفعل";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            if (!(reviewedRevenue && reviewedCash && reviewedKnet && reviewedExpenses && reviewedWithdrawals
                  && reviewedDeposits && reviewedAdvances && reviewedEmployeeDebts && reviewedCustody))
            {
                TempData["Error"] = "يجب مراجعة كل البنود في القائمة قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            var expectedCash = (await CashBoxCalculator.GetSnapshotAsync(_context, day, day.AddDays(1), null)).ClosingBalance;
            var systemKnet = await GetSystemKnetTotalAsync(day);

            var cashDiff = actualCashBalance - expectedCash;
            var knetDiff = deviceKnetTotal - systemKnet;
            bool hasDiscrepancy = Math.Abs(cashDiff) >= 0.001m || Math.Abs(knetDiff) >= 0.001m;

            if (Math.Abs(cashDiff) >= 0.001m && string.IsNullOrWhiteSpace(cashDifferenceReason))
            {
                TempData["Error"] = "يوجد فرق في الصندوق — يجب كتابة سبب الفرق قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            if (Math.Abs(knetDiff) >= 0.001m && string.IsNullOrWhiteSpace(knetDifferenceReason))
            {
                TempData["Error"] = "يوجد فرق في مطابقة KNET — يجب كتابة سبب الفرق قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            if (!hasDiscrepancy && !confirmedNoDiscrepancies)
            {
                TempData["Error"] = "يجب تأكيد عدم وجود فروقات قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            shift.ExpectedCashBalance = expectedCash;
            shift.ClosingBalance = actualCashBalance;
            shift.CashDifferenceReason = cashDifferenceReason?.Trim();
            shift.SystemKnetTotal = systemKnet;
            shift.DeviceKnetTotal = deviceKnetTotal;
            shift.KnetSettlementNumber = knetSettlementNumber?.Trim();
            shift.KnetDifferenceReason = knetDifferenceReason?.Trim();
            shift.ReviewedRevenue = reviewedRevenue;
            shift.ReviewedCash = reviewedCash;
            shift.ReviewedKnet = reviewedKnet;
            shift.ReviewedExpenses = reviewedExpenses;
            shift.ReviewedWithdrawals = reviewedWithdrawals;
            shift.ReviewedDeposits = reviewedDeposits;
            shift.ReviewedAdvances = reviewedAdvances;
            shift.ReviewedEmployeeDebts = reviewedEmployeeDebts;
            shift.ReviewedCustody = reviewedCustody;
            shift.ConfirmedNoDiscrepancies = confirmedNoDiscrepancies;
            shift.ApprovalNotes = approvalNotes?.Trim();
            shift.ApprovedByUserId = _userManager.GetUserId(User);
            shift.ApprovedByUserName = currentUser?.FullName ?? currentUser?.UserName ?? "غير معروف";
            shift.ApprovedAt = DateTime.Now;
            shift.Status = "مغلق";
            shift.EndTime = DateTime.Now.TimeOfDay;
            shift.ApprovalStatus = hasDiscrepancy
                ? Shift.ApprovalStatuses.ApprovedWithDiscrepancy
                : Shift.ApprovalStatuses.Approved;

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Approve", "Shifts",
                $"اعتماد وإغلاق يومية {day:yyyy/MM/dd}" + (hasDiscrepancy ? " — يوجد فروقات موثّقة" : " — سليمة بدون فروقات"),
                shift.Id);

            TempData["Success"] = hasDiscrepancy
                ? "تم اعتماد اليومية — تم تسجيل الفروقات في التقرير"
                : "تم اعتماد وإغلاق اليومية بنجاح";
            return RedirectToAction(nameof(Review), new { date = dateParam });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reopen(int shiftId, string reason)
        {
            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null) return NotFound();

            var dateParam = shift.ShiftDate.Date.ToString("yyyy-MM-dd");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "يجب كتابة سبب إعادة الفتح";
                return RedirectToAction(nameof(Review), new { date = dateParam });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var oldStatus = shift.ApprovalStatus;

            shift.ApprovalStatus = Shift.ApprovalStatuses.Open;
            shift.ReopenedByUserId = _userManager.GetUserId(User);
            shift.ReopenedByUserName = currentUser?.FullName ?? currentUser?.UserName ?? "غير معروف";
            shift.ReopenedAt = DateTime.Now;
            shift.ReopenReason = reason.Trim();
            await _context.SaveChangesAsync();

            await _audit.LogAsync("ReopenDailyClosure", "Shifts",
                $"إعادة فتح يومية {shift.ShiftDate:yyyy/MM/dd} (كانت: {oldStatus}) — السبب: {reason.Trim()}",
                shift.Id);

            TempData["Success"] = "تم إعادة فتح اليومية — يمكن الآن تعديل بيانات هذا اليوم";
            return RedirectToAction(nameof(Review), new { date = dateParam });
        }

        private async Task<decimal> GetSystemKnetTotalAsync(DateTime day)
        {
            var dayEnd = day.AddDays(1);
            var sales = await _context.Sales
                .Where(s => s.SaleDate >= day && s.SaleDate < dayEnd && s.Status != "ملغي")
                .ToListAsync();
            return sales.Sum(s => KnetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : s.PaymentMethod == "كي نت و كاش" ? (s.LinkAmount ?? 0) : 0m);
        }

        private async Task<DailyClosureViewModel> BuildViewModelAsync(Shift shift, DateTime day)
        {
            var dayEnd = day.AddDays(1);

            var snapshot = await CashBoxCalculator.GetSnapshotAsync(_context, day, dayEnd, null);

            var sales = await _context.Sales
                .Where(s => s.SaleDate >= day && s.SaleDate < dayEnd && s.Status != "ملغي")
                .ToListAsync();
            var totalRevenue = sales.Sum(s => s.NetAmount);
            var systemKnet = sales.Sum(s => KnetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : s.PaymentMethod == "كي نت و كاش" ? (s.LinkAmount ?? 0) : 0m);

            var expenses = await _context.Expenses
                .Where(e => e.ExpenseDate >= day && e.ExpenseDate < dayEnd && e.Category != "عهدة")
                .OrderByDescending(e => e.CreatedAt).ToListAsync();

            var withdrawals = await _context.Withdrawals
                .Where(w => w.WithdrawalDate >= day && w.WithdrawalDate < dayEnd)
                .OrderByDescending(w => w.CreatedAt).ToListAsync();

            var deposits = await _context.Deposits
                .Where(d => d.DepositDate >= day && d.DepositDate < dayEnd)
                .OrderByDescending(d => d.CreatedAt).ToListAsync();

            var advancesToday = await _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => a.AdvanceDate >= day && a.AdvanceDate < dayEnd
                         && EmployeeAdvance.Statuses.Realized.Contains(a.Status))
                .OrderByDescending(a => a.CreatedAt).ToListAsync();

            var outstandingDebts = (await _context.EmployeeAdvances
                .Where(a => EmployeeAdvance.Statuses.Realized.Contains(a.Status) && a.Status != EmployeeAdvance.Statuses.Repaid)
                .ToListAsync())
                .Sum(a => a.Amount - a.DeductedAmount);

            var custodies = await _context.Custodies
                .Include(c => c.Employee)
                .Include(c => c.PurchaseRequests)
                .ToListAsync();

            return new DailyClosureViewModel
            {
                Shift = shift,
                Date = day,
                TotalRevenue = totalRevenue,
                TotalCash = snapshot.CashRevenue,
                SystemKnetTotal = systemKnet,
                TotalExpenses = expenses.Sum(e => e.Amount),
                TotalWithdrawals = withdrawals.Sum(w => w.Amount),
                TotalDeposits = deposits.Sum(d => d.Amount),
                TotalAdvancesToday = advancesToday.Sum(a => a.Amount),
                OutstandingEmployeeDebts = outstandingDebts,
                CustodyRemaining = custodies.Sum(c => c.RemainingAmount),
                ExpectedCashBalance = snapshot.ClosingBalance,
                IsLocked = shift.ApprovalStatus == Shift.ApprovalStatuses.Approved
                    || shift.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy,
                CanApprove = User.IsInRole("Cashier") || User.IsInRole("Admin") || User.IsInRole("Manager"),
                CanReopen = User.IsInRole("Admin"),
                Expenses = expenses,
                Withdrawals = withdrawals,
                Deposits = deposits,
                Advances = advancesToday,
                Custodies = custodies.Where(c => c.CustodyDate >= day && c.CustodyDate < dayEnd).ToList()
            };
        }
    }
}
