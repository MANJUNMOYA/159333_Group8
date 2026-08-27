using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CampusCoffeeSystem.Controllers;

[AllowAnonymous]
public class PortalAuthController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IUserStore<IdentityUser> userStore,
    IEmailSender emailSender,
    ILogger<PortalAuthController> logger) : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager = signInManager;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserStore<IdentityUser> _userStore = userStore;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly ILogger<PortalAuthController> _logger = logger;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var loginAction = GetLoginAction(input.Portal);
        if (!ModelState.IsValid)
        {
            TempData["AuthError"] = "Enter a valid email address and password.";
            return RedirectToAction(loginAction, "Home");
        }

        var result = await _signInManager.PasswordSignInAsync(
            input.Email, input.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User signed in through the {Portal} portal.", input.Portal);
            return RedirectToAction("Portal", "Home");
        }

        TempData["AuthError"] = result.IsNotAllowed
            ? "Please confirm your email address before signing in."
            : "The email address or password is incorrect.";
        return RedirectToAction(loginAction, "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["RegistrationError"] = "Please complete every field and make sure your passwords match.";
            return RedirectToAction("CustomerRegister", "Home");
        }

        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The configured user store does not support email addresses.");
        }

        var user = new IdentityUser();
        await _userStore.SetUserNameAsync(user, input.Email, CancellationToken.None);
        await ((IUserEmailStore<IdentityUser>)_userStore).SetEmailAsync(user, input.Email, CancellationToken.None);
        var result = await _userManager.CreateAsync(user, input.Password);

        if (!result.Succeeded)
        {
            TempData["RegistrationError"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction("CustomerRegister", "Home");
        }

        await _userManager.SetPhoneNumberAsync(user, input.Phone);
        await _userManager.AddClaimsAsync(user,
        [
            new Claim(ClaimTypes.GivenName, input.FirstName),
            new Claim(ClaimTypes.Surname, input.LastName)
        ]);

        var userId = await _userManager.GetUserIdAsync(user);
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page("/Account/ConfirmEmail", null,
            new { area = "Identity", userId, code }, Request.Scheme);

        await _emailSender.SendEmailAsync(
            input.Email,
            "Welcome to Campus Coffee & Catering — Confirm your account",
            BuildConfirmationEmail(input.FirstName, callbackUrl!));

        _logger.LogInformation("Customer account created for {Email}.", input.Email);
        return RedirectToAction("CustomerRegisterConfirmation", "Home", new { email = input.Email });
    }

    private static string BuildConfirmationEmail(string firstName, string callbackUrl)
    {
        var safeName = HtmlEncoder.Default.Encode(firstName);
        var safeUrl = HtmlEncoder.Default.Encode(callbackUrl);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <body style="margin:0;padding:0;background:#f2eadc;color:#1d1b17;font-family:Arial,sans-serif;">
              <div style="max-width:620px;margin:0 auto;padding:36px 20px;">
                <div style="padding:32px 36px;background:#241713;color:#fff;text-align:center;">
                  <p style="margin:0 0 12px;color:#f1d4bd;font-size:11px;font-weight:700;letter-spacing:2px;text-transform:uppercase;">Campus Coffee &amp; Catering</p>
                  <h1 style="margin:0;font-family:Georgia,serif;font-size:38px;font-weight:400;">Welcome to the community</h1>
                </div>
                <div style="padding:38px 36px;background:#fbf8f1;border:1px solid #d8ccbb;line-height:1.7;">
                  <p style="margin-top:0;font-size:17px;">Dear {{safeName}},</p>
                  <p>Thank you for joining Campus Coffee &amp; Catering. We are delighted to welcome you to our campus community.</p>
                  <p>To complete your registration and begin using your account, please confirm your email address.</p>
                  <p style="margin:32px 0;text-align:center;"><a href="{{safeUrl}}" style="display:inline-block;padding:15px 24px;background:#5c392b;color:#fff;text-decoration:none;font-size:12px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;">Confirm my account</a></p>
                  <p>If you did not create this account, you may safely ignore this email.</p>
                  <p style="margin-bottom:0;">With warm wishes,<br /><strong>The Campus Coffee &amp; Catering Team</strong></p>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static string GetLoginAction(string? portal) => portal?.ToLowerInvariant() switch
    {
        "merchant" => "MerchantLogin",
        "administrator" => "AdministratorLogin",
        _ => "CustomerLogin"
    };

    public class LoginInput
    {
        public string? Portal { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterInput
    {
        [Required, Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
