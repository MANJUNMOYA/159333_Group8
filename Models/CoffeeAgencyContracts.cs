using System.ComponentModel.DataAnnotations;

namespace CampusCoffeeSystem.Models;

public sealed class CoffeeAgencyRequest
{
    [Required, StringLength(800, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed record CoffeeAgencyMessage(string Role, string Text);

public sealed record CoffeeAgencyReply(string? Text, string? Error = null);
