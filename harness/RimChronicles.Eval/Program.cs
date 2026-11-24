// rimchron-eval: Stage 0 model evaluation harness.
using RimChronicles.Eval.Commands;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Judging;
using RimChronicles.Eval.Reporting;
using RimChronicles.Eval.Running;
using RimChronicles.Eval.Servers;
using RimChronicles.Llm.Prompts;

const string usage = """
    rimchron-eval <command> [options]

    Commands:
      validate              Check snapshots, prompts and the model config agree
      check                 Preflight the model servers: names, a test call per task, thinking off, VRAM
      run                   Generate, cache and score outputs; writes results and summary.html
      judge <run>           Judge a run's prose outputs blind with the config's judge model; rewrites summary.html
      report <run>          Rebuild summary.html; --compare <run> adds deltas against an earlier run
      spotcheck <run>       First time: write a blind 10% sample to score by hand. After filling it in: score agreement
      director-gen          Generate director training cases (training track D2) into training/data/cases/synthetic
      director-import       Import director cases recorded in game (D3) into training/data/cases/exported
      director-pack         Write a labelling pack for a teacher working by hand (D4)
      director-labels-check Check a pack's labels.jsonl and aggregate it into labels-final.jsonl
      director-label        Label cases with a local model and the teacher rubric (D4, bulk; resumable)
      director-report       Score models on the frozen director test set (D8): harness/reports/<name>.md
      director-review       Blind review of a director model against the teacher (D9): a page to fill in, then --answers
      director-dataset      Build train/val/test JSONL for fine-tuning from labelled cases (D5)

    <run> is a run id under --out, a path to a run folder, or "latest".

    Common options (paths default to the repository layout):
      --config <file>          Model config (default harness/models.json)
      --prompts <dir>          Prompt folders (default prompts)
      --snapshots-dir <dir>    Snapshot files (default harness/snapshots)
      --passbars <file>        Pass bars (default harness/passbars.json)
      --out <dir>              Run folders (default results)
      --cache <dir>            Response cache (default results/cache)
      --no-cache               Call the models even when a cached response exists

    run options:
      --models a,b             Only these model ids
      --tasks t1,t2            Only these tasks: director, news-bulletin, inheritance-handoff
      --snapshots p1,p2        Only snapshot ids matching these patterns ('*' wildcard)
      --repeats <n>            Override the config's repeat count
      --judge                  Judge the run straight after generating it
      --dry-run                List the cells without calling any model
      --temperature <t>        Sampling temperature for the local models, or "default" to leave it to the server

    check options:
      --models a,b             Only these model ids
      --vram-budget <GB>       Warn above this much VRAM per model (default 13, leaving room for RimWorld)

    director-gen options:
      --seed <n>               Generator seed (default 1); each seed gives a reproducible set
      --count <n>              Cases to write (default 5000)
      --out <dir>              Where to write them (default training/data/cases/synthetic/seed-<n>)
      --arc crisis             The Imperial crisis's cases instead (crisis C11; default count 1200,
                               default out training/data/cases/synthetic/seed-<n>-crisis)

    director-import options:
      --from <dir>             The game's recorded cases (on Windows: %USERPROFILE%\AppData\LocalLow\Ludeon Studios\
                               RimWorld by Ludeon Studios\RimChronicles\director-cases)
      --out <dir>              Where to put them (default training/data/cases/exported)

    director-pack options:
      --cases <dir>            Case folder (default training/data/cases/synthetic/seed-1)
      --count <n>              Cases in the pack (default 50)
      --repeat <n>             Cases shown twice, to measure self-agreement (default 10)
      --seed <n>               Selection and shuffling seed (default 1)
      --out <dir>              Pack folder (default training/data/labels/pilot)

    director-labels-check options:
      --pack <dir>             Pack folder (default training/data/labels/pilot)
      --cases <dir>            Case folder the pack was made from (default training/data/cases/synthetic/seed-1)

    director-label options:
      --models <id>            Teacher model from --config (default gemma4-12b)
      --cases <dir>            Case folder (default training/data/cases/synthetic/seed-1; test seeds are refused)
      --count <n>              Cases to label, round-robin over arcs and beats (default 4000)
      --repeats <n>            Samples per case, each with the options reshuffled (default 3)
      --temperature <t>        Sampling temperature (default 0.7)
      --seed <n>               Selection and shuffling seed (default 1)
      --out <dir>              Pack folder (default training/data/labels/local-<model>); run again to carry on
      --no-wait-rule           Accept labels that contradict a case's waitExpected (default: reject and ask again)
      --recheck <dir>          Calibrate instead: label exactly the cases of a labelled folder (e.g. the pilot) into
                               results/label-check-<model>-<rubric> and report agreement with its labels

    director-report options:
      --models a,b             Models from --config to score (required); always-Wait is scored beside them
      --name <name>            Report name (default the model ids joined by '+')
      --base <id>              The prompted base the lift column compares against
      --count <n>              Only n test cases, spread evenly over the set (a quick look)
      --cases a,b              Test case folders (default training/data/cases/synthetic/seed-9001)
      --labels a,b             Test label folders, scored together (default training/data/labels/test-v1)
      --out <dir>              Report folder (default harness/reports)
      --confirm-spend          Allow paid API models (Claude); without it they're refused

    director-review options:
      --models <id>            The director model to review (one, from --config)
      --count <n>              Cases, spread over the test set (default 50)
      --seed <n>               Case order and A/B sides (default 1)
      --out <dir>              Folder for review.html and key.json (default harness/reports/review-<id>)
      --answers <file>         Score a downloaded review-answers.json against the key: writes review.md

    director-dataset options:
      --labels a,b             Label folders with labels-final.jsonl (default training/data/labels/*)
      --cases a,b              Case folders (default every folder under training/data/cases)
      --tier <tier>            Minimum, Recommended or High: the prompt budget to render for (default Recommended)
      --test-seeds a,b         Generator seeds that form the test split (default 9001)
      --max-wait-share <0-1>   Most of the training cases whose best answer is Wait (default 0.4; 1 keeps all)
      --hand-only <arc,...>    Open cases (no Wait rule) in these arcs take only hand labels, not local-* ones
      --out <dir>              Output (default training/data/splits/<tier>)

    spotcheck options:
      --rate <0-1>             Share of judged outputs to sample (default 0.1)
      --seed <n>               Sampling seed (default 1)
      --resample               Replace an existing sample (discards scores in it)
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(usage);
    return 0;
}

var root = FindRepoRoot();
var valueOptions = new HashSet<string>
{
    "config", "prompts", "snapshots-dir", "passbars", "models", "tasks", "snapshots", "repeats", "out", "cache", "compare", "rate", "seed", "vram-budget", "temperature", "count", "from", "cases", "repeat", "pack", "labels", "tier", "test-seeds", "max-wait-share", "hand-only", "recheck", "name", "base", "answers", "arc",
};
var flags = new HashSet<string> { "no-cache", "dry-run", "judge", "resample", "no-wait-rule", "confirm-spend" };

CliArgs cli;
try
{
    cli = new CliArgs(args.Skip(1), valueOptions, flags);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

string PathOpt(string name, params string[] fallback) => Path.GetFullPath(cli.Get(name) ?? Path.Combine([root, .. fallback]));

var configPath = PathOpt("config", "harness", "models.json");
var promptsRoot = PathOpt("prompts", "prompts");
var snapshotsDir = PathOpt("snapshots-dir", "harness", "snapshots");
var passBarsPath = PathOpt("passbars", "harness", "passbars.json");
var outRoot = PathOpt("out", "results");
var cacheDir = PathOpt("cache", "results", "cache");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.Error.WriteLine("Cancelling... (finished responses stay in the cache)");
};

try
{
    switch (args[0])
    {
        case "validate":
            return ValidateCommand.Run(snapshotsDir, Path.Combine(root, "harness", "schema", "snapshot.schema.json"), promptsRoot, configPath, Console.Out);

        case "check":
        {
            var budget = cli.Get("vram-budget") is { } v ? double.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : 13.0;
            var checks = await RimChronicles.Eval.Servers.CheckCommand.RunAsync(HarnessConfig.Load(configPath), cli.List("models"),
                promptsRoot, snapshotsDir, budget, Console.Out, cts.Token);
            return checks.Any(c => c.Problems.Count > 0) ? 1 : 0;
        }

        case "run":
        {
            var config = HarnessConfig.Load(configPath);
            if (cli.Get("temperature") is { } temperature)
            {
                config = config.WithLocalTemperature(temperature);
            }

            if (!cli.Has("dry-run"))
            {
                var selected = cli.List("models") is { } only ? config.Models.Where(m => only.Contains(m.Id)) : config.Models;
                var weighed = (await ServerProbe.WithWeightsAsync(selected, Console.Out, cts.Token)).ToDictionary(m => m.Id);
                config = config with { Models = config.Models.Select(m => weighed.GetValueOrDefault(m.Id, m)).ToList() };
            }

            var result = await Runner.RunAsync(new RunOptions
            {
                Config = config,
                ConfigPath = configPath,
                PromptsRoot = promptsRoot,
                SnapshotsDir = snapshotsDir,
                OutputRoot = outRoot,
                CacheDir = cacheDir,
                Models = cli.List("models"),
                Tasks = cli.List("tasks"),
                Snapshots = cli.List("snapshots"),
                Repeats = cli.Has("repeats") ? cli.Int("repeats", 1) : null,
                UseCache = !cli.Has("no-cache"),
                DryRun = cli.Has("dry-run"),
            }, Console.Out, cts.Token);

            if (cli.Has("dry-run"))
            {
                return 0;
            }

            if (cli.Has("judge"))
            {
                await JudgeRun(config, result.Folder);
            }

            WriteReport(result.Folder, null);
            return result.Cells.Any(c => c.Outcome == "Error") ? 1 : 0;
        }

        case "judge":
        {
            var folder = RunFolder();
            var judged = await JudgeRun(HarnessConfig.Load(configPath), folder);
            WriteReport(folder, null);
            return judged.Any(j => !j.Judged) ? 1 : 0;
        }

        case "report":
            WriteReport(RunFolder(), cli.Get("compare") is { } other ? ResolveRun(other) : null);
            return 0;

        case "spotcheck":
        {
            var folder = RunFolder();
            if (!File.Exists(Path.Combine(folder, SpotCheck.SampleFile)) || cli.Has("resample"))
            {
                var rate = cli.Get("rate") is { } r ? double.Parse(r, System.Globalization.CultureInfo.InvariantCulture) : 0.1;
                if (rate is <= 0 or > 1)
                {
                    throw new ArgumentException("--rate must be between 0 and 1.");
                }

                SpotCheck.Create(folder, rate, cli.Int("seed", 1), Console.Out);
            }
            else
            {
                SpotCheck.Score(folder, Console.Out);
            }

            return 0;
        }

        case "director-gen":
        {
            var seed = cli.Int("seed", 1);
            var crisisArc = cli.Get("arc") switch
            {
                null => false,
                "crisis" => true,
                var unknownArc => throw new ArgumentException($"director-gen --arc: unknown arc '{unknownArc}' (only 'crisis')."),
            };
            var dir = cli.Get("out") is { } o ? Path.GetFullPath(o)
                : Path.Combine(root, "training", "data", "cases", "synthetic", crisisArc ? $"seed-{seed}-crisis" : $"seed-{seed}");
            return RimChronicles.Eval.DirectorCases.DirectorGenCommand.Run(seed, cli.Int("count", crisisArc ? 1200 : 5000), dir,
                Path.Combine(root, "harness", "schema", "snapshot.schema.json"), Console.Out,
                crisisArc ? RimChronicles.Eval.DirectorCases.SiteArchetypeXml.Load(Path.Combine(root, "Defs", "SiteArchetypeDefs")) : null);
        }

        case "director-import":
            return RimChronicles.Eval.DirectorCases.DirectorImportCommand.Run(
                cli.Get("from") is { } from ? Path.GetFullPath(from) : throw new ArgumentException("director-import needs --from <folder>."),
                cli.Get("out") is { } io ? Path.GetFullPath(io) : Path.Combine(root, "training", "data", "cases", "exported"),
                Path.Combine(root, "harness", "schema", "snapshot.schema.json"), Console.Out);

        case "director-pack":
            return RimChronicles.Eval.DirectorCases.DirectorLabels.Pack(
                CasesDir(), cli.Int("count", 50), cli.Int("repeat", 10), cli.Int("seed", 1),
                cli.Get("out") is { } po ? Path.GetFullPath(po) : Path.Combine(root, "training", "data", "labels", "pilot"), promptsRoot, Console.Out);

        case "director-labels-check":
            return RimChronicles.Eval.DirectorCases.DirectorLabels.Check(
                cli.Get("pack") is { } pk ? Path.GetFullPath(pk) : Path.Combine(root, "training", "data", "labels", "pilot"), CasesDir(), Console.Out) == 0 ? 0 : 1;

        case "director-label":
        {
            var config = HarnessConfig.Load(configPath);
            var teacherId = cli.List("models")?.Single() ?? "gemma4-12b";
            var teacher = config.Models.FirstOrDefault(m => m.Id == teacherId)
                ?? throw new ArgumentException($"No model '{teacherId}' in {configPath}.");
            teacher = (await ServerProbe.WithWeightsAsync([teacher], Console.Out, cts.Token))[0];
            var labelsRoot = Path.Combine(root, "training", "data", "labels");
            var recheck = cli.Get("recheck") is { } rc ? Path.GetFullPath(rc) : null;
            // A recheck goes under results/, never training/data/labels/, so calibration labels can't reach a dataset.
            var outDir = cli.Get("out") is { } lo ? Path.GetFullPath(lo)
                : recheck is not null ? Path.Combine(outRoot, $"label-check-{teacher.Id}-{PromptLibrary.Load(promptsRoot).Get("director-teacher").Meta.Version}")
                : Path.Combine(labelsRoot, $"local-{teacher.Id}");
            var onlyCases = recheck is null ? null
                : RimChronicles.Eval.DirectorCases.DirectorLabelCommand.LabelledCases(recheck);
            var labelOptions = new RimChronicles.Eval.DirectorCases.LabelOptions
            {
                CasesDir = CasesDir(),
                OutDir = outDir,
                PromptsRoot = promptsRoot,
                CacheDir = cacheDir,
                SkipLabelDirs = recheck is not null || !Directory.Exists(labelsRoot)
                    ? [] : Directory.GetDirectories(labelsRoot).Where(d => Path.GetFullPath(d) != outDir).ToList(),
                OnlyCases = onlyCases,
                EnforceWait = !cli.Has("no-wait-rule"),
                Count = onlyCases?.Count ?? cli.Int("count", 4000),
                Samples = cli.Int("repeats", 3),
                Seed = cli.Int("seed", 1),
                Temperature = cli.Get("temperature") is { } t ? double.Parse(t, System.Globalization.CultureInfo.InvariantCulture) : 0.7,
            };
            var labelled = await RimChronicles.Eval.DirectorCases.DirectorLabelCommand.RunAsync(labelOptions, teacher,
                RimChronicles.Eval.Providers.ProviderFactory.Create(teacher), Console.Out, cts.Token);
            if (recheck is null || labelled != 0)
            {
                return labelled;
            }

            RimChronicles.Eval.DirectorCases.DirectorLabels.Check(outDir, labelOptions.CasesDir, Console.Out);
            return RimChronicles.Eval.DirectorCases.DirectorLabelCommand.Compare(recheck, outDir, Console.Out);
        }

        case "director-report":
        {
            var config = HarnessConfig.Load(configPath);
            var ids = cli.List("models") ?? throw new ArgumentException("director-report needs --models a,b.");
            var models = ids.Select(id => config.Models.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"No model '{id}' in {configPath}.")).ToList();
            models = (await ServerProbe.WithWeightsAsync(models, Console.Out, cts.Token)).ToList();
            if (models.FirstOrDefault(m => m.Provider == ProviderKind.Anthropic) is { } paid && !cli.Has("confirm-spend"))
            {
                throw new ArgumentException($"{paid.Id} is a paid API model (5 calls per test case); add --confirm-spend to run it.");
            }

            var data = Path.Combine(root, "training", "data");
            return await RimChronicles.Eval.DirectorCases.DirectorReportCommand.RunAsync(new RimChronicles.Eval.DirectorCases.ReportOptions
            {
                CaseDirs = cli.List("cases")?.Select(Path.GetFullPath).ToList() ?? [Path.Combine(data, "cases", "synthetic", "seed-9001")],
                LabelDirs = cli.List("labels")?.Select(Path.GetFullPath).ToList() ?? [Path.Combine(data, "labels", "test-v1")],
                PromptsRoot = promptsRoot,
                CacheDir = cacheDir,
                OutDir = cli.Get("out") is { } ro ? Path.GetFullPath(ro) : Path.Combine(root, "harness", "reports"),
                Name = cli.Get("name") ?? string.Join("+", ids),
                BaseModel = cli.Get("base"),
                Count = cli.Has("count") ? cli.Int("count", 0) : null,
            }, models, RimChronicles.Eval.Providers.ProviderFactory.Create, Console.Out, cts.Token);
        }

        case "director-review":
        {
            var config = HarnessConfig.Load(configPath);
            var id = cli.List("models")?.Single() ?? throw new ArgumentException("director-review needs --models <id>.");
            var model = config.Models.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException($"No model '{id}' in {configPath}.");
            if (cli.Get("answers") is null)
            {
                model = (await ServerProbe.WithWeightsAsync([model], Console.Out, cts.Token))[0];
            }

            var reviewDir = cli.Get("out") is { } rvo ? Path.GetFullPath(rvo) : Path.Combine(root, "harness", "reports", $"review-{id}");
            if (cli.Get("answers") is { } answers)
            {
                return RimChronicles.Eval.DirectorCases.DirectorReviewCommand.Score(reviewDir, Path.GetFullPath(answers), Console.Out);
            }

            var data = Path.Combine(root, "training", "data");
            return await RimChronicles.Eval.DirectorCases.DirectorReviewCommand.PrepareAsync(new RimChronicles.Eval.DirectorCases.ReviewOptions
            {
                CaseDirs = cli.List("cases")?.Select(Path.GetFullPath).ToList() ?? [Path.Combine(data, "cases", "synthetic", "seed-9001")],
                LabelDirs = cli.List("labels")?.Select(Path.GetFullPath).ToList() ?? [Path.Combine(data, "labels", "test-v1")],
                PromptsRoot = promptsRoot,
                CacheDir = cacheDir,
                OutDir = reviewDir,
                Count = cli.Int("count", 50),
                Seed = cli.Int("seed", 1),
            }, model, RimChronicles.Eval.Providers.ProviderFactory.Create(model), Console.Out, cts.Token);
        }

        case "director-dataset":
        {
            var data = Path.Combine(root, "training", "data");
            var tier = Enum.Parse<RimChronicles.Llm.ModelTier>(cli.Get("tier") ?? "Recommended", ignoreCase: true);
            var labelDirs = cli.List("labels")?.Select(Path.GetFullPath).ToList()
                ?? Directory.GetDirectories(Path.Combine(data, "labels")).Where(d => File.Exists(Path.Combine(d, "labels-final.jsonl")))
                    // Hand (Claude) labels before a local teacher's: where both cover a case, the first accepted label wins.
                    .OrderBy(d => Path.GetFileName(d).StartsWith("local-", StringComparison.Ordinal) ? 1 : 0).ThenBy(d => d, StringComparer.Ordinal).ToList();
            var caseDirs = cli.List("cases")?.Select(Path.GetFullPath).ToList()
                ?? Directory.GetFiles(Path.Combine(data, "cases"), "manifest.json", SearchOption.AllDirectories).Select(f => Path.GetDirectoryName(f)!).Order().ToList();
            return RimChronicles.Eval.DirectorCases.DirectorDataset.Build(new RimChronicles.Eval.DirectorCases.DatasetOptions
            {
                LabelDirs = labelDirs,
                CaseDirs = caseDirs,
                OutDir = cli.Get("out") is { } dso ? Path.GetFullPath(dso) : Path.Combine(data, "splits", tier.ToString().ToLowerInvariant()),
                PromptsRoot = promptsRoot,
                Tier = tier,
                TestSeeds = (cli.List("test-seeds") ?? ["9001"]).Select(int.Parse).ToHashSet(),
                MaxWaitShare = cli.Get("max-wait-share") is { } mw ? double.Parse(mw, System.Globalization.CultureInfo.InvariantCulture) : 0.4,
                HandOnlyArcs = (cli.List("hand-only") ?? []).ToHashSet(StringComparer.Ordinal),
            }, Console.Out);
        }

        default:
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            Console.Error.WriteLine(usage);
            return 2;
    }
}
catch (OperationCanceledException)
{
    return 130;
}
catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or KeyNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

string CasesDir() => cli.Get("cases") is { } c ? Path.GetFullPath(c) : Path.Combine(root, "training", "data", "cases", "synthetic", "seed-1");

async Task<IReadOnlyList<JudgeRow>> JudgeRun(HarnessConfig config, string folder) =>
    await Judge.RunAsync(new JudgeOptions
    {
        RunFolder = folder,
        JudgeModel = config.Judge ?? throw new InvalidDataException($"{configPath} has no judge."),
        PromptsRoot = promptsRoot,
        CacheDir = cacheDir,
        UseCache = !cli.Has("no-cache"),
    }, Console.Out, cts.Token);

void WriteReport(string folder, string? compareFolder)
{
    var bars = File.Exists(passBarsPath) ? PassBars.Load(passBarsPath) : PassBars.Empty;
    var run = RunData.Load(folder, bars);
    var compare = compareFolder is null ? null : RunData.Load(compareFolder, bars);
    Console.WriteLine($"Report: {HtmlReport.Write(run, compare)}");
}

string RunFolder() =>
    cli.Positional.Count == 1 ? ResolveRun(cli.Positional[0]) : throw new ArgumentException($"'{args[0]}' needs one run id, path or \"latest\".");

string ResolveRun(string idOrPath)
{
    if (idOrPath == "latest")
    {
        var latest = Directory.Exists(outRoot)
            ? Directory.GetDirectories(outRoot).Where(d => File.Exists(Path.Combine(d, "results.csv"))).Order(StringComparer.Ordinal).LastOrDefault()
            : null;
        return latest ?? throw new ArgumentException($"No runs in {outRoot}.");
    }

    foreach (var candidate in new[] { Path.Combine(outRoot, idOrPath), Path.GetFullPath(idOrPath) })
    {
        if (File.Exists(Path.Combine(candidate, "results.csv")))
        {
            return candidate;
        }
    }

    throw new ArgumentException($"No run '{idOrPath}' (looked in {outRoot}).");
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "RimChronicles.slnx")))
        {
            return dir.FullName;
        }
    }

    return Directory.GetCurrentDirectory();
}
