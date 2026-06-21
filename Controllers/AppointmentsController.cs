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

        public async Task<IActionResult> Index(string? from, string? to)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee).ThenInclude(e => e!.DepartmentNav)
                .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
                .AsQueryable();

            DateTime? dateFrom = null, dateTo = null;
            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var df))
            {
                dateFrom = df.Date;
                query = query.Where(a => a.AppointmentDate >= dateFrom);
            }
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var dt))
            {
                dateTo = dt.Date;
                query = query.Where(a => a.AppointmentDate < dateTo.Value.AddDays(1));
            }

            if (userDept == "حلاقة" || userDept == "مساج")
                query = query.Where(a => a.Employee!.DepartmentNav!.Name == userDept);

            var appointments = await query.OrderBy(a => a.AppointmentDate).ToListAsync();

            ViewBag.From = dateFrom?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.To = dateTo?.ToString("yyyy-MM-dd") ?? "";
            return View(appointments);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            ViewBag.UserDept = userDept;
            ViewBag.CanSelectDept = userDept != "حلاقة" && userDept != "مساج";
            ViewBag.InitialDept = (userDept == "حلاقة" || userDept == "مساج") ? userDept : "حلاقة";

            // Load services for the initial department
            var svcQuery = _context.Services.Include(s => s.ServiceCategory).Where(s => s.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                svcQuery = svcQuery.Where(s => s.ServiceCategory!.Department == userDept);
            ViewBag.Services = await svcQuery.OrderBy(s => s.Name).ToListAsync();

            return View(new Appointment { AppointmentDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model, int[]? serviceIds, int[]? companionCustomerIds)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Customer");
            ModelState.Remove("Employee");
            ModelState.Remove("CustomerPackage");
            ModelState.Remove("AppointmentServices");

            // الموعد يجب أن يكون في المستقبل
            if (model.AppointmentDate <= DateTime.Now)
                ModelState.AddModelError("AppointmentDate", "يجب أن يكون وقت الموعد بعد الوقت الحالي");

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
                }

                // Deduct session from package if used
                if (model.CustomerPackageId.HasValue)
                {
                    var customerPkg = await _context.CustomerPackages
                        .Include(cp => cp.ServicePackage)
                        .FirstOrDefaultAsync(cp => cp.Id == model.CustomerPackageId.Value);

                    if (customerPkg != null && customerPkg.RemainingSessions > 0)
                    {
                        customerPkg.RemainingSessions--;
                        if (customerPkg.RemainingSessions == 0)
                            customerPkg.IsActive = false;

                        _context.CustomerPackageTransactions.Add(new CustomerPackageTransaction
                        {
                            CustomerPackageId = customerPkg.Id,
                            UsedDate = model.AppointmentDate,
                            EmployeeId = model.EmployeeId,
                            Notes = $"حجز موعد #{model.Id}"
                        });
                    }
                }

                // Create companion appointments (same time/employee, different customer)
                if (companionCustomerIds != null)
                {
                    foreach (var cid in companionCustomerIds)
                    {
                        var companion = new Appointment
                        {
                            CustomerId = cid,
                            EmployeeId = model.EmployeeId,
                            AppointmentDate = model.AppointmentDate,
                            EndTime = model.EndTime,
                            Status = "مجدول",
                            Notes = $"مرافق الموعد #{model.Id}"
                        };
                        _context.Appointments.Add(companion);
                    }
                }

                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Appointments", $"New appointment on {model.AppointmentDate:yyyy/MM/dd HH:mm}", model.Id);
                TempData["Success"] = "تم إضافة الموعد بنجاح";
                return RedirectToAction(nameof(Index));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;
            ViewBag.UserDept = userDept;
            ViewBag.CanSelectDept = userDept != "حلاقة" && userDept != "مساج";
            ViewBag.InitialDept = (userDept == "حلاقة" || userDept == "مساج") ? userDept : "حلاقة";
            var svcQuery = _context.Services.Include(s => s.ServiceCategory).Where(s => s.IsActive);
            if (userDept == "حلاقة" || userDept == "مساج")
                svcQuery = svcQuery.Where(s => s.ServiceCategory!.Department == userDept);
            ViewBag.Services = await svcQuery.OrderBy(s => s.Name).ToListAsync();
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
            ModelState.Remove("Customer");
            ModelState.Remove("Employee");
            ModelState.Remove("CustomerPackage");
            ModelState.Remove("AppointmentServices");

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
                await _audit.LogAsync("Delete", "Appointments", $"Delete appointment on {apt.AppointmentDate:yyyy/MM/dd HH:mm}", id);
                TempData["Success"] = "تم حذف الموعد بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        // ─── AJAX: عملاء حسب القسم ────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCustomersByDepartment(string dept)
        {
            var q = _context.Customers.Where(c => c.IsActive);
            if (!string.IsNullOrEmpty(dept))
                q = q.Where(c => c.Department == dept);

            var list = await q.OrderBy(c => c.FullName)
                .Select(c => new { c.Id, c.FullName, c.Phone })
                .ToListAsync();

            return Json(list);
        }

        // ─── AJAX: موظفون حسب القسم ───────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetEmployeesByDepartment(string dept)
        {
            var q = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);

            if (!string.IsNullOrEmpty(dept))
                q = q.Where(e => e.DepartmentNav!.Name == dept);

            var list = await q.OrderBy(e => e.FullName)
                .Select(e => new { e.Id, e.FullName, dept = e.DepartmentNav != null ? e.DepartmentNav.Name : "" })
                .ToListAsync();

            return Json(list);
        }

        // ─── AJAX: باقات عميل النشطة ─────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCustomerPackages(int customerId)
        {
            var today = DateTime.Today;
            var list = await _context.CustomerPackages
                .Include(cp => cp.ServicePackage)
                .Where(cp => cp.CustomerId == customerId
                    && cp.IsActive
                    && cp.RemainingSessions > 0
                    && (cp.ExpiryDate == null || cp.ExpiryDate >= today))
                .Select(cp => new
                {
                    cp.Id,
                    name = cp.ServicePackage != null ? cp.ServicePackage.NameAr : "",
                    cp.RemainingSessions,
                    cp.TotalSessions,
                    expiry = cp.ExpiryDate != null ? cp.ExpiryDate.Value.ToString("dd/MM/yyyy") : ""
                })
                .ToListAsync();

            return Json(list);
        }

        // ─── AJAX: خدمات حسب القسم ───────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetServicesByDepartment(string dept)
        {
            var q = _context.Services
                .Include(s => s.ServiceCategory)
                .Where(s => s.IsActive);

            if (!string.IsNullOrEmpty(dept))
                q = q.Where(s => s.ServiceCategory!.Department == dept);

            var list = await q.OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    price = s.Price.ToString("N3"),
                    category = s.ServiceCategory != null ? s.ServiceCategory.Name : ""
                })
                .ToListAsync();

            return Json(list);
        }

        // ─── AJAX: فترات الوقت المتاحة ───────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetTimeSlots(string date, int? employeeId)
        {
            if (!DateTime.TryParse(date, out var targetDate))
                return Json(new List<object>());

            var startOfDay = targetDate.Date;
            var endOfDay = startOfDay.AddDays(1);

            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Employee)
                .Where(a => a.AppointmentDate >= startOfDay && a.AppointmentDate < endOfDay)
                .Where(a => !employeeId.HasValue || a.EmployeeId == employeeId)
                .ToListAsync();

            Attendance? attendance = null;
            List<AttendancePermission> breaks = new();
            if (employeeId.HasValue)
            {
                attendance = await _context.Attendances
                    .Include(a => a.Permissions)
                    .FirstOrDefaultAsync(a => a.EmployeeId == employeeId.Value && a.AttendanceDate == startOfDay);
                if (attendance != null)
                    breaks = attendance.Permissions.ToList();
            }

            var slots = new List<object>();
            for (int hour = 11; hour < 19; hour++)
            {
                var slotStart = TimeSpan.FromHours(hour);
                var slotEnd = TimeSpan.FromHours(hour + 1);

                bool isBreak = breaks.Any(b =>
                    b.LeaveTime < slotEnd && (b.ReturnTime == null || b.ReturnTime > slotStart));

                bool outsideShift = false;
                if (attendance != null)
                {
                    if (attendance.CheckIn.HasValue && slotEnd <= attendance.CheckIn.Value)
                        outsideShift = true;
                    if (attendance.CheckOut.HasValue && slotStart >= attendance.CheckOut.Value)
                        outsideShift = true;
                }

                var overlapping = appointments.FirstOrDefault(a =>
                {
                    var aptStart = a.AppointmentDate.TimeOfDay;
                    var aptEnd = a.EndTime ?? aptStart.Add(TimeSpan.FromHours(1));
                    return aptStart < slotEnd && aptEnd > slotStart;
                });

                string status;
                string? personName = null;

                if (overlapping != null)
                {
                    status = "محجوز";
                    personName = overlapping.Customer?.FullName;
                }
                else if (isBreak)
                {
                    status = "استراحة";
                    personName = "استراحة الموظف";
                }
                else if (outsideShift)
                {
                    status = "خارج الدوام";
                }
                else
                {
                    status = "متاح";
                }

                slots.Add(new
                {
                    startTime = $"{hour:D2}:00",
                    endTime = $"{hour + 1:D2}:00",
                    status,
                    personName
                });
            }

            return Json(slots);
        }

        // ─── AJAX: عرض التقويم (كل الموظفين × كل الأوقات) ──────────────
        [HttpGet]
        public async Task<IActionResult> GetCalendarView(string date, string? dept)
        {
            if (!DateTime.TryParse(date, out var targetDate))
                return Json(new { employees = new List<object>(), timeSlots = new List<object>() });

            var startOfDay = targetDate.Date;
            var endOfDay = startOfDay.AddDays(1);

            var empQuery = _context.Employees
                .Include(e => e.DepartmentNav)
                .Where(e => e.IsActive);
            if (!string.IsNullOrEmpty(dept) && dept != "الكل")
                empQuery = empQuery.Where(e => e.DepartmentNav!.Name == dept);
            var employees = await empQuery.OrderBy(e => e.FullName).ToListAsync();
            var empIds = employees.Select(e => e.Id).ToList();

            var appointments = await _context.Appointments
                .Include(a => a.Customer)
                .Where(a => a.AppointmentDate >= startOfDay && a.AppointmentDate < endOfDay
                         && a.EmployeeId != null && empIds.Contains(a.EmployeeId!.Value))
                .ToListAsync();

            var attendances = await _context.Attendances
                .Include(a => a.Permissions)
                .Where(a => a.AttendanceDate == startOfDay && empIds.Contains(a.EmployeeId))
                .ToListAsync();

            var employeeData = employees.Select(e => new
            {
                id = e.Id,
                name = e.FullName,
                jobTitle = e.JobTitle ?? "",
                dept = e.DepartmentNav?.Name ?? ""
            }).ToList<object>();

            var timeSlots = new List<object>();
            for (int m = 0; m < (18 - 9) * 60; m += 30)
            {
                var slotStart = TimeSpan.FromMinutes(9 * 60 + m);
                var slotEnd = slotStart.Add(TimeSpan.FromMinutes(30));

                var cells = employees.Select(emp =>
                {
                    var att = attendances.FirstOrDefault(a => a.EmployeeId == emp.Id);
                    var breaks = att?.Permissions ?? new List<AttendancePermission>();

                    bool isBreak = breaks.Any(b =>
                        b.LeaveTime < slotEnd && (b.ReturnTime == null || b.ReturnTime > slotStart));

                    bool outsideShift = false;
                    if (att != null)
                    {
                        if (att.CheckIn.HasValue && slotEnd <= att.CheckIn.Value) outsideShift = true;
                        if (att.CheckOut.HasValue && slotStart >= att.CheckOut.Value) outsideShift = true;
                    }

                    var overlap = appointments.FirstOrDefault(a =>
                        a.EmployeeId == emp.Id
                        && a.AppointmentDate.TimeOfDay < slotEnd
                        && (a.EndTime ?? a.AppointmentDate.TimeOfDay.Add(TimeSpan.FromHours(1))) > slotStart);

                    string status;
                    string? personName = null;

                    if (overlap != null) { status = "محجوز"; personName = overlap.Customer?.FullName; }
                    else if (isBreak) { status = "استراحة"; }
                    else if (outsideShift) { status = "خارج الدوام"; }
                    else { status = "متاح"; }

                    return new { empId = emp.Id, status, personName };
                }).ToList<object>();

                int h = (int)slotStart.TotalHours;
                int mins = slotStart.Minutes;
                string amPm = h < 12 ? "ص" : "م";
                int h12 = h == 0 ? 12 : h > 12 ? h - 12 : h;

                timeSlots.Add(new
                {
                    startTime = $"{h:D2}:{mins:D2}",
                    endTime = $"{(int)slotEnd.TotalHours:D2}:{slotEnd.Minutes:D2}",
                    timeLabel = $"{h12:D2}:{mins:D2} {amPm}",
                    cells
                });
            }

            return Json(new { employees = employeeData, timeSlots });
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