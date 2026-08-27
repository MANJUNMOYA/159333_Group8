namespace CampusCoffeeSystem.Models;

public sealed class UserActivity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class AiChatMessage
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record MenuCatalogItem(string Name, string Category, string Description);

public static class CampusMenuCatalog
{
    public static readonly IReadOnlyList<MenuCatalogItem> Items =
    [
        new("Campus Flat White", "Coffee", "Double espresso with smooth, velvety steamed milk."),
        new("Long Black", "Coffee", "Double espresso poured over hot water for a clean finish."),
        new("Iced Oat Latte", "Cold coffee", "Espresso, chilled oat milk and ice."),
        new("Dark Chocolate Mocha", "Coffee", "Espresso, dark cocoa and steamed milk."),
        new("Vanilla Cold Brew", "Cold coffee", "Slow-steeped coffee with house vanilla."),
        new("Maple Cinnamon Latte", "Seasonal coffee", "Espresso with maple, cinnamon and steamed milk."),
        new("Butter Croissant", "Bakery", "Flaky, golden and baked fresh."),
        new("Roast Vegetable Focaccia", "Lunch", "Seasonal vegetables, greens and herb dressing in focaccia."),
        new("Egg & Spinach Brioche", "Breakfast", "Free-range egg, spinach and cheddar in toasted brioche."),
        new("Banana Walnut Loaf", "Bakery", "Soft banana loaf with toasted walnuts."),
        new("Garden Salad Bowl", "Lunch", "Seasonal greens, roast vegetables, seeds and lemon herb dressing."),
        new("Mushroom & Thyme Toastie", "Lunch", "Roasted mushrooms, thyme and cheddar pressed until crisp.")
    ];

    public static MenuCatalogItem? Find(string productName) =>
        Items.FirstOrDefault(item => string.Equals(item.Name, productName, StringComparison.OrdinalIgnoreCase));
}
