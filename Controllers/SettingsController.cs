using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
            ViewBag.SalonName    = settings.GetValueOrDefault("SalonName",    "معهد موس");
            ViewBag.SalonNameEn  = settings.GetValueOrDefault("SalonNameEn",  "Mos Institute");
            ViewBag.SalonPhone   = settings.GetValueOrDefault("SalonPhone",   "");
            ViewBag.SalonAddress = settings.GetValueOrDefault("SalonAddress", "");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string salonName, string salonNameEn, string salonPhone, string salonAddress)
        {
            await UpsertSetting("SalonName",    salonName    ?? "");
            await UpsertSetting("SalonNameEn",  salonNameEn  ?? "");
            await UpsertSetting("SalonPhone",   salonPhone   ?? "");
            await UpsertSetting("SalonAddress", salonAddress ?? "");
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حفظ الإعدادات بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task UpsertSetting(string key, string value)
        {
            var existing = await _context.AppSettings.FindAsync(key);
            if (existing == null)
                _context.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else
                existing.Value = value;
        }

        public IActionResult Email() => View();
        public IActionResult Database() => View();
        public IActionResult UserCards() => View();
        public IActionResult Training() => View();
        public IActionResult Packages() => View();
    }
}
