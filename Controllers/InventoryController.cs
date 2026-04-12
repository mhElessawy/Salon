using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===== قائمة المنتجات =====
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Products
                .Include(p => p.Supplier)
                .Where(p => p.IsActive);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.Contains(search));

            var products = await query.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Search = search;
            return View(products);
        }

        // ===== إضافة منتج =====
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name");
            return View(new Product());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            if (ModelState.IsValid)
            {
                model.StockQuantity = model.OpeningQuantity;
                model.CreatedAt = DateTime.Now;
                _context.Products.Add(model);
                await _context.SaveChangesAsync();

                if (model.OpeningQuantity > 0)
                {
                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = model.Id,
                        MovementType = "استلام",
                        Quantity = model.OpeningQuantity,
                        UnitPrice = model.PurchasePrice,
                        SupplierId = model.SupplierId,
                        Notes = "كمية افتتاحية",
                        MovementDate = DateTime.Today,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "تم إضافة المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name");
            return View(model);
        }

        // ===== تعديل منتج =====
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name", product.SupplierId);
            return View(product);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل المنتج بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name", model.SupplierId);
            return View(model);
        }

        // ===== إضافة كمية / إهلاك =====
        public async Task<IActionResult> StockAdjust()
        {
            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name");
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name");
            return View(new StockMovement { MovementDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> StockAdjust(StockMovement model)
        {
            if (model.ProductId == 0)
                ModelState.AddModelError("ProductId", "اختر المنتج");

            var product = model.ProductId > 0 ? await _context.Products.FindAsync(model.ProductId) : null;

            if (ModelState.IsValid && product != null)
            {
                if (model.MovementType == "إهلاك" && model.Quantity > product.StockQuantity)
                {
                    ModelState.AddModelError("Quantity", $"الكمية المتاحة فقط: {product.StockQuantity}");
                }
                else
                {
                    product.StockQuantity += model.MovementType == "استلام" ? model.Quantity : -model.Quantity;
                    model.CreatedAt = DateTime.Now;
                    _context.StockMovements.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = model.MovementType == "استلام"
                        ? $"تم إضافة {model.Quantity} للمخزون"
                        : $"تم تسجيل إهلاك {model.Quantity}";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", model.ProductId);
            ViewBag.Suppliers = new SelectList(
                await _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
                "Id", "Name", model.SupplierId);
            return View(model);
        }

        // ===== بيع منتج =====
        public async Task<IActionResult> SellProduct()
        {
            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive && p.StockQuantity > 0).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name");
            return View(new StockMovement { MovementType = "بيع", MovementDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SellProduct(StockMovement model)
        {
            model.MovementType = "بيع";
            var product = model.ProductId > 0 ? await _context.Products.FindAsync(model.ProductId) : null;

            if (model.ProductId == 0)
                ModelState.AddModelError("ProductId", "اختر المنتج");

            if (ModelState.IsValid && product != null)
            {
                if (model.Quantity > product.StockQuantity)
                {
                    ModelState.AddModelError("Quantity", $"الكمية المتاحة فقط: {product.StockQuantity}");
                }
                else
                {
                    if (model.UnitPrice == 0) model.UnitPrice = product.SalePrice;
                    product.StockQuantity -= model.Quantity;
                    model.CreatedAt = DateTime.Now;
                    _context.StockMovements.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"تم بيع {model.Quantity} من {product.Name} — الإجمالي: {(model.UnitPrice * model.Quantity):N3} د.ك";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive && p.StockQuantity > 0).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", model.ProductId);
            return View(model);
        }

        // ===== استهلاك (موظف) =====
        public async Task<IActionResult> Consume()
        {
            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive && p.StockQuantity > 0).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name");
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
            return View(new StockMovement { MovementType = "استهلاك", MovementDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Consume(StockMovement model)
        {
            model.MovementType = "استهلاك";
            var product = model.ProductId > 0 ? await _context.Products.FindAsync(model.ProductId) : null;

            if (model.ProductId == 0)
                ModelState.AddModelError("ProductId", "اختر المنتج");
            if (!model.EmployeeId.HasValue)
                ModelState.AddModelError("EmployeeId", "اختر الموظف");

            if (ModelState.IsValid && product != null)
            {
                if (model.Quantity > product.StockQuantity)
                {
                    ModelState.AddModelError("Quantity", $"الكمية المتاحة فقط: {product.StockQuantity}");
                }
                else
                {
                    product.StockQuantity -= model.Quantity;
                    model.CreatedAt = DateTime.Now;
                    _context.StockMovements.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"تم تسجيل استهلاك {model.Quantity} من {product.Name}";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive && p.StockQuantity > 0).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", model.ProductId);
            ViewBag.Employees = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName", model.EmployeeId);
            return View(model);
        }

        // ===== سجل الحركات =====
        public async Task<IActionResult> Movements(int? productId)
        {
            var query = _context.StockMovements
                .Include(m => m.Product)
                .Include(m => m.Employee)
                .Include(m => m.Supplier)
                .AsQueryable();

            if (productId.HasValue)
                query = query.Where(m => m.ProductId == productId.Value);

            var movements = await query.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id).ToListAsync();
            ViewBag.Products = new SelectList(
                await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(),
                "Id", "Name", productId);
            ViewBag.ProductId = productId;
            return View(movements);
        }

        // ===== حذف منتج =====
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف المنتج بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}