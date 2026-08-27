using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CampusCoffeeSystem.Controllers;

[AllowAnonymous]
public class PortalAuthController(SignInManager<IdentityUser> signInManager, ILogger<PortalAuthController> logger) : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager = signInManager;
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
}
