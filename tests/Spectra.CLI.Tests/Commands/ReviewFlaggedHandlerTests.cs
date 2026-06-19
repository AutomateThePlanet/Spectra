using Spectra.CLI.Commands.Review;
using Spectra.CLI.IO;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands;

public class ReviewFlaggedHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _testsDir;
    private ReviewFlaggedHandler? _handler;

    public ReviewFlaggedHandlerTests()
    {
        _testsDir = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_testsDir);
        _handler = new ReviewFlaggedHandler(_root, "test-cases");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private async Task<string> WriteFlaggedTestAsync(string suite, string id, int repairAttempts = 1)
    {
        var suiteDir = Path.Combine(_testsDir, suite);
        Directory.CreateDirectory(suiteDir);
        var file = Path.Combine(suiteDir, $"{id}.md");
        await File.WriteAllTextAsync(file, $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            grounding:
              verdict: partial
              score: 0.72
              generator: claude-code-session
              critic: claude-sonnet-4-6
              verified_at: 2026-06-19T10:00:00Z
              unverified_claims:
                - "Step 2: conversion factor not in docs"
              flagged_for_review: true
              repair_attempts: {repairAttempts}
              condensed_findings:
                - element: "Step 2"
                  reason: "Conversion factor not found in docs"
            ---

            # Test {id}

            ## Steps

            1. Step one
            2. Step two

            ## Expected Result

            Expected result here
            """);
        return file;
    }

    private async Task<string> WriteGroundedTestAsync(string suite, string id)
    {
        var suiteDir = Path.Combine(_testsDir, suite);
        Directory.CreateDirectory(suiteDir);
        var file = Path.Combine(suiteDir, $"{id}.md");
        await File.WriteAllTextAsync(file, $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            grounding:
              verdict: grounded
              score: 0.95
              generator: claude-code-session
              critic: claude-sonnet-4-6
              verified_at: 2026-06-19T10:00:00Z
            ---

            # Test {id}

            ## Steps

            1. Step one

            ## Expected Result

            Expected result here
            """);
        return file;
    }

    // --- FindFlaggedAsync ---

    [Fact]
    public async Task FindFlaggedAsync_NoSuites_ReturnsEmpty()
    {
        var result = await _handler!.FindFlaggedAsync(null, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindFlaggedAsync_AllSuites_ReturnsFlaggedOnly()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        await WriteGroundedTestAsync("suite-a", "TC-114");

        var result = await _handler!.FindFlaggedAsync(null, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("TC-113", result[0].Id);
        Assert.Equal("suite-a", result[0].Suite);
    }

    [Fact]
    public async Task FindFlaggedAsync_SuiteFilter_OnlyScansThatSuite()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        await WriteFlaggedTestAsync("suite-b", "TC-200");

        var result = await _handler!.FindFlaggedAsync("suite-a", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("TC-113", result[0].Id);
    }

    [Fact]
    public async Task FindFlaggedAsync_PopulatesCondensedFindings()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        var result = await _handler!.FindFlaggedAsync(null, CancellationToken.None);

        Assert.Single(result);
        Assert.NotEmpty(result[0].CondensedFindings);
        Assert.Equal("Step 2", result[0].CondensedFindings[0].Element);
    }

    [Fact]
    public async Task FindFlaggedAsync_PopulatesRepairAttempts()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113", repairAttempts: 1);
        var result = await _handler!.FindFlaggedAsync(null, CancellationToken.None);

        Assert.Equal(1, result[0].RepairAttempts);
    }

    // --- AcceptAsync ---

    [Fact]
    public async Task AcceptAsync_ClearsFlaggedForReview()
    {
        var filePath = await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];

        var success = await _handler.AcceptAsync(test, CancellationToken.None);

        Assert.True(success);
        var content = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain("flagged_for_review: true", content);
    }

    [Fact]
    public async Task AcceptAsync_KeepsPartialVerdict()
    {
        var filePath = await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];

        await _handler.AcceptAsync(test, CancellationToken.None);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("verdict: partial", content);
        Assert.Contains("score: 0.72", content);
    }

    [Fact]
    public async Task AcceptAsync_KeepsCondensedFindings()
    {
        var filePath = await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];

        await _handler.AcceptAsync(test, CancellationToken.None);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("condensed_findings:", content);
    }

    [Fact]
    public async Task AcceptAsync_AfterAccept_NoLongerAppearsInFlagged()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];
        await _handler.AcceptAsync(test, CancellationToken.None);

        var remaining = await _handler.FindFlaggedAsync(null, CancellationToken.None);
        Assert.Empty(remaining);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_RemovesTestFile()
    {
        var filePath = await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];

        await _handler.DeleteAsync(test, CancellationToken.None);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteAsync_WritesDropTrail()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];

        await _handler.DeleteAsync(test, CancellationToken.None);

        var trailPath = Path.Combine(_root, ".spectra", "dropped-tests.json");
        Assert.True(File.Exists(trailPath));
        var content = await File.ReadAllTextAsync(trailPath);
        Assert.Contains("TC-113", content);
        Assert.Contains("user_decided", content);
        Assert.Contains("review", content);
    }

    [Fact]
    public async Task DeleteAsync_AfterDelete_NoLongerAppearsInFlagged()
    {
        await WriteFlaggedTestAsync("suite-a", "TC-113");
        var test = (await _handler!.FindFlaggedAsync(null, CancellationToken.None))[0];
        await _handler.DeleteAsync(test, CancellationToken.None);

        var remaining = await _handler.FindFlaggedAsync(null, CancellationToken.None);
        Assert.Empty(remaining);
    }
}
