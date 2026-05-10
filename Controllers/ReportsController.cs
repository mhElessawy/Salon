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

        public async Task<IActionResult> Sales(string? from, string? to)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? DateTime.Today.AddDays(-30) : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : DateTime.Parse(to).AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate < dateTo);

            if (userDept == "����")
                query = query.Where(s => s.SaleType != "�����");
            else if (userDept == "�����")
                query = query.Where(s => s.SaleType != "����");

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
            ViewBag.TotalSales = sales.Sum(s => s.NetAmount);
            ViewBag.TotalCount = sales.Count;
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

        public async Task<IActionResult> MyReport()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var salesQuery = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);

            if (userDept == "حلاقة")
                salesQuery = salesQuery.Where(s => s.SaleType != "مساج");
            else if (userDept == "مساج")
                salesQuery = salesQuery.Where(s => s.SaleType != "حلاقة");

            var sales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();

            var expensesToday = await _context.Expenses
                .Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow)
                .SumAsync(e => (decimal?)e.Amount) ?? 0;

            var salesToday = sales.Sum(s => s.NetAmount);
            var barberSales = sales.Where(s => s.SaleType == "حلاقة").Sum(s => s.NetAmount);
            var massageSales = sales.Where(s => s.SaleType == "مساج").Sum(s => s.NetAmount);
            var productSales = sales.Where(s => s.SaleType == "منتجات").Sum(s => s.NetAmount);

            var cashTotal = sales.Sum(s => s.CashAmount ?? (s.PaymentMethod == "نقدي" ? s.NetAmount : 0));
            var linkTotal = sales.Sum(s => s.LinkAmount ?? (s.PaymentMethod == "شبكة" ? s.NetAmount : 0));

            ViewBag.SalesToday = salesToday;
            ViewBag.ExpensesToday = expensesToday;
            ViewBag.NetProfit = salesToday - expensesToday;
            ViewBag.BarberSales = barberSales;
            ViewBag.MassageSales = massageSales;
            ViewBag.ProductSales = productSales;
            ViewBag.CashTotal = cashTotal;
            ViewBag.LinkTotal = linkTotal;
            ViewBag.Date = today.ToString("yyyy/MM/dd");
            ViewBag.UserDept = userDept;
            return View(sales);
        }
    }
}