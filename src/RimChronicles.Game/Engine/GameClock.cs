using RimChronicles.Core.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Engine;

public sealed class GameClock : IGameClock
{
    public static readonly GameClock Instance = new();

    public int TicksGame => Find.TickManager.TicksGame;

    public int DaysPassed => GenDate.DaysPassed;
}
