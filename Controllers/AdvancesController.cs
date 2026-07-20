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
    public class AdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _email;
        private readonly IDailyClosureService _closure;

        public AdvancesController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager, IEmailService email, IDailyClosureService closure)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _email = email;
            _closure = closure;
        }

        private async Task<bool> IsManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager");
        }

        private async Task<bool> IsCashierOrManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager") || roles.Contains("Cashier");
        }

        public async Task<IActionResult> Index(string? search)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var roles = await _userManager.GetRolesAsync(currentUser!);
            bool isManager = roles.Contains("Admin") || roles.Contains("Manager");
            bool isCashier = isManager || roles.Contains("Cashier");

            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var advancesQuery = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                advancesQuery = advancesQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            if (!isCashier && linkedEmpId.HasValue)
                advancesQuery = advancesQuery.Where(a => a.EmployeeId == linkedEmpId.Value);

            if (!string.IsNullOrEmpty(search))
                advancesQuery = advancesQuery.Where(a => a.Employee != null && a.Employee.FullName.Contains(search));

            var advances = await advancesQuery.OrderByDescending(a => a.AdvanceDate).ToListAsync();
            ViewBag.IsManager = isManager;
            ViewBag.IsCashier = isCashier;
            ViewBag.MyEmployeeId = linkedEmpId;

            // subquery to avoid EF Core generating CTE (WITH) syntax error on SQL Server
            var employeeIdsSubquery = advancesQuery.Select(a => a.EmployeeId).Distinct();

            var salaryDeductions = await _context.Salaries
                .Where(s => employeeIdsSubquery.Contains(s.EmployeeId) && s.AdvanceDeducted > 0)
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .ToListAsync();

            var summaries = advances
                .GroupBy(a => a.Employee)
                .Select(g => new EmployeeAdvanceSummaryViewModel
                {
                    Employee = g.Key!,
                    Advances = g.OrderByDescending(a => a.AdvanceDate).ToList(),
                    SalaryDeductions = salaryDeductions
                        .Where(s => s.EmployeeId == g.Key!.Id)
                        .Select(s => new SalaryAdvanceDeduction
                        {
                            Month = s.Month,
                            Year = s.Year,
                            Amount = s.AdvanceDeducted,
                            PaidDate = s.PaidDate
                        }).ToList()
                })
                .OrderBy(s => s.Employee.FullName)
                .ToList();

            ViewBag.Search = search;
            ViewBag.PendingCount = advances.Count(a => a.Status == EmployeeAdvance.Statuses.PendingApproval);
            ViewBag.PendingTotal = advances.Where(a => a.Status == EmployeeAdvance.Statuses.PendingApproval).Sum(a => a.Amount);
            ViewBag.CashierQueueCount = advances.Count(a => a.Status == EmployeeAdvance.Statuses.AwaitingCashierPayout);
            ViewBag.CashierQueueTotal = advances.Where(a => a.Status == EmployeeAdvance.Statuses.AwaitingCashierPayout).Sum(a => a.Amount);
            ViewBag.BankQueueCount = advances.Count(a => a.Status == EmployeeAdvance.Statuses.AwaitingBankTransfer);
            ViewBag.BankQueueTotal = advances.Where(a => a.Status == EmployeeAdvance.Statuses.AwaitingBankTransfer).Sum(a => a.Amount);
            ViewBag.ApprovedCount = advances.Count(a => EmployeeAdvance.Statuses.Realized.Contains(a.Status));
            ViewBag.ApprovedTotal = advances.Where(a => EmployeeAdvance.Statuses.Realized.Contains(a.Status)).Sum(a => a.Amount);
            return View(summaries);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);

            // If linked to an employee, pre-select them
            int? linkedEmpId = currentUser?.LinkedEmployeeId;
            if (linkedEmpId.HasValue && !isManager)
                empQuery = empQuery.Where(e => e.Id == linkedEmpId.Value);

            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName", linkedEmpId);
            ViewBag.IsManager = isManager;
            return View(new EmployeeAdvance { AdvanceDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeAdvance model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);

            // مسار واحد فقط لتقديم طلب السلفة: يبدأ دائماً بانتظار موافقة المدير، بغض النظر عمّن
            // يرسل الطلب. طريقة الصرف الفعلية تُحدَّد لاحقاً عند الموافقة.
            model.Status = EmployeeAdvance.Statuses.PendingApproval;
            model.PaymentMethod = "نقدي";

            int? linkedEmpId = currentUser?.LinkedEmployeeId;
            if (!isManager && linkedEmpId.HasValue && model.EmployeeId != linkedEmpId.Value)
            {
                TempData["Error"] = "لا يمكنك طلب سلفة لموظف آخر";
                return RedirectToAction(nameof(Create));
            }

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.EmployeeAdvances.Add(model);
                await _context.SaveChangesAsync();

                var emp = await _context.Employees.Include(e => e.DepartmentNav).FirstOrDefaultAsync(e => e.Id == model.EmployeeId);
                await _audit.LogAsync("Add", "Advances",
                    $"طلب سلفة للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بمبلغ {model.Amount:N3} KD",
                    model.Id);

                _ = _email.SendAdvanceRequestAsync(emp?.FullName ?? "-", emp?.DepartmentNav?.Name ?? "-", model.Amount, model.Reason, model.AdvanceDate);

                TempData["Success"] = "تم إرسال طلب السلفة بنجاح، في انتظار موافقة المدير";
                return RedirectToAction(nameof(Index));
            }

            var ud2 = currentUser?.UserDepartment;
            var eq2 = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (ud2 == "حلاقة" || ud2 == "مساج") eq2 = eq2.Where(e => e.DepartmentNav!.Name == ud2);
            ViewBag.Employees = new SelectList(await eq2.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            ViewBag.IsManager = isManager;
            return View(model);
        }

        public async Task<IActionResult> Report(DateTime? dateFrom, DateTime? dateTo, int? employeeId, string? status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            bool reportIsManager = await IsManagerAsync(currentUser);
            bool reportIsCashier = await IsCashierOrManagerAsync(currentUser);
            int? reportLinkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            if (!reportIsCashier && reportLinkedEmpId.HasValue)
            {
                query = query.Where(a => a.EmployeeId == reportLinkedEmpId.Value);
                employeeId = reportLinkedEmpId;
            }

            if (dateFrom.HasValue)
                query = query.Where(a => a.AdvanceDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(a => a.AdvanceDate <= dateTo.Value);

            if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            // التقرير لا يعرض إلا السلف التي صُرفت/حُوّلت فعلياً (وليست بانتظار موافقة أو صرف)
            query = query.Where(a => EmployeeAdvance.Statuses.Realized.Contains(a.Status));

            var advances = await query.OrderByDescending(a => a.AdvanceDate).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            if (!reportIsCashier && reportLinkedEmpId.HasValue)
                empQuery = empQuery.Where(e => e.Id == reportLinkedEmpId.Value);

            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId = employeeId;
            ViewBag.Status = status;

            return View(advances);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReconcileHistoricalDeductions()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);
            if (!isManager)
            {
                TempData["Error"] = "غير مصرح لك بهذا الإجراء";
                return RedirectToAction(nameof(Index));
            }

            var employeeIds = await _context.EmployeeAdvances.Select(a => a.EmployeeId).Distinct().ToListAsync();
            int fixedCount = 0;

            foreach (var employeeId in employeeIds)
            {
                decimal totalPaidFromSalary = await _context.Salaries
                    .Where(s => s.EmployeeId == employeeId && s.PaidDate != null)
                    .SumAsync(s => s.AdvanceDeducted);

                decimal totalReflectedOnAdvances = await _context.EmployeeAdvances
                    .Where(a => a.EmployeeId == employeeId)
                    .SumAsync(a => a.DeductedAmount);

                decimal missing = totalPaidFromSalary - totalReflectedOnAdvances;
                if (missing > 0)
                {
                    await AdvanceReconciliationHelper.ReconcileAsync(_context, employeeId, missing);
                    fixedCount++;
                }
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync("Reconcile", "Advances",
                $"إصلاح بيانات السلف القديمة غير المرتبطة برواتب مصروفة - عدد الموظفين المعدَّلين: {fixedCount}",
                null);

            TempData["Success"] = fixedCount > 0
                ? $"تم إصلاح بيانات السلف لـ {fixedCount} موظف"
                : "لا توجد بيانات سلف تحتاج إصلاح";
            return RedirectToAction(nameof(Index));
        }

        // موافقة المدير على الطلب — يجب اختيار طريقة الصرف: نقدي (يذهب لطابور الكاشير) أو
        // تحويل بنكي (يذهب لطابور التحويل). لا يُسجَّل أي أثر مالي في هذه الخطوة.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string paymentMethod = "نقدي")
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بالموافقة على طلبات السلف";
                return RedirectToAction(nameof(Index));
            }

            if (paymentMethod != "نقدي" && paymentMethod != "تحويل بنكي")
            {
                TempData["Error"] = "طريقة الصرف غير صحيحة";
                return RedirectToAction(nameof(Index));
            }

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return RedirectToAction(nameof(Index));

            if (advance.Status != EmployeeAdvance.Statuses.PendingApproval)
            {
                TempData["Error"] = "تمت معالجة هذا الطلب بالفعل";
                return RedirectToAction(nameof(Index));
            }

            advance.PaymentMethod = paymentMethod;
            advance.Status = paymentMethod == "نقدي"
                ? EmployeeAdvance.Statuses.AwaitingCashierPayout
                : EmployeeAdvance.Statuses.AwaitingBankTransfer;
            advance.ManagerId = currentUser!.Id;
            advance.ManagerName = currentUser.FullName;
            advance.DecisionAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Approve", "Advances",
                $"الموافقة على سلفة الموظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} KD | طريقة الصرف: {paymentMethod}",
                advance.Id);

            TempData["Success"] = paymentMethod == "نقدي"
                ? "تمت الموافقة على السلفة، بانتظار صرف الكاشير"
                : "تمت الموافقة على السلفة، بانتظار تنفيذ التحويل البنكي";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك برفض طلبات السلف";
                return RedirectToAction(nameof(Index));
            }

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return RedirectToAction(nameof(Index));

            if (advance.Status != EmployeeAdvance.Statuses.PendingApproval)
            {
                TempData["Error"] = "تمت معالجة هذا الطلب بالفعل";
                return RedirectToAction(nameof(Index));
            }

            advance.Status = EmployeeAdvance.Statuses.Rejected;
            advance.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            advance.ManagerId = currentUser!.Id;
            advance.ManagerName = currentUser.FullName;
            advance.DecisionAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Reject", "Advances",
                $"رفض طلب سلفة الموظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} KD" +
                (string.IsNullOrEmpty(advance.RejectionReason) ? "" : $" | السبب: {advance.RejectionReason}"),
                advance.Id);

            TempData["Success"] = "تم رفض طلب السلفة";
            return RedirectToAction(nameof(Index));
        }

        // زر "تم تسليم المبلغ" الذي يضغطه الكاشير — هنا فقط تُسجَّل السلفة فعلياً على الموظف
        // ويُخصَم المبلغ من الصندوق (عبر دخول الحالة ضمن EmployeeAdvance.Statuses.Realized
        // التي تعتمد عليها كل حسابات الصندوق والتقارير).
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Disburse(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بصرف السلف";
                return RedirectToAction(nameof(Index));
            }

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return RedirectToAction(nameof(Index));

            if (advance.Status != EmployeeAdvance.Statuses.AwaitingCashierPayout)
            {
                TempData["Error"] = "هذه السلفة ليست بانتظار صرف الكاشير";
                return RedirectToAction(nameof(Index));
            }

            advance.Status = EmployeeAdvance.Statuses.Disbursed;
            advance.CashierId = currentUser!.Id;
            advance.CashierName = currentUser.FullName;
            advance.DisbursedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Disburse", "Advances",
                $"صرف سلفة موظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} KD نقداً من الصندوق",
                advance.Id);

            TempData["Success"] = "تم تسليم المبلغ وتسجيل السلفة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        // زر "تم التحويل" الذي يضغطه المدير أو المستخدم المخوَّل بعد تنفيذ التحويل البنكي فعلياً.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTransfer(int id, string transferReference, string? transferBank, string? transferNotes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتنفيذ التحويلات البنكية";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(transferReference))
            {
                TempData["Error"] = "رقم مرجع التحويل مطلوب";
                return RedirectToAction(nameof(Index));
            }

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return RedirectToAction(nameof(Index));

            if (advance.Status != EmployeeAdvance.Statuses.AwaitingBankTransfer)
            {
                TempData["Error"] = "هذه السلفة ليست بانتظار التحويل البنكي";
                return RedirectToAction(nameof(Index));
            }

            advance.Status = EmployeeAdvance.Statuses.Transferred;
            advance.TransferReference = transferReference.Trim();
            advance.TransferBank = string.IsNullOrWhiteSpace(transferBank) ? null : transferBank.Trim();
            advance.TransferNotes = string.IsNullOrWhiteSpace(transferNotes) ? null : transferNotes.Trim();
            advance.TransferredById = currentUser!.Id;
            advance.TransferredByName = currentUser.FullName;
            advance.DisbursedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Transfer", "Advances",
                $"تحويل سلفة موظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} KD | مرجع التحويل: {advance.TransferReference}",
                advance.Id);

            TempData["Success"] = "تم تسجيل التحويل البنكي وتسجيل السلفة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return RedirectToAction(nameof(Index));

            bool isOwner = linkedEmpId.HasValue && advance.EmployeeId == linkedEmpId.Value;
            if (!isManager && !isOwner)
            {
                TempData["Error"] = "غير مصرح لك بإلغاء هذا الطلب";
                return RedirectToAction(nameof(Index));
            }

            if (advance.Status != EmployeeAdvance.Statuses.PendingApproval)
            {
                TempData["Error"] = "لا يمكن إلغاء طلب تمت معالجته بالفعل";
                return RedirectToAction(nameof(Index));
            }

            advance.Status = EmployeeAdvance.Statuses.Cancelled;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Cancel", "Advances",
                $"إلغاء طلب سلفة الموظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} KD",
                advance.Id);

            TempData["Success"] = "تم إلغاء طلب السلفة";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف السلف";
                return RedirectToAction(nameof(Index));
            }

            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance != null)
            {
                if (await _closure.IsDateLockedAsync(advance.AdvanceDate))
                {
                    TempData["Error"] = "لا يمكن حذف سلفة تخص يومية معتمدة — استخدم صلاحية إعادة فتح اليومية";
                    return RedirectToAction(nameof(Index));
                }

                string empName = advance.Employee?.FullName ?? advance.EmployeeId.ToString();
                decimal amount = advance.Amount;

                _context.EmployeeAdvances.Remove(advance);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "Advances",
                    $"حذف سلفة الموظف: {empName} بمبلغ {amount:N3} KD",
                    id);

                TempData["Success"] = "تم حذف السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}