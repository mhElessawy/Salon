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

            // Employee role: show only own records
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
                // Employee sees only themselves — no dropdown
                var employee = await _context.Employees.FindAsync(linkedId.Value);
                if (employee == null) return Forbid();
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

            // Employee role: force EmployeeId to their own, ignore any submitted value
            if (linkedId.HasValue)
                model.EmployeeId = linkedId.Value;

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

            // Employee role: only allowed to check out their own record
            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && record.EmployeeId != linkedId.Value)
                return Forbid();

            if (TimeSpan.TryParse(checkOutTime, out var time))
            {
                record.CheckOut = time;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم تسجيل الانصراف بنجاح";
            }
            return RedirectToAction(nameof(Index), new { date = record.AttendanceDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record != null)
            {
                // Employee role: only allowed to delete their own record
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

            // Employee role: show only own records in reports
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
