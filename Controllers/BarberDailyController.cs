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
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isEmployee = User.IsInRole("Employee");
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            bool isBarberOnly = userDept == "حلاقة";
            bool isMassageOnly = userDept == "مساج";
            bool showBoth = !isBarberOnly && !isMassageOnly;

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive && e.DepartmentNav != null);

            // Employee role → only their own record; Cashier → filter by department
            if (isEmployee)
                empQuery = empQuery.Where(e => e.Id == (linkedEmpId ?? -1));
            else if (isBarberOnly)
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة");
            else if (isMassageOnly)
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "مساج");
            else
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة" || e.DepartmentNav!.Name == "مساج");

            var employees = await empQuery
                .OrderBy(e => e.DepartmentNav!.Name)
                .ThenBy(e => e.FullName)
                .ToListAsync();

            var employeeIds = employees.Select(e => e.Id).ToList();

            var staffSalesQuery = _context.Sales
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow && s.Status != "ملغي");

            // Employee sees only their own sales; Cashier sees their department
            if (isEmployee)
                staffSalesQuery = staffSalesQuery.Where(s => s.EmployeeId == (linkedEmpId ?? -1));
            else if (isBarberOnly)
                staffSalesQuery = staffSalesQuery.Where(s => s.SaleType == "حلاقة");
            else if (isMassageOnly)
                staffSalesQuery = staffSalesQuery.Where(s => s.SaleType == "مساج");
            else
                staffSalesQuery = staffSalesQuery.Where(s => s.SaleType == "حلاقة" || s.SaleType == "مساج");

            var staffSales = await staffSalesQuery.ToListAsync();

            var productSales = await _context.Sales
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow
                         && s.Status != "ملغي" && s.SaleType == "منتجات")
                .ToListAsync();

            var attendances = await _context.Attendances
                .Where(a => a.AttendanceDate >= today && a.AttendanceDate < tomorrow
                         && employeeIds.Contains(a.EmployeeId))
                .ToListAsync();

            var todayAdvances = await _context.EmployeeAdvances
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow
                         && employeeIds.Contains(a.EmployeeId))
                .ToListAsync();

            var shift = await _context.Shifts
                .Where(s => s.ShiftDate >= today && s.ShiftDate < tomorrow)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            var staffRows = employees.Select(emp =>
            {
                var empDeptSaleType = emp.DepartmentNav?.Name == "مساج" ? "مساج" : "حلاقة";
                var sales = staffSales.Where(s => s.EmployeeId == emp.Id && s.SaleType == empDeptSaleType).ToList();
                var totalWork = sales.Sum(s => s.NetAmount);
                var knet = sales.Sum(s =>
                    knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
                var cash = sales.Sum(s =>
                    cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                    mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
                var debts = sales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
                var advance = todayAdvances.Where(a => a.EmployeeId == emp.Id).Sum(a => a.Amount);
                var commission = emp.Commission;
                var dueAmount = Math.Round(totalWork * commission / 100, 3);
                var deductions = advance + debts;
                return new BarberDailyRow
                {
                    Employee = emp,
                    TotalWork = totalWork,
                    KNet = knet,
                    Cash = cash,
                    Debts = debts,
                    Advance = advance,
                    CommissionPercent = commission,
                    DueAmount = dueAmount,
                    Deductions = deductions,
                    NetAfterDeduction = dueAmount - deductions,
                    ShopNet = totalWork - dueAmount
                };
            }).ToList();

            var totalRevenue = staffSales.Sum(s => s.NetAmount);
            var totalKNet = staffSales.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            var totalCash = staffSales.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);

            var attendedIds = attendances.Select(a => a.EmployeeId).Distinct().ToList();
            var presentCount = attendances.Count(a => a.Status == "حاضر");
            var absentCount = attendances.Count(a => a.Status == "غائب")
                            + employeeIds.Count(id => !attendedIds.Contains(id));
            var vacationCount = attendances.Count(a => a.Status == "إجازة");
            var lateCount = attendances.Count(a => a.Status == "متأخر");
            var earlyLeaveCount = attendances.Count(a => a.Status == "منصرف مبكراً");

            var reportCount = await _context.Sales
                .Where(s => s.SaleDate.Date <= today)
                .Select(s => s.SaleDate.Date)
                .Distinct()
                .CountAsync();

            string[] arabicDays = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
            var dayName = arabicDays[(int)today.DayOfWeek];

            var closingTime = shift?.EndTime != null
                ? DateTime.Today.Add(shift.EndTime.Value).ToString("hh:mm tt")
                : DateTime.Now.ToString("hh:mm tt");
            var cashierName = shift?.CashierName ?? "-";

            // Employees don't see the cash movement (store treasury is not their concern)
            var cashMovement = new List<CashMovementRow>();
            if (!isEmployee)
            {
                var monthAllSales = await _context.Sales
                    .Where(s => s.SaleDate >= monthStart && s.SaleDate < tomorrow && s.Status != "ملغي")
                    .ToListAsync();

                if (isBarberOnly)
                    monthAllSales = monthAllSales.Where(s => s.SaleType == "حلاقة").ToList();
                else if (isMassageOnly)
                    monthAllSales = monthAllSales.Where(s => s.SaleType == "مساج").ToList();

                var expQuery = _context.Expenses
                    .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate < tomorrow);

                if (isBarberOnly)
                    expQuery = expQuery.Where(e => e.Department == "حلاقة" || e.Department == null || e.Department == "");
                else if (isMassageOnly)
                    expQuery = expQuery.Where(e => e.Department == "مساج" || e.Department == null || e.Department == "");

                var monthExpenses = await expQuery
                    .OrderBy(e => e.ExpenseDate).ThenBy(e => e.Id)
                    .ToListAsync();

                var monthAdvances = await _context.EmployeeAdvances
                    .Include(a => a.Employee)
                    .Where(a => a.AdvanceDate >= monthStart && a.AdvanceDate < tomorrow
                             && employeeIds.Contains(a.EmployeeId))
                    .OrderBy(a => a.AdvanceDate).ThenBy(a => a.Id)
                    .ToListAsync();

                var firstDayShift = await _context.Shifts
                    .Where(s => s.ShiftDate >= monthStart && s.ShiftDate < monthStart.AddDays(1))
                    .OrderBy(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                decimal runningBalance = firstDayShift?.OpeningBalance ?? 0;

                var withdrawalEvents = new List<(DateTime Date, decimal Amount, string Type, string Notes)>();
                foreach (var exp in monthExpenses)
                    withdrawalEvents.Add((exp.ExpenseDate.Date, exp.Amount,
                        !string.IsNullOrEmpty(exp.Category) ? $"سحب لـ{exp.Category}" : "سحب لمصروفات",
                        exp.Description));
                foreach (var adv in monthAdvances)
                    withdrawalEvents.Add((adv.AdvanceDate.Date, adv.Amount,
                        "سحب لدفع سلف موظفين",
                        $"سلفة {adv.Employee?.FullName ?? ""}".Trim()));

                withdrawalEvents = withdrawalEvents.OrderBy(x => x.Date).ThenBy(x => x.Type).ToList();

                for (var d = monthStart; d <= today; d = d.AddDays(1))
                {
                    var dailyCashRev = monthAllSales
                        .Where(s => s.SaleDate.Date == d)
                        .Sum(s =>
                            cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                            mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);

                    var dayWithdrawals = withdrawalEvents.Where(e => e.Date == d).ToList();

                    if (!dayWithdrawals.Any())
                    {
                        var closing = runningBalance + dailyCashRev;
                        cashMovement.Add(new CashMovementRow
                        {
                            Date = d,
                            OpeningBalance = runningBalance,
                            CashRevenue = dailyCashRev,
                            Withdrawal = 0,
                            WithdrawalType = "-",
                            WithdrawnBy = "-",
                            ClosingBalance = closing,
                            Notes = "-"
                        });
                        runningBalance = closing;
                    }
                    else
                    {
                        bool first = true;
                        foreach (var (_, amount, type, notes) in dayWithdrawals)
                        {
                            var rev = first ? dailyCashRev : 0;
                            var closing = runningBalance + rev - amount;
                            cashMovement.Add(new CashMovementRow
                            {
                                Date = d,
                                OpeningBalance = runningBalance,
                                CashRevenue = rev,
                                Withdrawal = amount,
                                WithdrawalType = type,
                                WithdrawnBy = "المدير",
                                ClosingBalance = closing,
                                Notes = notes
                            });
                            runningBalance = closing;
                            first = false;
                        }
                    }
                }
            }

            var vm = new BarberDailyReportViewModel
            {
                ReportDate = today,
                ReportNumber = $"DY-{reportCount:D6}",
                DayName = dayName,
                CashierName = cashierName,
                ClosingTime = closingTime,
                UserDepartment = userDept,
                TotalRevenue = totalRevenue,
                TotalKNet = totalKNet,
                TotalCash = totalCash,
                ProductSalesTotal = productSales.Sum(s => s.NetAmount),
                NetShopIncome = totalRevenue - staffRows.Sum(r => r.DueAmount),
                RegisteredBarbers = employees.Count,
                PresentToday = presentCount,
                AbsentToday = absentCount,
                VacationToday = vacationCount,
                LateToday = lateCount,
                EarlyLeaveToday = earlyLeaveCount,
                BarberRows = staffRows,
                CashMovement = cashMovement,
                MonthStart = monthStart
            };

            return View(vm);
        }
    }
}