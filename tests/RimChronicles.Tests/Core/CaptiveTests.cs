using System.Globalization;
using System.Xml.Linq;
using RimChronicles.Core.Crisis;
using RimChronicles.Core.Crisis.Captives;
using RimChronicles.Core.Crisis.Operations;
using RimChronicles.Core.Crisis.Units;
using RimChronicles.Core.Persistence;
using RimChronicles.Tests.TestSupport;

namespace RimChronicles.Tests.Core;

public class CaptiveTests
{
    private static readonly IReadOnlyList<IntelSpec> Intel = LoadIntel();

    /// <summary>The repo's intel defs, as <c>IntelDef.ToCore</c> reads them.</summary>
    private static List<IntelSpec> LoadIntel() =>
        XDocument.Load(Repo.Path("Defs", "Captives", "RimChronicles_Captives.xml")).Root!.Elements()
            .Where(e => e.Name.LocalName.EndsWith(".IntelDef"))
            .Select(e => new IntelSpec(
                e.Element("defName")!.Value,
                e.Element("ranks")!.Elements("li").Select(li => Enum.Parse<CaptiveRank>(li.Value)).ToList(),
                Enum.Parse<IntelEffect>(e.Element("effect")!.Value),
                int.TryParse(e.Element("expiryDays")?.Value, out var d) ? d : 0,
                double.TryParse(e.Element("weight")?.Value, CultureInfo.InvariantCulture, out var w) ? w : 1) { Label = e.Element("label")!.Value })
            .ToList();

    private static IntelSpec Of(IntelEffect effect) => Intel.First(i => i.Effect == effect);

    private static readonly HashSet<string> AllBuilt = CrisisOperations.All.Select(o => o.Step).ToHashSet();

    private static CrisisContext Context(CrisisState state, CrisisStage stage = CrisisStage.War, bool captive = false) =>
        new() { Stage = stage, State = state, Built = AllBuilt, HoldsCaptive = captive, GapDays = 8 };

    // ---- the defs

    [Fact]
    public void IntelDefs_EveryRankHasSomethingToTell_AndEveryEffectIsUsed()
    {
        foreach (var rank in Enum.GetValues<CaptiveRank>().Where(r => r != CaptiveRank.None))
        {
            Assert.Contains(Intel, i => i.Ranks.Contains(rank));
        }

        Assert.Equal(Enum.GetValues<IntelEffect>().ToHashSet(), Intel.Select(i => i.Effect).ToHashSet());
        Assert.Equal(Intel.Count, Intel.Select(i => i.Id).Distinct().Count());
    }

    /// <summary>The unit pawn kinds carry the rank's resistance and will (vanilla applies them at capture).</summary>
    [Fact]
    public void PawnKinds_CarryTheirRanksResistanceAndWill()
    {
        var kinds = XDocument.Load(Repo.Path("Defs", "PawnKindDefs", "RimChronicles_Units.xml")).Root!.Elements("PawnKindDef").ToList();
        XElement? Find(string name) => kinds.FirstOrDefault(k => k.Element("defName")?.Value == name || k.Attribute("Name")?.Value == name);
        string? Get(XElement kind, string field)
        {
            for (XElement? k = kind; k is not null; k = k.Attribute("ParentName") is { } p ? Find(p.Value) : null)
            {
                if (k.Element(field) is { } e)
                {
                    return e.Value;
                }
            }

            return null;
        }

        (float, float) Range(string text)
        {
            var parts = text.Split('~').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            return (parts[0], parts[1]);
        }

        var checkedKinds = 0;
        foreach (var kind in kinds.Where(k => k.Element("defName") is not null))
        {
            var role = Enum.Parse<UnitRole>(Get(kind, "modExtensions") is null ? "Praetorian"
                : kind.Descendants("role").FirstOrDefault()?.Value ?? Find(kind.Attribute("ParentName")!.Value)!.Descendants("role").First().Value);
            var rank = CaptiveRules.RankOf(role);
            Assert.Equal(CaptiveRules.Resistance(rank), Range(Get(kind, "initialResistanceRange")!));
            Assert.Equal(CaptiveRules.Will(rank), Range(Get(kind, "initialWillRange")!));
            checkedKinds++;
        }

        Assert.True(checkedKinds >= 8);
    }

