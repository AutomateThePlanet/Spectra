using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.IO;

/// <summary>
/// Append-only NDJSON writer for the drop trail (.spectra/dropped-tests.json).
/// Each call to AppendAsync writes one JSON line; the file is created on first write.
/// </summary>
public sealed class DroppedTestsTrail
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _trailPath;

    public DroppedTestsTrail(string workingDir)
    {
        _trailPath = Path.Combine(workingDir, ".spectra", "dropped-tests.json");
    }

    /// <summary>
    /// Appends one drop-trail entry as a NDJSON line. Creates the file if absent.
    /// Throws on I/O error — caller must not proceed to delete if this throws.
    /// </summary>
    public async Task<int> AppendAsync(DroppedTestEntry entry, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_trailPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var line = JsonSerializer.Serialize(entry, SerializerOptions);
        await File.AppendAllTextAsync(_trailPath, line + Environment.NewLine, ct);

        var lines = await File.ReadAllLinesAsync(_trailPath, ct);
        return lines.Count(l => !string.IsNullOrWhiteSpace(l));
    }

    public string TrailPath => _trailPath;
}

/// <summary>
/// One entry in the drop trail.
/// </summary>
public sealed class DroppedTestEntry
{
    public required string Id { get; init; }
    public required string Suite { get; init; }
    public required string Title { get; init; }
    public required string DropReason { get; init; }
    public string? ContradictingClaim { get; init; }
    public string? DocRef { get; init; }
    public string? CriticModel { get; init; }
    public required string Timestamp { get; init; }
    public required string Source { get; init; }
}
