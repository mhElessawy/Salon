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
        private readonly IDailyClosureService _closure;

        private static readonly TimeZoneInfo _kuwaitTz =
            TimeZoneInfo.CreateCustomTimeZone("Kuwait Standard Time",
                TimeSpan.FromHours(3), "Kuwait Standard Time", "Kuwait Standard Time");

        private static DateTime KuwaitToday =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _kuwaitTz).Date;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IPermissionService permissionService, IDailyClosureService closure)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
            _closure = closure;
        }

        public async Task<IActionResult> Index()
        {
            if (!await _permissionService.HasAccessAsync("Dashboard"))
                return View(new DashboardViewModel { HasAccess = false });

            var today = KuwaitToday;
            var tomorrow = today.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            // نطاق العرض: الأدمن/المدير يشوف كل حاجة، سكرتير القسم (حلاقة/مساج) يشوف قسمه فقط،
            // والموظف المرتبط بسجل موظف (LinkedEmployeeId) يشوف بياناته الشخصية فقط.
            bool isAdmin = User.IsInRole("Admin") || User.IsInRole("Manager");
            bool isPersonalScope = !isAdmin && User.IsInRole("Employee") && currentUser?.LinkedEmployeeId != null;
            string viewScope = isPersonalScope ? "Personal"
                : userDept == Shift.ClosureDepartments.Haircut ? "Haircut"
                : userDept == Shift.ClosureDepartments.Massage ? "Massage"
                : "All";

            var vm = new DashboardViewModel
            {
                ViewScope = viewScope,
                UserDepartment = userDept
            };

            var salesBase = _context.Sales.Where(s => s.SaleDate >= today && s.SaleDate < tomorrow);

            if (isPersonalScope)
            {
                var empId = currentUser!.LinkedEmployeeId!.Value;
                var mySales = salesBase.Where(s => s.EmployeeId == empId);
                var myActive = mySales.Where(s => s.Status != Sale.Statuses.Cancelled);

                vm.Sales.Total = (await myActive.Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.CancelledSales.Total = (await mySales.Where(s => s.Status == Sale.Statuses.Cancelled).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.CancelledSalesCount.Total = await mySales.CountAsync(s => s.Status == Sale.Statuses.Cancelled);
                vm.RefundedSales.Total = (await mySales.Where(s => s.Status == Sale.Statuses.Refunded || s.Status == Sale.Statuses.PartiallyRefunded).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.RefundedSalesCount.Total = await mySales.CountAsync(s => s.Status == Sale.Statuses.Refunded || s.Status == Sale.Statuses.PartiallyRefunded);
                vm.Customers.Total = await myActive.Where(s => s.CustomerId != null).Select(s => s.CustomerId).Distinct().CountAsync();

                var myExpenses = _context.Expenses.Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow && e.EmployeeId == empId);
                vm.Expenses.Total = (await myExpenses.Select(e => e.Amount).ToListAsync()).Sum();

                var myAdvances = _context.EmployeeAdvances.Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow
                    && a.EmployeeId == empId && EmployeeAdvance.Statuses.Realized.Contains(a.Status));
                vm.Advances.Total = (await myAdvances.Select(a => a.Amount).ToListAsync()).Sum();

                vm.NetProfit.Total = vm.Sales.Total - vm.Expenses.Total - vm.Advances.Total;
            }
            else
            {
                var active = salesBase.Where(s => s.Status != Sale.Statuses.Cancelled);
                var cancelled = salesBase.Where(s => s.Status == Sale.Statuses.Cancelled);
                var refunded = salesBase.Where(s => s.Status == Sale.Statuses.Refunded || s.Status == Sale.Statuses.PartiallyRefunded);

                vm.Sales.Haircut = (await active.Where(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.Sales.Massage = (await active.Where(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.Sales.Total = (await active.Select(s => s.NetAmount).ToListAsync()).Sum();

                vm.CancelledSales.Haircut = (await cancelled.Where(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.CancelledSales.Massage = (await cancelled.Where(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.CancelledSales.Total = (await cancelled.Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.CancelledSalesCount.Haircut = await cancelled.CountAsync(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut)));
                vm.CancelledSalesCount.Massage = await cancelled.CountAsync(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage)));
                vm.CancelledSalesCount.Total = await cancelled.CountAsync();

                vm.RefundedSales.Haircut = (await refunded.Where(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.RefundedSales.Massage = (await refunded.Where(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage))).Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.RefundedSales.Total = (await refunded.Select(s => s.NetAmount).ToListAsync()).Sum();
                vm.RefundedSalesCount.Haircut = await refunded.CountAsync(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut)));
                vm.RefundedSalesCount.Massage = await refunded.CountAsync(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage)));
                vm.RefundedSalesCount.Total = await refunded.CountAsync();

                vm.Customers.Haircut = await active.Where(s => (s.SaleType == Shift.ClosureDepartments.Haircut || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Haircut)) && s.CustomerId != null).Select(s => s.CustomerId).Distinct().CountAsync();
                vm.Customers.Massage = await active.Where(s => (s.SaleType == Shift.ClosureDepartments.Massage || (s.SaleType == "منتجات" && s.Department == Shift.ClosureDepartments.Massage)) && s.CustomerId != null).Select(s => s.CustomerId).Distinct().CountAsync();
                vm.Customers.Total = await active.Where(s => s.CustomerId != null).Select(s => s.CustomerId).Distinct().CountAsync();

                var expensesBase = _context.Expenses.Where(e => e.ExpenseDate >= today && e.ExpenseDate < tomorrow);
                vm.Expenses.Haircut = (await expensesBase.Where(e => e.Department == Shift.ClosureDepartments.Haircut || e.Department == null).Select(e => e.Amount).ToListAsync()).Sum();
                vm.Expenses.Massage = (await expensesBase.Where(e => e.Department == Shift.ClosureDepartments.Massage || e.Department == null).Select(e => e.Amount).ToListAsync()).Sum();
                vm.Expenses.Total = (await expensesBase.Select(e => e.Amount).ToListAsync()).Sum();

                var advancesBase = _context.EmployeeAdvances
                    .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                    .Where(a => a.AdvanceDate >= today && a.AdvanceDate < tomorrow && EmployeeAdvance.Statuses.Realized.Contains(a.Status));
                vm.Advances.Haircut = (await advancesBase.Where(a => a.Employee!.DepartmentNav!.Name == Shift.ClosureDepartments.Haircut).Select(a => a.Amount).ToListAsync()).Sum();
                vm.Advances.Massage = (await advancesBase.Where(a => a.Employee!.DepartmentNav!.Name == Shift.ClosureDepartments.Massage).Select(a => a.Amount).ToListAsync()).Sum();
                vm.Advances.Total = (await advancesBase.Select(a => a.Amount).ToListAsync()).Sum();

                vm.NetProfit.Haircut = vm.Sales.Haircut - vm.Expenses.Haircut - vm.Advances.Haircut;
                vm.NetProfit.Massage = vm.Sales.Massage - vm.Expenses.Massage - vm.Advances.Massage;
                vm.NetProfit.Total = vm.Sales.Total - vm.Expenses.Total - vm.Advances.Total;

                // حضور الموظفين اليوم — لأقسام الحلاقة والمساج فقط
                vm.Attendance.HaircutTotal = await _context.Employees.CountAsync(e => e.IsActive && e.DepartmentNav!.Name == Shift.ClosureDepartments.Haircut);
                vm.Attendance.MassageTotal = await _context.Employees.CountAsync(e => e.IsActive && e.DepartmentNav!.Name == Shift.ClosureDepartments.Massage);
                vm.Attendance.HaircutPresent = await _context.Attendances.CountAsync(a => a.AttendanceDate == today && a.Employee!.IsActive && a.Employee.DepartmentNav!.Name == Shift.ClosureDepartments.Haircut);
                vm.Attendance.MassagePresent = await _context.Attendances.CountAsync(a => a.AttendanceDate == today && a.Employee!.IsActive && a.Employee.DepartmentNav!.Name == Shift.ClosureDepartments.Massage);
                vm.Attendance.AllTotal = vm.Attendance.HaircutTotal + vm.Attendance.MassageTotal;
                vm.Attendance.AllPresent = vm.Attendance.HaircutPresent + vm.Attendance.MassagePresent;

                // الأيام السابقة اللي فيها مبيعات ومالهاش سجل اعتماد معتمد — تظهر للكاشير والإدارة
                // كتنبيه فوري، لكل قسم لوحده (سكرتير قسم يشوف قسمه بس).
                List<string> departmentsToCheck = viewScope == "Haircut" ? new List<string> { Shift.ClosureDepartments.Haircut }
                    : viewScope == "Massage" ? new List<string> { Shift.ClosureDepartments.Massage }
                    : isAdmin || User.IsInRole("Cashier") ? Shift.ClosureDepartments.All.ToList()
                    : new List<string>();

                if (departmentsToCheck.Count > 0)
                    vm.UnapprovedClosures = await _closure.FindAllPendingApprovalsAsync(departmentsToCheck);

                vm.NewCustomersToday = await _context.Customers.CountAsync(c => c.CreatedAt >= today && c.CreatedAt < tomorrow);

                // Birthdays in next 7 days
                var upcomingBirthdays = await _context.Customers.Where(c => c.BirthDate != null && c.IsActive).ToListAsync();
                vm.UpcomingBirthdays = upcomingBirthdays
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
                vm.ExpiringProducts = await _context.Products
                    .Where(p => p.ExpiryDate != null && p.ExpiryDate <= expiryDate && p.IsActive)
                    .OrderBy(p => p.ExpiryDate)
                    .Take(10)
                    .ToListAsync();

                // إقامات الموظفين التي تنتهي قريباً — لسكرتير القسم تُعرض موظفي قسمه فقط
                var residencyQuery = _context.Employees.Where(e => e.IsActive && e.ResidencyExpiry != null && e.ResidencyExpiry <= expiryDate);
                if (viewScope == "Haircut")
                    residencyQuery = residencyQuery.Where(e => e.DepartmentNav!.Name == Shift.ClosureDepartments.Haircut);
                else if (viewScope == "Massage")
                    residencyQuery = residencyQuery.Where(e => e.DepartmentNav!.Name == Shift.ClosureDepartments.Massage);
                vm.ExpiringResidencies = await residencyQuery.OrderBy(e => e.ResidencyExpiry).Take(10).ToListAsync();
            }

            // فواتير اليوم التي بها ملاحظات للموظف المرتبط بالمستخدم
            if (currentUser?.LinkedEmployeeId.HasValue == true)
            {
                vm.InvoicesWithNotes = await _context.Sales
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow
                                && s.EmployeeId == currentUser.LinkedEmployeeId!.Value
                                && s.Status != Sale.Statuses.Cancelled
                                && s.Notes != null && s.Notes != "")
                    .OrderBy(s => s.SaleDate)
                    .ToListAsync();
            }

            return View(vm);
        }
    }
}