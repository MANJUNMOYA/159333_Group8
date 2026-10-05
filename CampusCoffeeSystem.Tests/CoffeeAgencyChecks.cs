using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class CoffeeAgencyChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var context = new MenuRecommendationContext
        {
            Candidates = [new RecommendationCandidate { ProductId = 1, ProductName = "Long Black", Price = 4.2m }],
            PurchaseHistory = [new RecommendationPurchase { ProductName = "Long Black", TotalQuantity = 2 }]
        };
        var transport = new Transport();
        var client = Client(transport);
        transport.Responses.Enqueue(Response("STOP", "A complete reply.", " With another part."));
        var reply = await client.ReplyAsync("Recommend a drink.", [new("user", "Hi"), new("model", "Hello")], context, default);
        check(reply.Text == "A complete reply.\n With another part.", "Chat joins every text part without cutting the reply");
        check(transport.Uri?.AbsolutePath.EndsWith("/models/test-model:generateContent") == true && transport.Key == "test-key",
            "Chat uses the shared Gemini model endpoint and server-side key header");
        using (var payload = JsonDocument.Parse(transport.Bodies[0]))
        {
            check(payload.RootElement.GetProperty("contents").GetArrayLength() == 3, "Recent history precedes the new question");
            var prompt = payload.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!;
            check(prompt.Contains("Long Black") && !prompt.Contains("Email") && !prompt.Contains("CustomerUserId"),
                "Catalog and anonymous preferences omit account identifiers");
            check(!transport.Bodies[0].Contains("test-key"), "Credentials are not embedded in the prompt");
        }

        var truncation = new Transport();
        truncation.Responses.Enqueue(Response("MAX_TOKENS", "Incomplete"));
        truncation.Responses.Enqueue(Response("STOP", "Complete replacement."));
        reply = await Client(truncation).ReplyAsync("Coffee?", [], context, default);
        check(reply.Text == "Complete replacement." && truncation.Bodies.Count == 2, "Truncated answers retry once instead of being displayed");
        check(truncation.Bodies[1].Contains("4096"), "Truncation retry increases output allowance");

        var incomplete = new Transport();
        incomplete.Responses.Enqueue(Response("MAX_TOKENS", "Half"));
        incomplete.Responses.Enqueue(Response("MAX_TOKENS", "Still half"));
        reply = await Client(incomplete).ReplyAsync("Coffee?", [], context, default);
        check(reply.Text is null && reply.Error!.Contains("complete answer"), "Repeated truncation returns an explicit failure");
        foreach (var reason in new[] { "SAFETY", "RECITATION" })
        {
            var blocked = new Transport();
            blocked.Responses.Enqueue(Response(reason, "Not a usable answer"));
            check((await Client(blocked).ReplyAsync("Coffee?", [], context, default)).Text is null, "Blocked output is not shown: " + reason);
        }
        var rate = new Transport();
        rate.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        reply = await Client(rate).ReplyAsync("Coffee?", [], context, default);
        check(reply.Text is null && reply.Error!.Contains("request limit"), "Provider rate limits have a useful error");
        var invalid = new Transport();
        invalid.Responses.Enqueue(new(HttpStatusCode.OK) { Content = new StringContent("not json") });
        check((await Client(invalid).ReplyAsync("Coffee?", [], context, default)).Text is null, "Malformed provider output fails safely");
        var disabled = new Transport();
        var disabledClient = new GeminiCoffeeAgencyClient(new HttpClient(disabled), Options.Create(new GeminiOptions()), NullLogger<GeminiCoffeeAgencyClient>.Instance);
        check((await disabledClient.ReplyAsync("Coffee?", [], context, default)).Text is null && disabled.Bodies.Count == 0,
            "Disabled configuration never calls the provider");

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var conversations = new CoffeeAgencyConversations(cache);
        var one = conversations.Get("one");
        for (var i = 0; i < 10; i++) one.Add("Question " + i, "Answer " + i);
        check(one.History.Count == 8 && one.History[0].Text == "Question 6", "Chat retains at most four recent exchanges");
        check(conversations.Get("two").History.Count == 0, "Conversation history is isolated by authenticated account");
        one.Clear();
        check(one.History.Count == 0, "Clear conversation removes retained messages");

        var fakeContext = new Context(context);
        var controllerTransport = new Transport();
        var controller = new CoffeeAgencyController(conversations, fakeContext, Client(controllerTransport))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is UnauthorizedResult, "Controller requires an authenticated user ID");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "one")], "test"));
        check(await controller.Chat(new() { Message = " " }, default) is BadRequestObjectResult, "Whitespace questions are rejected");
        await one.Gate.WaitAsync();
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is ConflictObjectResult, "Concurrent requests for one account are rejected");
        one.Gate.Release();
        controllerTransport.Responses.Enqueue(Response("STOP", "Try Long Black."));
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is OkObjectResult, "Controller returns completed provider replies");
        check(fakeContext.UserId == "one" && fakeContext.Email is null, "Personal context is selected only from the server-side identity");
        check(one.History.Count == 2, "Only completed exchanges enter history");
        controllerTransport.Responses.Enqueue(new(HttpStatusCode.Unauthorized));
        check(await controller.Chat(new() { Message = "More?" }, default) is ObjectResult { StatusCode: 503 } && one.History.Count == 2,
            "Provider failures do not contaminate history");
        check(await controller.Clear(default) is NoContentResult && one.History.Count == 0, "Controller clears the signed-in user's conversation");
    }

    private static GeminiCoffeeAgencyClient Client(Transport transport) => new(new HttpClient(transport),
        Options.Create(new GeminiOptions { Enabled = true, ApiKey = "test-key", Model = "test-model" }),
        NullLogger<GeminiCoffeeAgencyClient>.Instance);

    private static HttpResponseMessage Response(string reason, params string[] parts) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            candidates = new[] { new { finishReason = reason, content = new { parts = parts.Select(text => new { text }) } } }
        }), Encoding.UTF8, "application/json")
    };

    private sealed class Transport : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public List<string> Bodies { get; } = [];
        public Uri? Uri { get; private set; }
        public string? Key { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Key = request.Headers.GetValues("x-goog-api-key").Single();
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return Responses.Dequeue();
        }
    }

    private sealed class Context(MenuRecommendationContext context) : IMenuRecommendationContextService
    {
        public string? UserId { get; private set; }
        public string? Email { get; private set; }
        public Task<MenuRecommendationContext> BuildAsync(string userId, string? email, CancellationToken cancellationToken)
        {
            UserId = userId; Email = email;
            return Task.FromResult(context);
        }
    }
}
