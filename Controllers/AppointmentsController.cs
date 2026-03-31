using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AppointmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
                .Where(a => a.AppointmentDate >= filterDate && a.AppointmentDate < nextDay)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            return View(appointments);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View(new Appointment { AppointmentDate = DateTime.Now });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model, int[]? serviceIds)
        {
            if (ModelState.IsValid)
            {
                _context.Appointments.Add(model);
                await _context.SaveChangesAsync();

                if (serviceIds != null)
                {
                    foreach (var sid in serviceIds)
                    {
                        _context.AppointmentServices.Add(new AppointmentService
                        {
                            AppointmentId = model.Id,
                            ServiceId = sid
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "تم إضافة الموعد بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns();
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var apt = await _context.Appointments
                .Include(a => a.AppointmentServices)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (apt == null) return NotFound();
            await PopulateDropdowns();
            return View(apt);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Appointment model, int[]? serviceIds)
        {
            if (id != model.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existingServices = _context.AppointmentServices.Where(a => a.AppointmentId == id);
                _context.AppointmentServices.RemoveRange(existingServices);

                if (serviceIds != null)
                {
                    foreach (var sid in serviceIds)
                    {
                        _context.AppointmentServices.Add(new AppointmentService
                        {
                            AppointmentId = id,
                            ServiceId = sid
                        });
                    }
                }

                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تعديل الموعد بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdowns();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var apt = await _context.Appointments.FindAsync(id);
            if (apt != null)
            {
                _context.Appointments.Remove(apt);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الموعد بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.Customers = new SelectList(await _context.Customers.Where(c => c.IsActive).ToListAsync(), "Id", "FullName");
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            ViewBag.Services = await _context.Services.Where(s => s.IsActive).ToListAsync();
        }
    }
}
