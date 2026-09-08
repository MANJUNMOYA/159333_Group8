using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CampusCoffeeSystem.Models;

public sealed class CustomerProfile
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;

    [MaxLength(400)]
    public string? ProfileImagePath { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? Postcode { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool HasDefaultAddress =>
        !string.IsNullOrWhiteSpace(AddressLine1) &&
        !string.IsNullOrWhiteSpace(City) &&
        !string.IsNullOrWhiteSpace(Postcode);
}
