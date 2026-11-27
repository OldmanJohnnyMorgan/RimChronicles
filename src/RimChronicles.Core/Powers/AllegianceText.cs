namespace RimChronicles.Core.Powers;

/// <summary>Letter text for the colony's side between the powers: the rival's envoy when the colony has already
/// sided with one, and the letter when a power turns against the colony because it sided with the other.</summary>
public static class AllegianceText
{
    /// <summary>Added to an envoy's letter when the colony already stands with <paramref name="ally"/> (the other power).</summary>
    public static string EnvoyAside(string ally, string colony) =>
        $"\n\n{colony} already stands with {ally}. To stand with this envoy's masters is to break that alliance: {ally} "
        + "would count the colony its enemy from that day on.";

    public static string StayWith(Power ally) => $"Stay with {ally.DisplayName}";

    public static string BreakWith(Power ally, Power asking) => $"Break with {ally.DisplayName} and stand with {asking.DisplayName}";

    /// <summary>What an unanswered letter means when the colony already stands with <paramref name="ally"/>.</summary>
    public static string IfUnanswered(Power ally) => $"the colony stays with {ally.DisplayName}";

    /// <summary>The letter when <paramref name="enemy"/> becomes the colony's enemy because it sided with
    /// <paramref name="side"/>. <paramref name="wasAlly"/>: it broke an alliance with the enemy to do it.</summary>
    public static (string Label, string Text) TurnedAgainst(string enemy, string side, bool wasAlly, string colony) => wasAlly
        ? ($"{Cap(enemy)} turns on {colony}",
            $"{colony} has broken its alliance with {enemy} to stand with {side}. {Cap(enemy)} counts the colony a traitor and an "
            + "enemy now: its envoys are gone, and its soldiers will follow.")
        : ($"{Cap(enemy)} takes note",
            $"By standing with {side}, {colony} has made itself the enemy of {enemy}. Word travels fast between the two, and "
            + $"{enemy} will treat the colony as {side}'s creature from now on.");

    private static string Cap(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
}
