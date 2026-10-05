using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

internal static class MenuRecommendationApiChecks
{
    private const string TestAuthenticationScheme = "RecommendationApiTest";

    public static async Task RunAsync(Action<bool, string> check)
    {
        var recommendationService = new CapturingRecommendationService();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MenuRecommendationsController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton<IMenuRecommendationService>(recommendationService);
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<CoffeeAgencyConversations>();
        builder.Services.AddSingleton<IMenuRecommendationContextService, ChatContext>();
        builder.Services.AddSingleton<ICoffeeModelClient, ChatModel>();
        builder.Services.AddSingleton<CoffeeAgencyClient>();
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(MenuRecommendationsController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
        builder.Services.AddAuthentication(TestAuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationScheme,
                _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("coffee-agency", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Request.Headers["X-Test-Rate-Key"].ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("recommendations", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Request.Headers["X-Test-Rate-Key"].ToString(),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 2,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/test/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Text(tokens.RequestToken ?? string.Empty);
        }).AllowAnonymous();
        app.MapControllers();

        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?
                .Addresses.Single() ?? throw new InvalidOperationException("The API test server did not publish an address.");
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = new CookieContainer()
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/test/antiforgery");
            tokenRequest.Headers.Add("X-Test-User", "customer-1");
            tokenRequest.Headers.Add("X-Test-Role", PlatformRoles.Customer);
            var tokenResponse = await client.SendAsync(tokenRequest);
            var requestToken = await tokenResponse.Content.ReadAsStringAsync();
            check(tokenResponse.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(requestToken),
                "Recommendation API test obtains an antiforgery token");

            using (var request = CreateRequest(requestToken, null, null, "anonymous"))
            using (var response = await client.SendAsync(request))
            {
                check(response.StatusCode == HttpStatusCode.Unauthorized,
                    "Recommendation API rejects unauthenticated requests");
            }

            using (var request = CreateRequest(requestToken, "merchant-1", PlatformRoles.Merchant, "merchant"))
            using (var response = await client.SendAsync(request))
            {
                check(response.StatusCode == HttpStatusCode.Forbidden,
                    "Recommendation API rejects authenticated non-customer roles");
            }

            using (var request = CreateRequest(null, "customer-no-token", PlatformRoles.Customer, "csrf"))
            using (var response = await client.SendAsync(request))
            {
                check(response.StatusCode == HttpStatusCode.BadRequest,
                    "Recommendation API rejects a customer request without an antiforgery token");
            }

            using (var request = CreateRequest(requestToken, "customer-1", PlatformRoles.Customer, "success"))
            using (var response = await client.SendAsync(request))
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var payload = response.IsSuccessStatusCode
                    ? JsonSerializer.Deserialize<MenuRecommendationsResponse>(
                        responseJson,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    : null;
                check(response.StatusCode == HttpStatusCode.OK &&
                      payload?.Items is [{ ProductId: 3 }] &&
                      recommendationService.LastUserId == "customer-1",
                    "Recommendation API returns a validated response for the signed-in customer");
            }

            HttpStatusCode[] rateLimitStatuses = new HttpStatusCode[3];
            for (var index = 0; index < rateLimitStatuses.Length; index++)
            {
                using var request = CreateRequest(
                    requestToken,
                    "customer-1",
                    PlatformRoles.Customer,
                    "rate-limit");
                using var response = await client.SendAsync(request);
                rateLimitStatuses[index] = response.StatusCode;
            }

            check(rateLimitStatuses[0] == HttpStatusCode.OK &&
                  rateLimitStatuses[1] == HttpStatusCode.OK &&
                  rateLimitStatuses[2] == HttpStatusCode.TooManyRequests,
                "Recommendation API returns HTTP 429 after the configured request limit");

            async Task<HttpStatusCode> Chat(string? user, string? token, string message, string rateKey)
            {
                using var request = CreateRequest(token, user, PlatformRoles.Customer, rateKey);
                request.RequestUri = new Uri("/api/coffee-agency/chat", UriKind.Relative);
                request.Content = new StringContent(JsonSerializer.Serialize(new { message }), System.Text.Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request);
                return response.StatusCode;
            }
            check(await Chat(null, null, "Coffee?", "chat-anon") == HttpStatusCode.Unauthorized,
                "Chat API rejects anonymous requests");
            check(await Chat("customer-1", null, "Coffee?", "chat-csrf") == HttpStatusCode.BadRequest,
                "Chat API rejects missing antiforgery tokens");
            check(await Chat("customer-1", requestToken, new string('x', 801), "chat-long") == HttpStatusCode.BadRequest,
                "Chat API enforces the question length limit");
            check(await Chat("customer-1", requestToken, " ", "chat-blank") == HttpStatusCode.BadRequest,
                "Chat API rejects blank questions");
            check(await Chat("customer-1", requestToken, "Coffee?", "chat-success") == HttpStatusCode.OK,
                "Authenticated chat requests reach the shared provider client");
            async Task<int> HistoryCount(string user)
            {
                using var request = CreateRequest(null, user, PlatformRoles.Customer, "chat-history");
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri("/api/coffee-agency/history", UriKind.Relative);
                using var response = await client.SendAsync(request);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return body.RootElement.GetProperty("messages").GetArrayLength();
            }
            check(await HistoryCount("customer-1") == 2 && await HistoryCount("customer-2") == 0,
                "History endpoint never exposes another customer's conversation");
            using (var request = CreateRequest(null, "customer-1", PlatformRoles.Customer, "chat-clear"))
            {
                request.Method = HttpMethod.Delete;
                request.RequestUri = new Uri("/api/coffee-agency/history", UriKind.Relative);
                using var response = await client.SendAsync(request);
                check(response.StatusCode == HttpStatusCode.BadRequest, "Clearing chat requires an antiforgery token");
            }
            using (var request = CreateRequest(requestToken, "customer-1", PlatformRoles.Customer, "chat-clear"))
            {
                request.Method = HttpMethod.Delete;
                request.RequestUri = new Uri("/api/coffee-agency/history", UriKind.Relative);
                using var response = await client.SendAsync(request);
                check(response.StatusCode == HttpStatusCode.NoContent && await HistoryCount("customer-1") == 0,
                    "Authenticated clear removes history through the API");
            }
            var chatStatuses = new List<HttpStatusCode>();
            for (var i = 0; i < 3; i++) chatStatuses.Add(await Chat("customer-1", requestToken, "Coffee?", "chat-limit"));
            check(chatStatuses.SequenceEqual(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }),
                "Chat API enforces its request rate limit");
        }
        finally
        {
            await app.StopAsync();
        }

        Console.WriteLine("All menu recommendation API checks passed.");
    }

