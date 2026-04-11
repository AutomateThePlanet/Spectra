using Spectra.CLI.Infrastructure;
using Xunit;

namespace Spectra.CLI.Tests.Infrastructure;

// DebugLoggerTests mutates process-global state (Directory.SetCurrentDirectory
// and DebugLogger.Enabled / .LogFile). Put them in a shared collection so they
// never run in parallel with each other, and wrap each test method in a
// cross-test lock so xUnit's parallel class execution cannot stomp on the CWD
// while other tests are running.
[CollectionDefinition("DebugLogger", DisableParallelization = true)]
public class DebugLoggerCollection { }

[Collection("DebugLogger")]
public class DebugLoggerTests : IDisposable
{
    private static readonly object _globalLock = new();
    private readonly IDisposable _lockHold;
    private sealed class LockHold : IDisposable
    {
        private readonly object _obj;
        public LockHold(object obj) { _obj = obj; Monitor.Enter(_obj); }
        public void Dispose() { Monitor.Exit(_obj); }
    }

    private readonly string _tempDir;
    private readonly bool _previousEnabled;
    private readonly string _previousLogFile;
    private readonly string _previousMode;

    public DebugLoggerTests()
    {
        _lockHold = new LockHold(_globalLock);
        _previousEnabled = DebugLogger.Enabled;
        _previousLogFile = DebugLogger.LogFile;
        _previousMode = DebugLogger.Mode;
        _tempDir = Path.Combine(Path.GetTempPath(), "spectra-debuglogger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        // Write using an absolute path — do NOT mutate the process CWD, since
        // xUnit may run other test classes in parallel that depend on it.
        DebugLogger.LogFile = Path.Combine(_tempDir, ".spectra-debug.log");
        DebugLogger.Mode = "append";
    }

    public void Dispose()
    {
        DebugLogger.Enabled = _previousEnabled;
        DebugLogger.LogFile = _previousLogFile;
        DebugLogger.Mode = _previousMode;
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* cleanup is best-effort */ }
        _lockHold.Dispose();
    }

    [Fact]
    public void Disabled_NoFileWritten()
    {
        DebugLogger.Enabled = false;
        DebugLogger.Append("generate", "BATCH OK");
        DebugLogger.AppendAi("generate", "BATCH OK elapsed=1.0s", "gpt-4o", "github-models", 100, 200);

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Enabled_FileWritten()
    {
        DebugLogger.Enabled = true;
        DebugLogger.Append("generate", "BATCH OK");

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("[generate]", content);
        Assert.Contains("BATCH OK", content);
    }

    [Fact]
    public void AppendAi_KnownTokens_LineEndsWithAllFields()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("generate", "BATCH OK elapsed=1.0s",
            "gpt-4o", "github-models", 4521, 3890);

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        var content = File.ReadAllText(path);
        Assert.Contains("model=gpt-4o", content);
        Assert.Contains("provider=github-models", content);
        Assert.Contains("tokens_in=4521", content);
        Assert.Contains("tokens_out=3890", content);
    }

