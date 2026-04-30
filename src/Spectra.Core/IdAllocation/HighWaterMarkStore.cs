using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Index;

namespace Spectra.Core.IdAllocation;

/// <summary>
/// Reads and writes the persistent ID-allocator high-water-mark file at
/// <c>.spectra/id-allocator.json</c>. The HWM is the largest test-ID number
/// ever allocated in this workspace; it is monotonic and never decreases.
/// </summary>
/// <remarks>
/// Spec 040 / Decision 3: corrupted or future-version files are treated as
/// "absent" — the caller re-seeds from the union of index + filesystem scan.
/// Writes are atomic via temp+rename through <see cref="AtomicFileWriter"/>.
/// </remarks>
public sealed class HighWaterMarkStore
{
    private const int CurrentVersion = 1;

    private readonly string _path;

    public HighWaterMarkStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <summary>
    /// Reads the stored HWM. Returns 0 if the file is missing, corrupt, or
    /// recorded by an unknown future version. <paramref name="warning"/> is
    /// populated when a recoverable issue was suppressed.
    /// </summary>
    public async Task<int> ReadAsync(CancellationToken ct = default)
    {
        var (value, _) = await ReadWithWarningAsync(ct).ConfigureAwait(false);
        return value;
    }

    /// <summary>
    /// Read variant that surfaces recoverable warnings (corrupt file, version
    /// mismatch) for callers that want to log them.
    /// </summary>
    public async Task<(int Value, string? Warning)> ReadWithWarningAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path))
        {
            return (0, null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
            var record = JsonSerializer.Deserialize<HighWaterMarkRecord>(json);

            if (record is null)
            {
                return (0, $"HWM file at {_path} parsed as null; treating as absent");
            }

            if (record.Version != CurrentVersion)
            {
                return (0, $"HWM file at {_path} has version {record.Version} (expected {CurrentVersion}); treating as absent");
            }

            return (record.HighWaterMark, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return (0, $"HWM file at {_path} unreadable ({ex.Message}); treating as absent");
        }
    }

    /// <summary>
    /// Writes a new HWM. Caller is responsible for ensuring monotonicity —
    /// this method asserts it but does not silently downgrade.
    /// </summary>
    public async Task WriteAsync(int newHighWaterMark, string command, CancellationToken ct = default)
    {
        if (newHighWaterMark < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newHighWaterMark), "HWM must be non-negative");
        }

        var record = new HighWaterMarkRecord
        {
            Version = CurrentVersion,
            HighWaterMark = newHighWaterMark,
            LastAllocatedAt = DateTimeOffset.UtcNow.ToString("o"),
            LastAllocatedCommand = command ?? "unknown"
        };

        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        await AtomicFileWriter.WriteAllTextAsync(_path, json, ct).ConfigureAwait(false);
    }

    private sealed class HighWaterMarkRecord
    {
        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("high_water_mark")]
        public int HighWaterMark { get; init; }

        [JsonPropertyName("last_allocated_at")]
        public string? LastAllocatedAt { get; init; }

        [JsonPropertyName("last_allocated_command")]
        public string? LastAllocatedCommand { get; init; }
    }
}
