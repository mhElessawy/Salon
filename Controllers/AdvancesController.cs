using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class AdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdvancesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Report(int? employeeId, DateTime? dateFrom, DateTime? dateTo, string? status)
        {
            dateFrom ??= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dateTo   ??= DateTime.Today;

            var query = _context.EmployeeAdvances.Include(a => a.Employee).AsQueryable();

            query = query.Where(a => a.AdvanceDate >= dateFrom && a.AdvanceDate <= dateTo);

            if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            var advances = await query.OrderBy(a => a.Employee!.FullName).ThenBy(a => a.AdvanceDate).ToListAsync();

            ViewBag.DateFrom    = dateFrom.Value.ToString("yyyy-MM-dd");
            ViewBag.DateTo      = dateTo.Value.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId  = employeeId;
            ViewBag.Status      = status;
            ViewBag.Employees   = new SelectList(
                await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");

            return View(advances);
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.EmployeeAdvances.Include(a => a.Employee).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.Employee != null && a.Employee.FullName.Contains(search));

            var advances = await query.OrderByDescending(a => a.AdvanceDate).ToListAsync();
            ViewBag.Search = search;
            return View(advances);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(new EmployeeAdvance { AdvanceDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeAdvance model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.EmployeeAdvances.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة السلفة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var advance = await _context.EmployeeAdvances.FindAsync(id);
            if (advance != null)
            {
                advance.Status = "موافق عليها";
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم الموافقة على السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> PayDirect(int id)
        {
            var advance = await _context.EmployeeAdvances
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (advance == null) return NotFound();
            return View(advance);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PayDirect(int id, decimal payAmount)
        {
            var advance = await _context.EmployeeAdvances.FindAsync(id);
            if (advance == null) return NotFound();

            decimal maxPayable = advance.Amount - advance.AmountPaid;
            payAmount = Math.Min(payAmount, maxPayable);

            advance.AmountPaid += payAmount;
            advance.PaidDate = DateTime.Today;

            if (advance.AmountPaid >= advance.Amount)
                advance.Status = "مسددة";
            else
                advance.Status = "مدفوعة جزئياً";

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم تسجيل دفعة {payAmount:N3} د.ك — المتبقي: {advance.Amount - advance.AmountPaid:N3} د.ك";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var advance = await _context.EmployeeAdvances.FindAsync(id);
            if (advance != null)
            {
                _context.EmployeeAdvances.Remove(advance);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
