using System.Diagnostics;
using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class CopilotUsageObserverTests
{
    [Fact]
    public void GetOrEstimate_NoUsage_ReturnsEstimateAndFlag()
    {
        var observer = new CopilotUsageObserver();
        Assert.False(observer.UsageReceived);

        // prompt = 40 chars → 10 tokens; response = 8 chars → 2 tokens
        var (pt, cot, estimated) = observer.GetOrEstimate(new string('a', 40), "response");
        Assert.True(estimated);
        Assert.Equal(10, pt);
        Assert.Equal(2, cot);
    }

    [Fact]
    public void GetOrEstimate_UsageReceived_ReturnsObservedValues()
    {
        var observer = new CopilotUsageObserver();
        observer.RecordUsage(1000, 500);

        var (pt, cot, estimated) = observer.GetOrEstimate("prompt", "response");
        Assert.False(estimated);
        Assert.Equal(1000, pt);
        Assert.Equal(500, cot);
    }

    [Fact]
    public void RecordUsage_MultipleInvocations_AccumulatesTokens()
    {
        var observer = new CopilotUsageObserver();
        observer.RecordUsage(100, 50);
        observer.RecordUsage(200, 100);
        observer.RecordUsage(300, 150);

        var (pt, cot, estimated) = observer.GetOrEstimate("x", "y");
        Assert.False(estimated);
        Assert.Equal(600, pt);
        Assert.Equal(300, cot);
    }

    [Fact]
    public void RecordUsage_ConcurrentInvocations_ThreadSafe()
    {
        var observer = new CopilotUsageObserver();
        const int threads = 8;
        const int perThread = 100;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < perThread; i++)
            {
                observer.RecordUsage(10, 5);
            }
        });

        var (pt, cot, _) = observer.GetOrEstimate("", "");
        Assert.Equal(threads * perThread * 10, pt);
        Assert.Equal(threads * perThread * 5, cot);
    }

    [Fact]
    public async Task WaitForUsageAsync_AlreadyReceived_ReturnsImmediately()
    {
        var observer = new CopilotUsageObserver();
        observer.RecordUsage(100, 50);

        var sw = Stopwatch.StartNew();
        var result = await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        sw.Stop();

        Assert.True(result);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Expected immediate return, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task WaitForUsageAsync_TimeoutBeforeArrival_ReturnsFalse()
    {
        var observer = new CopilotUsageObserver();
        var result = await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.False(result);
        Assert.False(observer.UsageReceived);
    }

    [Fact]
    public async Task WaitForUsageAsync_ArrivesBeforeTimeout_ReturnsTrue()
    {
        var observer = new CopilotUsageObserver();

        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            observer.RecordUsage(1234, 567);
        });

        var result = await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
        Assert.True(result);
        Assert.True(observer.UsageReceived);
        Assert.Equal(1234, observer.ObservedPromptTokens);
        Assert.Equal(567, observer.ObservedCompletionTokens);
    }

    [Fact]
    public async Task WaitForUsageAsync_ZeroGrace_ReturnsFalseWhenNotReceived()
    {
        var observer = new CopilotUsageObserver();
        var result = await observer.WaitForUsageAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.False(result);
    }
}
