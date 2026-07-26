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

            // »Ì„”ﬂ «·«” À‰«¡ «·ÕﬁÌﬁÌ «··Ì Êﬁ¯⁄ «·’›Õ… ÊÌ”Ã¯·Â ›Ì «··ÊÃ œ«Ì„«° ÊÌ⁄—÷Â
            // ⁄·Ï «·‘«‘… »” ·Ê ShowDetailedErrors „›⁄¯·… ›Ì appsettings.json (ÌœÊÌ« Êﬁ 
            // «· ‘ŒÌ’) - ⁄‘«‰ „« Ì ”—»‘  ›«’Ì· Õ”«”… ··“Ê«— ›Ì «·Ê÷⁄ «·ÿ»Ì⁄Ì.
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is { } ex)
            {
                _logger.LogError(ex, "Unhandled exception at {Path}", exceptionFeature.Path);

                if (_configuration.GetValue<bool>("ShowDetailedErrors"))
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