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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermissionService _permissionService;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> Index()
        {
            if (!await _permissionService.HasAccessAsync("Dashboard"))
                return View(new DashboardViewModel { HasAccess = false });

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var salesBase = _context.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);
            if (userDept == "حلاقة")
                salesBase = salesBase.Where(s => s.SaleType != "مساج");
            else if (userDept == "مساج")
                salesBase = salesBase.Where(s => s.SaleType != "حلاقة");

            var salesToday = await salesBase.SumAsync(s => (decimal?)s.NetAmount) ?? 0;

            var customersToday = await salesBase
                .Where(s => s.CustomerId != null)
                .Select(s => s.CustomerId)
                .Distinct()
                .CountAsync();

            var expensesQuery = _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);

            // فلترة المصاريف حسب قسم المستخدم
            if (userDept == "حلاقة")
                expensesQuery = expensesQuery.Where(e => e.Department == "حلاقة" || e.Department == null);
            else if (userDept == "مساج")
                expensesQuery = expensesQuery.Where(e => e.Department == "مساج" || e.Department == null);
            // الأدمن يرى كل المصاريف

            var expensesToday = await expensesQuery.SumAsync(e => (decimal?)e.Amount) ?? 0;

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow);

            if (userDept == "حلاقة" || userDept == "مساج")
                advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var advancesToday = await advancesQuery.SumAsync(a => (decimal?)a.Amount) ?? 0;

            var netProfit = salesToday - expensesToday - advancesToday;

            var newCustomersToday = await _context.Customers
                .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
                .CountAsync();

            // Birthdays in next 7 days
            var nowDayOfYear = today.DayOfYear;
            var upcomingBirthdays = await _context.Customers
                .Where(c => c.BirthDate != null && c.IsActive)
                .ToListAsync();

            var birthdayList = upcomingBirthdays
                .Where(c => {
                    if (c.BirthDate == null) return false;
                    var bDay = new DateTime(today.Year, c.BirthDate.Value.Month, c.BirthDate.Value.Day);
                    if (bDay < today) bDay = bDay.AddYears(1);
                    return (bDay - today).TotalDays <= 7;
                })
                .OrderBy(c => {
                    var bDay = new DateTime(today.Year, c.BirthDate!.Value.Month, c.BirthDate.Value.Day);
                    if (bDay < today) bDay = bDay.AddYears(1);
                    return bDay;
                })
                .Take(10)
                .ToList();

            // Products expiring in next 30 days
            var expiryDate = today.AddDays(30);
            var expiringProducts = await _context.Products
                .Where(p => p.ExpiryDate != null && p.ExpiryDate <= expiryDate && p.IsActive)
                .OrderBy(p => p.ExpiryDate)
                .Take(10)
                .ToListAsync();

            // فواتير اليوم التي بها ملاحظات للموظف المرتبط بالمستخدم
            var invoicesWithNotes = new List<Sale>();
            if ((userDept == "حلاقة" || userDept == "مساج") && currentUser?.LinkedEmployeeId.HasValue == true)
            {
                invoicesWithNotes = await _context.Sales
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow
                                && s.EmployeeId == currentUser.LinkedEmployeeId!.Value
                                && s.Notes != null && s.Notes != "")
                    .OrderBy(s => s.SaleDate)
                    .ToListAsync();
            }

            var vm = new DashboardViewModel
            {
                SalesToday = salesToday,
                CustomersToday = customersToday,
                ExpensesToday = expensesToday,
                AdvancesToday = advancesToday,
                NetProfitToday = netProfit,
                NewCustomersToday = newCustomersToday,
                UpcomingBirthdays = birthdayList,
                ExpiringProducts = expiringProducts,
                UserDepartment = userDept,
                InvoicesWithNotes = invoicesWithNotes
            };

            return View(vm);
        }
    }
}