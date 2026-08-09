using System.Diagnostics;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace CampusCoffeeSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Menu()
        {
            return View();
        }

        public IActionResult Catering()
        {
            return View();
        }

        public IActionResult OurStory()
        {
            return View();
        }

        public IActionResult Portal()
        {
            return View();
        }

        public IActionResult CustomerLogin()
        {
            return View();
        }

        public IActionResult CustomerRegister()
        {
            return View();
        }

        public IActionResult MerchantLogin()
        {
            return View();
        }

        public IActionResult MerchantRegister()
        {
            return View();
        }

        public IActionResult MerchantApplication()
        {
            return View();
        }

        public IActionResult AdministratorLogin()
        {
            return View();
        }

        public IActionResult CustomerDashboard()
        {
            return View();
        }

        public IActionResult MerchantDashboard()
        {
            return View();
        }

        public IActionResult AdministratorDashboard()
        {
            return View();
        }

        public IActionResult ProductDetails()
        {
            return View();
        }

        public IActionResult ShoppingCart()
        {
            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        public IActionResult OrderConfirmation()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
