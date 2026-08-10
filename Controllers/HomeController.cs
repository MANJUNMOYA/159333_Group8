using System.Diagnostics;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoffeeSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Menu()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(product => product.IsActive)
                .OrderBy(product => product.SortOrder)
                .ToListAsync();
            return View(products);
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

        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> CustomerDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var model = new CustomerDashboardViewModel
            {
                DisplayName = User.FindFirstValue("display_name") ?? user.Email ?? "Customer",
                Email = user.Email ?? string.Empty,
                Orders = await _context.CustomerOrders
                    .AsNoTracking()
                    .Include(order => order.Items)
                    .Where(order => order.CustomerUserId == user.Id || order.Email == user.Email)
                    .OrderByDescending(order => order.PlacedAtUtc)
                    .Take(10)
                    .ToListAsync()
            };
            return View(model);
        }

        [Authorize(Roles = PlatformRoles.Merchant)]
        public async Task<IActionResult> MerchantDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var localToday = DateTime.Today;
            var orders = await _context.CustomerOrders
                .AsNoTracking()
                .Include(order => order.Items)
                .Where(order => !order.IsArchived)
                .OrderByDescending(order => order.PlacedAtUtc)
                .Take(100)
                .ToListAsync();
            var model = new MerchantDashboardViewModel
            {
                DisplayName = User.FindFirstValue("display_name") ?? user.Email ?? "Merchant",
                Email = user.Email ?? string.Empty,
                Products = await _context.Products.AsNoTracking().OrderBy(product => product.SortOrder).ToListAsync(),
                Orders = orders,
                TodayOrderCount = orders.Count(order => order.PlacedAtUtc.ToLocalTime().Date == localToday),
                TodayRevenue = orders
                    .Where(order => order.PlacedAtUtc.ToLocalTime().Date == localToday && order.Status != OrderStatuses.Cancelled)
                    .Sum(order => order.Total)
            };
            return View(model);
        }

        [Authorize(Roles = PlatformRoles.Administrator)]
        public async Task<IActionResult> AdministratorDashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null)
            {
                return Challenge();
            }

            var users = await _userManager.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync();
            var userModels = new List<AdministratorUserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var claims = await _userManager.GetClaimsAsync(user);
                userModels.Add(new AdministratorUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    DisplayName = claims.FirstOrDefault(claim => claim.Type == "display_name")?.Value ?? user.Email ?? "User",
                    Role = roles.FirstOrDefault() ?? "Pending"
                });
            }

            var model = new AdministratorDashboardViewModel
            {
                DisplayName = User.FindFirstValue("display_name") ?? currentUser.Email ?? "Administrator",
                Email = currentUser.Email ?? string.Empty,
                Users = userModels,
                MerchantApplications = await _context.MerchantApplications.AsNoTracking().OrderByDescending(item => item.SubmittedAtUtc).ToListAsync(),
                Products = await _context.Products.AsNoTracking().OrderBy(item => item.SortOrder).ToListAsync(),
                Orders = await _context.CustomerOrders.AsNoTracking().Include(order => order.Items).OrderByDescending(order => order.PlacedAtUtc).Take(100).ToListAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> ProductDetails(int id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.IsActive);
            return product is null ? NotFound() : View(product);
        }

        public IActionResult ShoppingCart()
        {
            return View();
        }

        public IActionResult Checkout()
        {
            return View();
        }

        public async Task<IActionResult> OrderConfirmation(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return View(model: null);
            }

            var order = await _context.CustomerOrders
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.OrderNumber == orderNumber);
            return View(order);
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
