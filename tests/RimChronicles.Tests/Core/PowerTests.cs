using RimChronicles.Core.Plots;
using RimChronicles.Core.Powers;

namespace RimChronicles.Tests.Core;

/// <summary>The saga and the arc read a power's ids and texts from its <see cref="Power"/> (War in Heaven W1), so a
/// second power gets its own options and news from the same rules.</summary>
public class PowerTests
{
    private sealed class StubText : PowerText
    {
        public override string StirringNews => "a hum in the hills";

        public override string AwakenedNews(string name) => $"{name} woke";

        public override IReadOnlyList<string> Rumours(string name, IReadOnlyList<string> neighbours) => [$"{name} sang"];

        public override string TriggerWhy(string trigger, string detail, string colony) => $"why:{trigger}";

        public override (string Label, string Text) AwakenedLetter(string name) => ("Awake", name);

        public override string Describe(string step) => $"stub {step}";

        public override string ExpansionNews(ExpansionResult result, string name, ExpansionTarget target) => $"{name} hit {target.Name}";
    }

    private static readonly Power Stub = new()
    {
        Id = "stub",
        NewsKind = "Stub",
        PlotTemplateId = "StubPlot",
        CrisisTemplateId = "StubCrisis",
        OptionPrefix = "Stub",
        EnvoyOption = "Act1:StubEnvoy",
        MarchOption = "Act2:StubMarches",
        Text = new StubText(),
    };

    private static (PlotBook Book, Plot Plot, PowerSaga Saga) NewPlot(Power power)
    {
        var book = new PlotBook();
        var plot = book.Start(new PlotTemplate { Id = power.PlotTemplateId, CountsTowardCap = false },
            new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" }, 40, 3, "stirring");
        return (book, plot, new PowerSaga { Power = power });
    }

    [Fact]
    public void TheEmpire_KeepsItsIds()
    {
        Assert.Equal("empire", Power.Empire.Id);
        Assert.Equal("Empire", Power.Empire.NewsKind);
        Assert.Equal("AwakenedEmpire", Power.Empire.PlotTemplateId);
        Assert.Equal("ImperialCrisis", Power.Empire.CrisisTemplateId);
        Assert.Same(Power.Empire, new PowerSaga().Power);
        Assert.Equal([Power.Empire, Power.Choir], Power.All);
    }

    [Fact]
    public void AnotherPower_GetsItsOwnOptionsAndDescriptions()
    {
        var (book, plot, saga) = NewPlot(Stub);
        saga.Awaken(AwakeningTrigger.Dev, "", 40, 20, "Wyrmfold");
        var envoy = PowerArc.Transitions(plot, saga, 41).Single();
        Assert.Equal(("Act1:StubEnvoy", "stub Envoy"), (envoy.Id, envoy.Desc));

        book.Advance(plot, PlotBeat.Hook, 43, 6, "envoy");
        saga.Wake(60, "the Stub");
        Assert.Equal(["Act2:StubMarches"], PowerArc.Transitions(plot, saga, 61).Select(o => o.Id));

        book.Advance(plot, PlotBeat.Complication, 62, 6, "marches");
        plot.SetFact(PowerArc.Stance, PowerStance.Oppose);
        Assert.Equal(["Stub:DefendAlly", "Stub:SabotageImperial", "Wait"], PowerArc.Transitions(plot, saga, 70).Select(o => o.Id));
    }

    [Fact]
    public void AnotherPower_WritesItsOwnNews()
    {
        var saga = new PowerSaga { Power = Stub };
        Assert.Equal("a hum in the hills", saga.Awaken(AwakeningTrigger.Wealth, "160000", 40, 20, "Wyrmfold").NewsText);
        Assert.EndsWith("why:Wealth", saga.Log[^1]);
        var woke = saga.Wake(60, "the Stub");
        Assert.Equal(("the Stub woke", "Awake", "the Stub"), (woke.NewsText, woke.LetterLabel, woke.LetterText));
        var hit = saga.Expanded(new ExpansionTarget("Ashgrove", "Ashgrove", "The Free Rim", 10, 2), ExpansionResult.Conquered, 70, 20, "the Stub");
        Assert.Equal("the Stub hit Ashgrove", hit.NewsText);
    }

    [Fact]
    public void TheChoir_IsDrawnByAMechanitorColonist_TheEmpireByAnImperialOne()
    {
        var signs = new PowerSigns(0, "Ada");
        Assert.Equal(["Awaken:MechanitorColonist"], new PowerSaga { Power = Power.Choir }.Triggers(signs, 40).Select(o => o.Id));
        Assert.Equal(["Awaken:ImperialColonist"], new PowerSaga().Triggers(signs, 40).Select(o => o.Id));
        Assert.Equal("Ada", new PowerSaga { Power = Power.Choir }.DetailFor(AwakeningTrigger.MechanitorColonist, signs));
        Assert.Contains("mechanitor", Power.Choir.Text.TriggerWhy(AwakeningTrigger.MechanitorColonist, "Ada", "Wyrmfold"));
    }
}
