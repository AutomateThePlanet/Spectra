using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Extraction;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Prompts;
using Spectra.Core.Config;
using Spectra.Core.Criteria;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 054: <c>spectra ai compile-extraction-prompt</c>. Deterministic, model-free compiler that
/// emits the acceptance-criteria extraction prompt for one document. Writes nothing to disk; no
/// model call. Mirrors <see cref="CompilePromptCommand"/>.
///
/// Empty/whitespace source short-circuits (FR-003): it reports an <c>Extracted, []</c> outcome with
/// no prompt emitted and no model turn — distinct from a refuse-to-emit.
/// </summary>
public sealed class CompileExtractionPromptCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitRefused = 4;
    private const int ExitError = 1;

    public const string ErrorDocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string ErrorMissingInput = "MISSING_INPUT";
    private const string CommandName = "compile-extraction-prompt";
    public const string ErrorDocumentUnreadable = "DOCUMENT_UNREADABLE";

    public CompileExtractionPromptCommand()
        : base("compile-extraction-prompt",
            "Compile a criteria-extraction prompt for a document (deterministic, no model call)")
    {
        var docOption = new Option<string?>(["--doc", "-d"], "Path to the source document to extract from");
        var componentOption = new Option<string?>(["--component", "-c"], "Component hint (defaults to a slug from the filename)");

        AddOption(docOption);
        AddOption(componentOption);

        this.SetHandler(async (context) =>
        {
            var doc = context.ParseResult.GetValueForOption(docOption);
            var component = context.ParseResult.GetValueForOption(componentOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(doc, component, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? doc, string? component, bool json, CancellationToken ct)
    {
        // Spec 080 B7: warnings are collected, not printed eagerly, so every JSON-mode failure is one document.
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(doc))
        {
            WriteRefusal(json, "document_path", "A source document path is required (--doc).", warnings);
            return ExitRefused;
        }

        var currentDir = Directory.GetCurrentDirectory();

        // Spec 080 A5: read command — defaults when there is no config, exit 1 on a malformed or invalid one
        // (never a silent fallback, which emptied existing_criteria and churned IDs).
        var loaded = await ConfigLoader.LoadForReadAsync(currentDir, ConfigSections.None, ct); // reads no checked section
        if (!loaded.IsSuccess)
        {
            WriteFailure(json, loaded.ErrorCode, loaded.Error!, warnings);
            return ExitError;
        }
        var config = loaded.Config!;
        warnings.AddRange(loaded.Warnings);

        // A Windows-style docs\file.md is not a path on Unix; '/' works on every OS.
        var docOnDisk = doc.Replace('\\', '/');
        var docFullPath = Path.IsPathRooted(docOnDisk) ? docOnDisk : Path.Combine(currentDir, docOnDisk);
        if (!File.Exists(docFullPath))
        {
            WriteFailure(json, ErrorDocumentNotFound, $"Document not found: {doc}", warnings);
            return ExitError;
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(docFullPath, ct);
        }
        catch (Exception ex)
        {
            WriteFailure(json, ErrorDocumentUnreadable, $"Error reading document: {ex.Message}", warnings);
            return ExitError;
        }

        // FR-003 empty-source short-circuit: nothing to extract, no model turn, no prompt emitted.
        if (string.IsNullOrWhiteSpace(content))
        {
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    short_circuit = true,
                    outcome = "Extracted",
                    criteria = Array.Empty<object>()
                }));
            }
            else
            {
                Console.Error.WriteLine(
                    $"Empty source — nothing to extract from '{doc}' (outcome Extracted, []). No prompt emitted.");
            }
            return ExitSuccess;
        }

        var templateLoader = new PromptTemplateLoader(currentDir);

        // Spec 079 FR-001/FR-016c-d: read (never write) this doc's existing criteria through the index `file`
        // entry, so the prompt carries their IDs in both the old and the per-doc layout.
        var existing = await LoadExistingCriteriaAsync(currentDir, config, doc, warnings, ct);
        if (existing.Count > 0
            && !templateLoader.LoadTemplate("criteria-extraction").Body.Contains("existing_criteria", StringComparison.Ordinal))
        {
            warnings.Add("The criteria-extraction prompt template in .spectra/prompts/ does not use {{existing_criteria}}, "
                + "so existing criterion IDs are not offered for reuse. Run 'spectra prompts reset criteria-extraction' to update it.");
        }

        var result = ExtractionPromptCompiler.Compile(doc, content, component, templateLoader, existing);

        if (!result.IsSuccess)
        {
            WriteRefusal(json, result.MissingInput!, result.Message!, warnings);
            return ExitRefused;
        }

        // Success: warnings go to stderr as text; the prompt IS the artifact on stdout. Nothing is written to disk.
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }

        Console.Out.Write(result.Prompt);
        if (!result.Prompt!.EndsWith('\n')) Console.Out.WriteLine();
        return ExitSuccess;
    }

    /// <summary>
    /// The doc's current criteria, located via its index entry's <c>file</c> (lenient: a missing or unreadable
    /// index or file gives no criteria). Adds any layout warning to <paramref name="warnings"/>. Never writes and
    /// never migrates.
    /// </summary>
    private static async Task<IReadOnlyList<AcceptanceCriterion>> LoadExistingCriteriaAsync(
        string currentDir, SpectraConfig config, string doc, List<string> warnings, CancellationToken ct)
    {
        var index = await new CriteriaIndexReader().ReadAsync(Path.Combine(currentDir, config.Coverage.CriteriaFile), ct);
        if (CriteriaPaths.IsOldLayout(index))
        {
            warnings.Add("Criteria files use the pre-2.6 layout; run 'spectra docs migrate-criteria' "
                + "(criteria writes also migrate automatically).");
        }

        var normalized = CriteriaPaths.NormalizeSourceDoc(doc, currentDir).Path ?? CriteriaPaths.NormalizeLexical(doc);
        // Spec 081 C1: repo-aware comparison, so an index entry stored as an absolute path still matches.
        var source = index.Sources.FirstOrDefault(s => CriteriaPaths.IsDocumentSource(s)
            && CriteriaProvenance.SameDocument(s.SourceDoc, normalized, currentDir));
        if (source is null || string.IsNullOrWhiteSpace(source.File))
        {
            return [];
        }

        var filePath = Path.Combine(currentDir, config.Coverage.CriteriaDir, source.File);
        var criteria = await new CriteriaFileReader().ReadAsync(filePath, ct);

        // Spec 080 B3: on a shared (collided) file only this doc's criteria are offered — exactly what ingest
        // accepts — so a returned ID is never another doc's (which ingest would reject with exit 7).
        if (CriteriaProvenance.IsShared(index, source))
        {
            criteria = CriteriaProvenance.OwnedBy(criteria, normalized, currentDir);
        }

        return criteria;
    }

    /// <summary>
    /// Spec 080 B7: a refusal (exit 4). JSON mode writes one object to stderr, always with <c>warnings</c>; human mode
    /// prints the warnings, then the refusal line.
    /// </summary>
    internal static void WriteRefusal(bool json, string missingInput, string message, IReadOnlyList<string> warnings)
    {
        if (json)
        {
            // Spec 081 C4: the shared criteria failure shape; the refusal keeps which input was missing.
            CriteriaCommandIO.WriteFailureJson(CommandName, ErrorMissingInput, [message], warnings,
                new Dictionary<string, object?> { ["missing_input"] = missingInput });
            return;
        }

        WriteWarnings(warnings);
        Console.Error.WriteLine($"Refusing to emit prompt — missing required input '{missingInput}': {message}");
    }

    /// <summary>
    /// Spec 080 B7: an error (exit 1). JSON mode writes one object to stderr, always with <c>warnings</c>; human mode
    /// prints the warnings, then the error line.
    /// </summary>
    internal static void WriteFailure(bool json, string? errorCode, string message, IReadOnlyList<string> warnings)
    {
        if (json)
        {
            CriteriaCommandIO.WriteFailureJson(CommandName, errorCode, [message], warnings);
            return;
        }

        WriteWarnings(warnings);
        Console.Error.WriteLine(message);
    }

    private static void WriteWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }
    }
}
