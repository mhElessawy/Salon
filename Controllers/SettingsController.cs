using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salon.Data;

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

        public IActionResult Index() => View();

        public IActionResult Email() => View();

        public IActionResult Database() => View();

        public IActionResult UserCards() => View();

        public IActionResult Training() => View();

        public IActionResult Packages() => View();
    }
}
