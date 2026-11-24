using RimChronicles.Core.Crisis;
using RimChronicles.Core.Empire;
using RimChronicles.Core.Powers;
using RimChronicles.Core.Persistence;
using RimChronicles.Core.Plots;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class CrisisTests
{
    private static readonly PlotTemplate EmpireTemplate = new() { Id = Power.Empire.PlotTemplateId, CountsTowardCap = false };
    private static readonly PlotTemplate CrisisTemplate = new() { Id = CrisisArc.TemplateId, CountsTowardCap = false, MinGapDays = 3, MaxGapDays = 6 };
    private static readonly ProtagonistSlot Colony = new() { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = "Wyrmfold" };

    /// <summary>An awake Empire whose plot is in Act 2 with the given stance.</summary>
    private static (PlotBook Book, Plot Plot, PowerSaga Saga) Act2(string stance)
    {
        var book = new PlotBook();
        var plot = book.Start(EmpireTemplate, Colony, 40, 3, "stirring");
        var saga = new PowerSaga { State = PowerState.Awakened };
        plot.SetFact(PowerArc.Stance, stance);
        book.Advance(plot, PlotBeat.Complication, 60, 6, "marches");
        return (book, plot, saga);
    }

    private static void Expansions(PowerSaga saga, int count)
    {
        for (var i = 0; i < count; i++)
        {
            saga.Expansions.Add(new ExpansionRecord { Day = 60 + i, Settlement = $"Town {i}", Faction = "Rivals", Result = ExpansionResult.Conquered });
        }
    }

    private static CrisisStage StageOf(Plot? plot, PowerSaga saga, bool hostile = false, int survived = 0) =>
        CrisisStages.Of(new CrisisInputs { EmpirePlot = plot, Saga = saga, EmpireHostile = hostile, OperationsSurvived = survived });

    // ---- stages

    [Fact]
    public void Stage_NoneBeforeTheEmpireWakes_OrBeforeAct2()
    {
        var book = new PlotBook();
        var plot = book.Start(EmpireTemplate, Colony, 40, 3, "stirring");
        plot.SetFact(PowerArc.Stance, PowerStance.Oppose);
        Assert.Equal(CrisisStage.None, StageOf(plot, new PowerSaga { State = PowerState.Stirring }));
        Assert.Equal(CrisisStage.None, StageOf(plot, new PowerSaga { State = PowerState.Awakened })); // still Act 1
        Assert.Equal(CrisisStage.None, StageOf(null, new PowerSaga { State = PowerState.Awakened }));
    }

    [Fact]
    public void Stage_NoneForAnAlly_EvenIfHostile_AndForANeutralColonyAtPeace()
    {
        var (_, ally, saga) = Act2(PowerStance.Ally);
        Assert.Equal(CrisisStage.None, StageOf(ally, saga, hostile: true));

        var (_, neutral, saga2) = Act2(PowerStance.Neutral);
        Assert.Equal(CrisisStage.None, StageOf(neutral, saga2));
        Assert.Equal(CrisisStage.Probing, StageOf(neutral, saga2, hostile: true)); // e.g. the Empire turned on it over a ship
    }

    [Fact]
    public void Stage_Act2EscalatesWithExpansionsAndSurvivedOperations()
    {
        var (_, plot, saga) = Act2(PowerStance.Oppose);
        Assert.Equal(CrisisStage.Probing, StageOf(plot, saga));
        Assert.Equal(CrisisStage.Reclamation, StageOf(plot, saga, survived: CrisisStages.ReclamationAfter));

        Expansions(saga, CrisisStages.ReclamationAfter);
        Assert.Equal(CrisisStage.Reclamation, StageOf(plot, saga));

        Expansions(saga, PowerArc.ExpansionsForReckoning - CrisisStages.WarBeforeReckoning - CrisisStages.ReclamationAfter);
        Assert.Equal(CrisisStage.War, StageOf(plot, saga));
    }

    [Fact]
    public void Stage_Act3IsWarAgainstAnAscendantEmpire_AndCappedAtReclamationWhenBroken()
    {
        var (book, plot, saga) = Act2(PowerStance.Oppose);
        book.Advance(plot, PlotBeat.Choice, 90, 6, "reckoning");
        saga.State = PowerState.Ascendant;
        Assert.Equal(CrisisStage.War, StageOf(plot, saga));

        saga.State = PowerState.Broken;
        Assert.Equal(CrisisStage.Reclamation, StageOf(plot, saga));

        PowerArc.StartJob(plot, PowerStep.StormCapital, "Capital");
        Assert.Equal(CrisisStage.Reclamation, StageOf(plot, saga)); // a broken Empire never reaches the finale
        saga.State = PowerState.Ascendant;
        Assert.Equal(CrisisStage.Finale, StageOf(plot, saga));
    }

    [Fact]
    public void Stage_FinaleOnAShipLaunch_OrARefusedFinalDemand()
    {
        var (_, plot, saga) = Act2(PowerStance.Oppose);
        saga.NextShipRaidDay = 70;
        Assert.Equal(CrisisStage.Finale, StageOf(plot, saga));

        var (book, neutral, saga2) = Act2(PowerStance.Neutral);
        book.Advance(neutral, PlotBeat.Choice, 90, 6, "reckoning");
        saga2.State = PowerState.Ascendant;
        PowerArc.StartJob(neutral, PowerStep.FinalDemand, "", 90);
        neutral.SetFact(PowerArc.Answer, PowerArc.Decline);
        PowerArc.FinishJob(neutral);
        Assert.Equal(PowerArc.CauseFinalDemand, neutral.Fact(PowerArc.StanceCause));
        Assert.Equal(CrisisStage.Finale, StageOf(neutral, saga2));
    }

    [Fact]
    public void Stage_ARefusedTributeOnlyBringsProbing_UntilTheColonyFinishesAJobAgainstTheEmpire()
    {
        var (_, plot, saga) = Act2(PowerStance.Neutral);
        Expansions(saga, PowerArc.ExpansionsForReckoning - CrisisStages.WarBeforeReckoning); // War for an opposed colony
        PowerArc.StartJob(plot, PowerStep.Tribute, "", 70);
        plot.SetFact(PowerArc.Answer, PowerArc.TributeRefused);
        PowerArc.FinishJob(plot);
        Assert.Equal(PowerStance.Oppose, PowerArc.StanceOf(plot));
        Assert.Equal(CrisisStage.Probing, StageOf(plot, saga, hostile: true));

        PowerArc.StartJob(plot, PowerStep.DefendAlly, "Marrow Hold", 80);
        plot.SetFact(PowerArc.JobQuestFact(plot)!, QuestOutcome.Success);
        PowerArc.FinishJob(plot);
        Assert.Equal(CrisisStage.War, StageOf(plot, saga, hostile: true));
    }

    [Fact]
    public void Stage_NoneOnceTheStoryEnds()
    {
        var (_, plot, saga) = Act2(PowerStance.Oppose);
        plot.SetFact(PowerArc.EndingFact, EmpireEnding.Resistance);
        Assert.Equal(CrisisStage.None, StageOf(plot, saga));
        plot.SetFact(PowerArc.EndingFact, "");
        saga.Ending = EmpireEnding.Independence;
        Assert.Equal(CrisisStage.None, StageOf(plot, saga));
    }

    // ---- budget

    private static readonly ColonyStrength Reference = new() { Fighters = 10, Gear = 0.5f, Defenses = 12.5f, PsylinkLevels = 0, Wealth = 0 };

    [Fact]
    public void Budget_IsTheStageBaseAtTheReferenceColony()
    {
        Assert.Equal(ColonyStrength.Reference, Reference.Score, 1);
        Assert.Equal(ThreatBudget.Base(CrisisStage.War), ThreatBudget.Compute(CrisisStage.War, 1f, Reference), 1);
        Assert.Equal(0f, ThreatBudget.Compute(CrisisStage.None, 1f, Reference));
    }

    [Fact]
    public void Budget_GrowsWithEachStage()
    {
        var stages = new[] { CrisisStage.Probing, CrisisStage.Reclamation, CrisisStage.War, CrisisStage.Finale };
        var budgets = stages.Select(s => ThreatBudget.Compute(s, 1f, Reference)).ToList();
        Assert.Equal(budgets.OrderBy(b => b), budgets);
        Assert.Equal(budgets.Count, budgets.Distinct().Count());
    }

    [Fact]
    public void Budget_StrengthIsClamped_SoAPoorColonyStillFacesTheCrisis()
    {
        var empty = new ColonyStrength();
        var huge = new ColonyStrength { Fighters = 200, Gear = 1f, Defenses = 500, PsylinkLevels = 100, Wealth = 10_000_000 };
        Assert.Equal(ThreatBudget.MinStrengthFactor, ThreatBudget.StrengthFactor(empty));
        Assert.Equal(ThreatBudget.MaxStrengthFactor, ThreatBudget.StrengthFactor(huge));
        Assert.Equal(ThreatBudget.Base(CrisisStage.Probing) * ThreatBudget.MinStrengthFactor, ThreatBudget.Compute(CrisisStage.Probing, 1f, empty), 1);
    }

    [Fact]
    public void Budget_WealthCountsForLittle_AndPsylinksCount()
    {
        var poor = Reference;
        var rich = Reference with { Wealth = 300_000 };
        Assert.True(rich.Score - poor.Score < ColonyStrength.PerFighter * 4); // 300k wealth is worth less than 4 fighters
        Assert.True((Reference with { PsylinkLevels = 6 }).Score > Reference.Score);
    }

    [Fact]
    public void Budget_ScalesWithDifficulty_Clamped_AndShrinksForABrokenEmpire()
    {
        var normal = ThreatBudget.Compute(CrisisStage.War, 1f, Reference);
        Assert.Equal(normal * 2, ThreatBudget.Compute(CrisisStage.War, 2f, Reference), 1);
        Assert.Equal(normal * ThreatBudget.MaxDifficulty, ThreatBudget.Compute(CrisisStage.War, 100f, Reference), 1);
        Assert.Equal(normal * ThreatBudget.MinDifficulty, ThreatBudget.Compute(CrisisStage.War, -1f, Reference), 1);
        Assert.Equal(normal * ThreatBudget.BrokenFactor, ThreatBudget.Compute(CrisisStage.War, 1f, Reference, broken: true), 1);
    }

    [Fact]
    public void Budget_BadInputsNeverGoNegative()
    {
        var bad = new ColonyStrength { Fighters = -5, Gear = 7f, Defenses = -3, PsylinkLevels = -2, Wealth = -100 };
        Assert.Equal(0f, bad.Score);
    }

    // ---- the arc

    private static readonly HashSet<string> AllBuilt = CrisisOperations.All.Select(o => o.Step).ToHashSet();

    private static (PlotBook Book, Plot Plot) CrisisPlot()
    {
        var book = new PlotBook();
        return (book, book.Start(CrisisTemplate, Colony, 60, 2, "probing"));
    }

    private static CrisisContext Context(CrisisStage stage, CrisisState? state = null, IReadOnlySet<string>? built = null, bool captive = false, int gap = CrisisArc.DefaultGapDays) =>
        new() { Stage = stage, State = state ?? new CrisisState(), Built = built ?? AllBuilt, HoldsCaptive = captive, GapDays = gap };

    private static List<string> Ids(Plot plot, CrisisContext context, int today = 100) =>
        CrisisArc.Transitions(plot, context, today).Select(o => o.Id).ToList();

    [Theory]
    [InlineData(CrisisStage.Probing, new[] { "Operation:ProbingRaid", "Wait" })]
    [InlineData(CrisisStage.Reclamation, new[] { "Operation:ProbingRaid", "Operation:Breach", "Operation:PsychicAssault", "Wait" })]
    [InlineData(CrisisStage.War, new[] { "Operation:ProbingRaid", "Operation:Breach", "Operation:PsychicAssault", "Operation:SiegeWithBombardment", "Operation:Decapitation", "Wait" })]
    [InlineData(CrisisStage.Finale, new[] { "Operation:ProbingRaid", "Operation:Breach", "Operation:PsychicAssault", "Operation:SiegeWithBombardment", "Operation:Decapitation", "Operation:FinalAssault", "Wait" })]
    public void Arc_OffersTheOperationsEachStageUnlocks(CrisisStage stage, string[] expected)
    {
        var (_, plot) = CrisisPlot();
        Assert.Equal(expected, Ids(plot, Context(stage)));
    }

    [Fact]
    public void Arc_OffersNothingWithoutACrisis_OrBeforeAnyOperationIsBuilt()
    {
        var (_, plot) = CrisisPlot();
        Assert.Empty(Ids(plot, Context(CrisisStage.None)));
        Assert.Empty(Ids(plot, Context(CrisisStage.War, built: new HashSet<string>())));
        Assert.Equal(["Operation:ProbingRaid", "Wait"], Ids(plot, Context(CrisisStage.War, built: new HashSet<string> { CrisisOperations.ProbingRaid })));
    }

    [Fact]
    public void Arc_RetrievalOnlyWhileTheColonyHoldsACaptive()
    {
        var (_, plot) = CrisisPlot();
        Assert.DoesNotContain("Operation:Retrieval", Ids(plot, Context(CrisisStage.Reclamation)));
        Assert.Contains("Operation:Retrieval", Ids(plot, Context(CrisisStage.Reclamation, captive: true)));
    }

    [Fact]
    public void Arc_KeepsThePacingGap_AndWaitsForARunningOperation()
    {
        var (book, plot) = CrisisPlot();
        var state = new CrisisState();
        CrisisArc.OperationStarted(book, plot, CrisisOperations.Find(CrisisOperations.ProbingRaid)!, CrisisStage.Probing, 800, state, 100, 4);
        Assert.Empty(Ids(plot, Context(CrisisStage.Probing, state), 120)); // still running

        state.Ended(OperationOutcome.Repelled);
        Assert.Empty(Ids(plot, Context(CrisisStage.Probing, state, gap: 8), 107));
        Assert.Equal(["Operation:ProbingRaid", "Wait"], Ids(plot, Context(CrisisStage.Probing, state, gap: 8), 108));
        Assert.Equal(1, state.OperationsSurvived);
    }

    [Fact]
    public void Arc_EachOperationHasItsOwnCooldown()
    {
        var (book, plot) = CrisisPlot();
        var state = new CrisisState();
        var breach = CrisisOperations.Find(CrisisOperations.Breach)!;
        CrisisArc.OperationStarted(book, plot, breach, CrisisStage.Reclamation, 1800, state, 100, 4);
        state.Ended(OperationOutcome.Repelled);

        var soon = 100 + CrisisArc.DefaultGapDays;
        Assert.DoesNotContain(breach.Id, Ids(plot, Context(CrisisStage.Reclamation, state), soon));
        Assert.Contains("Operation:PsychicAssault", Ids(plot, Context(CrisisStage.Reclamation, state), soon));
        Assert.Contains(breach.Id, Ids(plot, Context(CrisisStage.Reclamation, state), 100 + breach.CooldownDays));
    }

    [Fact]
    public void Arc_WaitsAtMostOnceBetweenOperations()
    {
        var (book, plot) = CrisisPlot();
        PlotBook.Hold(plot, 100, 5, "quiet");
        Assert.DoesNotContain("Wait", Ids(plot, Context(CrisisStage.Probing), 105));

        var state = new CrisisState();
        CrisisArc.OperationStarted(book, plot, CrisisOperations.Find(CrisisOperations.ProbingRaid)!, CrisisStage.Probing, 800, state, 105, 4);
        state.Ended(OperationOutcome.Repelled);
        Assert.Contains("Wait", Ids(plot, Context(CrisisStage.Probing, state), 130));
        Assert.Equal(PlotBeat.Complication, plot.Beat);
        Assert.Equal(105, book.LastBeatDay);
    }

    [Fact]
    public void Arc_TheGapSettingIsClamped()
    {
        var (book, plot) = CrisisPlot();
        var state = new CrisisState();
        CrisisArc.OperationStarted(book, plot, CrisisOperations.Find(CrisisOperations.ProbingRaid)!, CrisisStage.Probing, 800, state, 100, 4);
        state.Ended(OperationOutcome.Repelled);
        Assert.NotEmpty(Ids(plot, Context(CrisisStage.Probing, state, gap: 0), 100 + CrisisArc.MinGapDays));
        Assert.Empty(Ids(plot, Context(CrisisStage.Probing, state, gap: 1000), 100 + CrisisArc.MaxGapDays - 1));
    }

    [Fact]
    public void Arc_EveryOperationHasADescription_AndAUniqueId()
    {
        Assert.All(CrisisOperations.All, o => Assert.False(string.IsNullOrWhiteSpace(o.Desc)));
        Assert.Equal(CrisisOperations.All.Count, CrisisOperations.All.Select(o => o.Id).Distinct().Count());
        Assert.All(CrisisOperations.All, o => Assert.StartsWith(CrisisOperations.Prefix, o.Id));
    }

    [Fact]
    public void Allowed_IgnoringBuilt_ExplainsWhyNoneAreAllowed()
    {
        var state = new CrisisState();
        state.Started(CrisisOperations.ProbingRaid, CrisisStage.Probing, 800, 100);
        Assert.Empty(CrisisArc.Allowed(Context(CrisisStage.Probing, state), 101, ignoreBuilt: true, out var blocked));
        Assert.Contains("under way", blocked);
        Assert.Empty(CrisisArc.Allowed(Context(CrisisStage.None), 101, ignoreBuilt: true, out blocked));
        Assert.Equal("no crisis", blocked);
    }

    // ---- state and saving

    [Fact]
    public void State_AnnouncesEachStageChangeOnce()
    {
        var state = new CrisisState();
        Assert.Null(state.StageChange(CrisisStage.None, 10));
        Assert.Equal(CrisisStage.Probing, state.StageChange(CrisisStage.Probing, 11));
        Assert.Null(state.StageChange(CrisisStage.Probing, 12));
        Assert.Equal(CrisisStage.Reclamation, state.StageChange(CrisisStage.Reclamation, 13));
        Assert.Equal(13, state.AnnouncedDay);
    }

    [Fact]
    public void State_CapabilityHoldsExpire_AndALaterHoldWins()
    {
        var state = new CrisisState();
        state.Hold(CrisisCapability.Strikes, 120);
        state.Hold(CrisisCapability.Strikes, 110);
        Assert.True(state.IsDown(CrisisCapability.Strikes, 115));
        Assert.False(state.IsDown(CrisisCapability.Strikes, 120));
        Assert.False(state.IsDown(CrisisCapability.Psykers, 100));
        state.Hold(CrisisCapability.Strikes, 140);
        Assert.Single(state.Holds);
        Assert.True(state.IsDown(CrisisCapability.Strikes, 130));
    }

    [Fact]
    public void State_HistoryIsCapped()
    {
        var state = new CrisisState();
        for (var i = 0; i < CrisisState.MaxHistory + 10; i++)
        {
            state.Started(CrisisOperations.ProbingRaid, CrisisStage.Probing, 800, i);
            state.Ended(OperationOutcome.Repelled);
        }

        Assert.Equal(CrisisState.MaxHistory, state.History.Count);
        Assert.Equal(CrisisState.MaxHistory + 9, state.LastOperationDay);
    }

    private sealed class Holder : IArchivable
    {
        public CrisisState Crisis = new();

        public void Expose(IArchive archive) => archive.Deep(ref Crisis, "crisis");
    }

    [Fact]
    public void State_RoundTrips()
    {
        var holder = new Holder();
        holder.Crisis.Started(CrisisOperations.Breach, CrisisStage.Reclamation, 1800, 90);
        holder.Crisis.Ended(OperationOutcome.Succeeded);
        holder.Crisis.StageChange(CrisisStage.War, 95);
        holder.Crisis.Hold(CrisisCapability.Psykers, 130);
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);

        var loaded = new Holder { Crisis = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));

        var record = Assert.Single(loaded.Crisis.History);
        Assert.Equal((90, CrisisOperations.Breach, CrisisStage.Reclamation, OperationOutcome.Succeeded, 1800f),
            (record.Day, record.Operation, record.Stage, record.Outcome, record.Budget));
        Assert.Equal(CrisisStage.War, loaded.Crisis.AnnouncedStage);
        Assert.True(loaded.Crisis.IsDown(CrisisCapability.Psykers, 100));
    }

    [Fact]
    public void State_AnOldSaveWithoutItLoadsAsNoCrisis()
    {
        var holder = new Holder { Crisis = null! };
        holder.Expose(new DictionaryArchive(ArchiveMode.Loading));
        Assert.Empty(holder.Crisis.History);
        Assert.Equal(CrisisStage.None, holder.Crisis.AnnouncedStage);
        Assert.Equal(-1, holder.Crisis.LastOperationDay);
        Assert.Null(holder.Crisis.Running);
    }

    // ---- text

    [Fact]
    public void Text_EveryStageChangeHasNews_AndEscalationsHaveALetter()
    {
        var stages = Enum.GetValues<CrisisStage>();
        foreach (var to in stages)
        {
            foreach (var from in stages.Where(s => s != to))
            {
                var news = CrisisText.StageNews(to, from, "the Empire", "Wyrmfold");
                Assert.False(string.IsNullOrWhiteSpace(news), $"{from} -> {to}");
                Assert.Equal(to > from, CrisisText.StageLetter(to, from, "the Empire", "Wyrmfold") is not null);
            }
        }
    }

    // ---- seeded random numbers

    [Fact]
    public void Rng_IsFixedForASeed()
    {
        // SplitMix64's published first outputs for seed 0; if these change, every seeded site changes.
        var rng = new SeededRng(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, rng.NextULong());
        Assert.Equal(0x6E789E6AA1B965F4UL, rng.NextULong());
        Assert.Equal(0x06C45D188009454FUL, rng.NextULong());
    }

    [Fact]
    public void Rng_SeedsDependOnIdAndSalt_AndRangesStayInBounds()
    {
        Assert.Equal(SeededRng.Seed(42, "RimChronicles_Site_Relay"), SeededRng.Seed(42, "RimChronicles_Site_Relay"));
        Assert.NotEqual(SeededRng.Seed(42, "RimChronicles_Site_Relay"), SeededRng.Seed(43, "RimChronicles_Site_Relay"));
        Assert.NotEqual(SeededRng.Seed(42, "a"), SeededRng.Seed(42, "b"));

        var rng = new SeededRng(SeededRng.Seed(7));
        for (var i = 0; i < 2000; i++)
        {
            var d = rng.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999);
            Assert.InRange(rng.Next(5), 0, 4);
            Assert.InRange(rng.Range(-3, 3), -3, 3);
        }

        Assert.Equal(4, rng.Range(4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Next(0));
    }
}
