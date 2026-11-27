using RimChronicles.Core.Engine;

namespace RimChronicles.Tests.Core;

public class StepsTests
{
    [Fact]
    public void A_failing_step_does_not_stop_the_later_ones()
    {
        var ran = new List<string>();
        var reported = new List<(string, string)>();
        var failed = Steps.RunAll(
        [
            new("first", () => ran.Add("first")),
            new("broken", () => throw new InvalidOperationException("boom")),
            new("last", () => ran.Add("last")),
        ], (name, ex) => reported.Add((name, ex.Message)));

        Assert.Equal(["first", "last"], ran);
        Assert.Equal(["broken"], failed);
        Assert.Equal([("broken", "boom")], reported);
    }

    [Fact]
    public void No_failures_reports_nothing()
    {
        var failed = Steps.RunAll([new("ok", () => { })], (_, _) => Assert.Fail("nothing should fail"));
        Assert.Empty(failed);
    }
}
