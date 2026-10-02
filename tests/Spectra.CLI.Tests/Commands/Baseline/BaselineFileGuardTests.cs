using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Dashboard;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 084 T8 (FR-011): <c>_source-baseline.json</c> (and the snapshots) are never read as a test, an index, a suite or
/// a parse error by any command that enumerates suite folders or suite files. Each command's output is the same with
/// and without the baseline files; suite rename moves the file, suite delete removes it.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class BaselineFileGuardTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public BaselineFileGuardTests()
    {
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md"]);
        _ws.WriteTest("billing", "TC-201", ["docs/storage.md#Quotas"]);
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>Every command that enumerates suite folders or suite files.</summary>
    public static TheoryData<string> Commands => new()
    {
        "validate --output-format json",
        "index --rebuild --output-format json",
        "list --output-format json",
        "show TC-101 --output-format json",
        "ai analyze --coverage --format json --output-format json",
        "ai review-flagged --no-interaction --output-format json",
        "suite list --output-format json",
        "doctor ids --no-interaction --output-format json",
        "run list-suites --output-format json",
        "ai update-triage --all-suites --no-auto-ack --dry-run --output-format json",
    };

    private string Normalise(string text)
    {
        var root = _ws.Root;
        text = text.Replace(root, "<root>").Replace(root.Replace('\\', '/'), "<root>").Replace(root.Replace("\\", "\\\\"), "<root>");
        text = Regex.Replace(text, @"\d{4}-\d{2}-\d{2}[T ][\d:.]+(Z|[+-]\d{2}:\d{2})?", "<time>");
        text = Regex.Replace(text, @"\b\d+(\.\d+)?\s?(ms|s)\b", "<duration>");
        return text;
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task Output_IsTheSame_WithAndWithoutTheBaselineFiles(string command)
    {
        var args = command.Split(' ');
        var without = await _ws.RunAsync(args);

        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        Assert.True(File.Exists(_ws.BaselinePath("storage")));
        Assert.NotEmpty(_ws.SnapshotFiles());

        var with = await _ws.RunAsync(args);

        Assert.Equal(without.Exit, with.Exit);
        if (!command.StartsWith("ai update-triage", StringComparison.Ordinal))
        {
            Assert.Equal(Normalise(without.Out), Normalise(with.Out));
            Assert.Equal(Normalise(without.Err), Normalise(with.Err));
        }

        Assert.DoesNotContain("_source-baseline", with.Out + with.Err);
        Assert.DoesNotContain("source-snapshots", with.Out + with.Err);
    }

    [Fact]
    public async Task Dashboard_DataIsTheSame_WithAndWithoutTheBaselineFiles()
    {
        var without = JsonSerializer.Serialize((await new DataCollector(_ws.Root).CollectAsync()).Suites);

        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");

        var with = JsonSerializer.Serialize((await new DataCollector(_ws.Root).CollectAsync()).Suites);
        Assert.Equal(Normalise(without), Normalise(with));
    }

    [Fact]
    public async Task SuiteRename_MovesTheBaseline_AndSuiteDelete_RemovesIt()
    {
        await _ws.RunJsonAsync("ai", "source-baseline", "--init", "--all-suites");
        var content = _ws.ReadBaseline("storage");

        var rename = await _ws.RunAsync("suite", "rename", "storage", "archive", "--no-interaction", "--output-format", "json");
        Assert.True(rename.Exit == 0, rename.Out + rename.Err);
        Assert.Equal(content, _ws.ReadBaseline("archive"));
        Assert.Null(_ws.ReadBaseline("storage"));

        var delete = await _ws.RunAsync("suite", "delete", "archive", "--force", "--no-interaction", "--output-format", "json");
        Assert.True(delete.Exit == 0, delete.Out + delete.Err);
        Assert.False(Directory.Exists(_ws.PathOf("test-cases/archive")));
    }
}
