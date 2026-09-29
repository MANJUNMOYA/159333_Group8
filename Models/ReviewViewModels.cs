using System.ComponentModel.DataAnnotations;

namespace CampusCoffeeSystem.Models;

public sealed class ProductRatingSummary
{
    public decimal AverageRating { get; init; }
    public int ReviewCount { get; init; }
}

public sealed class MenuRecommendationViewModel
{
    public int Rank { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string RecommendationReason { get; init; } = string.Empty;
}

public sealed class MenuViewModel
{
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyDictionary<int, ProductRatingSummary> Ratings { get; init; } =
        new Dictionary<int, ProductRatingSummary>();
    public IReadOnlyList<MenuRecommendationViewModel> Recommendations { get; init; } = [];
}

public sealed class ProductDetailsViewModel
{
    public required Product Product { get; init; }
    public ProductRatingSummary Rating { get; init; } = new();
    public IReadOnlyList<PublicProductReviewViewModel> Reviews { get; init; } = [];
}

public sealed class PublicProductReviewViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class PublicOrderReviewViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public IReadOnlyList<string> ProductNames { get; init; } = [];
    public int Rating { get; init; }
    public string OverallComment { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ReviewsViewModel
{
    public IReadOnlyList<PublicOrderReviewViewModel> Reviews { get; init; } = [];
    public ProductRatingSummary Rating { get; init; } = new();
}

public sealed class OrderHistoryViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<CustomerOrder> Orders { get; init; } = [];
    public IReadOnlySet<int> ReviewedOrderIds { get; init; } = new HashSet<int>();
}

public sealed class LeaveReviewViewModel
{
    public required CustomerOrder Order { get; init; }
    public int? OverallRating { get; init; }
    public string OverallComment { get; init; } = string.Empty;
    public bool IsAnonymous { get; init; }
    public IReadOnlyList<LeaveReviewProductViewModel> Products { get; init; } = [];
}

public sealed class LeaveReviewProductViewModel
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public int? Rating { get; init; }
}

public sealed class LeaveReviewRequest
{
    [Range(1, 5)]
    public int OverallRating { get; set; }

    [Required, MaxLength(2000)]
    public string OverallComment { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }

    public List<ProductReviewRatingInputModel> ProductRatings { get; set; } = [];
}

public sealed class ProductReviewRatingInputModel
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }
}
