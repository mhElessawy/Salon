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
        public async Task<IActionResult> Create(Sale model, string[]? itemNames, decimal[]? itemPrices, int[]? itemQtys)
        {
            if (ModelState.IsValid)
            {
                model.TotalAmount = 0;
                model.SaleDate = DateTime.Now;

                _context.Sales.Add(model);
                await _context.SaveChangesAsync();

                if (itemNames != null)
                {
                    for (int i = 0; i < itemNames.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(itemNames[i]))
                        {
                            var qty = itemQtys != null && i < itemQtys.Length ? itemQtys[i] : 1;
                            var price = itemPrices != null && i < itemPrices.Length ? itemPrices[i] : 0;
                            var item = new SaleItem
                            {
                                SaleId = model.Id,
                                ItemName = itemNames[i],
                                Quantity = qty,
                                Price = price,
                                Total = qty * price
                            };
                            _context.SaleItems.Add(item);
                            model.TotalAmount += item.Total;
                        }
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
                .Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _context.Sales.FindAsync(id);
            if (sale != null)
            {
                sale.Status = "ملغي";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إلغاء الفاتورة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Customers = new SelectList(await _context.Customers.Where(c => c.IsActive).ToListAsync(), "Id", "FullName");
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
            ViewBag.Products = await _context.Products.Where(p => p.IsActive && p.StockQuantity > 0).ToListAsync();
        }
    }
}
