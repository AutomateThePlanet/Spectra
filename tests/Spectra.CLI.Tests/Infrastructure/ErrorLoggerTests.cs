using System.Net;
using Spectra.CLI.Infrastructure;
using Xunit;

namespace Spectra.CLI.Tests.Infrastructure;

// Mutates process-global state (ErrorLogger statics). Run serially.
[CollectionDefinition("ErrorLogger", DisableParallelization = true)]
public class ErrorLoggerCollection { }

[Collection("ErrorLogger")]
public class ErrorLoggerTests : IDisposable
{
    private static readonly SemaphoreSlim _globalLock = new(1, 1);
    private readonly IDisposable _lockHold;
    private sealed class LockHold : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        public LockHold(SemaphoreSlim sem) { _sem = sem; _sem.Wait(); }
        public void Dispose() { _sem.Release(); }
    }

    private readonly string _tempDir;
    private readonly string _logPath;
    private readonly bool _prevEnabled;
    private readonly string _prevLogFile;
    private readonly string _prevMode;

    public ErrorLoggerTests()
    {
        _lockHold = new LockHold(_globalLock);
        _prevEnabled = ErrorLogger.Enabled;
        _prevLogFile = ErrorLogger.LogFile;
        _prevMode = ErrorLogger.Mode;

        _tempDir = Path.Combine(Path.GetTempPath(), "spectra-errorlogger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _logPath = Path.Combine(_tempDir, "errors.log");

        ErrorLogger.LogFile = _logPath;
        ErrorLogger.Mode = "append";
        ErrorLogger.BeginRun();
    }

    public void Dispose()
    {
        ErrorLogger.Enabled = _prevEnabled;
        ErrorLogger.LogFile = _prevLogFile;
        ErrorLogger.Mode = _prevMode;
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        _lockHold.Dispose();
    }

    [Fact]
    public void Disabled_NoFileCreated()
    {
        ErrorLogger.Enabled = false;
        ErrorLogger.Write("critic", "test_id=TC-001", new InvalidOperationException("boom"));
        Assert.False(File.Exists(_logPath));
    }

    [Fact]
    public void Write_CreatesFileOnFirstError()
    {
        ErrorLogger.Enabled = true;
        Assert.False(File.Exists(_logPath));
        ErrorLogger.Write("critic", "test_id=TC-001", new InvalidOperationException("boom"));
        Assert.True(File.Exists(_logPath));
        var content = File.ReadAllText(_logPath);
        Assert.Contains("[critic] ERROR test_id=TC-001", content);
        Assert.Contains("Type: System.InvalidOperationException", content);
        Assert.Contains("Message: boom", content);
    }

    [Fact]
    public void Write_CapturesStackTrace()
    {
        ErrorLogger.Enabled = true;
        Exception captured;
        try
        {
            throw new ArgumentNullException("arg");
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        ErrorLogger.Write("generate", "batch=2", captured);
        var content = File.ReadAllText(_logPath);
        Assert.Contains("Stack:", content);
        Assert.Contains("ErrorLoggerTests", content);
    }

    [Fact]
    public void Write_CapturesResponseBodyTruncatedTo500Chars()
    {
        ErrorLogger.Enabled = true;
        var bigBody = new string('x', 800);
        ErrorLogger.Write("critic", "test_id=TC-001",
            new HttpRequestException("429"), responseBody: bigBody);

        var content = File.ReadAllText(_logPath);
        Assert.Contains("Response: " + new string('x', 500) + "...[truncated]", content);
    }

    [Fact]
    public void Write_CapturesRetryAfter()
    {
        ErrorLogger.Enabled = true;
        ErrorLogger.Write("critic", "test_id=TC-001",
            new HttpRequestException("429"),
            responseBody: "{\"error\":\"rate\"}",
            retryAfter: "2");

        var content = File.ReadAllText(_logPath);
        Assert.Contains("Retry-After: 2", content);
    }

    [Fact]
    public void Write_MultipleErrors_AppendsBoth()
    {
        ErrorLogger.Enabled = true;
        ErrorLogger.Write("critic", "test_id=TC-001", new Exception("first"));
        ErrorLogger.Write("critic", "test_id=TC-002", new Exception("second"));
        var content = File.ReadAllText(_logPath);
        Assert.Contains("Message: first", content);
        Assert.Contains("Message: second", content);
    }

    [Fact]
    public async Task Write_ConcurrentCalls_NoCorruption()
    {
        ErrorLogger.Enabled = true;
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            ErrorLogger.Write("critic", $"test_id=TC-{i:000}", new Exception($"err-{i}")))).ToArray();
        await Task.WhenAll(tasks);

        var content = File.ReadAllText(_logPath);
        for (int i = 0; i < 50; i++)
        {
            Assert.Contains($"test_id=TC-{i:000}", content);
            Assert.Contains($"err-{i}", content);
        }
    }

    [Fact]
    public void IsRateLimit_HttpRequestExceptionWith429_ReturnsTrue()
    {
        var ex = new HttpRequestException("Too Many Requests", null, HttpStatusCode.TooManyRequests);
        Assert.True(ErrorLogger.IsRateLimit(ex));
    }

    [Fact]
    public void IsRateLimit_MessageContainsRateLimit_ReturnsTrue()
    {
        Assert.True(ErrorLogger.IsRateLimit(new Exception("rate_limit_exceeded")));
        Assert.True(ErrorLogger.IsRateLimit(new Exception("Rate limit reached for gpt-4.1")));
        Assert.True(ErrorLogger.IsRateLimit(new Exception("HTTP 429")));
    }

    [Fact]
    public void IsRateLimit_GenericException_ReturnsFalse()
    {
        Assert.False(ErrorLogger.IsRateLimit(new InvalidOperationException("oops")));
        Assert.False(ErrorLogger.IsRateLimit(new TimeoutException("slow")));
    }
}
