using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CampusCoffeeSystem.Models;

public sealed class CoffeeModelRequest : IValidatableObject
{
    public string? Model { get; set; }
    public bool Stream { get; set; }
    [Range(1, 512)]
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 256;
    [Required, MinLength(1), MaxLength(12)]
    public List<CoffeeModelMessage> Messages { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Stream) yield return new("Only non-streaming requests are supported.");
        if (Messages is null) yield break;
        if (Messages.Any(message => message is null ||
            message.Role is not ("system" or "user" or "assistant") || string.IsNullOrWhiteSpace(message.Content)))
            yield return new("Each message must have a supported role and non-empty content.");
        if (Messages.Sum(message => message?.Content?.Length ?? 0) > 12000)
            yield return new("The combined message content is too long.");
        if (Messages.Count > 0 && Messages.Last()?.Role != "user")
            yield return new("The final message must be from the user.");
    }
}

public sealed class CoffeeModelMessage
{
    [Required, StringLength(16)]
    public string Role { get; set; } = string.Empty;
    [Required, StringLength(4000)]
    public string Content { get; set; } = string.Empty;
    public CoffeeModelMessage() { }
    public CoffeeModelMessage(string role, string content) { Role = role; Content = content; }
}

public sealed record CoffeeModelResult(string? Text, string? Error = null,
    int PromptTokens = 0, int CompletionTokens = 0, bool Busy = false);

public interface ICoffeeModelClient
{
    Task<CoffeeModelResult> GenerateAsync(IReadOnlyList<CoffeeModelMessage> messages,
        int maxTokens, CancellationToken cancellationToken);
}
