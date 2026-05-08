using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var salesBase = _context.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);
            if (userDept == "ãÓÇÌ")
                salesBase = salesBase.Where(s => s.SaleType != "ÍáÇÞÉ");
            else if (userDept == "ÍáÇÞÉ")
                salesBase = salesBase.Where(s => s.SaleType != "ãÓÇÌ");

            var salesToday = await salesBase.SumAsync(s => (decimal?)s.NetAmount) ?? 0;

            var customersToday = await salesBase
                .Where(s => s.CustomerId != null)
                .Select(s => s.CustomerId)
                .Distinct()
                .CountAsync();

            var expensesToday = await _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow)
                .SumAsync(e => (decimal?)e.Amount) ?? 0;

            var netProfit = salesToday - expensesToday;

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

            var vm = new DashboardViewModel
            {
                SalesToday = salesToday,
                CustomersToday = customersToday,
                ExpensesToday = expensesToday,
                NetProfitToday = netProfit,
                NewCustomersToday = newCustomersToday,
                UpcomingBirthdays = birthdayList,
                ExpiringProducts = expiringProducts
            };

            return View(vm);
        }
    }
}