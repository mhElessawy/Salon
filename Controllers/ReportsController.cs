using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Sales(string? from, string? to, int? employeeId, int? customerId, string? saleType, string? paymentMethod)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : DateTime.Parse(to).AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate < dateTo);

            if (userDept == "مساج")
                query = query.Where(s => s.SaleType == "مساج");
            else if (userDept == "حلاقة")
                query = query.Where(s => s.SaleType == "حلاقة");

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId);

            if (customerId.HasValue)
                query = query.Where(s => s.CustomerId == customerId);

            if (!string.IsNullOrEmpty(saleType))
                query = query.Where(s => s.SaleType == saleType);

            if (!string.IsNullOrEmpty(paymentMethod))
            {
                if (paymentMethod == "كاش")
                    query = query.Where(s => s.PaymentMethod == "كاش" || s.PaymentMethod == "نقدي" || s.PaymentMethod == "Cash");
                else if (paymentMethod == "كي نت")
                    query = query.Where(s => s.PaymentMethod == "كي نت" || s.PaymentMethod == "بطاقة" || s.PaymentMethod == "تحويل بنكي" || s.PaymentMethod == "K-Net");
                else
                    query = query.Where(s => s.PaymentMethod == paymentMethod);
            }

            var allSalesRaw = await query.OrderByDescending(s => s.SaleDate).ToListAsync();
            var sales = allSalesRaw; // kept for view model
            var activeSales = allSalesRaw.Where(s => s.Status != "ملغي").ToList();
            var cancelledSales = allSalesRaw.Where(s => s.Status == "ملغي").ToList();

            var employees = await _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            string[] cashMethodsSales = { "كاش", "نقدي", "Cash" };
            string[] knetMethodsSales = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethodsSales = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalSales = activeSales.Sum(s => s.NetAmount);
            ViewBag.TotalCount = activeSales.Count;
            ViewBag.TotalHaircut = activeSales.Where(s => s.SaleType == "حلاقة").Sum(s => s.NetAmount);
            ViewBag.TotalMassage = activeSales.Where(s => s.SaleType == "مساج").Sum(s => s.NetAmount);
            ViewBag.TotalProducts = activeSales.Where(s => s.SaleType == "منتجات").Sum(s => s.NetAmount);
            ViewBag.TotalCash = activeSales.Sum(s =>
                cashMethodsSales.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethodsSales.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            ViewBag.TotalKnet = activeSales.Sum(s =>
                knetMethodsSales.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethodsSales.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            ViewBag.TotalEmployeeDebt = activeSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            ViewBag.TotalOwnerDebt = activeSales.Where(s => s.PaymentMethod == "دين على صاحب المكان").Sum(s => s.NetAmount);
            ViewBag.TotalCancelled = cancelledSales.Sum(s => s.NetAmount);
            ViewBag.TotalCancelledCount = cancelledSales.Count;
            ViewBag.TotalGifts = activeSales.Sum(s => s.EmployeeGift ?? 0);
            ViewBag.TotalHadiya = activeSales.Sum(s => s.GiftForEmployee ?? 0);
            ViewBag.Employees = employees;
            ViewBag.Customers = customers;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedCustomerId = customerId;
            ViewBag.SelectedSaleType = saleType;
            ViewBag.SelectedPaymentMethod = paymentMethod;
            ViewBag.UserDept = userDept;

            // مصاريف الفترة — مفلترة حسب القسم إذا كان الفلتر محدداً
            var expensesQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= dateFrom && e.ExpenseDate < dateTo);

            if (saleType == "مساج")
                expensesQuery = expensesQuery.Where(e => e.Department == "مساج");
            else if (saleType == "حلاقة")
                expensesQuery = expensesQuery.Where(e => e.Department == "حلاقة");

            var salesExpenses = await expensesQuery.OrderBy(e => e.ExpenseDate).ToListAsync();

            var salariesQuery = _context.Salaries
                .Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(s => s.PaidDate >= dateFrom && s.PaidDate < dateTo);

            if (saleType == "مساج")
                salariesQuery = salariesQuery.Where(s => s.Employee!.DepartmentNav!.Name == "مساج");
            else if (saleType == "حلاقة")
                salariesQuery = salariesQuery.Where(s => s.Employee!.DepartmentNav!.Name == "حلاقة");

            var salesSalaries = await salariesQuery.OrderBy(s => s.PaidDate).ToListAsync();

            var depositsQuery = _context.Deposits
                .Where(d => d.DepositDate >= dateFrom && d.DepositDate < dateTo);

            if (!string.IsNullOrEmpty(saleType))
                depositsQuery = depositsQuery.Where(d => d.Department == saleType);
            else if (userDept == "مساج" || userDept == "حلاقة")
                depositsQuery = depositsQuery.Where(d => d.Department == userDept);

            var salesDeposits = await depositsQuery.OrderBy(d => d.DepositDate).ToListAsync();

            ViewBag.ExpensesInRange = salesExpenses;
            ViewBag.TotalExpensesAmount = salesExpenses.Sum(e => e.Amount);
            ViewBag.SalariesInRange = salesSalaries;
            ViewBag.TotalSalariesAmount = salesSalaries.Sum(s => s.NetSalary);
            ViewBag.TotalCombinedExpenses = salesExpenses.Sum(e => e.Amount) + salesSalaries.Sum(s => s.NetSalary);
            ViewBag.DepositsInRange = salesDeposits;
            ViewBag.TotalDepositsAmount = salesDeposits.Sum(d => d.Amount);

            return View(sales);
        }

        public async Task<IActionResult> Expenses(string? from, string? to)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? DateTime.Today.AddDays(-30) : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : DateTime.Parse(to).AddDays(1);

            var expenses = await _context.Expenses
                .Where(e => e.ExpenseDate >= dateFrom && e.ExpenseDate < dateTo)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            var salaries = await _context.Salaries
                .Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(s => s.PaidDate >= dateFrom && s.PaidDate < dateTo)
                .OrderBy(s => s.PaidDate)
                .ToListAsync();

            // الكل
            decimal totalExp = expenses.Sum(e => e.Amount);
            decimal totalSal = salaries.Sum(s => s.NetSalary);
            ViewBag.TotalExpenses = totalExp;
            ViewBag.TotalSalaries = totalSal;
            ViewBag.TotalCombined = totalExp + totalSal;
            ViewBag.Salaries = salaries;

            // حلاقة
            var barberExpenses = expenses.Where(e => e.Department == "حلاقة").ToList();
            var barberSalaries = salaries.Where(s => s.Employee?.Department == "حلاقة").ToList();
            decimal barberExp = barberExpenses.Sum(e => e.Amount);
            decimal barberSal = barberSalaries.Sum(s => s.NetSalary);
            ViewBag.BarberExpenses = barberExpenses;
            ViewBag.BarberSalaries = barberSalaries;
            ViewBag.TotalBarberExpenses = barberExp;
            ViewBag.TotalBarberSalaries = barberSal;
            ViewBag.TotalBarberCombined = barberExp + barberSal;

            // مساج
            var massageExpenses = expenses.Where(e => e.Department == "مساج").ToList();
            var massageSalaries = salaries.Where(s => s.Employee?.Department == "مساج").ToList();
            decimal massageExp = massageExpenses.Sum(e => e.Amount);
            decimal massageSal = massageSalaries.Sum(s => s.NetSalary);
            ViewBag.MassageExpenses = massageExpenses;
            ViewBag.MassageSalaries = massageSalaries;
            ViewBag.TotalMassageExpenses = massageExp;
            ViewBag.TotalMassageSalaries = massageSal;
            ViewBag.TotalMassageCombined = massageExp + massageSal;

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            return View(expenses);
        }

        public async Task<IActionResult> MyReport(string? saleType, string? paymentMethod, int? employeeId, string? date)
        {
            var today = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var tomorrow = today.AddDays(1);
            bool isToday = today == DateTime.Today;

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var baseQuery = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);

            if (userDept == "حلاقة")
                baseQuery = baseQuery.Where(s => s.SaleType != "مساج");
            else if (userDept == "مساج")
                baseQuery = baseQuery.Where(s => s.SaleType != "حلاقة");

            var allSales = await baseQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
            var activeSalesReport = allSales.Where(s => s.Status != "ملغي").ToList();
            var cancelledSalesReport = allSales.Where(s => s.Status == "ملغي").ToList();

            // تطبيق الفلاتر على الجدول
            var filtered = allSales.AsEnumerable();
            if (!string.IsNullOrEmpty(saleType))
                filtered = filtered.Where(s => s.SaleType == saleType);
            if (!string.IsNullOrEmpty(paymentMethod))
                filtered = filtered.Where(s => s.PaymentMethod == paymentMethod);
            if (employeeId.HasValue)
                filtered = filtered.Where(s => s.EmployeeId == employeeId);
            var filteredList = filtered.ToList();

            // القسم الفعّال: يُعطى الأولوية لقسم المستخدم، ثم فلتر النوع المحدد
            string? filterDept = (userDept == "حلاقة" || userDept == "مساج") ? userDept
                : (saleType == "حلاقة" || saleType == "مساج") ? saleType : null;

            var expensesTodayQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);
            if (!string.IsNullOrEmpty(filterDept))
                expensesTodayQuery = expensesTodayQuery.Where(e => e.Department == filterDept);
            var expensesTodayList = await expensesTodayQuery
                .OrderBy(e => e.ExpenseDate)
                .ToListAsync();
            var expensesToday = expensesTodayList.Sum(e => e.Amount);

            var salariesTodayQuery = _context.Salaries
                .Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(s => s.PaidDate >= today && s.PaidDate < tomorrow);
            if (!string.IsNullOrEmpty(filterDept))
                salariesTodayQuery = salariesTodayQuery.Where(s => s.Employee!.DepartmentNav!.Name == filterDept);
            var salariesTodayList = await salariesTodayQuery
                .OrderBy(s => s.Employee!.FullName)
                .ToListAsync();
            var salariesToday = salariesTodayList.Sum(s => s.NetSalary);

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow);
            if (!string.IsNullOrEmpty(filterDept))
                advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == filterDept);

            var advancesList = await advancesQuery.OrderBy(a => a.AdvanceDate).ToListAsync();
            var advancesToday = advancesList.Sum(a => a.Amount);

            var depositsTodayQuery = _context.Deposits
                .Where(d => d.DepositDate >= today && d.DepositDate < tomorrow);
            if (!string.IsNullOrEmpty(filterDept))
                depositsTodayQuery = depositsTodayQuery.Where(d => d.Department == filterDept);
            var depositsTodayList = await depositsTodayQuery
                .OrderBy(d => d.DepositDate)
                .ToListAsync();
            var depositsToday = depositsTodayList.Sum(d => d.Amount);

            var salesToday = activeSalesReport.Sum(s => s.NetAmount);

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };
            string[] debtMethods = { "دين على العميل", "دين على الموظف", "دين على صاحب المكان", "آجل", "Customer Debit", "Employee Debit", "Owner Debit" };

            ViewBag.SalesToday = salesToday;
            ViewBag.ExpensesToday = expensesToday;
            ViewBag.ExpensesTodayList = expensesTodayList;
            ViewBag.SalariesToday = salariesToday;
            ViewBag.SalariesTodayList = salariesTodayList;
            ViewBag.TotalExpensesWithSalaries = expensesToday + salariesToday;
            ViewBag.AdvancesToday = advancesToday;
            ViewBag.AdvancesList = advancesList;
            ViewBag.DepositsToday = depositsToday;
            ViewBag.DepositsTodayList = depositsTodayList;
            ViewBag.NetProfit = salesToday + depositsToday - expensesToday - salariesToday - advancesToday;
            ViewBag.BarberSales = activeSalesReport.Where(s => s.SaleType == "حلاقة").Sum(s => s.NetAmount);
            ViewBag.MassageSales = activeSalesReport.Where(s => s.SaleType == "مساج").Sum(s => s.NetAmount);
            ViewBag.CashTotal = activeSalesReport.Sum(s =>
                cashMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            ViewBag.KnetTotal = activeSalesReport.Sum(s =>
                knetMethods.Contains(s.PaymentMethod) ? s.NetAmount :
                mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);
            ViewBag.DebtTotal = activeSalesReport
                .Where(s => debtMethods.Contains(s.PaymentMethod))
                .Sum(s => s.NetAmount);
            ViewBag.EmployeeDebtTotal = activeSalesReport.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            ViewBag.OwnerDebtTotal = activeSalesReport.Where(s => s.PaymentMethod == "دين على صاحب المكان").Sum(s => s.NetAmount);
            ViewBag.CancelledTotal = cancelledSalesReport.Sum(s => s.NetAmount);
            ViewBag.CancelledCount = cancelledSalesReport.Count;
            ViewBag.TotalGiftsToday = activeSalesReport.Sum(s => s.EmployeeGift ?? 0);
            ViewBag.TotalHadiyaToday = activeSalesReport.Sum(s => s.GiftForEmployee ?? 0);

            // تشخيص: تفاصيل طرق الدفع الفعلية في قاعدة البيانات
            ViewBag.PaymentBreakdown = activeSalesReport
                .GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentMethod) ? "(غير محدد)" : s.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(x => x.NetAmount), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .ToList();
            ViewBag.Date = today.ToString("yyyy/MM/dd");
            ViewBag.SelectedDate = today.ToString("yyyy-MM-dd");
            ViewBag.IsToday = isToday;
            ViewBag.UserDept = userDept;
            ViewBag.Employees = allSales
                .Where(s => s.Employee != null)
                .Select(s => s.Employee!)
                .DistinctBy(e => e.Id)
                .OrderBy(e => e.FullName)
                .ToList();
            ViewBag.SelectedSaleType = saleType;
            ViewBag.SelectedPaymentMethod = paymentMethod;
            ViewBag.SelectedEmployeeId = employeeId;
            return View(filteredList);
        }

        public async Task<IActionResult> EvaluationList(string? from, string? to, string? dept)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            DateTime dateFrom = string.IsNullOrEmpty(from)
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to)
                ? DateTime.Today.AddDays(1)
                : DateTime.Parse(to).AddDays(1);

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (userDept == "حلاقة")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة");
            else if (userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "مساج");
            else if (!string.IsNullOrEmpty(dept))
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == dept);

            var employees = await empQuery.OrderBy(e => e.FullName).ToListAsync();

            var allSales = await _context.Sales
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate < dateTo && s.EmployeeId.HasValue)
                .Select(s => new { s.EmployeeId, s.NetAmount })
                .ToListAsync();

            var allAttendances = await _context.Attendances
                .Where(a => a.AttendanceDate >= dateFrom.Date && a.AttendanceDate < dateTo.Date)
                .Select(a => new { a.EmployeeId, a.Status })
                .ToListAsync();

            int periodDays = (int)(dateTo.Date - dateFrom.Date).TotalDays;

            var rows = employees.Select(emp => new EmployeeEvaluationRow
            {
                Employee = emp,
                TotalTransactions = allSales.Count(s => s.EmployeeId == emp.Id),
                TotalSales = allSales.Where(s => s.EmployeeId == emp.Id).Sum(s => s.NetAmount),
                PresentDays = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "حاضر"),
                AbsentDays = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "غائب"),
                LeaveDays = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "إجازة"),
                TotalAttendanceRecords = allAttendances.Count(a => a.EmployeeId == emp.Id),
                PeriodDays = periodDays,
            }).ToList();

            var vm = new EmployeeEvaluationListViewModel
            {
                Rows = rows,
                DateFrom = dateFrom,
                DateTo = dateTo.AddDays(-1),
                Department = userDept ?? dept,
            };

            return View(vm);
        }

        public async Task<IActionResult> BarberDailyReport(string? date)
        {
            var today = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment; // null or "الكل" = admin (see all)
            bool isEmployee = User.IsInRole("Employee");
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            bool isBarberOnly = userDept == "حلاقة";
            bool isMassageOnly = userDept == "مساج";
            bool showBoth = !isBarberOnly && !isMassageOnly;

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            // Employees for the relevant department(s)
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

            // Staff sales filtered by department
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

            // Per-employee rows
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
                var gift = sales.Sum(s => s.EmployeeGift ?? 0);
                var hadiya = sales.Sum(s => s.GiftForEmployee ?? 0);
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
                    ShopNet = totalWork - dueAmount,
                    Gift = gift,
                    Hadiya = hadiya
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
                // Cash movement (always uses all cash sales — the safe is shared)
                var monthAllSales = await _context.Sales
                    .Where(s => s.SaleDate >= monthStart && s.SaleDate < tomorrow && s.Status != "ملغي")
                    .ToListAsync();

                // For dept-specific users, show only their dept cash in the movement
                if (isBarberOnly)
                    monthAllSales = monthAllSales.Where(s => s.SaleType == "حلاقة").ToList();
                else if (isMassageOnly)
                    monthAllSales = monthAllSales.Where(s => s.SaleType == "مساج").ToList();

                var monthExpenses = await _context.Expenses
                    .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate < tomorrow)
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

        public async Task<IActionResult> CashMovement(string? from, string? to, string? type, string? dept)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : DateTime.Parse(to).AddDays(1);

            var items = new List<CashMovementReportItem>();

            bool showExpenses = string.IsNullOrEmpty(type) || type == "مصروف";
            bool showDeposits = string.IsNullOrEmpty(type) || type == "إيداع";
            bool showCashSales = string.IsNullOrEmpty(type) || type == "كاش";
            bool showKNetSales = string.IsNullOrEmpty(type) || type == "كي نت";
            bool showWithdrawals = string.IsNullOrEmpty(type) || type == "سحب";
            bool filterDept = !string.IsNullOrEmpty(dept);

            if (showExpenses)
            {
                var expensesQuery = _context.Expenses
                    .Where(e => e.ExpenseDate >= dateFrom && e.ExpenseDate < dateTo);
                if (filterDept)
                    expensesQuery = expensesQuery.Where(e => e.Department == dept);
                var expenses = await expensesQuery
                    .OrderByDescending(e => e.ExpenseDate)
                    .ToListAsync();

                items.AddRange(expenses.Select(e => new CashMovementReportItem
                {
                    Date = e.ExpenseDate,
                    Type = "مصروف",
                    Description = e.Description,
                    Amount = e.Amount,
                    Category = e.Category,
                    Notes = e.Notes
                }));

                var advancesQuery = _context.EmployeeAdvances
                    .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                    .Where(a => a.AdvanceDate >= dateFrom && a.AdvanceDate < dateTo);
                if (filterDept)
                    advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == dept);
                var advances = await advancesQuery
                    .OrderByDescending(a => a.AdvanceDate)
                    .ToListAsync();

                items.AddRange(advances.Select(a => new CashMovementReportItem
                {
                    Date = a.AdvanceDate,
                    Type = "سلفة",
                    Description = $"سلفة - {a.Employee?.FullName ?? ""}".Trim(' ', '-'),
                    Amount = a.Amount,
                    Category = "سلف الموظفين",
                    Notes = a.Reason
                }));

                var salariesQuery = _context.Salaries
                    .Include(s => s.Employee).ThenInclude(e => e!.DepartmentNav)
                    .Where(s => s.PaidDate.HasValue && s.PaidDate.Value >= dateFrom && s.PaidDate.Value < dateTo);
                if (filterDept)
                    salariesQuery = salariesQuery.Where(s => s.Employee!.DepartmentNav!.Name == dept);
                var salaries = await salariesQuery
                    .OrderByDescending(s => s.PaidDate)
                    .ToListAsync();

                items.AddRange(salaries.Select(s => new CashMovementReportItem
                {
                    Date = s.PaidDate!.Value,
                    Type = "راتب",
                    Description = $"راتب - {s.Employee?.FullName ?? ""}".Trim(' ', '-'),
                    Amount = s.NetSalary,
                    Category = "رواتب الموظفين",
                    Notes = s.Notes
                }));
            }

            if (showDeposits)
            {
                var deposits = await _context.Deposits
                    .Where(d => d.DepositDate >= dateFrom && d.DepositDate < dateTo)
                    .OrderByDescending(d => d.DepositDate)
                    .ToListAsync();

                items.AddRange(deposits.Select(d => new CashMovementReportItem
                {
                    Date = d.DepositDate,
                    Type = "إيداع",
                    Description = d.Description,
                    Amount = d.Amount,
                    Category = d.Source,
                    Notes = d.Notes
                }));
            }

            if (showCashSales || showKNetSales)
            {
                var salesQuery = _context.Sales
                    .Where(s => s.SaleDate >= dateFrom && s.SaleDate < dateTo && s.Status != "ملغي");

                if (filterDept)
                    salesQuery = salesQuery.Where(s => s.SaleType == dept);

                var salesRaw = await salesQuery.ToListAsync();

                if (showCashSales)
                {
                    var dailyCash = salesRaw
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Amount = g.Sum(s => s.PaymentMethod == "كاش"
                                ? s.NetAmount
                                : s.PaymentMethod == "كي نت و كاش"
                                    ? (s.CashAmount ?? 0)
                                    : 0m),
                            Count = g.Count(s => s.PaymentMethod == "كاش" || s.PaymentMethod == "كي نت و كاش")
                        })
                        .Where(d => d.Amount > 0);

                    items.AddRange(dailyCash.Select(d => new CashMovementReportItem
                    {
                        Date = d.Date,
                        Type = "مبيعات كاش",
                        Description = $"مبيعات كاش {d.Date:yyyy/MM/dd}",
                        Amount = d.Amount,
                        Category = $"{d.Count} فاتورة",
                        Notes = ""
                    }));
                }

                if (showKNetSales)
                {
                    var dailyKNet = salesRaw
                        .GroupBy(s => s.SaleDate.Date)
                        .Select(g => new
                        {
                            Date = g.Key,
                            Amount = g.Sum(s => s.PaymentMethod == "كي نت"
                                ? s.NetAmount
                                : s.PaymentMethod == "كي نت و كاش"
                                    ? (s.LinkAmount ?? 0)
                                    : 0m),
                            Count = g.Count(s => s.PaymentMethod == "كي نت" || s.PaymentMethod == "كي نت و كاش")
                        })
                        .Where(d => d.Amount > 0);

                    items.AddRange(dailyKNet.Select(d => new CashMovementReportItem
                    {
                        Date = d.Date,
                        Type = "كي نت",
                        Description = $"مبيعات كي نت {d.Date:yyyy/MM/dd}",
                        Amount = d.Amount,
                        Category = $"{d.Count} فاتورة",
                        Notes = ""
                    }));
                }
            }

            if (showWithdrawals)
            {
                var withdrawals = await _context.Withdrawals
                    .Where(w => w.WithdrawalDate >= dateFrom && w.WithdrawalDate < dateTo)
                    .OrderByDescending(w => w.WithdrawalDate)
                    .ToListAsync();

                items.AddRange(withdrawals.Select(w => new CashMovementReportItem
                {
                    Date = w.WithdrawalDate,
                    Type = "سحب",
                    Description = w.Description,
                    Amount = w.Amount,
                    Category = w.Reason,
                    Notes = w.Notes
                }));
            }

            items = items.OrderByDescending(i => i.Date).ThenBy(i => i.Type).ToList();

            decimal totalMasrouf = items.Where(i => i.Type == "مصروف").Sum(i => i.Amount);
            decimal totalSulfa = items.Where(i => i.Type == "سلفة").Sum(i => i.Amount);
            decimal totalRatib = items.Where(i => i.Type == "راتب").Sum(i => i.Amount);
            decimal totalExp = totalMasrouf + totalSulfa + totalRatib;
            decimal totalDep = items.Where(i => i.Type == "إيداع").Sum(i => i.Amount);
            decimal totalCashSales = items.Where(i => i.Type == "مبيعات كاش").Sum(i => i.Amount);
            decimal totalKNet = items.Where(i => i.Type == "كي نت").Sum(i => i.Amount);
            decimal totalWithdrawals = items.Where(i => i.Type == "سحب").Sum(i => i.Amount);

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.SelectedType = type;
            ViewBag.SelectedDept = dept;
            ViewBag.TotalMasrouf = totalMasrouf;
            ViewBag.TotalSulfa = totalSulfa;
            ViewBag.TotalRatib = totalRatib;
            ViewBag.TotalExpenses = totalExp;
            ViewBag.TotalDeposits = totalDep;
            ViewBag.TotalCashSales = totalCashSales;
            ViewBag.TotalKNet = totalKNet;
            ViewBag.TotalSales = totalCashSales + totalKNet;
            ViewBag.TotalWithdrawals = totalWithdrawals;
            // رصيد الكاش = مبيعات كاش + إيداعات - مصروفات - سلف - مرتبات - سحوبات (الكي نت خارج الحساب)
            ViewBag.CashBalance = totalCashSales + totalDep - totalExp - totalWithdrawals;
            ViewBag.NetBalance = ViewBag.CashBalance;

            return View(items);
        }

        public async Task<IActionResult> EmployeeEvaluation(int? employeeId, string? from, string? to)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            DateTime dateFrom = string.IsNullOrEmpty(from)
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to)
                ? DateTime.Today.AddDays(1)
                : DateTime.Parse(to).AddDays(1);

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (userDept == "حلاقة")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "حلاقة");
            else if (userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == "مساج");

            var employees = await empQuery.OrderBy(e => e.FullName).ToListAsync();

            Employee? selectedEmp = employeeId.HasValue
                ? employees.FirstOrDefault(e => e.Id == employeeId.Value)
                : null;

            List<Sale> sales = new();
            List<Attendance> attendances = new();

            if (selectedEmp != null)
            {
                sales = await _context.Sales
                    .Include(s => s.SaleItems)
                    .Where(s => s.EmployeeId == selectedEmp.Id
                             && s.SaleDate >= dateFrom && s.SaleDate < dateTo)
                    .OrderByDescending(s => s.SaleDate)
                    .ToListAsync();

                attendances = await _context.Attendances
                    .Where(a => a.EmployeeId == selectedEmp.Id
                             && a.AttendanceDate >= dateFrom.Date && a.AttendanceDate < dateTo.Date)
                    .OrderByDescending(a => a.AttendanceDate)
                    .ToListAsync();
            }

            ViewBag.Employees = employees;
            ViewBag.SelectedEmployee = selectedEmp;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.UserDept = userDept;
            ViewBag.TotalRevenue = sales.Sum(s => s.NetAmount);
            ViewBag.TotalGifts = sales.Sum(s => s.EmployeeGift ?? 0);
            ViewBag.SalesCount = sales.Count;
            ViewBag.AttendanceDays = attendances.Count(a => a.Status == "حاضر");
            ViewBag.Sales = sales;
            ViewBag.Attendances = attendances;

            return View();
        }

        public async Task<IActionResult> Revenue(string? from, string? to, int? employeeId, string? saleType)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : DateTime.Parse(to).AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Sales
                .Include(s => s.Employee)
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate < dateTo && s.Status != "ملغي");

            if (userDept == "مساج")
                query = query.Where(s => s.SaleType == "مساج");
            else if (userDept == "حلاقة")
                query = query.Where(s => s.SaleType == "حلاقة");

            if (!string.IsNullOrEmpty(saleType))
                query = query.Where(s => s.SaleType == saleType);

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId);

            var allSales = await query.OrderBy(s => s.SaleDate).ToListAsync();

            string[] cashMethods = { "كاش", "نقدي", "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة", "Cash & K-Net" };

            var dailyRows = allSales
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new DailyRevenueRow
                {
                    Date = g.Key,
                    Total = g.Sum(s => s.NetAmount),
                    Cash = g.Sum(s => cashMethods.Contains(s.PaymentMethod) ? s.NetAmount
                        : mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0),
                    Knet = g.Sum(s => knetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                        : mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0),
                    EmployeeDebt = g.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount),
                    OwnerDebt = g.Where(s => s.PaymentMethod == "دين على صاحب المكان").Sum(s => s.NetAmount),
                    Count = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToList();

            var employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            decimal totalRevenue = allSales.Sum(s => s.NetAmount);
            decimal totalCash = allSales.Sum(s => cashMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : mixedMethods.Contains(s.PaymentMethod) ? (s.CashAmount ?? 0) : 0);
            decimal totalKnet = allSales.Sum(s => knetMethods.Contains(s.PaymentMethod) ? s.NetAmount
                : mixedMethods.Contains(s.PaymentMethod) ? (s.LinkAmount ?? 0) : 0);

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalCash = totalCash;
            ViewBag.TotalKnet = totalKnet;
            ViewBag.TotalEmployeeDebt = allSales.Where(s => s.PaymentMethod == "دين على الموظف").Sum(s => s.NetAmount);
            ViewBag.TotalOwnerDebt = allSales.Where(s => s.PaymentMethod == "دين على صاحب المكان").Sum(s => s.NetAmount);
            ViewBag.TotalCount = allSales.Count;
            ViewBag.ReportNumber = "RPT-" + dateFrom.ToString("yyyy-MM-dd");
            ViewBag.Employees = employees;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedSaleType = saleType;
            ViewBag.UserDept = userDept;

            if (dailyRows.Any())
            {
                var best = dailyRows.OrderByDescending(d => d.Total).First();
                var worst = dailyRows.OrderBy(d => d.Total).First();
                ViewBag.BestDayAmount = best.Total;
                ViewBag.BestDayDate = best.Date.ToString("yyyy/MM/dd");
                ViewBag.WorstDayAmount = worst.Total;
                ViewBag.WorstDayDate = worst.Date.ToString("yyyy/MM/dd");
                ViewBag.AvgDailyRevenue = dailyRows.Average(d => d.Total);
                ViewBag.AvgDailyCount = dailyRows.Average(d => (double)d.Count);
            }

            return View(dailyRows);
        }
    }
}