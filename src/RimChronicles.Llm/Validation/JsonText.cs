using System.Text.Json;
using System.Text.Json.Nodes;

namespace RimChronicles.Llm.Validation;

/// <summary>Finds the JSON object in a model answer that may wrap it in code fences or a sentence.</summary>
public static class JsonText
{
    /// <summary>Returns the text from the first '{' to the last '}', or the trimmed text if there are none.</summary>
    public static string Extract(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        return start >= 0 && end > start ? output[start..(end + 1)] : output.Trim();
    }

    /// <summary>Parses the JSON object in <paramref name="output"/>; null with an error message if it isn't valid JSON.</summary>
    public static JsonNode? TryParse(string output, out string? error)
    {
        try
        {
            error = null;
            return JsonNode.Parse(Extract(output));
        }
        catch (JsonException ex)
        {
            error = $"Output is not valid JSON ({ex.Message})";
            return null;
        }
    }
}
