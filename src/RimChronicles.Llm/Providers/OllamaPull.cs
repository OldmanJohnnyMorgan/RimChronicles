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
                    return new(false, $"Download failed: {error}");
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
