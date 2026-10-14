using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 097 F1, CLI side (SC-001): with <c>--ids</c>, the grounding batch writes blocks only for the named tests, even
/// when another test has a fresh <c>grounded</c> verdict on disk. A guard (passes on 807047c: 091's <c>--ids</c> already
/// filters) pinning the behaviour the skills now rely on. Workspace from the real <c>spectra init</c>.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GroundingIdsBatchTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public GroundingIdsBatchTests()
    {
        _ws.InitWithRealTemplateAsync().GetAwaiter().GetResult();
        _ws.TwoSuites();
    }

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task OnlyTheNamedTests_GetBlocks()
    {
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            var (code, _, err) = await _ws.CompileCriticAsync("a", id);
            Assert.True(code == 0, err);
            _ws.WriteVerdict(id, "grounded");
        }

        var tc103 = File.ReadAllBytes(_ws.TestPath("a", "TC-103"));

        var (exit, output, error) = await _ws.IngestGroundingAsync("--suite", "a", "--all", "--ids", "TC-101,TC-102");

        Assert.True(exit == 0, error);
        Assert.Equal(2, VerdictWorkspace.Json(output).GetProperty("written").GetInt32());
        Assert.NotNull(_ws.GroundingOf("a", "TC-101"));
        Assert.NotNull(_ws.GroundingOf("a", "TC-102"));
        Assert.Equal(tc103, File.ReadAllBytes(_ws.TestPath("a", "TC-103")));
    }
}
