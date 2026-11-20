namespace RimChronicles.Tests.TestSupport;

/// <summary>Paths into the repository, found by walking up from the test binaries to RimChronicles.slnx.</summary>
public static class Repo
{
    public static readonly string Root = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public static string Prompts => Path("prompts");
    public static string Snapshots => Path("harness", "snapshots");
    public static string SnapshotSchema => Path("harness", "schema", "snapshot.schema.json");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "RimChronicles.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (RimChronicles.slnx).");
    }
}

/// <summary>
/// Golden-file assertions. Set UPDATE_GOLDEN=1 to rewrite the files from the current output, then review the diff.
/// </summary>
public static class Golden
{
    public static void Match(string name, string actual)
    {
        var path = Repo.Path("tests", "RimChronicles.Tests", "Golden", name);
        actual = actual.Replace("\r\n", "\n") + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, actual);
            if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") != "1")
            {
                Assert.Fail($"Golden file {name} did not exist and was created. Review it and run the tests again.");
            }

            return;
        }

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }
}
