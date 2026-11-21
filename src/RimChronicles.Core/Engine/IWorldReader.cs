using RimChronicles.Core.Snapshots;

namespace RimChronicles.Core.Engine;

/// <summary>Reads the running game into a <see cref="WorldSnapshot"/>. Main thread only; the snapshot is
/// plain data and may then be handed to a background task.</summary>
public interface IWorldReader
{
    WorldSnapshot Read();
}
