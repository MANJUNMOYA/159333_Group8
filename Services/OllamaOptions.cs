namespace CampusCoffeeSystem.Services;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string Model { get; set; } = "qwen2.5:0.5b-instruct";
}