    [Fact]
    public void Ranks_HigherRanksResistLongerAndKnowMore()
    {
        Assert.True(CaptiveRules.Resistance(CaptiveRank.Officer).Min > CaptiveRules.Resistance(CaptiveRank.Psyker).Max);
        Assert.True(CaptiveRules.Resistance(CaptiveRank.Psyker).Min > CaptiveRules.Resistance(CaptiveRank.Soldier).Min);
        Assert.True(CaptiveRules.Resistance(CaptiveRank.Soldier).Min > 25); // well above vanilla raiders
        Assert.True(CaptiveRules.MaxIntel(CaptiveRank.Officer) > CaptiveRules.MaxIntel(CaptiveRank.Herald));
        Assert.True(CaptiveRules.MaxIntel(CaptiveRank.Herald) > CaptiveRules.MaxIntel(CaptiveRank.Soldier));
        Assert.True(CaptiveRules.Chance(1, CaptiveRank.Officer) < CaptiveRules.Chance(1, CaptiveRank.Soldier));
        Assert.InRange(CaptiveRules.Chance(0, CaptiveRank.Soldier), 0.05f, 0.05f);
        Assert.InRange(CaptiveRules.Chance(9, CaptiveRank.Soldier), 0.8f, 0.8f);
        Assert.Equal(CaptiveRank.Psyker, CaptiveRules.RankOf(UnitRole.Adept));
        Assert.Equal(CaptiveRank.Officer, CaptiveRules.RankOf(UnitRole.Stellarch));
    }

    // ---- interrogation

    [Fact]
    public void Session_ASuccessGivesFreshIntel_UntilTheyveToldAllTheyKnow()
    {
        var record = new CaptiveRecord { PawnId = "p1", Rank = CaptiveRank.Herald };
        var first = CaptiveRules.Session(CaptiveRank.Herald, record, Intel, 2f, roll: 0, pick: 0);
        Assert.True(first.Success);
        Assert.Contains(CaptiveRank.Herald, first.Intel!.Ranks);
        var second = CaptiveRules.Session(CaptiveRank.Herald, record, Intel, 2f, roll: 0, pick: 0);
        Assert.NotEqual(first.Intel.Id, second.Intel!.Id); // a different item while fresh ones remain
        Assert.Equal(2, record.Interrogations);

        var done = CaptiveRules.Session(CaptiveRank.Herald, record, Intel, 2f, roll: 0, pick: 0);
        Assert.False(done.Success);
        Assert.Equal("has told all they know", done.NothingToTell);
        Assert.Equal(2, record.Interrogations); // a session with nothing to tell isn't a session
    }

    [Fact]
    public void Session_AFailureStillWearsThemDown_LessThanASuccess()
    {
        var record = new CaptiveRecord { Rank = CaptiveRank.Soldier };
        var miss = CaptiveRules.Session(CaptiveRank.Soldier, record, Intel, 1f, roll: 0.99, pick: 0);
        Assert.False(miss.Success);
        Assert.Null(miss.NothingToTell);
        Assert.True(miss.ResistanceLoss > 0 && miss.WillLoss > 0);
        var hit = CaptiveRules.Session(CaptiveRank.Soldier, record, Intel, 1f, roll: 0, pick: 0);
        Assert.True(hit.ResistanceLoss > miss.ResistanceLoss);
        Assert.Equal(2, record.Interrogations);
    }

    [Fact]
    public void Session_ANonImperialKnowsNothing()
    {
        Assert.Equal("knows nothing about the Empire", CaptiveRules.NothingToTell(CaptiveRank.None, null));
        Assert.Null(CaptiveRules.NothingToTell(CaptiveRank.Soldier, null));
        var result = CaptiveRules.Session(CaptiveRank.None, new CaptiveRecord(), Intel, 5f, 0, 0);
        Assert.False(result.Success);
        Assert.Equal(0, result.ResistanceLoss);
    }

    // ---- intel effects

