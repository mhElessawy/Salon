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
    public class WithdrawalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public WithdrawalsController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? date, string? dept)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            if ((userDept == "حلاقة" || userDept == "مساج") && string.IsNullOrEmpty(dept))
                dept = userDept;

            var query = _context.Withdrawals
                .Where(w => w.WithdrawalDate >= filterDate && w.WithdrawalDate < nextDay);

            if (!string.IsNullOrEmpty(dept))
                query = query.Where(w => w.Department == dept);

            var withdrawals = await query.OrderByDescending(w => w.CreatedAt).ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.FilterDept = dept;
            ViewBag.Total = withdrawals.Sum(w => w.Amount);
            ViewBag.UserDepartment = userDept;
            return View(withdrawals);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = currentUser?.UserDepartment;
            return View(new Withdrawal { WithdrawalDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Withdrawal model)
        {
            if (string.IsNullOrEmpty(model.Department))
                ModelState.AddModelError("Department", "يرجى اختيار القسم");

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Withdrawals.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Withdrawals",
                    $"[{model.Department}] {model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "تم إضافة السحب بنجاح";
                return RedirectToAction(nameof(Index));
            }
            var cu = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = cu?.UserDepartment;
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var w = await _context.Withdrawals.FindAsync(id);
            if (w == null) return NotFound();
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = currentUser?.UserDepartment;
            return View(w);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Withdrawal model)
        {
            if (id != model.Id) return NotFound();

            if (string.IsNullOrEmpty(model.Department))
                ModelState.AddModelError("Department", "يرجى اختيار القسم");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Withdrawals",
                    $"[{model.Department}] {model.Description} - {model.Amount:F3} KD", model.Id);
                TempData["Success"] = "تم تعديل السحب بنجاح";
                return RedirectToAction(nameof(Index));
            }
            var cu = await _userManager.GetUserAsync(User);
            ViewBag.UserDepartment = cu?.UserDepartment;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var w = await _context.Withdrawals.FindAsync(id);
            if (w != null)
            {
                var desc = $"[{w.Department}] {w.Description} - {w.Amount:F3} KD";
                _context.Withdrawals.Remove(w);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Withdrawals", desc, id);
                TempData["Success"] = "تم حذف السحب بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
