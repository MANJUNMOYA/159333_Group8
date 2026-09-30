using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Services;

public sealed class MenuRecommendationContextService(ApplicationDbContext context)
    : IMenuRecommendationContextService
{
    public async Task<MenuRecommendationContext> BuildAsync(
        string userId,
        string? email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var candidates = await context.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.StockQuantity > 0)
            .OrderBy(product => product.SortOrder)
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Category,
                product.DisplayCategory,
                product.Description,
                product.Price,
                product.DietaryLabel,
                product.AllergenLabel,
                product.SortOrder
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new MenuRecommendationContext();
        }

        var candidateIds = candidates.Select(candidate => candidate.Id).ToList();
        var purchaseRows = await context.OrderItems
            .AsNoTracking()
            .Where(item =>
                item.CustomerOrder.Status != OrderStatuses.Cancelled &&
                (item.CustomerOrder.CustomerUserId == userId ||
                 (item.CustomerOrder.CustomerUserId == null &&
                  normalizedEmail != string.Empty &&
                  item.CustomerOrder.Email == normalizedEmail)))
            .GroupBy(item => new { item.ProductId, item.ProductName })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                TotalQuantity = group.Sum(item => item.Quantity),
                OrderCount = group.Select(item => item.CustomerOrderId).Distinct().Count(),
                LastPurchasedAtUtc = group.Max(item => item.CustomerOrder.PlacedAtUtc)
            })
            .ToListAsync(cancellationToken);

        var purchasedProductIds = purchaseRows.Select(item => item.ProductId).Distinct().ToList();
        var purchaseCategories = purchasedProductIds.Count == 0
            ? new Dictionary<int, string>()
            : await context.Products
                .AsNoTracking()
                .Where(product => purchasedProductIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, product => product.Category, cancellationToken);

        var userRatings = await context.ProductReviewRatings
            .AsNoTracking()
            .Where(rating =>
                rating.Review.UserId == userId &&
                rating.Review.Status == ReviewStatuses.Active)
            .GroupBy(rating => new
            {
                rating.ProductId,
                rating.Product.Name,
                rating.Product.Category
            })
            .Select(group => new RecommendationRating
            {
                ProductId = group.Key.ProductId,
                ProductName = group.Key.Name,
                Category = group.Key.Category,
                AverageRating = group.Average(rating => (double)rating.Rating),
                RatingCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        var globalRatings = await context.ProductReviewRatings
            .AsNoTracking()
            .Where(rating =>
                candidateIds.Contains(rating.ProductId) &&
                rating.Review.Status == ReviewStatuses.Active)
            .GroupBy(rating => rating.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                AverageRating = group.Average(rating => (double)rating.Rating),
                ReviewCount = group.Count()
            })
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);

        var popularity = await context.OrderItems
            .AsNoTracking()
            .Where(item =>
                candidateIds.Contains(item.ProductId) &&
                item.CustomerOrder.Status != OrderStatuses.Cancelled)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                TotalQuantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(item => item.ProductId, item => item.TotalQuantity, cancellationToken);

        return new MenuRecommendationContext
        {
            PurchaseHistory = purchaseRows
                .Select(item => new RecommendationPurchase
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Category = purchaseCategories.GetValueOrDefault(item.ProductId, string.Empty),
                    TotalQuantity = item.TotalQuantity,
                    OrderCount = item.OrderCount,
                    LastPurchasedAtUtc = item.LastPurchasedAtUtc
                })
                .OrderByDescending(item => item.LastPurchasedAtUtc)
                .ToList(),
            UserRatings = userRatings,
            Candidates = candidates.Select(candidate =>
            {
                globalRatings.TryGetValue(candidate.Id, out var rating);
                return new RecommendationCandidate
                {
                    ProductId = candidate.Id,
                    ProductName = candidate.Name,
                    Category = candidate.Category,
                    DisplayCategory = candidate.DisplayCategory,
                    Description = candidate.Description,
                    Price = candidate.Price,
                    DietaryLabel = candidate.DietaryLabel,
                    AllergenLabel = candidate.AllergenLabel,
                    AverageRating = rating?.AverageRating ?? 0,
                    ReviewCount = rating?.ReviewCount ?? 0,
                    PopularityQuantity = popularity.GetValueOrDefault(candidate.Id),
                    SortOrder = candidate.SortOrder
                };
            }).ToList()
        };
    }
}
