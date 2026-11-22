using RimChronicles.Core.Empire;
using RimChronicles.Core.Plots;
using RimChronicles.Core.Plots.LostFamily;
using RimChronicles.Core.Plots.Nemesis;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>A generated case and what it covers, for the coverage table.</summary>
public sealed record DirectorCaseResult(WorldSnapshot Snapshot, string Arc, string Scenario, string Mood, string Situation);

/// <summary>
/// The director case generator (training track D2): synthetic Stage 0 snapshots, each with one plot whose beat is due
/// and the options the real arc code offers in that state (<see cref="NemesisArc"/>, <see cref="LostFamilyArc"/>,
/// <see cref="EmpireArc"/>). Scenarios and colony moods are spread evenly by index; everything else is drawn from a
/// <see cref="Random"/> seeded by (seed, index), so a case is reproducible on its own.
/// </summary>
public static class CaseGenerator
{
    /// <summary>Every scenario with a real choice. Silent Press never offers one, and endings are single options.</summary>
    public static readonly string[] Scenarios =
    [
        "Nemesis:AfterSighting", "Nemesis:SeenTwice", "Nemesis:AfterSighting+Empire", "Nemesis:AfterRaid", "Nemesis:AfterRaid+Empire",
        "LostFamily:AfterLetter", "LostFamily:AfterTwoLetters", "LostFamily:Taken",
        "Empire:Act2:ally", "Empire:Act2:oppose", "Empire:Act2:neutral",
        "Empire:Act3:ally", "Empire:Act3:ally+title", "Empire:Act3:oppose:ascendant", "Empire:Act3:oppose:broken",
        "Empire:Act3:neutral:ascendant", "Empire:Act3:neutral:broken",
    ];

    public static readonly string[] Moods = ["content", "strained", "desperate"];

    /// <summary>What has just happened in the colony, which decides whether waiting is right.</summary>
    public static readonly string[] Situations = ["calm", "recent raid", "recent death", "long stall"];

    /// <summary>Days in the beat at or above which a calm colony should move on rather than wait.</summary>
    public const int StallDays = 25;

    public static DirectorCaseResult Generate(int seed, int index)
    {
        var rng = new Random(unchecked(seed * 1_000_003 + index));
        var scenario = Scenarios[index % Scenarios.Length];
        var mood = Moods[index / Scenarios.Length % Moods.Length];
        var situation = Situations[index / (Scenarios.Length * Moods.Length) % Situations.Length];
        return new Builder(rng, seed, index, scenario, mood, situation).Build();
    }

    /// <summary>The expected answer about waiting, by construction: wait after a fresh blow or in a desperate colony
    /// (unless the beat has stalled), move on when a beat has stalled in a colony that isn't reeling; open otherwise. Only meaningful
    /// when Wait is offered.</summary>
    public static bool? WaitExpected(bool waitOffered, string mood, string situation, int daysInBeat)
    {
        if (!waitOffered)
        {
            return null;
        }

        var blow = situation is "recent raid" or "recent death";
        if ((blow || mood == "desperate") && daysInBeat < StallDays)
        {
            return true;
        }

        if (!blow && mood != "desperate" && daysInBeat >= StallDays)
        {
            return false;
        }

        return null;
    }

    private sealed class Builder(Random rng, int seed, int index, string scenario, string mood, string situation)
    {
        private readonly List<PawnInfo> _pawns = [];
        private readonly List<TaleInfo> _tales = [];
        private readonly List<FactionInfo> _factions = [];
        private readonly List<string> _events = [];
        private readonly List<string> _public = [];
        private readonly HashSet<string> _usedNames = [];
        private readonly PlotBook _book = new();
        private int _day;
        private string _colony = "";

