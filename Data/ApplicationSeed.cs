using System.Security.Claims;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Data;

public static class ApplicationSeed
{
    public static async Task InitialiseAsync(IServiceProvider services, IConfiguration configuration)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var roleName in PlatformRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        await SeedAdministratorAsync(userManager, configuration);
        // Old customer accounts predate roles. Exclude pending merchant accounts.
        var legacyCustomers = await context.Users
            .Where(user => !context.UserRoles.Any(role => role.UserId == user.Id) &&
                !context.MerchantApplications.Any(application => application.UserId == user.Id || application.Email == user.Email))
            .ToListAsync();
        foreach (var customer in legacyCustomers)
        {
            var roleResult = await userManager.AddToRoleAsync(customer, PlatformRoles.Customer);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Could not assign the Customer role to a legacy account.");
        }
        await SeedProductsAsync(context);

        if (configuration.GetValue<bool>("FrontendDemoSeed:Enabled"))
        {
            await SeedFrontendDemoDataAsync(context, userManager, configuration);
        }
    }

    private static async Task SeedAdministratorAsync(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var administrator = await userManager.FindByEmailAsync(email);
        if (administrator is null)
        {
            administrator = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(administrator, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "The development administrator could not be created: " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }

            await userManager.AddClaimsAsync(administrator,
            [
                new Claim(ClaimTypes.GivenName, "System"),
                new Claim(ClaimTypes.Surname, "Administrator"),
                new Claim("display_name", "System Administrator")
            ]);
        }

        if (!await userManager.IsInRoleAsync(administrator, PlatformRoles.Administrator))
        {
            await userManager.AddToRoleAsync(administrator, PlatformRoles.Administrator);
        }
    }

    private static async Task SeedProductsAsync(ApplicationDbContext context)
    {
        var seedProducts = CreateSeedProducts();
        var existingNames = (await context.Products
                .AsNoTracking()
                .Select(product => product.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingProducts = seedProducts
            .Where(product => !existingNames.Contains(product.Name))
            .ToList();

        if (missingProducts.Count == 0)
        {
            return;
        }

        context.Products.AddRange(missingProducts);
        await context.SaveChangesAsync();
    }

    private static List<Product> CreateSeedProducts() =>
    [
        CreateProduct(1, "Campus Flat White", "coffee", "Coffee", 4.80m, 28, "Campus Flat White.jpg", "Double espresso finished with smooth, velvety steamed milk.", "Vegetarian", "vegetarian", "Contains milk"),
        CreateProduct(3, "Long Black", "coffee", "Coffee", 4.20m, 24, "Long Black.jpg", "Rich double espresso poured over hot water for a clean finish.", "Vegan", "vegan", "No major allergens"),
        CreateProduct(5, "Iced Oat Latte", "coffee", "Cold coffee", 5.80m, 18, "Iced Oat Latte.jpg", "Espresso, chilled oat milk and ice for an easy campus pick-me-up.", "Vegan", "vegan", "Contains oats"),
        CreateProduct(7, "Dark Chocolate Mocha", "coffee", "Coffee", 5.60m, 7, "Dark Chocolate Mocha.jpg", "Espresso and dark cocoa balanced with silky steamed milk.", "Vegetarian", "vegetarian", "Contains milk"),
        CreateProduct(9, "Vanilla Cold Brew", "coffee", "Cold coffee", 5.40m, 8, "Vanilla Cold Brew.jpg", "Slow-steeped coffee with a light touch of house vanilla.", "Vegan", "vegan", "No major allergens"),
        CreateProduct(11, "Maple Cinnamon Latte", "coffee", "Seasonal coffee", 5.90m, 0, "Maple Cinnamon Latte.jpg", "A warming espresso with maple, cinnamon and steamed milk.", "Vegetarian", "vegetarian", "Contains milk"),
        CreateProduct(2, "Butter Croissant", "food", "Bakery", 4.90m, 22, "Butter Croissant.jpg", "Flaky, golden and baked fresh for an easy morning start.", "Vegetarian", "vegetarian", "Gluten · Milk · Egg"),
        CreateProduct(4, "Roast Vegetable Focaccia", "food", "Lunch", 9.80m, 3, "Roast Vegetable Focaccia.jpg", "Roasted seasonal vegetables, greens and herb dressing in focaccia.", "Vegan", "vegan", "Contains gluten"),
        CreateProduct(6, "Egg and Spinach Brioche", "food", "Breakfast", 10.50m, 6, "Egg Spinach Brioche.jpg", "Free-range egg, baby spinach and cheddar in a toasted brioche bun.", "Vegetarian", "vegetarian", "Gluten · Milk · Egg"),
        CreateProduct(8, "Banana Walnut Loaf", "food", "Bakery", 5.50m, 14, "Banana Walnut Loaf.jpg", "A generous slice of soft banana loaf finished with toasted walnuts.", "Vegetarian", "vegetarian", "Gluten · Egg · Nuts"),
        CreateProduct(10, "Garden Salad Bowl", "food", "Lunch", 11.20m, 14, "Garden Salad Bowl.jpg", "Seasonal greens, roast vegetables, seeds and lemon herb dressing.", "Vegan", "vegan", "Contains sesame"),
        CreateProduct(12, "Mushroom and Thyme Toastie", "food", "Lunch", 10.80m, 0, "Mushroom Thyme Toastie.jpg", "Roasted mushrooms, thyme and cheddar pressed until crisp and golden.", "Vegetarian", "vegetarian", "Gluten · Milk")
    ];

    private static async Task SeedFrontendDemoDataAsync(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IConfiguration configuration)
    {
        var password = configuration["FrontendDemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Frontend demo seeding is enabled, but FrontendDemoSeed:Password is missing.");
        }

        var customer = await EnsureDemoUserAsync(
            userManager,
            "customer@test.com",
            password,
            "Customer",
            "Demo",
            "Customer Demo",
            PlatformRoles.Customer);
        var merchant = await EnsureDemoUserAsync(
            userManager,
            "merchant@test.com",
            password,
            "Campus Coffee",
            "Merchant",
            "Campus Coffee Merchant",
            PlatformRoles.Merchant);
        await EnsureDemoUserAsync(
            userManager,
            "admin@test.com",
            password,
            "System",
            "Administrator",
            "System Administrator",
            PlatformRoles.Administrator);
        var engineeringApplicant = await EnsureDemoUserAsync(
            userManager,
            "aisha.patel@campuscoffee.local",
            password,
            "Aisha",
            "Patel",
            "Aisha Patel");

        await SeedMerchantApplicationsAsync(context, merchant.Id, engineeringApplicant.Id);
        await SeedDemoOrdersAsync(context, customer.Id);
    }

    private static async Task<IdentityUser> EnsureDemoUserAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password,
        string givenName,
        string surname,
        string displayName,
        string? role = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The development demo account {normalizedEmail} could not be created: " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }
        }

        var claims = await userManager.GetClaimsAsync(user);
        var missingClaims = new[]
            {
                new Claim(ClaimTypes.GivenName, givenName),
                new Claim(ClaimTypes.Surname, surname),
                new Claim("display_name", displayName)
            }
            .Where(required => !claims.Any(existing => existing.Type == required.Type))
            .ToList();
        if (missingClaims.Count > 0)
        {
            await userManager.AddClaimsAsync(user, missingClaims);
        }

        if (!string.IsNullOrWhiteSpace(role) && !await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static async Task SeedMerchantApplicationsAsync(
        ApplicationDbContext context,
        string merchantUserId,
        string engineeringApplicantUserId)
    {
        var existingBusinesses = (await context.MerchantApplications
                .AsNoTracking()
                .Select(application => application.BusinessName)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var applications = new[]
        {
            new MerchantApplication
            {
                UserId = merchantUserId,
                BusinessName = "Campus Coffee North",
                ContactName = "Mia Roberts",
                Email = "merchant@test.com",
                Address = "North Campus · Student Commons",
                Status = MerchantApplicationStatuses.Approved,
                SubmittedAtUtc = DemoTimeUtc(2026, 8, 1, 9, 0),
                ReviewedAtUtc = DemoTimeUtc(2026, 8, 2, 10, 0)
            },
            new MerchantApplication
            {
                UserId = engineeringApplicantUserId,
                BusinessName = "Engineering Cafe",
                ContactName = "Aisha Patel",
                Email = "aisha.patel@campuscoffee.local",
                Address = "Engineering Building · Level 1",
                Status = MerchantApplicationStatuses.Pending,
                SubmittedAtUtc = DemoTimeUtc(2026, 8, 10, 8, 40)
            },
            new MerchantApplication
            {
                BusinessName = "Business Cafe",
                ContactName = "Daniel Lee",
                Email = "daniel.lee@campuscoffee.local",
                Address = "Business School · Atrium",
                Status = MerchantApplicationStatuses.Suspended,
                SubmittedAtUtc = DemoTimeUtc(2026, 7, 28, 9, 0),
                ReviewedAtUtc = DemoTimeUtc(2026, 8, 1, 9, 0)
            }
        };

        var missingApplications = applications
            .Where(application => !existingBusinesses.Contains(application.BusinessName))
            .ToList();
        if (missingApplications.Count == 0)
        {
            return;
        }

        context.MerchantApplications.AddRange(missingApplications);
        await context.SaveChangesAsync();
    }

    private static async Task SeedDemoOrdersAsync(ApplicationDbContext context, string customerUserId)
    {
        var existingOrderNumbers = (await context.CustomerOrders
                .AsNoTracking()
                .Select(order => order.OrderNumber)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var products = await context.Products
            .ToDictionaryAsync(product => product.Name, StringComparer.OrdinalIgnoreCase);
        var orders = new[]
        {
            CreateDemoOrder(
                products,
                "#1001",
                customerUserId,
                "Emma",
                "customer@test.com",
                OrderStatuses.Preparing,
                DemoTimeUtc(2026, 8, 10, 9, 20),
                ("Campus Flat White", 5.50m, 2),
                ("Butter Croissant", 5.50m, 1)),
            CreateDemoOrder(
                products,
                "#1002",
                null,
                "Jack",
                "jack.customer@campuscoffee.local",
                OrderStatuses.Ready,
                DemoTimeUtc(2026, 8, 10, 9, 34),
                ("Iced Oat Latte", 6.50m, 1),
                ("Garden Salad Bowl", 12.50m, 1)),
            CreateDemoOrder(
                products,
                "#1004",
                null,
                "Noah",
                "noah.williams@campuscoffee.local",
                OrderStatuses.Pending,
                DemoTimeUtc(2026, 8, 10, 9, 47),
                ("Long Black", 4.50m, 2),
                ("Banana Walnut Loaf", 5.50m, 2))
        };

        var missingOrders = orders
            .Where(order => !existingOrderNumbers.Contains(order.OrderNumber))
            .ToList();
        if (missingOrders.Count == 0)
        {
            return;
        }

        context.CustomerOrders.AddRange(missingOrders);
        await context.SaveChangesAsync();
    }

    private static CustomerOrder CreateDemoOrder(
        IReadOnlyDictionary<string, Product> products,
        string orderNumber,
        string? customerUserId,
        string customerName,
        string email,
        string status,
        DateTime placedAtUtc,
        params (string ProductName, decimal UnitPrice, int Quantity)[] items)
    {
        var orderItems = items.Select(item =>
        {
            var product = products[item.ProductName];
            return new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            };
        }).ToList();
        var subtotal = orderItems.Sum(item => item.UnitPrice * item.Quantity);

        return new CustomerOrder
        {
            OrderNumber = orderNumber,
            CustomerUserId = customerUserId,
            CustomerName = customerName,
            Email = email,
            OrderMethod = "Pickup",
            PickupTime = placedAtUtc.AddHours(12).ToString("HH:mm"),
            Status = status,
            Subtotal = subtotal,
            DeliveryFee = 0,
            Total = subtotal,
            PlacedAtUtc = placedAtUtc,
            Items = orderItems
        };
    }

    private static DateTime DemoTimeUtc(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(12)).UtcDateTime;

    private static Product CreateProduct(
        int sortOrder,
        string name,
        string category,
        string displayCategory,
        decimal price,
        int stock,
        string imageFile,
        string description,
        string dietary,
        string dietaryCss,
        string allergens) => new()
        {
            Name = name,
            Category = category,
            DisplayCategory = displayCategory,
            Price = price,
            StockQuantity = stock,
            ImagePath = $"/images/menu/{imageFile}",
            Description = description,
            DietaryLabel = dietary,
            DietaryCssClass = dietaryCss,
            AllergenLabel = allergens,
            SortOrder = sortOrder,
            IsActive = true
        };
}

