using System.Security.Claims;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusCoffeeSystem.Controllers;

[ApiController, Authorize, Route("api/coffee-agency")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CoffeeAgencyController(CoffeeAgencyConversations conversations,
    IMenuRecommendationContextService contextService, GeminiCoffeeAgencyClient client) : ControllerBase
{
    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var conversation = conversations.Get(userId);
        await conversation.Gate.WaitAsync(cancellationToken);
        try { return Ok(new { messages = conversation.History }); }
        finally { conversation.Gate.Release(); }
    }

    [HttpDelete("history"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var conversation = conversations.Get(userId);
        if (!await conversation.Gate.WaitAsync(0, cancellationToken)) return Conflict(new { message = "Please wait for the current reply." });
        try { conversation.Clear(); return NoContent(); }
        finally { conversation.Gate.Release(); }
    }

    [HttpPost("chat"), ValidateAntiForgeryToken, EnableRateLimiting("coffee-agency")]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Chat(CoffeeAgencyRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Please enter a question." });
        var conversation = conversations.Get(userId);
        if (!await conversation.Gate.WaitAsync(0, cancellationToken)) return Conflict(new { message = "Please wait for the current reply." });
        try
        {
            var context = await contextService.BuildAsync(userId, null, cancellationToken);
            var reply = await client.ReplyAsync(request.Message.Trim(), conversation.History, context, cancellationToken);
            if (reply.Text is null) return StatusCode(503, new { message = reply.Error });
            conversation.Add(request.Message.Trim(), reply.Text);
            return Ok(new { message = reply.Text });
        }
        finally { conversation.Gate.Release(); }
    }
}
