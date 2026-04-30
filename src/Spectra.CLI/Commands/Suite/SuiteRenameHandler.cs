using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Suite;

/// <summary>
/// Spec 040: implements <c>spectra suite rename &lt;old&gt; &lt;new&gt;</c>.
/// Atomic with best-effort rollback on partial failure (Decision 8).
/// </summary>
public sealed class SuiteRenameHandler
{
    private static readonly Regex SuiteNameRegex = new("^[a-z0-9][a-z0-9_-]*$", RegexOptions.Compiled);

    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public SuiteRenameHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(
        string oldName,
        string newName,
        bool dryRun,
        bool force,
        bool noInteraction,
        CancellationToken ct = default)
    {
        var workspace = Directory.GetCurrentDirectory();
        var testCasesDir = ResolveTestCasesDir(workspace);
        var oldDir = Path.Combine(testCasesDir, oldName);
        var newDir = Path.Combine(testCasesDir, newName);

        if (!Directory.Exists(oldDir))
        {
            EmitFailure(oldName, newName, "SUITE_NOT_FOUND", $"Suite not found: {oldName}");
            return ExitCodes.NotFound;
        }

        if (!SuiteNameRegex.IsMatch(newName))
        {
            EmitFailure(oldName, newName, "INVALID_SUITE_NAME",
                $"Suite name '{newName}' violates the rule: must start with a letter or digit and contain only lowercase letters, digits, hyphens, and underscores.");
            return ExitCodes.InvalidSuiteName;
        }

        if (Directory.Exists(newDir))
        {
            EmitFailure(oldName, newName, "SUITE_ALREADY_EXISTS", $"Target suite already exists: {newName}");
            return ExitCodes.SuiteAlreadyExists;
        }

        // Pre-flight: snapshot config in memory for rollback
        var configPath = Path.Combine(workspace, "spectra.config.json");
        string? configSnapshot = null;
        if (File.Exists(configPath))
        {
            try { configSnapshot = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false); }
            catch { /* fall through; rollback may be partial */ }
        }

        var (selectionsToUpdate, hasConfigBlock) = await PlanConfigUpdatesAsync(configPath, oldName, ct).ConfigureAwait(false);

        if (dryRun)
        {
            var dryResult = new SuiteRenameResult
            {
                Command = "suite rename",
                Status = "completed",
                DryRun = true,
                OldName = oldName,
                NewName = newName,
                DirectoryRenamed = false,
                IndexUpdated = false,
                SelectionsUpdated = selectionsToUpdate,
                ConfigBlockRenamed = hasConfigBlock,
                Message = $"Dry run: would rename '{oldName}' → '{newName}', update {selectionsToUpdate.Count} selection(s){(hasConfigBlock ? ", and re-key config block" : string.Empty)}."
            };
            EmitResult(dryResult);
            return ExitCodes.Success;
        }

