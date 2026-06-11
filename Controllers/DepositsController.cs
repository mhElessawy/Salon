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
    public class DepositsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;
        private readonly UserManager<ApplicationUser> _userManager;

        public DepositsController(ApplicationDbContext context, IAuditService audit, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _audit = audit;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? date, string? department)
        {
            DateTime filterDate = string.IsNullOrEmpty(date) ? DateTime.Today : DateTime.Parse(date);
            var nextDay = filterDate.AddDays(1);

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            var query = _context.Deposits
                .Where(d => d.DepositDate >= filterDate && d.DepositDate < nextDay);

            // فرض فلتر القسم حسب صلاحية المستخدم
            if (userDept == "حلاقة" || userDept == "مساج")
            {
                query = query.Where(d => d.Department == userDept);
                department = userDept;
            }
            else if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(d => d.Department == department);
            }

            var deposits = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            ViewBag.FilterDate = filterDate.ToString("yyyy-MM-dd");
            ViewBag.FilterDepartment = department ?? "";
            ViewBag.UserDept = userDept;
            ViewBag.Total = deposits.Sum(d => d.Amount);
            return View(deposits);
        }

        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = currentUser?.UserDepartment;
            return View(new Deposit { DepositDate = DateTime.Today });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Deposit model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            // إذا كان المستخدم مقيداً بقسم، نفرض قسمه بغض النظر عن الإدخال
            if (userDept == "حلاقة" || userDept == "مساج")
                model.Department = userDept;

            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Deposits.Add(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Add", "Deposits",
                    $"{model.Description} - {model.Amount:F3} KD ({model.Department})", model.Id);
                TempData["Success"] = "تم إضافة الإيداع بنجاح";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UserDept = userDept;
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var deposit = await _context.Deposits.FindAsync(id);
            if (deposit == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.UserDept = currentUser?.UserDepartment;
            return View(deposit);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Deposit model)
        {
            if (id != model.Id) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var userDept = currentUser?.UserDepartment;

            // إذا كان المستخدم مقيداً بقسم، نفرض قسمه
            if (userDept == "حلاقة" || userDept == "مساج")
                model.Department = userDept;

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Edit", "Deposits",
                    $"{model.Description} - {model.Amount:F3} KD ({model.Department})", model.Id);
                TempData["Success"] = "تم تعديل الإيداع بنجاح";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UserDept = userDept;
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deposit = await _context.Deposits.FindAsync(id);
            if (deposit != null)
            {
                var desc = $"{deposit.Description} - {deposit.Amount:F3} KD";
                _context.Deposits.Remove(deposit);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Delete", "Deposits", desc, id);
                TempData["Success"] = "تم حذف الإيداع بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}