using System.Text.Json.Nodes;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Eval.Providers;

/// <summary>
/// A deterministic fake model for smoke tests: picks the first allowed choice for JSON tasks and writes 100 plain
/// lowercase words for prose tasks, so every check passes without a server.
/// </summary>
public sealed class StubChatProvider(string modelId) : IChatProvider
{
    private const string Sentence = "the wind moves over the dry ground and nobody on the road says much about it";

    public string ModelId => modelId;

    public int Calls { get; private set; }

    public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;

        string text;
        if (request.Schema is { } schema)
        {
            var choice = schema.Schema["properties"]?["choice"]?["enum"] is JsonArray { Count: > 0 } options
                ? options[0]!.GetValue<string>()
                : "Wait";
            text = new JsonObject { ["choice"] = choice, ["reason"] = "the stub always takes the first option" }.ToJsonString();
        }
        else
        {
            var words = Enumerable.Repeat(Sentence.Split(' '), 10).SelectMany(w => w).Take(100);
            text = string.Join(' ', words) + ".";
        }

        return Task.FromResult(new ChatResult
        {
            Ok = true,
            Text = text,
            InputTokens = (request.System.Length + request.User.Length) / 4,
            OutputTokens = text.Length / 4,
            ModelId = modelId,
            RawResponse = text,
        });
    }
}
