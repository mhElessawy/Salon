using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;
using Salon.Services;

namespace Salon.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _audit;

        public AppointmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
                .Where(a => a.AppointmentDate >= filterDate && a.AppointmentDate < nextDay);

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var appointments = await query.OrderBy(a => a.AppointmentDate).ToListAsync();

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

                await _audit.LogAsync("إضافة", "المواعيد", $"موعد جديد بتاريخ {model.AppointmentDate:yyyy/MM/dd HH:mm}", model.Id);
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
                await _audit.LogAsync("تعديل", "المواعيد", $"تعديل موعد رقم {model.Id}", model.Id);
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
                await _audit.LogAsync("حذف", "المواعيد", $"حذف موعد بتاريخ {apt.AppointmentDate:yyyy/MM/dd HH:mm}", id);
                TempData["Success"] = "تم حذف الموعد بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            ViewBag.Customers = new SelectList(await _context.Customers.Where(c => c.IsActive).ToListAsync(), "Id", "FullName");

            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            ViewBag.Employees = new SelectList(await empQuery.OrderBy(e => e.FullName).ToListAsync(), "Id", "FullName");

            var svcQuery = _context.Services.Include(s => s.ServiceCategory).Where(s => s.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                svcQuery = svcQuery.Where(s => s.ServiceCategory!.Department == userDept);
            ViewBag.Services = await svcQuery.ToListAsync();
        }
    }
}