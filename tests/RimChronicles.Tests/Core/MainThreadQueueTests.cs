using RimChronicles.Core.Engine;

namespace RimChronicles.Tests.Core;

public class MainThreadQueueTests
{
    [Fact]
    public void Drain_RunsWorkInPostedOrder()
    {
        var queue = new MainThreadQueue();
        var seen = new List<int>();
        queue.Post(() => seen.Add(1));
        queue.Post(() => seen.Add(2));
        queue.Post(() => seen.Add(3));

        var ran = queue.Drain(_ => { });

        Assert.Equal(3, ran);
        Assert.Equal([1, 2, 3], seen);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Drain_ReportsAThrowingItemAndKeepsGoing()
    {
        var queue = new MainThreadQueue();
        var errors = new List<Exception>();
        var after = false;
        queue.Post(() => throw new InvalidOperationException("boom"));
        queue.Post(() => after = true);

        queue.Drain(errors.Add);

        Assert.Single(errors);
        Assert.True(after);
    }

    [Fact]
    public void Drain_StopsAtMaxAndLeavesTheRest()
    {
        var queue = new MainThreadQueue();
        for (var i = 0; i < 5; i++)
        {
            queue.Post(() => { });
        }

        Assert.Equal(2, queue.Drain(_ => { }, max: 2));
        Assert.Equal(3, queue.Count);
    }

    [Fact]
    public async Task Post_IsSafeFromManyThreads()
    {
        var queue = new MainThreadQueue();
        var count = 0;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                queue.Post(() => count++);
            }
        })));

        queue.Drain(_ => { });

        Assert.Equal(8000, count);
    }

    [Fact]
    public void Drain_RunsWorkPostedDuringTheDrainOnTheSamePass()
    {
        var queue = new MainThreadQueue();
        var second = false;
        queue.Post(() => queue.Post(() => second = true));

        queue.Drain(_ => { });

        Assert.True(second);
    }
}
