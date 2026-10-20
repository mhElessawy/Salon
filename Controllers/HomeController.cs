using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Salon.Models;

namespace Salon.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var vm = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };

            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is { } ex)
            {
                vm.RequestPath = exceptionFeature.Path;
                _logger.LogError(ex, "Unhandled exception at {Path}. Request ID: {RequestId}", exceptionFeature.Path, vm.RequestId);

                if (User.IsInRole("Admin") || User.IsInRole("Manager") ||
                    _configuration.GetValue<bool>("ShowDetailedErrors"))
                {
                    vm.ExceptionType = ex.GetType().FullName;
                    vm.ExceptionMessage = ex.Message;
                    vm.StackTrace = ex.ToString();
                }
            }

            return View(vm);
        }
    }
}