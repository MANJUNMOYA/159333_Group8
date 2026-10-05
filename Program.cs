using CampusCoffeeSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<EmailConfirmationService>();
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddScoped<NotificationService>();
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddHttpClient<IGeminiRecommendationClient, GeminiRecommendationClient>();
builder.Services.AddScoped<IMenuRecommendationContextService, MenuRecommendationContextService>();
builder.Services.AddScoped<IMenuRecommendationService, MenuRecommendationService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CoffeeAgencyConversations>();
builder.Services.Configure<CoffeeModelOptions>(builder.Configuration.GetSection(CoffeeModelOptions.SectionName));
builder.Services.AddHttpClient("coffee-model", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<OllamaCoffeeModelClient>();
builder.Services.AddSingleton<RemoteCoffeeModelClient>();
builder.Services.AddSingleton<ICoffeeModelClient>(services =>
{
    var provider = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CoffeeModelOptions>>().Value.Provider;
    return provider.Equals("Local", StringComparison.OrdinalIgnoreCase)
        ? services.GetRequiredService<OllamaCoffeeModelClient>()
        : provider.Equals("Remote", StringComparison.OrdinalIgnoreCase)
            ? services.GetRequiredService<RemoteCoffeeModelClient>()
            : throw new InvalidOperationException("CoffeeModel:Provider must be Local or Remote.");
});
builder.Services.AddScoped<CoffeeAgencyClient>();
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, CoffeeModelKeyHandler>(
    CoffeeModelKeyHandler.SchemeName, _ => { });
// A persistent directory keeps confirmation links valid across application restarts.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("CampusCoffeeSystem");
}

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("coffee-model", _ => RateLimitPartition.GetFixedWindowLimiter(
        "coffee-model", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("coffee-agency", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
    options.AddPolicy("email", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0
        }));
    options.AddPolicy("recommendations", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PlatformRoles.Customer, policy => policy.RequireRole(PlatformRoles.Customer));
    options.AddPolicy(PlatformRoles.Merchant, policy => policy.RequireRole(PlatformRoles.Merchant));
    options.AddPolicy(PlatformRoles.Administrator, policy => policy.RequireRole(PlatformRoles.Administrator));
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Portal";
    options.AccessDeniedPath = "/Home/Portal";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Apache terminates HTTPS on the mini PC; its Docker upstream is loopback HTTP.
if (!app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    await ApplicationSeed.InitialiseAsync(scope.ServiceProvider, app.Configuration);
}

app.Run();
