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

    /// <summary>Writes the list as <c>&lt;label&gt;&lt;count/&gt;&lt;i0&gt;…&lt;/i0&gt;&lt;i1&gt;…</c> using only
    /// <c>Scribe.EnterNode</c> and <c>Scribe_Values</c>, so no Core type needs to be an <c>IExposable</c> or be found by
    /// name on load. Core types hold no cross-references, so passes other than save and load are skipped.</summary>
    public void DeepList<T>(ref List<T> list, string label) where T : class, IArchivable, new()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            var items = (list ?? []).Where(x => x is not null).ToList();
            Scribe.EnterNode(label);
            try
            {
                var count = items.Count;
                Scribe_Values.Look(ref count, "count");
                for (var i = 0; i < items.Count; i++)
                {
                    Scribe.EnterNode($"i{i}");
                    try
                    {
                        items[i].Expose(this);
                    }
                    finally
                    {
                        Scribe.ExitNode();
                    }
                }
            }
            finally
            {
                Scribe.ExitNode();
            }
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            list = [];
            if (!Scribe.EnterNode(label))
            {
                return;
            }

            try
            {
                var count = 0;
                Scribe_Values.Look(ref count, "count");
                for (var i = 0; i < count; i++)
                {
                    if (!Scribe.EnterNode($"i{i}"))
                    {
                        continue;
                    }

                    try
                    {
                        var item = new T();
                        item.Expose(this);
                        list.Add(item);
                    }
                    finally
                    {
                        Scribe.ExitNode();
                    }
                }
            }
            finally
            {
                Scribe.ExitNode();
            }
        }
    }
}
