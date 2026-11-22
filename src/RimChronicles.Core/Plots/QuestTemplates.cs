namespace RimChronicles.Core.Plots;

/// <summary>The quest templates (design spec, Stage 4; Escort, Defend ally and Sabotage from Stage 5) and the
/// rules that size them. The model never picks these numbers.</summary>
public static class QuestTemplates
{
    public const string Hunt = "Hunt";
    public const string Rescue = "Rescue";
    public const string Deliver = "Deliver";
    public const string Escort = "Escort";
    public const string DefendAlly = "DefendAlly";
    public const string Sabotage = "Sabotage";

    /// <summary>The QuestScriptDef each template runs.</summary>
    public static string ScriptDef(string template) => $"RimChronicles_{template}";

    /// <summary>Silver for a success: 60% of the storyteller's threat points, rounded to 10, between 150 and 1,500.</summary>
    public static int RewardSilver(float threatPoints) => (int)Math.Clamp(Math.Round(threatPoints * 0.6f / 10f) * 10f, 150f, 1500f);

    /// <summary>Days a quest site stays before it vanishes and the quest fails.</summary>
    public const int SiteDays = 15;

    /// <summary>Days a delivery may take once accepted.</summary>
    public const int DeliverDays = 20;

    /// <summary>Days an escort may take once accepted.</summary>
    public const int EscortDays = 20;

    /// <summary>Days to destroy a Sabotage objective once accepted.</summary>
    public const int SabotageDays = 20;
}
