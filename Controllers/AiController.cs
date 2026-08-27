using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampusCoffeeSystem.Controllers
{
    [ApiController]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [Route("api/[controller]")]
    public class AiController(IAiService aiService, AiContextService contextService, ILogger<AiController> logger) : ControllerBase
    {
        private readonly IAiService _aiService = aiService;
        private readonly AiContextService _contextService = contextService;
        private readonly ILogger<AiController> _logger = logger;

        [HttpPost("chat")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<ChatResponseDto>> Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
        {
            var message = request.UserMessage?.Trim() ?? string.Empty;
            if (message.Length is < 1 or > 800)
            {
                return BadRequest(new ChatResponseDto { Success = false, Message = "Please enter a message of up to 800 characters." });
            }

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            try
            {
                await _contextService.AddChatMessageAsync(userId, "user", message, cancellationToken);
                var context = await _contextService.BuildAsync(userId, cancellationToken);
                var reply = await _aiService.GenerateAsync(context.SystemInstruction, context.ConversationHistory, cancellationToken);
                await _contextService.AddChatMessageAsync(userId, "model", reply, cancellationToken);
                return Ok(new ChatResponseDto { Success = true, Message = reply });
            }
            catch (AiConfigurationException exception)
            {
                return StatusCode(503, new ChatResponseDto { Success = false, Message = exception.Message });
            }
            catch (AiUnavailableException exception)
            {
                return StatusCode(502, new ChatResponseDto { Success = false, Message = exception.Message });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Coffee agency chat failed.");
                return StatusCode(500, new ChatResponseDto { Success = false, Message = "We could not send your message. Please try again." });
            }
        }

        [HttpGet("recommendations")]
        public async Task<ActionResult<ChatResponseDto>> Recommendations(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            try
            {
                var context = await _contextService.BuildAsync(userId, cancellationToken);
                var request = new List<AiConversationTurn>
                {
                    new("user", "Recommend exactly three menu items for this customer. Give each item a short heading and one practical reason. Keep the answer under 130 words.")
                };
                var reply = await _aiService.GenerateAsync(context.SystemInstruction, request, cancellationToken);
                return Ok(new ChatResponseDto { Success = true, Message = reply });
            }
            catch (AiConfigurationException exception)
            {
                return StatusCode(503, new ChatResponseDto { Success = false, Message = exception.Message });
            }
            catch (AiUnavailableException exception)
            {
                return StatusCode(502, new ChatResponseDto { Success = false, Message = exception.Message });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Coffee agency recommendation failed.");
                return StatusCode(500, new ChatResponseDto { Success = false, Message = "Recommendations are unavailable right now." });
            }
        }

        [HttpPost("activity")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activity([FromBody] ActivityRequestDto request, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var activityType = request.ActivityType?.Trim().ToLowerInvariant();
            if (activityType is not ("view" or "purchase")) return BadRequest();

            var product = CampusMenuCatalog.Find(request.ProductName ?? string.Empty);
            if (product is null) return BadRequest();

            await _contextService.AddActivityAsync(userId, activityType, product, cancellationToken);
            return NoContent();
        }

        private string? GetUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
