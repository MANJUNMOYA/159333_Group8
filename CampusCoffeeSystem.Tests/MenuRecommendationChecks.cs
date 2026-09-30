using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

internal static class MenuRecommendationChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var personalizedContext = new MenuRecommendationContext
        {
            PurchaseHistory =
            [
                new RecommendationPurchase
                {
                    ProductId = 1,
                    ProductName = "Campus Flat White",
                    Category = "coffee",
                    TotalQuantity = 3,
                    OrderCount = 2,
                    LastPurchasedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            ],
            UserRatings =
            [
                new RecommendationRating
                {
                    ProductId = 1,
                    ProductName = "Campus Flat White",
                    Category = "coffee",
                    AverageRating = 5,
                    RatingCount = 1
                }
            ],
            Candidates = CreateCandidates(5)
        };

        var longReason = new string('x', 300);
        var mixedClient = new StubGeminiClient(
        [
            new GeminiRecommendation { ProductId = 2, Reason = "A model-selected match." },
            new GeminiRecommendation { ProductId = 2, Reason = "Duplicate ID." },
            new GeminiRecommendation { ProductId = 999, Reason = "Unknown ID." },
            new GeminiRecommendation { ProductId = 3, Reason = longReason }
        ]);
        var mixedService = CreateService(personalizedContext, mixedClient);
        var mixed = await mixedService.GetAsync("customer-1", "customer@example.test", CancellationToken.None);

        check(mixed.Source == RecommendationSources.Gemini, "Valid Gemini selections mark the response source as gemini");
        check(mixed.IsPersonalized, "Order or rating history marks recommendations as personalized");
        check(mixed.Items.Count == 5, "Fallback fills missing places to produce five recommendations");
        check(mixed.Items.Select(item => item.ProductId).Distinct().Count() == mixed.Items.Count,
            "Duplicate Gemini product IDs are removed");
        check(mixed.Items.All(item => item.ProductId is >= 1 and <= 5),
            "Unknown Gemini product IDs are rejected");
        check(mixed.Items.Select((item, index) => item.Rank == index + 1).All(value => value),
            "Server assigns contiguous trusted ranks");
        check(mixed.Items.Single(item => item.ProductId == 2).ProductName == "Product 2",
            "Product names come from trusted candidate data");
        check(mixed.Items.Single(item => item.ProductId == 3).Reason.Length <= 180,
            "Overlong Gemini reasons are truncated");

        var fallbackService = CreateService(personalizedContext, new StubGeminiClient(null));
        var fallback = await fallbackService.GetAsync("customer-1", null, CancellationToken.None);
        check(fallback.Source == RecommendationSources.Fallback, "Missing Gemini output uses fallback");
        check(fallback.Items.Count == 5 && fallback.Items[0].ProductId == 1,
            "Fallback uses order and rating history when ranking products");

        var coldStartContext = new MenuRecommendationContext
        {
            Candidates = CreateCandidates(2)
        };
        var coldStart = await CreateService(coldStartContext, new StubGeminiClient(null))
            .GetAsync("customer-2", null, CancellationToken.None);
        check(!coldStart.IsPersonalized && coldStart.Items.Count == 2,
            "Cold-start customers receive available fallback products without false personalization");

        var empty = await CreateService(new MenuRecommendationContext(), new StubGeminiClient(null))
            .GetAsync("customer-3", null, CancellationToken.None);
        check(empty.Items.Count == 0, "No in-stock candidates returns an empty recommendation list");

        const string geminiJson = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "{\"recommendations\":[{\"productId\":4,\"reason\":\"A structured model result.\"}]}" }
                    ]
                  }
                }
              ]
            }
            """;
        var handler = new CapturingGeminiHandler(geminiJson);
        var geminiClient = new GeminiRecommendationClient(
            new HttpClient(handler),
            Options.Create(new GeminiOptions
            {
                Enabled = true,
                ApiKey = "test-key",
                Model = "test-model",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta/",
                TimeoutSeconds = 3
            }),
            NullLogger<GeminiRecommendationClient>.Instance);
        var parsedGemini = await geminiClient.RecommendAsync(personalizedContext, CancellationToken.None);
        check(parsedGemini is [{ ProductId: 4, Reason: "A structured model result." }],
            "Gemini client parses structured recommendation JSON");
        check(handler.ApiKey == "test-key"
            && handler.RequestUri?.AbsolutePath.EndsWith("/models/test-model:generateContent") == true,
            "Gemini client sends the key in the header and targets the configured model");
        using var capturedRequest = JsonDocument.Parse(handler.RequestBody);
        check(capturedRequest.RootElement.GetProperty("generationConfig")
                .GetProperty("responseFormat")
                .GetProperty("text")
                .GetProperty("mimeType")
                .GetString() == "APPLICATION_JSON",
            "Gemini request asks for schema-constrained JSON output");

        Console.WriteLine("All menu recommendation checks passed.");
    }

    private static MenuRecommendationService CreateService(
        MenuRecommendationContext context,
        IGeminiRecommendationClient client) =>
        new(new StubContextService(context), client, NullLogger<MenuRecommendationService>.Instance);

    private static IReadOnlyList<RecommendationCandidate> CreateCandidates(int count) =>
        Enumerable.Range(1, count)
            .Select(id => new RecommendationCandidate
            {
                ProductId = id,
                ProductName = $"Product {id}",
                Category = id <= 3 ? "coffee" : "food",
                DisplayCategory = id <= 3 ? "Coffee" : "Food",
                Description = "Test candidate",
                Price = 5 + id,
                AverageRating = id == 1 ? 4.8 : 4,
                ReviewCount = id,
                PopularityQuantity = count - id,
                SortOrder = id
            })
            .ToList();

    private sealed class StubContextService(MenuRecommendationContext context)
        : IMenuRecommendationContextService
    {
        public Task<MenuRecommendationContext> BuildAsync(
            string userId,
            string? email,
            CancellationToken cancellationToken) => Task.FromResult(context);
    }

    private sealed class StubGeminiClient(IReadOnlyList<GeminiRecommendation>? recommendations)
        : IGeminiRecommendationClient
    {
        public Task<IReadOnlyList<GeminiRecommendation>?> RecommendAsync(
            MenuRecommendationContext context,
            CancellationToken cancellationToken) => Task.FromResult(recommendations);
    }

    private sealed class CapturingGeminiHandler(string responseJson) : HttpMessageHandler
    {
        public string ApiKey { get; private set; } = string.Empty;
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
