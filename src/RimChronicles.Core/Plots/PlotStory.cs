using RimChronicles.Core.Persistence;

namespace RimChronicles.Core.Plots;

/// <summary>One thing the player saw happen in a plot: a letter it sent, or the colony's answer to a choice. Saved with
/// the plot, for the Chronicle tab.</summary>
public sealed class StoryEntry : IArchivable
{
    public int Day;

    /// <summary>The letter's title, or "" for an answer.</summary>
    public string Label = "";

    public string Text = "";

    /// <summary>True for the colony's answer to a choice letter, false for a letter.</summary>
    public bool Answer;

    public void Expose(IArchive archive)
    {
        archive.Value(ref Day, "day");
        archive.Value(ref Label, "label", "");
        archive.Value(ref Text, "text", "");
        archive.Value(ref Answer, "answer", false);
        Label ??= "";
        Text ??= "";
    }
}

/// <summary>How the Chronicle tab names and describes a plot. Plain text, no game calls.</summary>
public static class PlotChronicle
{
    /// <summary>Most letters a plot keeps; the oldest go first. A long Empire game sends a few dozen at most.</summary>
    public const int MaxEntries = 100;

    /// <summary>"Nemesis: Kael", "Lost Family: Mira", "The Silent Press", "The Awakened Empire".</summary>
    public static string Title(Plot plot)
    {
        var (kind, subject) = plot.Template switch
        {
            Nemesis.NemesisPlot.TemplateId => ("Nemesis", plot.Fact(Nemesis.NemesisPlot.NemesisName)),
            LostFamily.LostFamilyArc.TemplateId => ("Lost Family", plot.Fact(LostFamily.LostFamilyArc.KinName)),
            SilentPress.SilentPressArc.TemplateId => ("The Silent Press", null),
            Empire.EmpireArc.TemplateId => ("The Awakened Empire", null),
            _ => (Humanize(plot.Template), null),
        };
        return subject is { Length: > 0 } ? $"{kind}: {subject}" : kind;
    }

    /// <summary>"Under way since day 12" or "Ended on day 40: killed by colony".</summary>
    public static string Status(Plot plot) =>
        plot.Ended
            ? plot.Outcome.Length > 0 ? $"Ended on day {plot.BeatDay}: {Humanize(plot.Outcome).ToLowerInvariant()}" : $"Ended on day {plot.BeatDay}"
            : $"Under way since day {plot.StartDay}";

    /// <summary>"KilledByColony" → "Killed by colony"; text that already has spaces is only capitalised.</summary>
    public static string Humanize(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        if (!text.Contains(' '))
        {
            var spaced = new System.Text.StringBuilder();
            foreach (var c in text)
            {
                if (char.IsUpper(c) && spaced.Length > 0)
                {
                    spaced.Append(' ').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    spaced.Append(c);
                }
            }

            text = spaced.ToString();
        }

        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
