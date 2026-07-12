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
    public class PurchaseRequestsController : Controller
    {
        private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };
        private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermissionService _perms;
        private readonly IEmailService _email;
        private readonly IWebHostEnvironment _env;

        public PurchaseRequestsController(ApplicationDbContext context, IAuditService audit,
            UserManager<ApplicationUser> userManager, IPermissionService perms,
            IEmailService email, IWebHostEnvironment env)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _perms = perms;
            _email = email;
            _env = env;
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

        public async Task<IActionResult> Index(int? employeeId, string? status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            bool isManager = await IsManagerAsync(currentUser);
            bool isCashier = await IsCashierOrManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            var query = _context.PurchaseRequests
                .Include(p => p.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(p => p.Supplier)
                .Include(p => p.Items)
                .Include(p => p.Custody)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(p => p.Employee!.DepartmentNav!.Name == userDept);

            if (!isCashier && linkedEmpId.HasValue)
                query = query.Where(p => p.EmployeeId == linkedEmpId.Value);

            if (employeeId.HasValue)
                query = query.Where(p => p.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            var requests = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = (await empQuery.OrderBy(e => e.FullName).ToListAsync())
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToList();

            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
            ViewBag.EmployeeId = employeeId;
            ViewBag.Status = status;
            ViewBag.IsManager = isManager;
            ViewBag.IsCashier = isCashier;
            ViewBag.CanApprove = await _perms.HasAccessAsync("PurchaseRequestApprove");
            ViewBag.PendingCount = requests.Count(r => r.Status == PurchaseRequest.Statuses.Pending);
            ViewBag.ApprovedCount = requests.Count(r => r.Status == PurchaseRequest.Statuses.Approved);
            ViewBag.CompletedTotal = requests.Where(r => r.Status == PurchaseRequest.Statuses.Completed).Sum(r => r.ActualAmount ?? 0);
            return View(requests);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);
            var userDept = currentUser?.UserDepartment;

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);

            int? linkedEmpId = currentUser?.LinkedEmployeeId;
            if (linkedEmpId.HasValue && !isManager)
                empQuery = empQuery.Where(e => e.Id == linkedEmpId.Value);

            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName", linkedEmpId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
            ViewBag.Custodies = await GetCustodyOptionsAsync(userDept, isManager, linkedEmpId);
            ViewBag.IsManager = isManager;
            return View(new PurchaseRequest { RequestDate = DateTime.Today });
        }

        private async Task<List<Custody>> GetCustodyOptionsAsync(string? userDept, bool isManager, int? linkedEmpId)
        {
            var custodyQuery = _context.Custodies
                .Include(c => c.Employee)
                .Include(c => c.PurchaseRequests)
                .AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                custodyQuery = custodyQuery.Where(c => c.Employee!.DepartmentNav!.Name == userDept);

            if (!isManager && linkedEmpId.HasValue)
                custodyQuery = custodyQuery.Where(c => c.EmployeeId == linkedEmpId.Value);

            return await custodyQuery.OrderByDescending(c => c.CustodyDate).ToListAsync();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseRequest model, List<string>? itemName, List<int>? itemQty)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool isManager = await IsManagerAsync(currentUser);
            int? linkedEmpId = currentUser?.LinkedEmployeeId;

            if (!isManager && linkedEmpId.HasValue && model.EmployeeId != linkedEmpId.Value)
            {
                TempData["Error"] = "لا يمكنك إنشاء طلب شراء لموظف آخر";
                return RedirectToAction(nameof(Create));
            }

            var items = new List<PurchaseRequestItem>();
            if (itemName != null)
            {
                for (int i = 0; i < itemName.Count; i++)
                {
                    var name = itemName[i]?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int qty = (itemQty != null && i < itemQty.Count && itemQty[i] > 0) ? itemQty[i] : 1;
                    items.Add(new PurchaseRequestItem { ProductName = name!, Quantity = qty });
                }
            }
            if (items.Count == 0)
                ModelState.AddModelError("", "يجب إضافة منتج واحد على الأقل");

            ModelState.Remove(nameof(PurchaseRequest.Items));

            var custody = await _context.Custodies.Include(c => c.PurchaseRequests).FirstOrDefaultAsync(c => c.Id == model.CustodyId);
            if (custody == null)
                ModelState.AddModelError("", "العهدة المختارة غير موجودة");
            else if (custody.EmployeeId != model.EmployeeId)
                ModelState.AddModelError("", "العهدة المختارة لا تتبع هذا الموظف");
            else if (model.EstimatedAmount > custody.AvailableForRequest)
                ModelState.AddModelError("", $"القيمة التقديرية أكبر من المتاح في العهدة ({custody.AvailableForRequest:N3} د.ك)");

            if (ModelState.IsValid)
            {
                var emp = await _context.Employees.Include(e => e.DepartmentNav).FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

                model.Items = items;
                model.Status = PurchaseRequest.Statuses.Pending;
                model.CreatedAt = DateTime.Now;
                _context.PurchaseRequests.Add(model);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Add", "PurchaseRequest",
                    $"طلب شراء من الموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بقيمة تقديرية {model.EstimatedAmount:N3} KD | المنتجات: {model.ItemsSummary}",
                    model.Id);

                _ = _email.SendPurchaseRequestAsync(emp?.FullName ?? "-", emp?.DepartmentNav?.Name ?? "-", model.EstimatedAmount, model.Reason, model.RequestDate);

                TempData["Success"] = "تم إرسال طلب الشراء بنجاح، في انتظار موافقة المدير";
                return RedirectToAction(nameof(Index));
            }

            var userDept = currentUser?.UserDepartment;
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
            ViewBag.Custodies = await GetCustodyOptionsAsync(userDept, isManager, linkedEmpId);
            ViewBag.IsManager = isManager;
            model.Items = items;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (!await _perms.HasAccessAsync("PurchaseRequestApprove"))
            {
                TempData["Error"] = "غير مصرح لك بالموافقة على طلبات الشراء";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.PurchaseRequests.Include(p => p.Employee).FirstOrDefaultAsync(p => p.Id == id);
            if (request == null) return RedirectToAction(nameof(Index));

            if (request.Status != PurchaseRequest.Statuses.Pending)
            {
                TempData["Error"] = "تمت معالجة هذا الطلب بالفعل";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            request.Status = PurchaseRequest.Statuses.Approved;
            request.ApprovedByUserId = currentUser?.Id;
            request.ApprovedByName = currentUser?.FullName ?? currentUser?.UserName;
            request.ApprovedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Approve", "PurchaseRequest",
                $"الموافقة على طلب شراء الموظف: {request.Employee?.FullName ?? request.EmployeeId.ToString()} بقيمة تقديرية {request.EstimatedAmount:N3} KD",
                request.Id);

            TempData["Success"] = "تمت الموافقة على طلب الشراء بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (!await _perms.HasAccessAsync("PurchaseRequestApprove"))
            {
                TempData["Error"] = "غير مصرح لك برفض طلبات الشراء";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "يجب كتابة سبب الرفض";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.PurchaseRequests.Include(p => p.Employee).FirstOrDefaultAsync(p => p.Id == id);
            if (request == null) return RedirectToAction(nameof(Index));

            if (request.Status != PurchaseRequest.Statuses.Pending)
            {
                TempData["Error"] = "تمت معالجة هذا الطلب بالفعل";
                return RedirectToAction(nameof(Index));
            }

            request.Status = PurchaseRequest.Statuses.Rejected;
            request.RejectionReason = reason.Trim();
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Reject", "PurchaseRequest",
                $"رفض طلب شراء الموظف: {request.Employee?.FullName ?? request.EmployeeId.ToString()} | السبب: {request.RejectionReason}",
                request.Id);

            TempData["Success"] = "تم رفض طلب الشراء";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CashierReview(int id, string invoiceNumber, int? supplierId, decimal actualAmount, IFormFile? invoicePhoto, string? notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بمطابقة واستلام طلبات الشراء";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.PurchaseRequests
                .Include(p => p.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(p => p.Items)
                .Include(p => p.Custody).ThenInclude(c => c!.PurchaseRequests)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (request == null) return RedirectToAction(nameof(Index));

            if (request.Status != PurchaseRequest.Statuses.Approved)
            {
                TempData["Error"] = "لا يمكن اعتماد وتسجيل هذا الطلب — يجب أن يكون بحالة (تمت الموافقة - بانتظار الشراء)";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                TempData["Error"] = "رقم الفاتورة مطلوب";
                return RedirectToAction(nameof(Index));
            }

            if (actualAmount <= 0)
            {
                TempData["Error"] = "المبلغ الفعلي يجب أن يكون أكبر من صفر";
                return RedirectToAction(nameof(Index));
            }

            // إعادة التحقق وقت الاعتماد (وليس وقت الطلب فقط) من أن المبلغ الفعلي لا يتجاوز
            // المتبقي الحالي في العهدة، لضمان عدم تجاوز مجموع المشتريات المعتمدة مبلغ العهدة الأصلي
            if (request.Custody != null && actualAmount > request.Custody.RemainingAmount)
            {
                TempData["Error"] = $"لا يمكن الاعتماد — المبلغ الفعلي ({actualAmount:N3}) أكبر من المتبقي الحالي في العهدة ({request.Custody.RemainingAmount:N3}) د.ك";
                return RedirectToAction(nameof(Index));
            }

            string? photoPath = null;
            if (invoicePhoto != null && invoicePhoto.Length > 0)
            {
                var ext = Path.GetExtension(invoicePhoto.FileName);
                if (!AllowedPhotoExtensions.Contains(ext))
                {
                    TempData["Error"] = "صيغة صورة الفاتورة غير مدعومة (jpg, jpeg, png, webp فقط)";
                    return RedirectToAction(nameof(Index));
                }
                if (invoicePhoto.Length > MaxPhotoSizeBytes)
                {
                    TempData["Error"] = "حجم صورة الفاتورة أكبر من الحد المسموح (5 ميجابايت)";
                    return RedirectToAction(nameof(Index));
                }

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "purchase-invoices");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
                var fullPath = Path.Combine(uploadsDir, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await invoicePhoto.CopyToAsync(stream);
                }
                photoPath = $"/uploads/purchase-invoices/{fileName}";
            }

            var expense = new Expense
            {
                Description = $"شراء عهدة ({request.Employee?.FullName ?? request.EmployeeId.ToString()}): {request.ItemsSummary}",
                Amount = actualAmount,
                Category = "مشتريات عهدة",
                Department = request.Employee?.DepartmentNav?.Name,
                ExpenseDate = DateTime.Today,
                PaymentMethod = "نقدي",
                EmployeeId = request.EmployeeId,
                Notes = notes,
                CreatedAt = DateTime.Now
            };
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            request.InvoiceNumber = invoiceNumber.Trim();
            if (supplierId.HasValue) request.SupplierId = supplierId;
            request.ActualAmount = actualAmount;
            request.InvoicePhotoPath = photoPath;
            request.ReviewedByUserId = currentUser?.Id;
            request.ReviewedByName = currentUser?.FullName ?? currentUser?.UserName;
            request.ReviewedAt = DateTime.Now;
            request.ExpenseId = expense.Id;
            request.Status = PurchaseRequest.Statuses.Completed;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Approve", "PurchaseRequest",
                $"مطابقة واستلام واعتماد طلب شراء الموظف: {request.Employee?.FullName ?? request.EmployeeId.ToString()} | رقم الفاتورة: {request.InvoiceNumber} | المبلغ الفعلي: {actualAmount:N3} KD",
                request.Id);

            TempData["Success"] = "تم اعتماد وتسجيل طلب الشراء بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف طلبات الشراء";
                return RedirectToAction(nameof(Index));
            }

            var request = await _context.PurchaseRequests.Include(p => p.Employee).Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
            if (request != null)
            {
                if (request.Status == PurchaseRequest.Statuses.Completed)
                {
                    TempData["Error"] = "لا يمكن حذف طلب شراء معتمَد وله فاتورة مسجَّلة على الصندوق";
                    return RedirectToAction(nameof(Index));
                }

                string empName = request.Employee?.FullName ?? request.EmployeeId.ToString();

                _context.PurchaseRequestItems.RemoveRange(request.Items);
                _context.PurchaseRequests.Remove(request);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "PurchaseRequest",
                    $"حذف طلب شراء الموظف: {empName} بقيمة تقديرية {request.EstimatedAmount:N3} KD",
                    id);

                TempData["Success"] = "تم حذف طلب الشراء بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
