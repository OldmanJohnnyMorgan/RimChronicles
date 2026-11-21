using RimChronicles.Core.Persistence;

namespace RimChronicles.Tests.TestSupport;

/// <summary>An in-memory <see cref="IArchive"/> standing in for RimWorld's Scribe. Save into one, then build a
/// loading archive from <see cref="Data"/> to round-trip.</summary>
public sealed class DictionaryArchive(ArchiveMode mode, Dictionary<string, object?>? data = null) : IArchive
{
    public ArchiveMode Mode { get; } = mode;

    public Dictionary<string, object?> Data { get; } = data ?? [];

    public void Value<T>(ref T value, string label, T defaultValue = default!)
    {
        switch (Mode)
        {
            case ArchiveMode.Saving:
                Data[label] = value;
                break;
            case ArchiveMode.Loading:
                value = Data.TryGetValue(label, out var stored) && stored is T typed ? typed : defaultValue;
                break;
        }
    }

    public void Strings(ref List<string> list, string label)
    {
        switch (Mode)
        {
            case ArchiveMode.Saving:
                Data[label] = list?.ToList();
                break;
            case ArchiveMode.Loading:
                list = Data.TryGetValue(label, out var stored) && stored is List<string> l ? [.. l] : [];
                break;
        }
    }

    public void Deep<T>(ref T value, string label) where T : class, IArchivable, new()
    {
        switch (Mode)
        {
            case ArchiveMode.Saving:
                var child = new DictionaryArchive(ArchiveMode.Saving);
                value.Expose(child);
                Data[label] = child.Data;
                break;
            case ArchiveMode.Loading:
                value = new T();
                if (Data.TryGetValue(label, out var stored) && stored is Dictionary<string, object?> d)
                {
                    value.Expose(new DictionaryArchive(ArchiveMode.Loading, d));
                }

                break;
        }
    }

    public void DeepList<T>(ref List<T> list, string label) where T : class, IArchivable, new()
    {
        switch (Mode)
        {
            case ArchiveMode.Saving:
                Data[label] = (list ?? []).Where(x => x is not null).Select(item =>
                {
                    var child = new DictionaryArchive(ArchiveMode.Saving);
                    item.Expose(child);
                    return child.Data;
                }).ToList();
                break;
            case ArchiveMode.Loading:
                list = [];
                if (Data.TryGetValue(label, out var stored) && stored is List<Dictionary<string, object?>> items)
                {
                    foreach (var d in items)
                    {
                        var item = new T();
                        item.Expose(new DictionaryArchive(ArchiveMode.Loading, d));
                        list.Add(item);
                    }
                }

                break;
        }
    }
}
