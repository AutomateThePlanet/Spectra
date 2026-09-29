using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Extraction;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Prompts;
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
        if (string.IsNullOrWhiteSpace(doc))
        {
            EmitRefusal(json, "document_path", "A source document path is required (--doc).");
            return ExitRefused;
        }

        var currentDir = Directory.GetCurrentDirectory();
        var docFullPath = Path.IsPathRooted(doc) ? doc : Path.Combine(currentDir, doc);
        if (!File.Exists(docFullPath))
        {
            Console.Error.WriteLine($"Document not found: {doc}");
            return ExitError;
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(docFullPath, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading document: {ex.Message}");
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
        var (existing, warnings) = await LoadExistingCriteriaAsync(currentDir, doc, ct);
        if (existing.Count > 0
            && !templateLoader.LoadTemplate("criteria-extraction").Body.Contains("existing_criteria", StringComparison.Ordinal))
        {
            warnings.Add("The criteria-extraction prompt template in .spectra/prompts/ does not use {{existing_criteria}}, "
                + "so existing criterion IDs are not offered for reuse. Run 'spectra prompts reset criteria-extraction' to update it.");
        }

        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }

        var result = ExtractionPromptCompiler.Compile(doc, content, component, templateLoader, existing);

        if (!result.IsSuccess)
        {
            EmitRefusal(json, result.MissingInput!, result.Message!);
            return ExitRefused;
        }

        // Success: the prompt IS the artifact. Print to stdout; write nothing to disk.
        Console.Out.Write(result.Prompt);
        if (!result.Prompt!.EndsWith('\n')) Console.Out.WriteLine();
        return ExitSuccess;
    }

    /// <summary>
    /// The doc's current criteria, located via its index entry's <c>file</c> (lenient: a missing or unreadable
    /// config, index or file gives no criteria plus a warning). Never writes and never migrates.
    /// </summary>
    private static async Task<(IReadOnlyList<AcceptanceCriterion> Criteria, List<string> Warnings)> LoadExistingCriteriaAsync(
        string currentDir, string doc, CancellationToken ct)
    {
        var warnings = new List<string>();
        var config = SpectraConfig.Default;
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                config = JsonSerializer.Deserialize<SpectraConfig>(await File.ReadAllTextAsync(configPath, ct),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? SpectraConfig.Default;
            }
            catch (JsonException)
            {
                warnings.Add("spectra.config.json could not be parsed; using default criteria paths.");
            }
        }

        var index = await new CriteriaIndexReader().ReadAsync(Path.Combine(currentDir, config.Coverage.CriteriaFile), ct);
        if (CriteriaPaths.IsOldLayout(index))
        {
            warnings.Add("Criteria files use the pre-2.6 layout; run 'spectra docs migrate-criteria' "
                + "(criteria writes also migrate automatically).");
        }

        var normalized = CriteriaPaths.NormalizeSourceDoc(doc, currentDir).Path ?? CriteriaPaths.NormalizeLexical(doc);
        var source = index.Sources.FirstOrDefault(s => CriteriaPaths.IsDocumentSource(s)
            && string.Equals(CriteriaPaths.NormalizeLexical(s.SourceDoc), normalized, StringComparison.OrdinalIgnoreCase));
        if (source is null || string.IsNullOrWhiteSpace(source.File))
        {
            return ([], warnings);
        }

        var filePath = Path.Combine(currentDir, config.Coverage.CriteriaDir, source.File);
        var criteria = await new CriteriaFileReader().ReadAsync(filePath, ct);
        return (criteria, warnings);
    }

    private static void EmitRefusal(bool json, string missingInput, string message)
    {
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                refused = true,
                missing_input = missingInput,
                message
            }));
        }
        else
        {
            Console.Error.WriteLine($"Refusing to emit prompt — missing required input '{missingInput}': {message}");
        }
    }
}
