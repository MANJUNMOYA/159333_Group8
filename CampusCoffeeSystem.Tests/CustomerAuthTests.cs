using System.Security.Claims;
using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusCoffeeSystem.Tests;

public class CustomerAuthTests
{
    private class TestEnvironment : IAsyncDisposable
    {
        public ServiceProvider Services { get; }
        public ApplicationDbContext Context { get; }
        public UserManager<IdentityUser> UserManager { get; }
        public SignInManager<IdentityUser> SignInManager { get; }

        public TestEnvironment()
        {
            var services = new ServiceCollection();

            services.AddLogging();

            // Authentication is required by SignInManager.
            services.AddAuthentication();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services
                .AddIdentityCore<IdentityUser>(options =>
                {
                    // Simple password rules for test accounts.
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 6;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager();

            Services = services.BuildServiceProvider();

            Context = Services.GetRequiredService<ApplicationDbContext>();
            UserManager = Services.GetRequiredService<UserManager<IdentityUser>>();
            SignInManager = Services.GetRequiredService<SignInManager<IdentityUser>>();

            CreateCustomerRole().GetAwaiter().GetResult();
        }

        private async Task CreateCustomerRole()
        {
            var roleManager =
                Services.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(PlatformRoles.Customer))
            {
                await roleManager.CreateAsync(
                    new IdentityRole(PlatformRoles.Customer));
            }
        }

        public AuthController CreateController()
        {
            var controller = new AuthController(
                UserManager,
                SignInManager,
                Context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = Services
                }
            };

            return controller;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Services.DisposeAsync();
        }
    }

    private static CustomerRegistrationRequest CreateRegistrationRequest(
        string email = "customer159333@test.com")
    {
        return new CustomerRegistrationRequest
        {
            FirstName = "Test",
            LastName = "Customer",
            Email = email,
            Phone = "0212345678",
            Password = "Test123!"
        };
    }

    [Fact]
    public async Task RegisterCustomer_ValidRequest_CreatesCustomer()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var request = CreateRegistrationRequest();

        var result = await controller.RegisterCustomer(request);

        Assert.IsType<OkObjectResult>(result);

        var user = await environment.UserManager.FindByEmailAsync(
            "customer159333@test.com");

        Assert.NotNull(user);
        Assert.Equal("customer159333@test.com", user.Email);
        Assert.Equal("0212345678", user.PhoneNumber);
    }

    [Fact]
    public async Task RegisterCustomer_EmailIsConvertedToLowerCase()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var request = CreateRegistrationRequest(
            "CUSTOMER159333@TEST.COM");

        var result = await controller.RegisterCustomer(request);

        Assert.IsType<OkObjectResult>(result);

        var user = await environment.UserManager.FindByEmailAsync(
            "customer159333@test.com");

        Assert.NotNull(user);
        Assert.Equal("customer159333@test.com", user.Email);
    }

    [Fact]
    public async Task RegisterCustomer_ValidRequest_AddsCustomerRole()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var request = CreateRegistrationRequest();

        await controller.RegisterCustomer(request);

        var user = await environment.UserManager.FindByEmailAsync(
            "customer159333@test.com");

        Assert.NotNull(user);

        var isCustomer = await environment.UserManager.IsInRoleAsync(
            user,
            PlatformRoles.Customer);

        Assert.True(isCustomer);
    }

    [Fact]
    public async Task RegisterCustomer_ValidRequest_AddsNameClaims()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var request = CreateRegistrationRequest();

        await controller.RegisterCustomer(request);

        var user = await environment.UserManager.FindByEmailAsync(
            "customer159333@test.com");

        Assert.NotNull(user);

        var claims = await environment.UserManager.GetClaimsAsync(user);

        Assert.Contains(
            claims,
            claim =>
                claim.Type == ClaimTypes.GivenName &&
                claim.Value == "Test");

        Assert.Contains(
            claims,
            claim =>
                claim.Type == ClaimTypes.Surname &&
                claim.Value == "Customer");

        Assert.Contains(
            claims,
            claim =>
                claim.Type == "display_name" &&
                claim.Value == "Test Customer");
    }

    [Fact]
    public async Task RegisterCustomer_DuplicateEmail_ReturnsConflict()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var firstRequest = CreateRegistrationRequest();

        await controller.RegisterCustomer(firstRequest);

        var duplicateRequest = CreateRegistrationRequest();

        var result = await controller.RegisterCustomer(duplicateRequest);

        Assert.IsType<ConflictObjectResult>(result);

        var users = await environment.Context.Users
            .Where(user =>
                user.Email == "customer159333@test.com")
            .ToListAsync();

        Assert.Single(users);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        await controller.RegisterCustomer(
            CreateRegistrationRequest());

        var request = new LoginRequest
        {
            Role = PlatformRoles.Customer,
            Identifier = "customer159333@test.com",
            Password = "WrongPassword!"
        };

        var result = await controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        await using var environment = new TestEnvironment();
        var controller = environment.CreateController();

        var request = new LoginRequest
        {
            Role = PlatformRoles.Customer,
            Identifier = "notexist159333@test.com",
            Password = "Test123!"
        };

        var result = await controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}