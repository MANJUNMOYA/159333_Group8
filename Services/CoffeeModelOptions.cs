namespace CampusCoffeeSystem.Services;

public sealed class CoffeeModelOptions
{
    public const string SectionName = "CoffeeModel";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Remote";
    public string BaseUrl { get; set; } = "http://ollama:11434/";
    public string RemoteBaseUrl { get; set; } = "https://campuscoffee.duckdns.org/api/coffee-model/v1/";
    public string RemoteApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "qwen2.5:0.5b-instruct";
    public string PublicApiKey { get; set; } = string.Empty;
    public bool PublicApiEnabled { get; set; }
    public int TimeoutSeconds { get; set; } = 90;
}
