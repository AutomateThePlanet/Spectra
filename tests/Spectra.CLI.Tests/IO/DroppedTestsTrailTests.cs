using System.Text.Json;
using Spectra.CLI.IO;

namespace Spectra.CLI.Tests.IO;

public class DroppedTestsTrailTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public DroppedTestsTrailTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private DroppedTestEntry MakeEntry(string id = "TC-138", string reason = "hallucinated") => new()
    {
        Id = id,
        Suite = "file-management",
        Title = $"Verify {id}",
        DropReason = reason,
        ContradictingClaim = reason == "hallucinated" ? "1 KB = 1000 bytes" : null,
        DocRef = reason == "hallucinated" ? "docs/sizes.md" : null,
        CriticModel = reason == "hallucinated" ? "claude-sonnet-4-6" : null,
        Timestamp = "2026-06-19T10:00:00Z",
        Source = reason == "hallucinated" ? "critic" : "review"
    };

    [Fact]
    public async Task AppendAsync_CreatesFileOnFirstWrite()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        await trail.AppendAsync(MakeEntry(), CancellationToken.None);
        Assert.True(File.Exists(trail.TrailPath));
    }

    [Fact]
    public async Task AppendAsync_AppendsNotOverwrites_OnSubsequentCalls()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        await trail.AppendAsync(MakeEntry("TC-138"), CancellationToken.None);
        await trail.AppendAsync(MakeEntry("TC-139"), CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(trail.TrailPath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public async Task AppendAsync_EachLineIsValidJson()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        await trail.AppendAsync(MakeEntry("TC-138"), CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(trail.TrailPath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        Assert.Single(lines);
        var obj = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        Assert.Equal("TC-138", obj.GetProperty("id").GetString());
        Assert.Equal("file-management", obj.GetProperty("suite").GetString());
    }

    [Fact]
    public async Task AppendAsync_HallucinatedEntry_HasCriticFields()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        await trail.AppendAsync(MakeEntry("TC-138", "hallucinated"), CancellationToken.None);

        var content = await File.ReadAllTextAsync(trail.TrailPath);
        var obj = JsonSerializer.Deserialize<JsonElement>(content.Trim());

        Assert.Equal("hallucinated", obj.GetProperty("drop_reason").GetString());
        Assert.Equal("1 KB = 1000 bytes", obj.GetProperty("contradicting_claim").GetString());
        Assert.Equal("critic", obj.GetProperty("source").GetString());
    }

    [Fact]
    public async Task AppendAsync_UserDecidedEntry_HasNullCriticFields()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        await trail.AppendAsync(MakeEntry("TC-114", "user_decided"), CancellationToken.None);

        var content = await File.ReadAllTextAsync(trail.TrailPath);
        var obj = JsonSerializer.Deserialize<JsonElement>(content.Trim());

        Assert.Equal("user_decided", obj.GetProperty("drop_reason").GetString());
        Assert.Equal("review", obj.GetProperty("source").GetString());
        // Null fields should be absent (WhenWritingNull)
        Assert.False(obj.TryGetProperty("contradicting_claim", out _));
        Assert.False(obj.TryGetProperty("critic_model", out _));
    }

    [Fact]
    public async Task AppendAsync_ReturnsTotalEntryCount()
    {
        var trail = new DroppedTestsTrail(_tempDir);
        var count1 = await trail.AppendAsync(MakeEntry("TC-138"), CancellationToken.None);
        var count2 = await trail.AppendAsync(MakeEntry("TC-139"), CancellationToken.None);
        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
    }
}