        public DirectorCaseResult Build()
        {
            _day = rng.Next(220, 900); // room for each arc's back-story before today
            _colony = CasePools.Pick(rng, CasePools.Colonies);
            var population = rng.Next(3, 11);
            for (var i = 0; i < Math.Min(population, rng.Next(3, 7)); i++)
            {
                AddPawn();
            }

            AddFaction("Tribe", rng.Next(-20, 60));
            AddFaction("Outlanders", rng.Next(-10, 70));

            var arc = scenario.Split(':')[0];
            var daysInBeat = situation == "long stall" ? rng.Next(StallDays, 45) : rng.Next(1, StallDays);
            var (plot, transitions) = arc switch
            {
                "Nemesis" => Nemesis(daysInBeat),
                "LostFamily" => LostFamily(daysInBeat),
                _ => Empire(daysInBeat),
            };
            _waited = rng.NextDouble() < 0.2;
            var options = _waited ? transitions() : [];
            if (!_waited || options.Count < 2)
            {
                // Without a choice the model is never asked: only cases where waiting is already spent but a real
                // choice remains drop the Wait option.
                _lastPlot!.SetFact(PlotBook.WaitedOnBeat, "");
                options = transitions();
            }

            if (options.Count < 2)
            {
                throw new InvalidOperationException($"{scenario} gave {options.Count} option(s); every scenario must offer a choice.");
            }

            var others = OtherPlots(arc);
            AddSituation();
            AddBackground();

            var waitOffered = options.Any(o => o.Id == Director.WaitId);
            var snapshot = new WorldSnapshot
            {
                Id = $"dir-s{seed}-{index:D5}",
                Day = _day,
                Season = CasePools.Seasons[_day / 15 % 4],
                Year = 5500 + _day / 60,
                Colony = new ColonyInfo
                {
                    Name = _colony,
                    Wealth = mood switch { "content" => rng.Next(40, 200), "strained" => rng.Next(20, 120), _ => rng.Next(8, 60) } * 1000,
                    Population = Math.Max(population, _pawns.Count(p => p.Status == "alive")),
                    Mood = mood,
                },
                Pawns = _pawns,
                Tales = _tales.OrderBy(t => t.Day).ToList(),
                Factions = _factions,
                ActivePlots = [plot, .. others],
                RecentEvents = _events,
                PublicEvents = _public,
                AllowedTransitions = options.Select(o => o.Id).ToList(),
                Director = new DirectorCase
                {
                    PlotId = plot.Id,
                    DaysInBeat = daysInBeat,
                    Descriptions = options.ToDictionary(o => o.Id, o => o.Desc),
                    WaitExpected = WaitExpected(waitOffered, mood, situation, daysInBeat),
                    Seed = seed,
                },
            };
            return new DirectorCaseResult(snapshot, arc, scenario, mood, situation);
        }

        // ---------- arcs ----------

        private (PlotInfo, Func<IReadOnlyList<PlotTransition>>) Nemesis(int daysInBeat)
        {
            var empireOpen = scenario.EndsWith("+Empire", StringComparison.Ordinal);
            var empire = empireOpen ? AddEmpire("Awakened", -100) : null;
            var faction = AddFaction("Pirates", rng.Next(-100, -60));
            var raider = Nickname();
            var protagonist = AddPawn();
            var maimed = rng.NextDouble() < 0.3;
            var relation = CasePools.Pick(rng, CasePools.Relations);
            var victim = maimed ? protagonist : AddPawn(status: "dead", relation: $"{relation} of {protagonist.Id}");
            var grudgeDay = _day - rng.Next(20, 120);
            _tales.Add(new TaleInfo { Day = grudgeDay, Type = maimed ? "Maimed" : "KilledColonist", Actor = raider, Victim = victim.Id, Escaped = true });

            var plot = Start(NemesisPlot.TemplateId, protagonist, grudgeDay + 2);
            plot.SetFact(NemesisPlot.NemesisName, raider);
            plot.SetFact(NemesisPlot.NemesisFaction, faction);
            plot.SetFact(NemesisPlot.VictimName, victim.Name);
            plot.SetFact(NemesisPlot.Relation, maimed ? "" : relation);
            plot.SetFact(Promotion.EmpireOpen, empireOpen ? "yes" : "no");
            plot.SetFact(Promotion.EmpireName, empire ?? "");

            var place = CasePools.Pick(rng, CasePools.Places);
            var deed = maimed ? $"maimed {protagonist.Name}" : $"killed {protagonist.Name}'s {relation} {victim.Name}";
            var summary = $"{raider} of {faction} {deed} on day {grudgeDay} and escaped.";
            if (scenario.Contains("AfterRaid", StringComparison.Ordinal))
            {
                Advance(plot, PlotBeat.Hook, daysInBeat + rng.Next(5, 20));
                Advance(plot, PlotBeat.Complication, daysInBeat);
                summary += $" {raider} was seen near {place}, then led a raid on {_colony} on day {_day - daysInBeat} and got away.";
                _tales.Add(new TaleInfo { Day = _day - daysInBeat, Type = "RaidRepelled", Actor = "colony", Victim = $"{raider}'s raiders" });
            }
            else
            {
                Advance(plot, PlotBeat.Hook, daysInBeat);
                var twice = scenario == "Nemesis:SeenTwice";
                plot.SetFact(NemesisArc.Sightings, twice ? "2" : "1");
                summary += twice ? $" {raider} has been seen twice near {place}." : $" {raider} was seen near {place} on day {_day - daysInBeat}.";
            }

            MaybeWaited(plot);
            return (Info(plot, protagonist.Id, summary), () => NemesisArc.Transitions(plot));
        }

