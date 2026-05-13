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
                query = query.Where(s => s.SaleType != "حلاقة");
            else if (userDept == "حلاقة")
                query = query.Where(s => s.SaleType != "مساج");

            if (employeeId.HasValue)
                query = query.Where(s => s.EmployeeId == employeeId);

            if (customerId.HasValue)
                query = query.Where(s => s.CustomerId == customerId);

            if (!string.IsNullOrEmpty(saleType))
                query = query.Where(s => s.SaleType == saleType);

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();

            var employees = await _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalSales = sales.Sum(s => s.NetAmount);
            ViewBag.TotalCount = sales.Count;
            ViewBag.TotalHaircut = sales.Where(s => s.SaleType == "حلاقة").Sum(s => s.NetAmount);
            ViewBag.TotalMassage = sales.Where(s => s.SaleType == "مساج").Sum(s => s.NetAmount);
            ViewBag.TotalProducts = sales.Where(s => s.SaleType == "منتجات").Sum(s => s.NetAmount);
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

        public async Task<IActionResult> MyReport(string? saleType, string? paymentMethod, int? employeeId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

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

            var salesToday = allSales.Sum(s => s.NetAmount);

            ViewBag.SalesToday = salesToday;
            ViewBag.ExpensesToday = expensesToday;
            ViewBag.NetProfit = salesToday - expensesToday;
            ViewBag.BarberSales = allSales.Where(s => s.SaleType == "حلاقة").Sum(s => s.NetAmount);
            ViewBag.MassageSales = allSales.Where(s => s.SaleType == "مساج").Sum(s => s.NetAmount);
            ViewBag.CashTotal = allSales.Sum(s =>
                s.PaymentMethod == "كاش" ? s.NetAmount :
                s.PaymentMethod == "كي نت و كاش" ? (s.CashAmount ?? 0) : 0);
            ViewBag.KnetTotal = allSales.Sum(s =>
                s.PaymentMethod == "كي نت" ? s.NetAmount :
                s.PaymentMethod == "كي نت و كاش" ? (s.LinkAmount ?? 0) : 0);
            ViewBag.DebtTotal = allSales
                .Where(s => s.PaymentMethod == "دين على العميل" || s.PaymentMethod == "دين على الموظف" || s.PaymentMethod == "دين على صاحب المكان")
                .Sum(s => s.NetAmount);
            ViewBag.Date = today.ToString("yyyy/MM/dd");
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

            // Department filter: restricted users override the dept param
            string? effectiveDept = userDept == "حلاقة" || userDept == "مساج" ? userDept : dept;

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (!string.IsNullOrEmpty(effectiveDept))
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == effectiveDept);

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
                Employee             = emp,
                PresentDays          = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "حاضر"),
                AbsentDays           = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "غائب"),
                LeaveDays            = allAttendances.Count(a => a.EmployeeId == emp.Id && a.Status == "إجازة"),
                TotalAttendanceRecords = allAttendances.Count(a => a.EmployeeId == emp.Id),
                PeriodDays           = periodDays,
                TotalSales           = allSales.Where(s => s.EmployeeId == emp.Id).Sum(s => s.NetAmount),
                TotalTransactions    = allSales.Count(s => s.EmployeeId == emp.Id),
            }).ToList();

            var vm = new EmployeeEvaluationListViewModel
            {
                Rows       = rows,
                DateFrom   = dateFrom,
                DateTo     = dateTo.AddDays(-1),
                Department = effectiveDept,
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