namespace RimChronicles.Core.Snapshots;

public static class EventText
{
    /// <summary>Shortens an archived event label for a snapshot: the first sentence if it fits in
    /// <paramref name="max"/> characters, else the text cut at a word boundary with an ellipsis. Some archived letters
    /// (the scenario intro) carry a whole paragraph as their label.</summary>
    public static string Shorten(string text, int max = 100)
    {
        text = (text ?? "").ReplaceLineEndings(" ").Trim();
        var end = text.IndexOfAny(['.', '!', '?']);
        if (end >= 0 && end + 1 < text.Length && end < max)
        {
            return text[..(end + 1)];
        }

        if (text.Length <= max)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', max - 1);
        return text[..(cut > 0 ? cut : max - 1)].TrimEnd(',', ';', ':', ' ') + "…";
    }
}
