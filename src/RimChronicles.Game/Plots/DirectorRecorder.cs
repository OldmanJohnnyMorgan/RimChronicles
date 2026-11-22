using RimChronicles.Core.Snapshots;
using Verse;

namespace RimChronicles.Game.Plots;

/// <summary>Saves director decisions as training cases (director-model track D3), when the Dev Mode setting is on.
/// One small JSON file per decision, written on the main thread; the harness's <c>director-import</c> picks them up.</summary>
public static class DirectorRecorder
{
    public static string Folder => Path.Combine(GenFilePaths.SaveDataFolderPath, "RimChronicles", "director-cases");

    public static void Save(WorldSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, snapshot.Id + ".json");
            File.WriteAllText(path, SnapshotJson.Serialize(snapshot) + "\n");
            if (Prefs.DevMode)
            {
                Log.Message($"[Rim Chronicles] Director case recorded: {path}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning($"[Rim Chronicles] Couldn't record the director case {snapshot.Id}: {ex.Message}");
        }
    }
}
