using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Spec 047: classifies the outcome of a single-document criteria extraction.
/// The caller uses this to decide whether the document's content hash is
/// safe to record in the cache — only <see cref="Extracted"/> is cacheable.
/// <see cref="EmptyResponse"/> and <see cref="ParseFailure"/> are
/// transport/parser-class failures and must be re-attempted on the next run.
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
/// Spec 047: typed result of <c>CriteriaExtractor.ExtractFromDocumentAsync</c>.
/// <see cref="IsCacheable"/> is the only signal the caller uses to decide
/// whether to write the document's content-hash cache record.
/// </summary>
public sealed record CriteriaExtractionResult(
    ExtractionOutcome Outcome,
    IReadOnlyList<AcceptanceCriterion> Criteria)
{
    public bool IsCacheable => Outcome == ExtractionOutcome.Extracted;
}
