using Spectra.Core.Criteria;
using Spectra.Core.IdAllocation;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Criteria;

/// <summary>
/// Spec 079: the single owner of acceptance-criteria read-modify-write. Every writer (ingest, import, retire,
/// apply-move, migrate) runs inside <see cref="WithLockAsync{T}"/>, loads a <see cref="CriteriaWorkspace"/>, and
/// persists through it, so concurrent writers never lose an index update or issue the same ID.
/// </summary>
public static class CriteriaStore
{
    public const string ErrorLockTimeout = "LOCK_TIMEOUT";
    public const string ErrorFileUnreadable = "CRITERIA_FILE_UNREADABLE";

    private const string CriteriaPattern = "*.criteria.yaml";

    /// <summary>Default time to wait for the criteria lock.</summary>
    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Runs <paramref name="body"/> while holding <c>.spectra/criteria.lock</c>. A lock that cannot be acquired in
    /// time throws <see cref="CriteriaStoreException"/> with <see cref="ErrorLockTimeout"/>.
    /// </summary>
    public static async Task<T> WithLockAsync<T>(
        string repoRoot, Func<Task<T>> body, CancellationToken ct, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentNullException.ThrowIfNull(body);

        var lockPath = Path.Combine(repoRoot, ".spectra", "criteria.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        FileLockHandle handle;
        try
        {
            handle = await FileLockHandle.AcquireAsync(lockPath, timeout ?? DefaultLockTimeout, ct);
        }
        catch (TimeoutException)
        {
            throw new CriteriaStoreException(ErrorLockTimeout,
                $"Could not acquire the criteria lock ({lockPath}); another SPECTRA command is writing criteria.");
        }

        using (handle)
        {
            return await body();
        }
    }

    /// <summary>
    /// Loads the index (strictly: a corrupt index throws) and every criteria file under the criteria directory
    /// (recursively, leniently: they are only scanned for IDs and duplicates).
    /// </summary>
    public static async Task<CriteriaWorkspace> LoadAsync(SpectraConfig config, string repoRoot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);

        var criteriaDir = Path.GetFullPath(Path.Combine(repoRoot, config.Coverage.CriteriaDir));
        var indexPath = Path.GetFullPath(Path.Combine(repoRoot, config.Coverage.CriteriaFile));

        CriteriaIndex index;
        try
        {
            index = await new CriteriaIndexReader().ReadStrictAsync(indexPath, ct);
        }
        catch (CriteriaFileUnreadableException ex)
        {
            throw new CriteriaStoreException(ErrorFileUnreadable, ex.Message);
        }

        var files = new List<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)>();
        if (Directory.Exists(criteriaDir))
        {
            var reader = new CriteriaFileReader();
            foreach (var path in Directory.GetFiles(criteriaDir, CriteriaPattern, SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(criteriaDir, path).Replace('\\', '/');
                files.Add((relative, await reader.ReadAsync(path, ct)));
            }
        }

        return new CriteriaWorkspace(repoRoot, criteriaDir, indexPath, index, files);
    }
}

/// <summary>A criteria operation failed with a machine-readable error code.</summary>
public sealed class CriteriaStoreException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// Criteria state loaded under the lock: the index plus every criteria file (relative to the criteria dir).
/// </summary>
public sealed class CriteriaWorkspace(
    string repoRoot,
    string criteriaDir,
    string indexPath,
    CriteriaIndex index,
    IReadOnlyList<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)> files)
{
    public string RepoRoot { get; } = repoRoot;
    public string CriteriaDir { get; } = criteriaDir;
    public string IndexPath { get; } = indexPath;
    public CriteriaIndex Index { get; } = index;
    public IReadOnlyList<(string File, IReadOnlyList<AcceptanceCriterion> Criteria)> Files { get; } = files;

    /// <summary>Every criterion ID present in any criteria file (for the allocator's effective mark).</summary>
    public IEnumerable<string> AllIds => Files.SelectMany(f => f.Criteria).Select(c => c.Id).Where(id => !string.IsNullOrWhiteSpace(id));

    /// <summary>Workspace-wide duplicate IDs.</summary>
    public IReadOnlyList<DuplicateCriterionId> Duplicates => CriteriaDuplicateScanner.Scan(Files);

    /// <summary>Absolute path of a file given relative to the criteria dir.</summary>
    public string FullPath(string fileRelativeToCriteriaDir) =>
        Path.GetFullPath(Path.Combine(CriteriaDir, fileRelativeToCriteriaDir.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>The index source for a document, matched on the lexically normalised <c>source_doc</c>.</summary>
    public CriteriaSource? FindDocumentSource(string normalizedDoc) =>
        Index.Sources.FirstOrDefault(s => CriteriaPaths.IsDocumentSource(s)
            && string.Equals(CriteriaPaths.NormalizeLexical(s.SourceDoc), normalizedDoc, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The index source other than <paramref name="self"/> whose file is <paramref name="file"/>, if any
    /// (the collision check before writing a derived path).
    /// </summary>
    public CriteriaSource? FileOwnedByOther(string file, CriteriaSource? self) =>
        Index.Sources.FirstOrDefault(s => !ReferenceEquals(s, self)
            && string.Equals(CriteriaPaths.NormalizeLexical(s.File), CriteriaPaths.NormalizeLexical(file), StringComparison.OrdinalIgnoreCase));

    /// <summary>Strictly reads one criteria file (missing = empty; corrupt = <see cref="CriteriaStoreException"/>).</summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ReadStrictAsync(string fileRelativeToCriteriaDir, CancellationToken ct)
    {
        try
        {
            return await new CriteriaFileReader().ReadStrictAsync(FullPath(fileRelativeToCriteriaDir), ct);
        }
        catch (CriteriaFileUnreadableException ex)
        {
            throw new CriteriaStoreException(CriteriaStore.ErrorFileUnreadable, ex.Message);
        }
    }

    public Task WriteCriteriaAsync(
        string fileRelativeToCriteriaDir, IReadOnlyList<AcceptanceCriterion> criteria, string sourceDoc,
        string? docHash, CancellationToken ct) =>
        new CriteriaFileWriter().WriteAsync(FullPath(fileRelativeToCriteriaDir), criteria, sourceDoc, docHash, ct);

    public Task SaveIndexAsync(CancellationToken ct) => new CriteriaIndexWriter().WriteAsync(IndexPath, Index, ct);
}
