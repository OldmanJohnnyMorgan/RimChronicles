namespace RimChronicles.Core.Powers;

/// <summary>
/// The summons' conversion levels (War in Heaven spec §6.1, D7; tech spec R5). A mechanitor the Empire takes comes back
/// a psycaster: psylink level = 1 + ⌊(bandwidth − 6) / 4⌋, from 1 to 6, where bandwidth is the pawn's own (the mechlink's
/// 6 and its implants and gear, without band nodes, which stay at the colony). A psycaster the Choir takes comes back a
/// mechanitor with the bandwidth of that level: 6 + 4 × (level − 1), the mechlink's 6 plus the rest as a bonus.
/// Plain numbers; the game reads and applies them (W6).
/// </summary>
public static class Conversion
{
    /// <summary>A mechlink's own bandwidth.</summary>
    public const int MechlinkBandwidth = 6;

    /// <summary>Bandwidth per psylink level above the first.</summary>
    public const int BandwidthPerLevel = 4;

    public const int MinPsylinkLevel = 1;
    public const int MaxPsylinkLevel = 6;

    /// <summary>The psylink level a mechanitor with <paramref name="ownBandwidth"/> becomes.</summary>
    public static int PsylinkLevelFor(int ownBandwidth) =>
        Math.Clamp(MinPsylinkLevel + Math.Max(0, ownBandwidth - MechlinkBandwidth) / BandwidthPerLevel, MinPsylinkLevel, MaxPsylinkLevel);

    /// <summary>The bandwidth a psycaster of <paramref name="psylinkLevel"/> becomes (the mechlink's 6 included).</summary>
    public static int BandwidthFor(int psylinkLevel) =>
        MechlinkBandwidth + BandwidthPerLevel * (Math.Clamp(psylinkLevel, MinPsylinkLevel, MaxPsylinkLevel) - MinPsylinkLevel);

    /// <summary>The bandwidth on top of the mechlink's 6 (what the bonus hediff adds).</summary>
    public static int BonusBandwidthFor(int psylinkLevel) => BandwidthFor(psylinkLevel) - MechlinkBandwidth;

    /// <summary>The pawn's own bandwidth: the total stat less what tuned band nodes add (ENGINE_NOTES, W0).</summary>
    public static int OwnBandwidth(int totalBandwidth, int bandNodeBandwidth) => Math.Max(0, totalBandwidth - Math.Max(0, bandNodeBandwidth));
}
