using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>Keeps the mod's own failures out of vanilla code. A Harmony patch or a daily driver that throws is logged
/// once (per name, per session) and skipped, so it can't break the game's tick, a pawn's death or a load, and can't
/// flood the log.</summary>
public static class Guard
{
    /// <summary>Logs <paramref name="ex"/> as the failure of <paramref name="what"/>, once per <paramref name="what"/>.</summary>
    public static void Report(string what, Exception ex) =>
        Log.ErrorOnce($"[Rim Chronicles] {what} failed; skipping it (logged once). {ex}", ("RimChronicles." + what).GetHashCode());
}
