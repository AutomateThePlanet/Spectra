using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Reads per-document .criteria.yaml files.
/// </summary>
public sealed class CriteriaFileReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Reads criteria from a .criteria.yaml file. Returns empty list if file doesn't exist or is malformed.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ReadAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return [];

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var doc = Deserializer.Deserialize<CriteriaDocument>(content);
            return doc?.Criteria ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Spec 079: strict read for write paths. Returns an empty list when the file does not exist, but throws
    /// <see cref="CriteriaFileUnreadableException"/> when it exists and cannot be read or parsed, so an ingest
    /// never mistakes a corrupt file for "no previous criteria".
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ReadStrictAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return [];

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var doc = Deserializer.Deserialize<CriteriaDocument>(content);
            return doc?.Criteria ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new CriteriaFileUnreadableException(filePath, ex);
        }
    }
}

/// <summary>A criteria file (or criteria index) exists but cannot be read or parsed.</summary>
public sealed class CriteriaFileUnreadableException(string path, Exception inner)
    : Exception($"Criteria file cannot be read: {path} ({inner.Message})", inner)
{
    public string FilePath { get; } = path;
}
