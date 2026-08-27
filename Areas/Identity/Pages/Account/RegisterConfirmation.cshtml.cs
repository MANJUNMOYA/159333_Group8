using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CampusCoffeeSystem.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterConfirmationModel : PageModel
{
    public string Email { get; private set; } = string.Empty;
    public string? ReturnUrl { get; private set; }

    public void OnGet(string? email, string? returnUrl = null)
    {
        Email = email ?? string.Empty;
        ReturnUrl = returnUrl;
    }
}
