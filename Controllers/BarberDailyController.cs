using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class BarberDailyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BarberDailyController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? date)
        {
            var today = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isEmployee = User.IsInRole("Employee");
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            bool isBarberOnly = userDept == "حلاقة";
            bool isMassageOnly = userDept == "مساج";

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            // --- Sales for today ---
            var salesQuery = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow && s.Status != "ملغي");

            if (isEmployee)
                salesQuery = salesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1));
            else if (isBarberOnly)
                salesQuery = salesQuery.Where(s => s.SaleType == "حلاقة");
            else if (isMassageOnly)
                salesQuery = salesQuery.Where(s => s.SaleType == "مساج");

            var allSales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
            var staffSales = allSales.Where(s => s.SaleType == "حلاقة" || s.SaleType == "مساج").ToList();

            // --- Employees ---
            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive && e.DepartmentNav != null);

            if (isEmployee)
                empQuery = empQuery.Where(e => e.Id == (linkedEmpId ?? -1));
            else if (isBarberOnly)
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة");
            else if (isMassageOnly)
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "مساج");
            else
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة" || e.DepartmentNav!.Name == "مساج");

            var employees = await empQuery.OrderBy(e => e.DepartmentNav!.Name).ThenBy(e => e.FullName).ToListAsync();
            var employeeIds = employees.Select(e => e.Id).ToList();

            // --- Expenses today ---
            var expQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);

            if (isBarberOnly)
                expQuery = expQuery.Where(e => e.Department == "حلاقة" || e.Department == null || e.Department == "");
            else if (isMassageOnly)
                expQuery = expQuery.Where(e => e.Department == "مساج" || e.Department == null || e.Department == "");

            var expenses = await expQuery.OrderBy(e => e.Id).ToListAsync();

            // --- Advances today ---
            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee)
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow);

            if (!isEmployee)
                advancesQuery = advancesQuery.Where(a => employeeIds.Contains(a.EmployeeId));
            else
                advancesQuery = advancesQuery.Where(a => a.EmployeeId == (linkedEmpId ?? -1));

            var advances = await advancesQuery.OrderBy(a => a.Id).ToListAsync();

            // --- Deposits today ---
            var deposits = await _context.Deposits
                .Where(d => d.DepositDate >= today && d.DepositDate < tomorrow)
                .ToListAsync();

            // --- Withdrawals today ---
            var withdrawals = await _context.Withdrawals
                .Where(w => w.WithdrawalDate >= today && w.WithdrawalDate < tomorrow)
                .ToListAsync();

            // --- Shift for opening balance ---
            var shift = await _context.Shifts
                .Where(s => s.ShiftDate >= today && s.ShiftDate < tomorrow)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            // --- Revenue totals ---
            decimal totalSales = staffSales.Sum(s => s.NetAmount);
            decimal cashRevenue = staffSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            decimal knetRevenue = staffSales.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            decimal debtRevenue = staffSales.Where(s =>
                s.PaymentMethod == "دين على الموظف" || s.PaymentMethod == "دين على صاحب المكان")
                .Sum(s => s.NetAmount);

            // Tips/gratuities
            var tipInvoices = allSales.Where(s => (s.GiftForEmployee ?? 0) > 0).ToList();
            decimal tipsTotal = allSales.Sum(s => s.GiftForEmployee ?? 0);
            decimal tipsDelivered = allSales.Sum(s => s.EmployeeGift ?? 0);

            // Total discount
            decimal totalDiscount = staffSales.Sum(s => s.Discount);

            // Expenses analytics
            decimal totalExpenses = expenses.Sum(e => e.Amount);
            var expensesByCategory = expenses
                .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "أخرى" : e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            // Build per-employee performance row helper
            EmployeePerformanceRow BuildRow(Employee emp)
            {
                var empDeptType = emp.DepartmentNav?.Name == "مساج" ? "مساج" : "حلاقة";
                var empSales = staffSales.Where(s => s.EmployeeId == emp.Id && s.SaleType == empDeptType).ToList();
                var empTotal = empSales.Sum(s => s.NetAmount);
                var empCash = empSales.Sum(s =>
                    cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
                var empKNet = empSales.Sum(s =>
                    knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
                var empDebts = empSales.Where(s =>
                    s.PaymentMethod == "دين على الموظف" || s.PaymentMethod == "دين على صاحب المكان")
                    .Sum(s => s.NetAmount);
                var empAdv = advances.Where(a => a.EmployeeId == emp.Id).Sum(a => a.Amount);
                return new EmployeePerformanceRow
                {
                    Employee = emp,
                    InvoiceCount = empSales.Count,
                    TotalSales = empTotal,
                    InstantCollection = empCash + empKNet,
                    Debts = empDebts,
                    Advances = empAdv,
                    SalesPercent = totalSales > 0 ? Math.Round(empTotal / totalSales * 100, 1) : 0
                };
            }

            var barberEmployees = employees.Where(e => e.DepartmentNav?.Name == "حلاقة").ToList();
            var massageEmployees = employees.Where(e => e.DepartmentNav?.Name == "مساج").ToList();
            var barberRows = barberEmployees.Select(BuildRow).ToList();
            var massageRows = massageEmployees.Select(BuildRow).ToList();

            // Report number
            var reportCount = await _context.Sales
                .Where(s => s.SaleDate.Date <= today)
                .Select(s => s.SaleDate.Date).Distinct().CountAsync();

            string[] arabicDays = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };

            // Shift hours
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

                TotalSales = totalSales,
                InvoiceCount = staffSales.Count,
                CashTotal = cashRevenue,
                KNetTotal = knetRevenue,
                DebtTotal = debtRevenue,
                TotalDiscount = totalDiscount,
                TipsTotal = tipsTotal,
                TipsDelivered = tipsDelivered,

                OpeningBalance = shift?.OpeningBalance ?? 0,
                CashRevenue = cashRevenue,
                TotalDeposits = deposits.Sum(d => d.Amount),
                TotalExpensesAmount = totalExpenses,
                TotalAdvancesAmount = advances.Sum(a => a.Amount),
                TotalWithdrawals = withdrawals.Sum(w => w.Amount),

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
                ShiftId = shift?.Id ?? 0
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveNote(int shiftId, string notes, string date)
        {
            if (shiftId > 0)
            {
                var shift = await _context.Shifts.FindAsync(shiftId);
                if (shift != null)
                {
                    shift.Notes = notes;
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction("Index", new { date });
        }
    }
}
