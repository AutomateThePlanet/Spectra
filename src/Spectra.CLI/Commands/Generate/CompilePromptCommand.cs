using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 053: <c>spectra ai compile-prompt</c>. Deterministic, model-free prompt compiler.
/// Resolves criteria + profile grounding for a suite, emits the compiled prompt to stdout,
/// and writes nothing to disk. Refuses to emit (exit 4) when a required input is missing.
/// </summary>
public sealed class CompilePromptCommand : Command
{
    // Local, contract-specific exit codes (see contracts/compile-prompt.md).
    private const int ExitSuccess = 0;
    private const int ExitRefused = 4;
    private const int ExitError = 1;

    public CompilePromptCommand()
        : base("compile-prompt", "Compile a grounded generation prompt (deterministic, no model call)")
    {
        var suiteArgument = new Argument<string?>("suite", () => null,
            "Target suite name") { Arity = ArgumentArity.ZeroOrOne };
        var suiteOption = new Option<string?>(["--suite", "-s"], "Target suite name (alternative to positional)");
        var countOption = new Option<int?>(["--count", "-n"], "Number of tests the prompt should request (default 5)");
        var focusOption = new Option<string?>(["--focus", "-f"], "Focus area / behaviors for generation");
        // Spec 059: from-description single-test mode over the seam. When set, count is forced to 1
        // and the user prompt is shaped from the description (+ context). Criteria remain optional
        // (the description is the source of truth) so this mode uses the lenient assembly.
        var fromDescriptionOption = new Option<string?>("--from-description", "Compile a single-test prompt from a plain-language description");
        var contextOption = new Option<string?>("--context", "Additional context for --from-description (page, module, flow)");

        AddArgument(suiteArgument);
        AddOption(suiteOption);
        AddOption(countOption);
        AddOption(focusOption);
        AddOption(fromDescriptionOption);
        AddOption(contextOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)
                        ?? context.ParseResult.GetValueForArgument(suiteArgument);
            var count = context.ParseResult.GetValueForOption(countOption) ?? 5;
            var focus = context.ParseResult.GetValueForOption(focusOption) ?? "";
            var fromDescription = context.ParseResult.GetValueForOption(fromDescriptionOption);
            var descriptionContext = context.ParseResult.GetValueForOption(contextOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, count, focus, fromDescription, descriptionContext,
                outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string? suite, int count, string focus, string? fromDescription, string? descriptionContext,
        bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            EmitRefusal(json, "suite", "A target suite is required.");
            return ExitRefused;
        }

        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitError;
        }

        SpectraConfig? config;
        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, ct);
            config = JsonSerializer.Deserialize<SpectraConfig>(configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error reading config: {ex.Message}");
            return ExitError;
        }
        if (config is null)
        {
            Console.Error.WriteLine("Could not parse spectra.config.json.");
            return ExitError;
        }

        // Resolve grounding via the relocated, model-free criteria loader (Spec 059).
        var criteria = await CriteriaContextLoader.LoadCriteriaContextAsync(currentDir, suite, config, ct);
        var profileFormat = ProfileFormatLoader.LoadFormat(currentDir);
        var templateLoader = new PromptTemplateLoader(currentDir);

        // Spec 059: from-description single-test mode. The user prompt is shaped from the
        // description (+ context); count is forced to 1; criteria are optional (the description
        // is the source of truth), so this mode uses the lenient assembly which omits the
        // MANDATORY block when no criteria match — but still injects it (Spec 050) when they do.
        if (!string.IsNullOrWhiteSpace(fromDescription))
        {
            var existingIds = await LoadExistingTestIdsAsync(currentDir, suite, config, ct);
            var userPrompt = DescriptionPromptBuilder.Build(
                fromDescription, descriptionContext, suite, existingIds);

            var fromDescPrompt = PromptCompiler.Assemble(
                userPrompt: userPrompt,
                requestedCount: 1,
                criteriaContext: criteria.Context,
                templateLoader: templateLoader,
                profileFormat: profileFormat,
                testimizeData: null);

            Console.Out.Write(fromDescPrompt);
            if (!fromDescPrompt.EndsWith('\n')) Console.Out.WriteLine();
            return ExitSuccess;
        }

        var result = PromptCompiler.Compile(
            userPrompt: string.IsNullOrWhiteSpace(focus) ? $"Generate manual test cases for the '{suite}' suite." : focus,
            requestedCount: count,
            criteriaContext: criteria.Context,
            templateLoader: templateLoader,
            profileFormat: profileFormat,
            testimizeData: null);

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
    /// Loads the existing test IDs for a suite (deterministic, sorted) so the from-description
    /// prompt can instruct the agent not to duplicate them. Model-free.
    /// </summary>
    private static async Task<IReadOnlyCollection<string>> LoadExistingTestIdsAsync(
        string currentDir, string suite, SpectraConfig config, CancellationToken ct)
    {
        var testsDir = config.Tests?.Dir ?? "test-cases";
        var suitePath = Path.Combine(currentDir, testsDir, suite);
        if (!Directory.Exists(suitePath))
            return [];

        var parser = new TestCaseParser();
        var ids = new List<string>();
        foreach (var file in Directory.GetFiles(suitePath, "*.md")
                     .Where(f => !Path.GetFileName(f).StartsWith('_')))
        {
            var parsed = parser.Parse(await File.ReadAllTextAsync(file, ct), Path.GetFileName(file));
            if (parsed.IsSuccess && !string.IsNullOrWhiteSpace(parsed.Value!.Id))
                ids.Add(parsed.Value.Id);
        }
        ids.Sort(StringComparer.Ordinal);
        return ids;
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
