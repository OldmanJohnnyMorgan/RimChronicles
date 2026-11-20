// rimchron-eval: Stage 0 model evaluation harness.
// Commands are added in milestones M3 (validate, run) and M4 (judge, report, spotcheck).

const string usage = """
    rimchron-eval <command> [options]

    Commands (planned):
      validate    Check snapshots against the schema and prompts for missing variables  (M3)
      run         Generate, cache and score outputs for snapshot x task x model           (M3)
      judge       Judge prose outputs of a run                                            (M4)
      report      Build results.csv and summary.html, optionally --compare <runId>        (M4)
      spotcheck   Sample 10% of a run for hand scoring                                    (M4)
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(usage);
    return 0;
}

Console.Error.WriteLine($"Command '{args[0]}' is not implemented yet.");
Console.Error.WriteLine(usage);
return 2;
