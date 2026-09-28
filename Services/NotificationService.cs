using System.Globalization;
using System.Net.Mail;
using System.Text.Encodings.Web;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace CampusCoffeeSystem.Services;

public enum NotificationDelivery { Sent, Unavailable, Failed, Skipped }

/// <summary>Best-effort transactional mail. Call only after business data has been committed.</summary>
public sealed class NotificationService(
    IEmailSender emailSender,
    IOptions<SmtpOptions> smtp,
    IOptions<NotificationOptions> options,
    IConfiguration configuration,
    ILogger<NotificationService> logger)
{
    public Task<NotificationDelivery> OrderConfirmedAsync(CustomerOrder order)
    {
        var rows = string.Join("", order.Items.Select(item =>
            $"<tr><td style=\"padding:10px 0;border-bottom:1px solid #d8ccbb\">{Encode(item.ProductName)} × {item.Quantity}</td>" +
            $"<td style=\"text-align:right;border-bottom:1px solid #d8ccbb\">{Money(item.UnitPrice * item.Quantity)}</td></tr>"));
        var body = $"""
            <p>Dear {Encode(order.CustomerName)},</p>
            <p>Thank you for choosing Campus Coffee &amp; Catering. We have received your order <strong>{Encode(order.OrderNumber)}</strong>.</p>
            <p>Method: <strong>{Encode(order.OrderMethod)}</strong><br />Status: {Encode(order.Status)}</p>
            <table role="presentation" style="width:100%;border-collapse:collapse">{rows}</table>
            <p>Subtotal: {Money(order.Subtotal)}<br />Delivery: {Money(order.DeliveryFee)}<br /><strong>Total: {Money(order.Total)}</strong></p>
            <p>This confirms receipt of your order; it is not a payment receipt or a notification that your order is ready.</p>
            <p>We appreciate your order and look forward to serving you.</p>
            {Link("Home/OrderHistory", "Sign in to view your orders")}
            """;
        return SendAsync(order.Email, $"Campus Coffee — Order {order.OrderNumber} received",
            "Your order is received", body, "order-confirmation", order.OrderNumber);
    }

    public async Task<NotificationDelivery> MerchantApplicationSubmittedAsync(MerchantApplication application)
    {
        var acknowledgement = await SendAsync(application.Email, "Campus Coffee — Merchant application received",
            "Thank you for applying", $"""
                <p>Dear {Encode(application.ContactName)},</p>
                <p>Thank you for your interest in joining Campus Coffee &amp; Catering.</p>
                <p>We have received application <strong>#{application.Id}</strong> for <strong>{Encode(application.BusinessName)}</strong>. It is now awaiting administrator review.</p>
                <p>We will email you when a decision has been made. Please also confirm your account email if you have not already done so.</p>
                {Link("Home/Portal", "Visit Campus Coffee")}
                """, "merchant-application", application.Id.ToString(CultureInfo.InvariantCulture));

        var reviewEmail = string.IsNullOrWhiteSpace(options.Value.MerchantReviewEmail)
            ? smtp.Value.FromEmail : options.Value.MerchantReviewEmail.Trim();
        await SendAsync(reviewEmail, "Campus Coffee — Merchant application awaiting review",
            "A new merchant application", $"""
                <p>Application <strong>#{application.Id}</strong> is ready for review.</p>
                <p>Business: <strong>{Encode(application.BusinessName)}</strong><br />Contact: {Encode(application.ContactName)}</p>
                <p>Please sign in to the administrator portal to review the application and make a decision.</p>
                {Link("Home/AdministratorLogin", "Review merchant applications")}
                """, "merchant-review-request", application.Id.ToString(CultureInfo.InvariantCulture));
        return acknowledgement;
    }

    public Task<NotificationDelivery> MerchantDecisionAsync(MerchantApplication application)
    {
        if (application.Status is not (MerchantApplicationStatuses.Approved or MerchantApplicationStatuses.Rejected))
            return Task.FromResult(NotificationDelivery.Skipped);

        var approved = application.Status == MerchantApplicationStatuses.Approved;
        var message = approved
            ? "We are delighted to welcome you as a Campus Coffee merchant. Your application has been approved. Please confirm your account email before signing in if you have not already done so."
            : "Thank you for taking the time to apply. After review, your application has not been approved on this occasion. Please contact the Campus Coffee team if you would like to discuss your application.";
        return SendAsync(application.Email,
            approved ? "Campus Coffee — Your merchant application is approved" : "Campus Coffee — Your merchant application update",
            approved ? "Welcome to our merchant community" : "Your application update", $"""
                <p>Dear {Encode(application.ContactName)},</p>
                <p>Application <strong>#{application.Id}</strong> for <strong>{Encode(application.BusinessName)}</strong></p>
                <p>{message}</p>
                {Link("Home/MerchantLogin", "Visit the merchant portal")}
                """, "merchant-decision", application.Id.ToString(CultureInfo.InvariantCulture));
    }

    public static string DeliveryMessage(NotificationDelivery delivery, string sentMessage) => delivery switch
    {
        NotificationDelivery.Sent => sentMessage,
        NotificationDelivery.Failed => "The email notification could not be sent. Your changes have been saved.",
        NotificationDelivery.Unavailable => "Email notifications are currently unavailable.",
        _ => string.Empty
    };

    private async Task<NotificationDelivery> SendAsync(string recipient, string subject, string heading,
        string content, string kind, string reference)
    {
        if (!options.Value.Enabled || !smtp.Value.IsConfigured)
        {
            logger.LogInformation("Notification {Kind} for {Reference} skipped: email service unavailable.", kind, reference);
            return NotificationDelivery.Unavailable;
        }

        try
        {
            await emailSender.SendEmailAsync(recipient, subject, $"""
                <!DOCTYPE html><html lang="en"><body style="margin:0;background:#f2eadc;color:#241713;font-family:Arial,sans-serif">
                <div style="max-width:620px;margin:auto;padding:32px 20px">
                <div style="padding:30px;background:#241713;color:#fff;text-align:center">
                <p style="color:#f1d4bd;font-size:12px;letter-spacing:2px">CAMPUS COFFEE &amp; CATERING</p>
                <h1 style="font-family:Georgia,serif;font-weight:400">{Encode(heading)}</h1></div>
                <div style="padding:30px;background:#fbf8f1;border:1px solid #d8ccbb;line-height:1.7">
                {content}<p>With warm wishes,<br /><strong>The Campus Coffee &amp; Catering Team</strong></p>
                </div></div></body></html>
                """);
            logger.LogInformation("Notification {Kind} for {Reference} accepted by email service.", kind, reference);
            return NotificationDelivery.Sent;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException
            or ArgumentException or OperationCanceledException)
        {
            logger.LogWarning("Notification {Kind} for {Reference} failed ({ErrorType}).", kind, reference, exception.GetType().Name);
            return NotificationDelivery.Failed;
        }
    }

    private string Link(string path, string label)
    {
        // Only configuration can supply the mail link origin; never use the request Host.
        if (!Uri.TryCreate(configuration["PublicBaseUrl"], UriKind.Absolute, out var origin)
            || origin.Scheme is not ("https" or "http")) return string.Empty;
        var url = new Uri(origin, "/" + path).AbsoluteUri;
        return $"<p><a href=\"{Encode(url)}\" style=\"display:inline-block;padding:12px 20px;background:#5c392b;color:#fff;text-decoration:none\">{Encode(label)}</a></p>";
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
    private static string Money(decimal amount) => "NZ$" + amount.ToString("0.00", CultureInfo.InvariantCulture);
}
