using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CampusCoffeeSystem.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel(UserManager<IdentityUser> userManager) : PageModel
{
    private readonly UserManager<IdentityUser> _userManager = userManager;

    public bool Succeeded { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;
    public string? ReturnUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (userId is null || code is null)
        {
            StatusMessage = "The confirmation link is incomplete or invalid.";
            return Page();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            StatusMessage = "We could not find this account.";
            return Page();
        }

        var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
        Succeeded = result.Succeeded;
        StatusMessage = result.Succeeded
            ? "Your email address has been confirmed. You can now sign in."
            : "We could not confirm this email. Please request a new confirmation link.";
        return Page();
    }
}
