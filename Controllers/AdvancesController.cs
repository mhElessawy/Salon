using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class AdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public AdvancesController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
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

                var emp = await _context.Employees.FindAsync(model.EmployeeId);
                await _audit.LogAsync("إضافة", "السلف",
                    $"إضافة سلفة للموظف: {emp?.FullName ?? model.EmployeeId.ToString()} بمبلغ {model.Amount:N3} د.ك",
                    model.Id);

                TempData["Success"] = "تم إضافة السلفة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance != null)
            {
                advance.Status = "موافق عليها";
                await _context.SaveChangesAsync();

                await _audit.LogAsync("موافقة", "السلف",
                    $"الموافقة على سلفة الموظف: {advance.Employee?.FullName ?? advance.EmployeeId.ToString()} بمبلغ {advance.Amount:N3} د.ك",
                    advance.Id);

                TempData["Success"] = "تم الموافقة على السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var advance = await _context.EmployeeAdvances.Include(a => a.Employee).FirstOrDefaultAsync(a => a.Id == id);
            if (advance != null)
            {
                string empName = advance.Employee?.FullName ?? advance.EmployeeId.ToString();
                decimal amount = advance.Amount;

                _context.EmployeeAdvances.Remove(advance);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("حذف", "السلف",
                    $"حذف سلفة الموظف: {empName} بمبلغ {amount:N3} د.ك",
                    id);

                TempData["Success"] = "تم حذف السلفة بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
