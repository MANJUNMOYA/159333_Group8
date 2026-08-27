using System.Text;
using System.Text.Json;
using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services
{
    public sealed class AiService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<GeminiOptions> options, ILogger<AiService> logger) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly GeminiOptions _options = options.Value;
        private readonly ILogger<AiService> _logger = logger;

        public async Task<string> GenerateAsync(string systemInstruction, IReadOnlyList<AiConversationTurn> conversation, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new AiConfigurationException("The coffee agency is not configured yet.");
            }

            var contents = conversation.Select(turn => new
            {
                role = turn.Role == "model" ? "model" : "user",
                parts = new[] { new { text = turn.Content } }
            });
            var payload = new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents,
                generationConfig = new { temperature = 0.55, maxOutputTokens = 500 }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(_options.Model)}:generateContent")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini returned status {StatusCode}.", (int)response.StatusCode);
                throw new AiUnavailableException("The coffee agency is temporarily unavailable. Please try again shortly.");
            }

            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new AiUnavailableException("The coffee agency could not prepare a response. Please try again.");
            }

            var parts = candidates[0].GetProperty("content").GetProperty("parts")
                .EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text));
            var answer = string.Join("\n", parts).Trim();
            if (string.IsNullOrEmpty(answer))
            {
                throw new AiUnavailableException("The coffee agency could not prepare a response. Please try again.");
            }

            return answer;
        }
    }

    public sealed class AiConfigurationException(string message) : Exception(message);
    public sealed class AiUnavailableException(string message) : Exception(message);
}
