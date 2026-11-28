using RimChronicles.Core.Powers;
using RimChronicles.Game.Quests;

namespace RimChronicles.Game.Powers;

/// <summary>
/// The envoy letter's options. When the colony already stands with the other power, the envoy can't simply be
/// accepted alongside it: the letter says so, and offers staying with the ally or breaking with it to stand with the
/// power asking (decided 2026-11-27). Otherwise the usual three choices stand.
/// </summary>
public static class EnvoyChoices
{
    public sealed record Offer(string Text, IReadOnlyList<PlotChoice> Choices, string IfUnanswered);

    public static Offer For(RimChroniclesGameComponent component, Power asking, string text, string colony, IReadOnlyList<PlotChoice> usual,
        string usualIfUnanswered)
    {
        if (Power.All.FirstOrDefault(p => p != asking && PowerFactions.Running.Contains(p)
                && component.Allegiance.StanceToward(p) == PowerStance.Ally) is not { } ally)
        {
            return new(text, usual, usualIfUnanswered);
        }

        var allyName = PowerFactions.Of(ally)?.Name ?? ally.DisplayName;
        return new(text + AllegianceText.EnvoyAside(allyName, colony),
            [
                new PlotChoice(AllegianceText.StayWith(ally), PowerStance.Oppose),
                new PlotChoice(AllegianceText.BreakWith(ally, asking), PowerStance.Ally),
            ],
            AllegianceText.IfUnanswered(ally));
    }
}
