namespace CampusCoffeeSystem.Models;

public static class PlatformRoles
{
    public const string Customer = "Customer";
    public const string Merchant = "Merchant";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Customer, Merchant, Administrator];
}

