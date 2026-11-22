using System.Text;

namespace RimChronicles.Core.Snapshots;

public static class SnapshotIds
{
    /// <summary>An id for an exported snapshot, safe as a file name: <c>export-&lt;colony&gt;-day&lt;N&gt;</c>, with the
    /// colony name lower-cased and anything but ASCII letters and digits turned into single dashes.</summary>
    public static string ForExport(string colonyName, int day) => $"export-{Slug(colonyName, "colony")}-day{day}";

    /// <summary>An id for a director decision recorded in game (training track D3):
    /// <c>dir-game-&lt;colony&gt;-day&lt;N&gt;-&lt;plot&gt;</c>.</summary>
    public static string ForDirectorCase(string colonyName, int day, string plotId) =>
        $"dir-game-{Slug(colonyName, "colony")}-day{day}-{Slug(plotId, "plot")}";

    private static string Slug(string? text, string fallback)
    {
        var slug = new StringBuilder();
        foreach (var c in (text ?? "").ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var name = slug.ToString().Trim('-');
        if (name.Length > 40)
        {
            name = name[..40].TrimEnd('-');
        }

        return name.Length == 0 ? fallback : name;
    }
}
