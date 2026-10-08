using Spectra.CLI.Generation;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Validation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 063 — fail-loud update boundary tests. The edit preserves the original id and manual
/// fields regardless of model output; an out-of-scope protected-field change fails loud
/// (DRIFT_DETECTED) and persists nothing. Token-free.
/// </summary>
public sealed class UpdatedTestIngestorTests : IDisposable
{
    private readonly string _root;
    private readonly string _testsPath;
    private const string Suite = "checkout";

    public UpdatedTestIngestorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "spectra-update-" + Guid.NewGuid().ToString("N"));
        _testsPath = Path.Combine(_root, "test-cases");
        Directory.CreateDirectory(_testsPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }

    private static UpdatedTestIngestor NewIngestor() =>
        new(new TestPersistenceService(new TestFileWriter(), new IndexGenerator(), new IndexWriter()));

    private static TestCase Original(
        string id = "TC-100",
        Priority priority = Priority.High,
        string? component = null,
        IReadOnlyList<string>? tags = null,
        GroundingMetadata? grounding = null) => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Title = "Original title",
        Priority = priority,
        Component = component,
        Tags = tags ?? [],
        Steps = ["original step"],
        ExpectedResult = "original result",
        Grounding = grounding
    };

    private static GroundingMetadata ManualGrounding() => new()
    {
        Verdict = VerificationVerdict.Manual,
        Score = 1.0,
        Generator = "user",
        Critic = "none",
        VerifiedAt = DateTimeOffset.UnixEpoch
    };

    private static string EditJson(
        string id = "TC-100",
        string title = "Edited title",
        string priority = "high",
        string? component = null,
        string[]? tags = null,
        string steps = "edited step",
        string expected = "edited result")
    {
        var tagsJson = tags is null ? "[]" : "[" + string.Join(", ", tags.Select(t => $"\"{t}\"")) + "]";
        var componentJson = component is null ? "null" : $"\"{component}\"";
        return $$"""
            [
              {
                "id": "{{id}}",
                "title": "{{title}}",
                "priority": "{{priority}}",
                "component": {{componentJson}},
                "tags": {{tagsJson}},
                "steps": ["{{steps}}"],
                "expected_result": "{{expected}}"
              }
            ]
            """;
    }

    // ---------- US1: edit, not regenerate; id preserved (T009) ----------

    [Fact]
    public async Task Ingest_ValidEdit_Persists_AndPreservesOriginalId_EvenWhenModelChangesId()
    {
        var original = Original(id: "TC-100");
        // Model returns a DIFFERENT id — must be ignored.
        var content = EditJson(id: "TC-999", title: "Edited title");

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.True(result.IsSuccess);
        Assert.Single(result.PersistedTests);
        Assert.Equal("TC-100", result.PersistedTests[0].Id);          // id from original, not "TC-999"
        Assert.Equal("Edited title", result.PersistedTests[0].Title); // content edited

        var suiteDir = Path.Combine(_testsPath, Suite);
        Assert.True(File.Exists(Path.Combine(suiteDir, "TC-100.md")), "edited test file written under original id");
        Assert.False(File.Exists(Path.Combine(suiteDir, "TC-999.md")), "no new-id file created");
        Assert.True(File.Exists(Path.Combine(suiteDir, "_index.json")), "index regenerated");
    }

    [Fact]
    public void ApplyEdit_TakesEditableContentFromCandidate_PreservesInvariantsFromOriginal()
    {
        var original = Original(id: "TC-100", priority: Priority.High, component: "cart", tags: ["smoke"]);
        var candidate = new TestCase
        {
            Id = "TC-XXX", FilePath = "TC-XXX.md", Title = "New", Priority = Priority.High,
            Component = "cart", Tags = ["smoke"], Steps = ["new"], ExpectedResult = "new result"
        };

        var merged = UpdatedTestIngestor.ApplyEdit(original, candidate);

        Assert.Equal("TC-100", merged.Id);            // invariant
        Assert.Equal("TC-100.md", merged.FilePath);   // invariant
        Assert.Equal("New", merged.Title);            // editable
        Assert.Equal(["new"], merged.Steps);          // editable
        Assert.Equal("new result", merged.ExpectedResult);
    }

    // ---------- US2: manual content preserved regardless of model output (T015) ----------

    [Fact]
    public async Task Ingest_ManualOriginal_PreservesManualGrounding_EvenWhenModelDropsIt()
    {
        var original = Original(id: "TC-100", grounding: ManualGrounding());
        var content = EditJson(); // model output carries no grounding at all

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PersistedTests[0].Grounding);
        Assert.Equal(VerificationVerdict.Manual, result.PersistedTests[0].Grounding!.Verdict);
    }

    [Fact]
    public async Task Ingest_NonManualOriginal_IsUnaffected()
    {
        var original = Original(id: "TC-100", grounding: null);
        var result = await NewIngestor().IngestAsync(EditJson(), _testsPath, Suite, original, [original], default);

        Assert.True(result.IsSuccess);
        Assert.Null(result.PersistedTests[0].Grounding); // preserved from original (was null)
    }

    // ---------- US3: drift guard (T017) ----------

    [Fact]
    public async Task Ingest_ProtectedFieldChanged_FailsLoud_DriftDetected_NothingPersisted()
    {
        var original = Original(id: "TC-100", priority: Priority.High);
        var content = EditJson(priority: "low"); // out-of-scope change to a protected field

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdatedTestIngestor.DriftDetected, result.ErrorCode);
        Assert.Contains(result.Errors, e => e.Contains("priority"));

        var suiteDir = Path.Combine(_testsPath, Suite);
        Assert.False(Directory.Exists(suiteDir) && Directory.EnumerateFileSystemEntries(suiteDir).Any(),
            "drift must persist nothing");
    }

    [Fact]
    public void CompareForDrift_DetectsProtectedChanges_IgnoresTagReorder()
    {
        var original = Original(component: "cart", tags: ["a", "b"]);
        var candidateSamePriorityReorderedTags = new TestCase
        {
            Id = "TC-100", FilePath = "TC-100.md", Title = "x", Priority = Priority.High,
            Component = "cart", Tags = ["b", "a"], Steps = ["s"], ExpectedResult = "r"
        };
        Assert.False(UpdatedTestIngestor.CompareForDrift(original, candidateSamePriorityReorderedTags).HasDrift);

        var candidateChangedComponent = new TestCase
        {
            Id = "TC-100", FilePath = "TC-100.md", Title = "x", Priority = Priority.High,
            Component = "billing", Tags = ["a", "b"], Steps = ["s"], ExpectedResult = "r"
        };
        var drift = UpdatedTestIngestor.CompareForDrift(original, candidateChangedComponent);
        Assert.True(drift.HasDrift);
        Assert.Contains(drift.Entries, e => e.FieldName == "component");
    }

    [Fact]
    public async Task Ingest_EditConfinedToEditableFields_Passes()
    {
        var original = Original(id: "TC-100", priority: Priority.High, component: "cart", tags: ["smoke"]);
        // Same protected fields, only content changes.
        var content = EditJson(priority: "high", component: "cart", tags: ["smoke"],
            title: "New title", steps: "new step", expected: "new expected");

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", result.PersistedTests[0].Title);
    }

    // ---------- US4: fail-loud reuse of the generation pipeline (T021 unit-level) ----------

    [Theory]
    [InlineData(null, IngestErrorCode.EmptyContent)]
    [InlineData("", IngestErrorCode.EmptyContent)]
    [InlineData("not json", IngestErrorCode.EmptyContent)]
    public async Task Ingest_InvalidContent_FailsLoud_NothingPersisted(string? content, string expectedCode)
    {
        var original = Original();
        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(result.PersistedTests);
    }

    // ---------- Spec 088 FR-015: an edit that changes nothing ----------

    [Fact]
    public async Task Ingest_IdenticalEdit_IsUnchanged_NothingWritten()
    {
        var original = Original();
        var content = EditJson(title: "Original title", steps: "original step", expected: "original result");

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Unchanged);
        Assert.Empty(result.PersistedTests);
        Assert.Same(original, result.Original);
        Assert.False(Directory.Exists(Path.Combine(_testsPath, Suite)), "nothing written");
    }

    [Fact]
    public async Task Ingest_IdenticalEditWithReorderedTags_IsUnchanged()
    {
        var original = Original(tags: ["a", "b"]);
        var content = EditJson(title: "Original title", tags: ["b", "a"], steps: "original step", expected: "original result");

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.True(result.Unchanged);
    }

    [Fact]
    public async Task Ingest_DriftFailsBeforeTheUnchangedCheck()
    {
        var original = Original();
        var content = EditJson(title: "Original title", priority: "low", steps: "original step", expected: "original result");

        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdatedTestIngestor.DriftDetected, result.ErrorCode);
    }

    public static TheoryData<string, Func<TestCase, TestCase>, bool> UnchangedRows => new()
    {
        { "identical", t => t with { }, true },
        { "trailing space in a step", t => t with { Steps = ["step one ", "step two"] }, true },
        { "CRLF and a final newline", t => t with { ExpectedResult = "original result\r\n" }, true },
        { "null vs empty preconditions", t => t with { Preconditions = "" }, true },
        { "inner whitespace", t => t with { Steps = ["step  one", "step two"] }, false },
        { "a word", t => t with { ExpectedResult = "original results" }, false },
        { "a step added", t => t with { Steps = ["step one", "step two", "step three"] }, false },
        { "steps reordered", t => t with { Steps = ["step two", "step one"] }, false },
        { "a criterion added", t => t with { Criteria = ["AC-1"] }, false },
        { "a source ref changed", t => t with { SourceRefs = ["docs/a.md"] }, false },
    };

    [Theory]
    [MemberData(nameof(UnchangedRows))]
    public void IsUnchanged_ComparesNormalisedEditableFields(string row, Func<TestCase, TestCase> edit, bool unchanged)
    {
        var original = Original() with { Steps = ["step one", "step two"] };

        Assert.True(unchanged == UpdatedTestIngestor.IsUnchanged(original, edit(original)), row);
    }

    [Fact]
    public void IsUnchanged_CrLineEndings_EqualLf()
    {
        var original = Original() with { Preconditions = "line one\nline two" };

        Assert.True(UpdatedTestIngestor.IsUnchanged(original, original with { Preconditions = "line one\rline two" }));
        Assert.True(UpdatedTestIngestor.IsUnchanged(original, original with { Preconditions = "line one  \r\nline two\n\n" }));
    }

    // ---------- Spec 089 F2: one test per edit ----------

    [Fact]
    public async Task Ingest_TwoTestObjects_IsMultipleTests_NothingWritten()
    {
        var original = Original();
        var two = "[" + EditJson(id: "TC-100").Trim().Trim('[', ']') + "," + EditJson(id: "TC-101").Trim().Trim('[', ']') + "]";

        var result = await NewIngestor().IngestAsync(two, _testsPath, Suite, original, [original], default);

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdatedTestIngestor.MultipleTests, result.ErrorCode);
        Assert.Equal("The edit holds 2 test objects (TC-100, TC-101); a staged file holds exactly one test.", Assert.Single(result.Errors));
        Assert.False(Directory.Exists(Path.Combine(_testsPath, Suite)), "nothing written");
    }

    [Fact]
    public void ArrayObjects_CountsObjects_NotValidTests()
    {
        Assert.Equal(["TC-1", null], GeneratedTestIngestor.ArrayObjects("[{\"id\":\"TC-1\",\"title\":\"t\"},{\"title\":\"no id\"}]"));
        Assert.Equal(["TC-1"], GeneratedTestIngestor.ArrayObjects("```json\n[{\"id\":\"TC-1\"}]\n```"));
        Assert.Empty(GeneratedTestIngestor.ArrayObjects("[]")!);
        Assert.Null(GeneratedTestIngestor.ArrayObjects("not json"));
        Assert.Null(GeneratedTestIngestor.ArrayObjects(null));
    }

    [Fact]
    public async Task Ingest_SchemaInvalidEdit_FailsLoud()
    {
        var original = Original(id: "TC-100");
        var content = EditJson(id: "BADID"); // violates TC-### — but id is forced anyway; schema still rejects
        var result = await NewIngestor().IngestAsync(content, _testsPath, Suite, original, [original], default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestErrorCode.SchemaInvalid, result.ErrorCode);
    }
}
