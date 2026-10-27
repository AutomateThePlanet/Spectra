using System.Text.Json;
using Spectra.CLI.Extraction;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 US2 (FR-007–FR-010): IDs come from the committed per-prefix high-water mark, are unique across docs,
/// are never reused, survive a fresh state that only has the committed index, and concurrent ingests are safe.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestCriteriaUniquenessTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public IngestCriteriaUniquenessTests()
    {
        _ws.WriteDoc("docs/checkout.md", "# Checkout\n");
        _ws.WriteDoc("docs/a/login.md", "# Login A\n");
        _ws.WriteDoc("docs/b/login.md", "# Login B\n");
    }

    public void Dispose() => _ws.Dispose();

    private static object Item(string text) => new { text, rfc2119 = "MUST", priority = "high" };

    private static IReadOnlyList<string> Ids(string stdout) =>
        CriteriaTestWorkspace.Json(stdout).GetProperty("ids").EnumerateArray().Select(e => e.GetString()!).ToList();

    [Fact]
    public async Task DroppingTheHighestId_ThenAdding_IssuesAHigherNumber()
    {
        await _ws.IngestAsync("docs/checkout.md", Item("one"), Item("two"), Item("three"));

        // 003 (the highest) is dropped; a new criterion is added.
        var (exit, output, err) = await _ws.IngestAsync("docs/checkout.md", Item("one"), Item("two"), Item("four"));

        Assert.True(exit == 0, err);
        Assert.Equal(["AC-CHECKOUT-001", "AC-CHECKOUT-002", "AC-CHECKOUT-004"], Ids(output));
        Assert.Equal(4, (await _ws.ReadIndexAsync()).IdHighWaterMarks!["AC-CHECKOUT"]);
    }

    [Fact]
    public async Task OnlyTheCommittedIndexLeft_NumberingStillContinues()
    {
        await _ws.IngestAsync("docs/checkout.md", Item("one"), Item("two"), Item("three"));
        await _ws.IngestAsync("docs/checkout.md", Item("one"), Item("two"), Item("four"));   // mark = 4

        // Simulate a fresh state: the criteria file is gone, only the committed index remains.
        File.Delete(_ws.PathOf("docs/criteria/docs/checkout.criteria.yaml"));

        var (_, output, _) = await _ws.IngestAsync("docs/checkout.md", Item("brand new"));

        Assert.Equal(["AC-CHECKOUT-005"], Ids(output));
    }

    [Fact]
    public async Task TwoDocsWithTheSameComponent_GetDisjointIds()
    {
        var (_, a, _) = await _ws.IngestAsync("docs/a/login.md", Item("a1"), Item("a2"));
        var (_, b, _) = await _ws.IngestAsync("docs/b/login.md", Item("b1"), Item("b2"));

        Assert.Empty(Ids(a).Intersect(Ids(b)));
        Assert.Equal(["AC-LOGIN-003", "AC-LOGIN-004"], Ids(b));
    }

    [Fact]
    public async Task ConcurrentIngests_LoseNoIndexUpdate_AndShareNoId()
    {
        // Call the ingestor directly with an explicit workspace root: the CLI harness changes the process's
        // working directory and so cannot run two commands at once.
        var config = JsonSerializer.Deserialize<SpectraConfig>(
            await File.ReadAllTextAsync(_ws.PathOf("spectra.config.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        string Response(string prefix) => JsonSerializer.Serialize(
            Enumerable.Range(1, 5).Select(i => Item($"{prefix} criterion {i}")));

        var results = await Task.WhenAll(
            Task.Run(() => new CriteriaIngestor(config).IngestAsync(Response("a"), _ws.Root, "docs/a/login.md", "login", "h1", false)),
            Task.Run(() => new CriteriaIngestor(config).IngestAsync(Response("b"), _ws.Root, "docs/b/login.md", "login", "h2", false)),
            Task.Run(() => new CriteriaIngestor(config).IngestAsync(Response("c"), _ws.Root, "docs/checkout.md", "login", "h3", false)));

        Assert.All(results, r => Assert.True(r.IsSuccess, string.Join("; ", r.Errors)));
        var allIds = results.SelectMany(r => r.PersistedCriteria.Select(c => c.Id)).ToList();
        Assert.Equal(15, allIds.Distinct().Count());

        var index = await _ws.ReadIndexAsync();
        Assert.Equal(3, index.Sources.Count);
        Assert.Equal(15, index.IdHighWaterMarks!["AC-LOGIN"]);
    }

    [Fact]
    public async Task ExistingDuplicateIds_AreReported_NotRenumbered()
    {
        _ws.WriteCriteriaFile("docs/a/login.criteria.yaml", "criteria:\n- id: AC-LOGIN-001\n  text: a\n");
        _ws.WriteCriteriaFile("docs/b/login.criteria.yaml", "criteria:\n- id: AC-LOGIN-001\n  text: b\n");

        var (exit, output, err) = await _ws.IngestAsync("docs/checkout.md", Item("one"));

        Assert.Equal(0, exit);
        var dup = Assert.Single(CriteriaTestWorkspace.Json(output).GetProperty("duplicate_ids").EnumerateArray());
        Assert.Equal("AC-LOGIN-001", dup.GetProperty("id").GetString());
        Assert.Equal(2, dup.GetProperty("files").GetArrayLength());
        Assert.Contains("AC-LOGIN-001 is defined more than once", err);
        Assert.Equal("AC-LOGIN-001", (await _ws.ReadCriteriaAsync("docs/b/login.criteria.yaml"))[0].Id);
    }
}
