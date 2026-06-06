using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;
using Salon.Models;

namespace Salon.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var messages = await _context.Messages
                .Where(m => m.IsBroadcast || m.ReceiverId == userId || m.SenderId == userId)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
            return View(messages);
        }

        public IActionResult Create() => View(new Message());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Message model)
        {
            if (ModelState.IsValid)
            {
                var userId = _userManager.GetUserId(User);
                model.SenderId = userId ?? string.Empty;
                model.SentAt = DateTime.Now;
                _context.Messages.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Message sent created successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Read(int id)
        {
            var message = await _context.Messages.FindAsync(id);
            if (message == null) return NotFound();
            message.IsRead = true;
            await _context.SaveChangesAsync();
            return View(message);
        }
    }
}