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
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IAuditService _audit;
        private readonly IPermissionService _perms;

        public AttendanceController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IAuditService audit,
            IPermissionService perms)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _audit = audit;
            _perms = perms;
        }

        private static readonly TimeZoneInfo KuwaitTz =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuwait");

        private static DateTime KuwaitNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, KuwaitTz);

        private static DateTime KuwaitToday => KuwaitNow.Date;

        private async Task<int?> GetLinkedEmployeeIdIfEmployee()
        {
            if (!User.IsInRole("Employee")) return null;
            var user = await _userManager.GetUserAsync(User);
            return user?.LinkedEmployeeId;
        }

        private async Task<string?> GetUserDepartmentAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.UserDepartment;
        }

        // Returns the next queue position for the given department today.
        private async Task<int> NextQueuePosition(string? dept)
        {
            if (string.IsNullOrEmpty(dept)) return 1;

            var today = KuwaitToday;
            var max = await (
                from a in _context.Attendances
                join e in _context.Employees on a.EmployeeId equals e.Id
                join d in _context.Departments on e.DepartmentId equals d.Id
                where a.AttendanceDate == today && a.QueuePosition != null && d.Name == dept
                select a.QueuePosition
            ).MaxAsync();

            return (max ?? 0) + 1;
        }

        public async Task<IActionResult> Index(string? date)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? KuwaitToday : DateTime.Parse(date);

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            var userDept = await GetUserDepartmentAsync();

            // ── 1. سجلات يوم الفلتر (GroupBy بدل ToDictionary لدعم ورديات متعددة) ──
            var attQuery = _context.Attendances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(a => a.Permissions)
                .Where(a => a.AttendanceDate == filterDate);

            if (linkedId.HasValue)
                attQuery = attQuery.Where(a => a.EmployeeId == linkedId.Value);
            else if (userDept == "حلاقة" || userDept == "مساج")
                attQuery = attQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var records = await attQuery.ToListAsync();

            // GroupBy يتعامل مع وردية + وردية جديدة لنفس الموظف
            var allTodayByEmpId = records
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(a => a.CheckIn).ToList());

            // ── 2. موظفو الليل: حضروا امبارح ولسه لم ينصرفوا ──────────
            var prevDate = filterDate.AddDays(-1);
            var overnightQuery = _context.Attendances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(a => a.Permissions)
                .Where(a => a.AttendanceDate == prevDate && a.CheckIn.HasValue && !a.CheckOut.HasValue);

            if (linkedId.HasValue)
                overnightQuery = overnightQuery.Where(a => a.EmployeeId == linkedId.Value);
            else if (userDept == "حلاقة" || userDept == "مساج")
                overnightQuery = overnightQuery.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var overnightByEmpId = (await overnightQuery.ToListAsync())
                .Where(a => !allTodayByEmpId.ContainsKey(a.EmployeeId))
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.First());

            // ── 3. كل الموظفين النشطين ───────────────────────────────
            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (linkedId.HasValue)
                empQuery = empQuery.Where(e => e.Id == linkedId.Value);
            else if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);

            var employees = await empQuery.OrderBy(e => e.FullName).ToListAsync();

            // ── 4. بناء الصفوف ────────────────────────────────────────
            var rows = employees.Select(e =>
            {
                if (allTodayByEmpId.TryGetValue(e.Id, out var todayRecs))
                {
                    // السجل النشط = المفتوح (بدون انصراف)، أو الأحدث
                    var active = todayRecs.FirstOrDefault(a => !a.CheckOut.HasValue)
                                 ?? todayRecs.Last();
                    return new AttendanceIndexRow
                    {
                        Employee = e,
                        Record = active,
                        AllRecords = todayRecs,
                        IsOvernight = false
                    };
                }
                if (overnightByEmpId.TryGetValue(e.Id, out var overnight))
                {
                    return new AttendanceIndexRow
                    {
                        Employee = e,
                        Record = overnight,
                        AllRecords = new List<Attendance> { overnight },
                        IsOvernight = true
                    };
                }
                return new AttendanceIndexRow
                {
                    Employee = e,
                    Record = null,
                    AllRecords = new List<Attendance>(),
                    IsOvernight = false
                };
            })
            .OrderBy(r => r.Employee.DepartmentNav?.Name == "مساج" ? 1 : 0)
            .ThenBy(r => r.Record?.QueuePosition ?? int.MaxValue)
            .ThenBy(r => r.Employee.FullName)
            .ToList();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.IsEmployee = linkedId.HasValue;
            ViewBag.IsToday = filterDate.Date == KuwaitToday;
            return View(rows);
        }

        public async Task<IActionResult> Create()
        {
            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            if (linkedId.HasValue)
            {
                var employee = await _context.Employees
                    .Include(e => e.DepartmentNav)
                    .FirstOrDefaultAsync(e => e.Id == linkedId.Value);
                if (employee == null) return Forbid();

                bool alreadyExists = await _context.Attendances.AnyAsync(a =>
                    a.EmployeeId == linkedId.Value && a.AttendanceDate == KuwaitToday);
                if (alreadyExists)
                {
                    TempData["Error"] = "Attendance already recorded today";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.IsEmployee = true;
                ViewBag.EmployeeName = employee.FullName;
                return View(new Attendance { AttendanceDate = KuwaitToday, EmployeeId = linkedId.Value });
            }

            var userDeptForCreate = await GetUserDepartmentAsync();
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDeptForCreate == "حلاقة" || userDeptForCreate == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDeptForCreate);
            ViewBag.IsEmployee = false;
            ViewBag.Employees = new SelectList(
                await empQuery.OrderBy(e => e.FullName).ToListAsync(),
                "Id", "FullName");
            return View(new Attendance { AttendanceDate = KuwaitToday });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Attendance model)
        {
            var linkedId = await GetLinkedEmployeeIdIfEmployee();

            if (linkedId.HasValue)
            {
                model.EmployeeId = linkedId.Value;
                model.CheckIn = KuwaitNow.TimeOfDay;
                model.CheckOut = null;
                model.AttendanceDate = KuwaitToday;
            }

            // Prevent duplicate for same employee+date
            bool duplicate = await _context.Attendances.AnyAsync(a =>
                a.EmployeeId == model.EmployeeId && a.AttendanceDate == model.AttendanceDate);
            if (duplicate)
                ModelState.AddModelError(string.Empty, "Attendance record already exists for this employee today");

            if (ModelState.IsValid)
            {
                // Assign queue position based on employee's department
                var employee = await _context.Employees
                    .Include(e => e.DepartmentNav)
                    .FirstOrDefaultAsync(e => e.Id == model.EmployeeId);
                var dept = employee?.DepartmentNav?.Name;
                model.QueuePosition = await NextQueuePosition(dept);
                model.CreatedAt = KuwaitNow;

                _context.Attendances.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Check-In", "Attendance", $"Check-in: {employee?.FullName} — Position: {model.QueuePosition}", model.Id);
                TempData["Success"] = $"Attendance recorded created successfully — Position: {model.QueuePosition}";
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
                var userDeptPost = await GetUserDepartmentAsync();
                var empQPost = _context.Employees.Where(e => e.IsActive);
                if (userDeptPost == "حلاقة" || userDeptPost == "مساج")
                    empQPost = empQPost.Where(e => e.DepartmentNav!.Name == userDeptPost);
                ViewBag.IsEmployee = false;
                ViewBag.Employees = new SelectList(
                    await empQPost.OrderBy(e => e.FullName).ToListAsync(),
                    "Id", "FullName");
            }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCheckIn(int employeeId, string? date)
        {
            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && linkedId.Value != employeeId)
                return Forbid();

            var attendanceDate = string.IsNullOrEmpty(date) ? KuwaitToday : DateTime.Parse(date);

            // منع التسجيل لو في سجل مفتوح امبارح (موظف ليلي لسه شغال)
            var prevDate = attendanceDate.AddDays(-1);
            bool hasOpenOvernight = await _context.Attendances.AnyAsync(a =>
                a.EmployeeId == employeeId && a.AttendanceDate == prevDate
                && a.CheckIn.HasValue && !a.CheckOut.HasValue);
            if (hasOpenOvernight)
            {
                TempData["Error"] = "There is an open attendance record from yesterday — record check-out first";
                return RedirectToAction(nameof(Index), new { date });
            }

            // يُمنع التسجيل فقط لو في سجل مفتوح (بدون انصراف) في نفس اليوم
            bool hasOpenToday = await _context.Attendances.AnyAsync(a =>
                a.EmployeeId == employeeId && a.AttendanceDate == attendanceDate
                && !a.CheckOut.HasValue);

            if (hasOpenToday)
            {
                TempData["Error"] = "There is an open attendance record for this employee — record check-out first";
                return RedirectToAction(nameof(Index), new { date });
            }

            var employee = await _context.Employees
                .Include(e => e.DepartmentNav)
                .FirstOrDefaultAsync(e => e.Id == employeeId);
            var dept = employee?.DepartmentNav?.Name;
            var queuePosition = await NextQueuePosition(dept);

            var attendance = new Attendance
            {
                EmployeeId = employeeId,
                AttendanceDate = attendanceDate,
                CheckIn = KuwaitNow.TimeOfDay,
                QueuePosition = queuePosition,
                CreatedAt = KuwaitNow
            };
            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Check-In", "Attendance", $"Quick check-in: {employee?.FullName} — Position: {queuePosition}", attendance.Id);
            TempData["SuccessTitle"] = "تم تسجيل الحضور";
            TempData["SuccessDetail"] = $"تم تسجيل حضور {employee?.FullName} — الدور: {queuePosition}";

            _ = Task.Run(() => _emailService.SendAttendanceNotificationAsync(
                employee?.FullName ?? "-", dept ?? "-", "Check-In",
                attendance.CheckIn!.Value, attendanceDate, $"الدور: {queuePosition}"));

            return RedirectToAction(nameof(Index), new { date });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int id, string checkOutTime)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record == null) return RedirectToAction(nameof(Index));

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && record.EmployeeId != linkedId.Value)
                return Forbid();

            var time = linkedId.HasValue
                ? KuwaitNow.TimeOfDay
                : (TimeSpan.TryParse(checkOutTime, out var parsed) ? parsed : KuwaitNow.TimeOfDay);

            record.CheckOut = time;
            await _context.SaveChangesAsync();
            var empForAudit = await _context.Employees.FindAsync(record.EmployeeId);
            await _audit.LogAsync("Check-Out", "Attendance", $"Check-out: {empForAudit?.FullName}", record.Id);
            TempData["SuccessTitle"] = "تم تسجيل الانصراف";
            TempData["SuccessDetail"] = $"تم تسجيل انصراف {empForAudit?.FullName} بنجاح";

            var empForMail = await _context.Employees.Include(e => e.DepartmentNav)
                .FirstOrDefaultAsync(e => e.Id == record.EmployeeId);
            _ = Task.Run(() => _emailService.SendAttendanceNotificationAsync(
                empForMail?.FullName ?? "-", empForMail?.DepartmentNav?.Name ?? "-",
                "Check-Out", time, record.AttendanceDate));

            // لو كان موظفاً ليلياً (تاريخ الحضور قبل النهارده) → ارجع لصفحة النهارده
            var redirectDate = record.AttendanceDate.Date < KuwaitToday
                ? KuwaitToday.ToString("yyyy-MM-dd")
                : record.AttendanceDate.ToString("yyyy-MM-dd");

            return RedirectToAction(nameof(Index), new { date = redirectDate });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPermission(int id)
        {
            var record = await _context.Attendances.FindAsync(id);
            if (record == null) return RedirectToAction(nameof(Index));

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && record.EmployeeId != linkedId.Value)
                return Forbid();

            // Check no active permission already open
            bool hasOpen = await _context.AttendancePermissions
                .AnyAsync(p => p.AttendanceId == id && p.ReturnTime == null);
            if (hasOpen)
            {
                TempData["Error"] = "There is already an open permission leave — record return first";
                return RedirectToAction(nameof(Index), new { date = record.AttendanceDate.ToString("yyyy-MM-dd") });
            }

            var perm = new AttendancePermission
            {
                AttendanceId = id,
                LeaveTime = KuwaitNow.TimeOfDay,
                CreatedAt = KuwaitNow
            };
            _context.AttendancePermissions.Add(perm);
            await _context.SaveChangesAsync();
            var empPermAudit = await _context.Employees.FindAsync(record.EmployeeId);
            await _audit.LogAsync("Permission", "Attendance", $"Permission leave: {empPermAudit?.FullName}", id);
            TempData["SuccessTitle"] = "تم تسجيل الاستئذان";
            TempData["SuccessDetail"] = $"تم تسجيل استئذان {empPermAudit?.FullName} بنجاح";

            var empPerm = await _context.Employees.Include(e => e.DepartmentNav)
                .FirstOrDefaultAsync(e => e.Id == record.EmployeeId);
            _ = Task.Run(() => _emailService.SendAttendanceNotificationAsync(
                empPerm?.FullName ?? "-", empPerm?.DepartmentNav?.Name ?? "-",
                "Permission", perm.LeaveTime, record.AttendanceDate));

            return RedirectToAction(nameof(Index), new { date = record.AttendanceDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePermission(int id)
        {
            var perm = await _context.AttendancePermissions
                .Include(p => p.Attendance)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (perm == null) return RedirectToAction(nameof(Index));

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && perm.Attendance?.EmployeeId != linkedId.Value)
                return Forbid();

            var date = perm.Attendance!.AttendanceDate.ToString("yyyy-MM-dd");
            var empPermDel = await _context.Employees.FindAsync(perm.Attendance.EmployeeId);
            _context.AttendancePermissions.Remove(perm);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Delete", "Attendance", $"Delete permission leave: {empPermDel?.FullName}", id);
            TempData["Success"] = "Permission leave deleted created successfully";
            return RedirectToAction(nameof(Index), new { date });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnFromPermission(int id)
        {
            var perm = await _context.AttendancePermissions
                .Include(p => p.Attendance)
                .FirstOrDefaultAsync(p => p.AttendanceId == id && p.ReturnTime == null);

            if (perm == null)
            {
                TempData["Error"] = "No open permission leave for this employee";
                return RedirectToAction(nameof(Index));
            }

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            if (linkedId.HasValue && perm.Attendance?.EmployeeId != linkedId.Value)
                return Forbid();

            perm.ReturnTime = KuwaitNow.TimeOfDay;
            await _context.SaveChangesAsync();
            var empRetAudit = await _context.Employees.FindAsync(perm.Attendance!.EmployeeId);
            await _audit.LogAsync("Return", "Attendance", $"Return from leave: {empRetAudit?.FullName}", id);
            TempData["SuccessTitle"] = "عودة من الاستئذان";
            TempData["SuccessDetail"] = $"تم تسجيل عودة {empRetAudit?.FullName} من الاستئذان";

            var empRet = await _context.Employees.Include(e => e.DepartmentNav)
                .FirstOrDefaultAsync(e => e.Id == perm.Attendance!.EmployeeId);
            _ = Task.Run(() => _emailService.SendAttendanceNotificationAsync(
                empRet?.FullName ?? "-", empRet?.DepartmentNav?.Name ?? "-",
                "Return", perm.ReturnTime!.Value, perm.Attendance!.AttendanceDate));

            return RedirectToAction(nameof(Index), new { date = perm.Attendance!.AttendanceDate.ToString("yyyy-MM-dd") });
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

                var empDelAudit = await _context.Employees.FindAsync(record.EmployeeId);
                _context.Attendances.Remove(record);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Attendance", $"Delete attendance record: {empDelAudit?.FullName} {record.AttendanceDate:yyyy/MM/dd}", id);
                TempData["Success"] = "Attendance record deleted created successfully";
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reports(string? dateFrom, string? dateTo, int? employeeId)
        {
            if (!await _perms.HasAccessAsync("ReportAttendance"))
                return Forbid();

            var today = KuwaitToday;
            var from = string.IsNullOrEmpty(dateFrom)
                ? new DateTime(today.Year, today.Month, 1)
                : DateTime.Parse(dateFrom);
            var to = string.IsNullOrEmpty(dateTo) ? today : DateTime.Parse(dateTo);

            var linkedId = await GetLinkedEmployeeIdIfEmployee();
            var userDept = await GetUserDepartmentAsync();

            var query = _context.Attendances
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(a => a.Permissions)
                .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to);

            if (linkedId.HasValue)
                query = query.Where(a => a.EmployeeId == linkedId.Value);
            else if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(a => a.Employee!.DepartmentNav!.Name == userDept);
            else if (employeeId.HasValue)
                query = query.Where(a => a.EmployeeId == employeeId.Value);

            var records = await query
                .OrderBy(a => a.Employee!.FullName)
                .ThenBy(a => a.AttendanceDate)
                .ThenBy(a => a.CheckIn)
                .ToListAsync();

            // قائمة الموظفين للـ dropdown
            var empQuery = _context.Employees.Include(e => e.DepartmentNav).Where(e => e.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == userDept);
            var employees = await empQuery.OrderBy(e => e.FullName).ToListAsync();

            ViewBag.DateFrom = from.ToString("yyyy-MM-dd");
            ViewBag.DateTo = to.ToString("yyyy-MM-dd");
            ViewBag.EmployeeId = employeeId;
            ViewBag.Employees = employees;
            ViewBag.IsEmployee = linkedId.HasValue;
            return View(records);
        }
    }
}