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
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public ExpensesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? from, string? to, string? department, string? paymentMethod)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from) ? DateTime.Today : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to) ? DateTime.Today : DateTime.Parse(to);
            var rangeEnd = dateTo.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Expenses
                .Include(e => e.Employee)
                .Where(e => e.ExpenseDate >= dateFrom && e.ExpenseDate < rangeEnd);

            // فلترة حسب قسم المستخدم
            if (userDept == "حلاقة")
                query = query.Where(e => e.Department == "حلاقة" || e.Department == null);
            else if (userDept == "مساج")
                query = query.Where(e => e.Department == "مساج" || e.Department == null);
            // الأدمن يرى الكل

            // فلترة حسب القسم المختار
            if (department == "حلاقة" || department == "مساج")
                query = query.Where(e => e.Department == department);

            // فلترة حسب طريقة الدفع
            if (!string.IsNullOrEmpty(paymentMethod))
                query = query.Where(e => e.PaymentMethod == paymentMethod);

            var expenses = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.ToString("yyyy-MM-dd");
            ViewBag.Total = expenses.Sum(e => e.Amount);
            ViewBag.TotalBarber = expenses.Where(e => e.Department == "حلاقة").Sum(e => e.Amount);
            ViewBag.TotalMassage = expenses.Where(e => e.Department == "مساج").Sum(e => e.Amount);
            ViewBag.UserDept = userDept;
            ViewBag.Department = department;
            ViewBag.PaymentMethod = paymentMethod;
            return View(expenses);
        }

        private async Task LoadEmployeesAsync(string? userDept)
        {
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = currentUser?.UserDepartment;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            await LoadEmployeesAsync(currentUser?.UserDepartment);
            return View(new Expense { ExpenseDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Expense model)
        {
            if (model.Category == "عهدة" && !model.EmployeeId.HasValue)
                ModelState.AddModelError(nameof(Expense.EmployeeId), "يجب اختيار الموظف عند اختيار فئة العهدة");

            if (model.Category != "عهدة")
                model.EmployeeId = null;

            if (ModelState.IsValid)
            {
                // إذا لم يُحدَّد القسم يدوياً، ضعه تلقائياً حسب قسم المستخدم
                if (string.IsNullOrEmpty(model.Department))
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var userDept = currentUser?.UserDepartment;
                    if (userDept == "حلاقة" || userDept == "مساج")
                        model.Department = userDept;
                }

                model.CreatedAt = DateTime.Now;
                _context.Expenses.Add(model);
                await _context.SaveChangesAsync();

                // مصروف فئة "عهدة" مرتبط بموظف يُنشئ سجل عهدة مرتبط تلقائياً، تماماً كما لو تم
                // تسليمها من شاشة "عهد الموظفين" مباشرة.
                if (model.Category == "عهدة" && model.EmployeeId.HasValue)
                {
                    var custody = new Custody
                    {
                        EmployeeId = model.EmployeeId.Value,
                        Amount = model.Amount,
                        CustodyDate = model.ExpenseDate,
                        PaymentMethod = model.PaymentMethod == "نقدي" ? "نقدي" : "لينك",
                        Reason = model.Description,
                        Notes = model.Notes,
                        ExpenseId = model.Id,
                        CreatedAt = DateTime.Now
                    };
                    _context.Custodies.Add(custody);
                    await _context.SaveChangesAsync();
                }

                await _audit.LogAsync("Add", "Expenses",
                    $"{model.Description} - {model.Amount:F3} KD" + (!string.IsNullOrEmpty(model.Department) ? $" ({model.Department})" : ""),
                    model.Id);
                TempData["Success"] = "Expense added created successfully";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = user?.UserDepartment;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            await LoadEmployeesAsync(user?.UserDepartment);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = currentUser?.UserDepartment;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(expense);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Expense model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Expenses",
                    $"{model.Description} - {model.Amount:F3} KD",
                    model.Id);
                TempData["Success"] = "Expense updated created successfully";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = user?.UserDepartment;
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                var desc = $"{expense.Description} - {expense.Amount:F3} KD";

                // بعض المصروفات (فئة "عهدة") تكون مولَّدة تلقائياً من عهدة موظف — احذف العهدة معها
                // حتى لا يبقى سجل عهدة يشير لمصروف محذوف. لا تحذفها لو ليها تسويات/إرجاعات مسجَّلة.
                var linkedCustody = await _context.Custodies.Include(c => c.Settlements).FirstOrDefaultAsync(c => c.ExpenseId == id);
                if (linkedCustody != null)
                {
                    if (linkedCustody.Settlements.Any())
                    {
                        TempData["Error"] = "لا يمكن حذف هذا المصروف — العهدة المرتبطة به لها تسويات/إرجاعات مسجَّلة. احذف التسويات أولاً من شاشة العهد";
                        return RedirectToAction(nameof(Index));
                    }
                    _context.Custodies.Remove(linkedCustody);
                }

                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Expenses", desc, id);
                TempData["Success"] = "Expense deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}