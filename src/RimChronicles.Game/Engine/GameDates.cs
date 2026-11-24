using RimWorld;
using UnityEngine;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>The in-game calendar date of a day number, "5th of Aprimay, 5501", as the game's own date readout shows it
/// at the colony's longitude. UI helper (Unity stub <see cref="Vector2"/> allowed in UI code). Main thread only.</summary>
public static class GameDates
{
    private static readonly Dictionary<int, string> Cache = [];
    private static int _cacheStart = int.MinValue;

    /// <summary>The date of <paramref name="day"/> (days since the game started), or "" when no game is running.</summary>
    public static string Of(int day)
    {
        if (Current.Game is null || Find.TickManager is not { } ticks)
        {
            return "";
        }

        if (_cacheStart != ticks.gameStartAbsTick)
        {
            Cache.Clear(); // a different game
            _cacheStart = ticks.gameStartAbsTick;
        }

        if (!Cache.TryGetValue(day, out var date))
        {
            // Midday, so a longitude offset can't tip the date into the day before or after.
            var abs = GenDate.TickGameToAbs(day * GenDate.TicksPerDay + GenDate.TicksPerDay / 2);
            var location = Find.AnyPlayerHomeMap is { } map ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero;
            date = GenDate.DateFullStringAt(abs, location);
            Cache[day] = date;
        }

        return date;
    }

    /// <summary>"Day 21 · 5th of Aprimay, 5501", or "Day 21" without a game.</summary>
    public static string DayAndDate(int day) => Of(day) is { Length: > 0 } date ? $"Day {day}  ·  {date}" : $"Day {day}";
}
