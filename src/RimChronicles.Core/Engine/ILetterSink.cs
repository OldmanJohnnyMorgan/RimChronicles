namespace RimChronicles.Core.Engine;

/// <summary>The tone of a letter, which picks its colour and sound in game.</summary>
public enum LetterKind
{
    Neutral,
    Positive,
    Negative,
}

/// <summary>Sends letters to the player. Main thread only.</summary>
public interface ILetterSink
{
    void Send(string label, string text, LetterKind kind = LetterKind.Neutral);
}
