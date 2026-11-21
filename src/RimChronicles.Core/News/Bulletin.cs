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

/// <summary>Where the reporter stands (Stage 2 M5).</summary>
public enum ReporterStatus
{
    /// <summary>Free and writing.</summary>
    Active,

    /// <summary>Held prisoner by the colony: bulletins pause until they are freed.</summary>
    Held,

    /// <summary>Killed by the colony, or recruited by it: bulletins stop with no explanation (Silent Press, Stage 3).</summary>
    Silenced,

    /// <summary>Died some other way: the outlet sends a replacement on <see cref="ReporterState.ReplaceDay"/>.</summary>
    Replacing,
}

/// <summary>The reporter writing the bulletins. The Game layer ties it to a pawn in the world; until then (or with
/// no friendly faction to recruit from) the default reporter writes.</summary>
public sealed class ReporterState : IArchivable
{
    public string Name = WorldSummarizer.DefaultReporter.Name;
    public string Outlet = WorldSummarizer.DefaultReporter.Outlet;
    public string Persona = WorldSummarizer.DefaultReporter.Persona;

    /// <summary>The faction the reporter comes from, or "" for the default reporter.</summary>
    public string Faction = "";

    public ReporterStatus Status = ReporterStatus.Active;

    /// <summary>The day of the reporter's next visit to the colony, or -1 if none is planned.</summary>
    public int NextVisitDay = -1;

    /// <summary>The day a replacement arrives, while <see cref="Status"/> is <see cref="ReporterStatus.Replacing"/>.</summary>
    public int ReplaceDay = -1;

    /// <summary>The day the colony silenced the reporter, or -1. Stage 3's Silent Press arc starts from it.</summary>
    public int SilencedDay = -1;

    /// <summary>Bulletins still to be written in a cold voice (after Silent Press reparations); 0 normally.</summary>
    public int ColdBulletinsLeft;

    /// <summary>The persona to use while <see cref="ColdBulletinsLeft"/> is above 0.</summary>
    public string ColdPersona = "";

    /// <summary>The persona the next bulletin is written with.</summary>
    public string CurrentPersona => ColdBulletinsLeft > 0 && ColdPersona.Length > 0 ? ColdPersona : Persona;

    /// <summary>The last tick the colony hurt the reporter, or -1: a later death with no killer (bleeding out) counts
    /// as the colony's doing if it comes soon after.</summary>
    public int LastColonyHarmTick = -1;

    public ReporterInfo ToInfo() => new() { Name = Name, Outlet = Outlet, Persona = CurrentPersona };

    public void Expose(IArchive archive)
    {
        var d = WorldSummarizer.DefaultReporter;
        archive.Value(ref Name, "name", d.Name);
        archive.Value(ref Outlet, "outlet", d.Outlet);
        archive.Value(ref Persona, "persona", d.Persona);
        archive.Value(ref Faction, "faction", "");
        archive.Value(ref Status, "status", ReporterStatus.Active);
        archive.Value(ref NextVisitDay, "nextVisitDay", -1);
        archive.Value(ref ReplaceDay, "replaceDay", -1);
        archive.Value(ref SilencedDay, "silencedDay", -1);
        archive.Value(ref LastColonyHarmTick, "lastColonyHarmTick", -1);
        archive.Value(ref ColdBulletinsLeft, "coldBulletinsLeft");
        archive.Value(ref ColdPersona, "coldPersona", "");
        ColdPersona ??= "";
        Faction ??= "";
        Name = string.IsNullOrWhiteSpace(Name) ? d.Name : Name;
        Outlet = string.IsNullOrWhiteSpace(Outlet) ? d.Outlet : Outlet;
        Persona = string.IsNullOrWhiteSpace(Persona) ? d.Persona : Persona;
    }
}

