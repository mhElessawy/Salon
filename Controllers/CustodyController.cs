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
        private readonly IPermissionService _perms;

        public CustodyController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager, IPermissionService perms)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _perms = perms;
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
                .Include(c => c.Settlements)
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
            ViewBag.CanApprove = await _perms.HasAccessAsync("CustodyApprove");
            // موظف (غير مدير) عنده صلاحية "إضافة" يقدر يطلب تسوية/إرجاع عهدته هو فقط — تسليم عهدة
            // جديدة يفضل حصراً للمدير/الأدمن
            ViewBag.CanRequestSettlement = isManager || (linkedEmpId.HasValue && await _perms.HasAccessAsync("CustodyAdd"));
            ViewBag.LinkedEmployeeId = linkedEmpId;
            ViewBag.TotalCash = custodies.Where(c => c.PaymentMethod == "نقدي").Sum(c => c.Amount);
            ViewBag.TotalLink = custodies.Where(c => c.PaymentMethod == "لينك").Sum(c => c.Amount);
            ViewBag.Total = custodies.Sum(c => c.Amount);
            ViewBag.TotalRemaining = custodies.Sum(c => c.RemainingAmount);
            ViewBag.TotalPending = custodies.Sum(c => c.PendingSettlementAmount);
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

                // كل عهدة تُسجَّل تلقائياً كمصروف (فئة "عهدة") بنفس طريقة الدفع، فتنعكس مباشرة
                // على الصندوق وكل التقارير المالية التي تعتمد على جدول المصروفات.
                var expense = new Expense
                {
                    Description = $"عهدة - {emp?.FullName ?? model.EmployeeId.ToString()}".Trim(' ', '-'),
                    Amount = model.Amount,
                    Category = "عهدة",
                    Department = emp?.DepartmentNav?.Name,
                    ExpenseDate = model.CustodyDate,
                    PaymentMethod = model.PaymentMethod,
                    Notes = model.Reason,
                    CreatedAt = DateTime.Now
                };
                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();

                model.ExpenseId = expense.Id;
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

            var custody = await _context.Custodies.Include(c => c.Employee).Include(c => c.Settlements).FirstOrDefaultAsync(c => c.Id == id);
            if (custody != null)
            {
                if (custody.Settlements.Any())
                {
                    TempData["Error"] = "لا يمكن حذف عهدة لها تسويات/إرجاعات مسجَّلة — احذف التسويات أولاً";
                    return RedirectToAction(nameof(Index));
                }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settle(int custodyId, decimal amount, DateTime settlementDate, string paymentMethod, string? notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);

            var custody = await _context.Custodies
                .Include(c => c.Employee)
                .Include(c => c.Settlements)
                .FirstOrDefaultAsync(c => c.Id == custodyId);
            if (custody == null)
                return RedirectToAction(nameof(Index));

            // موظف (غير مدير) يقدر يطلب تسوية/إرجاع عهدته هو فقط، وبس لو عنده صلاحية "إضافة" على
            // شاشة العهد — تسليم عهدة جديدة يفضل حصراً للمدير/الأدمن
            bool isSelfRequest = !isManager
                && currentUser?.LinkedEmployeeId.HasValue == true
                && custody.EmployeeId == currentUser.LinkedEmployeeId.Value
                && await _perms.HasAccessAsync("CustodyAdd");

            if (!isManager && !isSelfRequest)
            {
                TempData["Error"] = "غير مصرح لك بطلب تسوية عهدة";
                return RedirectToAction(nameof(Index));
            }

            if (paymentMethod != "نقدي" && paymentMethod != "لينك")
                paymentMethod = "نقدي";

            if (amount <= 0 || amount > custody.AvailableForSettlement)
            {
                TempData["Error"] = $"المبلغ يجب أن يكون بين 0 و{custody.AvailableForSettlement:N3} د.ك (المتاح للتسوية بعد استبعاد الطلبات المعلَّقة)";
                return RedirectToAction(nameof(Index));
            }

            // طلب فقط بانتظار الموافقة — لا يُنشأ الإيداع ولا يُخصم من رصيد العهدة إلا بعد الموافقة
            var settlement = new CustodySettlement
            {
                CustodyId = custody.Id,
                Amount = amount,
                SettlementDate = settlementDate,
                PaymentMethod = paymentMethod,
                Notes = notes,
                Status = "معلق",
                CreatedAt = DateTime.Now
            };
            _context.CustodySettlements.Add(settlement);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Request", "Custody",
                $"طلب تسوية عهدة الموظف: {custody.Employee?.FullName ?? custody.EmployeeId.ToString()} بمبلغ {amount:N3} KD | طريقة الإرجاع: {paymentMethod} | بانتظار الموافقة",
                custody.Id);

            TempData["Success"] = "تم إرسال طلب التسوية، في انتظار موافقة الأدمن";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSettlement(int id)
        {
            if (!await _perms.HasAccessAsync("CustodyApprove"))
            {
                TempData["Error"] = "غير مصرح لك بالموافقة على تسوية العهدة";
                return RedirectToAction(nameof(Index));
            }

            var settlement = await _context.CustodySettlements
                .Include(s => s.Custody).ThenInclude(c => c!.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(s => s.Custody).ThenInclude(c => c!.Settlements)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (settlement == null || settlement.Custody == null)
                return RedirectToAction(nameof(Index));

            if (settlement.Status != "معلق")
            {
                TempData["Error"] = "تمت الموافقة على هذه التسوية بالفعل";
                return RedirectToAction(nameof(Index));
            }

            var custody = settlement.Custody;

            // إعادة التحقق وقت الموافقة (وليس وقت الطلب فقط) لضمان عدم تجاوز مجموع التسويات
            // الموافَق عليها مبلغ العهدة الأصلي، في حال تمت الموافقة على طلبات أخرى في الأثناء
            if (settlement.Amount > custody.RemainingAmount)
            {
                TempData["Error"] = $"لا يمكن الموافقة — المبلغ ({settlement.Amount:N3}) أكبر من المتبقي الحالي ({custody.RemainingAmount:N3}) د.ك";
                return RedirectToAction(nameof(Index));
            }

            // الإرجاع النقدي بيرجع فعلياً للصندوق كإيداع — عكس التسليم اللي بيسجَّل كمصروف
            if (settlement.PaymentMethod == "نقدي")
            {
                var deposit = new Deposit
                {
                    Description = $"إرجاع عهدة - {custody.Employee?.FullName ?? custody.EmployeeId.ToString()}".Trim(' ', '-'),
                    Amount = settlement.Amount,
                    Source = "إرجاع عهدة",
                    Department = custody.Employee?.DepartmentNav?.Name ?? "",
                    PaymentMethod = "نقدي",
                    DepositDate = settlement.SettlementDate,
                    Notes = settlement.Notes,
                    CreatedAt = DateTime.Now
                };
                _context.Deposits.Add(deposit);
                await _context.SaveChangesAsync();
                settlement.DepositId = deposit.Id;
            }

            settlement.Status = "موافق عليها";
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Approve", "Custody",
                $"الموافقة على تسوية عهدة الموظف: {custody.Employee?.FullName ?? custody.EmployeeId.ToString()} بمبلغ {settlement.Amount:N3} KD | طريقة الإرجاع: {settlement.PaymentMethod}",
                custody.Id);

            TempData["Success"] = "تمت الموافقة على التسوية بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSettlement(int id, string reason)
        {
            if (!await _perms.HasAccessAsync("CustodyApprove"))
            {
                TempData["Error"] = "غير مصرح لك برفض تسوية العهدة";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "يجب كتابة سبب الرفض";
                return RedirectToAction(nameof(Index));
            }

            var settlement = await _context.CustodySettlements
                .Include(s => s.Custody).ThenInclude(c => c!.Employee)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (settlement == null || settlement.Custody == null)
                return RedirectToAction(nameof(Index));

            if (settlement.Status != "معلق")
            {
                TempData["Error"] = "لا يمكن رفض تسوية تمت الموافقة عليها بالفعل أو مرفوضة مسبقاً";
                return RedirectToAction(nameof(Index));
            }

            settlement.Status = "مرفوضة";
            settlement.RejectionReason = reason.Trim();
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Reject", "Custody",
                $"رفض تسوية عهدة الموظف: {settlement.Custody.Employee?.FullName ?? settlement.Custody.EmployeeId.ToString()} بمبلغ {settlement.Amount:N3} KD | السبب: {settlement.RejectionReason}",
                settlement.CustodyId);

            TempData["Success"] = "تم رفض طلب التسوية";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSettlement(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف تسوية العهدة";
                return RedirectToAction(nameof(Index));
            }

            var settlement = await _context.CustodySettlements.Include(s => s.Custody).ThenInclude(c => c!.Employee).FirstOrDefaultAsync(s => s.Id == id);
            if (settlement != null)
            {
                if (settlement.DepositId.HasValue)
                {
                    var linkedDeposit = await _context.Deposits.FindAsync(settlement.DepositId.Value);
                    if (linkedDeposit != null)
                        _context.Deposits.Remove(linkedDeposit);
                }

                string empName = settlement.Custody?.Employee?.FullName ?? settlement.Custody?.EmployeeId.ToString() ?? "-";
                decimal amount = settlement.Amount;

                _context.CustodySettlements.Remove(settlement);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "Custody",
                    $"حذف تسوية عهدة الموظف: {empName} بمبلغ {amount:N3} KD",
                    settlement.CustodyId);

                TempData["Success"] = "تم حذف التسوية بنجاح";
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
                .Include(c => c.Settlements)
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
