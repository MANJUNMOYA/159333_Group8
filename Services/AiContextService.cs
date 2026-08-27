using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Services;

public sealed class AiContextService(ApplicationDbContext db)
{
    private const string WebsiteBrief = """
        You are the Campus Coffee & Catering coffee agency for a campus café website.
        You may answer questions about public customer workflows: creating an account, confirming an email, signing in,
        browsing the menu, adding items to a cart, placing a pickup or delivery order, and checking an order summary.
        You may recommend only items from the supplied public menu. Be warm, concise, practical, and use English.
        Never claim an order has been placed, access private account data, reveal credentials, or invent ingredients,
        availability, prices, dietary advice, or policies that are not in the supplied context.
        """;

    public async Task<AiUserContext> BuildAsync(string userId, CancellationToken cancellationToken)
    {
        var activities = await db.UserActivities
            .Where(activity => activity.UserId == userId)
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(30)
            .ToListAsync(cancellationToken);

        var history = await db.AiChatMessages
            .Where(message => message.UserId == userId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(12)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new AiConversationTurn(message.Role, message.Content))
            .ToListAsync(cancellationToken);

        var activitySummary = activities.Count == 0
            ? "No recorded menu activity yet. Offer a helpful starter recommendation."
            : string.Join("; ", activities
                .GroupBy(activity => new { activity.ActivityType, activity.ProductName, activity.ProductCategory })
                .OrderByDescending(group => group.Count())
                .Take(8)
                .Select(group => $"{group.Key.ActivityType}: {group.Key.ProductName} ({group.Key.ProductCategory}) ×{group.Count()}"));

        var menuSummary = string.Join("\n", CampusMenuCatalog.Items.Select(item =>
            $"- {item.Name} | {item.Category} | {item.Description}"));

        var systemInstruction = $"{WebsiteBrief}\n\nPublic menu:\n{menuSummary}\n\nCurrent customer's anonymised activity summary:\n{activitySummary}";
        return new AiUserContext(systemInstruction, history, activities);
    }

    public async Task AddActivityAsync(string userId, string activityType, MenuCatalogItem product, CancellationToken cancellationToken)
    {
        if (activityType == "view")
        {
            var recentlyTracked = await db.UserActivities.AnyAsync(activity =>
                activity.UserId == userId && activity.ActivityType == "view" &&
                activity.ProductName == product.Name && activity.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(-30), cancellationToken);

            if (recentlyTracked) return;
        }

        db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            ActivityType = activityType,
            ProductName = product.Name,
            ProductCategory = product.Category,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddChatMessageAsync(string userId, string role, string content, CancellationToken cancellationToken)
    {
        db.AiChatMessages.Add(new AiChatMessage
        {
            UserId = userId,
            Role = role,
            Content = content.Length > 1900 ? content[..1900] : content,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AiConversationTurn(string Role, string Content);
public sealed record AiUserContext(string SystemInstruction, IReadOnlyList<AiConversationTurn> ConversationHistory, IReadOnlyList<UserActivity> Activities);
