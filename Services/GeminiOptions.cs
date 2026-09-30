namespace CampusCoffeeSystem.Services;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.1-flash-lite";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public int TimeoutSeconds { get; set; } = 12;
}
