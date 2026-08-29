using System.Text;
using System.Text.Json;
using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Services
{
    public sealed class AiService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<OllamaOptions> options, ILogger<AiService> logger) : IAiService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly OllamaOptions _options = options.Value;
        private readonly ILogger<AiService> _logger = logger;

        public async Task<string> GenerateAsync(string systemInstruction, IReadOnlyList<AiConversationTurn> conversation, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.Model))
            {
                throw new AiConfigurationException("The local coffee agency is not configured yet.");
            }

            var messages = new List<object>
            {
                new { role = "system", content = systemInstruction }
            };
            messages.AddRange(conversation.Select(turn => new
            {
                role = turn.Role == "model" ? "assistant" : "user",
                content = turn.Content
            }));

            var payload = new
            {
                model = _options.Model,
                messages,
                stream = false,
                options = new
                {
                    temperature = 0.25,
                    num_predict = 260
                },
                keep_alive = "10m"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned status {StatusCode}.", (int)response.StatusCode);
                throw new AiUnavailableException("The local coffee agency is starting or temporarily unavailable. Please try again shortly.");
            }

            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
            {
                throw new AiUnavailableException("The coffee agency could not prepare a response. Please try again.");
            }

            var answer = content.GetString()?.Trim();
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
