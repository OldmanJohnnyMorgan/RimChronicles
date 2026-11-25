using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RimChronicles.Core.Snapshots;

namespace RimChronicles.Eval.DirectorCases;

/// <summary>
/// <c>director-rule-labels</c> (War in Heaven W13): labels a siege's goal and a crisis site by the teacher rubric's own
/// rules (<c>prompts/director-teacher/v5/system.md</c>, the siege-goal and site lines), for the Imperial crisis and the
/// Hollow Choir's. These choices have no Wait and the rubric fixes them by the colony's state, but the local 12B
/// teacher broke the rules there (it agreed with them on 46 of 120 war cases), while the test labels follow them (81 of
/// 84). Where the local label already agrees, its reason is kept; otherwise the reason comes from a template. The
/// folder's name doesn't start with <c>local-</c>, so <c>director-dataset</c> reads it before the local labels and its
/// label wins for these cases.
/// </summary>
public static class DirectorRuleLabels
{
    public const string Teacher = "rubric rules (director-teacher/v5 siege-goal and site lines)";

    private static readonly JsonSerializerOptions Line = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex Event = new(@"^Day (-?\d+): (.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Blow = new(@"raid|died|killed", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The rubric's "reeling": a desperate mood, or a raid on the colony or a death within the last 3 days.
    /// Also why, for the reason.</summary>
    public static (bool Reeling, string Why) Reeling(WorldSnapshot s)
    {
        foreach (var e in s.RecentEvents)
        {
            var m = Event.Match(e);
            if (m.Success && s.Day - int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) <= 3 && Blow.IsMatch(m.Groups[2].Value))
            {
                return (true, m.Groups[2].Value.Contains("raid", StringComparison.OrdinalIgnoreCase) ? "was raided days ago" : "is mourning a fresh death");
            }
        }

        return s.Colony.Mood == "desperate" ? (true, "is desperate") : (false, "");
    }

    /// <summary>The rule's answer and a reason, or null when the case isn't a siege goal or a site.</summary>
    public static (string Best, string Reason)? Label(WorldSnapshot s)
    {
        var group = DirectorLabels.Group(s);
        return group.EndsWith("/SiegeGoal", StringComparison.Ordinal) ? Siege(s)
            : group.EndsWith("/Site", StringComparison.Ordinal) ? Site(s)
            : null;
    }

    private static (string, string) Siege(WorldSnapshot s)
    {
        var (reeling, why) = Reeling(s);
        var name = s.Colony.Name;
        var small = s.Colony.Population <= 5;
        var strained = s.Colony.Mood == "strained";
        string[] pref = reeling ? ["Goal:BreakWalls", "Goal:Starve", "Goal:KillLeader"]
            : small ? ["Goal:KillLeader", .. strained ? new[] { "Goal:Starve", "Goal:BreakWalls" } : ["Goal:BreakWalls", "Goal:Starve"]]
            : strained ? ["Goal:Starve", "Goal:BreakWalls", "Goal:KillLeader"]
            : ["Goal:BreakWalls", "Goal:Starve", "Goal:KillLeader"];
        var best = Pick(pref, s.AllowedTransitions);
        var reason = (best, reeling, small, strained) switch
        {
            ("Goal:BreakWalls", true, _, _) => $"{name} {why}; the siege should go for its walls and turrets, sparing its people and its food.",
            ("Goal:Starve", true, _, _) => $"{name} {why}; with its walls not on offer, starving it out spares its people.",
            ("Goal:KillLeader", false, true, _) => $"{name} has only {s.Colony.Population} people; a siege that hunts its leader would hurt it most.",
            ("Goal:Starve", false, _, true) => $"{name} is already strained; striking its fields and food stores would bite deepest.",
            ("Goal:BreakWalls", false, false, false) => $"{name} is steady with {s.Colony.Population} people; breaking its walls and turrets opens it for the assault.",
            ("Goal:Starve", _, _, _) => $"Starving {name} out by striking its fields and stores fits the siege best of what's offered.",
            ("Goal:BreakWalls", _, _, _) => $"Breaking {name}'s walls and turrets opens it for the assault without hunting its people.",
            _ => $"Striking at {name}'s leader is the sharpest goal on offer for this siege.",
        };
        return (best, reason);
    }

