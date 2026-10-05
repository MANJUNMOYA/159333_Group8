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
        transport.Responses.Enqueue(Response("stop", "A complete reply."));
        var reply = await new CoffeeAgencyClient(Model(transport)).ReplyAsync("Recommend a drink.",
            [new("user", "Hi"), new("model", "Hello")], context, default);
        check(reply.Text == "A complete reply.", "Coffee agency receives a complete local model answer");
        check(transport.Uri?.AbsolutePath == "/api/chat", "Coffee agency calls the private Ollama chat endpoint");
        using (var payload = JsonDocument.Parse(transport.Bodies[0]))
        {
            var root = payload.RootElement;
            check(root.GetProperty("model").GetString() == "qwen2.5:0.5b-instruct" && !root.GetProperty("stream").GetBoolean(),
                "Local inference uses the fixed Qwen model without streaming");
            var messages = root.GetProperty("messages");
            check(messages.GetArrayLength() == 4 && messages[2].GetProperty("role").GetString() == "assistant",
                "Recent conversation is translated to local model roles");
            var prompt = messages[0].GetProperty("content").GetString()!;
            check(prompt.Contains("Long Black") && !prompt.Contains("CustomerUserId") && !prompt.Contains("Email"),
                "Model context contains menu preferences but no account identifiers");
            check(root.GetProperty("options").GetProperty("num_ctx").GetInt32() == 4096,
                "Local inference has an explicit bounded context window");
        }
        var truncation = new Transport();
        truncation.Responses.Enqueue(Response("length", "Incomplete"));
        truncation.Responses.Enqueue(Response("stop", "Complete replacement."));
        var result = await Model(truncation).GenerateAsync([new("user", "Coffee?")], 256, default);
        check(result.Text == "Complete replacement." && truncation.Bodies.Count == 2,
            "Length-limited replies retry once rather than being shown halfway");
        var incomplete = new Transport();
        incomplete.Responses.Enqueue(Response("length", "Half"));
        incomplete.Responses.Enqueue(Response("length", "Still half"));
        result = await Model(incomplete).GenerateAsync([new("user", "Coffee?")], 256, default);
        check(result.Text is null && result.Error!.Contains("complete answer"), "Repeated truncation fails explicitly");
        var invalid = new Transport();
        invalid.Responses.Enqueue(new(HttpStatusCode.OK) { Content = new StringContent("not json") });
        check((await Model(invalid).GenerateAsync([new("user", "Coffee?")], 256, default)).Text is null,
            "Invalid local model output fails safely");
        var disabled = new OllamaCoffeeModelClient(new Factory(new Transport()), Options.Create(new CoffeeModelOptions()),
            NullLogger<OllamaCoffeeModelClient>.Instance);
        check((await disabled.GenerateAsync([new("user", "Coffee?")], 256, default)).Text is null,
            "Disabled local model never starts inference");

        var dairyTransport = new Transport();
        dairyTransport.Responses.Enqueue(Response("stop", "Try Long Black."));
        await new CoffeeAgencyClient(Model(dairyTransport)).ReplyAsync("Coffee without milk?", [], new MenuRecommendationContext
        {
            Candidates =
            [
                new RecommendationCandidate { ProductName = "Long Black", DietaryLabel = "Vegan", AllergenLabel = "No major allergens" },
                new RecommendationCandidate { ProductName = "Campus Flat White", DietaryLabel = "Vegetarian", AllergenLabel = "Contains milk" }
            ]
        }, default);
        check(dairyTransport.Bodies[0].Contains("Long Black") && !dairyTransport.Bodies[0].Contains("Campus Flat White"),
            "Explicit milk-free questions exclude milk-labelled menu candidates");

        var blocking = new BlockingTransport();
        var serializedModel = new OllamaCoffeeModelClient(new BlockingFactory(blocking),
            Options.Create(new CoffeeModelOptions { Enabled = true }), NullLogger<OllamaCoffeeModelClient>.Instance);
        var firstCall = serializedModel.GenerateAsync([new("user", "First")], 128, default);
        await blocking.Entered.Task;
        var secondCall = await serializedModel.GenerateAsync([new("user", "Second")], 128, default);
        check(secondCall.Busy && secondCall.Text is null, "Only one model inference can run at a time across all callers");
        blocking.Finish.TrySetResult();
        check((await firstCall).Text is not null, "The active inference completes after another request is rejected");

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var conversations = new CoffeeAgencyConversations(cache);
        var one = conversations.Get("one");
        for (var i = 0; i < 10; i++) one.Add("Question " + i, "Answer " + i);
        check(one.History.Count == 8 && one.History[0].Text == "Question 6", "Retained history is limited to four exchanges");
        check(conversations.Get("two").History.Count == 0, "Conversation history remains isolated by account");
        one.Clear();
        var fakeContext = new Context(context);
        var controllerTransport = new Transport();
        var controller = new CoffeeAgencyController(conversations, fakeContext, new CoffeeAgencyClient(Model(controllerTransport)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is UnauthorizedResult, "Website chat requires an authenticated user ID");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "one")], "test"));
        check(await controller.Chat(new() { Message = " " }, default) is BadRequestObjectResult, "Whitespace questions are rejected");
        await one.Gate.WaitAsync();
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is ConflictObjectResult, "Parallel questions from one account are rejected");
        one.Gate.Release();
        controllerTransport.Responses.Enqueue(Response("stop", "Try Long Black."));
        check(await controller.Chat(new() { Message = "Coffee?" }, default) is OkObjectResult, "Website chat returns completed local model replies");
        check(fakeContext.UserId == "one" && fakeContext.Email is null, "Context comes only from the signed-in account");
        controllerTransport.Responses.Enqueue(new(HttpStatusCode.ServiceUnavailable));
        check(await controller.Chat(new() { Message = "More?" }, default) is ObjectResult { StatusCode: 503 } && one.History.Count == 2,
            "Failed inference is not added to history");
        check(await controller.Clear(default) is NoContentResult && one.History.Count == 0, "Clearing the conversation still works");
    }

    private static OllamaCoffeeModelClient Model(Transport transport) => new(new Factory(transport),
        Options.Create(new CoffeeModelOptions { Enabled = true }), NullLogger<OllamaCoffeeModelClient>.Instance);

    private static HttpResponseMessage Response(string reason, string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            done = true, done_reason = reason, message = new { content = text }, prompt_eval_count = 10, eval_count = 5
        }), Encoding.UTF8, "application/json")
    };

    private sealed class Transport : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = new();
        public List<string> Bodies { get; } = [];
        public Uri? Uri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return Responses.Dequeue();
        }
    }

    private sealed class Factory(Transport transport) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(transport, disposeHandler: false);
    }

    private sealed class BlockingTransport : HttpMessageHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Finish.Task.WaitAsync(cancellationToken);
            return Response("stop", "Done.");
        }
    }

    private sealed class BlockingFactory(BlockingTransport transport) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(transport, disposeHandler: false);
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