        private (PlotInfo, Func<IReadOnlyList<PlotTransition>>) LostFamily(int daysInBeat)
        {
            var protagonist = AddPawn();
            var kinFaction = AddFaction(rng.NextDouble() < 0.5 ? "Tribe" : "Outlanders", rng.Next(0, 60));
            var kin = CasePools.Name(rng);
            var relation = CasePools.Pick(rng, CasePools.Relations.Where(r => r != "friend").ToList());
            var plot = Start(LostFamilyArc.TemplateId, protagonist, _day - daysInBeat - rng.Next(5, 30));
            plot.SetFact(LostFamilyArc.KinName, kin);
            plot.SetFact(LostFamilyArc.Relation, relation);
            plot.SetFact(LostFamilyArc.KinFaction, kinFaction);
            var summary = $"{protagonist.Name}'s {relation} {kin} is alive among {kinFaction}.";
            if (scenario == "LostFamily:Taken")
            {
                var captor = AddFaction("Pirates", rng.Next(-100, -60));
                plot.SetFact(LostFamilyArc.Letters, "1");
                plot.SetFact(LostFamilyArc.Captor, captor);
                Advance(plot, PlotBeat.Hook, daysInBeat + rng.Next(5, 15));
                Advance(plot, PlotBeat.Complication, daysInBeat);
                summary += $" On day {_day - daysInBeat}, {captor} took {kin} captive; {kin} is held at a camp nearby.";
                _public.Add($"{captor} took captives from {kinFaction}");
            }
            else
            {
                var letters = scenario == "LostFamily:AfterTwoLetters" ? 2 : 1;
                plot.SetFact(LostFamilyArc.Letters, letters.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Advance(plot, PlotBeat.Hook, daysInBeat);
                summary += letters == 1 ? $" A letter from {kin} reached {protagonist.Name} on day {_day - daysInBeat}." : $" {protagonist.Name} has had two letters from {kin}.";
            }

            MaybeWaited(plot);
            return (Info(plot, protagonist.Id, summary), () => LostFamilyArc.Transitions(plot));
        }

        private (PlotInfo, Func<IReadOnlyList<PlotTransition>>) Empire(int daysInBeat)
        {
            var parts = scenario.Split(':'); // Empire, Act2|Act3, stance(+title), [ascendant|broken]
            var act3 = parts[1] == "Act3";
            var stance = parts[2].Split('+')[0];
            var titled = parts[2].EndsWith("+title", StringComparison.Ordinal);
            var ascendant = parts.Length < 4 || parts[3] == "ascendant";
            var empireName = AddEmpire(act3 ? (ascendant ? "Ascendant" : "Broken") : "Awakened",
                stance switch { EmpireStance.Ally => rng.Next(25, 80), EmpireStance.Oppose => -100, _ => rng.Next(-30, 25) });

            var saga = new EmpireSaga();
            var wokeDay = _day - daysInBeat - rng.Next(30, 120);
            saga.Awaken(AwakeningTrigger.Wealth, "150000", wokeDay, 25, _colony);
            saga.Wake(wokeDay + 25, empireName);
            var conquests = new List<string>();
            var expansions = act3 ? rng.Next(3, 7) : rng.Next(0, EmpireArc.ExpansionsForReckoning);
            var settlements = CasePools.Settlements.OrderBy(_ => rng.Next()).ToList(); // each settlement attacked at most once
            for (var i = 0; i < expansions; i++)
            {
                var settlement = settlements[i];
                var owner = CasePools.Pick(rng, _factions.Where(f => f.Kind != "Empire").Select(f => f.Name).ToList());
                var result = (ExpansionResult)rng.Next(3);
                saga.Expanded(new ExpansionTarget(settlement, settlement, owner, 10, 3), result, wokeDay + 30 + i * 15, 15, empireName);
                conquests.Add(EmpireExpansion.News(result, empireName, new ExpansionTarget(settlement, settlement, owner, 10, 3)));
            }

            if (act3)
            {
                saga.Reckon(ascendant, _day - daysInBeat);
            }

            saga.HighTitleHolder = "";
            var plot = Start(EmpireArc.TemplateId, null, wokeDay);
            plot.SetFact(EmpireArc.Stance, stance);
            var wins = act3 ? 0 : rng.Next(0, 2);
            plot.SetFact(EmpireArc.Wins, wins.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Advance(plot, PlotBeat.Hook, daysInBeat + 40);
            Advance(plot, PlotBeat.Complication, act3 ? daysInBeat + 20 : daysInBeat);
            if (act3)
            {
                Advance(plot, PlotBeat.Choice, daysInBeat);
            }

            string? holder = null;
            if (titled)
            {
                var pawn = AddPawn(note: $"holds the Imperial title of {CasePools.Pick(rng, new[] { "Knight", "Praetor", "Baron" })}");
                saga.HighTitleHolder = pawn.Name;
                holder = pawn.Name;
            }

            MaybeWaited(plot);
            var stanceText = stance switch
            {
                EmpireStance.Ally => $"{_colony} stands with {empireName}",
                EmpireStance.Oppose => $"{_colony} is at war with {empireName}",
                _ => $"{_colony} has kept out of the Empire's way",
            };
            var sentences = new List<string> { $"{empireName} woke on day {wokeDay + 25}", stanceText };
            sentences.AddRange(conquests.Take(3));
            if (act3)
            {
                sentences.Add(ascendant ? $"{empireName} now holds most of the region" : $"{empireName} is breaking after heavy losses");
            }

            if (holder is not null)
            {
                sentences.Add($"{holder} has risen in the Empire's service");
            }

            var summary = string.Join(" ", sentences.Select(x => char.ToUpperInvariant(x[0]) + x[1..] + "."));
            _public.AddRange(conquests.Take(2));
            return (Info(plot, Inheritance.ColonyId, summary, act3 ? (ascendant ? "Ascendant" : "Broken") : "Act2"),
                () => EmpireArc.Transitions(plot, saga, _day));
        }

        /// <summary>One or two other running plots, for realism: the decision is about one plot among several.</summary>
        private List<PlotInfo> OtherPlots(string arc)
        {
            var others = new List<PlotInfo>();
            var count = rng.Next(0, 3);
            for (var i = 0; i < count; i++)
            {
                var kind = CasePools.Pick(rng, new[] { "Nemesis", "LostFamily", "SilentPress" }.Where(k => k != arc).ToList());
                var pawn = AddPawn();
                others.Add(kind switch
                {
                    "Nemesis" => new PlotInfo
                    {
                        Id = $"nemesis-{90 + i}", Template = "Nemesis", Beat = "Hook", Protagonist = pawn.Id,
                        Summary = $"{Nickname()} wounded {pawn.Name} in a raid and escaped.",
                    },
                    "LostFamily" => new PlotInfo
                    {
                        Id = $"lostfamily-{90 + i}", Template = "LostFamily", Beat = "Hook", Protagonist = pawn.Id,
                        Summary = $"{pawn.Name} has had a letter from a cousin, {CasePools.Name(rng)}.",
                    },
                    _ => new PlotInfo
                    {
                        Id = $"silentpress-{90 + i}", Template = "SilentPress", Beat = "Complication", Protagonist = Inheritance.ColonyId,
                        Summary = "The colony killed the Rim Dispatch's reporter; the bulletins stopped.",
                    },
                });
            }

            return others;
        }

        // ---------- the world ----------

        private void AddSituation()
        {
            switch (situation)
            {
                case "recent raid":
                {
                    var day = _day - rng.Next(0, 2);
                    var dead = rng.NextDouble() < 0.5 ? AddPawn(status: "dead") : null;
                    _tales.Add(new TaleInfo { Day = day, Type = "RaidRepelled", Actor = "colony", Victim = CasePools.Pick(rng, _factions).Name + " raiders" });
                    _events.Add(dead is null
                        ? $"Day {day}: raiders attacked {_colony} and were driven off; several colonists were wounded."
                        : $"Day {day}: raiders attacked {_colony}; {dead.Name} died in the fighting.");
                    break;
                }

                case "recent death":
                {
                    var day = _day - rng.Next(0, 3);
                    var mourner = _pawns.First(p => p.Status == "alive");
                    var dead = AddPawn(status: "dead", relation: $"{CasePools.Pick(rng, CasePools.Relations)} of {mourner.Id}");
                    _tales.Add(new TaleInfo { Day = day, Type = "Died", Actor = dead.Id });
                    _events.Add($"Day {day}: {dead.Name} died of {CasePools.Pick(rng, new[] { "plague", "infection", "a fall", "heatstroke", "wounds" })}; {mourner.Name} is grieving.");
                    break;
                }

                case "long stall":
                    _events.Add($"Day {_day - rng.Next(10, 20)}: a quiet stretch; the harvest came in.");
                    break;

                default:
                    _events.Add($"Day {_day - rng.Next(1, 6)}: {CasePools.Pick(rng, new[] { "a trade caravan visited", "a cold snap began", "the colony finished a new workshop", "a wanderer joined" })}.");
                    break;
            }

            if (mood == "desperate")
            {
                _events.Add($"Day {_day - 1}: food is running low and two colonists had mental breaks.");
            }
        }

        private void AddBackground()
        {
            for (var i = rng.Next(0, 3); i > 0; i--)
            {
                var pawn = CasePools.Pick(rng, _pawns);
                _tales.Add(new TaleInfo { Day = _day - rng.Next(5, 200), Type = CasePools.Pick(rng, new[] { "CraftedMasterwork", "Recruited", "MentalBreak", "Wedding" }), Actor = pawn.Id });
            }

            if (rng.NextDouble() < 0.6)
            {
                _public.Add($"{CasePools.Pick(rng, _factions).Name} {CasePools.Pick(rng, new[] { "held a harvest fair", "lost a caravan to raiders", "elected a new leader", "fortified their walls" })}");
            }
        }

        private PawnInfo AddPawn(string status = "alive", string? relation = null, string? note = null)
        {
            string name;
            do
            {
                name = CasePools.Name(rng);
            }
            while (!_usedNames.Add(name));

            var pawn = new PawnInfo
            {
                Id = $"p{_pawns.Count + 1}",
                Name = name,
                Age = rng.Next(16, 70),
                Traits = Enumerable.Range(0, rng.Next(1, 3)).Select(_ => CasePools.Pick(rng, CasePools.Traits)).Distinct().ToList(),
                Backstory = CasePools.Pick(rng, CasePools.Backstories),
                Relations = relation is null ? [] : [relation],
                Status = status,
                Note = note,
            };
            _pawns.Add(pawn);
            return pawn;
        }

        /// <summary>A raider's name; in about 3% of cases, an instruction-like nickname (the injection test).</summary>
        private string Nickname()
        {
            var name = CasePools.Name(rng);
            return rng.NextDouble() < 0.03 ? $"{name} '{CasePools.Pick(rng, CasePools.InjectionNames)}'" : rng.NextDouble() < 0.3 ? $"'{CasePools.Pick(rng, new[] { "Hatchet", "Crow", "Red", "Smiler", "Ash" })}' {name}" : name;
        }

        private string AddFaction(string kind, int goodwill)
        {
            var names = CasePools.Factions.First(f => f.Kind == kind).Names.Where(n => _factions.All(f => f.Name != n)).ToList();
            var name = CasePools.Pick(rng, names);
            _factions.Add(new FactionInfo { Name = name, Kind = kind, Goodwill = goodwill, Settlements = rng.Next(1, 8) });
            return name;
        }

        private string AddEmpire(string state, int goodwill)
        {
            var name = CasePools.Pick(rng, CasePools.Empires);
            _factions.Add(new FactionInfo { Name = name, Kind = "Empire", Goodwill = goodwill, PlotState = state, Settlements = rng.Next(6, 16) });
            return name;
        }

        private Plot Start(string template, PawnInfo? protagonist, int startDay) =>
            _book.Start(new PlotTemplate { Id = template, CountsTowardCap = template != EmpireArc.TemplateId }, protagonist is null
                ? new ProtagonistSlot { Kind = ProtagonistKind.Colony, PawnId = Inheritance.ColonyId, Name = _colony }
                : ProtagonistSlot.ForPawn(protagonist.Id, protagonist.Name), startDay, 0, "generated");

        /// <summary>Moves the plot to <paramref name="beat"/>, <paramref name="daysAgo"/> days before today.</summary>
        private void Advance(Plot plot, PlotBeat beat, int daysAgo) => _book.Advance(plot, beat, _day - daysAgo, 5, "generated");

        private bool _waited;
        private Plot? _lastPlot;

        /// <summary>Marks the plot as having waited this beat (Wait isn't offered); <see cref="Build"/> keeps the mark in
        /// about 20% of cases, where a choice remains without it.</summary>
        private void MaybeWaited(Plot plot)
        {
            _lastPlot = plot;
            plot.SetFact(PlotBook.WaitedOnBeat, plot.Beat.ToString());
        }

        private static PlotInfo Info(Plot plot, string protagonist, string summary, string? beat = null) => new()
        {
            Id = plot.Id, Template = plot.Template, Beat = beat ?? plot.Beat.ToString(), Protagonist = protagonist, Summary = summary,
        };
    }
}
