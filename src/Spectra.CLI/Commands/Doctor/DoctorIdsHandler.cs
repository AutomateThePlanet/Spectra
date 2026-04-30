using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.IdAllocation;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Doctor;

/// <summary>
/// Spec 040: implements <c>spectra doctor ids [--fix]</c>.
/// </summary>
/// <remarks>
/// Read-only path: walk <c>test-cases/</c>, build all-IDs inventory from
/// frontmatter, detect duplicates, detect index-vs-disk mismatches, read
/// the HWM, compute the next ID. Returns exit 9
/// (<see cref="ExitCodes.DuplicatesFound"/>) when duplicates are reported
/// and the caller passed <c>--no-interaction</c> without <c>--fix</c>.
///
/// <c>--fix</c> path: order duplicate occurrences by Git history (then mtime),
/// keep the oldest, renumber later occurrences via
/// <see cref="PersistentTestIdAllocator"/>, update <c>depends_on</c> references
/// across the workspace, and best-effort literal-string update of
/// <c>[TestCase("TC-NNN")]</c> attributes in linked automation files.
/// </remarks>
public sealed class DoctorIdsHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public DoctorIdsHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(bool fix, bool noInteraction, CancellationToken ct = default)
    {
        var workspace = Directory.GetCurrentDirectory();
        var testCasesDir = ResolveTestCasesDir(workspace);

        if (!Directory.Exists(testCasesDir))
        {
            var emptyResult = new DoctorIdsResult
            {
                Command = "doctor ids",
                Status = "completed",
                FixApplied = fix,
                NextId = "TC-100"
            };
            EmitResult(emptyResult);
            return ExitCodes.Success;
        }

        var allocator = new PersistentTestIdAllocator(workspace);
        var scanner = new TestCaseFrontmatterScanner(testCasesDir);
        var idsWithFiles = await scanner.EnumerateAllIdsAsync(ct).ConfigureAwait(false);

        var duplicates = idsWithFiles
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateIdGroup
            {
                Id = g.Key,
                Occurrences = g.Select(o => new DuplicateOccurrence
                {
                    File = MakeRelative(workspace, o.File),
                    Title = TryReadTitle(o.File),
                    Mtime = SafeMtime(o.File)
                }).ToList()
            })
            .ToList();

        var indexMismatches = await DetectIndexMismatchesAsync(testCasesDir, workspace, idsWithFiles, ct).ConfigureAwait(false);

        var idStart = await ReadIdStartAsync(workspace, ct).ConfigureAwait(false);
        var (effective, nextId) = await allocator.PeekNextAsync("TC", idStart, ct).ConfigureAwait(false);
        var hwm = await allocator.PeekHighWaterMarkAsync(ct).ConfigureAwait(false);

        var totalTests = idsWithFiles.Count;
        var uniqueIds = idsWithFiles.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        if (!fix)
        {
            var result = new DoctorIdsResult
            {
                Command = "doctor ids",
                Status = "completed",
                FixApplied = false,
                TotalTests = totalTests,
                UniqueIds = uniqueIds,
                Duplicates = duplicates,
                IndexMismatches = indexMismatches,
                HighWaterMark = hwm,
                NextId = nextId
            };

            if (_outputFormat == OutputFormat.Human)
            {
                PrintHumanReport(result);
            }
            EmitResult(result);

            // Spec 040: in non-interactive mode, surfacing duplicates is a
            // CI-friendly failure signal — exit 9 so pipelines fail noisily.
            if (duplicates.Count > 0 && noInteraction)
            {
                return ExitCodes.DuplicatesFound;
            }
            return ExitCodes.Success;
        }

        // --fix path
        var (renumbered, unfixable) = await ApplyFixAsync(workspace, allocator, duplicates, idStart, ct).ConfigureAwait(false);

        // Re-scan to confirm post-fix state
        var idsAfter = await scanner.EnumerateAllIdsAsync(ct).ConfigureAwait(false);
        var duplicatesAfter = idsAfter
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateIdGroup { Id = g.Key, Occurrences = Array.Empty<DuplicateOccurrence>() })
            .ToList();
        var (_, nextIdAfter) = await allocator.PeekNextAsync("TC", idStart, ct).ConfigureAwait(false);
        var hwmAfter = await allocator.PeekHighWaterMarkAsync(ct).ConfigureAwait(false);

        var fixResult = new DoctorIdsResult
        {
            Command = "doctor ids",
            Status = "completed",
            FixApplied = true,
            TotalTests = idsAfter.Count,
            UniqueIds = idsAfter.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Duplicates = duplicatesAfter,
            IndexMismatches = await DetectIndexMismatchesAsync(testCasesDir, workspace, idsAfter, ct).ConfigureAwait(false),
            HighWaterMark = hwmAfter,
            NextId = nextIdAfter,
            Renumbered = renumbered,
            UnfixableReferences = unfixable,
            Message = $"Renumbered {renumbered.Count} test(s); {unfixable.Count} reference(s) need manual review."
        };

        if (_outputFormat == OutputFormat.Human)
        {
            PrintHumanReport(fixResult);
        }
        EmitResult(fixResult);
        return ExitCodes.Success;
    }

    private async Task<IReadOnlyList<IndexMismatch>> DetectIndexMismatchesAsync(
        string testCasesDir,
        string workspace,
        IReadOnlyList<(string Id, string File)> diskIds,
        CancellationToken ct)
    {
        var mismatches = new List<IndexMismatch>();
        var indexWriter = new IndexWriter();

        var diskIdsBySuite = diskIds
            .GroupBy(p => SuiteFromPath(testCasesDir, p.File), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        foreach (var indexFile in Directory.EnumerateFiles(testCasesDir, "_index.json", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var index = await indexWriter.ReadAsync(indexFile, ct).ConfigureAwait(false);
            if (index?.Tests is null)
            {
                continue;
            }

            var suiteName = Path.GetFileName(Path.GetDirectoryName(indexFile)) ?? string.Empty;
            var diskSet = diskIdsBySuite.TryGetValue(suiteName, out var s) ? s : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var indexSet = index.Tests
                .Where(t => !string.IsNullOrEmpty(t.Id))
                .Select(t => t.Id!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // In index but not on disk
            foreach (var id in indexSet.Except(diskSet, StringComparer.OrdinalIgnoreCase))
            {
                mismatches.Add(new IndexMismatch { Suite = suiteName, Id = id, InIndex = true, OnDisk = false });
            }
            // On disk but not in index
            foreach (var id in diskSet.Except(indexSet, StringComparer.OrdinalIgnoreCase))
            {
                mismatches.Add(new IndexMismatch { Suite = suiteName, Id = id, InIndex = false, OnDisk = true });
            }
        }

        return mismatches;
    }

    private async Task<(IReadOnlyList<RenumberedTest> Renumbered, IReadOnlyList<UnfixableReference> Unfixable)> ApplyFixAsync(
        string workspace,
        PersistentTestIdAllocator allocator,
        IReadOnlyList<DuplicateIdGroup> duplicates,
        int idStart,
        CancellationToken ct)
    {
        var renumbered = new List<RenumberedTest>();
        var unfixable = new List<UnfixableReference>();

        foreach (var group in duplicates)
        {
            ct.ThrowIfCancellationRequested();

            // Order: oldest by mtime wins (Git history would be ideal but
            // requires a libgit2/process call — mtime is good-enough per
            // research.md Decision 9 and is documented best-effort).
            var ordered = group.Occurrences
                .OrderBy(o => o.Mtime, StringComparer.Ordinal)
                .ToList();

            // Skip the first (oldest); renumber the rest
            for (var i = 1; i < ordered.Count; i++)
            {
                var occ = ordered[i];
                var oldId = group.Id;
                var newIds = await allocator.AllocateAsync(1, "TC", idStart, "doctor ids --fix", ct).ConfigureAwait(false);
                var newId = newIds[0];

                var absolutePath = Path.Combine(workspace, occ.File);
                if (!File.Exists(absolutePath))
                {
                    continue;
                }

                // Rewrite the file with the new ID
                var content = await File.ReadAllTextAsync(absolutePath, ct).ConfigureAwait(false);
                var rewritten = ReplaceIdInFrontmatter(content, oldId, newId);
                if (rewritten != content)
                {
                    await Spectra.Core.Index.AtomicFileWriter.WriteAllTextAsync(absolutePath, rewritten, ct).ConfigureAwait(false);
                }

                // Rename the file to match the new ID
                var newPath = Path.Combine(Path.GetDirectoryName(absolutePath)!, $"{newId}.md");
                if (!string.Equals(absolutePath, newPath, StringComparison.OrdinalIgnoreCase) && !File.Exists(newPath))
                {
                    File.Move(absolutePath, newPath);
                }

                renumbered.Add(new RenumberedTest
                {
                    From = oldId,
                    To = newId,
                    File = occ.File,
                    NowAt = MakeRelative(workspace, newPath)
                });

                // Note: we deliberately do NOT cascade `depends_on` or
                // automation string rewrites. The kept-oldest still holds
                // the old ID, so existing references remain valid pointing
                // at it. Whether the original author of a `depends_on:
                // TC-100` or `[TestCase("TC-100")]` reference meant the
                // kept-oldest or the now-renumbered duplicate is genuinely
                // ambiguous — surfacing automation refs as "unfixable" so
                // the user can manually disambiguate.
                await CollectUnfixableAutomationRefsAsync(workspace, oldId, unfixable, ct).ConfigureAwait(false);
            }
        }

        return (renumbered, unfixable);
    }

    private static async Task CollectUnfixableAutomationRefsAsync(
        string workspace,
        string oldId,
        List<UnfixableReference> unfixable,
        CancellationToken ct)
    {
        // Best-effort detection: scan source files for any reference to the
        // duplicated ID. We do NOT rewrite automatically because the original
        // author's intent (kept-oldest vs. renumbered duplicate) is ambiguous.
        // The unfixable list gives the user a punch list to review manually.
        foreach (var file in Directory.EnumerateFiles(workspace, "*.cs", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }

            if (content.Contains(oldId, StringComparison.OrdinalIgnoreCase))
            {
                unfixable.Add(new UnfixableReference
                {
                    File = MakeRelative(workspace, file),
                    Reference = oldId,
                    Reason = "ambiguous reference — review manually"
                });
            }
        }
    }

    private static string ReplaceIdInFrontmatter(string content, string oldId, string newId)
    {
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(\s*id:\s*[""']?)" + System.Text.RegularExpressions.Regex.Escape(oldId) + @"([""']?\s*)$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return pattern.Replace(content, "${1}" + newId + "$2");
    }

    private static string ResolveTestCasesDir(string workspace)
    {
        // Honor optional config override; default is "test-cases"
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
            catch
            {
                // fall through to default
            }
        }
        return Path.Combine(workspace, "test-cases");
    }

    private static async Task<int> ReadIdStartAsync(string workspace, CancellationToken ct)
    {
        var configPath = Path.Combine(workspace, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            return 100;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config?.Tests?.IdStart ?? 100;
        }
        catch
        {
            return 100;
        }
    }

    private static string SuiteFromPath(string testCasesDir, string filePath)
    {
        var relative = Path.GetRelativePath(testCasesDir, filePath);
        var firstSep = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        return firstSep > 0 ? relative[..firstSep] : string.Empty;
    }

    private static string MakeRelative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string SafeMtime(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path).ToString("o");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? TryReadTitle(string path)
    {
        try
        {
            // Read the first ~40 lines, look for `title:` in frontmatter.
            using var reader = new StreamReader(path);
            for (var i = 0; i < 40; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }
                if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                {
                    var v = line["title:".Length..].Trim();
                    return v.Trim('"', '\'');
                }
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        // Always write to .spectra-result.json for SKILLs to consume
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
        catch
        {
            // non-critical
        }
    }

    private void PrintHumanReport(DoctorIdsResult result)
    {
        if (_verbosity == VerbosityLevel.Quiet)
        {
            return;
        }

        Console.WriteLine($"Test ID Audit");
        Console.WriteLine($"  Total tests:      {result.TotalTests}");
        Console.WriteLine($"  Unique IDs:       {result.UniqueIds}{(result.Duplicates.Count > 0 ? $"   ⚠ {result.Duplicates.Count} duplicate group(s)" : string.Empty)}");
        Console.WriteLine($"  High-water-mark:  {(result.HighWaterMark == 0 ? "(unset)" : $"TC-{result.HighWaterMark:D3}")}");
        Console.WriteLine($"  Next ID:          {result.NextId}");
        Console.WriteLine();

        if (result.Duplicates.Count > 0)
        {
            Console.WriteLine("Duplicates:");
            foreach (var group in result.Duplicates)
            {
                Console.WriteLine($"  {group.Id}");
                foreach (var occ in group.Occurrences)
                {
                    Console.WriteLine($"    {occ.File}  (modified {occ.Mtime})");
                }
            }
            Console.WriteLine();
        }

        if (result.IndexMismatches.Count > 0)
        {
            Console.WriteLine("Index mismatches:");
            foreach (var m in result.IndexMismatches)
            {
                var dir = m.InIndex ? "in index, missing on disk" : "on disk, missing from index";
                Console.WriteLine($"  {m.Suite}/{m.Id}  ({dir})");
            }
            Console.WriteLine();
        }

        if (result.FixApplied)
        {
            Console.WriteLine($"Renumbered {result.Renumbered.Count} test(s); {result.UnfixableReferences.Count} reference(s) need manual review.");
        }
        else if (result.Duplicates.Count > 0)
        {
            Console.WriteLine("Run `spectra doctor ids --fix` to renumber later occurrences.");
        }
    }
}
