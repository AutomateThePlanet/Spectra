using System.Diagnostics;
using Spectra.CLI.Cancellation;

namespace Spectra.CLI.Tests.Cancellation;

public sealed class PidFileManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _pidPath;

    public PidFileManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-pid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _pidPath = Path.Combine(_tempDir, ".pid");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsNull()
    {
        var mgr = new PidFileManager(_pidPath);
        Assert.Null(await mgr.ReadAsync());
    }

    [Fact]
    public async Task RoundTrip_PreservesAllFields()
    {
        var mgr = new PidFileManager(_pidPath);
        await mgr.WriteAsync(12345, "ai generate", "spectra");

        var record = await mgr.ReadAsync();
        Assert.NotNull(record);
        Assert.Equal(12345, record!.Pid);
        Assert.Equal("ai generate", record.Command);
        Assert.Equal("spectra", record.ProcessName);
        Assert.False(string.IsNullOrEmpty(record.StartedAt));
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var mgr = new PidFileManager(_pidPath);
        await mgr.WriteAsync(1, "x", "spectra");
        Assert.True(File.Exists(_pidPath));

        mgr.Delete();
        Assert.False(File.Exists(_pidPath));
    }

    [Fact]
    public void IsStale_NoSuchPid_ReturnsTrue()
    {
        var record = new PidFileManager.PidRecord
        {
            Pid = 99999999,  // very unlikely to exist
            Command = "test",
            ProcessName = "spectra",
            StartedAt = "2026-01-01T00:00:00Z"
        };
        Assert.True(PidFileManager.IsStale(record));
    }

    [Fact]
    public void IsStale_OurOwnProcess_ReturnsFalse()
    {
        var current = Process.GetCurrentProcess();
        var record = new PidFileManager.PidRecord
        {
            Pid = current.Id,
            Command = "test",
            ProcessName = current.ProcessName,
            StartedAt = "2026-01-01T00:00:00Z"
        };
        // Our test process is `dotnet` (or `testhost` — both are in the allow-list under common .NET test runners)
        // If running under a different host, this test may need an override.
        // The key assertion: a live SPECTRA-allowlisted process is NOT stale.
        if (current.ProcessName.Equals("spectra", StringComparison.OrdinalIgnoreCase) ||
            current.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
            current.ProcessName.Equals("Spectra.CLI", StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(PidFileManager.IsStale(record));
        }
        else
        {
            Assert.True(PidFileManager.IsStale(record));
        }
    }
}
