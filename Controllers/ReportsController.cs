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

        public async Task<IActionResult> Sales(string? from, string? to, int? employeeId, int? customerId, string? saleType)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? DateTime.Today.AddDays(-30) : DateTime.Parse(from);
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

            var allSalesRaw = await query.OrderByDescending(s => s.SaleDate).ToListAsync();
            var sales = allSalesRaw; // kept for view model
            var activeSales    = allSalesRaw.Where(s => s.Status != "ملغي").ToList();
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

            string[] cashMethodsSales  = { "كاش", "نقدي", "Cash" };
            string[] knetMethodsSales  = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
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
            ViewBag.TotalCancelled = cancelledSales.Sum(s => s.NetAmount);
            ViewBag.TotalCancelledCount = cancelledSales.Count;
            ViewBag.Employees = employees;
            ViewBag.Customers = customers;
            ViewBag.SelectedEmployeeId = employeeId;
            ViewBag.SelectedCustomerId = customerId;
            ViewBag.SelectedSaleType = saleType;
            ViewBag.UserDept = userDept;
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

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalExpenses = expenses.Sum(e => e.Amount);
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
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);

            if (userDept == "حلاقة")
                baseQuery = baseQuery.Where(s => s.SaleType != "مساج");
            else if (userDept == "مساج")
                baseQuery = baseQuery.Where(s => s.SaleType != "حلاقة");

            var allSales = await baseQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
            var activeSalesReport    = allSales.Where(s => s.Status != "ملغي").ToList();
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

            var expensesToday = await _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow)
                .SumAsync(e => (decimal?)e.Amount) ?? 0;

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow);

            if (userDept == "حلاقة" || userDept == "مساج")
                advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var advancesToday = await advancesQuery.SumAsync(a => (decimal?)a.Amount) ?? 0;

            var salesToday = activeSalesReport.Sum(s => s.NetAmount);

            string[] cashMethods = { "كاش", "نقدي" , "Cash" };
            string[] knetMethods = { "كي نت", "بطاقة", "تحويل بنكي", "K-Net" };
            string[] mixedMethods = { "كي نت و كاش", "مناصفة" ,"Cash & K-Net" };
            string[] debtMethods = { "دين على العميل", "دين على الموظف", "دين على صاحب المكان", "آجل" ,"Customer Debit","Employee Debit","Owner Debit"};

            ViewBag.SalesToday = salesToday;
            ViewBag.ExpensesToday = expensesToday;
            ViewBag.AdvancesToday = advancesToday;
            ViewBag.NetProfit = salesToday - expensesToday - advancesToday;
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
            ViewBag.CancelledTotal = cancelledSalesReport.Sum(s => s.NetAmount);
            ViewBag.CancelledCount = cancelledSalesReport.Count;

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
    }
}