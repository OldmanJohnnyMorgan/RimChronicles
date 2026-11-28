using HarmonyLib;
using RimChronicles.Core.News;
using RimChronicles.Game.Engine;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimChronicles.Game.Patches;

// Stage 2 M2: Harmony postfixes that record public world news into the game's NewsLog. Each one only reads game
// state and never changes it, runs on the main thread, and does nothing outside a running game (so loading a save,
// which rebuilds conditions and relations, records nothing). All four targets are in Assembly-CSharp, the only
// JIT-patchable assembly on 1.7. Every patch body is guarded (code review fix 3).

/// <summary>Writes news into the current game's log, if a game is running.</summary>
internal static class NewsRecorder
{
    public static void Record(string kind, string text, string faction = "")
    {
        if (Current.ProgramState != ProgramState.Playing || RimChroniclesGameComponent.Current is not { } component)
        {
            return;
        }

        var day = GenDate.DaysPassed;
        if (component.News.Add(new NewsEvent(day, kind, text, faction)))
        {
            component.News.Prune(day);
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] News: {text}");
            }
        }
    }

    public static RelationKind ToCore(FactionRelationKind kind) => kind switch
    {
        FactionRelationKind.Hostile => RelationKind.Hostile,
        FactionRelationKind.Ally => RelationKind.Ally,
        _ => RelationKind.Neutral,
    };

    /// <summary>Null for a world-wide condition. For the colony's own map, the colony's name: the home settlement's
    /// label stays "Colony" until the player renames it. For any other map, that map's place name.</summary>
    public static string? Place(GameCondition condition) => condition.gameConditionManager?.ownerMap switch
    {
        null => null,
        { IsPlayerHome: true } => Faction.OfPlayer.Name,
        var map => map.Parent?.LabelCap ?? Faction.OfPlayer.Name,
    };
}

/// <summary>Weather and disasters starting: toxic fallout, volcanic winter, cold snaps, heat waves, eclipses…</summary>
[HarmonyPatch(typeof(GameConditionManager), nameof(GameConditionManager.RegisterCondition))]
public static class ConditionStartedPatch
{
    public static void Postfix(GameCondition cond)
    {
        try
        {
            if (cond?.def is null || string.IsNullOrWhiteSpace(cond.def.label))
            {
                return;
            }

            // def.label, not Label: some conditions add detail to Label ("psychic soothe: Female").
            NewsRecorder.Record(NewsKind.ConditionStarted, NewsPhrasing.Condition(cond.def.label, NewsRecorder.Place(cond), ended: false));
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ConditionStartedPatch), ex);
        }
    }
}

/// <summary>Conditions ending, for ones that lasted at least <see cref="MinDays"/> days (short ones aren't news).</summary>
[HarmonyPatch(typeof(GameCondition), nameof(GameCondition.End))]
public static class ConditionEndedPatch
{
    public const int MinDays = 3;

    public static void Postfix(GameCondition __instance)
    {
        try
        {
            if (__instance?.def is null || string.IsNullOrWhiteSpace(__instance.def.label) || __instance.TicksPassed < MinDays * GenDate.TicksPerDay)
            {
                return;
            }

            NewsRecorder.Record(NewsKind.ConditionEnded, NewsPhrasing.Condition(__instance.def.label, NewsRecorder.Place(__instance), ended: true));
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(ConditionEndedPatch), ex);
        }
    }
}

/// <summary>War and peace: a faction's relation kind with the colony or another faction changing.</summary>
[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_RelationKindChanged))]
public static class RelationChangedPatch
{
    public static void Postfix(Faction __instance, Faction other, FactionRelationKind previousKind)
    {
        try
        {
            if (__instance is null || other is null || __instance.Hidden || other.Hidden || __instance.IsPlayer)
            {
                return;
            }

            // The game notifies both sides. The colony's side is skipped above; between two other factions, keep one.
            if (!other.IsPlayer && string.CompareOrdinal(__instance.Name, other.Name) > 0)
            {
                return;
            }

            var now = __instance.RelationKindWith(other);
            if (now == previousKind)
            {
                return;
            }

            var text = NewsPhrasing.RelationChanged(__instance.Name, other.Name, NewsRecorder.ToCore(now), NewsRecorder.ToCore(previousKind));
            NewsRecorder.Record(NewsKind.RelationChanged, text, __instance.Name);
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(RelationChangedPatch), ex);
        }
    }
}

/// <summary>A faction leader dying. The prefix catches the old leader before the game picks a successor.</summary>
[HarmonyPatch(typeof(Faction), nameof(Faction.Notify_LeaderDied))]
public static class LeaderDiedPatch
{
    public static void Prefix(Faction __instance, out Pawn? __state) => __state = __instance?.leader;

    public static void Postfix(Faction __instance, Pawn? __state)
    {
        try
        {
            if (__instance is null || __state is null || __instance.Hidden || __instance.temporary || __instance.IsPlayer)
            {
                return;
            }

            var old = __state.Name?.ToStringShort ?? __state.LabelShort;
            var successor = __instance.leader is { } leader && leader != __state ? leader.Name?.ToStringShort ?? leader.LabelShort : null;
            NewsRecorder.Record(NewsKind.LeaderDied, NewsPhrasing.LeaderDied(__instance.Name, __instance.LeaderTitle, old, successor), __instance.Name);
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(LeaderDiedPatch), ex);
        }
    }
}

/// <summary>A settlement destroyed by the colony (the only way vanilla destroys one), and its faction wiped out if
/// it was the last.</summary>
[HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
public static class SettlementDefeatedPatch
{
    public static void Prefix(Settlement factionBase, out (string Name, Faction? Faction, bool WasDefeated) __state) =>
        __state = (factionBase?.Label ?? "", factionBase?.Faction, factionBase?.Faction?.defeated ?? false);

    public static void Postfix(Settlement factionBase, (string Name, Faction? Faction, bool WasDefeated) __state)
    {
        try
        {
            if (factionBase is null || !factionBase.Destroyed || __state.Faction is not { } faction || faction.IsPlayer)
            {
                return;
            }

            NewsRecorder.Record(NewsKind.SettlementDestroyed, NewsPhrasing.SettlementDestroyed(__state.Name, faction.Name, Faction.OfPlayer.Name), faction.Name);
            if (faction.defeated && !__state.WasDefeated)
            {
                NewsRecorder.Record(NewsKind.FactionDefeated, NewsPhrasing.FactionDefeated(faction.Name), faction.Name);
            }
        }
        catch (Exception ex)
        {
            Guard.Report(nameof(SettlementDefeatedPatch), ex);
        }
    }
}
