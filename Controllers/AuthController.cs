using System.Security.Claims;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ApplicationDbContext context) : ControllerBase
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var role = PlatformRoles.All.FirstOrDefault(candidate =>
            string.Equals(candidate, request.Role, StringComparison.OrdinalIgnoreCase));
        if (role is null)
        {
            return BadRequest(new { message = "Unknown account type." });
        }

        var email = request.Identifier.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            if (role == PlatformRoles.Merchant)
            {
                var application = await context.MerchantApplications
                    .AsNoTracking()
                    .Where(item => item.Email == email)
                    .OrderByDescending(item => item.SubmittedAtUtc)
                    .FirstOrDefaultAsync();

                var message = application?.Status switch
                {
                    MerchantApplicationStatuses.Draft => "Complete and submit your merchant application before signing in.",
                    MerchantApplicationStatuses.Rejected => "Your merchant application was not approved.",
                    MerchantApplicationStatuses.Suspended => "Your merchant access is suspended.",
                    MerchantApplicationStatuses.Pending => "Your merchant application is awaiting administrator approval.",
                    _ => "This account does not have merchant access."
                };
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = $"This account does not have {role} access." });
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        var redirectUrl = role switch
        {
            PlatformRoles.Administrator => Url.Action("AdministratorDashboard", "Home"),
            PlatformRoles.Merchant => Url.Action("MerchantDashboard", "Home"),
            _ => Url.Action("CustomerDashboard", "Home")
        };

        return Ok(new { redirectUrl });
    }

    [HttpPost("customers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCustomer(CustomerRegistrationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = "An account already exists for this email address." });
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = request.Phone.Trim(),
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        }

        await userManager.AddToRoleAsync(user, PlatformRoles.Customer);
        await userManager.AddClaimsAsync(user,
        [
            new Claim(ClaimTypes.GivenName, request.FirstName.Trim()),
            new Claim(ClaimTypes.Surname, request.LastName.Trim()),
            new Claim("display_name", $"{request.FirstName.Trim()} {request.LastName.Trim()}")
        ]);

        return Ok(new { message = "Your customer account has been created." });
    }

    [HttpPost("merchants")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterMerchant(MerchantRegistrationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { message = "An account already exists for this email address." });
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        }

        await userManager.AddClaimsAsync(user,
        [
            new Claim(ClaimTypes.Name, request.ContactName.Trim()),
            new Claim("display_name", request.BusinessName.Trim())
        ]);

        var application = await context.MerchantApplications
            .Where(item => item.Email == email)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .FirstOrDefaultAsync();

        if (application is null)
        {
            context.MerchantApplications.Add(new MerchantApplication
            {
                UserId = user.Id,
                BusinessName = request.BusinessName.Trim(),
                ContactName = request.ContactName.Trim(),
                Email = email,
                Status = MerchantApplicationStatuses.Draft
            });
        }
        else
        {
            application.UserId = user.Id;
            application.BusinessName = request.BusinessName.Trim();
            application.ContactName = request.ContactName.Trim();

            if (application.Status == MerchantApplicationStatuses.Approved)
            {
                await userManager.AddToRoleAsync(user, PlatformRoles.Merchant);
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { message = "Your merchant account and application have been created." });
    }

    [HttpPost("merchant-applications")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitMerchantApplication(MerchantApplicationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        var application = await context.MerchantApplications
            .Where(item => item.Email == email && item.Status == MerchantApplicationStatuses.Pending)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .FirstOrDefaultAsync();

        if (application is null)
        {
            application = new MerchantApplication { Email = email };
            context.MerchantApplications.Add(application);
        }

        application.UserId = user?.Id;
        application.BusinessName = request.BusinessName.Trim();
        application.ContactName = request.ContactName.Trim();
        application.Phone = request.Phone.Trim();
        application.Address = request.Address.Trim();
        application.Description = request.Description.Trim();
        application.Reason = request.Reason.Trim();
        application.Status = MerchantApplicationStatuses.Pending;
        application.SubmittedAtUtc = DateTime.UtcNow;
        application.ReviewedAtUtc = null;

        await context.SaveChangesAsync();
        return Ok(new { message = "Your application has been submitted and is awaiting administrator approval." });
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new { redirectUrl = Url.Action("Portal", "Home") });
    }
}
