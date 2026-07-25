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
    public class BarberDailyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDailyClosureService _closure;

        public BarberDailyController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IDailyClosureService closure)
        {
            _context = context;
            _userManager = userManager;
            _closure = closure;
        }

        public async Task<IActionResult> Index(string? date, string? dept)
        {
            var today = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isEmployee = User.IsInRole("Employee");
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            bool isBarberOnly = userDept == "حلاقة";
            bool isMassageOnly = userDept == "مساج";

            string? filterDept = isBarberOnly ? "حلاقة"
                                : isMassageOnly ? "مساج"
                                : dept;

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            var salesQuery = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow && s.Status != "ملغي");

            if (isEmployee)
                salesQuery = salesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1));
            else if (filterDept == "حلاقة")
                salesQuery = salesQuery.Where(s => s.SaleType == "حلاقة");
            else if (filterDept == "مساج")
                salesQuery = salesQuery.Where(s => s.SaleType == "مساج");

            var allSales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
            var staffSales = allSales.Where(s => s.SaleType == "حلاقة" || s.SaleType == "مساج").ToList();

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive && e.DepartmentNav != null);

            if (isEmployee)
                empQuery = empQuery.Where(e => e.Id == (linkedEmpId ?? -1));
            else if (filterDept == "حلاقة")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة");
            else if (filterDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "مساج");
            else
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة" || e.DepartmentNav!.Name == "مساج");

            var employees = await empQuery.OrderBy(e => e.DepartmentNav!.Name).ThenBy(e => e.FullName).ToListAsync();
            var employeeIds = employees.Select(e => e.Id).ToList();

            var expQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);

            if (filterDept == "حلاقة")
                expQuery = expQuery.Where(e => e.Department == "حلاقة" || e.Department == null || e.Department == "");
            else if (filterDept == "مساج")
                expQuery = expQuery.Where(e => e.Department == "مساج" || e.Department == null || e.Department == "");

            var expenses = await expQuery.OrderBy(e => e.Id).ToListAsync();

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow && EmployeeAdvance.Statuses.Realized.Contains(a.Status));

            if (!isEmployee)
                advancesQuery = advancesQuery.Where(a => employeeIds.Contains(a.EmployeeId));
            else
                advancesQuery = advancesQuery.Where(a => a.EmployeeId == (linkedEmpId ?? -1));

            var advances = await advancesQuery.OrderBy(a => a.Id).ToListAsync();

            var custodiesQuery = _context.Custodies
                .Include(c => c.Employee)
                .Where(c => c.CustodyDate >= today && c.CustodyDate < tomorrow);

            if (!isEmployee)
                custodiesQuery = custodiesQuery.Where(c => employeeIds.Contains(c.EmployeeId));
            else
                custodiesQuery = custodiesQuery.Where(c => c.EmployeeId == (linkedEmpId ?? -1));

            var todayCustodies = await custodiesQuery.OrderBy(c => c.Id).ToListAsync();

            // Current custody balances per employee — every custody ever handed out (not just
            // today's), minus amounts already spent via completed purchase requests, so the card
            // reflects what each employee is still holding right now rather than only today's
            // movement.
            var allCustodiesQuery = _context.Custodies
                .Include(c => c.Employee)
                .Include(c => c.PurchaseRequests)
                .Include(c => c.InvoicePayments)
                .AsQueryable();

            if (!isEmployee)
                allCustodiesQuery = allCustodiesQuery.Where(c => employeeIds.Contains(c.EmployeeId));
            else
                allCustodiesQuery = allCustodiesQuery.Where(c => c.EmployeeId == (linkedEmpId ?? -1));

            var allCustodies = await allCustodiesQuery.ToListAsync();
            var currentCustodies = allCustodies
                .GroupBy(c => c.Employee?.FullName ?? "—")
                .Select(g => new EmployeeCustodyBalance { EmployeeName = g.Key, Amount = g.Sum(c => c.RemainingAmount) })
                .Where(x => x.Amount > 0)
                .OrderByDescending(x => x.Amount)
                .ToList();

            var salariesQuery = _context.Salaries
                .Where(s => s.PaidDate.HasValue && s.PaidDate.Value >= today && s.PaidDate.Value < tomorrow);
            salariesQuery = isEmployee
                ? salariesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1))
                : salariesQuery.Where(s => employeeIds.Contains(s.EmployeeId));
            var todaySalaries = await salariesQuery.ToListAsync();

            // Deposits/withdrawals tagged with a department belong to that department's own
            // cash share; untagged ones are shared and only surface in the unfiltered (no dept) view.
            var depositsQuery = _context.Deposits
                .Where(d => d.DepositDate >= today && d.DepositDate < tomorrow);
            if (filterDept == "حلاقة" || filterDept == "مساج")
                depositsQuery = depositsQuery.Where(d => d.Department == filterDept);
            var deposits = await depositsQuery.ToListAsync();

            var withdrawalsQuery = _context.Withdrawals
                .Where(w => w.WithdrawalDate >= today && w.WithdrawalDate < tomorrow);
            if (filterDept == "حلاقة" || filterDept == "مساج")
                withdrawalsQuery = withdrawalsQuery.Where(w => w.Department == filterDept);
            var withdrawals = await withdrawalsQuery.ToListAsync();

            var shift = await _context.Shifts
                .Where(s => !s.IsClosureRecord && s.ShiftDate >= today && s.ShiftDate < tomorrow)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            // Carry the cash balance forward continuously from the very first recorded shift
            // instead of resetting it to zero at the start of every calendar month. Every
            // component below is scoped by filterDept using the exact same rules as the
            // "today" queries above, so a department's opening balance stays its own running
            // share of the safe (matching Reports/CashMovement) rather than the whole register.
            // ملحوظة: عدم استبعاد صفوف IsClosureRecord هنا مقصود — نفس السبب الموضّح في
            // CashBoxCalculator.GetSnapshotAsync (تحديد أول تاريخ عندنا فيه بيانات، مش رصيد
            // افتتاحي يدوي حقيقي بالضرورة).
            var firstShiftEver = await _context.Shifts
                .OrderBy(s => s.ShiftDate).ThenBy(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            bool hasBaseline = firstShiftEver != null && firstShiftEver.ShiftDate.Date <= today;
            DateTime baseDate = hasBaseline ? firstShiftEver!.ShiftDate.Date : today;

            // Shift.OpeningBalance is a manual physical cash count of the whole shared safe — it
            // has no per-department split. Only fold it in for the unfiltered (whole-safe) view;
            // a department/employee-scoped balance should be that scope's own running share only.
            bool isScoped = isEmployee || filterDept == "حلاقة" || filterDept == "مساج";
            decimal baseBalance = isScoped
                ? 0
                : (hasBaseline ? firstShiftEver!.OpeningBalance : (shift?.OpeningBalance ?? 0));

            // Must mirror staffSales' scope exactly — including its unconditional exclusion of
            // "منتجات" sales — since that's what today's CashRevenue is built from. Otherwise a
            // day's opening balance won't equal the previous day's closing balance.
            var prevSalesQuery = _context.Sales
                .Where(s => s.SaleDate >= baseDate && s.SaleDate < today && s.Status != "ملغي"
                         && (s.SaleType == "حلاقة" || s.SaleType == "مساج"));
            if (isEmployee)
                prevSalesQuery = prevSalesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1));
            else if (filterDept == "حلاقة")
                prevSalesQuery = prevSalesQuery.Where(s => s.SaleType == "حلاقة");
            else if (filterDept == "مساج")
                prevSalesQuery = prevSalesQuery.Where(s => s.SaleType == "مساج");
            var prevSales = await prevSalesQuery.ToListAsync();
            decimal prevCash = prevSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);

            var prevDepositsQuery = _context.Deposits
                .Where(d => d.DepositDate >= baseDate && d.DepositDate < today);
            if (filterDept == "حلاقة" || filterDept == "مساج")
                prevDepositsQuery = prevDepositsQuery.Where(d => d.Department == filterDept);
            decimal prevDeposits = await prevDepositsQuery.SumAsync(d => d.Amount);

            // Only cash-paid expenses/advances actually leave the physical register — ones paid
            // by card or bank transfer never touched the cash box, so they must not reduce it.
            // Category "عهدة" is excluded entirely: custody is money set aside under an
            // employee's custody, not a cash outflow, so it never reduces the register.
            var prevExpensesQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= baseDate && e.ExpenseDate < today && e.PaymentMethod == "نقدي" && e.Category != "عهدة");
            if (filterDept == "حلاقة")
                prevExpensesQuery = prevExpensesQuery.Where(e => e.Department == "حلاقة" || e.Department == null || e.Department == "");
            else if (filterDept == "مساج")
                prevExpensesQuery = prevExpensesQuery.Where(e => e.Department == "مساج" || e.Department == null || e.Department == "");
            decimal prevExpenses = await prevExpensesQuery.SumAsync(e => e.Amount);

            var prevAdvancesQuery = _context.EmployeeAdvances
                .Where(a => a.AdvanceDate >= baseDate && a.AdvanceDate < today
                         && EmployeeAdvance.Statuses.Realized.Contains(a.Status) && a.PaymentMethod == "نقدي");
            prevAdvancesQuery = isEmployee
                ? prevAdvancesQuery.Where(a => a.EmployeeId == (linkedEmpId ?? -1))
                : prevAdvancesQuery.Where(a => employeeIds.Contains(a.EmployeeId));
            decimal prevAdvances = await prevAdvancesQuery.SumAsync(a => a.Amount);

            // Salaries paid in cash reduce the register too — Reports/CashMovement folds these
            // into the same "cash expenses" deduction alongside مصروف/سلفة.
            var prevSalariesQuery = _context.Salaries
                .Where(s => s.PaidDate.HasValue && s.PaidDate.Value >= baseDate && s.PaidDate.Value < today && s.PaymentMethod == "نقدي");
            prevSalariesQuery = isEmployee
                ? prevSalariesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1))
                : prevSalariesQuery.Where(s => employeeIds.Contains(s.EmployeeId));
            decimal prevSalaries = await prevSalariesQuery.SumAsync(s => s.NetSalary);

            var prevWithdrawalsQuery = _context.Withdrawals
                .Where(w => w.WithdrawalDate >= baseDate && w.WithdrawalDate < today);
            if (filterDept == "حلاقة" || filterDept == "مساج")
                prevWithdrawalsQuery = prevWithdrawalsQuery.Where(w => w.Department == filterDept);
            decimal prevWithdrawals = await prevWithdrawalsQuery.SumAsync(w => w.Amount);

            decimal openingBalance = baseBalance + prevCash + prevDeposits - prevExpenses - prevAdvances - prevSalaries - prevWithdrawals;

            decimal totalSales = staffSales.Sum(s => s.NetAmount);
            decimal cashRevenue = staffSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            decimal knetRevenue = staffSales.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            decimal employeeDebtRevenue = staffSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            decimal ownerDebtRevenue = staffSales.Where(s => s.PaymentMethod == "دين على الإدارة").Sum(s => s.NetAmount);

            var tipInvoices = allSales.Where(s => (s.GiftForEmployee ?? 0) > 0).ToList();
            decimal tipsTotal = allSales.Sum(s => s.GiftForEmployee ?? 0);
            decimal tipsDelivered = allSales.Sum(s => s.EmployeeGift ?? 0);
            decimal totalDiscount = staffSales.Sum(s => s.Discount);
            decimal totalExpenses = expenses.Sum(e => e.Amount);
            decimal cashExpenses = expenses.Where(e => e.PaymentMethod == "نقدي" && e.Category != "عهدة").Sum(e => e.Amount);
            decimal cashAdvances = advances.Where(a => a.PaymentMethod == "نقدي").Sum(a => a.Amount);
            decimal cashSalaries = todaySalaries.Where(s => s.PaymentMethod == "نقدي").Sum(s => s.NetSalary);
            // العهدة معلوماتية فقط هنا — لا تُخصم من الصندوق (راجع todayCustodies أعلاه).
            decimal totalCustody = todayCustodies.Sum(c => c.Amount);

            // "حركة الصندوق" في هذه الصفحة تستخدم نفس معادلة CashBoxCalculator المستخدمة في
            // Reports/CashMovement (كل أنواع الفواتير وكل الموظفين عند عدم تحديد قسم) حتى لا
            // يختلف الرقمان — إلا في عرض الموظف لنفسه فقط، حيث يبقى الحساب مقتصراً على مبيعاته
            // وسلفه ورواتبه هو (لا يوجد مفهوم مماثل في الحاسبة المشتركة).
            var box = isEmployee
                ? new CashBoxSnapshot(openingBalance, cashRevenue, deposits.Sum(d => d.Amount), cashExpenses, cashAdvances, cashSalaries, withdrawals.Sum(w => w.Amount))
                : await CashBoxCalculator.GetSnapshotAsync(_context, today, tomorrow, filterDept);

            var expensesByCategory = expenses
                .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "أخرى" : e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            EmployeePerformanceRow BuildRow(Employee emp)
            {
                var empDeptType = emp.DepartmentNav?.Name == "مساج" ? "مساج" : "حلاقة";
                var empSales = staffSales.Where(s => s.EmployeeId == emp.Id && s.SaleType == empDeptType).ToList();
                var empTotal = empSales.Sum(s => s.NetAmount);
                var empDiscount = empSales.Sum(s => s.Discount);
                var empCash = empSales.Sum(s =>
                    cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
                var empKNet = empSales.Sum(s =>
                    knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
                var empDebtToEmployee = empSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
                var empDebtToOwner = empSales.Where(s => s.PaymentMethod == "دين على الإدارة").Sum(s => s.NetAmount);
                var empAdv = advances.Where(a => a.EmployeeId == emp.Id).Sum(a => a.Amount);
                var empHadiya = allSales.Where(s => s.EmployeeId == emp.Id).Sum(s => s.GiftForEmployee ?? 0);
                var empServiceCommission = allSales.Where(s => s.EmployeeId == emp.Id).Sum(s => s.EmployeeGift ?? 0);
                var commission = emp.Commission;
                var dueAmount = Math.Round(empTotal * commission / 100, 3);

                return new EmployeePerformanceRow
                {
                    Employee = emp,
                    InvoiceCount = empSales.Count,
                    TotalSales = empTotal,
                    Discount = empDiscount,
                    InstantCollection = empCash + empKNet,
                    Cash = empCash,
                    KNet = empKNet,
                    EmployeeDebt = empDebtToEmployee,
                    OwnerDebt = empDebtToOwner,
                    Advances = empAdv,
                    SalesPercent = totalSales > 0 ? Math.Round(empTotal / totalSales * 100, 1) : 0,
                    CommissionPercent = commission,
                    DueAmount = dueAmount,
                    ShopNet = empTotal - dueAmount - empHadiya,
                    Hadiya = empHadiya,
                    ServiceCommission = empServiceCommission,
                };
            }

            var barberEmployees = employees.Where(e => e.DepartmentNav?.Name == "حلاقة").ToList();
            var massageEmployees = employees.Where(e => e.DepartmentNav?.Name == "مساج").ToList();
            var barberRows = barberEmployees.Select(BuildRow).ToList();
            var massageRows = massageEmployees.Select(BuildRow).ToList();

            var reportCount = await _context.Sales
                .Where(s => s.SaleDate.Date <= today)
                .Select(s => s.SaleDate.Date).Distinct().CountAsync();

            string[] arabicDays = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };

            int workHours = 10;
            if (shift?.StartTime != null && shift?.EndTime != null)
                workHours = (int)(shift.EndTime.Value - shift.StartTime).TotalHours;

            var vm = new DailyPerformanceViewModel
            {
                ReportDate = today,
                DayName = arabicDays[(int)today.DayOfWeek],
                UserName = currentUser?.FullName ?? currentUser?.UserName ?? "المستخدم",
                ReportTime = DateTime.Now.ToString("hh:mm tt"),
                ReportNumber = $"DY-{reportCount:D6}",
                UserDepartment = userDept,
                SelectedDept = filterDept,

                TotalSales = totalSales,
                InvoiceCount = staffSales.Count,
                CashTotal = cashRevenue,
                KNetTotal = knetRevenue,
                EmployeeDebtTotal = employeeDebtRevenue,
                OwnerDebtTotal = ownerDebtRevenue,
                TotalDiscount = totalDiscount,
                TipsTotal = tipsTotal,
                TipsDelivered = tipsDelivered,

                OpeningBalance = box.OpeningBalance,
                CashRevenue = box.CashRevenue,
                TotalDeposits = box.Deposits,
                TotalExpensesAmount = totalExpenses,
                TotalAdvancesAmount = advances.Sum(a => a.Amount),
                TotalWithdrawals = box.Withdrawals,
                CashExpensesAmount = box.CashExpenses,
                CashAdvancesAmount = box.CashAdvances,
                CashSalariesAmount = box.CashSalaries,
                TotalCustodyAmount = totalCustody,
                CurrentCustodies = currentCustodies,

                ExpenseCount = expenses.Count,
                MaxExpense = expenses.Any() ? expenses.Max(e => e.Amount) : 0,
                ExpensesByCategory = expensesByCategory,

                BarberRows = barberRows,
                MassageRows = massageRows,
                WorkHours = workHours,

                Invoices = allSales,
                Expenses = expenses,
                Advances = advances,
                TipInvoices = tipInvoices,

                DailyNotes = shift?.Notes ?? string.Empty,
                ShiftId = shift?.Id ?? 0,
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveNote(int shiftId, string notes, string date)
        {
            if (shiftId > 0)
            {
                var shift = await _context.Shifts.FindAsync(shiftId);
                if (shift != null && !await _closure.IsDateLockedAsync(shift.ShiftDate, null))
                {
                    shift.Notes = notes;
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction("Index", new { date });
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePaymentSplit(int saleId, decimal cashAmount, decimal linkAmount, string date, string? dept)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale != null && !await _closure.IsDateLockedAsync(sale.SaleDate, sale.SaleType))
            {
                sale.CashAmount = cashAmount;
                sale.LinkAmount = linkAmount;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", new { date, dept });
        }
    }
}