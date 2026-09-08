namespace CampusCoffeeSystem;

public static class ReviewDisplay
{
    public static string PublicCustomerName(Models.OrderReview review) =>
        review.IsAnonymous ? "Anonymous Customer" : review.CustomerDisplayName;

    public static string Rating(decimal rating) =>
        rating.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    public static string Stars(decimal rating)
    {
        var filledStars = Math.Clamp(
            (int)Math.Round(rating, MidpointRounding.AwayFromZero),
            0,
            5);
        return new string('★', filledStars) + new string('☆', 5 - filledStars);
    }
}