    private static HttpRequestMessage CreateRequest(
        string? requestToken,
        string? userId,
        string? role,
        string rateKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/menu-recommendations");
        request.Headers.Add("X-Test-Rate-Key", rateKey);
        if (!string.IsNullOrWhiteSpace(requestToken))
        {
            request.Headers.Add("RequestVerificationToken", requestToken);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            request.Headers.Add("X-Test-User", userId);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            request.Headers.Add("X-Test-Role", role);
        }

        return request;
    }

    private sealed class CapturingRecommendationService : IMenuRecommendationService
    {
        public string? LastUserId { get; private set; }

        public Task<MenuRecommendationsResponse> GetAsync(
            string userId,
            string? email,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            return Task.FromResult(new MenuRecommendationsResponse
            {
                Source = RecommendationSources.Gemini,
                IsPersonalized = true,
                GeneratedAtUtc = DateTime.UtcNow,
                Items =
                [
                    new MenuRecommendationItemResponse
                    {
                        Rank = 1,
                        ProductId = 3,
                        ProductName = "Iced Oat Latte",
                        Reason = "A pipeline-tested recommendation."
                    }
                ]
            });
        }
    }

    private sealed class ChatContext : IMenuRecommendationContextService
    {
        public Task<MenuRecommendationContext> BuildAsync(string userId, string? email, CancellationToken cancellationToken) =>
            Task.FromResult(new MenuRecommendationContext());
    }

    private sealed class ChatModel : ICoffeeModelClient
    {
        public Task<CoffeeModelResult> GenerateAsync(IReadOnlyList<CoffeeModelMessage> messages,
            int maxTokens, CancellationToken cancellationToken) => Task.FromResult(new CoffeeModelResult("Please browse our Menu."));
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Headers["X-Test-User"].ToString();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, userId),
                new(ClaimTypes.Email, $"{userId}@example.test")
            };
            var role = Request.Headers["X-Test-Role"].ToString();
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, TestAuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestAuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
