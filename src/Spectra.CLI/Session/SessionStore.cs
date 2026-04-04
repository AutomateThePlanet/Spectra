using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.Session;

/// <summary>
/// Reads and writes generation session state to .spectra/session.json.
/// </summary>
public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _sessionPath;

    public SessionStore(string? basePath = null)
    {
        var root = basePath ?? Directory.GetCurrentDirectory();
        var spectraDir = Path.Combine(root, ".spectra");
        _sessionPath = Path.Combine(spectraDir, "session.json");
    }

    /// <summary>
    /// Creates a new session for a suite, replacing any existing session.
    /// </summary>
    public GenerationSessionState CreateSession(string suite)
    {
        var now = DateTimeOffset.UtcNow;
        return new GenerationSessionState
        {
            SessionId = $"gen-{now:yyyy-MM-dd-HHmmss}",
            Suite = suite,
            StartedAt = now,
            ExpiresAt = now.AddHours(1)
        };
    }

    /// <summary>
    /// Saves session state to disk.
    /// </summary>
    public async Task SaveAsync(GenerationSessionState session, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_sessionPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(_sessionPath, json, ct);
    }

    /// <summary>
    /// Loads the current session for a suite. Returns null if no session, expired, or different suite.
    /// </summary>
    public async Task<GenerationSessionState?> LoadAsync(string suite, CancellationToken ct = default)
    {
        if (!File.Exists(_sessionPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_sessionPath, ct);
            var session = JsonSerializer.Deserialize<GenerationSessionState>(json, JsonOptions);

            if (session is null || session.Suite != suite || session.IsExpired)
                return null;

            return session;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
