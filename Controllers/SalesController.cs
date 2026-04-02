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

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var sales = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.SaleItems)
                .Where(s => s.SaleDate >= filterDate && s.SaleDate < nextDay)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.TotalSales = sales.Sum(s => s.NetAmount);
            return View(sales);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            var sale = new Sale
            {
                InvoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}",
                SaleDate = DateTime.Now
            };
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Sale model,
            string[]? itemTypes,
            int[]? itemIds,
            string[]? itemNames,
            decimal[]? itemPrices,
            int[]? itemQtys)
        {
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

                        var qty   = itemQtys  != null && i < itemQtys.Length  ? itemQtys[i]  : 1;
                        var price = itemPrices != null && i < itemPrices.Length ? itemPrices[i] : 0;
                        var type  = itemTypes  != null && i < itemTypes.Length  ? itemTypes[i]  : "";
                        var id    = itemIds    != null && i < itemIds.Length    ? itemIds[i]    : 0;

                        var item = new SaleItem
                        {
                            SaleId   = model.Id,
                            ItemName = itemNames[i],
                            Quantity = qty,
                            Price    = price,
                            Total    = qty * price
                        };

                        // ربط الخدمة أو المنتج
                        if (type == "service" && id > 0)
                            item.ServiceId = id;

                        if (type == "product" && id > 0)
                        {
                            item.ProductId = id;
                            // خصم الكمية من المخزون
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

                TempData["Success"] = $"تم إنشاء الفاتورة {model.InvoiceNumber} بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns();
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

        private async Task PopulateDropdowns()
        {
            ViewBag.Customers = new SelectList(
                await _context.Customers.Where(c => c.IsActive).OrderBy(c => c.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
            ViewBag.Services = await _context.Services
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category).ThenBy(s => s.Name)
                .ToListAsync();
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.StockQuantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
    }
}
