using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CampusCoffeeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;

        public AiController(IAiService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<ChatResponseDto>> Chat([FromBody] ChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.UserMessage))
            {
                return BadRequest(new ChatResponseDto
                {
                    Success = false,
                    Message = "UserMessage cannot be empty."
                });
            }

            try
            {
                var reply = await _aiService.GetChatResponseAsync(request);

                return Ok(new ChatResponseDto
                {
                    Success = true,
                    Message = reply
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponseDto
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("recommend")]
        public async Task<ActionResult<ChatResponseDto>> Recommend([FromBody] RecommendRequestDto request)
        {
            try
            {
                var reply = await _aiService.GetRecommendationAsync(request);

                return Ok(new ChatResponseDto
                {
                    Success = true,
                    Message = reply
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponseDto
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}