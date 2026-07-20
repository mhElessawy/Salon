using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public ShiftsController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            var shifts = await _context.Shifts
                .Where(s => !s.IsClosureRecord)
                .OrderByDescending(s => s.ShiftDate)
                .ThenByDescending(s => s.CreatedAt)
                .Take(50)
                .ToListAsync();
            return View(shifts);
        }

        public IActionResult Create() => View(new Shift
        {
            ShiftDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(DateTime.Now.Hour)
        });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shift model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.IsClosureRecord = false;
                _context.Shifts.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Open", "Shifts", $"Open shift: {model.ShiftDate:yyyy/MM/dd} {model.StartTime}", model.Id);
                TempData["Success"] = "Shift opened created successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, decimal closingBalance, string? notes)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null && !shift.IsClosureRecord)
            {
                shift.EndTime = TimeSpan.FromTicks(DateTime.Now.TimeOfDay.Ticks);
                shift.ClosingBalance = closingBalance;
                shift.Status = "مغلق";
                shift.Notes = notes;
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Close", "Shifts", $"Close shift: {shift.ShiftDate:yyyy/MM/dd} — Balance: {closingBalance:F3} KD", id);
                TempData["Success"] = "Shift closed created successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reports(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var shifts = await _context.Shifts
                .Where(s => !s.IsClosureRecord && s.ShiftDate >= filterDate && s.ShiftDate < nextDay)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            return View(shifts);
        }
    }
}