    [Fact]
    public void AppendAi_NullTokens_RendersQuestionMarks()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("critic ", "CRITIC OK verdict=grounded",
            "gpt-4o-mini", "azure-openai", null, null);

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        var content = File.ReadAllText(path);
        Assert.Contains("model=gpt-4o-mini", content);
        Assert.Contains("provider=azure-openai", content);
        Assert.Contains("tokens_in=?", content);
        Assert.Contains("tokens_out=?", content);
    }

    [Fact]
    public void AppendAi_NullModelProvider_RendersQuestionMarks()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("analyze ", "ANALYSIS ERROR", null, null, null, null);

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        var content = File.ReadAllText(path);
        Assert.Contains("model=?", content);
        Assert.Contains("provider=?", content);
    }

    [Fact]
    public void Append_NonAiMessage_UnchangedFormat()
    {
        DebugLogger.Enabled = true;
        DebugLogger.Append("testimize", "TESTIMIZE HEALTHY command=testimize-mcp");

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        var content = File.ReadAllText(path);
        Assert.Contains("TESTIMIZE HEALTHY", content);
        // Non-AI line must NOT include the AI suffix fields.
        Assert.DoesNotContain("model=", content);
        Assert.DoesNotContain("provider=", content);
        Assert.DoesNotContain("tokens_in=", content);
    }

    [Fact]
    public void AppendAi_Estimated_PrefixesTildeOnTokenFields()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("generate", "BATCH OK elapsed=1.0s",
            "gpt-4.1", "github-models", 4521, 3890, estimated: true);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        Assert.Contains("~tokens_in=4521", content);
        Assert.Contains("~tokens_out=3890", content);
        // Must NOT contain the unprefixed forms (since the whole suffix uses ~)
        Assert.DoesNotContain(" tokens_in=4521", content);
        Assert.DoesNotContain(" tokens_out=3890", content);
    }

    [Fact]
    public void AppendAi_NotEstimated_NoTildePrefix()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("generate", "BATCH OK elapsed=1.0s",
            "gpt-4.1", "github-models", 100, 50, estimated: false);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        Assert.Contains("tokens_in=100", content);
        Assert.Contains("tokens_out=50", content);
        Assert.DoesNotContain("~tokens_in", content);
        Assert.DoesNotContain("~tokens_out", content);
    }

    [Fact]
    public void AppendAi_EstimatedWithNullTokens_StillRendersQuestionMarks()
    {
        DebugLogger.Enabled = true;
        DebugLogger.AppendAi("analyze ", "ANALYSIS ERROR",
            "gpt-4.1", "github-models", null, null, estimated: true);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        Assert.Contains("~tokens_in=?", content);
        Assert.Contains("~tokens_out=?", content);
    }

    [Fact]
    public void BeginRun_Disabled_NoFileWritten()
    {
        DebugLogger.Enabled = false;
        DebugLogger.BeginRun();

        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void BeginRun_Overwrite_TruncatesAndWritesHeader()
    {
        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        // Seed existing content.
        File.WriteAllText(path, "STALE CONTENT FROM PRIOR RUN\n");

        DebugLogger.Enabled = true;
        DebugLogger.Mode = "overwrite";
        DebugLogger.BeginRun();

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("STALE CONTENT", content);
        Assert.Contains("=== SPECTRA v", content);
        Assert.Contains(" ===", content);
        Assert.Contains("──────────", content); // separator
    }

    [Fact]
    public void BeginRun_Append_ExistingFile_PrependsSeparatorAndHeader()
    {
        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        File.WriteAllText(path, "PRIOR RUN LINE\n");

        DebugLogger.Enabled = true;
        DebugLogger.Mode = "append";
        DebugLogger.BeginRun();

        var content = File.ReadAllText(path);
        // Prior run content preserved
        Assert.Contains("PRIOR RUN LINE", content);
        // New header present
        Assert.Contains("=== SPECTRA v", content);
        Assert.Contains("──────────", content);
        // Prior content comes BEFORE the separator
        var separatorIndex = content.IndexOf("──────────");
        var priorIndex = content.IndexOf("PRIOR RUN LINE");
        Assert.True(priorIndex < separatorIndex, "Separator should appear after prior content in append mode");
    }

    [Fact]
    public void BeginRun_Append_MissingFile_CreatesWithHeader()
    {
        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        Assert.False(File.Exists(path));

        DebugLogger.Enabled = true;
        DebugLogger.Mode = "append";
        DebugLogger.BeginRun();

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("=== SPECTRA v", content);
        Assert.Contains("──────────", content);
    }

    [Fact]
    public void BeginRun_UnknownMode_TreatedAsAppend()
    {
        var path = Path.Combine(_tempDir, ".spectra-debug.log");
        File.WriteAllText(path, "PRIOR CONTENT\n");

        DebugLogger.Enabled = true;
        DebugLogger.Mode = "garbage-value";
        DebugLogger.BeginRun();

        // Unknown values fall back to append, so prior content is preserved.
        var content = File.ReadAllText(path);
        Assert.Contains("PRIOR CONTENT", content);
        Assert.Contains("=== SPECTRA v", content);
    }

    [Fact]
    public void BeginRun_HeaderIncludesIsoTimestamp()
    {
        DebugLogger.Enabled = true;
        DebugLogger.Mode = "overwrite";
        DebugLogger.BeginRun();

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        // ISO-8601 UTC timestamp pattern: YYYY-MM-DDTHH:MM:SSZ
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", content);
    }

    [Fact]
    public void BeginRun_ThenAppendAi_LogContainsHeaderAndAiLine()
    {
        DebugLogger.Enabled = true;
        DebugLogger.Mode = "overwrite";
        DebugLogger.BeginRun();
        DebugLogger.AppendAi("generate", "BATCH OK elapsed=1.0s",
            "gpt-4.1", "github-models", 100, 50);

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        Assert.Contains("=== SPECTRA v", content);
        Assert.Contains("BATCH OK", content);
        Assert.Contains("tokens_in=100", content);
    }

    [Fact]
    public void BeginRun_ThenAppendTestimize_LinesSurvive()
    {
        // Regression test: testimize lifecycle must be written AFTER BeginRun
        // and survive the truncate (in overwrite mode).
        DebugLogger.Enabled = true;
        DebugLogger.Mode = "overwrite";
        DebugLogger.BeginRun();
        DebugLogger.Append("generate", "TESTIMIZE DISABLED");
        DebugLogger.Append("generate", "TESTIMIZE HEALTHY command=testimize-mcp");
        DebugLogger.Append("generate", "TESTIMIZE DISPOSED");

        var content = File.ReadAllText(Path.Combine(_tempDir, ".spectra-debug.log"));
        Assert.Contains("=== SPECTRA v", content);
        Assert.Contains("TESTIMIZE DISABLED", content);
        Assert.Contains("TESTIMIZE HEALTHY", content);
        Assert.Contains("TESTIMIZE DISPOSED", content);
    }
}
