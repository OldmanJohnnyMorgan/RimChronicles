// rimchron-eval: Stage 0 model evaluation harness.
using RimChronicles.Eval.Commands;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Judging;
using RimChronicles.Eval.Reporting;
using RimChronicles.Eval.Running;

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
    "config", "prompts", "snapshots-dir", "passbars", "models", "tasks", "snapshots", "repeats", "out", "cache", "compare", "rate", "seed", "vram-budget", "temperature", "count",
};
var flags = new HashSet<string> { "no-cache", "dry-run", "judge", "resample" };

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
            var dir = cli.Get("out") is { } o ? Path.GetFullPath(o) : Path.Combine(root, "training", "data", "cases", "synthetic", $"seed-{seed}");
            return RimChronicles.Eval.DirectorCases.DirectorGenCommand.Run(seed, cli.Int("count", 5000), dir,
                Path.Combine(root, "harness", "schema", "snapshot.schema.json"), Console.Out);
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
