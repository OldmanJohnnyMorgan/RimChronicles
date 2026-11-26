using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RimChronicles.Llm.Chat;

namespace RimChronicles.Llm.Providers;

/// <summary>Where a download stands: the server's last status line and, while a layer downloads, its bytes so far.</summary>
public sealed record PullStatus(string Status, long Completed, long Total)
{
    public static readonly PullStatus Starting = new("Starting…", 0, 0);

    /// <summary>One line for the settings window, e.g. "Downloading: 1.2 of 3.1 GB (39%)".</summary>
    public string Describe()
    {
        if (Total <= 0)
        {
            return Status;
        }

        var percent = (int)(100 * Math.Clamp((double)Completed / Total, 0, 1));
        return $"Downloading: {Completed / 1e9:0.0} of {Total / 1e9:0.0} GB ({percent}%)";
    }
}

/// <summary>The settings window's Download model button: asks an Ollama server to pull a model (POST {endpoint}/api/pull,
/// streamed), so a player never needs a terminal. Ollama-only: other OpenAI-compatible servers have no pull, and say so
/// with a 404. Never throws for HTTP, timeout or parse errors. Runs off the main thread; touches no game state.</summary>
public static class OllamaPull
{
    public sealed record Result(bool Ok, string Message);

    /// <param name="endpoint">The server's root, as for chat (e.g. http://localhost:11434/).</param>
    /// <param name="report">Called with each status line, from the download's thread.</param>
    public static async Task<Result> RunAsync(HttpClient http, Uri endpoint, string model, string? apiKey, Action<PullStatus> report,
        CancellationToken cancellationToken)
    {
        model = model.Trim();
        if (model.Length == 0)
        {
            return new(false, "No model name to download.");
        }

        var body = new JsonObject { ["model"] = model, ["stream"] = true };
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/pull"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        try
        {
            // Headers only: the body is a stream of JSON lines that lasts as long as the download.
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new(false, (int)response.StatusCode == 404 && !raw.Contains("error", StringComparison.Ordinal)
                    ? "This server can't download models (only Ollama can). Download it on the server instead."
                    : $"HTTP {(int)response.StatusCode}: {ErrorOf(raw) ?? Truncate(raw)}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var last = "";
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (node?["error"]?.GetValue<string>() is { } error)
                {
                    return new(false, error.Contains("file does not exist", StringComparison.OrdinalIgnoreCase)
                        ? $"Download failed: ollama.com has no model named {model}. Check the name ({error})."
                        : $"Download failed: {error}");
                }

                last = node?["status"]?.GetValue<string>() ?? last;
                report(new PullStatus(last, Long(node?["completed"]), Long(node?["total"])));
            }

            return last == "success"
                ? new(true, $"Downloaded {model}. Press Test connection to check it.")
                : new(false, $"The download stopped before it finished (last: {(last.Length > 0 ? last : "nothing")}). Try again.");
        }
        catch (OperationCanceledException)
        {
            return new(false, "Download cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return new(false, $"Request failed: {ex.Message} ({ConnectionProbe.ServerHint})");
        }
        catch (IOException ex)
        {
            return new(false, $"The download was cut off: {ex.Message}. Try again; Ollama resumes where it stopped.");
        }
    }

    /// <summary>Whether the server already has <paramref name="model"/> (POST {endpoint}/api/show): true or false, or
    /// null when it can't tell (not Ollama, unreachable, an error).</summary>
    public static async Task<bool?> HasModelAsync(HttpClient http, Uri endpoint, string model, string? apiKey, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/show"))
        {
            Content = new StringContent(new JsonObject { ["model"] = model.Trim() }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        try
        {
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            // Ollama answers a missing model with 404 and a JSON error; a server without /api/show 404s with no JSON.
            return (int)response.StatusCode == 404 && ErrorOf(raw) is not null ? false : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>What to download for a model the settings name: a fine-tuned preset set under its local, bare name
    /// (<c>rimchron-director-e4b</c>, as training makes it) is fetched under its registry name, since the registry
    /// knows no other. Anything else is fetched as named.</summary>
    public static string PullName(string model) =>
        TierPresets.DirectorPreset(model) is { } preset && !model.Contains('/', StringComparison.Ordinal) ? preset.Model : model.Trim();

    private static long Long(JsonNode? node)
    {
        try
        {
            return node?.GetValue<long>() ?? 0;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            return 0;
        }
    }

    private static string? ErrorOf(string raw)
    {
        try
        {
            return JsonNode.Parse(raw)?["error"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
