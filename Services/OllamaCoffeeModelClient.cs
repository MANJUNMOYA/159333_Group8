using System.Net.Http.Json;
using System.Text.Json;
using CampusCoffeeSystem.Models;
using Microsoft.Extensions.Options;

namespace CampusCoffeeSystem.Services;

public sealed class OllamaCoffeeModelClient(IHttpClientFactory clients, IOptions<CoffeeModelOptions> options,
    ILogger<OllamaCoffeeModelClient> logger) : ICoffeeModelClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<CoffeeModelResult> GenerateAsync(IReadOnlyList<CoffeeModelMessage> messages,
        int maxTokens, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.Model) ||
            !Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var baseUrl))
            return new(null, "The coffee model is not configured yet.");
        if (!await _gate.WaitAsync(0, cancellationToken))
            return new(null, "The coffee model is busy. Please try again shortly.", Busy: true);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 10, 110)));
            var http = clients.CreateClient("coffee-model");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var payload = new
                {
                    model = config.Model,
                    stream = false,
                    keep_alive = "5m",
                    messages = messages.Select(message => new { role = message.Role, content = message.Content }),
                    options = new
                    {
                        num_ctx = 4096,
                        num_predict = attempt == 0 ? Math.Clamp(maxTokens, 64, 512) : 640,
                        temperature = 0.3,
                        num_thread = 2
                    }
                };
                using var response = await http.PostAsJsonAsync(new Uri(baseUrl, "api/chat"), payload, deadline.Token);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Local coffee model returned HTTP {StatusCode}.", (int)response.StatusCode);
                    return new(null, "The coffee model is temporarily unavailable. Please try again shortly.");
                }
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token);
                var root = json.RootElement;
                var reason = root.TryGetProperty("done_reason", out var finish) ? finish.GetString() : null;
                if (reason == "length" && attempt == 0) continue;
                if (reason != "stop" || !root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True ||
                    !root.TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("content", out var textElement) || textElement.ValueKind != JsonValueKind.String)
                    break;
                var text = textElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length > 6000) break;
                return new(text, PromptTokens: Count(root, "prompt_eval_count"), CompletionTokens: Count(root, "eval_count"));
            }
            return new(null, "The coffee model could not produce a complete answer. Please try a shorter question.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "The coffee model took too long to reply. Please try again.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Local coffee model request failed ({ErrorType}).", exception.GetType().Name);
            return new(null, "The coffee model is temporarily unavailable. Please try again shortly.");
        }
        finally { _gate.Release(); }
    }

    private static int Count(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : 0;
}
