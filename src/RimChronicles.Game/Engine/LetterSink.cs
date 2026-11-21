using RimChronicles.Core.Engine;
using RimWorld;
using Verse;

namespace RimChronicles.Game.Engine;

public sealed class LetterSink : ILetterSink
{
    public static readonly LetterSink Instance = new();

    public void Send(string label, string text, LetterKind kind = LetterKind.Neutral)
    {
        var def = kind switch
        {
            LetterKind.Positive => LetterDefOf.PositiveEvent,
            LetterKind.Negative => LetterDefOf.NegativeEvent,
            _ => LetterDefOf.NeutralEvent,
        };
        Find.LetterStack.ReceiveLetter(label, text, def);
    }
}
