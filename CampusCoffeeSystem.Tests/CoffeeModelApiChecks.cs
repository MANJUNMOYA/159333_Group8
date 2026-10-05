using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.RateLimiting;

internal static class CoffeeModelApiChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var fake = new Model();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton<ICoffeeModelClient>(fake);
        builder.Services.Configure<CoffeeModelOptions>(options => { options.Enabled = true; options.PublicApiKey = "test-only-secret"; });
        builder.Services.AddControllers().AddApplicationPart(typeof(CoffeeModelController).Assembly);
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, CoffeeModelKeyHandler>(CoffeeModelKeyHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("coffee-model", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Request.Headers["X-Test-Rate"].ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            async Task<HttpResponseMessage> Send(object body, string? key = "test-only-secret", string rate = "normal")
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/coffee-model/v1/chat/completions")
                { Content = JsonContent.Create(body) };
                if (key is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Headers.Add("X-Test-Rate", rate);
                return await client.SendAsync(request);
            }
            var valid = new { model = "qwen2.5:0.5b-instruct", messages = new[] { new { role = "user", content = "Hello" } }, max_tokens = 100 };
            using (var response = await Send(valid, null, "no-key"))
                check(response.StatusCode == HttpStatusCode.Unauthorized, "Public model API rejects missing bearer credentials");
            using (var response = await Send(valid, "wrong-key", "bad-key"))
                check(response.StatusCode == HttpStatusCode.Unauthorized, "Public model API rejects invalid bearer credentials");
            check(fake.Calls == 0, "Unauthorized requests never invoke inference");
            using (var response = await Send(valid, rate: "success"))
            {
                check(response.IsSuccessStatusCode, "Model API accepts valid authenticated input (HTTP " + (int)response.StatusCode + ")");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                check(response.IsSuccessStatusCode && json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() == "Hello from Qwen.",
                    "Authenticated model API returns a chat-completion response");
                check(fake.MaxTokens == 100, "OpenAI-style max_tokens is parsed and passed to the model");
            }
            using (var response = await Send(new { model = "different-model", messages = valid.messages }, rate: "other-model"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Public API cannot select another model");
            using (var response = await Send(new { messages = valid.messages, stream = true }, rate: "stream"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Unsupported streaming requests are rejected");
            using (var response = await Send(new { messages = Array.Empty<object>() }, rate: "empty"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Empty conversations are rejected");
            using (var response = await Send(new { messages = new[] { new { role = "tool", content = "Hi" } } }, rate: "role"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Unsupported roles are rejected");
            using (var response = await Send(new { messages = valid.messages, max_tokens = 999999 }, rate: "tokens"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Generation limits cannot be increased arbitrarily");
            using (var response = await Send(new { messages = new[] { new { role = "user", content = new string('x', 4001) } } }, rate: "length"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Public message length is bounded");
            using (var response = await Send(new { messages = (object?)null }, rate: "null"))
                check(response.StatusCode == HttpStatusCode.BadRequest, "Null messages fail validation without crashing");
            for (var i = 0; i < 3; i++)
            {
                using var response = await Send(valid, rate: "rate-limit");
                check(response.StatusCode == (i < 2 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests), "Public API rate limiting request " + (i + 1));
            }
            using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/coffee-model/v1/models");
            listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-only-secret");
            listRequest.Headers.Add("X-Test-Rate", "list");
            using var listResponse = await client.SendAsync(listRequest);
            check(listResponse.IsSuccessStatusCode && !(await listResponse.Content.ReadAsStringAsync()).Contains("test-only-secret"),
                "Model discovery returns the configured model without credentials");
        }
        finally { await app.StopAsync(); }
    }

    private sealed class Model : ICoffeeModelClient
    {
        public int Calls { get; private set; }
        public int MaxTokens { get; private set; }
        public Task<CoffeeModelResult> GenerateAsync(IReadOnlyList<CoffeeModelMessage> messages, int maxTokens, CancellationToken cancellationToken)
        {
            Calls++; MaxTokens = maxTokens;
            return Task.FromResult(new CoffeeModelResult("Hello from Qwen.", PromptTokens: 10, CompletionTokens: 5));
        }
    }
}
