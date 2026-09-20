using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CampusCoffeeSystem.Areas.Identity.Pages.Account;
using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Self-contained integration checks. No Gmail or Ollama requests are sent.
// Every execution owns a newly generated database; cleanup never targets application data.
var databaseName = "CampusCoffeeIntegration_" + Guid.NewGuid().ToString("N");
var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
{
    DataSource = @"(localdb)\CampusCoffeeIntegration",
    InitialCatalog = databaseName,
    IntegratedSecurity = true,
    TrustServerCertificate = true
}.ConnectionString;
var services = new ServiceCollection();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PublicBaseUrl"] = "https://campuscoffee.test"
}).Build();
var mail = new CapturingEmailSender();
services.AddLogging();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
services.AddDataProtection().UseEphemeralDataProtectionProvider();
services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = true;
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
services.AddSingleton<IEmailSender>(mail);
services.AddScoped<EmailConfirmationService>();
services.AddScoped<AiContextService>();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;
var db = sp.GetRequiredService<ApplicationDbContext>();
var users = sp.GetRequiredService<UserManager<IdentityUser>>();
var signIn = sp.GetRequiredService<SignInManager<IdentityUser>>();
var accessor = sp.GetRequiredService<IHttpContextAccessor>();
var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
DefaultHttpContext NewRequest()
{
    var http = new DefaultHttpContext { RequestServices = sp };
    http.Request.Scheme = "https";
    http.Request.Host = new HostString("campuscoffee.test");
    accessor.HttpContext = http;
    signIn.Context = http;
    return http;
}
AuthController Auth()
{
    var http = NewRequest();
    return new AuthController(users, signIn, db, sp.GetRequiredService<EmailConfirmationService>(), config)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        Url = new TestUrlHelper()
    };
}
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAILED: " + label);
    Console.WriteLine("PASS: " + label);
}
string TokenFromLastEmail()
{
    var match = Regex.Match(WebUtility.HtmlDecode(mail.LastBody), "href=\"([^\"]+)\"");
    return QueryHelpers.ParseQuery(new Uri(match.Groups[1].Value).Query)["code"].ToString();
}
string DecodeToken(string encoded) => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
try
{
    await db.GetService<IMigrator>().MigrateAsync("20260809133612_InitialIdentityBaseline");
    var legacy = new IdentityUser { UserName = "legacy@example.test", Email = "legacy@example.test", EmailConfirmed = true };
    Check((await users.CreateAsync(legacy, "DemoPass1!")).Succeeded, "Create legacy account");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM [__EFMigrationsHistory]");
    var baselinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../deployment/sql/baseline-legacy-identity.sql"));
    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(baselinePath));
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE AiChatMessages (
            Id int IDENTITY PRIMARY KEY, UserId nvarchar(450) NOT NULL,
            Role nvarchar(16) NOT NULL, Content nvarchar(2000) NOT NULL,
            CreatedAt datetimeoffset NOT NULL);
        INSERT INTO AiChatMessages (UserId, Role, Content, CreatedAt)
        VALUES ('legacy-history', 'user', 'Preserve my conversation', SYSDATETIMEOFFSET());
        """);
    await db.Database.MigrateAsync();
    await ApplicationSeed.InitialiseAsync(sp, config);
    Check(await users.IsInRoleAsync(legacy, PlatformRoles.Customer), "Legacy customers gain Customer role");
    Check(await db.AiChatMessages.AnyAsync(x => x.Content == "Preserve my conversation"), "Migrations preserve existing chat records");
    Check(!db.Database.HasPendingModelChanges(), "Migration snapshot matches the combined model");

    var controller = Auth();
    var register = await controller.RegisterCustomer(new CustomerRegistrationRequest
    {
        FirstName = "<script>test</script>", LastName = "Student",
        Email = "customer@example.test", Phone = "021000000", Password = "DemoPass1!"
    });
    Check(register is OkObjectResult, "Customer registration succeeds");
    var customer = (await users.FindByEmailAsync("customer@example.test"))!;
    Check(!customer.EmailConfirmed, "New customer remains unconfirmed");
    Check(customer.PasswordHash != "DemoPass1!" && await users.CheckPasswordAsync(customer, "DemoPass1!"),
        "Identity hashes and verifies the password");
    Check(mail.LastBody.Contains("&lt;script&gt;") && !mail.LastBody.Contains("<script>"), "Email encodes untrusted display names");
    var customerToken = TokenFromLastEmail();
    var login = new LoginRequest { Identifier = customer.Email!, Password = "DemoPass1!", Role = "customer" };
    controller = Auth();
    Check(await controller.Login(login) is ObjectResult { StatusCode: 403 }, "Unconfirmed customer cannot log in");
    Check(!controller.Response.Headers.ContainsKey("Set-Cookie"), "Blocked login does not issue a cookie");
    Check(!(await users.ConfirmEmailAsync(customer, "tampered")).Succeeded, "Tampered token rejected");
    Check(!(await users.ConfirmEmailAsync(legacy, DecodeToken(customerToken))).Succeeded, "Token rejected for a different account");
    var page = new ConfirmEmailModel(users) { PageContext = new PageContext { HttpContext = NewRequest() }, Url = new TestUrlHelper() };
    await page.OnGetAsync(customer.Id, "!");
    Check(!page.Succeeded, "Malformed link handled without crashing");
    await page.OnGetAsync(customer.Id, customerToken);
    Check(page.Succeeded && await users.IsEmailConfirmedAsync(customer), "Actual confirmation page confirms valid token");
    controller = Auth();
    Check(await controller.Login(login) is OkObjectResult, "Confirmed customer can log in");
    Check(controller.Response.Headers.ContainsKey("Set-Cookie"), "Successful login issues Identity cookie");
    controller = Auth();
    Check(await controller.Login(new LoginRequest { Identifier = customer.Email!, Password = "DemoPass1!", Role = "administrator" })
        is ObjectResult { StatusCode: 403 }, "Customer cannot enter Administrator portal");

    controller = Auth();
    await controller.RegisterMerchant(new MerchantRegistrationRequest
    {
        BusinessName = "Test Cafe", ContactName = "Merchant", Email = "merchant@example.test", Password = "DemoPass1!"
    });
    var merchant = (await users.FindByEmailAsync("merchant@example.test"))!;
    Check(!merchant.EmailConfirmed, "New merchant also requires confirmation");
    await users.ConfirmEmailAsync(merchant, DecodeToken(TokenFromLastEmail()));
    Check(await Auth().Login(new LoginRequest { Identifier = merchant.Email!, Password = "DemoPass1!", Role = "merchant" })
        is ObjectResult { StatusCode: 403 }, "Confirmed merchant still requires approval");
    await ApplicationSeed.InitialiseAsync(sp, config);
    Check(!await users.IsInRoleAsync(merchant, PlatformRoles.Customer), "Compatibility backfill does not promote pending merchants");

    mail.Fail = true;
    var deliveryFailure = (OkObjectResult)await Auth().RegisterCustomer(new CustomerRegistrationRequest
    {
        FirstName = "Retry", LastName = "User", Email = "retry@example.test", Phone = "021000001", Password = "DemoPass1!"
    });
    Check(!JsonSerializer.SerializeToElement(deliveryFailure.Value).GetProperty("emailSent").GetBoolean(),
        "SMTP failure reports saved account and failed delivery");
    var retry = (await users.FindByEmailAsync("retry@example.test"))!;
    Check(!retry.EmailConfirmed, "SMTP failure never activates account");
    mail.Fail = false;
    await Auth().ResendConfirmation(new ResendConfirmationRequest { Email = retry.Email! });
    Check(mail.LastRecipient == retry.Email, "Unconfirmed account can request another email");
    var expiryOptions = sp.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>();
    expiryOptions.Value.TokenLifespan = TimeSpan.FromSeconds(-1);
    Check(!(await users.ConfirmEmailAsync(retry, DecodeToken(TokenFromLastEmail()))).Succeeded, "Expired confirmation rejected");
    expiryOptions.Value.TokenLifespan = TimeSpan.FromHours(24);

    db.UserActivities.AddRange(
        new UserActivity { UserId = customer.Id, ActivityType = "view", ProductName = "Long Black", ProductCategory = "Coffee", OccurredAt = DateTimeOffset.UtcNow },
        new UserActivity { UserId = merchant.Id, ActivityType = "view", ProductName = "PRIVATE_OTHER_USER", ProductCategory = "Coffee", OccurredAt = DateTimeOffset.UtcNow });
    var actualProduct = await db.Products.FirstAsync(x => x.StockQuantity > 0);
    db.CustomerOrders.Add(new CustomerOrder
    {
        OrderNumber = "integration-order", CustomerUserId = customer.Id,
        CustomerName = "Private customer name", Email = customer.Email!,
        OrderMethod = "Pickup", Items = [new OrderItem { ProductId = actualProduct.Id, ProductName = actualProduct.Name, Quantity = 2 }]
    });
    await db.SaveChangesAsync();
    var aiContext = await sp.GetRequiredService<AiContextService>().BuildAsync(customer.Id, default);
    Check(aiContext.SystemInstruction.Contains("purchase:") && !aiContext.SystemInstruction.Contains("PRIVATE_OTHER_USER")
        && !aiContext.SystemInstruction.Contains(customer.Email!), "AI uses own persisted orders and omits other users and email fields");
    var aiController = new AiController(new FakeAi(), sp.GetRequiredService<AiContextService>(), db, loggerFactory.CreateLogger<AiController>())
    { ControllerContext = new ControllerContext { HttpContext = NewRequest() } };
    aiController.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customer.Id)], "Test"));
    Check(await aiController.Activity(new ActivityRequestDto { ActivityType = "purchase", ProductName = actualProduct.Name }, default)
        is BadRequestResult, "Browser cannot fabricate purchase history");
    var handler = new FakeOllamaHandler();
    var service = new AiService(new HttpClient(handler) { BaseAddress = new Uri("http://ollama:11434/") },
        Options.Create(new OllamaOptions()), loggerFactory.CreateLogger<AiService>());
    Check(await service.GenerateAsync("Menu rules", [new AiConversationTurn("user", "Recommend coffee")], default) == "Try Long Black.",
        "AI service parses local Ollama response");
    Check(handler.RequestUri == "http://ollama:11434/api/chat" &&
        handler.Payload.Contains("qwen2.5:0.5b-instruct"), "AI request targets expected local model and endpoint");
    handler.Fail = true;
    try { await service.GenerateAsync("", [], default); throw new Exception("Expected model error"); }
    catch (AiUnavailableException) { Console.WriteLine("PASS: Ollama failure becomes controlled application error"); }
    Console.WriteLine("All integration checks passed.");
}
finally
{
    // Both the exact target and unique prefix are verified before deleting this test database.
    if (db.Database.GetDbConnection().Database == databaseName && databaseName.StartsWith("CampusCoffeeIntegration_"))
        await db.Database.EnsureDeletedAsync();
}

sealed class CapturingEmailSender : IEmailSender
{
    public string LastBody = "", LastRecipient = "";
    public bool Fail;
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (Fail) throw new SmtpException("Simulated delivery failure");
        LastBody = htmlMessage; LastRecipient = email;
        return Task.CompletedTask;
    }
}
sealed class TestUrlHelper : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();
    public string? Action(UrlActionContext context) => "/Home/" + context.Action;
    public string? Content(string? path) => path;
    public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true && !url.StartsWith("//");
    public string? Link(string? routeName, object? values) => "https://campuscoffee.test/";
    public string? RouteUrl(UrlRouteContext context) => "/Identity/Account/ConfirmEmail";
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "CampusCoffeeSystem";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class FakeAi : IAiService
{
    public Task<string> GenerateAsync(string systemInstruction, IReadOnlyList<AiConversationTurn> conversation, CancellationToken cancellationToken)
        => Task.FromResult("Test recommendation");
}
sealed class FakeOllamaHandler : HttpMessageHandler
{
    public string RequestUri = "", Payload = "";
    public bool Fail;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        RequestUri = request.RequestUri!.ToString();
        Payload = await request.Content!.ReadAsStringAsync(token);
        return new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        { Content = new StringContent("""{"message":{"content":"Try Long Black."}}""") };
    }
}
