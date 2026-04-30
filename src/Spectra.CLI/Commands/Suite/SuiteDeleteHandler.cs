using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Suite;

/// <summary>
/// Spec 040: implements <c>spectra suite delete &lt;name&gt;</c>.
/// </summary>
public sealed class SuiteDeleteHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public SuiteDeleteHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(
        string suiteName,
        bool dryRun,
        bool force,
        bool noInteraction,
        CancellationToken ct = default)
    {
        var workspace = Directory.GetCurrentDirectory();
        var testCasesDir = ResolveTestCasesDir(workspace);
        var suiteDir = Path.Combine(testCasesDir, suiteName);

        if (!Directory.Exists(suiteDir))
        {
            EmitFailure(suiteName, "SUITE_NOT_FOUND", $"Suite not found: {suiteName}");
            return ExitCodes.NotFound;
        }

        // Pre-flight: count automation links and external depends_on references.
        var (testCount, automationFiles) = await ScanSuiteAsync(suiteDir, ct).ConfigureAwait(false);
        var externalDeps = await CountExternalDepsAsync(testCasesDir, suiteName, ct).ConfigureAwait(false);

        if (!force && automationFiles.Count > 0)
        {
            EmitFailure(suiteName, "AUTOMATION_LINKED",
                $"{automationFiles.Count} test(s) have automation links. Use --force to proceed.",
                testsRemoved: 0,
                strandedFiles: automationFiles);
            return ExitCodes.AutomationLinked;
        }

        if (!force && externalDeps.Count > 0)
        {
            EmitFailure(suiteName, "EXTERNAL_DEPENDENCIES",
                $"{externalDeps.Count} external depends_on reference(s) target tests in this suite. Use --force to proceed.",
                externalCleanup: externalDeps);
            return ExitCodes.ExternalDependencies;
        }

        if (dryRun)
        {
            var dryResult = new SuiteDeleteResult
            {
                Command = "suite delete",
                Status = "completed",
                DryRun = true,
                Suite = suiteName,
                TestsRemoved = testCount,
                StrandedAutomationCount = automationFiles.Count,
                StrandedAutomationFiles = automationFiles,
                ExternalDependencyCleanup = externalDeps,
                Message = $"Dry run: would delete suite '{suiteName}' ({testCount} tests, {externalDeps.Count} external deps cleaned)."
            };
            EmitResult(dryResult);
            return ExitCodes.Success;
        }

        if (!force && !noInteraction && _outputFormat == OutputFormat.Human)
        {
            if (!Confirm(suiteName, testCount, automationFiles.Count, externalDeps.Count))
            {
                EmitResult(new SuiteDeleteResult
                {
                    Command = "suite delete",
                    Status = "completed",
                    Suite = suiteName,
                    Message = "Cancelled by user."
                });
                return ExitCodes.Success;
            }
        }

        // Execute
        // 1. Cascade external depends_on cleanup
        await CascadeRemoveDependsOnAsync(testCasesDir, suiteName, externalDeps, ct).ConfigureAwait(false);

        // 2. Delete the suite directory recursively
        try
        {
            Directory.Delete(suiteDir, recursive: true);
        }
        catch (Exception ex)
        {
            EmitFailure(suiteName, "DELETE_FAILED", $"Failed to delete suite directory: {ex.Message}");
            return ExitCodes.Error;
        }

        // 3. Update spectra.config.json (selections + suite-specific block)
        var (selectionsUpdated, configBlockRemoved) = await UpdateConfigAsync(workspace, suiteName, ct).ConfigureAwait(false);

        var result = new SuiteDeleteResult
        {
            Command = "suite delete",
            Status = "completed",
            Suite = suiteName,
            TestsRemoved = testCount,
            StrandedAutomationCount = automationFiles.Count,
            StrandedAutomationFiles = automationFiles,
            ExternalDependencyCleanup = externalDeps,
            SelectionsUpdated = selectionsUpdated,
            ConfigBlockRemoved = configBlockRemoved,
            Message = $"Deleted suite '{suiteName}' ({testCount} tests). Use Git to recover."
        };
        EmitResult(result);
        return ExitCodes.Success;
    }

    private static async Task<(int TestCount, IReadOnlyList<string> AutomationFiles)> ScanSuiteAsync(string suiteDir, CancellationToken ct)
    {
        var testCount = 0;
        var automationFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(suiteDir, "*.md", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (name.StartsWith('_'))
            {
                continue;
            }
            testCount++;

            try
            {
                var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                foreach (Match m in Regex.Matches(content, @"^\s*-\s*[""']?(?<path>[^""'\s]+)[""']?\s*$", RegexOptions.Multiline))
                {
                    // crude — only match if the preceding line is automated_by:
                    // The structured frontmatter parser would be more accurate.
                }

                // Lighter approach: scan for the literal `automated_by:` block
                var auto = Regex.Match(content, @"automated_by:\s*\n((?:\s+-\s.*\n?)+)", RegexOptions.IgnoreCase);
                if (auto.Success)
                {
                    foreach (Match item in Regex.Matches(auto.Groups[1].Value, @"^\s+-\s+[""']?(?<path>[^""'\s]+)[""']?\s*$", RegexOptions.Multiline))
                    {
                        automationFiles.Add(item.Groups["path"].Value);
                    }
                }
            }
            catch (IOException) { /* skip */ }
        }

        return (testCount, automationFiles.ToList());
    }

    private static async Task<List<ExternalDependencyCleanup>> CountExternalDepsAsync(
        string testCasesDir, string targetSuite, CancellationToken ct)
    {
        var idsInTarget = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetSuiteDir = Path.Combine(testCasesDir, targetSuite);
        if (Directory.Exists(targetSuiteDir))
        {
            foreach (var file in Directory.EnumerateFiles(targetSuiteDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file).StartsWith('_')) continue;
                try
                {
                    var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var idMatch = Regex.Match(content, @"^\s*id:\s*[""']?(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)[""']?\s*$", RegexOptions.Multiline);
                    if (idMatch.Success)
                    {
                        idsInTarget.Add(idMatch.Groups["id"].Value);
                    }
                }
                catch (IOException) { /* skip */ }
            }
        }

        if (idsInTarget.Count == 0)
        {
            return new List<ExternalDependencyCleanup>();
        }

        var results = new Dictionary<(string Suite, string TestId), List<string>>();

        foreach (var suiteDir in Directory.EnumerateDirectories(testCasesDir))
        {
            ct.ThrowIfCancellationRequested();
            var suiteName = Path.GetFileName(suiteDir);
            if (string.Equals(suiteName, targetSuite, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var file in Directory.EnumerateFiles(suiteDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file).StartsWith('_')) continue;
                string content;
                try { content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false); } catch (IOException) { continue; }

                var idMatch = Regex.Match(content, @"^\s*id:\s*[""']?(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)[""']?\s*$", RegexOptions.Multiline);
                if (!idMatch.Success) continue;
                var dependentId = idMatch.Groups["id"].Value;

                foreach (Match m in Regex.Matches(content, @"^\s+-\s+[""']?(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)[""']?\s*$", RegexOptions.Multiline))
                {
                    var dep = m.Groups["id"].Value;
                    if (idsInTarget.Contains(dep))
                    {
                        var key = (suiteName, dependentId);
                        if (!results.TryGetValue(key, out var list))
                        {
                            list = new List<string>();
                            results[key] = list;
                        }
                        if (!list.Contains(dep))
                        {
                            list.Add(dep);
                        }
                    }
                }
            }
        }

        return results.Select(kv => new ExternalDependencyCleanup
        {
            TestId = kv.Key.TestId,
            Suite = kv.Key.Suite,
            RemovedDeps = kv.Value
        }).ToList();
    }

    private static async Task CascadeRemoveDependsOnAsync(
        string testCasesDir,
        string targetSuite,
        IReadOnlyList<ExternalDependencyCleanup> cleanups,
        CancellationToken ct)
    {
        var idsToRemove = cleanups.SelectMany(c => c.RemovedDeps).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (idsToRemove.Count == 0) return;

        foreach (var suiteDir in Directory.EnumerateDirectories(testCasesDir))
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFileName(suiteDir), targetSuite, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var file in Directory.EnumerateFiles(suiteDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file).StartsWith('_')) continue;
                string content;
                try { content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false); } catch (IOException) { continue; }

                var rewritten = Regex.Replace(content,
                    @"^(\s+-\s+[""']?)(?<id>[A-Za-z][A-Za-z0-9_-]*-\d+)([""']?\s*)$",
                    m => idsToRemove.Contains(m.Groups["id"].Value) ? string.Empty : m.Value,
                    RegexOptions.Multiline);

                rewritten = Regex.Replace(rewritten, @"\r?\n\r?\n+", Environment.NewLine + Environment.NewLine);

                if (rewritten != content)
                {
                    await Spectra.Core.Index.AtomicFileWriter.WriteAllTextAsync(file, rewritten, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<(IReadOnlyList<string> SelectionsUpdated, bool ConfigBlockRemoved)> UpdateConfigAsync(
        string workspace, string suiteName, CancellationToken ct)
    {
        var configPath = Path.Combine(workspace, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            return (Array.Empty<string>(), false);
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var (newJson, selectionsUpdated, blockRemoved) = ScrubSuiteFromConfig(root, suiteName);
            if (newJson is not null)
            {
                await Spectra.Core.Index.AtomicFileWriter.WriteAllTextAsync(configPath, newJson, ct).ConfigureAwait(false);
            }
            return (selectionsUpdated, blockRemoved);
        }
        catch
        {
            return (Array.Empty<string>(), false);
        }
    }

    private static (string? NewJson, IReadOnlyList<string> SelectionsUpdated, bool BlockRemoved) ScrubSuiteFromConfig(JsonElement root, string suiteName)
    {
        // Build a mutable copy by serializing-deserializing into a Dictionary<string, object>
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText())
            ?? new Dictionary<string, object>();

        var selectionsUpdated = new List<string>();
        var blockRemoved = false;

        // Remove suites.<name> block
        if (dict.TryGetValue("suites", out var suitesObj) && suitesObj is JsonElement suitesEl && suitesEl.ValueKind == JsonValueKind.Object)
        {
            var suitesDict = JsonSerializer.Deserialize<Dictionary<string, object>>(suitesEl.GetRawText()) ?? new();
            var keys = suitesDict.Keys.ToList();
            foreach (var key in keys)
            {
                if (string.Equals(key, suiteName, StringComparison.OrdinalIgnoreCase))
                {
                    suitesDict.Remove(key);
                    blockRemoved = true;
                }
            }
            dict["suites"] = JsonSerializer.SerializeToElement(suitesDict);
        }

        // Strip name from selections[*].suites arrays
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
                        var before = suitesList.Count;
                        suitesList.RemoveAll(s => string.Equals(s, suiteName, StringComparison.OrdinalIgnoreCase));
                        if (suitesList.Count != before)
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

        if (selectionsUpdated.Count == 0 && !blockRemoved)
        {
            return (null, Array.Empty<string>(), false);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        return (JsonSerializer.Serialize(dict, options), selectionsUpdated, blockRemoved);
    }

    private bool Confirm(string suiteName, int testCount, int automationCount, int externalDeps)
    {
        Console.WriteLine();
        Console.WriteLine($"Delete suite \"{suiteName}\" containing {testCount} tests?");
        if (automationCount > 0) Console.WriteLine($"  ⚠ {automationCount} test(s) have automation links (would be stranded)");
        if (externalDeps > 0) Console.WriteLine($"  ⚠ {externalDeps} external depends_on reference(s) will be cleaned up");
        Console.WriteLine();
        Console.WriteLine("This is a hard delete. Use Git to recover.");
        Console.Write("[y/N]: ");
        var input = Console.ReadLine()?.Trim();
        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private void EmitFailure(
        string suite, string error, string message,
        int testsRemoved = 0,
        IReadOnlyList<string>? strandedFiles = null,
        IReadOnlyList<ExternalDependencyCleanup>? externalCleanup = null)
    {
        var result = new SuiteDeleteResult
        {
            Command = "suite delete",
            Status = "failed",
            Suite = suite,
            TestsRemoved = testsRemoved,
            StrandedAutomationCount = strandedFiles?.Count ?? 0,
            StrandedAutomationFiles = strandedFiles ?? Array.Empty<string>(),
            ExternalDependencyCleanup = externalCleanup ?? Array.Empty<ExternalDependencyCleanup>(),
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
}
