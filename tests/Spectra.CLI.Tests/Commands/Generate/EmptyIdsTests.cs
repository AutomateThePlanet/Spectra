using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 097 F5 (FR-016): an <c>--ids</c> value that is empty after trimming counts as not given — in
/// <c>ingest-grounding</c> and <c>ingest-verdict</c> — instead of silently processing nothing. With
/// <c>--repaired</c> it is still <c>IDS_REQUIRED</c>. On 807047c an empty list processed no test.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class EmptyIdsTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public EmptyIdsTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
        foreach (var id in new[] { "TC-102", "TC-103" })
        {
            var (code, _, err) = _ws.CompileCriticAsync("a", id).GetAwaiter().GetResult();
            Assert.True(code == 0, err);
            _ws.WriteVerdict(id, "grounded");
        }
    }

    public void Dispose() => _ws.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    public async Task IngestGrounding_WithEmptyIds_ProcessesTheWholeSuite(string ids)
    {
        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "a", "--all", "--ids", ids);

        Assert.True(exit == 0, error);
        Assert.Equal(3, VerdictWorkspace.Json(output).GetProperty("written").GetInt32()); // TC-101 (no record), TC-102, TC-103
    }

    [Fact]
    public async Task IngestGroundingRepaired_WithEmptyIds_IsStillIdsRequired()
    {
        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "a", "--all", "--repaired", "--repair-attempts", "1", "--ids", " ");

        Assert.Equal(4, exit);
        Assert.Contains("IDS_REQUIRED", output + error);
    }

    [Fact]
    public async Task IngestVerdict_WithEmptyIds_ClassifiesTheWholeSuite()
    {
        var (exit, output, error) = await _ws.RunAsync("ai", "ingest-verdict", "--suite", "a", "--all", "--ids", " ", "--output-format", "json");

        Assert.True(exit == 0, error);
        Assert.Equal(3, VerdictWorkspace.Json(output).GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task EmptyIds_WithoutAll_AreNotAUsageError()
    {
        var (_, _, error) = await _ws.IngestGroundingAsync("--suite", "a", "--test", "TC-102", "--ids", "");

        Assert.DoesNotContain("--ids is only valid with --all", error);
    }
}
