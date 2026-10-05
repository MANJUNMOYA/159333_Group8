using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CampusCoffeeSystem.Controllers;

[ApiController, Route("api/coffee-model/v1")]
[Authorize(AuthenticationSchemes = CoffeeModelKeyHandler.SchemeName)]
[EnableRateLimiting("coffee-model")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CoffeeModelController(ICoffeeModelClient client, IOptions<CoffeeModelOptions> options) : ControllerBase
{
    [HttpGet("models")]
    public IActionResult Models() => Ok(new
    {
        @object = "list", data = new[] { new { id = options.Value.Model, @object = "model", owned_by = "campus-coffee" } }
    });

    [HttpPost("chat/completions"), RequestSizeLimit(24576)]
    public async Task<IActionResult> Chat(CoffeeModelRequest request, CancellationToken cancellationToken)
    {
        if (request.Model is not null && request.Model != options.Value.Model)
            return BadRequest(new { error = new { message = "Only the configured coffee model is available." } });
        var result = await client.GenerateAsync(request.Messages, request.MaxTokens, cancellationToken);
        if (result.Text is null)
            return StatusCode(503, new { error = new { message = result.Error } });
        return Ok(new
        {
            id = "chatcmpl-" + Guid.NewGuid().ToString("N"),
            @object = "chat.completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = options.Value.Model,
            choices = new[] { new { index = 0, message = new { role = "assistant", content = result.Text }, finish_reason = "stop" } },
            usage = new { prompt_tokens = result.PromptTokens, completion_tokens = result.CompletionTokens,
                total_tokens = result.PromptTokens + result.CompletionTokens }
        });
    }
}
