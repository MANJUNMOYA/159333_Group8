using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace CampusCoffeeSystem.Models;

public sealed class CateringRequest
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string CustomerUserId { get; set; } = string.Empty;
    public IdentityUser CustomerUser { get; set; } = null!;

    [Required, MaxLength(160)]
    public string CustomerDisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string EventName { get; set; } = string.Empty;

    public DateOnly EventDate { get; set; }
    public TimeOnly EventTime { get; set; }

    [Required, MaxLength(240)]
    public string Location { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int NumberOfGuests { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Budget { get; set; }

    [MaxLength(2000)]
    public string DietaryRequirements { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string AdditionalRequirements { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = CateringRequestStatuses.Pending;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class CateringRequestStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Completed = "Completed";

    public static IReadOnlyList<string> NextStatuses(string status) => status switch
    {
        Pending => [Accepted, Declined],
        Accepted => [Completed, Declined],
        _ => []
    };
}
