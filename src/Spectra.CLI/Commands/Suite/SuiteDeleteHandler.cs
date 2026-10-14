using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Commands.Delete;
using Spectra.CLI.Generation.Plan;
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

        // Pre-flight: automation links, and (Spec 096) the same plan `spectra delete` makes — dependents in other suites
        // from frontmatter, scalar ones cleared through the lossless writer, list or unparseable ones only warned. The
        // suite's own baseline goes with its folder, so the plan reads no baseline and takes no baseline lock.
        var (testCount, automationFiles) = await ScanSuiteAsync(suiteDir, ct).ConfigureAwait(false);
        var service = await TestDeletionService.LoadAsync(workspace, testCasesDir, ct).ConfigureAwait(false);
        var plan = await service.PlanAsync(service.SuiteTargets(suiteName), includeBaseline: false, ct).ConfigureAwait(false);
        var leftovers = Leftovers(workspace, suiteName);
        var references = plan.ScalarDependents.Count + plan.Warnings.Count;

        if (!force && automationFiles.Count > 0)
        {
            EmitFailure(suiteName, "AUTOMATION_LINKED",
                $"{automationFiles.Count} test(s) have automation links. Use --force to proceed.",
                testsRemoved: 0,
                strandedFiles: automationFiles);
            return ExitCodes.AutomationLinked;
        }

        if (!force && references > 0)
        {
            EmitFailure(suiteName, "EXTERNAL_DEPENDENCIES",
                $"{references} external depends_on reference(s) target tests in this suite. Use --force to proceed.",
                externalCleanup: ExternalDependencies(testCasesDir, plan.ScalarDependents));
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
                ExternalDependencyCleanup = ExternalDependencies(testCasesDir, plan.ScalarDependents),
                DependentsCleaned = Dependents(service, testCasesDir, plan.ScalarDependents),
                Warnings = plan.Warnings,
                Leftovers = leftovers.Select(l => l.Relative).ToList(),
                VerdictFilesRemoved = plan.VerdictFiles.Select(service.Relative).ToList(),
                Message = $"Dry run: would delete suite '{suiteName}' ({testCount} tests, {plan.ScalarDependents.Count} external deps cleaned, "
                          + $"{plan.Warnings.Count} warning(s), {leftovers.Count} leftover(s) removed)."
            };
            EmitResult(dryResult);
            return ExitCodes.Success;
        }

        if (!force && !noInteraction && _outputFormat == OutputFormat.Human)
        {
            if (!Confirm(suiteName, testCount, automationFiles.Count, references))
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
        // 1. Dependents (lossless writer), the tests' staging and verdict files, the test files (Spec 096: TestDeletionService)
        var outcome = await service.ApplyAsync(plan, ct).ConfigureAwait(false);
        var warnings = plan.Warnings.Concat(outcome.Errors.Select(e => $"{e.Id}: {e.Message}")).ToList();

        // 2. The suite's leftovers: staging folders and generation plan (Spec 096, FR-005). Spec 098 (F3): the verdict
        //    files were removed by the service in step 1 and are reported once, under verdict_files_removed.
        var leftoversRemoved = RemoveLeftovers(leftovers, warnings);

        // 3. Delete the suite directory recursively (its _index.json and _source-baseline.json with it)
        try
        {
            Directory.Delete(suiteDir, recursive: true);
        }
        catch (Exception ex)
        {
            EmitFailure(suiteName, "DELETE_FAILED", $"Failed to delete suite directory: {ex.Message}");
            return ExitCodes.Error;
        }

        // 4. Update spectra.config.json (selections + suite-specific block)
        var (selectionsUpdated, configBlockRemoved) = await UpdateConfigAsync(workspace, suiteName, ct).ConfigureAwait(false);

        var failed = warnings.Count > plan.Warnings.Count;
        var result = new SuiteDeleteResult
        {
            Command = "suite delete",
            Status = failed ? "failed" : "completed",
            Suite = suiteName,
            TestsRemoved = testCount,
            StrandedAutomationCount = automationFiles.Count,
            StrandedAutomationFiles = automationFiles,
            ExternalDependencyCleanup = ExternalDependencies(testCasesDir, outcome.DependentsCleaned),
            DependentsCleaned = Dependents(service, testCasesDir, outcome.DependentsCleaned),
            Warnings = warnings,
            LeftoversRemoved = leftoversRemoved,
            VerdictFilesRemoved = outcome.VerdictFilesRemoved.Select(service.Relative).ToList(),
            SelectionsUpdated = selectionsUpdated,
            ConfigBlockRemoved = configBlockRemoved,
            Message = $"Deleted suite '{suiteName}' ({testCount} tests, {outcome.DependentsCleaned.Count} external deps cleaned, "
                      + $"{leftoversRemoved.Count} leftover(s) removed). Use Git to recover."
        };
        EmitResult(result);
        return failed ? ExitCodes.Error : ExitCodes.Success;
    }

    private static string SuiteOf(string testCasesDir, string file) =>
        Path.GetRelativePath(testCasesDir, file).Replace('\\', '/').Split('/')[0];

    /// <summary>Cleared scalar dependents in the pre-096 <c>external_dependency_cleanup</c> shape.</summary>
    private static List<ExternalDependencyCleanup> ExternalDependencies(string testCasesDir, IEnumerable<ScalarDependent> dependents) =>
        dependents.Select(d => new ExternalDependencyCleanup
        {
            TestId = d.DependentId,
            Suite = SuiteOf(testCasesDir, d.File),
            RemovedDeps = [d.RemovedDep]
        }).ToList();

    private static List<SuiteDependentCleaned> Dependents(TestDeletionService service, string testCasesDir, IEnumerable<ScalarDependent> dependents) =>
        dependents.Select(d => new SuiteDependentCleaned
        {
            Id = d.DependentId,
            Suite = SuiteOf(testCasesDir, d.File),
            File = service.Relative(d.File),
            RemovedDep = d.RemovedDep
        }).ToList();

    /// <summary>
    /// Spec 096 (FR-005): what the suite leaves outside its folder — <c>.spectra/updates/&lt;suite&gt;/</c>,
    /// <c>.spectra/repairs/&lt;suite&gt;/</c> and <c>.spectra/generation-plan/&lt;suite&gt;.json</c>. Only what exists.
    /// Spec 098 (F3): not its tests' verdict and record files — <see cref="TestDeletionService"/> removes and reports those.
    /// </summary>
    private static List<(string Path, string Relative, bool IsFolder)> Leftovers(string workspace, string suite)
    {
        var leftovers = new List<(string, string, bool)>();
        foreach (var folder in new[] { "updates", "repairs" })
        {
            var path = Path.Combine(workspace, ".spectra", folder, suite);
            if (Directory.Exists(path))
            {
                leftovers.Add((path, $".spectra/{folder}/{suite}/", true));
            }
        }

        var generationPlan = GenerationPlanStore.PathFor(workspace, suite);
        if (File.Exists(generationPlan))
        {
            leftovers.Add((generationPlan, GenerationPlanStore.RelativePathFor(suite), false));
        }

        return leftovers;
    }

    private static List<string> RemoveLeftovers(IEnumerable<(string Path, string Relative, bool IsFolder)> leftovers, List<string> warnings)
    {
        var removed = new List<string>();
        foreach (var (path, relative, isFolder) in leftovers)
        {
            try
            {
                if (isFolder)
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }

                removed.Add(relative);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Failed to remove {relative}: {ex.Message}");
            }
        }

        return removed;
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

    /// <summary>Spec 096: the human summary — on stdout, or on stderr in JSON mode (never suppressed).</summary>
    private static void WriteSummary(SuiteDeleteResult result, TextWriter writer)
    {
        if (result.Message is not null)
        {
            writer.WriteLine(result.Message);
        }

        var verb = result.DryRun ? "would clear" : "cleared";
        foreach (var d in result.DependentsCleaned)
        {
            writer.WriteLine($"  {d.Id}: {verb} depends_on {d.RemovedDep} ({d.File})");
        }

        foreach (var w in result.Warnings)
        {
            writer.WriteLine($"  warning: {w}");
        }

        foreach (var l in result.Leftovers ?? result.LeftoversRemoved ?? [])
        {
            writer.WriteLine($"  {(result.DryRun ? "would remove" : "removed")} {l}");
        }

        // Spec 098 (F3): the verdict and record files, once.
        if (result.VerdictFilesRemoved.Count > 0)
        {
            writer.WriteLine($"  {(result.DryRun ? "would remove" : "removed")} verdict files: {string.Join(", ", result.VerdictFilesRemoved)}");
        }
    }

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }

        if (result is SuiteDeleteResult { Error: null } summary)
        {
            WriteSummary(summary, _outputFormat == OutputFormat.Json ? Console.Error : Console.Out);
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
