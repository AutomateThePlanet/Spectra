namespace Spectra.Core.Models.Index;

/// <summary>
/// In-memory model of one <c>groups/{id}.index.md</c> file. Same per-doc entry
/// shape as the legacy single-file index, scoped to one suite, with no checksum
/// block (checksums live in <see cref="ChecksumStore"/>).
/// </summary>
public sealed class SuiteIndexFile
{
    public required string SuiteId { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public required int DocumentCount { get; init; }

    public required int TokensEstimated { get; init; }

    /// <summary>
    /// Per-document entries, sorted by <see cref="Spectra.Core.Models.DocumentIndexEntry.Path"/>
    /// (ordinal) on write. Reuses the existing
    /// <see cref="Spectra.Core.Models.DocumentIndexEntry"/> shape verbatim.
    /// </summary>
    public required IReadOnlyList<Spectra.Core.Models.DocumentIndexEntry> Entries { get; init; }
}
