using RimChronicles.Core.Persistence;
using RimChronicles.Core.Snapshots;
using RimChronicles.Core.Summaries;

namespace RimChronicles.Core.News;

/// <summary>One news bulletin. Once delivered it is never rewritten ("generate once, save forever").</summary>
public sealed class Bulletin : IArchivable
{
    /// <summary>The day it is (or was) due.</summary>
    public int DueDay;

    /// <summary>The day it reached the player, or -1 while it waits.</summary>
    public int DeliveredDay = -1;

    public string Text = "";

    /// <summary>"Model", "ModelRetry" or "Template", as the task runner reported it.</summary>
    public string Source = "";

    /// <summary>The prompt version that wrote it, e.g. "news-bulletin/v8", or "" for a template.</summary>
    public string PromptVersion = "";

    /// <summary>The news items this bulletin covered; they become old news for the next one.</summary>
    public List<string> NewsCovered = [];

    public bool Delivered => DeliveredDay >= 0;

    public void Expose(IArchive archive)
    {
        archive.Value(ref DueDay, "dueDay");
        archive.Value(ref DeliveredDay, "deliveredDay", -1);
        archive.Value(ref Text, "text", "");
        archive.Value(ref Source, "source", "");
        archive.Value(ref PromptVersion, "promptVersion", "");
        archive.Strings(ref NewsCovered, "newsCovered");
        Text ??= "";
        Source ??= "";
        PromptVersion ??= "";
    }
}

/// <summary>The reporter writing the bulletins. Stage 2 M4 ties it to a pawn in the world.</summary>
public sealed class ReporterState : IArchivable
{
    public string Name = WorldSummarizer.DefaultReporter.Name;
    public string Outlet = WorldSummarizer.DefaultReporter.Outlet;
    public string Persona = WorldSummarizer.DefaultReporter.Persona;

    public ReporterInfo ToInfo() => new() { Name = Name, Outlet = Outlet, Persona = Persona };

    public void Expose(IArchive archive)
    {
        var d = WorldSummarizer.DefaultReporter;
        archive.Value(ref Name, "name", d.Name);
        archive.Value(ref Outlet, "outlet", d.Outlet);
        archive.Value(ref Persona, "persona", d.Persona);
        Name = string.IsNullOrWhiteSpace(Name) ? d.Name : Name;
        Outlet = string.IsNullOrWhiteSpace(Outlet) ? d.Outlet : Outlet;
        Persona = string.IsNullOrWhiteSpace(Persona) ? d.Persona : Persona;
    }
}

