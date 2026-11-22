namespace RimChronicles.Core.Crisis;

/// <summary>The crisis's news and letters (template text). The news agent announces every stage change (fairness rule 7).</summary>
public static class CrisisText
{
    /// <summary>News for the reporter when the stage changes; empty for <see cref="CrisisStage.None"/> after a crisis ends quietly.</summary>
    public static string StageNews(CrisisStage stage, CrisisStage before, string empire, string colony) => stage switch
    {
        CrisisStage.Probing when before == CrisisStage.None => $"Imperial scouts have been seen testing the defenses of {colony}",
        CrisisStage.Probing => $"{empire} has pulled back from its campaign against {colony} to probing raids",
        CrisisStage.Reclamation when before > stage => $"{empire} is losing its grip, and its war on {colony} is faltering",
        CrisisStage.Reclamation => $"{empire} is sending real forces against {colony}: breachers and psykers",
        CrisisStage.War => $"{empire} is at open war with {colony}: sieges and orbital strikes are expected",
        CrisisStage.Finale => $"{empire} is throwing everything it has at {colony}",
        _ when before != CrisisStage.None => $"The Imperial campaign against {colony} has stopped",
        _ => "",
    };

    /// <summary>A letter for an escalation; null when the stage went down or the crisis ended.</summary>
    public static (string Label, string Text)? StageLetter(CrisisStage stage, CrisisStage before, string empire, string colony)
    {
        if (stage <= before)
        {
            return null;
        }

        return stage switch
        {
            CrisisStage.Probing => ("Imperial scouts",
                $"{empire} has started testing {colony}. Small forces will probe the defenses and pull back to report what they found.\n\n"
                + "Each raid that learns the colony's layout makes the next one sharper."),
            CrisisStage.Reclamation => ("The Empire escalates",
                $"{empire} is done probing. Breachers who can knock out turrets and open walls, and psykers who can turn a fight, "
                + $"will come for {colony} now."),
            CrisisStage.War => ("Open war with the Empire",
                $"{empire} is at war with {colony}. Expect sieges that dig in and grow, orbital strikes called by Imperial heralds, "
                + "and drop pods aimed at the colony's leaders.\n\nStrike the siege camps early, before they're finished."),
            CrisisStage.Finale => ("The Empire's last campaign",
                $"{empire} is throwing everything it has at {colony}. Its best troops and its High Stellarch will lead the attacks."),
            _ => null,
        };
    }
}
