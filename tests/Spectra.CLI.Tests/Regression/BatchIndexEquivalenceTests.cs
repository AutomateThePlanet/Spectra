using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Regression;

/// <summary>
/// Spec 049 FR-009 / SC-005 regression guard. After the batch flow was
/// refactored to call <see cref="TestPersistenceService"/>, the per-batch
/// _index.json must remain semantically equivalent to today's output for the
/// same inputs (same id/title/priority/tags/component/file shape, id-sorted,
/// lowercase priorities). Asserts structurally rather than via JSON string
/// snapshot to remain robust against the per-write <c>GeneratedAt</c> field.
/// </summary>
public sealed class BatchIndexEquivalenceTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;

    public BatchIndexEquivalenceTests()
    {
        _root = Directory.CreateTempSubdirectory("spectra-batchindex-").FullName;
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task Batch_StillIndexes_AfterRefactor()
    {
        const string suite = "smoke";
        var batch = new[]
        {
            MakeTest("TC-001", "Login happy path", Priority.High,
                tags: ["smoke", "auth"], component: "auth", criteria: ["AC-001"]),
            MakeTest("TC-002", "Logout", Priority.Medium,
                tags: ["regression"], component: "auth"),
            MakeTest("TC-003", "Password reset email", Priority.Low,
                tags: ["regression", "email"], component: "auth"),
            MakeTest("TC-004", "Add to cart", Priority.High,
                tags: ["smoke", "checkout"], component: "checkout"),
            MakeTest("TC-005", "Apply discount", Priority.Medium,
                tags: ["regression"], component: "checkout"),
            MakeTest("TC-006", "Pay with card", Priority.High,
                tags: ["smoke", "payment"], component: "payment", criteria: ["AC-010", "AC-011"]),
            MakeTest("TC-007", "Confirm order", Priority.Medium,
                tags: ["smoke"], component: "checkout"),
        };

        var persistence = new TestPersistenceService(
            new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        await persistence.PersistAsync(_testsPath, suite, batch, batch, CancellationToken.None);

        var indexPath = Path.Combine(_testsPath, suite, "_index.json");
        Assert.True(File.Exists(indexPath));
        var index = JsonSerializer.Deserialize<MetadataIndex>(
            await File.ReadAllTextAsync(indexPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(index);
        Assert.Equal(suite, index!.Suite);
        Assert.Equal(batch.Length, index.Tests.Count);

        // Id-sorted ascending.
        Assert.Equal(
            batch.Select(t => t.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            index.Tests.Select(e => e.Id).ToArray());

        // Per-entry field shape preserved.
        foreach (var t in batch)
        {
            var entry = index.Tests.Single(e => e.Id == t.Id);
            Assert.Equal(t.Title, entry.Title);
            Assert.Equal(t.Priority.ToString().ToLowerInvariant(), entry.Priority);
            Assert.Equal(t.Tags.ToList(), entry.Tags.ToList());
            Assert.Equal(t.Component, entry.Component);
            Assert.Equal(t.FilePath, entry.File);   // shape preserved: "{id}.md"
            Assert.Equal(t.Criteria.ToList(), entry.Criteria.ToList());

            // Priority is always a lowercase string in the index.
            Assert.Equal(entry.Priority, entry.Priority.ToLowerInvariant());
        }
    }

    private static TestCase MakeTest(
        string id,
        string title,
        Priority priority,
        IReadOnlyList<string>? tags = null,
        string? component = null,
        IReadOnlyList<string>? criteria = null)
        => new()
        {
            Id = id,
            Title = title,
            Priority = priority,
            Tags = tags ?? [],
            Component = component,
            Criteria = criteria ?? [],
            Steps = ["Step 1", "Step 2"],
            ExpectedResult = "Expected",
            // Pre-refactor batch shape: GenerationAgent sets FilePath = "{id}.md".
            FilePath = $"{id}.md",
        };
}
