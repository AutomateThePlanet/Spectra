using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Extraction;

/// <summary>
/// Spec 047/069: classifies the outcome of a single-document criteria extraction.
/// The caller uses this to decide whether the document's content hash is
/// safe to record in the cache — only <see cref="Extracted"/> is cacheable.
/// <see cref="EmptyResponse"/> and <see cref="ParseFailure"/> are
/// transport/parser-class failures and must be re-attempted on the next run.
///
/// Spec 069 relocated this enum + <see cref="CriteriaExtractionResult"/> out of the (now-deleted)
/// <c>Agent/Copilot</c> namespace into the model-free <c>Spectra.CLI.Extraction</c> home so the
/// classification path carries no GitHub Copilot SDK dependency.
/// </summary>
public enum ExtractionOutcome
{
    /// <summary>
    /// AI returned a valid (possibly empty) criteria list, or the source
    /// document was empty/whitespace and there was nothing to extract.
    /// </summary>
    Extracted,

    /// <summary>
    /// AI returned empty or whitespace-only text — transport-class failure.
    /// </summary>
    EmptyResponse,

    /// <summary>
    /// Response was present but unparseable: no JSON array delimiters,
    /// deserialize returned null, or an exception was raised while parsing.
    /// </summary>
    ParseFailure,
}

/// <summary>
/// Spec 047/069: typed result of a single-document criteria classification.
/// <see cref="IsCacheable"/> is the only signal the caller uses to decide
/// whether to write the document's content-hash cache record.
/// </summary>
public sealed record CriteriaExtractionResult(
    ExtractionOutcome Outcome,
    IReadOnlyList<AcceptanceCriterion> Criteria)
{
    public bool IsCacheable => Outcome == ExtractionOutcome.Extracted;
}
