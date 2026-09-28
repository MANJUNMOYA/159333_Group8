namespace CampusCoffeeSystem.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool Enabled { get; init; } = true;
    // If blank, application alerts go to the configured website sender mailbox.
    public string MerchantReviewEmail { get; init; } = string.Empty;
}
