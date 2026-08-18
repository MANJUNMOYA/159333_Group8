using System.Globalization;

namespace CampusCoffeeSystem;

public static class CurrencyFormatter
{
    public static string NzDollars(decimal value) =>
        $"NZ${value.ToString("N2", CultureInfo.InvariantCulture)}";
}
