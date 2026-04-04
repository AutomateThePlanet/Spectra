using Spectra.CLI.Session;

namespace Spectra.CLI.Tests.Session;

[Collection("WorkingDirectory")]
public class SessionStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SessionStore _store;

    public SessionStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new SessionStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CreateSession_SetsFields()
    {
        var session = _store.CreateSession("checkout");

        Assert.StartsWith("gen-", session.SessionId);
        Assert.Equal("checkout", session.Suite);
        Assert.True(session.ExpiresAt > session.StartedAt);
        Assert.False(session.IsExpired);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var session = _store.CreateSession("checkout");
        session.Generated.Add("TC-001");
        session.Suggestions.Add(new SessionSuggestion
        {
            Index = 1,
            Title = "Test suggestion",
            Category = "edge_case",
            Status = SuggestionStatus.Pending
        });

        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync("checkout");

        Assert.NotNull(loaded);
        Assert.Equal("checkout", loaded.Suite);
        Assert.Single(loaded.Generated);
        Assert.Equal("TC-001", loaded.Generated[0]);
        Assert.Single(loaded.Suggestions);
        Assert.Equal(SuggestionStatus.Pending, loaded.Suggestions[0].Status);
    }

    [Fact]
    public async Task Load_WrongSuite_ReturnsNull()
    {
        var session = _store.CreateSession("checkout");
        await _store.SaveAsync(session);

        var loaded = await _store.LoadAsync("other-suite");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Load_NoSession_ReturnsNull()
    {
        var loaded = await _store.LoadAsync("checkout");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Load_ExpiredSession_ReturnsNull()
    {
        var session = new GenerationSessionState
        {
            SessionId = "gen-expired",
            Suite = "checkout",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // expired 1 hour ago
        };

        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync("checkout");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Save_OverwritesPrevious()
    {
        var session1 = _store.CreateSession("checkout");
        session1.Generated.Add("TC-001");
        await _store.SaveAsync(session1);

        var session2 = _store.CreateSession("checkout");
        session2.Generated.Add("TC-002");
        await _store.SaveAsync(session2);

        var loaded = await _store.LoadAsync("checkout");
        Assert.NotNull(loaded);
        Assert.Single(loaded.Generated);
        Assert.Equal("TC-002", loaded.Generated[0]);
    }
}
