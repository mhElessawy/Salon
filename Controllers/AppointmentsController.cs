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

                await _audit.LogAsync("Add", "Appointments", $"New appointment on {model.AppointmentDate:yyyy/MM/dd HH:mm}", model.Id);
                TempData["Success"] = "Appointment added created successfully";
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
                await _audit.LogAsync("Edit", "Appointments", $"Edit appointment ID {model.Id}", model.Id);
                TempData["Success"] = "Appointment updated created successfully";
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
                await _audit.LogAsync("Delete", "Appointments", $"Delete appointment on {apt.AppointmentDate:yyyy/MM/dd HH:mm}", id);
                TempData["Success"] = "Appointment deleted created successfully";
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

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkCreate([FromBody] List<BulkAppointmentItem> items)
        {
            if (items == null || !items.Any())
                return Json(new { success = false, message = "لا توجد مواعيد" });

            int savedCount = 0;
            foreach (var item in items)
            {
                if (item.CustomerId <= 0) continue;
                _context.Appointments.Add(new Appointment
                {
                    CustomerId = item.CustomerId,
                    EmployeeId = item.EmployeeId.HasValue && item.EmployeeId.Value > 0 ? item.EmployeeId : null,
                    AppointmentDate = item.AppointmentDate,
                    Status = "مجدول",
                    Notes = item.Notes
                });
                savedCount++;
            }
            if (savedCount > 0)
            {
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Appointments", $"تحديد {savedCount} مواعيد دفعة واحدة", 0);
            }
            return Json(new { success = true, count = savedCount });
        }
    }

    public class BulkAppointmentItem
    {
        public int CustomerId { get; set; }
        public int? EmployeeId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string? Notes { get; set; }
    }
}