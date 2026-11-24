using RimChronicles.Core.Powers;

namespace RimChronicles.Core.Empire;

/// <summary>The three endings of the major plot (design spec, Stage 5).</summary>
public static class EmpireEnding
{
    /// <summary>The colony and its allies destroyed the Imperial capital.</summary>
    public const string Resistance = "Resistance";

    /// <summary>A colonist rose high in the Empire's service and accepted a governorship.</summary>
    public const string Imperial = "Imperial";

    /// <summary>The colony forced a treaty recognising it as a free state.</summary>
    public const string Independence = "Independence";
}

/// <summary>The Empire plot's letters (template text) and news.</summary>
public static class EmpireLetters
{
    public static (string Label, string Text) Envoy(string empire, string colony) => (
        "An Imperial envoy",
        $"An envoy of {empire} has come to {colony}, under banner and escort. The Empire, the envoy says, is waking from a long "
        + "sleep, and every settlement on the rim will soon have to decide where it stands.\n\n"
        + $"Will {colony} stand with the Empire, stand against it, or keep out of its way?");

    public static (string Label, string Text) StanceTaken(string stance, string empire, string colony) => stance switch
    {
        PowerStance.Ally => ("Allied with the Empire",
            $"{colony} has pledged itself to {empire}. The envoy leaves pleased. The Empire will have work for its friends."),
        PowerStance.Oppose => ("At war with the Empire",
            $"{colony} has refused {empire}. The envoy leaves without a word, and {empire} now counts {colony} among its enemies."),
        _ => ("Keeping out of it",
            $"{colony} will keep to itself. The envoy shrugs: the Empire will remember who stood aside, and it will ask for its due."),
    };

    public static (string Label, string Text) Marches(string stance, string empire, string colony) => (
        "The Empire marches",
        $"{empire} is on the move. Its columns are taking the rim settlement by settlement.\n\n" + stance switch
        {
            PowerStance.Ally => $"As its ally, {colony} can expect to be asked for help.",
            PowerStance.Oppose => $"{colony} has chosen to stand in its way, and its neighbours may come asking for help.",
            _ => $"{colony} has kept out of it so far. The Empire will want something for that.",
        });

    public static (string Label, string Text) TributeDemand(string empire, string colony, int silver) => (
        "The Empire demands tribute",
        $"An Imperial tax-collector has come to {colony}. {empire} demands {silver} silver as the price of being left in peace.\n\n"
        + $"{colony} can pay, refuse, or bend the knee and join the Empire's cause.");

    public static (string Label, string Text) Reckoning(bool ascendant, string empire, string colony) => ascendant
        ? ("The Empire ascendant",
            $"{empire} now holds most of the rim. Its final demands are going out to every settlement still standing.\n\n"
            + $"Whatever {colony} does next will decide how this ends.")
        : ("The Empire breaking",
            $"{empire} is reeling from its losses. Its columns are pulling back and its officers quarrel.\n\n"
            + $"Whatever {colony} does next will decide how this ends.");

    public static (string Label, string Text) Governorship(string empire, string colony, string holder) => (
        "An Imperial governorship",
        $"{empire} has taken note of {holder}'s service. An Imperial herald brings the offer: {colony} and the lands around it, "
        + $"to be governed in the Empire's name by {holder}.\n\nAccepting would end {colony}'s story as a free colony, and begin it as an Imperial province.");

    public static (string Label, string Text) FinalDemand(string empire, string colony) => (
        "The Empire's final demand",
        $"{empire} has stopped asking. Its herald reads the terms aloud: {colony} will submit and serve the Empire, or be counted among its enemies.\n\n"
        + "There is no third option on the parchment.");

    public static (string Label, string Text) Treaty(string empire, string colony) => (
        "A treaty of independence",
        $"{empire} is in no state to fight on. Its envoy comes with a treaty: the Empire will recognise {colony} as a free state, "
        + "bound to no one, if the fighting stops.\n\nSigning ends the war, and the story of the Empire with it.");

    public static (string Name, string Description) CapitalQuest(string empire, string capital, string allies) => (
        $"Storm {capital}",
        $"{capital} is the heart of {empire}. Take a force there and destroy it, and the Empire falls with it."
        + (allies.Length > 0 ? $" {allies} stand with you." : "")
        + $" You have {PowerArc.CapitalDays} days.");

    public static string EndingNews(string ending, string empire, string colony) => ending switch
    {
        EmpireEnding.Resistance => $"{colony} destroyed the capital of {empire}, and the Empire has fallen",
        EmpireEnding.Imperial => $"{colony} became an Imperial province, governed in the name of {empire}",
        _ => $"{empire} recognised {colony} as a free state",
    };

    public static string AftermathNews(PowerArc.JobResult r, string empire, string colony) => (r.Job, r.Success) switch
    {
        (PowerStep.EscortEnvoy, true) => $"Imperial envoys reached {r.Target} under the protection of {colony}",
        (PowerStep.ConquestJob, true) => $"{r.Target} fell to {empire} after its defences were sabotaged",
        (PowerStep.DefendAlly, true) => $"{colony} broke the Imperial siege of {r.Target}",
        (PowerStep.DefendAlly, false) => $"{r.Target} fell to {empire} after a siege",
        (PowerStep.SabotageImperial, true) => $"Saboteurs struck the Imperial settlement of {r.Target}",
        (PowerStep.Tribute, _) when r.NewStance == PowerStance.Oppose => $"{colony} refused to pay tribute to {empire}",
        (PowerStep.Tribute, _) when r.NewStance == PowerStance.Ally => $"{colony} swore fealty to {empire}",
        (PowerStep.FinalDemand, _) when r.NewStance == PowerStance.Ally => $"{colony} submitted to {empire}",
        (PowerStep.FinalDemand, _) => $"{colony} refused the final demand of {empire}",
        (PowerStep.StormCapital, false) => $"An assault on the Imperial capital of {r.Target} failed",
        _ => "",
    };
}
