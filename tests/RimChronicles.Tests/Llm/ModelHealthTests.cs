using RimChronicles.Llm.Chat;
using RimChronicles.Llm.Tasks;

namespace RimChronicles.Tests.Llm;

public class ModelHealthTests
{
    private static readonly ChatRequest Request = new() { System = "s", User = "u" };

    private static TaskAttempt Down(string model = "gemma4:e4b", string error = "Connection refused") =>
        new(Request, ChatResult.Failure(model, error, TimeSpan.Zero), [error]);

    private static TaskAttempt Answered(string model = "gemma4:e4b", params string[] failures) =>
        new(Request, new ChatResult { Ok = true, Text = "x", ModelId = model }, failures);

    [Fact]
    public void WarnsOnce_WhileTheModelStaysDown()
    {
        var health = new ModelHealth();
        var first = health.Observe([Down(), Down()], "narration");
        Assert.NotNull(first);
        Assert.Contains("narration model (gemma4:e4b)", first);
        Assert.Contains("Connection refused", first);
        Assert.Contains("Test connection", first);
        Assert.Null(health.Observe([Down()], "narration"));
    }

    [Fact]
    public void WarnsAgain_AfterTheModelAnsweredInBetween()
    {
        var health = new ModelHealth();
        Assert.NotNull(health.Observe([Down()], "narration"));
        Assert.Null(health.Observe([Answered()], "narration"));
        Assert.NotNull(health.Observe([Down()], "narration"));
    }

    [Fact]
    public void AnAnswerThatFailsTheChecks_IsNotAnOutage()
    {
        var health = new ModelHealth();
        Assert.Null(health.Observe([Answered(failures: "too long"), Answered(failures: "too long")], "narration"));
        Assert.Null(health.Observe([Down(), Answered(failures: "too long")], "narration"));
    }

    [Fact]
    public void EachModelIsTrackedOnItsOwn()
    {
        var health = new ModelHealth();
        Assert.NotNull(health.Observe([Down("gemma4:e4b")], "narration"));
        var director = health.Observe([Down("rimchron-director-e2b", "HTTP 404: model not found")], "director");
        Assert.NotNull(director);
        Assert.Contains("director model (rimchron-director-e2b)", director);
    }

    [Fact]
    public void NoAttempts_NoNotice() => Assert.Null(new ModelHealth().Observe([], "narration"));
}
