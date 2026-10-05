using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CampusCoffeeSystem.Models;
using Microsoft.Extensions.Options;

namespace CampusCoffeeSystem.Services;

public sealed class RemoteCoffeeModelClient(IHttpClientFactory clients, IOptions<CoffeeModelOptions> options,
    ILogger<RemoteCoffeeModelClient> logger) : ICoffeeModelClient
{
    public async Task<CoffeeModelResult> GenerateAsync(IReadOnlyList<CoffeeModelMessage> messages,
        int maxTokens, CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.RemoteApiKey) ||
            !Uri.TryCreate(config.RemoteBaseUrl, UriKind.Absolute, out var address) ||
            address.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            return new(null, "The remote coffee service is not configured yet.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds + 10, 15, 120)));
            var endpoint = new Uri(new Uri(address.AbsoluteUri.TrimEnd('/') + '/'), "chat/completions");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.RemoteApiKey);
            request.Content = JsonContent.Create(new
            {
                model = config.Model,
                stream = false,
                max_tokens = Math.Clamp(maxTokens, 1, 512),
                messages = messages.Select(message => new { role = message.Role, content = message.Content })
            });
            using var response = await clients.CreateClient("coffee-model").SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Remote coffee service returned HTTP {StatusCode}.", (int)response.StatusCode);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return new(null, "The coffee service credentials were rejected or revoked. Please update the project configuration.");
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return new(null, "The coffee service request limit has been reached. Please wait a minute.");
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    return new(null, "The coffee service is busy or unavailable. Please try again shortly.", Busy: true);
                return new(null, "The coffee service is temporarily unavailable. Please try again shortly.");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token);
            var root = json.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return Incomplete();
            var choice = choices[0];
            if (!choice.TryGetProperty("finish_reason", out var finish) || finish.GetString() != "stop" ||
                !choice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                return Incomplete();
            var text = content.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 6000) return Incomplete();
            var prompt = 0;
            var completion = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var p)) p.TryGetInt32(out prompt);
                if (usage.TryGetProperty("completion_tokens", out var c)) c.TryGetInt32(out completion);
            }
            return new(text, PromptTokens: prompt, CompletionTokens: completion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, "The coffee service took too long to reply. Please try again.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Remote coffee service request failed ({ErrorType}).", exception.GetType().Name);
            return new(null, "The coffee service is temporarily unavailable. Please try again shortly.");
        }
    }

    private static CoffeeModelResult Incomplete() => new(null,
        "The coffee service could not produce a complete answer. Please try a shorter question.");
}
