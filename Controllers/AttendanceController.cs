using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AttendanceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Returns the LinkedEmployeeId if the current user is in Employee role, otherwise null.
        private async Task<int?> GetLinkedEmployeeIdIfEmployee()
        {
            if (!User.IsInRole("Employee")) return null;
            var user = await _userManager.GetUserAsync(User);
            return user?.LinkedEmployeeId;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);

            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            var query = _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.AttendanceDate == filterDate);

            if (linkedId.HasValue)
                query = query.Where(a => a.EmployeeId == linkedId.Value);

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            if (linkedId.HasValue)
            {
                var employee = await _context.Employees.FindAsync(linkedId.Value);
                if (employee == null) return Forbid();

                // Check if employee already has a record today
                bool alreadyExists = await _context.Attendances.AnyAsync(a =>
                    a.EmployeeId == linkedId.Value && a.AttendanceDate == DateTime.Today);
                if (alreadyExists)
                {
                    TempData["Error"] = "لقد سجلت حضورك اليوم مسبقاً";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.IsEmployee = true;
                ViewBag.EmployeeName = employee.FullName;
                return View(new Attendance { AttendanceDate = DateTime.Today, EmployeeId = linkedId.Value });
            }

            ViewBag.IsEmployee = false;
            ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            return View(new Attendance { AttendanceDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Attendance model)
        {
            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            if (linkedId.HasValue)
            {
                // Force employee's own ID and current time — ignore any submitted values
                model.EmployeeId = linkedId.Value;
                model.CheckIn = DateTime.Now.TimeOfDay;
                model.CheckOut = null;
                model.AttendanceDate = DateTime.Today;
            }

            // Prevent duplicate record for the same employee on the same date (all roles)
            bool duplicate = await _context.Attendances.AnyAsync(a =>
                a.EmployeeId == model.EmployeeId && a.AttendanceDate == model.AttendanceDate);
            if (duplicate)
            {
                ModelState.AddModelError(string.Empty, "يوجد سجل حضور لهذا الموظف في هذا اليوم مسبقاً");
            }

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Attendances.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تسجيل الحضور بنجاح";
                return RedirectToAction(nameof(Index));
            }

            if (linkedId.HasValue)
            {
                var employee = await _context.Employees.FindAsync(linkedId.Value);
                ViewBag.IsEmployee = true;
                ViewBag.EmployeeName = employee?.FullName;
            }
            else
            {
                ViewBag.IsEmployee = false;
                ViewBag.Employees = new SelectList(await _context.Employees.Where(e => e.IsActive).ToListAsync(), "Id", "FullName");
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int id, string checkOutTime)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record == null) return RedirectToAction(nameof(Index));

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && record.EmployeeId != linkedId.Value)
                return Forbid();

            // Employee role: always use current time, ignore submitted value
            var time = linkedId.HasValue
                ? DateTime.Now.TimeOfDay
                : (TimeSpan.TryParse(checkOutTime, out var parsed) ? parsed : DateTime.Now.TimeOfDay);

            record.CheckOut = time;
            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تسجيل الانصراف بنجاح";
            return RedirectToAction(nameof(Index), new { date = record.AttendanceDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record != null)
            {
                var linkedId = await GetLinkedEmployeeIdIfEmployee();
                if (linkedId.HasValue && record.EmployeeId != linkedId.Value)
                    return Forbid();

                _context.Attendances.Remove(record);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف سجل الحضور بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reports(string? month)
        {
            DateTime filterMonth = string.IsNullOrEmpty(month)
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Parse(month + "-01");

            var nextMonth = filterMonth.AddMonths(1);

            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            var query = _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.AttendanceDate >= filterMonth && a.AttendanceDate < nextMonth);

            if (linkedId.HasValue)
                query = query.Where(a => a.EmployeeId == linkedId.Value);

            var records = await query
                .OrderBy(a => a.Employee!.FullName)
                .ThenBy(a => a.AttendanceDate)
                .ToListAsync();

            ViewBag.FilterMonth = filterMonth.ToString("yyyy-MM");
            return View(records);
        }
    }
}
