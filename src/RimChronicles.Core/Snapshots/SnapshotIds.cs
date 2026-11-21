using System.Text;

namespace RimChronicles.Core.Snapshots;

public static class SnapshotIds
{
    /// <summary>An id for an exported snapshot, safe as a file name: <c>export-&lt;colony&gt;-day&lt;N&gt;</c>, with the
    /// colony name lower-cased and anything but ASCII letters and digits turned into single dashes.</summary>
    public static string ForExport(string colonyName, int day)
    {
        var slug = new StringBuilder();
        foreach (var c in (colonyName ?? "").ToLowerInvariant())
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

        return $"export-{(name.Length == 0 ? "colony" : name)}-day{day}";
    }
}
