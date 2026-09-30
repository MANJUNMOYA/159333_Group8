namespace CampusCoffeeSystem.Services;

public sealed class MenuRecommendationContext
{
    public IReadOnlyList<RecommendationPurchase> PurchaseHistory { get; init; } = [];
    public IReadOnlyList<RecommendationRating> UserRatings { get; init; } = [];
    public IReadOnlyList<RecommendationCandidate> Candidates { get; init; } = [];

    public bool HasPersonalHistory => PurchaseHistory.Count > 0 || UserRatings.Count > 0;
}

public sealed class RecommendationPurchase
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int TotalQuantity { get; init; }
    public int OrderCount { get; init; }
    public DateTime LastPurchasedAtUtc { get; init; }
}

public sealed class RecommendationRating
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
}

public sealed class RecommendationCandidate
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DisplayCategory { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string DietaryLabel { get; init; } = string.Empty;
    public string AllergenLabel { get; init; } = string.Empty;
    public double AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public int PopularityQuantity { get; init; }
    public int SortOrder { get; init; }
}

public sealed class GeminiRecommendation
{
    public int ProductId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public interface IMenuRecommendationContextService
{
    Task<MenuRecommendationContext> BuildAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken);
}

public interface IGeminiRecommendationClient
{
    Task<IReadOnlyList<GeminiRecommendation>?> RecommendAsync(
        MenuRecommendationContext context,
        CancellationToken cancellationToken);
}
