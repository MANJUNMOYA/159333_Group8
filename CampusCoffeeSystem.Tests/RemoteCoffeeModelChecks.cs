using System.Net;
using System.Text;
using System.Text.Json;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class RemoteCoffeeModelChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var transport = new Transport(Response("stop", "A complete remote answer."));
        var client = Client(transport);
        var result = await client.GenerateAsync([new("user", "Coffee?")], 128, default);
        check(result.Text == "A complete remote answer.", "Remote client parses completed gateway replies");
        check(transport.Url == "https://campuscoffee.duckdns.org/api/coffee-model/v1/chat/completions",
            "Remote client uses the configured public HTTPS chat endpoint");
        check(transport.Authorization == "Bearer test-only-key", "Shared credentials are sent in the authorization header");
        check(!transport.Body!.Contains("test-only-key"), "Shared credentials are excluded from prompts and request bodies");
        using (var json = JsonDocument.Parse(transport.Body))
            check(json.RootElement.GetProperty("max_tokens").GetInt32() == 128 &&
                json.RootElement.GetProperty("model").GetString() == "qwen2.5:0.5b-instruct",
                "Remote requests preserve model selection and generation limits");
        check(result.PromptTokens == 10 && result.CompletionTokens == 5, "Remote usage counters are parsed");
        foreach (var code in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
        {
            var failure = await Client(new Transport(new(code))).GenerateAsync([new("user", "Coffee?")], 128, default);
            check(failure.Text is null && failure.Error is not null, "Remote failure is reported safely: " + (int)code);
            if (code == HttpStatusCode.Unauthorized)
                check(failure.Error!.Contains("revoked"), "A revoked shared key reports a configuration update requirement");
        }
        var partial = await Client(new Transport(Response("length", "Half an answer"))).GenerateAsync([new("user", "Hi")], 128, default);
        check(partial.Text is null, "Incomplete remote replies are not displayed");
        var insecureTransport = new Transport(Response("stop", "Hi"));
        var insecure = Client(insecureTransport, new CoffeeModelOptions
        { Enabled = true, RemoteApiKey = "test-only-key", RemoteBaseUrl = "http://example.test/" });
        check((await insecure.GenerateAsync([new("user", "Hi")], 128, default)).Text is null && insecureTransport.Url is null,
            "Remote credentials are never sent over unencrypted HTTP");
        var missingTransport = new Transport(Response("stop", "Hi"));
        check((await Client(missingTransport, new CoffeeModelOptions { Enabled = true }).GenerateAsync([new("user", "Hi")], 128, default)).Text is null &&
            missingTransport.Url is null, "Missing shared credentials prevent remote requests");
        var invalid = await Client(new Transport(new(HttpStatusCode.OK) { Content = new StringContent("not json") }))
            .GenerateAsync([new("user", "Hi")], 128, default);
        check(invalid.Text is null, "Malformed remote replies fail safely");
        var defaults = LoadProjectOptions();
        check(defaults.Enabled && defaults.Provider == "Remote" && !string.IsNullOrWhiteSpace(defaults.RemoteApiKey),
            "Project defaults enable remote calling without member-specific model setup");
        check(!defaults.PublicApiEnabled && string.IsNullOrEmpty(defaults.PublicApiKey),
            "Ordinary project copies do not expose their own model gateway");
    }

    public static async Task SmokeAsync(Action<bool, string> check)
    {
        var config = LoadProjectOptions();
        var client = new RemoteCoffeeModelClient(new LiveFactory(), Options.Create(config), NullLogger<RemoteCoffeeModelClient>.Instance);
        var reply = await new CoffeeAgencyClient(client).ReplyAsync("What is the available black coffee and its price?", [],
            new MenuRecommendationContext
            {
                Candidates = [new RecommendationCandidate
                { ProductName = "Long Black", Price = 4.20m, Description = "Espresso and hot water.", DietaryLabel = "Vegan", AllergenLabel = "No major allergens" }]
            }, default);
        check(reply.Text is not null, "Default project configuration reaches the live public coffee service: " + (reply.Error ?? "completed"));
        Console.WriteLine("Remote coffee answer: " + reply.Text);
    }

    private static CoffeeModelOptions LoadProjectOptions() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
        .Build().GetSection(CoffeeModelOptions.SectionName).Get<CoffeeModelOptions>()!;

    private static RemoteCoffeeModelClient Client(Transport transport, CoffeeModelOptions? options = null) => new(new Factory(transport),
        Options.Create(options ?? new CoffeeModelOptions { Enabled = true, RemoteApiKey = "test-only-key" }),
        NullLogger<RemoteCoffeeModelClient>.Instance);

    private static HttpResponseMessage Response(string reason, string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            choices = new[] { new { finish_reason = reason, message = new { content = text } } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        }), Encoding.UTF8, "application/json")
    };

    private sealed class Transport(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
    private sealed class Factory(Transport transport) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(transport, disposeHandler: false);
    }
    private sealed class LiveFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = Timeout.InfiniteTimeSpan };
    }
}
