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
        private static readonly string[] MixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

        // الكاشير المرتبط بقسم إيرادي محدد (حلاقة/مساج) يشوف قسمه بس ومفيش فلتر ليه. أي حد
        // تاني (أدمن/مدير/كاشير "الكل") يقدر يختار القسم بحرية.
        private async Task<(string Department, bool CanPickDepartment)> ResolveDepartmentAsync(string? requestedDept)
        {
            bool isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");
            if (!isAdminOrManager)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.UserDepartment == Shift.ClosureDepartments.Haircut
                    || currentUser?.UserDepartment == Shift.ClosureDepartments.Massage)
                {
                    return (currentUser.UserDepartment!, false);
                }
            }

            var department = requestedDept switch
            {
                Shift.ClosureDepartments.Haircut => Shift.ClosureDepartments.Haircut,
                Shift.ClosureDepartments.Massage => Shift.ClosureDepartments.Massage,
                Shift.ClosureDepartments.Shared => Shift.ClosureDepartments.Shared,
                _ => Shift.ClosureDepartments.Haircut
            };
            return (department, true);
        }

        public async Task<IActionResult> Review(string? date, string? dept)
        {
            var day = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var (department, canPickDepartment) = await ResolveDepartmentAsync(dept);

            var shift = await _closure.GetOrCreateForDateAsync(day, department);
            var vm = await BuildViewModelAsync(shift, day, department, canPickDepartment);
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
            if (shift == null || !shift.IsClosureRecord) return NotFound();

            var department = shift.ClosureDepartment ?? Shift.ClosureDepartments.Shared;
            var currentUser = await _userManager.GetUserAsync(User);

            // كاشير مرتبط بقسم محدد يعتمد يومية قسمه بس، حتى لو اتلعب في الطلب المرسل يدوياً.
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager")
                && (currentUser?.UserDepartment == Shift.ClosureDepartments.Haircut || currentUser?.UserDepartment == Shift.ClosureDepartments.Massage)
                && currentUser.UserDepartment != department)
            {
                return Forbid();
            }

            var day = shift.ShiftDate.Date;
            var dateParam = day.ToString("yyyy-MM-dd");

            if (shift.ApprovalStatus == Shift.ApprovalStatuses.Approved
                || shift.ApprovalStatus == Shift.ApprovalStatuses.ApprovedWithDiscrepancy)
            {
                TempData["Error"] = "هذه اليومية معتمدة بالفعل";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
            }

            if (!(reviewedRevenue && reviewedCash && reviewedKnet && reviewedExpenses && reviewedWithdrawals
                  && reviewedDeposits && reviewedAdvances && reviewedEmployeeDebts && reviewedCustody))
            {
                TempData["Error"] = "يجب مراجعة كل البنود في القائمة قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
            }

            var expectedCash = await GetExpectedCashAsync(day, department);
            var systemKnet = await GetSystemKnetTotalAsync(day, department);

            var cashDiff = actualCashBalance - expectedCash;
            var knetDiff = deviceKnetTotal - systemKnet;
            bool hasDiscrepancy = Math.Abs(cashDiff) >= 0.001m || Math.Abs(knetDiff) >= 0.001m;

            if (Math.Abs(cashDiff) >= 0.001m && string.IsNullOrWhiteSpace(cashDifferenceReason))
            {
                TempData["Error"] = "يوجد فرق في الصندوق — يجب كتابة سبب الفرق قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
            }

            if (Math.Abs(knetDiff) >= 0.001m && string.IsNullOrWhiteSpace(knetDifferenceReason))
            {
                TempData["Error"] = "يوجد فرق في مطابقة KNET — يجب كتابة سبب الفرق قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
            }

            if (!hasDiscrepancy && !confirmedNoDiscrepancies)
            {
                TempData["Error"] = "يجب تأكيد عدم وجود فروقات قبل الاعتماد";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
            }

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
                $"اعتماد وإغلاق يومية {department} بتاريخ {day:yyyy/MM/dd}" + (hasDiscrepancy ? " — يوجد فروقات موثّقة" : " — سليمة بدون فروقات"),
                shift.Id);

            TempData["Success"] = hasDiscrepancy
                ? "تم اعتماد اليومية — تم تسجيل الفروقات في التقرير"
                : "تم اعتماد وإغلاق اليومية بنجاح";
            return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reopen(int shiftId, string reason)
        {
            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null || !shift.IsClosureRecord) return NotFound();

            var department = shift.ClosureDepartment ?? Shift.ClosureDepartments.Shared;
            var dateParam = shift.ShiftDate.Date.ToString("yyyy-MM-dd");

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "يجب كتابة سبب إعادة الفتح";
                return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
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
                $"إعادة فتح يومية {department} بتاريخ {shift.ShiftDate:yyyy/MM/dd} (كانت: {oldStatus}) — السبب: {reason.Trim()}",
                shift.Id);

            TempData["Success"] = "تم إعادة فتح اليومية — يمكن الآن تعديل بيانات هذا اليوم";
            return RedirectToAction(nameof(Review), new { date = dateParam, dept = department });
        }

        private static bool IsShared(string department) => department == Shift.ClosureDepartments.Shared;

        private async Task<decimal> GetExpectedCashAsync(DateTime day, string department)
        {
            var snapshot = IsShared(department)
                ? await CashBoxCalculator.GetSnapshotAsync(_context, day, day.AddDays(1), null, sharedOnly: true)
                : await CashBoxCalculator.GetSnapshotAsync(_context, day, day.AddDays(1), department);
            return snapshot.ClosingBalance;
        }

        private async Task<decimal> GetSystemKnetTotalAsync(DateTime day, string department)
        {
            var dayEnd = day.AddDays(1);
            var query = _context.Sales.Where(s => s.SaleDate >= day && s.SaleDate < dayEnd && s.Status != "ملغي");
            query = IsShared(department)
                ? query.Where(s => s.SaleType != Shift.ClosureDepartments.Haircut && s.SaleType != Shift.ClosureDepartments.Massage)
                : query.Where(s => s.SaleType == department);
            var sales = await query.ToListAsync();
            return sales.Sum(s => KnetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : MixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0m);
        }

        private async Task<DailyClosureViewModel> BuildViewModelAsync(Shift shift, DateTime day, string department, bool canPickDepartment)
        {
            var dayEnd = day.AddDays(1);
            bool isShared = IsShared(department);

            var snapshot = isShared
                ? await CashBoxCalculator.GetSnapshotAsync(_context, day, dayEnd, null, sharedOnly: true)
                : await CashBoxCalculator.GetSnapshotAsync(_context, day, dayEnd, department);

            var salesQuery = _context.Sales.Where(s => s.SaleDate >= day && s.SaleDate < dayEnd && s.Status != "ملغي");
            salesQuery = isShared
                ? salesQuery.Where(s => s.SaleType != Shift.ClosureDepartments.Haircut && s.SaleType != Shift.ClosureDepartments.Massage)
                : salesQuery.Where(s => s.SaleType == department);
            var sales = await salesQuery.ToListAsync();
            var totalRevenue = sales.Sum(s => s.NetAmount);
            var systemKnet = sales.Sum(s => KnetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : MixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0m);
            var employeeDebtToday = sales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            var ownerDebtToday = sales.Where(s => s.PaymentMethod == "دين على الإدارة").Sum(s => s.NetAmount);

            var expensesQuery = _context.Expenses.Where(e => e.ExpenseDate >= day && e.ExpenseDate < dayEnd && e.Category != "عهدة");
            expensesQuery = isShared
                ? expensesQuery.Where(e => e.Department != Shift.ClosureDepartments.Haircut && e.Department != Shift.ClosureDepartments.Massage)
                : expensesQuery.Where(e => e.Department == department);
            var expenses = await expensesQuery.OrderByDescending(e => e.CreatedAt).ToListAsync();

            var withdrawalsQuery = _context.Withdrawals.Where(w => w.WithdrawalDate >= day && w.WithdrawalDate < dayEnd);
            withdrawalsQuery = isShared
                ? withdrawalsQuery.Where(w => w.Department != Shift.ClosureDepartments.Haircut && w.Department != Shift.ClosureDepartments.Massage)
                : withdrawalsQuery.Where(w => w.Department == department);
            var withdrawals = await withdrawalsQuery.OrderByDescending(w => w.CreatedAt).ToListAsync();

            var depositsQuery = _context.Deposits.Where(d => d.DepositDate >= day && d.DepositDate < dayEnd);
            depositsQuery = isShared
                ? depositsQuery.Where(d => d.Department != Shift.ClosureDepartments.Haircut && d.Department != Shift.ClosureDepartments.Massage)
                : depositsQuery.Where(d => d.Department == department);
            var deposits = await depositsQuery.OrderByDescending(d => d.CreatedAt).ToListAsync();

            var advancesQuery = _context.EmployeeAdvances.Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= day && a.AdvanceDate < dayEnd
                         && EmployeeAdvance.Statuses.Realized.Contains(a.Status));
            advancesQuery = isShared
                ? advancesQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Haircut
                                         && (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Massage)
                : advancesQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) == department);
            var advancesToday = await advancesQuery.OrderByDescending(a => a.CreatedAt).ToListAsync();

            var outstandingDebtsQuery = _context.EmployeeAdvances.Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate < dayEnd
                         && EmployeeAdvance.Statuses.Realized.Contains(a.Status) && a.Status != EmployeeAdvance.Statuses.Repaid);
            outstandingDebtsQuery = isShared
                ? outstandingDebtsQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Haircut
                                                 && (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Massage)
                : outstandingDebtsQuery.Where(a => (a.Employee!.RevenueDepartment ?? a.Employee!.DepartmentNav!.Name) == department);
            var outstandingDebts = (await outstandingDebtsQuery.ToListAsync()).Sum(a => a.Amount - a.DeductedAmount);

            // ملحوظة: النوع المصرَّح به IQueryable<Custody> (مش var) عمداً — لو سبناه var هيتحدد
            // نوعه IIncludableQueryable بسبب الـ Include المتتالي، وإعادة تعيينه بـ .Where() تاني
            // (اللي بترجّع IQueryable عادي) بترمي InvalidCastException وقت التشغيل.
            IQueryable<Custody> custodiesQuery = _context.Custodies.Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(c => c.Allocations)
                .Include(c => c.InvoicePayments)
                .Where(c => c.SettlementType == null);
            custodiesQuery = isShared
                ? custodiesQuery.Where(c => (c.Employee!.RevenueDepartment ?? c.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Haircut
                                          && (c.Employee!.RevenueDepartment ?? c.Employee!.DepartmentNav!.Name) != Shift.ClosureDepartments.Massage)
                : custodiesQuery.Where(c => (c.Employee!.RevenueDepartment ?? c.Employee!.DepartmentNav!.Name) == department);
            var custodies = await custodiesQuery.ToListAsync();

            var pending = await _closure.FindMostRecentPendingApprovalAsync(new[] { department }, day, department);

            return new DailyClosureViewModel
            {
                Shift = shift,
                Date = day,
                Department = department,
                CanPickDepartment = canPickDepartment,
                AvailableDepartments = Shift.ClosureDepartments.All.ToList(),
                TotalRevenue = totalRevenue,
                TotalCash = snapshot.CashRevenue,
                SystemKnetTotal = systemKnet,
                TotalExpenses = expenses.Sum(e => e.Amount),
                TotalWithdrawals = withdrawals.Sum(w => w.Amount),
                TotalDeposits = deposits.Sum(d => d.Amount),
                TotalAdvancesToday = advancesToday.Sum(a => a.Amount),
                OutstandingEmployeeDebts = outstandingDebts,
                EmployeeDebtToday = employeeDebtToday,
                OwnerDebtToday = ownerDebtToday,
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
                Custodies = custodies.Where(c => c.CustodyDate >= day && c.CustodyDate < dayEnd).ToList(),
                PendingApprovalDate = pending?.Date
            };
        }
    }
}