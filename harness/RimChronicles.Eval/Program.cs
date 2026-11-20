// rimchron-eval: Stage 0 model evaluation harness.
using RimChronicles.Eval.Commands;
using RimChronicles.Eval.Config;
using RimChronicles.Eval.Running;

const string usage = """
    rimchron-eval <command> [options]

    Commands:
      validate    Check snapshots against the schema, prompts against the summarizer, and the model config
      run         Generate, cache and score outputs for snapshot x task x model x grammar mode x repeat

    Common options (paths default to the repository layout):
      --config <file>          Model config (default harness/models.json)
      --prompts <dir>          Prompt folders (default prompts)
      --snapshots-dir <dir>    Snapshot files (default harness/snapshots)

    run options:
      --models a,b             Only these model ids
      --tasks t1,t2            Only these tasks: director, news-bulletin, inheritance-handoff
      --snapshots p1,p2        Only snapshot ids matching these patterns ('*' wildcard)
      --repeats <n>            Override the config's repeat count
      --out <dir>              Run folders go here (default results)
      --cache <dir>            Response cache (default results/cache)
      --no-cache               Call the models even when a cached response exists
      --dry-run                List the cells without calling any model

    Planned: judge, report, spotcheck (M4).
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(usage);
    return 0;
}

var root = FindRepoRoot();
var valueOptions = new HashSet<string> { "config", "prompts", "snapshots-dir", "models", "tasks", "snapshots", "repeats", "out", "cache" };
var flags = new HashSet<string> { "no-cache", "dry-run" };

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

switch (args[0])
{
    case "validate":
        return ValidateCommand.Run(snapshotsDir, Path.Combine(root, "harness", "schema", "snapshot.schema.json"), promptsRoot, configPath, Console.Out);

    case "run":
        using (var cts = new CancellationTokenSource())
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.Error.WriteLine("Cancelling... (finished responses stay in the cache)");
            };

            try
            {
                var options = new RunOptions
                {
                    Config = HarnessConfig.Load(configPath),
                    ConfigPath = configPath,
                    PromptsRoot = promptsRoot,
                    SnapshotsDir = snapshotsDir,
                    OutputRoot = PathOpt("out", "results"),
                    CacheDir = PathOpt("cache", "results", "cache"),
                    Models = cli.List("models"),
                    Tasks = cli.List("tasks"),
                    Snapshots = cli.List("snapshots"),
                    Repeats = cli.Has("repeats") ? cli.Int("repeats", 1) : null,
                    UseCache = !cli.Has("no-cache"),
                    DryRun = cli.Has("dry-run"),
                };

                var result = await Runner.RunAsync(options, Console.Out, cts.Token);
                return result.Cells.Any(c => c.Outcome == "Error") ? 1 : 0;
            }
            catch (OperationCanceledException)
            {
                return 130;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or FileNotFoundException)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        Console.Error.WriteLine(usage);
        return 2;
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
