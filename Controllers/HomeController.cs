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
        private readonly IWebHostEnvironment _environment;
        private const long MaximumProfileImageBytes = 5 * 1024 * 1024;
        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _environment = environment;
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
            return View(new MenuViewModel
            {
                Products = products,
                Ratings = await GetProductRatingsAsync(products.Select(product => product.Id))
            });
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

        public IActionResult CustomerRegisterConfirmation(string? email, bool sent = true, bool merchant = false)
        {
            ViewData["Email"] = email;
            ViewData["EmailSent"] = sent;
            ViewData["IsMerchant"] = merchant;
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
                Orders = await CurrentCustomerOrders(user)
                    .AsNoTracking()
                    .Include(order => order.Items)
                    .OrderByDescending(order => order.PlacedAtUtc)
                    .Take(10)
                    .ToListAsync()
            };
            return View(model);
        }

        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var profile = await _context.CustomerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == user.Id);
            return View(CreateCustomerProfileViewModel(user, profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> Profile(CustomerProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var profile = await _context.CustomerProfiles
                .FirstOrDefaultAsync(item => item.UserId == user.Id);
            ValidateDefaultAddress(model.AddressLine1, model.AddressLine2, model.City, model.Postcode);

            string? imageExtension = null;
            if (model.ProfileImage is not null)
            {
                if (model.ProfileImage.Length == 0 || model.ProfileImage.Length > MaximumProfileImageBytes)
                {
                    ModelState.AddModelError(
                        nameof(model.ProfileImage),
                        "Choose a JPG, PNG or WebP image no larger than 5 MB.");
                }
                else
                {
                    imageExtension = await GetVerifiedProfileImageExtension(model.ProfileImage);
                    if (imageExtension is null)
                    {
                        ModelState.AddModelError(
                            nameof(model.ProfileImage),
                            "The profile photo must be a valid JPG, PNG or WebP image.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                PopulateCustomerProfileDisplay(model, user, profile);
                return View(model);
            }

            profile ??= new CustomerProfile { UserId = user.Id };
            if (_context.Entry(profile).State == EntityState.Detached)
            {
                _context.CustomerProfiles.Add(profile);
            }

            if (model.ProfileImage is not null && imageExtension is not null)
            {
                profile.ProfileImagePath = await SaveProfileImage(model.ProfileImage, imageExtension);
            }

            user.PhoneNumber = model.PhoneNumber.Trim();
            profile.AddressLine1 = NullIfWhiteSpace(model.AddressLine1);
            profile.AddressLine2 = NullIfWhiteSpace(model.AddressLine2);
            profile.City = NullIfWhiteSpace(model.City);
            profile.Postcode = NullIfWhiteSpace(model.Postcode);
            profile.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["ProfileMessage"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> OrderHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var orders = await CurrentCustomerOrders(user)
                .AsNoTracking()
                .Include(order => order.Items)
                .OrderByDescending(order => order.PlacedAtUtc)
                .ToListAsync();
            var orderIds = orders.Select(order => order.Id).ToList();

            return View(new OrderHistoryViewModel
            {
                DisplayName = User.FindFirstValue("display_name") ?? user.Email ?? "Customer",
                Email = user.Email ?? string.Empty,
                Orders = orders,
                ReviewedOrderIds = (await _context.OrderReviews
                    .AsNoTracking()
                    .Where(review => review.UserId == user.Id && orderIds.Contains(review.OrderId))
                    .Select(review => review.OrderId)
                    .ToListAsync())
                    .ToHashSet()
            });
        }

        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> LeaveReview(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var order = await FindOwnedCompletedOrder(user, id);
            if (order is null)
            {
                return NotFound();
            }

            if (await _context.OrderReviews.AnyAsync(review => review.OrderId == order.Id))
            {
                TempData["ReviewMessage"] = "This order has already been reviewed.";
                return RedirectToAction(nameof(OrderHistory));
            }

            return View(CreateLeaveReviewViewModel(order));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = PlatformRoles.Customer)]
        public async Task<IActionResult> LeaveReview(int id, LeaveReviewRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var order = await FindOwnedCompletedOrder(user, id);
            if (order is null)
            {
                return NotFound();
            }

            if (await _context.OrderReviews.AnyAsync(review => review.OrderId == order.Id))
            {
                TempData["ReviewMessage"] = "This order has already been reviewed.";
                return RedirectToAction(nameof(OrderHistory));
            }

            var orderProductIds = order.Items.Select(item => item.ProductId).Distinct().ToHashSet();
            var submittedProductIds = request.ProductRatings.Select(rating => rating.ProductId).ToList();
            if (submittedProductIds.Count != orderProductIds.Count ||
                submittedProductIds.Distinct().Count() != submittedProductIds.Count ||
                submittedProductIds.Any(productId => !orderProductIds.Contains(productId)))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "A rating from 1 to 5 is required for every product in this order.");
            }

            if (!ModelState.IsValid)
            {
                return View(CreateLeaveReviewViewModel(order, request));
            }

            var displayName = User.FindFirstValue("display_name") ?? order.CustomerName;
            var now = DateTime.UtcNow;
            var review = new OrderReview
            {
                OrderId = order.Id,
                UserId = user.Id,
                CustomerDisplayName = displayName,
                OverallRating = request.OverallRating,
                OverallComment = request.OverallComment.Trim(),
                IsAnonymous = request.IsAnonymous,
                Status = ReviewStatuses.Active,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ProductRatings = request.ProductRatings
                    .Select(rating => new ProductReviewRating
                    {
                        ProductId = rating.ProductId,
                        Rating = rating.Rating,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    })
                    .ToList()
            };

            _context.OrderReviews.Add(review);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var orderWasReviewed = await _context.OrderReviews
                    .AsNoTracking()
                    .AnyAsync(existingReview => existingReview.OrderId == order.Id);
                if (!orderWasReviewed)
                {
                    throw;
                }

                TempData["ReviewMessage"] = "This order has already been reviewed.";
                return RedirectToAction(nameof(OrderHistory));
            }

            TempData["ReviewMessage"] = "Thank you. Your review has been published.";
            return RedirectToAction(nameof(OrderHistory));
        }

        [Authorize(Roles = PlatformRoles.Merchant)]
        public async Task<IActionResult> MerchantDashboard(string period = "7days")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var localToday = DateTime.Today;
            var selectedRevenuePeriod = period?.ToLowerInvariant() switch
            {
                "30days" => "30days",
                "1year" => "1year",
                _ => "7days"
            };
            var revenueChartStartLocal = selectedRevenuePeriod switch
            {
                "30days" => localToday.AddDays(-29),
                "1year" => new DateTime(localToday.Year, localToday.Month, 1).AddMonths(-11),
                _ => localToday.AddDays(-6)
            };
            var revenueChartStartUtc = revenueChartStartLocal.ToUniversalTime();
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
                CateringRequests = await _context.CateringRequests.AsNoTracking().OrderByDescending(request => request.SubmittedAtUtc).ToListAsync(),
                Orders = orders,
                RevenuePeriod = selectedRevenuePeriod,
                RevenueOrders = await _context.CustomerOrders
                    .AsNoTracking()
                    .Where(order => !order.IsArchived && order.PlacedAtUtc >= revenueChartStartUtc)
                    .OrderBy(order => order.PlacedAtUtc)
                    .ToListAsync(),
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
                Orders = await _context.CustomerOrders.AsNoTracking().Include(order => order.Items).OrderByDescending(order => order.PlacedAtUtc).Take(100).ToListAsync(),
                Reviews = await _context.OrderReviews
                    .AsNoTracking()
                    .Include(review => review.User)
                    .Include(review => review.Order)
                        .ThenInclude(order => order.Items)
                    .Include(review => review.ProductRatings)
                        .ThenInclude(rating => rating.Product)
                    .OrderByDescending(review => review.CreatedAtUtc)
                    .ToListAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> ProductDetails(int id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.IsActive);
            if (product is null)
            {
                return NotFound();
            }

            var productRatings = await _context.ProductReviewRatings
                .AsNoTracking()
                .Where(rating =>
                    rating.ProductId == product.Id &&
                    rating.Review.Status == ReviewStatuses.Active)
                .Include(rating => rating.Review)
                .OrderByDescending(rating => rating.Rating)
                .ThenByDescending(rating => rating.CreatedAtUtc)
                .ToListAsync();
            var publicReviews = productRatings
                .Select(rating => new PublicProductReviewViewModel
                {
                    DisplayName = ReviewDisplay.PublicCustomerName(rating.Review),
                    Rating = rating.Rating,
                    CreatedAtUtc = rating.CreatedAtUtc
                })
                .ToList();

            return View(new ProductDetailsViewModel
            {
                Product = product,
                Rating = CreateRatingSummary(publicReviews.Select(review => review.Rating)),
                Reviews = publicReviews
            });
        }

        public async Task<IActionResult> Reviews()
        {
            var reviews = await _context.OrderReviews
                .AsNoTracking()
                .Where(review => review.Status == ReviewStatuses.Active)
                .Include(review => review.Order)
                    .ThenInclude(order => order.Items)
                .OrderByDescending(review => review.OverallRating)
                .ThenByDescending(review => review.CreatedAtUtc)
                .ToListAsync();

            var publicReviews = reviews.Select(review => new PublicOrderReviewViewModel
            {
                DisplayName = ReviewDisplay.PublicCustomerName(review),
                OrderNumber = review.Order.OrderNumber,
                ProductNames = review.Order.Items
                    .Select(item => item.ProductName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Rating = review.OverallRating,
                OverallComment = review.OverallComment,
                CreatedAtUtc = review.CreatedAtUtc
            }).ToList();

            return View(new ReviewsViewModel
            {
                Reviews = publicReviews,
                Rating = CreateRatingSummary(publicReviews.Select(review => review.Rating))
            });
        }

        public IActionResult ShoppingCart()
        {
            return View();
        }

        public async Task<IActionResult> Checkout()
        {
            if (User.Identity?.IsAuthenticated != true || !User.IsInRole(PlatformRoles.Customer))
            {
                return RedirectToAction(nameof(CustomerLogin));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return RedirectToAction(nameof(CustomerLogin));
            }

            var profile = await _context.CustomerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == user.Id);
            return View(new CheckoutViewModel
            {
                Name = User.FindFirstValue("display_name") ?? user.Email ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber ?? string.Empty,
                AddressLine1 = profile?.AddressLine1 ?? string.Empty,
                AddressLine2 = profile?.AddressLine2 ?? string.Empty,
                City = profile?.City ?? string.Empty,
                Postcode = profile?.Postcode ?? string.Empty,
                HasSavedDeliveryAddress = profile?.HasDefaultAddress == true,
                IsAuthenticatedCustomer = true
            });
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

        private IQueryable<CustomerOrder> CurrentCustomerOrders(IdentityUser user)
        {
            var email = user.Email ?? string.Empty;
            return _context.CustomerOrders.Where(order =>
                order.CustomerUserId == user.Id ||
                (order.CustomerUserId == null && order.Email == email));
        }

        private CustomerProfileViewModel CreateCustomerProfileViewModel(
            IdentityUser user,
            CustomerProfile? profile) => new()
        {
            DisplayName = User.FindFirstValue("display_name") ?? user.Email ?? "Customer",
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            ProfileImagePath = profile?.ProfileImagePath,
            AddressLine1 = profile?.AddressLine1 ?? string.Empty,
            AddressLine2 = profile?.AddressLine2 ?? string.Empty,
            City = profile?.City ?? string.Empty,
            Postcode = profile?.Postcode ?? string.Empty
        };

        private void PopulateCustomerProfileDisplay(
            CustomerProfileViewModel model,
            IdentityUser user,
            CustomerProfile? profile)
        {
            model.DisplayName = User.FindFirstValue("display_name") ?? user.Email ?? "Customer";
            model.Email = user.Email ?? string.Empty;
            model.ProfileImagePath = profile?.ProfileImagePath;
        }

        private void ValidateDefaultAddress(
            string addressLine1,
            string addressLine2,
            string city,
            string postcode)
        {
            var hasAnyAddressValue = new[] { addressLine1, addressLine2, city, postcode }
                .Any(value => !string.IsNullOrWhiteSpace(value));
            if (!hasAnyAddressValue)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(addressLine1))
            {
                ModelState.AddModelError(nameof(CustomerProfileViewModel.AddressLine1), "Address Line 1 is required for a default address.");
            }
            if (string.IsNullOrWhiteSpace(city))
            {
                ModelState.AddModelError(nameof(CustomerProfileViewModel.City), "City is required for a default address.");
            }
            if (string.IsNullOrWhiteSpace(postcode))
            {
                ModelState.AddModelError(nameof(CustomerProfileViewModel.Postcode), "Postcode is required for a default address.");
            }
        }

        private static string? NullIfWhiteSpace(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static async Task<string?> GetVerifiedProfileImageExtension(IFormFile image)
        {
            var contentType = image.ContentType.Trim().ToLowerInvariant();
            if (contentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp"))
            {
                return null;
            }

            var header = new byte[12];
            await using var stream = image.OpenReadStream();
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));

            if (bytesRead >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF &&
                contentType is "image/jpeg" or "image/jpg")
            {
                return ".jpg";
            }

            if (bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                contentType == "image/png")
            {
                return ".png";
            }

            if (bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
                contentType == "image/webp")
            {
                return ".webp";
            }

            return null;
        }

        private async Task<string> SaveProfileImage(IFormFile image, string extension)
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var uploadDirectory = Path.Combine(webRoot, "uploads", "profiles");
            Directory.CreateDirectory(uploadDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDirectory, fileName);
            await using var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await image.CopyToAsync(output);
            return $"/uploads/profiles/{fileName}";
        }

        private async Task<CustomerOrder?> FindOwnedCompletedOrder(IdentityUser user, int orderId)
        {
            return await CurrentCustomerOrders(user)
                .AsNoTracking()
                .Include(order => order.Items)
                .FirstOrDefaultAsync(order =>
                    order.Id == orderId &&
                    order.Status == OrderStatuses.Completed);
        }

        private static LeaveReviewViewModel CreateLeaveReviewViewModel(
            CustomerOrder order,
            LeaveReviewRequest? submittedReview = null)
        {
            var submittedByProductId = submittedReview?.ProductRatings
                .GroupBy(rating => rating.ProductId)
                .ToDictionary(group => group.Key, group => group.First());

            return new LeaveReviewViewModel
            {
                Order = order,
                OverallRating = submittedReview?.OverallRating is >= 1 and <= 5
                    ? submittedReview.OverallRating
                    : null,
                OverallComment = submittedReview?.OverallComment ?? string.Empty,
                IsAnonymous = submittedReview?.IsAnonymous ?? false,
                Products = order.Items
                    .GroupBy(item => item.ProductId)
                    .Select(group =>
                    {
                        var firstItem = group.First();
                        ProductReviewRatingInputModel? submitted = null;
                        submittedByProductId?.TryGetValue(group.Key, out submitted);
                        return new LeaveReviewProductViewModel
                        {
                            ProductId = group.Key,
                            ProductName = firstItem.ProductName,
                            UnitPrice = firstItem.UnitPrice,
                            Quantity = group.Sum(item => item.Quantity),
                            Rating = submitted?.Rating is >= 1 and <= 5 ? submitted.Rating : null
                        };
                    })
                    .ToList()
            };
        }

        private async Task<IReadOnlyDictionary<int, ProductRatingSummary>> GetProductRatingsAsync(
            IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            var ratings = await _context.ProductReviewRatings
                .AsNoTracking()
                .Where(rating =>
                    ids.Contains(rating.ProductId) &&
                    rating.Review.Status == ReviewStatuses.Active)
                .Select(rating => new { rating.ProductId, rating.Rating })
                .ToListAsync();

            return ids.ToDictionary(
                productId => productId,
                productId => CreateRatingSummary(ratings
                    .Where(rating => rating.ProductId == productId)
                    .Select(rating => rating.Rating)));
        }

        private static ProductRatingSummary CreateRatingSummary(IEnumerable<int> ratings)
        {
            var values = ratings.ToList();
            return new ProductRatingSummary
            {
                AverageRating = values.Count == 0
                    ? 0
                    : Math.Round((decimal)values.Average(), 1),
                ReviewCount = values.Count
            };
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
