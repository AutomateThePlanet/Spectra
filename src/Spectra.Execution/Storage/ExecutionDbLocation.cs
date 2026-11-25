namespace Spectra.MCP.Storage;

/// <summary>
/// One-time migration for execution databases written one folder too deep.
/// Up to 2.7.0, <c>spectra run</c> passed <c>&lt;repo&gt;/.execution</c> as the base path to
/// <see cref="ExecutionDb"/>, which appends <c>.execution/spectra.db</c> itself, so runs were stored in
/// <c>.execution/.execution/spectra.db</c>. The dashboard reads <c>.execution/spectra.db</c> and never saw them.
/// </summary>
public static class ExecutionDbLocation
{
    private const string DbDirectory = ".execution";
    private const string DbFileName = "spectra.db";
    private static readonly string[] SqliteFileSuffixes = ["", "-wal", "-shm"];

    public enum MigrationOutcome
    {
        /// <summary>No database at the nested path.</summary>
        NothingToDo,

        /// <summary>The nested database (and its WAL/SHM files) moved to <c>.execution/spectra.db</c>.</summary>
        Moved,

        /// <summary>Both paths hold a database; nothing was moved so no run history is overwritten.</summary>
        BothExist,
    }

    /// <summary>The database path every reader and writer uses: <c>&lt;basePath&gt;/.execution/spectra.db</c>.</summary>
    public static string DatabasePath(string basePath) => Path.Combine(basePath, DbDirectory, DbFileName);

    /// <summary>The wrong path older builds wrote to: <c>&lt;basePath&gt;/.execution/.execution/spectra.db</c>.</summary>
    public static string NestedLegacyPath(string basePath) => Path.Combine(basePath, DbDirectory, DbDirectory, DbFileName);

    /// <summary>
    /// Moves <c>.execution/.execution/spectra.db</c> (with its <c>-wal</c> and <c>-shm</c> files) to
    /// <c>.execution/spectra.db</c> when only the nested one exists. Call it before opening the database.
    /// </summary>
    public static MigrationOutcome MigrateNestedDatabase(string basePath)
    {
        var legacyDb = NestedLegacyPath(basePath);
        if (!File.Exists(legacyDb))
        {
            return MigrationOutcome.NothingToDo;
        }

        var targetDb = DatabasePath(basePath);
        if (File.Exists(targetDb))
        {
            return MigrationOutcome.BothExist;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetDb)!);
        foreach (var suffix in SqliteFileSuffixes)
        {
            var source = legacyDb + suffix;
            if (File.Exists(source))
            {
                File.Move(source, targetDb + suffix);
            }
        }

        var legacyDir = Path.GetDirectoryName(legacyDb)!;
        if (!Directory.EnumerateFileSystemEntries(legacyDir).Any())
        {
            Directory.Delete(legacyDir);
        }

        return MigrationOutcome.Moved;
    }
}