    [Fact]
    public void NextOperation_WarnsOfOne_AndTheCrisisHoldsToIt_UntilItFiresOrGoesStale()
    {
        var state = new CrisisState();
        state.Started(CrisisOperations.ProbingRaid, CrisisStage.War, 1000, 10);
        state.Ended(OperationOutcome.Repelled);
        var outcome = IntelEffects.Apply(Of(IntelEffect.NextOperation), state, Context(state), 12, roll: 0.5);
        var forecast = state.Forecast;
        Assert.True(forecast.Valid(12));
        Assert.Equal(18, forecast.EarliestDay); // the last operation's day plus the gap
        Assert.Contains($"day {forecast.EarliestDay}", outcome.Text);

        Assert.Empty(CrisisArc.Allowed(Context(state), 17, ignoreBuilt: false, out var why));
        Assert.NotNull(why);
        Assert.Equal([forecast.Operation], CrisisArc.Allowed(Context(state), 18, ignoreBuilt: false, out _).Select(o => o.Step));
        Assert.True(CrisisArc.Allowed(Context(state), forecast.ExpiresDay + 1, ignoreBuilt: false, out _).Count > 1); // stale: anything goes

        state.Started(forecast.Operation, CrisisStage.War, 1000, 18);
        Assert.False(state.Forecast.Valid(18));
    }

    [Fact]
    public void NextOperation_NothingWhenNothingWouldBeAllowed()
    {
        var state = new CrisisState();
        var outcome = IntelEffects.Apply(Of(IntelEffect.NextOperation), state, Context(state, CrisisStage.None), 5, 0.5);
        Assert.False(state.Forecast.Valid(5));
        Assert.Contains("no operation", outcome.Text);
    }

    [Fact]
    public void UplinkCodes_JamTheNextStrikes_UpToTwo()
    {
        var state = new CrisisState();
        foreach (var _ in Enumerable.Range(0, 3))
        {
            IntelEffects.Apply(Of(IntelEffect.UplinkCodes), state, Context(state), 5, 0);
        }

        Assert.Equal(IntelEffects.MaxStrikeJams, state.StrikeJams);
        Assert.True(state.TryUseJam());
        Assert.True(state.TryUseJam());
        Assert.False(state.TryUseJam());
    }

    [Fact]
    public void RelayLocation_RevealsARelay_AndTheNextPsychicAssaultIsShortOfAdepts()
    {
        var state = new CrisisState();
        var outcome = IntelEffects.Apply(Of(IntelEffect.RelayLocation), state, Context(state), 5, 0);
        Assert.Equal("PsychicRelay", outcome.RevealSite);
        Assert.True(state.AdeptShortfall);

        float Cost(MixEntry e) => 150;
        int Adepts(IReadOnlyList<MixEntry> mix) => ForceMix.Build(mix, CrisisStage.War, 3000, Cost).Count(e => e.Role == UnitRole.Adept);
        Assert.True(Adepts(ForceMix.ShortOfAdepts(ForceMix.PsychicAssault)) < Adepts(ForceMix.PsychicAssault));
    }

    [Fact]
    public void CitadelPlans_AndGarrison()
    {
        var state = new CrisisState();
        IntelEffects.Apply(Of(IntelEffect.CitadelPlans), state, Context(state), 5, 0);
        Assert.True(state.KeepEntranceKnown);
        Assert.True(IntelEffects.Apply(Of(IntelEffect.Garrison), state, Context(state), 5, 0).RevealGarrison);
    }

    [Fact]
    public void TheLog_KeepsEveryItem_WithItsExpiry_AndIsCapped()
    {
        var state = new CrisisState();
        IntelEffects.Apply(Of(IntelEffect.UplinkCodes), state, Context(state), 10, 0);
        var item = state.Intel.Single();
        Assert.Equal(10 + Of(IntelEffect.UplinkCodes).ExpiryDays, item.ExpiresDay);
        Assert.Single(state.LiveIntel(item.ExpiresDay));
        Assert.Empty(state.LiveIntel(item.ExpiresDay + 1));
        IntelEffects.Apply(Of(IntelEffect.CitadelPlans), state, Context(state), 10, 0);
        Assert.Equal(-1, state.Intel[^1].ExpiresDay);

        foreach (var day in Enumerable.Range(0, 60))
        {
            IntelEffects.Apply(Of(IntelEffect.Garrison), state, Context(state), day, 0);
        }

        Assert.Equal(CrisisState.MaxIntel, state.Intel.Count);
    }

    // ---- defectors, release, treatment

