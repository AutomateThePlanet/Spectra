using Spectra.CLI.Commands.Analyze;
using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// Spec 048 test plan row 2. The handler's per-document upsert path constructs
/// <see cref="CriteriaSource"/> entries with <c>Outcome = OutcomeExtracted</c>
/// at two literal sites in <c>AnalyzeHandler.RunExtractCriteriaAsync</c>. End-
/// to-end verification of those sites against a real Copilot SDK lives in
/// quickstart.md §F; here we pin the contract: the constant the handler uses
/// equals the value the YAML serializer emits and that legacy readers default
/// to. If any of those three drift apart, the silent-skip bug Spec 048 was
/// written to prevent could resurface.
/// </summary>
public class CriteriaExtractorOutcomeTests
{
    [Fact]
    public void Outcome_HandlerConstant_MatchesModelDefault()
    {
        var fresh = new CriteriaSource { File = "x.criteria.yaml" };
        Assert.Equal(AnalyzeHandler.OutcomeExtracted, fresh.Outcome);
    }

    [Fact]
    public void Extract_RealEmpty_RecordsOutcomeExtracted()
    {
        // Mirrors the handler's "add new source" upsert branch in
        // RunExtractCriteriaAsync — the construction is a literal Outcome
        // assignment, and the resulting YAML must carry the affirmed-extracted
        // marker even though criteria_count is zero. (FR-001 / FR-003.)
        var source = new CriteriaSource
        {
            File = "docs/criteria/empty.criteria.yaml",
            SourceDoc = "docs/empty.md",
            SourceType = "document",
            DocHash = "stub-hash",
            CriteriaCount = 0,
            LastExtracted = DateTime.UtcNow,
            Outcome = AnalyzeHandler.OutcomeExtracted,
        };

        var yaml = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build()
            .Serialize(source);

        Assert.Contains("outcome: extracted", yaml);
        Assert.Contains("criteria_count: 0", yaml);
    }
}
