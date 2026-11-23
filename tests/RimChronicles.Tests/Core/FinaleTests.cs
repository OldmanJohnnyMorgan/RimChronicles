using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class FinaleTests
{
    private static readonly HashSet<string> AllBuilt = CrisisOperations.All.Select(o => o.Step).ToHashSet();

    private static CrisisContext Context(CrisisStage stage) => new() { Stage = stage, State = new CrisisState(), Built = AllBuilt, GapDays = 8 };

    // ---- the done-when: the final assault fires at stage 4 only

    [Theory]
    [InlineData(CrisisStage.Probing)]
    [InlineData(CrisisStage.Reclamation)]
    [InlineData(CrisisStage.War)]
    public void FinalAssault_NeverBeforeTheFinale(CrisisStage stage) =>
        Assert.DoesNotContain(CrisisArc.Allowed(Context(stage), 100, ignoreBuilt: true, out _), o => o.Step == CrisisOperations.FinalAssault);

    [Fact]
    public void FinalAssault_InTheFinale_AndItFightsToTheEnd()
    {
        Assert.Contains(CrisisArc.Allowed(Context(CrisisStage.Finale), 100, ignoreBuilt: false, out _), o => o.Step == CrisisOperations.FinalAssault);
        var profile = OperationProfiles.For(CrisisOperations.FinalAssault);
        Assert.False(profile.RetreatsOnLosses());
        Assert.DoesNotContain(profile.Phases.SelectMany(p => p.Links), l => l.Trigger == PhaseTrigger.LossesOver);
        Assert.Contains(profile.Phases.SelectMany(p => p.Links), l => l.Trigger == PhaseTrigger.Abandoned); // it still pulls out if the crisis ends
    }

    [Fact]
    public void FinalAssault_EveryArmOfTheEmpire_TheStellarchOnTop()
    {
        var force = ForceMix.Build(ForceMix.For(CrisisOperations.FinalAssault), CrisisStage.Finale, 6000, e => 160);
        Assert.Contains(force, e => e.Role == UnitRole.Praetorian);
        Assert.Contains(force, e => e.Role == UnitRole.Breacher);
        Assert.Contains(force, e => e.Role == UnitRole.Herald);
        Assert.Equal(4, force.Where(e => e.Role == UnitRole.Adept).Select(e => e.Specialty).Distinct().Count());
        Assert.DoesNotContain(ForceMix.FinalAssault, e => e.Role == UnitRole.Stellarch); // there's one, added by the game
    }

    // ---- the done-when: the Stellarch escapes at most once

    [Fact]
    public void Stellarch_EscapesOnce_WhenBadlyHurtOrDowned()
    {
        var state = new CrisisState { StellarchFate = StellarchFate.Alive };
        Assert.False(StellarchRules.ShouldEscape(state, 0.8f, downed: false, dead: false));
        Assert.True(StellarchRules.ShouldEscape(state, 0.3f, downed: false, dead: false));
        Assert.True(StellarchRules.ShouldEscape(state, 0.9f, downed: true, dead: false));
        Assert.False(StellarchRules.ShouldEscape(state, 0.1f, downed: true, dead: true));

        StellarchRules.Escaped(state);
        Assert.False(StellarchRules.ShouldEscape(state, 0.1f, downed: true, dead: false));
        Assert.False(StellarchRules.ShouldEscape(state, 0.01f, downed: false, dead: false));
    }

    [Fact]
    public void Stellarch_FallsOnce_AndNeverLeadsAgain()
    {
        var state = new CrisisState { StellarchFate = StellarchFate.Alive };
        Assert.True(StellarchRules.CanLead(state));
        Assert.False(StellarchRules.Fell(state, StellarchFate.Alive, 10)); // not a fall
        Assert.True(StellarchRules.Fell(state, StellarchFate.Captured, 12));
        Assert.False(StellarchRules.Fell(state, StellarchFate.Dead, 13)); // a captive killed later isn't a second fall
        Assert.Equal((StellarchFate.Captured, 12), (state.StellarchFate, state.StellarchFellDay));
        Assert.False(StellarchRules.CanLead(state));
        Assert.False(StellarchRules.ShouldEscape(state, 0.1f, downed: false, dead: false));
    }

    private sealed class Holder : IArchivable
    {
        public CrisisState Crisis = new();

        public void Expose(IArchive archive) => archive.Deep(ref Crisis, "crisis");
    }

    [Fact]
    public void TheStellarch_IsSaved_AndAnOldSaveHasNone()
    {
        var holder = new Holder();
        holder.Crisis.StellarchPawnId = "Human123";
        holder.Crisis.StellarchFate = StellarchFate.Alive;
        StellarchRules.Escaped(holder.Crisis);
        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);
        var loaded = new Holder { Crisis = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        Assert.Equal(("Human123", StellarchFate.Alive, true), (loaded.Crisis.StellarchPawnId, loaded.Crisis.StellarchFate, loaded.Crisis.StellarchEscaped));

        var old = new Holder { Crisis = null! };
        old.Expose(new DictionaryArchive(ArchiveMode.Loading, new Dictionary<string, object?>()));
        Assert.Equal(("", StellarchFate.None, false, -1), (old.Crisis.StellarchPawnId, old.Crisis.StellarchFate, old.Crisis.StellarchEscaped, old.Crisis.StellarchFellDay));
        Assert.True(StellarchRules.CanLead(old.Crisis));
    }
}
