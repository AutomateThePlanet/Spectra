using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Tests.Models.Coverage;

/// <summary>
/// Spec 048 test plan row 1: round-trip and legacy-default behaviour of the
/// new <see cref="CriteriaSource.Outcome"/> field.
/// </summary>
public class CriteriaSourceTests
{
    private static ISerializer Serializer => new SerializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .Build();

    private static IDeserializer Deserializer => new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    [Fact]
    public void CriteriaSource_DefaultOutcome_IsExtracted()
    {
        // A freshly-constructed instance carries the property default, which is
        // the only value Spec 047+ code paths write.
        var source = new CriteriaSource { File = "x.criteria.yaml" };
        Assert.Equal("extracted", source.Outcome);
    }

    [Fact]
    public void CriteriaSource_Roundtrip_PreservesOutcome()
    {
        var original = new CriteriaSource
        {
            File = "docs/criteria/login.criteria.yaml",
            SourceDoc = "docs/login.md",
            SourceType = "document",
            DocHash = "abc123",
            CriteriaCount = 3,
            LastExtracted = DateTime.SpecifyKind(new DateTime(2026, 6, 2, 10, 0, 0), DateTimeKind.Utc),
            Outcome = "extracted",
        };

        var yaml = Serializer.Serialize(original);

        // YAML must carry the new key with the expected value.
        Assert.Contains("outcome: extracted", yaml);

        var roundTripped = Deserializer.Deserialize<CriteriaSource>(yaml);
        Assert.Equal("extracted", roundTripped.Outcome);
        Assert.Equal(original.File, roundTripped.File);
        Assert.Equal(original.CriteriaCount, roundTripped.CriteriaCount);
    }

    [Fact]
    public void CriteriaSource_LegacyYamlWithoutOutcome_DeserializesAsExtracted()
    {
        // Simulates a pre-Spec-048 entry written by an earlier release: the
        // `outcome:` key is absent. FR-002 requires this to deserialize as
        // "extracted" via the property default — no migration step.
        const string legacyYaml = """
            file: docs/criteria/login.criteria.yaml
            source_doc: docs/login.md
            source_type: document
            doc_hash: c0ffee
            criteria_count: 0
            last_extracted: 2026-04-12T10:00:00Z
            """;

        var entry = Deserializer.Deserialize<CriteriaSource>(legacyYaml);

        Assert.Equal("extracted", entry.Outcome);
        Assert.Equal("docs/criteria/login.criteria.yaml", entry.File);
        Assert.Equal(0, entry.CriteriaCount);
    }
}
