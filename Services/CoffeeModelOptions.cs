namespace CampusCoffeeSystem.Services;

public sealed class CoffeeModelOptions
{
    public const string SectionName = "CoffeeModel";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://ollama:11434/";
    public string Model { get; set; } = "qwen2.5:0.5b-instruct";
    public string PublicApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 90;
}
