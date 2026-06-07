using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class WithdrawalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public WithdrawalsController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var withdrawals = await _context.Withdrawals
                .Where(w => w.WithdrawalDate >= filterDate && w.WithdrawalDate < nextDay)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.Total = withdrawals.Sum(w => w.Amount);
            return View(withdrawals);
        }

        public IActionResult Create()
        {
            return View(new Withdrawal { WithdrawalDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Withdrawal model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Withdrawals.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Withdrawals",
                    $"{model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "تم إضافة السحب بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var w = await _context.Withdrawals.FindAsync(id);
            if (w == null) return NotFound();
            return View(w);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Withdrawal model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Withdrawals",
                    $"{model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "تم تعديل السحب بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var w = await _context.Withdrawals.FindAsync(id);
            if (w != null)
            {
                var desc = $"{w.Description} - {w.Amount:F3} KD";
                _context.Withdrawals.Remove(w);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Withdrawals", desc, id);
                TempData["Success"] = "تم حذف السحب بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
