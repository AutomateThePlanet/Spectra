using System.Net;
using Spectra.CLI.Services;
using Xunit;

namespace Spectra.CLI.Tests.Services;

public class RunErrorTrackerTests
{
    [Fact]
    public void New_BothCountersZero()
    {
        var t = new RunErrorTracker();
        Assert.Equal(0, t.Errors);
        Assert.Equal(0, t.RateLimits);
    }

    [Fact]
    public void RecordError_BumpsErrorsOnly()
    {
        var t = new RunErrorTracker();
        t.RecordError();
        t.RecordError();
        Assert.Equal(2, t.Errors);
        Assert.Equal(0, t.RateLimits);
    }

    [Fact]
    public void Record_GenericException_BumpsErrorsOnly()
    {
        var t = new RunErrorTracker();
        t.Record(new InvalidOperationException("boom"));
        Assert.Equal(1, t.Errors);
        Assert.Equal(0, t.RateLimits);
    }

    [Fact]
    public void Record_RateLimitException_BumpsBothCounters()
    {
        var t = new RunErrorTracker();
        t.Record(new HttpRequestException("429", null, HttpStatusCode.TooManyRequests));
        Assert.Equal(1, t.Errors);
        Assert.Equal(1, t.RateLimits);
    }

    [Fact]
    public async Task Record_ConcurrentCalls_AreThreadSafe()
    {
        var t = new RunErrorTracker();
        var tasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() =>
        {
            if (i % 3 == 0)
                t.Record(new HttpRequestException("429", null, HttpStatusCode.TooManyRequests));
            else
                t.Record(new InvalidOperationException("x"));
        })).ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1000, t.Errors);
        // ~334 multiples of 3 in [0, 1000)
        Assert.Equal(Enumerable.Range(0, 1000).Count(i => i % 3 == 0), t.RateLimits);
    }
}
