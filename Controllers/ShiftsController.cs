using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShiftsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var shifts = await _context.Shifts
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
                _context.Shifts.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم فتح الشفت بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, decimal closingBalance, string? notes)
        {
            var shift = await _context.Shifts.FindAsync(id);
            if (shift != null)
            {
                shift.EndTime = TimeSpan.FromTicks(DateTime.Now.TimeOfDay.Ticks);
                shift.ClosingBalance = closingBalance;
                shift.Status = "مغلق";
                shift.Notes = notes;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إغلاق الشفت بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reports(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var shifts = await _context.Shifts
                .Where(s => s.ShiftDate >= filterDate && s.ShiftDate < nextDay)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            return View(shifts);
        }
    }
}
