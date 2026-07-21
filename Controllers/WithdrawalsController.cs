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
        private readonly IDailyClosureService _closure;

        public WithdrawalsController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager, IDailyClosureService closure)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
            _closure = closure;
        }

        public async Task<IActionResult> Index(string? from, string? to, string? dept)
        {
            DateTime dateFrom = string.IsNullOrEmpty(from)
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Parse(from);
            DateTime dateTo = string.IsNullOrEmpty(to)
                ? DateTime.Today.AddDays(1)
                : DateTime.Parse(to).AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            if ((userDept == "حلاقة" || userDept == "مساج") && string.IsNullOrEmpty(dept))
                dept = userDept;

            var query = _context.Withdrawals
                .Where(w => w.WithdrawalDate >= dateFrom && w.WithdrawalDate < dateTo);

            if (!string.IsNullOrEmpty(dept))
                query = query.Where(w => w.Department == dept);

            var withdrawals = await query.OrderByDescending(w => w.WithdrawalDate).ThenByDescending(w => w.CreatedAt).ToListAsync();

            ViewBag.From = dateFrom.ToString("yyyy-MM-dd");
            ViewBag.To = dateTo.AddDays(-1).ToString("yyyy-MM-dd");
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

            if (await _closure.IsDateLockedAsync(model.WithdrawalDate, model.Department))
            {
                TempData["Error"] = "لا يمكن تعديل سحب يخص يومية معتمدة — استخدم صلاحية إعادة فتح اليومية";
                return RedirectToAction(nameof(Index));
            }

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
                if (await _closure.IsDateLockedAsync(w.WithdrawalDate, w.Department))
                {
                    TempData["Error"] = "لا يمكن حذف سحب يخص يومية معتمدة — استخدم صلاحية إعادة فتح اليومية";
                    return RedirectToAction(nameof(Index));
                }

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