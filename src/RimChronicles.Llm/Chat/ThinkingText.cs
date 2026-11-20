using System.Text.RegularExpressions;

namespace RimChronicles.Llm.Chat;

/// <summary>Removes reasoning that some servers leave inside the answer text.</summary>
public static partial class ThinkingText
{
    [GeneratedRegex(@"<think(?:ing)?>.*?</think(?:ing)?>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ClosedBlock();

    [GeneratedRegex(@"^\s*<think(?:ing)?>.*\z", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex UnclosedBlock();

    /// <summary>Strips closed &lt;think&gt; blocks, and an unclosed one at the start (output cut off mid-thought).</summary>
    public static string Strip(string text)
    {
        var stripped = ClosedBlock().Replace(text, "");
        stripped = UnclosedBlock().Replace(stripped, "");
        return stripped.Trim();
    }
}
