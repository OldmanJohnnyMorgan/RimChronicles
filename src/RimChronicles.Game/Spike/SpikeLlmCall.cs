using RimChronicles.Core.Engine;
using RimChronicles.Llm.Chat;
using RimChronicles.Game.Settings;
using Verse;

namespace RimChronicles.Game.Spike;

/// <summary>Spike test 3: one LLM call on a background task, with the reply delivered as a letter through the
/// main-thread queue. If the server can't be reached, a template letter says so (rule 7's fallback).</summary>
public static class SpikeLlmCall
{
    private const string System = "You are a frontier radio reporter on a RimWorld rim planet. Reply with plain text only.";

    public static void Start(RimChroniclesGameComponent component)
    {
        var settings = RimChroniclesMod.Settings;
        var colony = RimWorld.Faction.OfPlayer.Name; // read on the main thread; the task gets only plain data
        var day = RimWorld.GenDate.DaysPassed;
        var mainThread = component.MainThread;
        var token = component.GameToken;

        if (ProviderFactory.Create(settings) is not { } provider)
        {
            Engine.LetterSink.Instance.Send("Rim Chronicles: radio silence",
                "The model tier is Off (or the server address is invalid) in the mod settings, so this letter is the template fallback.",
                LetterKind.Negative);
            return;
        }

        var request = new ChatRequest
        {
            System = System,
            User = $"In two short sentences, welcome listeners to the first broadcast for the colony of {colony}, day {day}.",
            MaxOutputTokens = 120,
            Thinking = ThinkingMode.Off,
        };

        var threadId = Environment.CurrentManagedThreadId;
        Task.Run(async () =>
        {
            var background = Environment.CurrentManagedThreadId;
            var result = await provider.CompleteAsync(request, token).ConfigureAwait(false);
            mainThread.Post(() => Deliver(component, result, day, threadId, background));
        }, token);

        Messages.Message("Rim Chronicles: asking the model in the background…", RimWorld.MessageTypeDefOf.NeutralEvent, historical: false);
    }

    private static void Deliver(RimChroniclesGameComponent component, ChatResult result, int day, int mainThreadId, int backgroundThreadId)
    {
        var threads = $"main thread {mainThreadId}, call on thread {backgroundThreadId}, delivered on thread {Environment.CurrentManagedThreadId}";
        if (result.Ok && !string.IsNullOrWhiteSpace(result.Text))
        {
            var text = result.Text.Trim();
            component.Spike.LastReply = text;
            component.Spike.LastReplyDay = day;
            component.Spike.AddLog($"day {day}: reply in {result.Latency.TotalSeconds:0.0} s");
            Engine.LetterSink.Instance.Send("Rim Chronicles: test broadcast",
                $"{text}\n\n({result.ModelId}, {result.Latency.TotalSeconds:0.0} s, {result.OutputTokens} tokens; {threads})");
            Log.Message($"[Rim Chronicles] LLM reply in {result.Latency.TotalSeconds:0.0} s; {threads}.");
        }
        else
        {
            component.Spike.AddLog($"day {day}: call failed");
            Engine.LetterSink.Instance.Send("Rim Chronicles: radio silence",
                $"The model didn't answer, so this letter is the template fallback.\n\nError: {result.Error}\n\n({threads})",
                LetterKind.Negative);
            Log.Warning($"[Rim Chronicles] LLM call failed: {result.Error}");
        }
    }
}