    [Fact]
    public void Defectors_OnlyCaptivesRecruitedAfterInterrogation_AndOnce()
    {
        var state = new CrisisState();
        state.Captured("a", "Ana", CaptiveRank.Psyker, 3);
        Assert.Null(state.Recruited("a", 5)); // never questioned: just a recruit
        state.Captive("a")!.Interrogations = 2;
        var defector = state.Recruited("a", 6);
        Assert.Equal((CaptiveRank.Psyker, "Ana", 6), (defector!.Rank, defector.Name, defector.Day));
        Assert.Null(state.Recruited("a", 7));
        Assert.True(state.IsDefector("a"));
        Assert.Null(state.Recruited("nobody", 7));
        Assert.Same(state.Captive("a"), state.Captured("a", "Ana", CaptiveRank.Psyker, 9)); // captured once
    }

    [Fact]
    public void Release_MakesTheNextOperationCheaper_Once()
    {
        var state = new CrisisState();
        state.Release();
        state.Release();
        Assert.Equal(2, state.Released);
        Assert.Equal(CaptiveRules.ReleaseBudgetFactor, state.NextBudgetFactor);
        state.Started(CrisisOperations.Breach, CrisisStage.War, 1000, 20);
        Assert.Equal(1f, state.NextBudgetFactor);
    }

    // ---- retrieval

    [Fact]
    public void Retrieval_GoesForTheHeldPawn_AndCanAlwaysLeave()
    {
        var profile = OperationProfiles.For(CrisisOperations.Retrieval);
        Assert.Same(OperationProfiles.Retrieval, profile);
        Assert.Equal(PhaseKind.Retrieve, profile.Find(profile.Start)!.Kind);
        Assert.True(profile.RetreatsOnLosses());
        var links = profile.Find(profile.Start)!.Links;
        Assert.Equal(OperationOutcome.Succeeded, links.Single(l => l.Trigger == PhaseTrigger.TargetDown).Outcome);
        Assert.Equal(OperationOutcome.Repelled, links.Single(l => l.Trigger == PhaseTrigger.TimeUp).Outcome); // gave up
        var force = ForceMix.Build(ForceMix.For(CrisisOperations.Retrieval), CrisisStage.Reclamation, 2500, e => 150);
        Assert.Contains(force, e => e.Specialty == Specialty.Skip);
        Assert.Contains(force, e => e.Role == UnitRole.Breacher);
    }

    // ---- saving

    private sealed class Holder : IArchivable
    {
        public CrisisState Crisis = new();

        public void Expose(IArchive archive) => archive.Deep(ref Crisis, "crisis");
    }

    [Fact]
    public void TheNewState_RoundTrips_AndAnOldSaveLoadsEmpty()
    {
        var holder = new Holder();
        var c = holder.Crisis;
        c.Captured("a", "Ana", CaptiveRank.Herald, 3).Told.Add("RimChronicles_Intel_UplinkCodes");
        c.Captive("a")!.Interrogations = 1;
        c.Recruited("a", 4);
        IntelEffects.Apply(Of(IntelEffect.UplinkCodes), c, Context(c), 5, 0);
        IntelEffects.Apply(Of(IntelEffect.NextOperation), c, Context(c), 5, 0.3);
        c.Release();
        c.Executed = 2;
        c.AdeptShortfall = true;

        var saving = new DictionaryArchive(ArchiveMode.Saving);
        holder.Expose(saving);
        var loaded = new Holder { Crisis = null! };
        loaded.Expose(new DictionaryArchive(ArchiveMode.Loading, saving.Data));
        var l = loaded.Crisis;
        Assert.Equal(["RimChronicles_Intel_UplinkCodes"], l.Captive("a")!.Told);
        Assert.True(l.IsDefector("a"));
        Assert.Equal(c.Intel.Count, l.Intel.Count);
        Assert.Equal((c.Forecast.Operation, c.Forecast.EarliestDay), (l.Forecast.Operation, l.Forecast.EarliestDay));
        Assert.Equal((1, 1, 2, true, CaptiveRules.ReleaseBudgetFactor), (l.StrikeJams, l.Released, l.Executed, l.AdeptShortfall, l.NextBudgetFactor));

        var old = new Holder { Crisis = null! };
        old.Expose(new DictionaryArchive(ArchiveMode.Loading, new Dictionary<string, object?>()));
        Assert.Empty(old.Crisis.Captives);
        Assert.False(old.Crisis.Forecast.Valid(0));
        Assert.Equal(1f, old.Crisis.NextBudgetFactor);
    }
}
