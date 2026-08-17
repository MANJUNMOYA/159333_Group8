using CampusCoffeeSystem.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CampusCoffeeSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


//用户注册后，需要点开邮箱收到的验证链接激活账号，账号才允许登录 本地开发没有配置邮件发送服务，收不到验证邮件，没法手动激活账号 添加RequireConfirmedAccount = false
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

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
