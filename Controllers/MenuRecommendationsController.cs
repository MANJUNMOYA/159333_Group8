using System.Security.Claims;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/menu-recommendations")]
[Authorize(Roles = PlatformRoles.Customer)]
[EnableRateLimiting("recommendations")]
public sealed class MenuRecommendationsController(IMenuRecommendationService recommendationService)
    : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<MenuRecommendationsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MenuRecommendationsResponse>> GetRecommendations(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var response = await recommendationService.GetAsync(userId, email, cancellationToken);
        return Ok(response);
    }
}
