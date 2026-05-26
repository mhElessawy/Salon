using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class PackagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PackagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                TempData["Success"] = "تم إضافة الباقة بنجاح";
                return RedirectToAction(nameof(Index), new { tab = "packages" });
            }
            TempData["Error"] = "يرجى مراجعة البيانات المدخلة";
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
                TempData["Success"] = "تم تعديل الباقة بنجاح";
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
                TempData["Success"] = "تم حذف الباقة بنجاح";
            }
            return RedirectToAction(nameof(Index), new { tab = "packages" });
        }

        // ─── تعيين باقة لعميل ─────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPackage(int customerId, int servicePackageId, decimal pricePaid, string? notes)
        {
            var pkg = await _context.ServicePackages.FindAsync(servicePackageId);
            if (pkg == null)
            {
                TempData["Error"] = "الباقة غير موجودة";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            var customerPkg = new CustomerPackage
            {
                CustomerId = customerId,
                ServicePackageId = servicePackageId,
                PurchaseDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddDays(pkg.ValidityDays),
                TotalSessions = pkg.SessionCount,
                RemainingSessions = pkg.SessionCount,
                PricePaid = pricePaid > 0 ? pricePaid : pkg.Price,
                Notes = notes,
                IsActive = true
            };

            _context.CustomerPackages.Add(customerPkg);
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تعيين الباقة للعميل بنجاح";

            // إذا كان الطلب AJAX ارجع JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, customerPackageId = customerPkg.Id });

            return RedirectToAction(nameof(Index), new { tab = "balances" });
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
                TempData["Error"] = "اشتراك العميل غير موجود";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            if (customerPkg.RemainingSessions <= 0)
            {
                TempData["Error"] = "لا توجد جلسات متبقية في هذه الباقة";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            if (customerPkg.ExpiryDate.HasValue && customerPkg.ExpiryDate.Value < DateTime.Today)
            {
                TempData["Error"] = "انتهت صلاحية هذه الباقة";
                return RedirectToAction(nameof(Index), new { tab = "balances" });
            }

            customerPkg.RemainingSessions--;
            if (customerPkg.RemainingSessions == 0)
                customerPkg.IsActive = false;

            var transaction = new CustomerPackageTransaction
            {
                CustomerPackageId = customerPackageId,
                UsedDate = DateTime.Now,
                EmployeeId = employeeId,
                Notes = notes
            };

            _context.CustomerPackageTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم تسجيل الجلسة بنجاح. الجلسات المتبقية: {customerPkg.RemainingSessions}";
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
                TempData["Success"] = "تم إلغاء تنشيط الاشتراك";
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