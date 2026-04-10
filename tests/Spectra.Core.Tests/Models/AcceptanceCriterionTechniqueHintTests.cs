using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Models;

/// <summary>
/// Spec 037: AcceptanceCriterion gains an optional TechniqueHint field
/// (BVA, EP, DT, ST). Must round-trip via YAML, default to null when absent,
/// and be omitted from output when null (to keep legacy files clean).
/// </summary>
public class AcceptanceCriterionTechniqueHintTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CriteriaFileReader _reader = new();
    private readonly CriteriaFileWriter _writer = new();

    public AcceptanceCriterionTechniqueHintTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"crit-hint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void TechniqueHint_DefaultsToNull()
    {
        var c = new AcceptanceCriterion { Id = "AC-001", Text = "x" };
        Assert.Null(c.TechniqueHint);
    }

    [Fact]
    public async Task TechniqueHint_RoundTripsThroughYaml()
    {
        var path = Path.Combine(_tempDir, "test.criteria.yaml");
        var input = new List<AcceptanceCriterion>
        {
            new() { Id = "AC-001", Text = "Username MUST be 3-20 characters", TechniqueHint = "BVA" },
            new() { Id = "AC-002", Text = "User MUST be authenticated" } // no hint
        };

        await _writer.WriteAsync(path, input);
        var loaded = await _reader.ReadAsync(path);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("BVA", loaded[0].TechniqueHint);
        Assert.Null(loaded[1].TechniqueHint);
    }

    [Fact]
    public async Task TechniqueHint_NullValue_OmittedFromYamlOutput()
    {
        var path = Path.Combine(_tempDir, "test.criteria.yaml");
        var input = new List<AcceptanceCriterion>
        {
            new() { Id = "AC-001", Text = "User MUST be authenticated", TechniqueHint = null }
        };

        await _writer.WriteAsync(path, input);
        var yaml = await File.ReadAllTextAsync(path);

        // YamlDotNet OmitNull policy must keep technique_hint out of the file
        Assert.DoesNotContain("technique_hint", yaml);
    }

    [Fact]
    public async Task LegacyFile_WithoutTechniqueHint_ReadsAsNull()
    {
        var path = Path.Combine(_tempDir, "legacy.criteria.yaml");
        // Write a YAML file by hand that pre-dates spec 037
        await File.WriteAllTextAsync(path, """
            criteria:
              - id: AC-001
                text: "User MUST be authenticated"
                priority: high
                tags: []
                linked_test_ids: []
            """);

        var loaded = await _reader.ReadAsync(path);

        Assert.Single(loaded);
        Assert.Null(loaded[0].TechniqueHint);
    }
}
