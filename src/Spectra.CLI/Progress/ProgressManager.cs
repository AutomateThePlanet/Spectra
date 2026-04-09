using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Results;

namespace Spectra.CLI.Progress;

/// <summary>
/// Shared service for writing .spectra-result.json and .spectra-progress.html files.
/// Encapsulates the progress/result file lifecycle: Reset → Start → Update(n) → Complete | Fail.
/// </summary>
public sealed class ProgressManager
{
    private static readonly JsonSerializerOptions ResultFileOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _command;
    private readonly string[] _phases;
    private readonly string _title;
    private readonly string _resultPath;
    private readonly string _progressPath;

    public ProgressManager(string command, string[] phases, string? title = null)
    {
        _command = command;
        _phases = phases;
        _title = title ?? command;
        _resultPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        _progressPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-progress.html");
    }

    /// <summary>For testing: create with explicit paths.</summary>
    internal ProgressManager(string command, string[] phases, string resultPath, string progressPath, string? title = null)
    {
        _command = command;
        _phases = phases;
        _title = title ?? command;
        _resultPath = resultPath;
        _progressPath = progressPath;
    }

    public string ResultPath => _resultPath;
    public string ProgressPath => _progressPath;

    /// <summary>
    /// Reset stale files from previous runs.
    /// Writes a "starting" placeholder HTML so the browser shows a loading state
    /// (with auto-refresh enabled) instead of the previous run's completed state.
    /// </summary>
    public void Reset()
    {
        try
        {
            if (File.Exists(_resultPath))
                File.Delete(_resultPath);

            var placeholder = new CommandResult
            {
                Command = _command,
                Status = "starting",
                Message = "Initializing..."
            };
            var json = JsonSerializer.Serialize(placeholder, ResultFileOptions);
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal: false, _title);
        }
        catch
        {
            // Non-critical — don't fail the command if cleanup fails
        }
    }

    /// <summary>Create initial progress HTML with first phase active.</summary>
    public void Start(string? message = null)
    {
        try
        {
            var result = new CommandResult
            {
                Command = _command,
                Status = _phases.Length > 0 ? _phases[0] : "started",
                Message = message
            };
            var json = Serialize(result);
            FlushWriteFile(_resultPath, json);
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal: false, _title);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Update current phase and status message.</summary>
    public void Update(string status, string? message = null)
    {
        try
        {
            var result = new CommandResult
            {
                Command = _command,
                Status = status,
                Message = message
            };
            var json = Serialize(result);
            FlushWriteFile(_resultPath, json);
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal: false, _title);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Update with a typed result object for richer progress data.</summary>
    public void Update(CommandResult result)
    {
        try
        {
            var json = SerializeTyped(result);
            FlushWriteFile(_resultPath, json);

            var isTerminal = result.Status is "completed" or "failed";
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal, _title);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Write final result and mark progress as complete.</summary>
    public void Complete(CommandResult result)
    {
        try
        {
            var json = SerializeTyped(result);
            FlushWriteFile(_resultPath, json);
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal: true, _title);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Write failure result and mark progress as failed.</summary>
    public void Fail(string error, CommandResult? partialResult = null)
    {
        try
        {
            CommandResult result = partialResult ?? new ErrorResult
            {
                Command = _command,
                Status = "failed",
                Error = error
            };
            if (partialResult != null)
                partialResult.Message ??= error;

            var json = SerializeTyped(result);
            FlushWriteFile(_resultPath, json);
            ProgressPageWriter.WriteProgressPage(_progressPath, json, isTerminal: true, _title);
        }
        catch
        {
            // Non-critical
        }
    }

    /// <summary>Write result file only (no progress HTML). For fast commands.</summary>
    public void WriteResultOnly(CommandResult result)
    {
        try
        {
            var json = SerializeTyped(result);
            FlushWriteFile(_resultPath, json);
        }
        catch
        {
            // Non-critical
        }
    }

    private static string Serialize(CommandResult result)
    {
        return JsonSerializer.Serialize(result, result.GetType(), ResultFileOptions);
    }

    private static string SerializeTyped(CommandResult result)
    {
        return JsonSerializer.Serialize(result, result.GetType(), ResultFileOptions);
    }

    private static void FlushWriteFile(string path, string json)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(fs);
        writer.Write(json);
        writer.Flush();
        fs.Flush(true); // Force OS to flush to disk
    }
}
