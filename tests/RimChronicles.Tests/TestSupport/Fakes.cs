using System.Net;
using System.Text;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Tests.TestSupport;

/// <summary>A chat provider that replays queued results and records every request.</summary>
public sealed class FakeChatProvider(params ChatResult[] results) : IChatProvider
{
    private readonly Queue<ChatResult> _results = new(results);

    public List<ChatRequest> Requests { get; } = [];

    public string ModelId => "fake";

    public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : ChatResult.Failure("fake", "No queued result", TimeSpan.Zero));
    }

    public static ChatResult Answer(string text) => new() { Ok = true, Text = text, ModelId = "fake" };

    public static ChatResult Error(string error) => ChatResult.Failure("fake", error, TimeSpan.Zero);
}

/// <summary>An HTTP handler that answers from a delegate and records requests (with their bodies).</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public static StubHttpHandler Json(HttpStatusCode status, string json) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return await respond(request, cancellationToken);
    }
}
