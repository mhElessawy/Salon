using System.Text;
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
    // الصندوق أو البنك)، مرتبطة بالمخزون (كل سطر منتج ينشئ حركة استلام ويُحدِّث تكلفة الشراء)، مع
    // تتبع دفعاتها حتى السداد الكامل
    [Authorize]
    public class SupplierInvoicesController : Controller
    {
        private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };
        private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SupplierInvoicesController(ApplicationDbContext context, IAuditService audit,
            UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _env = env;
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

        // ===== قائمة فواتير الموردين الآجلة =====
        public async Task<IActionResult> Index(int? supplierId, string? status, DateTime? fromDate, DateTime? toDate,
            DateTime? dueDate, string? invoiceNumber)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            bool canPay = await IsCashierOrManagerAsync(currentUser);
            bool isManager = await IsManagerAsync(currentUser);

            var allInvoices = await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.PurchaseRequest).ThenInclude(p => p!.Employee)
                .Include(i => i.Items)
                .Include(i => i.Installments)
                .Include(i => i.Payments)
                .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt)
                .ToListAsync();

            // بطاقات الملخص — تُحسب على كل الفواتير الفعلية (غير المسودة وغير الملغاة) بصرف النظر عن فلاتر الجدول
            var activeInvoices = allInvoices.Where(i => !i.IsDraft && !i.IsCancelled).ToList();
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var dueThisMonth = activeInvoices
                .Where(i => i.RemainingAmount > 0.001m && i.NextDueDate.HasValue
                         && i.NextDueDate.Value >= monthStart && i.NextDueDate.Value < monthEnd)
                .ToList();

            var paidThisMonthInvoiceIds = new HashSet<int>();
            decimal paidThisMonthAmount = 0;
            foreach (var inv in allInvoices)
            {
                foreach (var p in inv.Payments.Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthEnd))
                {
                    paidThisMonthAmount += p.Amount;
                    paidThisMonthInvoiceIds.Add(inv.Id);
                }
            }

            ViewBag.SummaryTotalCount = activeInvoices.Count;
            ViewBag.SummaryTotalOutstanding = activeInvoices.Sum(i => i.RemainingAmount);
            ViewBag.SummaryDueThisMonthAmount = dueThisMonth.Sum(i => i.RemainingAmount);
            ViewBag.SummaryDueThisMonthCount = dueThisMonth.Count;
            ViewBag.SummaryOverdueAmount = activeInvoices.Where(i => i.Status == SupplierInvoice.Statuses.Overdue).Sum(i => i.RemainingAmount);
            ViewBag.SummaryOverdueCount = activeInvoices.Count(i => i.Status == SupplierInvoice.Statuses.Overdue);
            ViewBag.SummaryPaidThisMonthAmount = paidThisMonthAmount;
            ViewBag.SummaryPaidThisMonthCount = paidThisMonthInvoiceIds.Count;

            // فلاتر الجدول
            var filtered = allInvoices.AsEnumerable();
            if (supplierId.HasValue)
                filtered = filtered.Where(i => i.SupplierId == supplierId.Value);
            if (!string.IsNullOrWhiteSpace(invoiceNumber))
                filtered = filtered.Where(i => i.InvoiceNumber.Contains(invoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase)
                                             || (i.Reference != null && i.Reference.Contains(invoiceNumber.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (fromDate.HasValue)
                filtered = filtered.Where(i => i.InvoiceDate.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                filtered = filtered.Where(i => i.InvoiceDate.Date <= toDate.Value.Date);
            if (dueDate.HasValue)
                filtered = filtered.Where(i => i.NextDueDate.HasValue && i.NextDueDate.Value.Date == dueDate.Value.Date);
            if (!string.IsNullOrEmpty(status))
                filtered = filtered.Where(i => i.Status == status);

            var invoices = filtered.ToList();

            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name", supplierId);
            ViewBag.Products = await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
            ViewBag.SupplierId = supplierId;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.DueDate = dueDate;
            ViewBag.InvoiceNumber = invoiceNumber;
            ViewBag.CanPay = canPay;
            ViewBag.IsManager = isManager;
            ViewBag.Custodies = await GetCustodyOptionsAsync();

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

        private async Task<(string? path, string? error)> SaveAttachmentAsync(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return (null, null);

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedAttachmentExtensions.Contains(ext))
                return (null, "صيغة المرفق غير مدعومة (jpg, jpeg, png, webp, pdf فقط)");
            if (file.Length > MaxAttachmentSizeBytes)
                return (null, "حجم المرفق أكبر من الحد المسموح (5 ميجابايت)");

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return ($"/uploads/{folder}/{fileName}", null);
        }

        // يستلم كمية من منتج في المخزون ويُحدِّث تكلفة الشراء بطريقة المتوسط المرجَّح
        private void ReceiveStock(Product product, int quantity, decimal unitCost, DateTime movementDate, int? supplierId, string notes)
        {
            if (quantity <= 0) return;
            decimal oldQty = product.StockQuantity;
            decimal newQty = oldQty + quantity;
            if (newQty > 0)
                product.PurchasePrice = Math.Round(((oldQty * product.PurchasePrice) + (quantity * unitCost)) / newQty, 3);
            product.StockQuantity += quantity;

            _context.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = "استلام",
                Quantity = quantity,
                UnitPrice = unitCost,
                SupplierId = supplierId,
                Notes = notes,
                MovementDate = movementDate,
                CreatedAt = DateTime.Now
            });
        }

        // عكس أثر استلام سابق على المخزون عند إلغاء فاتورة (لا يُعيد حساب متوسط التكلفة، فقط الكمية)
        private void ReverseStock(Product product, int quantity, int? supplierId, string notes)
        {
            if (quantity <= 0) return;
            product.StockQuantity -= quantity;

            _context.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                MovementType = "إلغاء فاتورة مورد",
                Quantity = quantity,
                UnitPrice = 0,
                SupplierId = supplierId,
                Notes = notes,
                MovementDate = DateTime.Today,
                CreatedAt = DateTime.Now
            });
        }

        // يطبّق أثر الاستلام على المخزون لكل سطر منتج مرتبط بمنتج فعلي في الفاتورة
        private void PostInvoiceItemsToInventory(SupplierInvoice invoice)
        {
            foreach (var item in invoice.Items)
            {
                if (!item.ProductId.HasValue) continue;
                var product = item.Product ?? _context.Products.Find(item.ProductId.Value);
                if (product == null) continue;
                ReceiveStock(product, item.Quantity, item.UnitPrice, invoice.InvoiceDate, invoice.SupplierId,
                    $"استلام من فاتورة المورد {invoice.Reference ?? invoice.InvoiceNumber}");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddProduct(string name, string? unit, decimal purchasePrice, decimal salePrice, int? supplierId, string? category)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
                return Json(new { success = false, message = "غير مصرح لك بإضافة منتجات" });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "اسم المنتج مطلوب" });

            var product = new Product
            {
                Name = name.Trim(),
                Unit = unit,
                Category = category,
                PurchasePrice = purchasePrice,
                SalePrice = salePrice,
                SupplierId = supplierId,
                OpeningQuantity = 0,
                StockQuantity = 0,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Add", "Inventory", $"منتج جديد من نافذة فاتورة مورد: {product.Name}", product.Id);

            return Json(new { success = true, id = product.Id, name = product.Name, unit = product.Unit, purchasePrice = product.PurchasePrice });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddSupplier(string name, string? phone)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
                return Json(new { success = false, message = "غير مصرح لك بإضافة موردين" });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "اسم المورد مطلوب" });

            var supplier = new Supplier { Name = name.Trim(), Phone = phone, IsActive = true, CreatedAt = DateTime.Now };
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Add", "Suppliers", $"مورد جديد من نافذة فاتورة مورد: {supplier.Name}", supplier.Id);

            return Json(new { success = true, id = supplier.Id, name = supplier.Name });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int supplierId, string invoiceNumber, DateTime invoiceDate, DateTime dueDate,
            decimal totalAmount, decimal discountAmount, decimal extraExpenses, string? notes, bool isDraft,
            IFormFile? attachment,
            List<int?>? itemProductId, List<string>? itemProductName, List<string?>? itemUnit,
            List<int>? itemQuantity, List<decimal>? itemUnitPrice, List<decimal>? itemDiscount,
            List<decimal>? installmentAmount, List<DateTime>? installmentDueDate,
            string paymentOption = "none", decimal? paymentAmount = null, DateTime? paymentDate = null,
            string? paymentSource = null, int? paymentCustodyId = null, string? paymentReference = null,
            string? paymentNotes = null, IFormFile? paymentAttachment = null)
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

            // بناء أسطر المنتجات من القوائم المتوازية القادمة من جدول القسم الثاني
            var items = new List<SupplierInvoiceItem>();
            if (itemProductName != null)
            {
                for (int i = 0; i < itemProductName.Count; i++)
                {
                    var pName = itemProductName[i]?.Trim();
                    var qty = itemQuantity != null && i < itemQuantity.Count ? itemQuantity[i] : 0;
                    if (string.IsNullOrWhiteSpace(pName) || qty <= 0) continue;

                    items.Add(new SupplierInvoiceItem
                    {
                        ProductId = itemProductId != null && i < itemProductId.Count ? itemProductId[i] : null,
                        ProductName = pName,
                        Unit = itemUnit != null && i < itemUnit.Count ? itemUnit[i] : null,
                        Quantity = qty,
                        UnitPrice = itemUnitPrice != null && i < itemUnitPrice.Count ? itemUnitPrice[i] : 0,
                        Discount = itemDiscount != null && i < itemDiscount.Count ? itemDiscount[i] : 0
                    });
                }
            }

            // قيمة الفاتورة تُحسب تلقائياً من أسطر المنتجات لو وُجدت، وإلا تُستخدم القيمة المُدخلة يدوياً
            decimal computedTotal;
            if (items.Count > 0)
            {
                var itemsSubtotal = items.Sum(x => x.Quantity * x.UnitPrice);
                var itemsDiscount = items.Sum(x => x.Discount);
                computedTotal = itemsSubtotal - itemsDiscount - discountAmount + extraExpenses;
            }
            else
            {
                computedTotal = totalAmount - discountAmount + extraExpenses;
            }

            if (computedTotal <= 0)
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
                    var dDate = (installmentDueDate != null && i < installmentDueDate.Count && installmentDueDate[i] != default)
                        ? installmentDueDate[i] : dueDate;
                    installments.Add(new SupplierInvoiceInstallment { SequenceNo = installments.Count + 1, Amount = installmentAmount[i], DueDate = dDate });
                }
            }

            if (installments.Count == 0)
                installments.Add(new SupplierInvoiceInstallment { SequenceNo = 1, Amount = computedTotal, DueDate = dueDate == default ? invoiceDate : dueDate });

            decimal installmentsTotal = installments.Sum(x => x.Amount);
            if (Math.Abs(installmentsTotal - computedTotal) > 0.001m)
            {
                TempData["Error"] = $"مجموع دفعات جدول السداد ({installmentsTotal:N3}) يجب أن يساوي قيمة الفاتورة ({computedTotal:N3}) د.ك";
                return RedirectToAction(nameof(Index));
            }

            string? attachmentPath = null;
            if (attachment != null)
            {
                var (path, error) = await SaveAttachmentAsync(attachment, "supplier-invoices");
                if (error != null)
                {
                    TempData["Error"] = error;
                    return RedirectToAction(nameof(Index));
                }
                attachmentPath = path;
            }

            var invoice = new SupplierInvoice
            {
                SupplierId = supplierId,
                InvoiceNumber = invoiceNumber.Trim(),
                InvoiceDate = invoiceDate == default ? DateTime.Today : invoiceDate,
                TotalAmount = computedTotal,
                DiscountAmount = discountAmount,
                ExtraExpenses = extraExpenses,
                AttachmentPath = attachmentPath,
                IsDraft = isDraft,
                Notes = notes,
                CreatedByUserId = currentUser?.Id,
                CreatedByName = currentUser?.FullName ?? currentUser?.UserName,
                CreatedAt = DateTime.Now,
                Items = items,
                Installments = installments
            };
            _context.SupplierInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            invoice.Reference = $"SINV-{invoice.Id:D6}";

            if (!isDraft)
            {
                PostInvoiceItemsToInventory(invoice);
            }
            await _context.SaveChangesAsync();

            string paymentMsg = "";
            if (!isDraft && paymentOption != "none" && paymentAmount.HasValue && paymentAmount.Value > 0)
            {
                var (ok, error) = await ValidateAndCreatePaymentAsync(invoice, paymentAmount.Value,
                    paymentDate ?? DateTime.Today, paymentSource ?? SupplierInvoicePayment.Sources.Cash,
                    paymentCustodyId, paymentReference, paymentNotes, paymentAttachment, currentUser);
                if (!ok)
                {
                    TempData["Error"] = $"تم حفظ الفاتورة، لكن تعذَّر تسجيل الدفعة: {error}";
                    return RedirectToAction(nameof(Index));
                }
                paymentMsg = " مع تسجيل الدفعة";
            }

            await _audit.LogAsync("Add", "SupplierInvoice",
                (isDraft ? "مسودة فاتورة مورد آجلة: " : "فاتورة مورد آجلة جديدة: ") +
                $"{supplier.Name} | رقم الفاتورة: {invoice.InvoiceNumber} | المرجع: {invoice.Reference} | القيمة: {computedTotal:N3} KD",
                invoice.Id);

            TempData["Success"] = isDraft ? "تم حفظ مسودة الفاتورة بنجاح" : $"تم تسجيل فاتورة المورد الآجلة بنجاح{paymentMsg}";
            return RedirectToAction(nameof(Index));
        }

        // اعتماد فاتورة محفوظة كمسودة — يطبّق أثرها على المخزون (لا يوجد دفع وقت الاعتماد، يُسجَّل لاحقاً)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDraft(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsCashierOrManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك باعتماد فواتير الموردين";
                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.SupplierInvoices
                .Include(i => i.Items).ThenInclude(it => it.Product)
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null || !invoice.IsDraft) return RedirectToAction(nameof(Index));

            PostInvoiceItemsToInventory(invoice);
            invoice.IsDraft = false;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Edit", "SupplierInvoice",
                $"اعتماد مسودة فاتورة المورد {invoice.Supplier?.Name ?? "-"} رقم {invoice.InvoiceNumber}", invoice.Id);

            TempData["Success"] = "تم اعتماد الفاتورة وتحديث المخزون";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelInvoice(int id, string? reason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (!await IsManagerAsync(currentUser))
            {
                TempData["Error"] = "غير مصرح لك بإلغاء فواتير الموردين";
                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.SupplierInvoices
                .Include(i => i.Items).ThenInclude(it => it.Product)
                .Include(i => i.Payments)
                .Include(i => i.Supplier)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return RedirectToAction(nameof(Index));

            if (invoice.IsCancelled)
                return RedirectToAction(nameof(Index));

            if (invoice.Payments.Any())
            {
                TempData["Error"] = "لا يمكن إلغاء فاتورة عليها دفعات مسجَّلة — يجب حذف الدفعات أولاً";
                return RedirectToAction(nameof(Index));
            }

            if (!invoice.IsDraft)
            {
                foreach (var item in invoice.Items)
                {
                    if (!item.ProductId.HasValue) continue;
                    var product = item.Product ?? await _context.Products.FindAsync(item.ProductId.Value);
                    if (product == null) continue;
                    ReverseStock(product, item.Quantity, invoice.SupplierId,
                        $"إلغاء فاتورة المورد {invoice.Reference ?? invoice.InvoiceNumber}");
                }
            }

            invoice.IsCancelled = true;
            invoice.CancelledAt = DateTime.Now;
            invoice.CancelledByName = currentUser?.FullName ?? currentUser?.UserName;
            invoice.CancelReason = reason;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Cancel", "SupplierInvoice",
                $"إلغاء فاتورة المورد {invoice.Supplier?.Name ?? "-"} رقم {invoice.InvoiceNumber}"
                + (!string.IsNullOrWhiteSpace(reason) ? $" | السبب: {reason}" : ""), invoice.Id);

            TempData["Success"] = "تم إلغاء الفاتورة";
            return RedirectToAction(nameof(Index));
        }

        // يتحقَّق من صحة بيانات دفعة ويُسجِّلها فعلياً (يُستخدم من Create ومن RegisterPayment)
        private async Task<(bool ok, string? error)> ValidateAndCreatePaymentAsync(SupplierInvoice invoice, decimal amount,
            DateTime paymentDate, string source, int? custodyId, string? referenceNumber, string? notes,
            IFormFile? attachment, ApplicationUser? currentUser)
        {
            if (amount <= 0)
                return (false, "مبلغ الدفعة يجب أن يكون أكبر من صفر");

            if (amount > invoice.RemainingAmount + 0.001m)
                return (false, $"مبلغ الدفعة ({amount:N3}) أكبر من المتبقي على الفاتورة ({invoice.RemainingAmount:N3}) د.ك");

            if (source != SupplierInvoicePayment.Sources.Cash
                && source != SupplierInvoicePayment.Sources.Bank
                && source != SupplierInvoicePayment.Sources.Custody)
                return (false, "مصدر الدفع غير صحيح");

            Custody? custody = null;
            if (source == SupplierInvoicePayment.Sources.Custody)
            {
                if (!custodyId.HasValue)
                    return (false, "يجب اختيار العهدة المستخدمة للسداد");

                custody = await _context.Custodies
                    .Include(c => c.PurchaseRequests)
                    .Include(c => c.InvoicePayments)
                    .FirstOrDefaultAsync(c => c.Id == custodyId.Value);
                if (custody == null)
                    return (false, "العهدة المختارة غير موجودة");

                if (amount > custody.RemainingAmount + 0.001m)
                    return (false, $"مبلغ الدفعة ({amount:N3}) أكبر من المتبقي في العهدة المختارة ({custody.RemainingAmount:N3}) د.ك");
            }

            string? attachmentPath = null;
            if (attachment != null)
            {
                var (path, error) = await SaveAttachmentAsync(attachment, "supplier-payments");
                if (error != null) return (false, error);
                attachmentPath = path;
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
                AttachmentPath = attachmentPath,
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

            return (true, null);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPayment(int invoiceId, decimal amount, DateTime paymentDate,
            string source, int? custodyId, string? referenceNumber, string? notes, IFormFile? attachment)
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

            if (invoice.IsDraft)
            {
                TempData["Error"] = "لا يمكن تسجيل دفعة على فاتورة في حالة مسودة — يجب اعتمادها أولاً";
                return RedirectToAction(nameof(Index));
            }
            if (invoice.IsCancelled)
            {
                TempData["Error"] = "لا يمكن تسجيل دفعة على فاتورة ملغاة";
                return RedirectToAction(nameof(Index));
            }

            var (ok, error) = await ValidateAndCreatePaymentAsync(invoice, amount, paymentDate, source, custodyId,
                referenceNumber, notes, attachment, currentUser);
            if (!ok)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Index));
            }

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

        // ===== كشف حساب المورد =====
        public async Task<IActionResult> Statement(int? supplierId, DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name", supplierId);
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SupplierId = supplierId;

            if (!supplierId.HasValue)
                return View(new SupplierStatementViewModel());

            var supplier = await _context.Suppliers.FindAsync(supplierId.Value);
            if (supplier == null)
                return View(new SupplierStatementViewModel());

            var model = await BuildStatementAsync(supplier, fromDate, toDate);
            return View(model);
        }

        private async Task<SupplierStatementViewModel> BuildStatementAsync(Supplier supplier, DateTime? fromDate, DateTime? toDate)
        {
            var invoices = await _context.SupplierInvoices
                .Include(i => i.Payments)
                .Where(i => i.SupplierId == supplier.Id)
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync();

            var entries = new List<SupplierStatementEntry>();
            foreach (var inv in invoices)
            {
                entries.Add(new SupplierStatementEntry
                {
                    Date = inv.InvoiceDate,
                    Type = "فاتورة شراء",
                    DocumentNumber = inv.Reference ?? inv.InvoiceNumber,
                    Description = string.IsNullOrWhiteSpace(inv.Notes) ? $"فاتورة شراء رقم {inv.InvoiceNumber}" : inv.Notes,
                    Debit = inv.TotalAmount,
                    Credit = 0
                });

                if (inv.IsCancelled && inv.CancelledAt.HasValue)
                {
                    entries.Add(new SupplierStatementEntry
                    {
                        Date = inv.CancelledAt.Value,
                        Type = "إلغاء فاتورة",
                        DocumentNumber = inv.Reference ?? inv.InvoiceNumber,
                        Description = string.IsNullOrWhiteSpace(inv.CancelReason) ? "إلغاء فاتورة" : inv.CancelReason,
                        Debit = 0,
                        Credit = inv.TotalAmount
                    });
                }

                foreach (var p in inv.Payments)
                {
                    string type = p.Source switch
                    {
                        SupplierInvoicePayment.Sources.Bank => "تحويل بنكي",
                        SupplierInvoicePayment.Sources.Custody => "دفعة من عهدة",
                        _ => "دفعة نقدية"
                    };
                    entries.Add(new SupplierStatementEntry
                    {
                        Date = p.PaymentDate,
                        Type = type,
                        DocumentNumber = p.ReferenceNumber ?? "-",
                        Description = string.IsNullOrWhiteSpace(p.Notes) ? $"دفعة على فاتورة {inv.InvoiceNumber}" : p.Notes,
                        Debit = 0,
                        Credit = p.Amount
                    });
                }
            }

            entries = entries.OrderBy(e => e.Date).ToList();

            decimal running = 0;
            foreach (var e in entries)
            {
                running += e.Debit - e.Credit;
                e.Balance = running;
            }

            var filtered = entries.AsEnumerable();
            if (fromDate.HasValue) filtered = filtered.Where(e => e.Date.Date >= fromDate.Value.Date);
            if (toDate.HasValue) filtered = filtered.Where(e => e.Date.Date <= toDate.Value.Date);

            return new SupplierStatementViewModel
            {
                Supplier = supplier,
                Entries = filtered.ToList(),
                TotalPurchases = invoices.Where(i => !i.IsCancelled).Sum(i => i.TotalAmount),
                TotalPayments = invoices.SelectMany(i => i.Payments).Sum(p => p.Amount),
                CurrentBalance = running
            };
        }

        public async Task<IActionResult> StatementExport(int supplierId, DateTime? fromDate, DateTime? toDate)
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null) return RedirectToAction(nameof(Statement));

            var model = await BuildStatementAsync(supplier, fromDate, toDate);

            var sb = new StringBuilder();
            sb.AppendLine("التاريخ,النوع,رقم المستند,البيان,مدين,دائن,الرصيد");
            foreach (var e in model.Entries)
            {
                sb.AppendLine(string.Join(",",
                    e.Date.ToString("yyyy/MM/dd"),
                    CsvEscape(e.Type),
                    CsvEscape(e.DocumentNumber),
                    CsvEscape(e.Description),
                    e.Debit.ToString("F3"),
                    e.Credit.ToString("F3"),
                    e.Balance.ToString("F3")));
            }

            var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            var fileName = $"kashf-hesab-{supplier.Name}-{DateTime.Today:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }

        private static string CsvEscape(string? value)
        {
            value ??= "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // ===== تقرير أعمار الديون =====
        public async Task<IActionResult> Aging()
        {
            var invoices = await _context.SupplierInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Installments)
                .Include(i => i.Payments)
                .Where(i => !i.IsDraft && !i.IsCancelled)
                .ToListAsync();

            var withBalance = invoices.Where(i => i.RemainingAmount > 0.001m).ToList();

            var buckets = new List<AgingBucket>
            {
                new() { Key = "current", Label = "غير مستحقة" },
                new() { Key = "b1_30", Label = "من 1 إلى 30 يومًا" },
                new() { Key = "b31_60", Label = "من 31 إلى 60 يومًا" },
                new() { Key = "b61_90", Label = "من 61 إلى 90 يومًا" },
                new() { Key = "b90plus", Label = "أكثر من 90 يومًا" }
            };

            var rows = new List<AgingRow>();
            foreach (var inv in withBalance)
            {
                int days = inv.OverdueDays;
                string bucketKey = days switch
                {
                    0 => "current",
                    <= 30 => "b1_30",
                    <= 60 => "b31_60",
                    <= 90 => "b61_90",
                    _ => "b90plus"
                };

                var bucket = buckets.First(b => b.Key == bucketKey);
                bucket.TotalAmount += inv.RemainingAmount;
                bucket.Count++;

                rows.Add(new AgingRow
                {
                    SupplierName = inv.Supplier?.Name ?? "-",
                    InvoiceNumber = inv.InvoiceNumber,
                    DueDate = inv.NextDueDate,
                    OverdueDays = days,
                    RemainingAmount = inv.RemainingAmount,
                    Status = inv.Status
                });
            }

            decimal grandTotal = withBalance.Sum(i => i.RemainingAmount);
            foreach (var b in buckets)
                b.Percentage = grandTotal > 0 ? (b.TotalAmount / grandTotal) * 100 : 0;

            var model = new AgingReportViewModel
            {
                Buckets = buckets,
                Rows = rows.OrderByDescending(r => r.OverdueDays).ToList(),
                GrandTotal = grandTotal
            };

            return View(model);
        }
    }

    public class SupplierStatementEntry
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public string DocumentNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
    }

    public class SupplierStatementViewModel
    {
        public Supplier? Supplier { get; set; }
        public List<SupplierStatementEntry> Entries { get; set; } = new();
        public decimal TotalPurchases { get; set; }
        public decimal TotalPayments { get; set; }
        public decimal CurrentBalance { get; set; }
    }

    public class AgingBucket
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class AgingRow
    {
        public string SupplierName { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public int OverdueDays { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Status { get; set; } = "";
    }

    public class AgingReportViewModel
    {
        public List<AgingBucket> Buckets { get; set; } = new();
        public List<AgingRow> Rows { get; set; } = new();
        public decimal GrandTotal { get; set; }
    }
}