        if (!force && !noInteraction && _outputFormat == OutputFormat.Human)
        {
            Console.WriteLine();
            Console.WriteLine($"Rename suite '{oldName}' → '{newName}'?");
            if (selectionsToUpdate.Count > 0) Console.WriteLine($"  {selectionsToUpdate.Count} saved selection(s) will be updated");
            if (hasConfigBlock) Console.WriteLine("  suites.{old} config block will be re-keyed");
            Console.Write("[y/N]: ");
            var input = Console.ReadLine()?.Trim();
            if (!string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) && !string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase))
            {
                EmitResult(new SuiteRenameResult
                {
                    Command = "suite rename",
                    Status = "completed",
                    OldName = oldName,
                    NewName = newName,
                    Message = "Cancelled by user."
                });
                return ExitCodes.Success;
            }
        }

        // Execute: directory move → index rewrite → config rewrite
        // On any failure, roll back what we've done so far.

        // 1. Rename directory
        try
        {
            Directory.Move(oldDir, newDir);
        }
        catch (Exception ex)
        {
            EmitFailure(oldName, newName, "RENAME_FAILED", $"Failed to rename directory: {ex.Message}");
            return ExitCodes.Error;
        }

        // 2. Update _index.json `suite` field
        var indexUpdated = false;
        var newIndexPath = Path.Combine(newDir, "_index.json");
        if (File.Exists(newIndexPath))
        {
            try
            {
                var writer = new IndexWriter();
                var index = await writer.ReadAsync(newIndexPath, ct).ConfigureAwait(false);
                if (index is not null)
                {
                    var renamed = new MetadataIndex
                    {
                        Suite = newName,
                        GeneratedAt = DateTime.UtcNow,
                        Tests = index.Tests
                    };
                    await writer.WriteAsync(newIndexPath, renamed, ct).ConfigureAwait(false);
                    indexUpdated = true;
                }
            }
            catch (Exception ex)
            {
                // Rollback step 1
                try { Directory.Move(newDir, oldDir); } catch { /* best-effort */ }
                EmitFailure(oldName, newName, "INDEX_UPDATE_FAILED", $"Failed to update _index.json: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        // 3. Update config (selections + suites block)
        var configBlockRenamed = false;
        var selectionsUpdated = Array.Empty<string>() as IReadOnlyList<string>;
        if (File.Exists(configPath))
        {
            try
            {
                (selectionsUpdated, configBlockRenamed) = await ApplyConfigRenameAsync(configPath, oldName, newName, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Rollback steps 1 and 2 — restore config from in-memory snapshot
                try
                {
                    if (configSnapshot is not null)
                    {
                        await AtomicFileWriter.WriteAllTextAsync(configPath, configSnapshot, ct).ConfigureAwait(false);
                    }
                    if (Directory.Exists(newDir) && !Directory.Exists(oldDir))
                    {
                        Directory.Move(newDir, oldDir);
                    }
                }
                catch { /* best-effort */ }

                EmitFailure(oldName, newName, "CONFIG_UPDATE_FAILED", $"Failed to update config: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        var result = new SuiteRenameResult
        {
            Command = "suite rename",
            Status = "completed",
            DryRun = false,
            OldName = oldName,
            NewName = newName,
            DirectoryRenamed = true,
            IndexUpdated = indexUpdated,
            SelectionsUpdated = selectionsUpdated,
            ConfigBlockRenamed = configBlockRenamed,
            Message = $"Renamed '{oldName}' → '{newName}'."
        };
        EmitResult(result);
        return ExitCodes.Success;
    }

    private static async Task<(IReadOnlyList<string> SelectionsToUpdate, bool HasConfigBlock)> PlanConfigUpdatesAsync(
        string configPath, string oldName, CancellationToken ct)
    {
        if (!File.Exists(configPath))
        {
            return (Array.Empty<string>(), false);
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var hasBlock = false;
            if (root.TryGetProperty("suites", out var suites) && suites.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in suites.EnumerateObject())
                {
                    if (string.Equals(prop.Name, oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        hasBlock = true;
                        break;
                    }
                }
            }

            var selUpdates = new List<string>();
            if (root.TryGetProperty("selections", out var sel) && sel.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in sel.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object &&
                        prop.Value.TryGetProperty("suites", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in arr.EnumerateArray())
                        {
                            if (s.ValueKind == JsonValueKind.String &&
                                string.Equals(s.GetString(), oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                selUpdates.Add(prop.Name);
                                break;
                            }
                        }
                    }
                }
            }

            return (selUpdates, hasBlock);
        }
        catch
        {
            return (Array.Empty<string>(), false);
        }
    }

    private static async Task<(IReadOnlyList<string> SelectionsUpdated, bool ConfigBlockRenamed)> ApplyConfigRenameAsync(
        string configPath, string oldName, string newName, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText())
            ?? new Dictionary<string, object>();

        var selectionsUpdated = new List<string>();
        var blockRenamed = false;

        // Re-key suites.<oldName> → suites.<newName>
        if (dict.TryGetValue("suites", out var suitesObj) && suitesObj is JsonElement suitesEl && suitesEl.ValueKind == JsonValueKind.Object)
        {
            var suitesDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(suitesEl.GetRawText()) ?? new();
            foreach (var key in suitesDict.Keys.ToList())
            {
                if (string.Equals(key, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    var value = suitesDict[key];
                    suitesDict.Remove(key);
                    suitesDict[newName] = value;
                    blockRenamed = true;
                }
            }
            dict["suites"] = JsonSerializer.SerializeToElement(suitesDict);
        }

        // Update selections.*.suites arrays
        if (dict.TryGetValue("selections", out var selObj) && selObj is JsonElement selEl && selEl.ValueKind == JsonValueKind.Object)
        {
            var newSelections = new Dictionary<string, JsonElement>();
            foreach (var prop in selEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var selDict = JsonSerializer.Deserialize<Dictionary<string, object>>(prop.Value.GetRawText()) ?? new();
                    if (selDict.TryGetValue("suites", out var suitesArr) && suitesArr is JsonElement arrEl && arrEl.ValueKind == JsonValueKind.Array)
                    {
                        var suitesList = arrEl.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
                        var changed = false;
                        for (var i = 0; i < suitesList.Count; i++)
                        {
                            if (string.Equals(suitesList[i], oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                suitesList[i] = newName;
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            selDict["suites"] = suitesList;
                            selectionsUpdated.Add(prop.Name);
                        }
                    }
                    newSelections[prop.Name] = JsonSerializer.SerializeToElement(selDict);
                }
                else
                {
                    newSelections[prop.Name] = prop.Value.Clone();
                }
            }
            dict["selections"] = JsonSerializer.SerializeToElement(newSelections);
        }

        if (selectionsUpdated.Count == 0 && !blockRenamed)
        {
            return (Array.Empty<string>(), false);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var newJson = JsonSerializer.Serialize(dict, options);
        await AtomicFileWriter.WriteAllTextAsync(configPath, newJson, ct).ConfigureAwait(false);

        return (selectionsUpdated, blockRenamed);
    }

    private static string ResolveTestCasesDir(string workspace)
    {
        var configPath = Path.Combine(workspace, "spectra.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (!string.IsNullOrWhiteSpace(config?.Tests?.Dir))
                {
                    return Path.Combine(workspace, config.Tests.Dir);
                }
            }
            catch { /* fall through */ }
        }
        return Path.Combine(workspace, "test-cases");
    }

    private void EmitFailure(string oldName, string newName, string error, string message)
    {
        var result = new SuiteRenameResult
        {
            Command = "suite rename",
            Status = "failed",
            OldName = oldName,
            NewName = newName,
            Error = error,
            Message = message
        };
        EmitResult(result);
        if (_outputFormat == OutputFormat.Human)
        {
            Console.Error.WriteLine(message);
        }
    }

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            using var fs = new FileStream(resultPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            writer.Write(JsonSerializer.Serialize(result, result.GetType(), options));
            writer.Flush();
            fs.Flush(true);
        }
        catch { /* non-critical */ }
    }
}
