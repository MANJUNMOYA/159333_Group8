namespace CampusCoffeeSystem.Models;

public sealed class MenuRecommendationsResponse
{
    public string Source { get; init; } = RecommendationSources.Fallback;
    public bool IsPersonalized { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<MenuRecommendationItemResponse> Items { get; init; } = [];
}

public sealed class MenuRecommendationItemResponse
{
    public int Rank { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public static class RecommendationSources
{
    public const string Gemini = "gemini";
    public const string Fallback = "fallback";
}
