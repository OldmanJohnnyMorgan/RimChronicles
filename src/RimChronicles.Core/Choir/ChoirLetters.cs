using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;

namespace RimChronicles.Core.Choir;

/// <summary>The Hollow Choir's endings before the War in Heaven reworks them (W11).</summary>
public static class ChoirEnding
{
    /// <summary>A colonist rose high in the Chorus and accepted the First Voice's offer.</summary>
    public const string Choral = "Choral";

    /// <summary>The colony silenced the Choir: its Cathedral fell.</summary>
    public const string Silenced = "Silenced";
}

/// <summary>The Hollow Choir plot's letters (template text) and news: the mirror of <see cref="EmpireLetters"/>.</summary>
public static class ChoirLetters
{
    public static (string Label, string Text) Envoy(string choir, string colony) => (
        "A Chorus envoy",
        $"A mechanitor of {choir} has come to {colony}, flanked by two silent mechs that hum in one voice. The Choir, the envoy "
        + "says, has woken, and the rim will be remade in war, as every people must be.\n\n"
        + $"Will {colony} stand with the Choir, stand against it, or keep out of its way?");

    public static (string Label, string Text) StanceTaken(string stance, string choir, string colony) => stance switch
    {
        PowerStance.Ally => ("Allied with the Hollow Choir",
            $"{colony} has joined its voice to {choir}'s. The envoy's mechs fall silent, which the Chorus call a blessing."),
        PowerStance.Oppose => ("At war with the Hollow Choir",
            $"{colony} has refused {choir}. The envoy leaves, and the hum on the horizon grows louder."),
        _ => ("Keeping out of it",
            $"{colony} will keep to itself. The envoy says nothing: the Choir is patient, and it will ask again."),
    };

    public static (string Label, string Text) Marches(string stance, string choir, string colony) => (
        "The Choir marches",
        $"{choir} is on the move. Its mechs are taking the rim settlement by settlement, singing as they go.\n\n" + stance switch
        {
            PowerStance.Ally => $"As its ally, {colony} can expect to be asked for help.",
            PowerStance.Oppose => $"{colony} has chosen to stand in its way, and its neighbours may come asking for help.",
            _ => $"{colony} has kept out of it so far. The Choir will want something for that.",
        });

    public static (string Label, string Text) TributeDemand(string choir, string colony, int silver) => (
        "The Choir demands tribute",
        $"A Chorus mechanitor has come to {colony} with a list of what the Choir needs. {choir} demands {silver} silver as the price "
        + $"of being left in peace.\n\n{colony} can pay, refuse, or join the Chorus.");

    public static (string Label, string Text) Reckoning(bool ascendant, string choir, string colony) => ascendant
        ? ("The Choir ascendant",
            $"{choir} now holds most of the rim. Its hum carries from every hill, and its final demands are going out.\n\n"
            + $"Whatever {colony} does next will decide how this ends.")
        : ("The Choir faltering",
            $"{choir} is reeling from its losses. Its mechs are falling silent one by one.\n\n"
            + $"Whatever {colony} does next will decide how this ends.");

    public static (string Label, string Text) FirstVoiceOffer(string choir, string colony, string holder) => (
        "The First Voice's offer",
        $"The First Voice of {choir} has heard of {holder}'s service. A Cantor brings the offer: {colony} and the lands around it, "
        + $"to be held by {holder} in the Choir's name, its people joined to the Chorus.\n\nAccepting would end {colony}'s story as a free "
        + "colony, and begin it as a voice in the Choir.");

    public static (string Label, string Text) FinalDemand(string choir, string colony) => (
        "The Choir's final demand",
        $"{choir} has stopped asking. Its Cantor sings the terms: {colony} will join the Chorus, or be counted among its enemies.\n\n"
        + "There is no third verse.");

    public static (string Label, string Text) Treaty(string choir, string colony) => (
        "A treaty of independence",
        $"{choir} is in no state to fight on. Its envoy comes with a treaty: the Choir will leave {colony} free and silent, "
        + "bound to no one, if the fighting stops.\n\nSigning ends the war, and the story of the Choir with it.");

    public static (string Name, string Description) CathedralQuest(string choir, string cathedral, string allies) => (
        $"Silence {cathedral}",
        $"{cathedral} is the heart of {choir}, where the First Voice sings. Take a force there and destroy it, and the Choir falls silent."
        + (allies.Length > 0 ? $" {allies} stand with you." : "")
        + $" You have {PowerArc.CapitalDays} days.");

    public static string EndingNews(string ending, string choir, string colony) => ending switch
    {
        ChoirEnding.Silenced => $"{colony} destroyed the Cathedral of {choir}, and the Choir has fallen silent",
        ChoirEnding.Choral => $"{colony} joined the Chorus, and holds its lands in the name of {choir}",
        _ => $"{choir} recognised {colony} as a free state",
    };

    public static string AftermathNews(PowerArc.JobResult r, string choir, string colony) => (r.Job, r.Success) switch
    {
        (PowerStep.EscortEnvoy, true) => $"Chorus envoys reached {r.Target} under the protection of {colony}",
        (PowerStep.ConquestJob, true) => $"{r.Target} fell to {choir} after its defences were sabotaged",
        (PowerStep.DefendAlly, true) => $"{colony} broke the Choir's siege of {r.Target}",
        (PowerStep.DefendAlly, false) => $"{r.Target} fell to {choir} after a siege",
        (PowerStep.SabotageImperial, true) => $"Saboteurs struck the Choir settlement of {r.Target}",
        (PowerStep.Tribute, _) when r.NewStance == PowerStance.Oppose => $"{colony} refused to pay tribute to {choir}",
        (PowerStep.Tribute, _) when r.NewStance == PowerStance.Ally => $"{colony} joined the Chorus",
        (PowerStep.FinalDemand, _) when r.NewStance == PowerStance.Ally => $"{colony} joined the Chorus",
        (PowerStep.FinalDemand, _) => $"{colony} refused the final demand of {choir}",
        (PowerStep.StormCapital, false) => $"An assault on the Choir's Cathedral at {r.Target} failed",
        _ => "",
    };
}
