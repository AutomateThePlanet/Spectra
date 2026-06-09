#pragma warning disable CS0618 // RequirementDefinition is obsolete project-wide; the docs-index path still produces it pending the full criteria migration (Spec 047 kept the two payload types separate).
using Spectra.CLI.Extraction;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Spec 054 (FR-004 / FR-007): the unified failure-semantics result for the legacy
/// <see cref="RequirementsExtractor"/> (the <c>docs index</c> path). It deliberately reuses the
/// <b>same</b> <see cref="ExtractionOutcome"/> enum and the <b>same</b> cacheability rule
/// (<see cref="IsCacheable"/> ⇔ <see cref="ExtractionOutcome.Extracted"/>) as
/// <see cref="CriteriaExtractionResult"/> — that shared contract is the "one extraction
/// failure-semantics contract" this spec converges on. The payload type
/// (<see cref="RequirementDefinition"/>) stays distinct from the criteria path's
/// <c>AcceptanceCriterion</c>, honoring the Spec 047 decision to keep the two extractor
/// implementations separate; only the failure semantics are unified.
/// </summary>
public sealed record RequirementsExtractionResult(
    ExtractionOutcome Outcome,
    IReadOnlyList<RequirementDefinition> Requirements)
{
    /// <summary>
    /// Only a genuine extraction is cacheable. Identical rule to
    /// <see cref="CriteriaExtractionResult.IsCacheable"/>.
    /// </summary>
    public bool IsCacheable => Outcome == ExtractionOutcome.Extracted;
}
