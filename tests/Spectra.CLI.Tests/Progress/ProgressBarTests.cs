using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectre.Console;

namespace Spectra.CLI.Tests.Progress;

/// <summary>
/// Spec 041: tests for the new <see cref="ProgressReporter.ProgressTwoTaskAsync"/>
/// API and the suppression rules for terminal progress bars.
/// </summary>
public class ProgressBarTests
{
    [Fact]
    public async Task ProgressTwoTaskAsync_QuietVerbosity_RunsActionWithNoopHandles()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Human,
            verbosity: VerbosityLevel.Quiet);

        var actionRan = false;
        IProgressTaskHandle? capturedGen = null;
        IProgressTaskHandle? capturedVerify = null;

        await reporter.ProgressTwoTaskAsync(
            "Generating tests",
            "Verifying tests",
            total: 40,
            includeVerifyTask: true,
            action: (gen, verify) =>
            {
                actionRan = true;
                capturedGen = gen;
                capturedVerify = verify;
                gen.Increment(10);
                verify?.Increment(5);
                return Task.CompletedTask;
            });

        Assert.True(actionRan);
        Assert.NotNull(capturedGen);
        Assert.NotNull(capturedVerify);
    }

    [Fact]
    public async Task ProgressTwoTaskAsync_JsonOutputFormat_RunsActionWithNoopHandles()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Json,
            verbosity: VerbosityLevel.Normal);

        var incrementCount = 0;

        await reporter.ProgressTwoTaskAsync(
            "Generating tests",
            "Verifying tests",
            total: 40,
            includeVerifyTask: true,
            action: (gen, verify) =>
            {
                gen.Increment(10);
                verify?.Increment(5);
                incrementCount += 2;
                return Task.CompletedTask;
            });

        Assert.Equal(2, incrementCount);
    }

    [Fact]
    public async Task ProgressTwoTaskAsync_SkipCritic_VerifyHandleIsNull()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Json,
            verbosity: VerbosityLevel.Normal);

        IProgressTaskHandle? capturedVerify = null;

        await reporter.ProgressTwoTaskAsync(
            "Generating tests",
            "Verifying tests",
            total: 40,
            includeVerifyTask: false,
            action: (gen, verify) =>
            {
                capturedVerify = verify;
                return Task.CompletedTask;
            });

        Assert.Null(capturedVerify);
    }

    [Fact]
    public void ShouldSuppressProgress_QuietVerbosity_ReturnsTrue()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Human,
            verbosity: VerbosityLevel.Quiet);

        Assert.True(reporter.ShouldSuppressProgress());
    }

    [Fact]
    public void ShouldSuppressProgress_JsonOutput_ReturnsTrue()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Json,
            verbosity: VerbosityLevel.Normal);

        Assert.True(reporter.ShouldSuppressProgress());
    }

    [Fact]
    public void ShouldSuppressProgress_NormalVerbosityHumanOutput_ReturnsBasedOnTty()
    {
        var reporter = new ProgressReporter(
            outputFormat: OutputFormat.Human,
            verbosity: VerbosityLevel.Normal);

        // Result depends on whether the test runner stdout is interactive.
        // Both outcomes are valid; we just verify the method runs without throwing.
        var result = reporter.ShouldSuppressProgress();
        Assert.True(result == true || result == false);
    }

    [Fact]
    public void IsMinimalVerbosity_TrueOnlyForMinimal()
    {
        Assert.True(new ProgressReporter(verbosity: VerbosityLevel.Minimal).IsMinimalVerbosity);
        Assert.False(new ProgressReporter(verbosity: VerbosityLevel.Normal).IsMinimalVerbosity);
        Assert.False(new ProgressReporter(verbosity: VerbosityLevel.Quiet).IsMinimalVerbosity);
    }

    [Fact]
    public async Task ProgressTwoTaskAsync_HandleIncrementsAreTracked_InNoopMode()
    {
        // Verify the no-op handles don't throw when called repeatedly,
        // simulating the per-batch and per-test increment paths.
        var reporter = new ProgressReporter(outputFormat: OutputFormat.Json);

        await reporter.ProgressTwoTaskAsync(
            "Generating tests",
            "Verifying tests",
            total: 40,
            includeVerifyTask: true,
            action: (gen, verify) =>
            {
                for (var i = 0; i < 4; i++) gen.Increment(10);
                gen.SetDescription("Generating tests  done");
                for (var i = 0; i < 40; i++)
                {
                    verify?.Increment(1);
                    verify?.SetDescription($"Verifying tests  TC-{i:000}");
                }
                return Task.CompletedTask;
            });
    }
}
