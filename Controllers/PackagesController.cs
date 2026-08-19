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
    public class PackagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;
        private readonly IEmailService _emailService;
        private readonly IDailyClosureService _closure;

        public PackagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IAuditService audit, IEmailService emailService, IDailyClosureService closure)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
            _emailService = emailService;
            _closure = closure;
        }

        // ─── الباقات - Tab 1 ─────────────────────────────────────────
        public async Task<IActionResult> Index(string? tab)
        {
            ViewBag.ActiveTab = tab ?? "packages";
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.ServicePackages.Include(p => p.ServiceCategory).AsQueryable();

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(p => p.ServiceCategory == null || p.ServiceCategory.Department == userDept);

            ViewBag.Packages = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            // Customer balances
            var balancesQuery = _context.CustomerPackages
                .Include(cp => cp.Customer)
                .Include(cp => cp.ServicePackage)
                .Where(cp => cp.IsActive);

            if (userDept == "حلاقة" || userDept == "مساج")
                balancesQuery = balancesQuery.Where(cp =>
                    cp.ServicePackage == null ||
                    cp.ServicePackage.ServiceCategory == null ||
                    cp.ServicePackage.ServiceCategory.Department == userDept);

            ViewBag.CustomerPackages = await balancesQuery
                .OrderByDescending(cp => cp.PurchaseDate)
                .ToListAsync();

            // Transactions
            var transQuery = _context.CustomerPackageTransactions
                .Include(t => t.CustomerPackage).ThenInclude(cp => cp!.Customer)
                .Include(t => t.CustomerPackage).ThenInclude(cp => cp!.ServicePackage)
                .Include(t => t.Employee)
                .OrderByDescending(t => t.UsedDate)
                .Take(100);

            ViewBag.Transactions = await transQuery.ToListAsync();

            // For create form
            await PopulateCategories();
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");

            return View(new ServicePackage());
        }

        // ─── إنشاء باقة جديدة ─────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServicePackage model)
        {
            if (ModelState.IsValid)
            {
                _context.ServicePackages.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Packages", $"New package: {model.NameAr}", model.Id);
                TempData["Success"] = "Package added created successfully";
                return RedirectToAction(nameof(Index), new { tab = "packages" });
            }
            TempData["Error"] = "Please check the entered data";
            return RedirectToAction(nameof(Index), new { tab = "packages" });
        }

        // ─── تعديل باقة ─────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            var pkg = await _context.ServicePackages.FindAsync(id);
            if (pkg == null) return NotFound();
            await PopulateCategories(pkg.ServiceCategoryId);
            return View(pkg);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServicePackage model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Packages", $"Edit package: {model.NameAr}", model.Id);
                TempData["Success"] = "Package updated created successfully";
                return RedirectToAction(nameof(Index), new { tab = "packages" });
            }
            await PopulateCategories(model.ServiceCategoryId);
            return View(model);
        }

        // ─── حذف باقة ────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pkg = await _context.ServicePackages.FindAsync(id);
            if (pkg != null)
            {
                pkg.IsActive = false;
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Packages", $"Delete package: {pkg.NameAr}", pkg.Id);
                TempData["Success"] = "Package deleted created successfully";
            }
            return RedirectToAction(nameof(Index), new { tab = "packages" });
        }

        // ─── تعيين باقة لعميل (بيع باقة جديدة) ─────────────────────────
        // هذا الإجراء هو لحظة "بيع الباقة" الفعلية: عند وجود مبلغ مدفوع الآن (pricePaid > 0)
        // يُعتبر بيعاً جديداً يستلزم اتفاقية إلكترونية موقّعة (انظر اتفاقية الباقات الإلكترونية)
        // قبل اعتماد عملية البيع، ثم يُنشئ فاتورة رسمية ويربطها بالاتفاقية وبمحفظة العميل.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPackage(
            int customerId, int servicePackageId, decimal pricePaid, string? notes,
            string? paymentMethod, bool agreedToTerms, string? signatureData)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            var customer = await _context.Customers.FindAsync(customerId);
            var pkg = await _context.ServicePackages.FindAsync(servicePackageId);
            if (customer == null || pkg == null)
            {
                const string notFoundMsg = "بيانات العميل أو الباقة غير صحيحة";
                if (isAjax) return Json(new { success = false, message = notFoundMsg });
                TempData["Error"] = notFoundMsg;
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            var isNewSale = pricePaid > 0;
            if (isNewSale && (!agreedToTerms || string.IsNullOrWhiteSpace(signatureData)))
            {
                const string agreementMsg = "يجب الموافقة على شروط وأحكام الباقة والتوقيع الإلكتروني قبل إتمام البيع";
                if (isAjax) return Json(new { success = false, message = agreementMsg });
                TempData["Error"] = agreementMsg;
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var payMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "نقدي" : paymentMethod;

            var customerPkg = new CustomerPackage
            {
                CustomerId = customerId,
                ServicePackageId = servicePackageId,
                PurchaseDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddDays(pkg.ValidityDays),
                TotalSessions = pkg.SessionCount,
                RemainingSessions = pkg.SessionCount,
                PricePaid = pricePaid,
                CurrentBalance = pricePaid,
                RegistrationType = CustomerPackage.RegistrationTypes.New,
                Notes = notes,
                IsActive = true
            };

            _context.CustomerPackages.Add(customerPkg);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Assign", "Packages", $"Assign package: {pkg.NameAr} for customer ID {customerId} — Amount: {pricePaid:F3} KD", customerPkg.Id);

            int? saleId = null;
            string? invoiceNumber = null;
            int? agreementId = null;

            if (isNewSale)
            {
                var sale = new Sale
                {
                    CustomerId = customerId,
                    SaleDate = DateTime.Now,
                    PaymentMethod = payMethod,
                    SaleType = "باقات",
                    Status = Sale.Statuses.Completed,
                    TotalAmount = pricePaid,
                    Discount = 0,
                    NetAmount = pricePaid,
                    Notes = notes,
                    CreatedByUserId = currentUser?.Id,
                    CreatedByUserName = currentUser?.FullName ?? currentUser?.UserName ?? User.Identity?.Name
                };
                _context.Sales.Add(sale);

                // إعادة المحاولة عند تعارض رقم الفاتورة (طلبين شبه متزامنين) بدل إظهار خطأ للمستخدم
                const int maxInvoiceAttempts = 5;
                for (int attempt = 1; ; attempt++)
                {
                    sale.InvoiceNumber = await GeneratePackageInvoiceNumber();
                    try
                    {
                        await _context.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateException) when (attempt < maxInvoiceAttempts)
                    {
                    }
                }

                _context.SaleItems.Add(new SaleItem
                {
                    SaleId = sale.Id,
                    ItemName = $"باقة: {pkg.NameAr}",
                    Quantity = 1,
                    Price = pricePaid,
                    Total = pricePaid
                });
                await _context.SaveChangesAsync();

                saleId = sale.Id;
                invoiceNumber = sale.InvoiceNumber;

                var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
                var agreement = new PackageAgreement
                {
                    CustomerPackageId = customerPkg.Id,
                    CustomerId = customerId,
                    ServicePackageId = servicePackageId,
                    SaleId = sale.Id,
                    CustomerName = customer.FullName,
                    CustomerPhone = customer.Phone,
                    PackageNameAr = pkg.NameAr,
                    PackageNameEn = pkg.NameEn,
                    SessionCount = pkg.SessionCount,
                    PackagePrice = pkg.Price,
                    AmountPaid = pricePaid,
                    PurchaseDate = customerPkg.PurchaseDate,
                    ExpiryDate = customerPkg.ExpiryDate,
                    PaymentMethod = payMethod,
                    InvoiceNumber = sale.InvoiceNumber,
                    TermsAr = settings.GetValueOrDefault("PackageAgreementTermsAr", PackageAgreement.DefaultTermsAr),
                    TermsEn = settings.GetValueOrDefault("PackageAgreementTermsEn", PackageAgreement.DefaultTermsEn),
                    SignatureImage = signatureData ?? "",
                    SignedAt = DateTime.Now,
                    SignedByUserId = currentUser?.Id,
                    SignedByUserName = currentUser?.FullName ?? currentUser?.UserName ?? User.Identity?.Name ?? "—"
                };
                _context.PackageAgreements.Add(agreement);
                await _context.SaveChangesAsync();
                agreementId = agreement.Id;

                await _audit.LogAsync("Add", "Packages", $"Invoice {sale.InvoiceNumber} — package agreement signed for {customer.FullName}", agreement.Id);
            }

            TempData["Success"] = "تم بيع الباقة للعميل بنجاح";

            if (isAjax)
                return Json(new
                {
                    success = true,
                    customerPackageId = customerPkg.Id,
                    saleId,
                    invoiceNumber,
                    agreementId,
                    printUrl = agreementId.HasValue ? Url.Action("PrintAgreement", new { id = agreementId }) : null
                });

            return RedirectToAction(nameof(Index), new { tab = "balances" });
        }

        // ─── API: بيانات معاينة الاتفاقية قبل التوقيع (عميل + باقة + الشروط الحالية) ───
        [HttpGet]
        public async Task<IActionResult> GetAgreementData(int customerId, int servicePackageId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            var pkg = await _context.ServicePackages.FindAsync(servicePackageId);
            if (customer == null || pkg == null) return NotFound();

            var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            return Json(new
            {
                customerName = customer.FullName,
                customerPhone = customer.Phone,
                packageNameAr = pkg.NameAr,
                packageNameEn = pkg.NameEn,
                sessionCount = pkg.SessionCount,
                price = pkg.Price,
                validityDays = pkg.ValidityDays,
                termsAr = settings.GetValueOrDefault("PackageAgreementTermsAr", PackageAgreement.DefaultTermsAr),
                termsEn = settings.GetValueOrDefault("PackageAgreementTermsEn", PackageAgreement.DefaultTermsEn)
            });
        }

        // ─── عرض/طباعة/تحميل اتفاقية باقة موقّعة ────────────────────────
        public async Task<IActionResult> PrintAgreement(int id)
        {
            var agreement = await _context.PackageAgreements
                .Include(a => a.Sale)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (agreement == null) return NotFound();

            await LoadSalonSettings();
            return View(agreement);
        }

        // ─── إرسال الاتفاقية عبر البريد الإلكتروني ──────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SendAgreementEmail(int id, string? email)
        {
            var agreement = await _context.PackageAgreements.FirstOrDefaultAsync(a => a.Id == id);
            if (agreement == null) return Json(new { success = false, message = "الاتفاقية غير موجودة" });

            var toEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                var customer = await _context.Customers.FindAsync(agreement.CustomerId);
                toEmail = customer?.Email;
            }
            if (string.IsNullOrWhiteSpace(toEmail))
                return Json(new { success = false, message = "لا يوجد بريد إلكتروني مسجّل لهذا العميل" });

            var printUrl = Url.Action("PrintAgreement", "Packages", new { id = agreement.Id }, Request.Scheme);
            try
            {
                await _emailService.SendPackageAgreementEmailAsync(agreement, toEmail!, printUrl ?? "");
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "تعذر إرسال البريد الإلكتروني: " + ex.Message });
            }
            await _audit.LogAsync("Send", "Packages", $"Package agreement #{agreement.Id} emailed to {toEmail}", agreement.Id);
            return Json(new { success = true });
        }

        private async Task<string> GeneratePackageInvoiceNumber()
        {
            const string prefix = "PKG";
            var allNumbers = await _context.Sales
                .Where(s => s.InvoiceNumber.StartsWith(prefix + "-"))
                .Select(s => s.InvoiceNumber)
                .ToListAsync();

            int maxSeq = 0;
            foreach (var inv in allNumbers)
            {
                var parts = inv.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out var n))
                    if (n > maxSeq) maxSeq = n;
            }
            return $"{prefix}-{(maxSeq + 1):D4}";
        }

        private async Task LoadSalonSettings()
        {
            var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
            ViewBag.SalonName = settings.GetValueOrDefault("SalonName", "معهد موس");
            ViewBag.SalonNameEn = settings.GetValueOrDefault("SalonNameEn", "Mos Institute");
            ViewBag.SalonPhone = settings.GetValueOrDefault("SalonPhone", "");
            ViewBag.SalonAddress = settings.GetValueOrDefault("SalonAddress", "");
        }

        // ─── حذف باقة غير مستخدمة وغير مدفوعة ───────────────────────
        // مدير/أدمن فقط يقدر يحذف باقة تم استخدام جلسات منها (forceDeleteUsed) — بيتم حذف سجل
        // استخدام الجلسات المرتبط بيها كمان. الباقة المدفوعة (PricePaid > 0) ما بتتحذفش أبداً من
        // هنا لأن لها فاتورة/تحصيل فعلي، لازم تُلغى عبر إلغاء الاشتراك مش الحذف.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomerPackage(int id, bool forceDeleteUsed = false)
        {
            var cp = await _context.CustomerPackages
                .Include(x => x.ServicePackage)
                .Include(x => x.Transactions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cp == null)
                return Json(new { success = false, error = "الباقة غير موجودة" });

            if (cp.PricePaid > 0)
                return Json(new { success = false, error = "لا يمكن حذف باقة تم دفعها" });

            bool isUsed = cp.RemainingSessions != cp.TotalSessions;
            if (isUsed)
            {
                bool isAdminOrManager = User.IsInRole("Admin") || User.IsInRole("Manager");
                if (!isAdminOrManager)
                    return Json(new { success = false, error = "لا يمكن حذف باقة تم استخدامها" });

                if (!forceDeleteUsed)
                    return Json(new
                    {
                        success = false,
                        requiresConfirmation = true,
                        error = "تم استخدام جلسات من هذه الباقة، وسيتم حذف سجل استخدام الجلسات المرتبط بها نهائياً — أعد المحاولة مع التأكيد"
                    });

                _context.CustomerPackageTransactions.RemoveRange(cp.Transactions);
            }

            _context.CustomerPackages.Remove(cp);
            await _context.SaveChangesAsync();
            var logNote = isUsed ? " (تم استخدام جلسات منها — حُذف سجل الاستخدام معها)" : "";
            await _audit.LogAsync("Delete", "Packages", $"Delete customer package ID {id}{logNote}: {cp.ServicePackage?.NameAr}", id);

            return Json(new { success = true });
        }

        // ─── حذف كامل لباقة مدفوعة مع فاتورتها واتفاقيتها — أدمن/مدير فقط ───
        // يُستخدم فقط لتصحيح خطأ تسجيل (باقة اتباعت غلط) قبل اعتماد يومية ذلك اليوم؛ بيمسح
        // الباقة وسجل استخدامها والاتفاقية الموقّعة والفاتورة (وبنودها) نهائياً من قاعدة
        // البيانات — عكس DeleteCustomerPackage اللي بيرفض أي باقة مدفوعة تماماً.
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteCustomerPackageAndSale(int id)
        {
            var cp = await _context.CustomerPackages
                .Include(x => x.ServicePackage)
                .Include(x => x.Transactions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cp == null)
                return Json(new { success = false, error = "الباقة غير موجودة" });

            var agreement = await _context.PackageAgreements.FirstOrDefaultAsync(a => a.CustomerPackageId == id);

            Sale? sale = null;
            if (agreement?.SaleId != null)
                sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .Include(s => s.Refunds)
                    .FirstOrDefaultAsync(s => s.Id == agreement.SaleId.Value);

            if (sale != null)
            {
                if (await _closure.IsDateLockedAsync(sale.SaleDate, sale.SaleType))
                    return Json(new { success = false, error = "لا يمكن حذف فاتورة تخص يومية معتمدة — استخدم صلاحية إعادة فتح اليومية أولاً" });

                if (sale.Refunds.Any())
                    return Json(new { success = false, error = "لا يمكن حذف هذه الباقة — الفاتورة المرتبطة بها لها مبالغ مستردة مسجّلة" });
            }

            _context.CustomerPackageTransactions.RemoveRange(cp.Transactions);
            if (agreement != null) _context.PackageAgreements.Remove(agreement);
            _context.CustomerPackages.Remove(cp);
            if (sale != null) _context.Sales.Remove(sale);
            await _context.SaveChangesAsync();

            var logNote = sale != null ? $" مع فاتورتها رقم {sale.InvoiceNumber}" : "";
            await _audit.LogAsync("Delete", "Packages", $"حذف كامل لباقة مدفوعة ID {id}{logNote}: {cp.ServicePackage?.NameAr}", id);

            return Json(new { success = true });
        }

        // ─── استخدام جلسة ────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UseSession(int customerPackageId, int? employeeId, string? notes)
        {
            var customerPkg = await _context.CustomerPackages
                .Include(cp => cp.ServicePackage)
                .FirstOrDefaultAsync(cp => cp.Id == customerPackageId);

            if (customerPkg == null)
            {
                TempData["Error"] = "Customer subscription not found";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            if (customerPkg.RemainingSessions <= 0)
            {
                TempData["Error"] = "No remaining sessions in this package";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            if (customerPkg.ExpiryDate.HasValue && customerPkg.ExpiryDate.Value < DateTime.Today)
            {
                TempData["Error"] = "This package has expired";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            var sessionValue = customerPkg.RemainingSessions > 0
                ? customerPkg.CurrentBalance / customerPkg.RemainingSessions
                : 0;
            customerPkg.CurrentBalance = Math.Max(0, customerPkg.CurrentBalance - sessionValue);
            customerPkg.RemainingSessions--;
            if (customerPkg.RemainingSessions == 0)
            {
                customerPkg.IsActive = false;
                customerPkg.CurrentBalance = 0;
            }

            var transaction = new CustomerPackageTransaction
            {
                CustomerPackageId = customerPackageId,
                UsedDate = DateTime.Now,
                EmployeeId = employeeId,
                Notes = notes
            };

            _context.CustomerPackageTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Use", "Packages", $"Session used: {customerPkg.ServicePackage?.NameAr} — Remaining: {customerPkg.RemainingSessions}", customerPackageId);
            TempData["Success"] = $"Session recorded created successfully. Remaining sessions: {customerPkg.RemainingSessions}";
            return RedirectToAction(nameof(Index), new { tab = "balances" });
        }

        // ─── إلغاء تنشيط اشتراك عميل ────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateCustomerPackage(int id)
        {
            var cp = await _context.CustomerPackages.FindAsync(id);
            if (cp != null)
            {
                cp.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Subscription deactivated";
            }
            return RedirectToAction(nameof(Index), new { tab = "balances" });
        }

        // ─── API: جلب تفاصيل باقة ────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetPackageDetails(int id)
        {
            var pkg = await _context.ServicePackages.FindAsync(id);
            if (pkg == null) return NotFound();
            return Json(new
            {
                sessionCount = pkg.SessionCount,
                price = pkg.Price,
                validityDays = pkg.ValidityDays
            });
        }

        // ─── API: جلب باقات عميل النشطة ──────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCustomerActivePackages(int customerId)
        {
            var pkgs = await _context.CustomerPackages
                .Include(cp => cp.ServicePackage)
                .Where(cp => cp.CustomerId == customerId && cp.IsActive && cp.RemainingSessions > 0)
                .Select(cp => new
                {
                    id = cp.Id,
                    name = cp.ServicePackage!.NameAr,
                    remaining = cp.RemainingSessions,
                    expiry = cp.ExpiryDate.HasValue ? cp.ExpiryDate.Value.ToString("yyyy-MM-dd") : "-"
                })
                .ToListAsync();
            return Json(pkgs);
        }

        private async Task PopulateCategories(int? selectedId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            var catQuery = _context.ServiceCategories.Where(c => c.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                catQuery = catQuery.Where(c => c.Department == userDept);
            ViewBag.Categories = new SelectList(
                await catQuery.OrderBy(c => c.Name).ToListAsync(),
                "Id", "Name", selectedId);
        }
    }
}