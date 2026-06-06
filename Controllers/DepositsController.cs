using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class DepositsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public DepositsController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var deposits = await _context.Deposits
                .Where(d => d.DepositDate >= filterDate && d.DepositDate < nextDay)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.Total = deposits.Sum(d => d.Amount);
            return View(deposits);
        }

        public IActionResult Create()
        {
            return View(new Deposit { DepositDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Deposit model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Deposits.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Deposits",
                    $"{model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "Deposit added created successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var deposit = await _context.Deposits.FindAsync(id);
            if (deposit == null) return NotFound();
            return View(deposit);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Deposit model)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Deposits",
                    $"{model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "Deposit updated created successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deposit = await _context.Deposits.FindAsync(id);
            if (deposit != null)
            {
                var desc = $"{deposit.Description} - {deposit.Amount:F3} KD";
                _context.Deposits.Remove(deposit);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Deposits", desc, id);
                TempData["Success"] = "Deposit deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}