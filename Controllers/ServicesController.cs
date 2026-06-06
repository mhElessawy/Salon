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
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public ServicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? search, string? filter)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Services
                .Include(s => s.ServiceCategory)
                .Include(s => s.SaleItems).ThenInclude(si => si.Sale)
                .Where(s => s.IsActive);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(s => s.ServiceCategory!.Department == userDept);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.Name.Contains(search));

            if (!string.IsNullOrEmpty(filter) && filter != "all")
                query = query.Where(s => s.ServiceCategory != null && s.ServiceCategory.Department == filter);

            var services = await query.ToListAsync();
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            var invoiceCounts = new Dictionary<int, int>();
            var totalSalesDict = new Dictionary<int, decimal>();
            var last30Invoices = new Dictionary<int, int>();
            var last30SalesDict = new Dictionary<int, decimal>();

            foreach (var s in services)
            {
                var items = s.SaleItems.ToList();
                var last30 = items.Where(si => si.Sale?.SaleDate >= thirtyDaysAgo).ToList();
                invoiceCounts[s.Id] = items.Select(si => si.SaleId).Distinct().Count();
                totalSalesDict[s.Id] = items.Sum(si => si.Total);
                last30Invoices[s.Id] = last30.Select(si => si.SaleId).Distinct().Count();
                last30SalesDict[s.Id] = last30.Sum(si => si.Total);
            }

            var sorted = services
                .OrderByDescending(s => last30SalesDict[s.Id])
                .ThenByDescending(s => totalSalesDict[s.Id])
                .ThenBy(s => s.Name)
                .ToList();

            var rankDict = sorted.Select((s, i) => new { s.Id, Rank = i + 1 })
                                 .ToDictionary(x => x.Id, x => x.Rank);

            var maxLast30 = last30SalesDict.Values.DefaultIfEmpty(0).Max();
            var statusTextDict = new Dictionary<int, string>();
            var statusClassDict = new Dictionary<int, string>();
            foreach (var s in services)
            {
                var v = last30SalesDict[s.Id];
                if (v == 0) { statusTextDict[s.Id] = "متوقفة"; statusClassDict[s.Id] = "svc-status-stopped"; }
                else if (maxLast30 > 0 && v >= maxLast30 * 0.7m) { statusTextDict[s.Id] = "ممتازة"; statusClassDict[s.Id] = "svc-status-excellent"; }
                else if (maxLast30 > 0 && v >= maxLast30 * 0.3m) { statusTextDict[s.Id] = "جيدة"; statusClassDict[s.Id] = "svc-status-good"; }
                else { statusTextDict[s.Id] = "تحتاج تنشيط"; statusClassDict[s.Id] = "svc-status-needs"; }
            }

            ViewBag.Search = search;
            ViewBag.Filter = filter ?? "all";
            ViewBag.InvoiceCounts = invoiceCounts;
            ViewBag.TotalSales = totalSalesDict;
            ViewBag.Last30Invoices = last30Invoices;
            ViewBag.Last30Sales = last30SalesDict;
            ViewBag.Ranks = rankDict;
            ViewBag.StatusText = statusTextDict;
            ViewBag.StatusClass = statusClassDict;
            return View(sorted);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateCategories();
            return View(new Service());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Service model)
        {
            if (ModelState.IsValid)
            {
                _context.Services.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Services", $"New service: {model.Name}", model.Id);
                TempData["Success"] = "Service added created successfully";
                return RedirectToAction(nameof(Index));
            }
            await PopulateCategories(model.ServiceCategoryId);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null) return NotFound();
            await PopulateCategories(service.ServiceCategoryId);
            return View(service);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Service model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Services", $"Edit service: {model.Name}", model.Id);
                TempData["Success"] = "Service updated created successfully";
                return RedirectToAction(nameof(Index));
            }
            await PopulateCategories(model.ServiceCategoryId);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.IsActive = false;
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Services", $"Delete service: {service.Name}", service.Id);
                TempData["Success"] = "Service deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
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