    private static (string, string) Site(WorldSnapshot s)
    {
        var choir = s.Crisis is null && s.ChoirCrisis is not null;
        var crisis = (choir ? s.ChoirCrisis : s.Crisis)!;
        var (outpost, holding, relay, uplink) = choir
            ? ("Site:ClusterOutpost", "Site:ChoirHolding", "Site:Resonator", "Site:SignalSpire")
            : ("Site:ForwardOutpost", "Site:ReclaimedHolding", "Site:PsychicRelay", "Site:OrbitalUplink");
        var (psychic, siege) = choir ? ("TheHum", "ClusterSiege") : ("PsychicAssault", "SiegeWithBombardment");
        var power = choir ? "the Choir" : "the Empire";
        var (reeling, why) = Reeling(s);
        var name = s.Colony.Name;
        var last = crisis.Operations.Count > 0 ? crisis.Operations[^1].Operation : null;
        var war = crisis.Stage == "War";

        string best;
        if (!reeling && s.AllowedTransitions.Contains(holding))
        {
            best = holding;
        }
        else if (reeling)
        {
            best = Pick([outpost], s.AllowedTransitions);
        }
        else
        {
            var pref = new List<string>();
            if (last == psychic)
            {
                pref.Add(relay);
            }

            if (war || last == siege)
            {
                pref.Add(uplink);
            }

            pref.Add(outpost);
            best = Pick(pref, s.AllowedTransitions);
        }

        var reason = best switch
        {
            _ when best == holding => choir
                ? $"{name} is steady enough to strike; freeing the town the Choir holds is the best site on offer."
                : $"{name} is steady enough to strike; liberating the settlement the Empire holds is the best site on offer.",
            _ when best == outpost && reeling => $"{name} {why}; the smallest site, {(choir ? "the cluster outpost" : "the forward outpost")}, is all it should take on now.",
            _ when best == relay => choir
                ? $"The Choir's hum just struck {name}; destroying the resonator silences it for weeks."
                : $"Psychic assaults just struck {name}; destroying the psychic relay weakens the Empire's adepts for weeks.",
            _ when best == uplink => choir
                ? (last == siege ? $"A cluster siege just hit {name}; destroying the signal spire stops the cluster drops." : $"In open war, destroying the signal spire stops the Choir's cluster drops on {name}.")
                : (last == siege ? $"A siege just shelled {name}; destroying the orbital uplink stops the Empire's strikes." : $"In open war, destroying the orbital uplink stops the Empire's orbital strikes on {name}."),
            _ when best == outpost => $"Nothing of {power}'s presses {name} hardest yet; the outpost blinds {power} to the colony at little cost.",
            _ => $"Of the sites offered, this one strikes best at what is hurting {name}.",
        };
        return (best, reason);
    }

    private static string Pick(IEnumerable<string> pref, IReadOnlyList<string> allowed) =>
        pref.FirstOrDefault(allowed.Contains) ?? allowed[0];

    /// <summary>Labels every siege-goal and site case in <paramref name="caseDirs"/> into <paramref name="outDir"/>
    /// (<c>labels-final.jsonl</c> and <c>pack.md</c>), keeping the reason of a label in <paramref name="localDirs"/>
    /// that picked the same option. Refuses test-seed cases: the test labels must stay the only labels for them.</summary>
    public static int Run(IReadOnlyList<string> caseDirs, IReadOnlyList<string> localDirs, IReadOnlySet<int> testSeeds, string outDir, TextWriter log)
    {
        var local = new Dictionary<string, JsonNode>();
        foreach (var dir in localDirs)
        {
            foreach (var line in File.ReadAllLines(Path.Combine(dir, "labels-final.jsonl")).Where(l => l.Length > 0))
            {
                var node = JsonNode.Parse(line)!;
                local.TryAdd(node["case"]!.GetValue<string>(), node);
            }
        }

        var sb = new StringBuilder();
        var counts = new SortedDictionary<string, (int Cases, int Agree, int Local)>(StringComparer.Ordinal);
        var picks = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var dir in caseDirs)
        {
            foreach (var file in Directory.GetFiles(dir, "dir-*.json").Order(StringComparer.Ordinal))
            {
                var s = SnapshotJson.Load(file);
                if (s.Director?.Seed is { } seed && testSeeds.Contains(seed))
                {
                    log.WriteLine($"{s.Id}: a test-seed case; refusing to label the test split.");
                    return 1;
                }

                if (Label(s) is not var (best, reason))
                {
                    continue;
                }

                var group = DirectorLabels.Group(s);
                var c = counts.GetValueOrDefault(group);
                c.Cases++;
                if (local.TryGetValue(s.Id, out var l))
                {
                    c.Local++;
                    if (l["best"]?.GetValue<string>() == best)
                    {
                        c.Agree++;
                        reason = l["reason"]!.GetValue<string>();
                    }
                }

                counts[group] = c;
                picks[$"{group} {best}"] = picks.GetValueOrDefault($"{group} {best}") + 1;
                var scores = new JsonObject();
                foreach (var o in s.AllowedTransitions)
                {
                    scores[o] = o == best ? 5 : 2;
                }

                sb.Append(JsonSerializer.Serialize(new JsonObject
                {
                    ["case"] = s.Id,
                    ["group"] = group,
                    ["samples"] = 1,
                    ["planned"] = 1,
                    ["choiceCounts"] = new JsonObject { [best] = 1 },
                    ["scores"] = scores,
                    ["best"] = best,
                    ["agreement"] = 1.0,
                    ["reason"] = reason,
                    ["forced"] = 0,
                    ["waitExpected"] = null,
                    ["teacher"] = Teacher,
                }, Line)).Append('\n');
            }
        }

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "labels-final.jsonl"), sb.ToString());
        var md = new StringBuilder("# Rule labels: siege goals and crisis sites\n\n")
            .Append($"Written by `director-rule-labels` from the teacher rubric's rules ({Teacher}), not by a model. ")
            .Append("Where the local teacher picked the same option, its reason is kept. `director-dataset` reads this folder ")
            .Append("before the `local-*` ones, so these labels replace the local teacher's for these cases.\n\n")
            .Append("| Group | Cases | Had a local label | Local label agreed |\n| --- | --- | --- | --- |\n");
        foreach (var (group, c) in counts)
        {
            md.Append($"| {group} | {c.Cases} | {c.Local} | {c.Agree} |\n");
            log.WriteLine($"{group}: {c.Cases} cases; the local teacher agreed on {c.Agree} of {c.Local}.");
        }

        md.Append("\n| Group and pick | Cases |\n| --- | --- |\n");
        foreach (var (k, n) in picks)
        {
            md.Append($"| {k} | {n} |\n");
        }

        File.WriteAllText(Path.Combine(outDir, "pack.md"), md.ToString());
        log.WriteLine($"Wrote {counts.Values.Sum(c => c.Cases)} rule labels to {outDir}.");
        return 0;
    }
}
