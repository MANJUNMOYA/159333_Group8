using System.Text;
using System.Text.Json;
using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services
{
    public class AiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GetChatResponseAsync(ChatRequestDto request)
        {
            var baseUrl = _configuration["AiSettings:BaseUrl"] ?? "http://localhost:8000";
            var url = $"{baseUrl}/chat";

            var requestBody = new
            {
                user_message = request.UserMessage,
                conversation_history = request.ConversationHistory
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, jsonContent);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            return doc.RootElement.GetProperty("reply").GetString() ?? string.Empty;
        }

        public async Task<string> GetRecommendationAsync(RecommendRequestDto request)
        {
            var baseUrl = _configuration["AiSettings:BaseUrl"] ?? "http://localhost:8000";
            var url = $"{baseUrl}/recommend";

            var requestBody = new
            {
                order_history = request.OrderHistory,
                current_menu = request.CurrentMenu
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(url, jsonContent);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);

            return doc.RootElement.GetProperty("recommendation").GetString() ?? string.Empty;
        }
    }
}