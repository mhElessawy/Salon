using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class CustodyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustodyController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        private async Task<bool> IsManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager");
        }

        public async Task<IActionResult> Index(int? employeeId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.Custodies
                .Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(c => c.Employee!.DepartmentNav!.Name == userDept);

            if (!isManager && linkedEmpId.HasValue)
                query = query.Where(c => c.EmployeeId == linkedEmpId.Value);

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            var custodies = await query.OrderByDescending(c => c.CustodyDate).ThenByDescending(c => c.CreatedAt).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.EmployeeId = employeeId;
            ViewBag.IsManager = isManager;
            ViewBag.TotalCash = custodies.Where(c => c.PaymentMethod == "نقدي").Sum(c => c.Amount);
            ViewBag.TotalLink = custodies.Where(c => c.PaymentMethod == "لينك").Sum(c => c.Amount);
            ViewBag.Total = custodies.Sum(c => c.Amount);
            return View(custodies);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسليم عهدة";
                return RedirectToAction(nameof(Index));
            }

            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);

            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(new Custody { CustodyDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Custody model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسليم عهدة";
                return RedirectToAction(nameof(Index));
            }

            if (model.PaymentMethod != "نقدي" && model.PaymentMethod != "لينك")
                model.PaymentMethod = "نقدي";

            if (ModelState.IsValid)
            {
                var emp = await _context.Employees.Include(e => e.DepartmentNav).FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

                // العهدة لا تُنشئ مصروفاً ولا تخصم من الصندوق — هي مبلغ منفصل تحت عهدة الموظف
                // فقط، يظهر في شاشة العهد وملخص BarberDaily كرقم معلوماتي.
                model.CreatedAt = DateTime.Now;
                _context.Custodies.Add(model);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Add", "Custody",
                    $"تسليم عهدة للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بمبلغ {model.Amount:N3} KD | طريقة التسليم: {model.PaymentMethod}",
                    model.Id);

                TempData["Success"] = "تم تسليم العهدة بنجاح";
                return RedirectToAction(nameof(Index));
            }

            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف العهد";
                return RedirectToAction(nameof(Index));
            }

            var custody = await _context.Custodies.Include(c => c.Employee).FirstOrDefaultAsync(c => c.Id == id);
            if (custody != null)
            {
                string empName = custody.Employee?.FullName ?? custody.EmployeeId.ToString();
                decimal amount = custody.Amount;

                if (custody.ExpenseId.HasValue)
                {
                    var linkedExpense = await _context.Expenses.FindAsync(custody.ExpenseId.Value);
                    if (linkedExpense != null)
                        _context.Expenses.Remove(linkedExpense);
                }

                _context.Custodies.Remove(custody);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "Custody",
                    $"حذف عهدة الموظف: {empName} بمبلغ {amount:N3} KD",
                    id);

                TempData["Success"] = "تم حذف العهدة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Report(DateTime? dateFrom, DateTime? dateTo, int? employeeId, string? paymentMethod)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.Custodies
                .Include(c => c.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(c => c.Employee!.DepartmentNav!.Name == userDept);

            if (!isManager && linkedEmpId.HasValue)
            {
                query = query.Where(c => c.EmployeeId == linkedEmpId.Value);
                employeeId = linkedEmpId;
            }

            if (dateFrom.HasValue)
                query = query.Where(c => c.CustodyDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(c => c.CustodyDate <= dateTo.Value);

            if (employeeId.HasValue)
                query = query.Where(c => c.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(paymentMethod))
                query = query.Where(c => c.PaymentMethod == paymentMethod);

            var custodies = await query.OrderByDescending(c => c.CustodyDate).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            if (!isManager && linkedEmpId.HasValue)
                empQuery = empQuery.Where(e => e.Id == linkedEmpId.Value);

            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId = employeeId;
            ViewBag.PaymentMethod = paymentMethod;

            return View(custodies);
        }
    }
}
