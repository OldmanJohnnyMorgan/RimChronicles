using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Eval.Providers;

/// <summary>Cache hit and miss counts shared by every cell in a run.</summary>
public sealed class CacheStats
{
    private int _hits;
    private int _misses;

    public int Hits => _hits;
    public int Misses => _misses;

    public void Hit() => Interlocked.Increment(ref _hits);

    public void Miss() => Interlocked.Increment(ref _misses);
}

/// <summary>
/// Caches successful responses on disk, keyed by the model's settings, the full request and the repeat index.
/// A changed prompt changes the request, so stale entries are never reused. Failures are not cached, so a
/// re-run retries them.
/// </summary>
public sealed class CachingChatProvider(IChatProvider inner, string cacheDir, string modelIdentity, int repeat, CacheStats stats)
    : IChatProvider
{
    public string ModelId => inner.ModelId;

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var path = Path.Combine(cacheDir, Key(request) + ".json");
        if (File.Exists(path))
        {
            var cached = JsonSerializer.Deserialize<ChatResult>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            if (cached is not null)
            {
                stats.Hit();
                return cached;
            }
        }

        stats.Miss();
        var result = await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Ok)
        {
            Directory.CreateDirectory(cacheDir);
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(result), cancellationToken).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }

        return result;
    }

    internal string Key(ChatRequest request)
    {
        var material = new JsonObject
        {
            ["model"] = modelIdentity,
            ["repeat"] = repeat,
            ["system"] = request.System,
            ["user"] = request.User,
            ["maxOutputTokens"] = request.MaxOutputTokens,
            ["schema"] = request.Schema?.Schema.DeepClone(),
            ["grammar"] = request.UseGrammar,
            ["thinking"] = request.Thinking.ToString(),
            ["extra"] = request.ExtraBody is null ? null : new JsonObject(request.ExtraBody.Select(kv => KeyValuePair.Create(kv.Key, kv.Value?.DeepClone()))),
        }.ToJsonString();

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
