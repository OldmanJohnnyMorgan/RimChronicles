using RimChronicles.Core.Persistence;
using Verse;

namespace RimChronicles.Game.Engine;

/// <summary>Backs Core's <see cref="IArchive"/> with RimWorld's Scribe. Use it only inside an ExposeData call.</summary>
public sealed class ScribeArchive : IArchive
{
    public static readonly ScribeArchive Instance = new();

    private ScribeArchive()
    {
    }

    public ArchiveMode Mode => Scribe.mode switch
    {
        LoadSaveMode.Saving => ArchiveMode.Saving,
        LoadSaveMode.LoadingVars => ArchiveMode.Loading,
        _ => ArchiveMode.Other,
    };

    public void Value<T>(ref T value, string label, T defaultValue = default!) =>
        Scribe_Values.Look(ref value, label, defaultValue);

    public void Strings(ref List<string> list, string label)
    {
        Scribe_Collections.Look(ref list, label, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            list ??= [];
        }
    }

    public void Deep<T>(ref T value, string label) where T : class, IArchivable, new()
    {
        if (Scribe.mode == LoadSaveMode.Saving && value is null)
        {
            return;
        }

        if (Scribe.EnterNode(label))
        {
            try
            {
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    value = new T();
                }

                value ??= new T();
                value.Expose(this);
            }
            finally
            {
                Scribe.ExitNode();
            }
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            value = new T();
        }
    }
}
