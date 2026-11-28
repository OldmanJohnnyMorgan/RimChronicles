using RimChronicles.Core.Persistence;
using RimChronicles.Core.Powers;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

/// <summary>The colony's side (War in Heaven W2, tech spec R8).</summary>
public class AllegianceTests
{
    private static readonly Power Rival = new()
    {
        Id = "rival",
        NewsKind = "Rival",
        PlotTemplateId = "RivalPlot",
        CrisisTemplateId = "RivalCrisis",
        OptionPrefix = "Rival",
        EnvoyOption = "Act1:RivalEnvoy",
        MarchOption = "Act2:RivalMarches",
        Text = Power.Empire.Text,
    };

    private static readonly IReadOnlyList<Power> Both = [Power.Empire, Rival];

    [Fact]
    public void ANewColony_IsUndeclaredAndNeutralToEveryone()
    {
        var a = new Allegiance();
        Assert.Equal(Side.Undeclared, a.SideAmong(Both));
        Assert.All(Both, p => Assert.Equal(PowerStance.Neutral, a.StanceToward(p)));
    }

    [Fact]
    public void SidingWithOnePower_MakesTheColonyTheOthersEnemy()
    {
        var a = new Allegiance();
        var changed = a.Set(Power.Empire, PowerStance.Ally, Both, 50, AllegianceCause.Plot);

        Assert.Equal([(Power.Empire, PowerStance.Ally), (Rival, PowerStance.Oppose)], changed);
        Assert.Equal(Power.Empire.Id, a.SideAmong(Both));
        Assert.Equal(PowerStance.Oppose, a.StanceToward(Rival));
        Assert.Equal(["Day 50: empire neutral -> ally (plot)", "Day 50: rival neutral -> oppose (rival)"], a.Log);
    }

    [Fact]
    public void ChangingSides_TurnsOnTheOldAlly()
    {
        var a = new Allegiance();
        a.Set(Power.Empire, PowerStance.Ally, Both, 50, AllegianceCause.Plot);
        var changed = a.Set(Rival, PowerStance.Ally, Both, 90, AllegianceCause.Dev);

        Assert.Equal([(Rival, PowerStance.Ally), (Power.Empire, PowerStance.Oppose)], changed);
        Assert.Equal(Rival.Id, a.SideAmong(Both));
    }

    [Fact]
    public void OpposingOnePower_LeavesTheOtherAlone()
    {
        var a = new Allegiance();
        var changed = a.Set(Power.Empire, PowerStance.Oppose, Both, 50, AllegianceCause.Plot);

        Assert.Equal([(Power.Empire, PowerStance.Oppose)], changed);
        Assert.Equal(PowerStance.Neutral, a.StanceToward(Rival));
        Assert.Equal(Side.Undeclared, a.SideAmong(Both));
        Assert.Equal(Side.Independent, a.SideAmong([Power.Empire])); // with only the Empire, opposing it is independence
    }

    [Fact]
    public void Independence_OpposesEveryPower()
    {
        var a = new Allegiance();
        a.Set(Power.Empire, PowerStance.Ally, Both, 50, AllegianceCause.Plot);
        var changed = a.DeclareIndependence(Both, 80, AllegianceCause.Summons);

        Assert.Equal([(Power.Empire, PowerStance.Oppose)], changed); // the rival was already an enemy
        Assert.Equal(Side.Independent, a.SideAmong(Both));
    }

    [Fact]
    public void SettingTheSameStanceAgain_ChangesNothing()
    {
        var a = new Allegiance();
        a.Set(Power.Empire, PowerStance.Oppose, Both, 50, AllegianceCause.Plot);
        Assert.Empty(a.Set(Power.Empire, PowerStance.Oppose, Both, 60, AllegianceCause.Plot));
        Assert.Single(a.Log);
    }

    [Fact]
    public void ItSavesAndLoads_AndAnOldSaveLoadsUndeclared()
    {
        var a = new Allegiance();
        a.Set(Power.Empire, PowerStance.Ally, Both, 50, AllegianceCause.Plot);
        var save = new DictionaryArchive(ArchiveMode.Saving);
        save.Deep(ref a, "allegiance");

        Allegiance loaded = null!;
        new DictionaryArchive(ArchiveMode.Loading, save.Data).Deep(ref loaded, "allegiance");
        Assert.Equal(PowerStance.Ally, loaded.StanceToward(Power.Empire));
        Assert.Equal(PowerStance.Oppose, loaded.StanceToward(Rival));
        Assert.Equal(2, loaded.Log.Count);

        Allegiance old = null!;
        new DictionaryArchive(ArchiveMode.Loading, []).Deep(ref old, "allegiance");
        Assert.Equal(Side.Undeclared, old.SideAmong(Both));
    }

    [Fact]
    public void EnvoyAnswers_QueueInOrder_AreSaved_AndTakenOnce()
    {
        var a = new Allegiance();
        a.Answered(Power.Empire, PowerStance.Ally, 30);
        a.Answered(Rival, PowerStance.Ally, 30);

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        a.Expose(saving);
        var loaded = new Allegiance();
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        Assert.Equal([Power.Empire.Id, Rival.Id], loaded.TakeAnswers().Select(e => e.Power));
        Assert.Empty(loaded.TakeAnswers());
    }

    [Fact]
    public void TheLaterOfTwoAllyAnswers_Wins()
    {
        // As the daily sync applies them: in the order given.
        var a = new Allegiance();
        a.Answered(Power.Empire, PowerStance.Ally, 30);
        a.Answered(Rival, PowerStance.Ally, 30);
        foreach (var answer in a.TakeAnswers())
        {
            a.Set(Both.First(p => p.Id == answer.Power), answer.Stance, Both, 30, AllegianceCause.Plot);
        }

        Assert.Equal(Rival.Id, a.SideAmong(Both));
        Assert.Equal(PowerStance.Oppose, a.StanceToward(Power.Empire));
    }

    [Theory]
    [InlineData(PowerStance.Ally, PowerStance.Oppose, true)]
    [InlineData(PowerStance.Neutral, PowerStance.Oppose, true)]
    [InlineData(PowerStance.Oppose, PowerStance.Oppose, false)]
    [InlineData(PowerStance.Neutral, PowerStance.Ally, false)]
    public void TurnsAgainst_OnlyWhenThePowerBecomesAnEnemy(string was, string now, bool expected) =>
        Assert.Equal(expected, Allegiance.TurnsAgainst(was, now));

    [Fact]
    public void Letters_SayWhatSwitchingSidesMeans()
    {
        Assert.Equal("Stay with the Empire", AllegianceText.StayWith(Power.Empire));
        Assert.Equal("Break with the Empire and stand with the Hollow Choir", AllegianceText.BreakWith(Power.Empire, Power.Choir));
        Assert.Contains("Wyrmfold already stands with the Shattered Empire", AllegianceText.EnvoyAside("the Shattered Empire", "Wyrmfold"));

        var (label, text) = AllegianceText.TurnedAgainst("the Shattered Empire", "the Hollow Choir", true, "Wyrmfold");
        Assert.Equal("The Shattered Empire turns on Wyrmfold", label);
        Assert.Contains("broken its alliance", text);
        Assert.Equal("The Shattered Empire takes note", AllegianceText.TurnedAgainst("the Shattered Empire", "the Hollow Choir", false, "Wyrmfold").Label);
    }
}
