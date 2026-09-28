using System.Net;
using System.Net.Mail;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

// Email verification and notification integration checks. No real email is sent.
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
services.Configure<SmtpOptions>(options => { });
services.AddSingleton(Options.Create(new SmtpOptions
{
    Host = "smtp.example.test", Username = "sender@example.test", Password = "test-only",
    FromEmail = "sender@example.test"
}));
services.Configure<NotificationOptions>(options => { });
services.AddScoped<NotificationService>();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;
var db = sp.GetRequiredService<ApplicationDbContext>();
var users = sp.GetRequiredService<UserManager<IdentityUser>>();
var signIn = sp.GetRequiredService<SignInManager<IdentityUser>>();
var accessor = sp.GetRequiredService<IHttpContextAccessor>();
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
    return new AuthController(users, signIn, db, sp.GetRequiredService<EmailConfirmationService>(), config,
        sp.GetRequiredService<NotificationService>())
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        Url = new TestUrlHelper()
    };
}
OrdersController Orders(NotificationService? notifications = null) => new(db,
    notifications ?? sp.GetRequiredService<NotificationService>())
{
    ControllerContext = new ControllerContext { HttpContext = NewRequest() }, Url = new TestUrlHelper()
};
PlatformController Platform() => new(db, users, new TestEnvironment(), NullLogger<PlatformController>.Instance,
    sp.GetRequiredService<NotificationService>())
{
    ControllerContext = new ControllerContext { HttpContext = NewRequest() }, Url = new TestUrlHelper()
};
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
    await db.Database.MigrateAsync();
    await ApplicationSeed.InitialiseAsync(sp, config);
    var legacy = new IdentityUser { UserName = "legacy@example.test", Email = "legacy@example.test", EmailConfirmed = true };
    Check((await users.CreateAsync(legacy, "DemoPass1!")).Succeeded, "Create account for cross-account token check");

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
    Check(!await users.IsInRoleAsync(merchant, PlatformRoles.Customer), "Pending merchant does not gain Customer access");

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

    var notifications = sp.GetRequiredService<NotificationService>();
    var product = await db.Products.FirstAsync(item => item.StockQuantity >= 5);
    var beforeStock = product.StockQuantity;
    PlaceOrderRequest OrderRequest() => new()
    {
        Name = "<script>Customer</script>", Email = "order@example.test", Phone = "021000002",
        OrderMethod = "Delivery", AddressLine1 = "Private street", City = "Auckland", Postcode = "1010",
        Items = [new PlaceOrderItemRequest { ProductId = product.Id, Quantity = 1 }]
    };
    var baselineMailCount = mail.Messages.Count;
    mail.BeforeSend = async (_, subject, _) =>
    {
        if (!subject.Contains("Order cc_")) return;
        await using var independentDb = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
        Check(await independentDb.CustomerOrders.AnyAsync(item => item.Email == "order@example.test"),
            "Order is committed and readable on a separate connection before notification");
    };
    var placed = (OkObjectResult)await Orders().PlaceOrder(OrderRequest());
    mail.BeforeSend = null;
    var placedResult = JsonSerializer.SerializeToElement(placed.Value);
    var number = placedResult.GetProperty("orderNumber").GetString();
    Check(placedResult.GetProperty("notificationStatus").GetString() == "sent"
        && mail.Messages.Count == baselineMailCount + 1 && mail.LastRecipient == "order@example.test",
        "Successful order sends exactly one notification to the checkout email");
    Check(mail.LastBody.Contains(number!) && mail.LastBody.Contains(product.Name)
        && mail.LastBody.Contains("NZ$") && mail.LastBody.Contains("Delivery")
        && mail.LastBody.Contains("&lt;script&gt;") && !mail.LastBody.Contains("<script>")
        && !mail.LastBody.Contains("Private street"), "Order mail includes receipt details and encodes text without exposing the address");
    Check(product.StockQuantity == beforeStock - 1, "Order notification does not change stock handling");

    baselineMailCount = mail.Messages.Count;
    var invalidOrder = OrderRequest();
    invalidOrder.OrderMethod = "Invalid";
    Check(await Orders().PlaceOrder(invalidOrder) is BadRequestObjectResult
        && mail.Messages.Count == baselineMailCount, "Rejected order sends no email");
    mail.Fail = true;
    var failedMailOrder = (OkObjectResult)await Orders().PlaceOrder(OrderRequest());
    Check(JsonSerializer.SerializeToElement(failedMailOrder.Value).GetProperty("notificationStatus").GetString() == "failed"
        && await db.CustomerOrders.CountAsync() == 2, "SMTP failure preserves a successful order and reports failed delivery");
    mail.Fail = false;

    var unavailable = new NotificationService(mail, Options.Create(new SmtpOptions()),
        Options.Create(new NotificationOptions()), config, NullLogger<NotificationService>.Instance);
    baselineMailCount = mail.Messages.Count;
    var noMailOrder = (OkObjectResult)await Orders(unavailable).PlaceOrder(OrderRequest());
    Check(JsonSerializer.SerializeToElement(noMailOrder.Value).GetProperty("notificationStatus").GetString() == "unavailable"
        && mail.Messages.Count == baselineMailCount && await db.CustomerOrders.CountAsync() == 3,
        "Missing SMTP skips notification while preserving checkout");
    var disabled = new NotificationService(mail, sp.GetRequiredService<IOptions<SmtpOptions>>(),
        Options.Create(new NotificationOptions { Enabled = false }), config, NullLogger<NotificationService>.Instance);
    Check(await disabled.OrderConfirmedAsync(await db.CustomerOrders.FirstAsync()) == NotificationDelivery.Unavailable
        && mail.Messages.Count == baselineMailCount, "Disabled notifications never call the mail sender");

    var applicationRequest = new MerchantApplicationRequest
    {
        BusinessName = "<b>Test Cafe</b>", ContactName = "Merchant", Email = merchant.Email!,
        Phone = "021000003", Address = "Private business address", Description = "PRIVATE_DESCRIPTION", Reason = "PRIVATE_REASON"
    };
    var draftId = await db.MerchantApplications.Where(item => item.UserId == merchant.Id).Select(item => item.Id).SingleAsync();
    var submitted = (OkObjectResult)await Auth().SubmitMerchantApplication(applicationRequest);
    var application = await db.MerchantApplications.SingleAsync(item => item.UserId == merchant.Id);
    Check(application.Id == draftId && application.Status == MerchantApplicationStatuses.Pending,
        "Submitting a merchant draft reuses the application and persists Pending");
    Check(JsonSerializer.SerializeToElement(submitted.Value).GetProperty("notificationStatus").GetString() == "sent"
        && mail.Messages.Count == baselineMailCount + 2
        && mail.Messages[^2].Email == merchant.Email && mail.Messages[^1].Email == "sender@example.test",
        "Application sends applicant acknowledgement and a review alert to the website mailbox");
    Check(mail.LastBody.Contains("&lt;b&gt;") && !mail.LastBody.Contains("<b>Test Cafe</b>")
        && !mail.LastBody.Contains("PRIVATE_DESCRIPTION") && !mail.LastBody.Contains("PRIVATE_REASON"),
        "Review alert encodes business names and leaves private application details in the portal");
    baselineMailCount = mail.Messages.Count;
    await Auth().SubmitMerchantApplication(applicationRequest);
    Check(mail.Messages.Count == baselineMailCount, "Updating an already pending application does not resend notifications");

    var approved = (OkObjectResult)await Platform().UpdateMerchantApplication(application.Id,
        new StatusUpdateRequest { Status = "Approved" });
    Check(application.Status == MerchantApplicationStatuses.Approved
        && await users.IsInRoleAsync(merchant, PlatformRoles.Merchant)
        && JsonSerializer.SerializeToElement(approved.Value).GetProperty("notificationStatus").GetString() == "sent"
        && mail.LastRecipient == merchant.Email && mail.LastBody.Contains("has been approved"),
        "Approval persists role and status before sending the applicant welcome email");
    baselineMailCount = mail.Messages.Count;
    await Platform().UpdateMerchantApplication(application.Id, new StatusUpdateRequest { Status = "Approved" });
    Check(mail.Messages.Count == baselineMailCount, "Repeated approval does not send a duplicate email");
    Check(await Platform().UpdateMerchantApplication(application.Id, new StatusUpdateRequest { Status = "Invalid" })
        is BadRequestObjectResult && mail.Messages.Count == baselineMailCount, "Invalid decision sends no notification");
    mail.Fail = true;
    var rejected = (OkObjectResult)await Platform().UpdateMerchantApplication(application.Id,
        new StatusUpdateRequest { Status = "Rejected" });
    Check(application.Status == MerchantApplicationStatuses.Rejected
        && !await users.IsInRoleAsync(merchant, PlatformRoles.Merchant)
        && JsonSerializer.SerializeToElement(rejected.Value).GetProperty("notificationStatus").GetString() == "failed",
        "Email failure does not undo a saved review decision or role change");
    mail.Fail = false;
    Check(await notifications.MerchantDecisionAsync(application) == NotificationDelivery.Sent
        && mail.LastBody.Contains("has not been approved"), "Rejection message is distinct from approval");

    var customReview = new NotificationService(mail, sp.GetRequiredService<IOptions<SmtpOptions>>(),
        Options.Create(new NotificationOptions { MerchantReviewEmail = "review@example.test" }), config,
        NullLogger<NotificationService>.Instance);
    await customReview.MerchantApplicationSubmittedAsync(application);
    Check(mail.LastRecipient == "review@example.test", "Configured reviewer mailbox overrides sender mailbox");
    mail.Timeout = true;
    Check(await notifications.OrderConfirmedAsync(await db.CustomerOrders.FirstAsync()) == NotificationDelivery.Failed,
        "Mail timeout is contained as a notification failure");
    mail.Timeout = false;
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
    public bool Timeout;
    public List<(string Email, string Subject, string Body)> Messages { get; } = [];
    public Func<string, string, string, Task>? BeforeSend;
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (Fail) throw new SmtpException("Simulated delivery failure");
        if (Timeout) throw new TaskCanceledException("Simulated timeout");
        if (BeforeSend is not null) await BeforeSend(email, subject, htmlMessage);
        LastBody = htmlMessage; LastRecipient = email;
        Messages.Add((email, subject, htmlMessage));
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
