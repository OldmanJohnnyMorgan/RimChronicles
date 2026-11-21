namespace RimChronicles.Core.Engine;

/// <summary>The game's clock, as the story layer sees it.</summary>
public interface IGameClock
{
    /// <summary>Ticks since this game started (60,000 per in-game day).</summary>
    int TicksGame { get; }

    /// <summary>Days since this game started, counting from 0.</summary>
    int DaysPassed { get; }
}
