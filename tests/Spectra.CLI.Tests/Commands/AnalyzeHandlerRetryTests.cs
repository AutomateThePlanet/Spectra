using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Commands.Analyze;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 047 test plan rows 8–12. Exercises the bounded-retry helper
/// (<see cref="AnalyzeHandler.ExtractWithRetryAsync"/>) and the cacheability
/// gate that the per-doc loop in <c>RunExtractCriteriaAsync</c> applies after
/// the helper returns. End-to-end tests against the full per-doc loop are
/// not run here because the Copilot SDK has no in-process stubbing seam;
/// what we verify is the contract those callers rely on.
/// </summary>
public class AnalyzeHandlerRetryTests
{
    /// <summary>
    /// Test-only delay provider: records each requested delay and returns
    /// immediately, so retry-path tests don't wait real wall-clock time.
    /// </summary>
    private sealed class NoOpDelayProvider : IExtractionDelayProvider
    {
        public readonly List<TimeSpan> Calls = new();

        public Task DelayAsync(TimeSpan duration, CancellationToken ct)
        {
            Calls.Add(duration);
            return Task.CompletedTask;
        }
    }

    private static CriteriaExtractionResult Extracted(int count = 0)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => new AcceptanceCriterion { Id = $"AC-{i:D3}", Text = $"Criterion {i}" })
            .ToList();
        return new CriteriaExtractionResult(ExtractionOutcome.Extracted, items);
    }

    private static CriteriaExtractionResult ParseFail() =>
        new(ExtractionOutcome.ParseFailure, Array.Empty<AcceptanceCriterion>());

    private static CriteriaExtractionResult EmptyResp() =>
        new(ExtractionOutcome.EmptyResponse, Array.Empty<AcceptanceCriterion>());

    // ── Retry_* (test plan rows 11–12) ────────────────────────────────────

    [Fact]
    public async Task Retry_TransientParseFailThenSuccess_Caches()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        var result = await AnalyzeHandler.ExtractWithRetryAsync(
            extractAttempt: _ =>
            {
                attempts++;
                return Task.FromResult(attempts == 1 ? ParseFail() : Extracted(count: 3));
            },
            maxAttempts: 2,
            backoff: TimeSpan.FromMilliseconds(1500),
            delayProvider: delay,
            ct: CancellationToken.None);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.True(result.IsCacheable);
        Assert.Equal(3, result.Criteria.Count);
        Assert.Equal(2, attempts);
        Assert.Single(delay.Calls);                                    // exactly one backoff before the retry
        Assert.Equal(TimeSpan.FromMilliseconds(1500), delay.Calls[0]);
    }

    [Fact]
    public async Task Retry_BoundedToTwoAttempts()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        var result = await AnalyzeHandler.ExtractWithRetryAsync(
            extractAttempt: _ =>
            {
                attempts++;
                return Task.FromResult(ParseFail());
            },
            maxAttempts: 2,
            backoff: TimeSpan.FromMilliseconds(1500),
            delayProvider: delay,
            ct: CancellationToken.None);

        Assert.Equal(ExtractionOutcome.ParseFailure, result.Outcome);
        Assert.False(result.IsCacheable);
        Assert.Equal(2, attempts);                                     // exactly maxAttempts, not 3, not 5
        Assert.Single(delay.Calls);                                    // one delay between the two attempts; no trailing delay
    }

    [Fact]
    public async Task Retry_EmptyResponseThenSuccess_Caches()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        var result = await AnalyzeHandler.ExtractWithRetryAsync(
            extractAttempt: _ =>
            {
                attempts++;
                return Task.FromResult(attempts == 1 ? EmptyResp() : Extracted(count: 1));
            },
            maxAttempts: 2,
            backoff: TimeSpan.FromMilliseconds(50),
            delayProvider: delay,
            ct: CancellationToken.None);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Retry_ExtractedFirstAttempt_NoRetry()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        var result = await AnalyzeHandler.ExtractWithRetryAsync(
            extractAttempt: _ =>
            {
                attempts++;
                return Task.FromResult(Extracted(count: 5));
            },
            maxAttempts: 2,
            backoff: TimeSpan.FromMilliseconds(1500),
            delayProvider: delay,
            ct: CancellationToken.None);

        Assert.Equal(ExtractionOutcome.Extracted, result.Outcome);
        Assert.Equal(1, attempts);                                     // Extracted is final by construction
        Assert.Empty(delay.Calls);                                     // no delay because no retry
    }

    [Fact]
    public async Task Retry_ThrownException_PropagatesWithoutRetry()
    {
        var attempts = 0;
        var delay = new NoOpDelayProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await AnalyzeHandler.ExtractWithRetryAsync(
                extractAttempt: _ =>
                {
                    attempts++;
                    throw new InvalidOperationException("transport error");
                },
                maxAttempts: 2,
                backoff: TimeSpan.FromMilliseconds(1500),
                delayProvider: delay,
                ct: CancellationToken.None);
        });

        Assert.Equal(1, attempts);                                     // exceptions are NOT retried per FR-005
        Assert.Empty(delay.Calls);
    }

    // ── Caller_* (test plan rows 8–10) ────────────────────────────────────
    //
    // The caller's per-doc loop in RunExtractCriteriaAsync gates the cache
    // write on result.IsCacheable. The IsCacheable contract is exercised
    // exhaustively below; the loop's branching on that flag is a direct
    // pattern (`if (!result.IsCacheable) { failedDocuments.Add; continue; }`)
    // and is observed via the integration smoke checks in quickstart.md §2.

    [Fact]
    public void Caller_ParseFailure_IsNotCacheable()
    {
        // After retries exhausted, ParseFailure must NOT write the cache hash.
        // The per-doc loop checks IsCacheable; ParseFailure returns false.
        var failed = ParseFail();
        Assert.False(failed.IsCacheable);
    }

    [Fact]
    public void Caller_RealEmpty_IsCacheable()
    {
        // A genuine empty result MUST be cacheable so the next run skips the doc.
        var realEmpty = Extracted(count: 0);
        Assert.True(realEmpty.IsCacheable);
        Assert.Empty(realEmpty.Criteria);
    }

    [Fact]
    public void Caller_EmptyResponse_IsNotCacheable()
    {
        var empty = EmptyResp();
        Assert.False(empty.IsCacheable);
    }

    // ── Reporting_* (US3, test plan addendum) ─────────────────────────────

    [Fact]
    public void Reporting_NoFailures_AllSuccessExitCode()
    {
        Assert.Equal(Spectra.CLI.Infrastructure.ExitCodes.Success,
            AnalyzeHandler.ComputeExtractionExitCode(totalDocs: 5, failedDocs: 0));
    }

    [Fact]
    public void Reporting_PartialFailure_ValidationErrorExitCode()
    {
        // Non-cacheable docs after retries land here. Convention preserved
        // from before Spec 047.
        Assert.Equal(Spectra.CLI.Infrastructure.ExitCodes.ValidationError,
            AnalyzeHandler.ComputeExtractionExitCode(totalDocs: 5, failedDocs: 2));
    }

    [Fact]
    public void Reporting_AllDocsFailed_ErrorExitCode()
    {
        Assert.Equal(Spectra.CLI.Infrastructure.ExitCodes.Error,
            AnalyzeHandler.ComputeExtractionExitCode(totalDocs: 5, failedDocs: 5));
    }

    [Fact]
    public void Reporting_NoDocuments_AllSuccessExitCode()
    {
        // Empty corpus → success (no failure to report).
        Assert.Equal(Spectra.CLI.Infrastructure.ExitCodes.Success,
            AnalyzeHandler.ComputeExtractionExitCode(totalDocs: 0, failedDocs: 0));
    }
}
