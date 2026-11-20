using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using RimChronicles.Eval.Config;

namespace RimChronicles.Eval.Servers;

/// <summary>What a model server reports about itself and one model. Every field is best effort.</summary>
public sealed record ServerInfo
{
    public required string ModelId { get; init; }
    public bool Reachable { get; init; }

    /// <summary>"ollama", "llama.cpp", or "openai-compatible" when the server can't be told apart.</summary>
    public string? ServerKind { get; init; }

    public string? ServerVersion { get; init; }

    /// <summary>Whether the configured model name appears in /v1/models; null when the list couldn't be read.</summary>
    public bool? ModelListed { get; init; }

    public IReadOnlyList<string> AvailableModels { get; init; } = [];
    public string? Quantization { get; init; }
    public string? ParameterSize { get; init; }

    /// <summary>GPU memory the loaded model uses (Ollama /api/ps); null when unknown or not loaded.</summary>
    public long? VramBytes { get; init; }

    /// <summary>Total memory the loaded model uses, GPU and system RAM; above VramBytes means part runs on the CPU.</summary>
    public long? SizeBytes { get; init; }

    public string? Error { get; init; }

    public double? VramGb => VramBytes / 1e9;

    /// <summary>True when Ollama reports part of the model outside GPU memory, which slows it down sharply.</summary>
    public bool PartlyOnCpu => VramBytes is { } v && SizeBytes is { } s && s > v;

    public JsonObject ToJson() => new()
    {
        ["reachable"] = Reachable,
        ["serverKind"] = ServerKind,
        ["serverVersion"] = ServerVersion,
        ["modelListed"] = ModelListed,
        ["quantization"] = Quantization,
        ["parameterSize"] = ParameterSize,
        ["vramBytes"] = VramBytes,
        ["sizeBytes"] = SizeBytes,
        ["error"] = Error,
    };

    public static ServerInfo FromJson(string modelId, JsonNode? n) => new()
    {
        ModelId = modelId,
        Reachable = n?["reachable"]?.GetValue<bool>() ?? false,
        ServerKind = n?["serverKind"]?.GetValue<string>(),
        ServerVersion = n?["serverVersion"]?.GetValue<string>(),
        ModelListed = n?["modelListed"]?.GetValue<bool>(),
        Quantization = n?["quantization"]?.GetValue<string>(),
        ParameterSize = n?["parameterSize"]?.GetValue<string>(),
        VramBytes = n?["vramBytes"]?.GetValue<long>(),
        SizeBytes = n?["sizeBytes"]?.GetValue<long>(),
        Error = n?["error"]?.GetValue<string>(),
    };
}

/// <summary>
/// Asks an OpenAI-compatible server what it is and what it has loaded: /v1/models everywhere, plus Ollama's
/// /api/version, /api/show and /api/ps, or llama.cpp's /props. Never throws for server errors.
/// </summary>
public static class ServerProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<ServerInfo> ProbeAsync(ModelConfig m, CancellationToken ct, HttpClient? http = null)
    {
        http ??= Http;
        if (m.Provider != ProviderKind.OpenAi || m.Endpoint is null || m.Model is null)
        {
            return new ServerInfo { ModelId = m.Id, Reachable = m.Provider != ProviderKind.OpenAi, ServerKind = m.Provider.ToString().ToLowerInvariant() };
        }

        var baseUri = new Uri(m.Endpoint.EndsWith('/') ? m.Endpoint : m.Endpoint + "/");
        var models = await GetJsonAsync(http, new Uri(baseUri, "v1/models"), ct).ConfigureAwait(false);
        if (models is null)
        {
            return new ServerInfo { ModelId = m.Id, Error = $"No answer from {baseUri}v1/models. Is the server running?" };
        }

        var available = (models["data"] as JsonArray ?? [])
            .Select(d => d?["id"]?.GetValue<string>()).OfType<string>().Order(StringComparer.Ordinal).ToList();
        var info = new ServerInfo
        {
            ModelId = m.Id,
            Reachable = true,
            ServerKind = "openai-compatible",
            AvailableModels = available,
            ModelListed = available.Count == 0 ? null : available.Contains(m.Model) || available.Contains(m.Model + ":latest"),
        };

        // Ollama
        if (await GetJsonAsync(http, new Uri(baseUri, "api/version"), ct).ConfigureAwait(false) is { } version)
        {
            var show = await PostJsonAsync(http, new Uri(baseUri, "api/show"), new JsonObject { ["model"] = m.Model }, ct).ConfigureAwait(false);
            var loaded = (await GetJsonAsync(http, new Uri(baseUri, "api/ps"), ct).ConfigureAwait(false))?["models"] as JsonArray;
            var running = loaded?.FirstOrDefault(x => x?["name"]?.GetValue<string>() is { } name && (name == m.Model || name == m.Model + ":latest"));

            return info with
            {
                ServerKind = "ollama",
                ServerVersion = version["version"]?.GetValue<string>(),
                Quantization = show?["details"]?["quantization_level"]?.GetValue<string>(),
                ParameterSize = show?["details"]?["parameter_size"]?.GetValue<string>(),
                VramBytes = running?["size_vram"]?.GetValue<long>(),
                SizeBytes = running?["size"]?.GetValue<long>(),
            };
        }

        // llama.cpp server
        if (await GetJsonAsync(http, new Uri(baseUri, "props"), ct).ConfigureAwait(false) is { } props)
        {
            var path = props["model_path"]?.GetValue<string>();
            return info with
            {
                ServerKind = "llama.cpp",
                ServerVersion = props["build_info"]?.GetValue<string>(),
                Quantization = path is null ? null : QuantFromFileName(path),
            };
        }

        return info;
    }

    /// <summary>Reads a GGUF quantization tag such as Q4_K_M out of a model file name.</summary>
    internal static string? QuantFromFileName(string path)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path),
            @"(?i)\b(I?Q\d(?:_[A-Z0-9]+)*|F16|BF16|F32)\b");
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    public static string FormatGb(long? bytes) =>
        bytes is { } b ? (b / 1e9).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : "–";

    private static async Task<JsonNode?> GetJsonAsync(HttpClient http, Uri uri, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(uri, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<JsonNode?> PostJsonAsync(HttpClient http, Uri uri, JsonObject body, CancellationToken ct)
    {
        try
        {
            using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(uri, content, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
