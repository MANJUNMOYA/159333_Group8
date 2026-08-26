using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services
{
    public interface IAiService
    {
        Task<string> GetChatResponseAsync(ChatRequestDto request);
        Task<string> GetRecommendationAsync(RecommendRequestDto request);
    }
}