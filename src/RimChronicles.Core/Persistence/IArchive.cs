namespace RimChronicles.Core.Persistence;

/// <summary>What an <see cref="IArchive"/> is doing on this pass.</summary>
public enum ArchiveMode
{
    /// <summary>Writing values out; refs are read.</summary>
    Saving,

    /// <summary>Reading values in; refs are written. A missing value leaves the default.</summary>
    Loading,

    /// <summary>Any later pass (e.g. resolving references); values are left alone.</summary>
    Other,
}

/// <summary>Save-game storage for Core types, which can't call RimWorld's Scribe. The Game layer backs it with
/// Scribe; tests back it with a dictionary. Loading must tolerate missing values, because the mod can be added to
/// an existing save.</summary>
public interface IArchive
{
    ArchiveMode Mode { get; }

    /// <summary>Saves or loads a primitive (int, bool, float, string, enum). Missing on load → <paramref name="defaultValue"/>.</summary>
    void Value<T>(ref T value, string label, T defaultValue = default!);

    /// <summary>Saves or loads a list of strings. Missing on load → an empty list, never null.</summary>
    void Strings(ref List<string> list, string label);

    /// <summary>Saves or loads a nested object under <paramref name="label"/>. Missing on load → a new instance.</summary>
    void Deep<T>(ref T value, string label) where T : class, IArchivable, new();

    /// <summary>Saves or loads a list of nested objects. Missing on load → an empty list, never null; null items are dropped.</summary>
    void DeepList<T>(ref List<T> list, string label) where T : class, IArchivable, new();
}

/// <summary>A Core type that is stored in the save file.</summary>
public interface IArchivable
{
    void Expose(IArchive archive);
}
