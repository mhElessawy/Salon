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
    // ذمم الموردين الآجلة — فواتير شراء اعتُمدت بطريقة "آجل من المورد" (بدون خصم فوري من العهدة أو
    // الصندوق أو البنك)، مع تتبع دفعاتها حتى السداد الكامل
    [Authorize]
    public class SupplierInvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupplierInvoicesController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        private async Task<bool> IsCashierOrManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager") || roles.Contains("Cashier");
        }

        private async Task<bool> IsManagerAsync(ApplicationUser? user)
        {
            if (user == null) return false;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains("Admin") || roles.Contains("Manager");
        }

        public async Task<IActionResult> Index(int? supplierId, string? status)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool canPay = await IsCashierOrManagerAsync(currentUser);
            bool isManager = await IsManagerAsync(currentUser);

            var query = _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.PurchaseRequest).ThenInclude(p => p!.Employee)
                .Include(i => i.Installments)
                .Include(i => i.Payments)
                .AsQueryable();

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId.Value);

            var invoices = (await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt).ToListAsync());

            if (!string.IsNullOrEmpty(status))
                invoices = invoices.Where(i => i.Status == status).ToList();

            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name", supplierId);
            ViewBag.SupplierId = supplierId;
            ViewBag.Status = status;
            ViewBag.CanPay = canPay;
            ViewBag.IsManager = isManager;
            ViewBag.Custodies = await GetCustodyOptionsAsync();
            ViewBag.TotalOutstanding = invoices.Sum(i => i.RemainingAmount);
            ViewBag.OverdueCount = invoices.Count(i => i.Status == SupplierInvoice.Statuses.Overdue);

            return View(invoices);
        }

        private async Task<List<Custody>> GetCustodyOptionsAsync()
        {
            return await _context.Custodies
                .Include(c => c.Employee)
                .Include(c => c.PurchaseRequests)
                .Include(c => c.InvoicePayments)
                .OrderByDescending(c => c.CustodyDate)
                .ToListAsync();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int supplierId, string invoiceNumber, DateTime invoiceDate, decimal totalAmount,
            string? notes, List<decimal>? installmentAmount, List<DateTime>? installmentDueDate)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بإضافة فواتير موردين آجلة";
                return RedirectToAction(nameof(Index));
            }

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null)
            {
                TempData["Error"] = "المورد المختار غير موجود";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                TempData["Error"] = "رقم الفاتورة مطلوب";
                return RedirectToAction(nameof(Index));
            }

            if (totalAmount <= 0)
            {
                TempData["Error"] = "قيمة الفاتورة يجب أن تكون أكبر من صفر";
                return RedirectToAction(nameof(Index));
            }

            var installments = new List<SupplierInvoiceInstallment>();
            if (installmentAmount != null)
            {
                for (int i = 0; i < installmentAmount.Count; i++)
                {
                    if (installmentAmount[i] <= 0) continue;
                    var dueDate = (installmentDueDate != null && i < installmentDueDate.Count && installmentDueDate[i] != default)
                        ? installmentDueDate[i] : DateTime.Today;
                    installments.Add(new SupplierInvoiceInstallment { SequenceNo = installments.Count + 1, Amount = installmentAmount[i], DueDate = dueDate });
                }
            }

            if (installments.Count == 0)
                installments.Add(new SupplierInvoiceInstallment { SequenceNo = 1, Amount = totalAmount, DueDate = invoiceDate == default ? DateTime.Today : invoiceDate });

            decimal installmentsTotal = installments.Sum(x => x.Amount);
            if (Math.Abs(installmentsTotal - totalAmount) > 0.001m)
            {
                TempData["Error"] = $"مجموع الدفعات ({installmentsTotal:N3}) يجب أن يساوي قيمة الفاتورة ({totalAmount:N3}) د.ك";
                return RedirectToAction(nameof(Index));
            }

            var invoice = new SupplierInvoice
            {
                SupplierId = supplierId,
                InvoiceNumber = invoiceNumber.Trim(),
                InvoiceDate = invoiceDate == default ? DateTime.Today : invoiceDate,
                TotalAmount = totalAmount,
                Notes = notes,
                CreatedByUserId = currentUser?.Id,
                CreatedByName = currentUser?.FullName ?? currentUser?.UserName,
                CreatedAt = DateTime.Now,
                Installments = installments
            };
            _context.SupplierInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Add", "SupplierInvoice",
                $"فاتورة مورد آجلة جديدة: {supplier.Name} | رقم الفاتورة: {invoice.InvoiceNumber} | القيمة: {totalAmount:N3} KD",
                invoice.Id);

            TempData["Success"] = "تم تسجيل فاتورة المورد الآجلة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPayment(int invoiceId, decimal amount, DateTime paymentDate,
            string source, int? custodyId, string? referenceNumber, string? notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بتسجيل دفعات الموردين";
                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return RedirectToAction(nameof(Index));

            if (amount <= 0)
            {
                TempData["Error"] = "مبلغ الدفعة يجب أن يكون أكبر من صفر";
                return RedirectToAction(nameof(Index));
            }

            if (amount > invoice.RemainingAmount + 0.001m)
            {
                TempData["Error"] = $"مبلغ الدفعة ({amount:N3}) أكبر من المتبقي على الفاتورة ({invoice.RemainingAmount:N3}) د.ك";
                return RedirectToAction(nameof(Index));
            }

            if (source != SupplierInvoicePayment.Sources.Cash
                && source != SupplierInvoicePayment.Sources.Bank
                && source != SupplierInvoicePayment.Sources.Custody)
            {
                TempData["Error"] = "مصدر الدفع غير صحيح";
                return RedirectToAction(nameof(Index));
            }

            Custody? custody = null;
            if (source == SupplierInvoicePayment.Sources.Custody)
            {
                if (!custodyId.HasValue)
                {
                    TempData["Error"] = "يجب اختيار العهدة المستخدمة للسداد";
                    return RedirectToAction(nameof(Index));
                }

                custody = await _context.Custodies
                    .Include(c => c.PurchaseRequests)
                    .Include(c => c.InvoicePayments)
                    .FirstOrDefaultAsync(c => c.Id == custodyId.Value);
                if (custody == null)
                {
                    TempData["Error"] = "العهدة المختارة غير موجودة";
                    return RedirectToAction(nameof(Index));
                }

                if (amount > custody.RemainingAmount + 0.001m)
                {
                    TempData["Error"] = $"مبلغ الدفعة ({amount:N3}) أكبر من المتبقي في العهدة المختارة ({custody.RemainingAmount:N3}) د.ك";
                    return RedirectToAction(nameof(Index));
                }
            }

            var payment = new SupplierInvoicePayment
            {
                SupplierInvoiceId = invoice.Id,
                Amount = amount,
                PaymentDate = paymentDate == default ? DateTime.Today : paymentDate,
                Source = source,
                CustodyId = source == SupplierInvoicePayment.Sources.Custody ? custodyId : null,
                ReferenceNumber = referenceNumber?.Trim(),
                Notes = notes,
                PaidByUserId = currentUser?.Id,
                PaidByName = currentUser?.FullName ?? currentUser?.UserName,
                CreatedAt = DateTime.Now
            };

            if (source == SupplierInvoicePayment.Sources.Cash || source == SupplierInvoicePayment.Sources.Bank)
            {
                var expense = new Expense
                {
                    Description = $"دفعة مورد آجل - {invoice.Supplier?.Name ?? "-"} - فاتورة {invoice.InvoiceNumber}",
                    Amount = amount,
                    Category = "دفعة مورد آجل",
                    ExpenseDate = payment.PaymentDate,
                    PaymentMethod = source == SupplierInvoicePayment.Sources.Cash ? "نقدي" : "تحويل بنكي",
                    Notes = notes,
                    CreatedAt = DateTime.Now
                };
                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();
                payment.ExpenseId = expense.Id;
            }

            _context.SupplierInvoicePayments.Add(payment);
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Add", "SupplierInvoicePayment",
                $"دفعة لفاتورة المورد {invoice.Supplier?.Name ?? "-"} رقم {invoice.InvoiceNumber} بمبلغ {amount:N3} KD | المصدر: {source}"
                + (custody != null ? $" ({custody.Employee?.FullName})" : ""),
                payment.Id);

            TempData["Success"] = "تم تسجيل دفعة المورد بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بحذف دفعات الموردين";
                return RedirectToAction(nameof(Index));
            }

            var payment = await _context.SupplierInvoicePayments
                .Include(p => p.SupplierInvoice).ThenInclude(i => i!.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (payment != null)
            {
                if (payment.ExpenseId.HasValue)
                {
                    var linkedExpense = await _context.Expenses.FindAsync(payment.ExpenseId.Value);
                    if (linkedExpense != null)
                        _context.Expenses.Remove(linkedExpense);
                }

                decimal amount = payment.Amount;
                string supplierName = payment.SupplierInvoice?.Supplier?.Name ?? "-";
                string invoiceNumber = payment.SupplierInvoice?.InvoiceNumber ?? "-";

                _context.SupplierInvoicePayments.Remove(payment);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Delete", "SupplierInvoicePayment",
                    $"حذف دفعة فاتورة المورد {supplierName} رقم {invoiceNumber} بمبلغ {amount:N3} KD", id);

                TempData["Success"] = "تم حذف الدفعة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
