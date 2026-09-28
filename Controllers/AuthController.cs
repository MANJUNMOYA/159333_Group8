using System.Security.Claims;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ApplicationDbContext context,
    EmailConfirmationService confirmationService,
    IConfiguration configuration,
    NotificationService notifications) : ControllerBase
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

        // SignInAsync alone bypasses Identity's confirmation and lockout checks.
        var signInResult = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = signInResult.IsNotAllowed
                    ? "Please confirm your email address before signing in. You can request a new confirmation email below."
                    : "Sign in is temporarily unavailable for this account. Please try again later."
            });
        }
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
    [EnableRateLimiting("email")]
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
            EmailConfirmed = false
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

        return await RegistrationResultAsync(user, request.FirstName.Trim());
    }

    [HttpPost("merchants")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("email")]
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
            EmailConfirmed = false
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
        return await RegistrationResultAsync(user, request.ContactName.Trim(), merchant: true);
    }

    [HttpPost("resend-confirmation")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
        {
            await confirmationService.TrySendAsync(user, "Campus Coffee member", ConfirmationPageUrl());
        }
        // Do not reveal whether an account exists.
        return Ok(new { message = "If this address has an unconfirmed account, a confirmation email has been requested. Please check your inbox and spam folder." });
    }

    private string ConfirmationPageUrl()
    {
        var publicUrl = configuration["PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(publicUrl)
            ? Url.Page("/Account/ConfirmEmail", null, new { area = "Identity" }, Request.Scheme)!
            : new Uri(new Uri(publicUrl.TrimEnd('/') + "/"), "Identity/Account/ConfirmEmail").AbsoluteUri;
    }

    private async Task<IActionResult> RegistrationResultAsync(IdentityUser user, string name, bool merchant = false)
    {
        var sent = await confirmationService.TrySendAsync(user, name, ConfirmationPageUrl());
        return Ok(new
        {
            requiresEmailConfirmation = true,
            emailSent = sent,
            message = sent
                ? "Your account has been created. Please check your inbox to confirm your email address."
                : "Your account was created, but the confirmation email could not be sent. Please use Resend confirmation email to try again.",
            redirectUrl = Url.Action("CustomerRegisterConfirmation", "Home", new { email = user.Email, sent, merchant })
        });
    }

    [HttpPost("merchant-applications")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> SubmitMerchantApplication(MerchantApplicationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        var application = await context.MerchantApplications
            .Where(item => item.Email == email && (item.Status == MerchantApplicationStatuses.Pending
                || item.Status == MerchantApplicationStatuses.Draft))
            .OrderByDescending(item => item.SubmittedAtUtc)
            .FirstOrDefaultAsync();

        var wasPending = application?.Status == MerchantApplicationStatuses.Pending;
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
        var delivery = wasPending ? NotificationDelivery.Skipped
            : await notifications.MerchantApplicationSubmittedAsync(application);
        return Ok(new
        {
            message = ("Your application has been submitted and is awaiting administrator approval. "
                + NotificationService.DeliveryMessage(delivery, "A confirmation email has been sent.")).Trim(),
            notificationStatus = delivery.ToString().ToLowerInvariant()
        });
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new { redirectUrl = Url.Action("Portal", "Home") });
    }
}
