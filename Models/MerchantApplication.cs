using System.ComponentModel.DataAnnotations;

namespace CampusCoffeeSystem.Models;

public class MerchantApplication
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required, MaxLength(160)]
    public string BusinessName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(240)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = MerchantApplicationStatuses.Pending;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
}

public static class MerchantApplicationStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Suspended = "Suspended";
}
