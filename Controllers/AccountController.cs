using Microsoft.AspNetCore.Mvc;
using Salon.Models;

namespace Salon.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // TODO: Replace with real authentication
            if (model.Email == "admin@salon.com" && model.Password == "admin123")
            {
                HttpContext.Session.SetString("IsLoggedIn", "true");
                HttpContext.Session.SetString("Email", model.Email);
                return RedirectToAction("Index", "Dashboard");
            }

            ModelState.AddModelError("", "البريد الإلكتروني أو كلمة المرور غير صحيحة");
            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}