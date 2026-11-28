using RimChronicles.Core.Plots;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>Sends the mod's story letters with something to jump to: the pawns a plot names (a nemesis, a relative, the
/// protagonist) where the player can see them, and the faction the letter is about. Main thread only.</summary>
public static class StoryLetters
{
    public static void Send(string label, string text, LetterDef def, LookTargets? targets = null, Faction? faction = null) =>
        Find.LetterStack.ReceiveLetter(label, text, def, targets is { Any: true } ? targets : LookTargets.Invalid, faction);

    /// <summary>A plot letter, pointing at the plot's people, and kept in the plot's story for the Chronicle tab.</summary>
    public static void Send(RimChroniclesGameComponent component, Plot plot, string label, string text, LetterDef def)
    {
        plot.Tell(GenDate.DaysPassed, label, text);
        Send(label, text, def, For(component, plot), FactionOf(component, plot));
    }

    /// <summary>An Imperial crisis letter: about the Empire unless another faction is given, and kept in the Awakened
    /// Empire plot's story for the Chronicle tab unless <paramref name="record"/> is false (for frequent tactical
    /// warnings, such as a strike being called).</summary>
    public static void Crisis(string label, string text, LetterDef def, LookTargets? targets = null, Faction? faction = null, bool record = true)
    {
        if (record && RimChroniclesGameComponent.Current is { } component && Game.Empire.EmpireBeats.Current(component) is { } plot)
        {
            plot.Tell(GenDate.DaysPassed, label, text);
        }

        Send(label, text, def, targets, faction ?? Empire);
    }

    /// <summary>A Hollow Choir crisis letter (War in Heaven W7): about the Choir, and kept in the Choir plot's story
    /// unless <paramref name="record"/> is false.</summary>
    public static void ChoirCrisis(string label, string text, LetterDef def, LookTargets? targets = null, bool record = true)
    {
        if (record && RimChroniclesGameComponent.Current is { } component && Game.Powers.ChoirBeats.Current(component) is { } plot)
        {
            plot.Tell(GenDate.DaysPassed, label, text);
        }

        Send(label, text, def, targets, Game.Powers.PowerFactions.Of(Core.Powers.Power.Choir));
    }

    /// <summary>Keeps a letter in the story of the plot with this id, if it is still running.</summary>
    public static void Record(string plotId, string label, string text)
    {
        if (RimChroniclesGameComponent.Current?.Plots.Active.FirstOrDefault(p => p.Id == plotId) is { } plot)
        {
            plot.Tell(GenDate.DaysPassed, label, text);
        }
    }

    /// <summary>The colony answered a choice letter: keeps the answer in the plot's story and confirms it on screen.</summary>
    public static void Answered(Plot plot, string text)
    {
        plot.Answered(GenDate.DaysPassed, text);
        Confirm(text);
    }

    /// <summary>The plot's pawns the player can jump to, outsiders first (the nemesis or relative is usually the news),
    /// then its colonists. A pawn on a map is targeted directly; one travelling in a caravan, by its caravan. Null if none.</summary>
    public static LookTargets? For(RimChroniclesGameComponent component, Plot plot)
    {
        var targets = Pawns(component, plot)
            .OrderBy(p => p.Faction is { IsPlayer: true } ? 1 : 0)
            .Select(Target)
            .Where(t => t.IsValid)
            .Distinct()
            .ToList();
        return targets.Count > 0 ? new LookTargets(targets) : null;
    }

    /// <summary>The first outside faction among the plot's pawns (the nemesis's or the relative's), or null.</summary>
    public static Faction? FactionOf(RimChroniclesGameComponent component, Plot plot) =>
        Pawns(component, plot).Select(p => p.Faction).FirstOrDefault(f => f is { IsPlayer: false });

    /// <summary>Points a choice letter at its plot's people and faction, before it is sent.</summary>
    public static void Point(Letter letter, string plotId, Faction? faction = null)
    {
        if (RimChroniclesGameComponent.Current is { } component && component.Plots.Active.FirstOrDefault(p => p.Id == plotId) is { } plot)
        {
            letter.lookTargets = For(component, plot) ?? LookTargets.Invalid;
            letter.relatedFaction = faction ?? FactionOf(component, plot);
        }
        else
        {
            letter.relatedFaction = faction;
        }
    }

    /// <summary>The line a choice letter ends with: what happens if no one answers before it expires.</summary>
    public static string IfUnanswered(int days, string what) => $"\n\nIf no one answers within {days} days, {what}.";

    /// <summary>A short note confirming the player's answer, such as the silver paid.</summary>
    public static void Confirm(string text) => Messages.Message(text, MessageTypeDefOf.NeutralEvent, historical: false);

    /// <summary>The non-player faction with this name, or null.</summary>
    public static Faction? FactionNamed(string? name) =>
        string.IsNullOrEmpty(name) ? null : Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => !f.IsPlayer && f.Name == name);

    /// <summary>The Empire faction, or null without Royalty.</summary>
    public static Faction? Empire => Find.FactionManager.OfEmpire;

    private static IEnumerable<Pawn> Pawns(RimChroniclesGameComponent component, Plot plot)
    {
        var ids = plot.Facts.Select(f => f.Value)
            .Concat([plot.Protagonist.PawnId, plot.Protagonist.CoLeadId])
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct();
        foreach (var id in ids)
        {
            if (component.TrackedOrFind(id) is { Dead: false } pawn)
            {
                yield return pawn;
            }
        }
    }

    private static GlobalTargetInfo Target(Pawn pawn) =>
        pawn.Spawned ? new GlobalTargetInfo(pawn)
        : pawn.GetCaravan() is { } caravan ? new GlobalTargetInfo(caravan)
        : GlobalTargetInfo.Invalid;
}
