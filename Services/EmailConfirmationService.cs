using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace CampusCoffeeSystem.Services;

public sealed class EmailConfirmationService(
    UserManager<IdentityUser> userManager,
    IEmailSender emailSender,
    ILogger<EmailConfirmationService> logger)
{
    public async Task<bool> TrySendAsync(IdentityUser user, string displayName, string confirmationPageUrl)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = QueryHelpers.AddQueryString(confirmationPageUrl,
                new Dictionary<string, string?> { ["userId"] = user.Id, ["code"] = code });
            await emailSender.SendEmailAsync(user.Email!,
                "Welcome to Campus Coffee & Catering — Confirm your account",
                BuildConfirmationEmail(displayName, callbackUrl));
            return true;
        }
        catch (Exception exception) when (exception is System.Net.Mail.SmtpException
            or InvalidOperationException or ArgumentException or OperationCanceledException)
        {
            // Keep the unconfirmed account so the user can retry delivery.
            logger.LogWarning("Confirmation delivery failed ({ErrorType}).", exception.GetType().Name);
            return false;
        }
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

}
