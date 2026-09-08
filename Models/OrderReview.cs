using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CampusCoffeeSystem.Models;

public sealed class OrderReview
{
    public int ReviewId { get; set; }

    public int OrderId { get; set; }
    public CustomerOrder Order { get; set; } = null!;

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;

    [Required, MaxLength(160)]
    public string CustomerDisplayName { get; set; } = string.Empty;

    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Required, MaxLength(2000)]
    public string OverallComment { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = ReviewStatuses.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ProductReviewRating> ProductRatings { get; set; } = [];
}

public sealed class ProductReviewRating
{
    public int ProductReviewRatingId { get; set; }

    public int ReviewId { get; set; }
    public OrderReview Review { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Range(1, 5)]
    public int Rating { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class ReviewStatuses
{
    public const string Active = "Active";
    public const string Hidden = "Hidden";
}
