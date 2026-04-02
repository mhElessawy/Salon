using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? date, string? type)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var query = _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= filterDate && s.SaleDate < nextDay);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(s => s.SaleType == type);

            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.FilterType = type;
            ViewBag.TotalSales = sales.Sum(s => s.NetAmount);
            return View(sales);
        }

        // ===== فاتورة خدمات =====
        public async Task<IActionResult> CreateService()
        {
            await PopulateServiceDropdowns();
            var sale = new Sale
            {
                InvoiceNumber = $"SRV-{DateTime.Now:yyyyMMddHHmmss}",
                SaleDate = DateTime.Now,
                SaleType = "خدمة"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateService")]
        public async Task<IActionResult> CreateServicePost(
            Sale model,
            string[]? itemTypes,
            int[]? itemIds,
            string[]? itemNames,
            decimal[]? itemPrices,
            int[]? itemQtys)
        {
            model.SaleType = "خدمة";
            if (ModelState.IsValid)
            {
                model.TotalAmount = 0;
                model.SaleDate = DateTime.Now;

                _context.Sales.Add(model);
                await _context.SaveChangesAsync();

                if (itemNames != null && itemNames.Length > 0)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (string.IsNullOrEmpty(itemNames[i])) continue;
                        var qty   = itemQtys  != null && i < itemQtys.Length   ? itemQtys[i]   : 1;
                        var price = itemPrices != null && i < itemPrices.Length ? itemPrices[i] : 0;
                        var id    = itemIds    != null && i < itemIds.Length    ? itemIds[i]    : 0;

                        var item = new SaleItem
                        {
                            SaleId   = model.Id,
                            ItemName = itemNames[i],
                            Quantity = qty,
                            Price    = price,
                            Total    = qty * price
                        };
                        if (id > 0) item.ServiceId = id;

                        _context.SaleItems.Add(item);
                        model.TotalAmount += item.Total;
                    }
                }

                model.NetAmount = model.TotalAmount - model.Discount;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"تم إنشاء فاتورة الخدمات {model.InvoiceNumber} بنجاح";
                return RedirectToAction(nameof(Index), new { type = "خدمة" });
            }
            await PopulateServiceDropdowns();
            return View(model);
        }

        // ===== فاتورة مبيعات منتجات =====
        public async Task<IActionResult> CreateProduct()
        {
            await PopulateProductDropdowns();
            var sale = new Sale
            {
                InvoiceNumber = $"PRD-{DateTime.Now:yyyyMMddHHmmss}",
                SaleDate = DateTime.Now,
                SaleType = "منتجات"
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [ActionName("CreateProduct")]
        public async Task<IActionResult> CreateProductPost(
            Sale model,
            int[]? itemIds,
            string[]? itemNames,
            decimal[]? itemPrices,
            int[]? itemQtys)
        {
            model.SaleType = "منتجات";
            if (ModelState.IsValid)
            {
                model.TotalAmount = 0;
                model.SaleDate = DateTime.Now;

                _context.Sales.Add(model);
                await _context.SaveChangesAsync();

                if (itemNames != null && itemNames.Length > 0)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (string.IsNullOrEmpty(itemNames[i])) continue;
                        var qty   = itemQtys  != null && i < itemQtys.Length   ? itemQtys[i]   : 1;
                        var price = itemPrices != null && i < itemPrices.Length ? itemPrices[i] : 0;
                        var id    = itemIds    != null && i < itemIds.Length    ? itemIds[i]    : 0;

                        var item = new SaleItem
                        {
                            SaleId   = model.Id,
                            ItemName = itemNames[i],
                            Quantity = qty,
                            Price    = price,
                            Total    = qty * price
                        };

                        if (id > 0)
                        {
                            item.ProductId = id;
                            var product = await _context.Products.FindAsync(id);
                            if (product != null)
                                product.StockQuantity = Math.Max(0, product.StockQuantity - qty);
                        }

                        _context.SaleItems.Add(item);
                        model.TotalAmount += item.Total;
                    }
                }

                model.NetAmount = model.TotalAmount - model.Discount;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"تم إنشاء فاتورة المنتجات {model.InvoiceNumber} بنجاح";
                return RedirectToAction(nameof(Index), new { type = "منتجات" });
            }
            await PopulateProductDropdowns();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems).ThenInclude(i => i.Service)
                .Include(s => s.SaleItems).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale != null)
            {
                // استعادة المخزون عند الإلغاء
                foreach (var item in sale.SaleItems.Where(i => i.ProductId != null))
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                        product.StockQuantity += item.Quantity;
                }
                sale.Status = "ملغي";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إلغاء الفاتورة وتمت استعادة المخزون";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateServiceDropdowns()
        {
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.ServiceCategories = await _context.ServiceCategories
                .Include(c => c.Services.Where(s => s.IsActive))
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
            ViewBag.UncategorizedServices = await _context.Services
                .Where(s => s.IsActive && s.ServiceCategoryId == null)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        private async Task PopulateProductDropdowns()
        {
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
    }
}
