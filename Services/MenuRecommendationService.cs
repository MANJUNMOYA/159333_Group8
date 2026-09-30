using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services;

public interface IMenuRecommendationService
{
    Task<MenuRecommendationsResponse> GetAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken);
}

public sealed class MenuRecommendationService(
    IMenuRecommendationContextService contextService,
    IGeminiRecommendationClient geminiClient,
    ILogger<MenuRecommendationService> logger) : IMenuRecommendationService
{
    private const int MaximumRecommendations = 5;
    private const int MaximumReasonLength = 180;

    public async Task<MenuRecommendationsResponse> GetAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken)
    {
        var context = await contextService.BuildAsync(userId, email, cancellationToken);
        var fallback = BuildFallback(context);
        var modelRecommendations = context.Candidates.Count == 0
            ? null
            : await geminiClient.RecommendAsync(context, cancellationToken);
        var selected = ValidateModelRecommendations(modelRecommendations, context, fallback);
        var usedGemini = selected.GeminiItemCount > 0;

        if (usedGemini && selected.GeminiItemCount < selected.Items.Count)
        {
            logger.LogInformation(
                "Gemini supplied {GeminiCount} valid menu recommendations; fallback filled {FallbackCount} places.",
                selected.GeminiItemCount,
                selected.Items.Count - selected.GeminiItemCount);
        }

        return new MenuRecommendationsResponse
        {
            Source = usedGemini ? RecommendationSources.Gemini : RecommendationSources.Fallback,
            IsPersonalized = context.HasPersonalHistory,
            GeneratedAtUtc = DateTime.UtcNow,
            Items = selected.Items
        };
    }

    private static RecommendationSelection ValidateModelRecommendations(
        IReadOnlyList<GeminiRecommendation>? modelRecommendations,
        MenuRecommendationContext context,
        IReadOnlyList<MenuRecommendationItemResponse> fallback)
    {
        var candidates = context.Candidates.ToDictionary(candidate => candidate.ProductId);
        var selectedIds = new HashSet<int>();
        var selected = new List<MenuRecommendationItemResponse>(MaximumRecommendations);
        var geminiItemCount = 0;

        foreach (var recommendation in modelRecommendations ?? [])
        {
            if (selected.Count == MaximumRecommendations ||
                !candidates.TryGetValue(recommendation.ProductId, out var candidate) ||
                !selectedIds.Add(recommendation.ProductId))
            {
                continue;
            }

            selected.Add(new MenuRecommendationItemResponse
            {
                ProductId = candidate.ProductId,
                ProductName = candidate.ProductName,
                Reason = CleanReason(recommendation.Reason, FallbackReason(candidate, context))
            });
            geminiItemCount++;
        }

        foreach (var recommendation in fallback)
        {
            if (selected.Count == MaximumRecommendations)
            {
                break;
            }

            if (selectedIds.Add(recommendation.ProductId))
            {
                selected.Add(recommendation);
            }
        }

        return new RecommendationSelection
        {
            GeminiItemCount = geminiItemCount,
            Items = selected.Select((item, index) => new MenuRecommendationItemResponse
            {
                Rank = index + 1,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Reason = item.Reason
            }).ToList()
        };
    }

    private static IReadOnlyList<MenuRecommendationItemResponse> BuildFallback(
        MenuRecommendationContext context)
    {
        var purchases = context.PurchaseHistory.ToDictionary(item => item.ProductId);
        var ratings = context.UserRatings.ToDictionary(item => item.ProductId);
        var categoryWeights = context.PurchaseHistory
            .Where(item => !string.IsNullOrWhiteSpace(item.Category))
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.TotalQuantity),
                StringComparer.OrdinalIgnoreCase);

        return context.Candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreCandidate(candidate, purchases, ratings, categoryWeights)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.SortOrder)
            .Take(MaximumRecommendations)
            .Select((item, index) => new MenuRecommendationItemResponse
            {
                Rank = index + 1,
                ProductId = item.Candidate.ProductId,
                ProductName = item.Candidate.ProductName,
                Reason = FallbackReason(item.Candidate, context)
            })
            .ToList();
    }

    private static double ScoreCandidate(
        RecommendationCandidate candidate,
        IReadOnlyDictionary<int, RecommendationPurchase> purchases,
        IReadOnlyDictionary<int, RecommendationRating> ratings,
        IReadOnlyDictionary<string, int> categoryWeights)
    {
        var score = candidate.AverageRating * 0.8
            + Math.Log(1 + candidate.PopularityQuantity) * 1.4
            + Math.Min(candidate.ReviewCount, 20) * 0.03;

        if (purchases.TryGetValue(candidate.ProductId, out var purchase))
        {
            score += purchase.TotalQuantity * 4 + purchase.OrderCount * 2;
        }

        if (ratings.TryGetValue(candidate.ProductId, out var rating))
        {
            score += (rating.AverageRating - 3) * 8;
        }

        if (categoryWeights.TryGetValue(candidate.Category, out var categoryWeight))
        {
            score += categoryWeight * 1.25;
        }

        return score;
    }

    private static string FallbackReason(
        RecommendationCandidate candidate,
        MenuRecommendationContext context)
    {
        var rating = context.UserRatings.FirstOrDefault(item => item.ProductId == candidate.ProductId);
        if (rating?.AverageRating >= 4)
        {
            return "You rated this highly, so it is worth enjoying again.";
        }

        if (context.PurchaseHistory.Any(item => item.ProductId == candidate.ProductId))
        {
            return "A familiar choice based on your previous orders.";
        }

        if (context.PurchaseHistory.Any(item =>
                string.Equals(item.Category, candidate.Category, StringComparison.OrdinalIgnoreCase)))
        {
            return $"This matches the {candidate.Category} choices you often order.";
        }

        if (candidate.AverageRating >= 4 && candidate.ReviewCount > 0)
        {
            return "A highly rated choice from the current menu.";
        }

        return "A popular choice that is available on the current menu.";
    }

    private static string CleanReason(string? reason, string fallback)
    {
        var clean = string.Join(' ', (reason ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(clean))
        {
            return fallback;
        }

        return clean.Length <= MaximumReasonLength
            ? clean
            : clean[..(MaximumReasonLength - 1)].TrimEnd() + "…";
    }

    private sealed class RecommendationSelection
    {
        public int GeminiItemCount { get; init; }
        public IReadOnlyList<MenuRecommendationItemResponse> Items { get; init; } = [];
    }
}
