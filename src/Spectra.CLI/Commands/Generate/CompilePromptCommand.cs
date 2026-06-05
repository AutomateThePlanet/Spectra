using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.Core.Models.Config;

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

        AddArgument(suiteArgument);
        AddOption(suiteOption);
        AddOption(countOption);
        AddOption(focusOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)
                        ?? context.ParseResult.GetValueForArgument(suiteArgument);
            var count = context.ParseResult.GetValueForOption(countOption) ?? 5;
            var focus = context.ParseResult.GetValueForOption(focusOption) ?? "";
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, count, focus, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string? suite, int count, string focus, bool json, CancellationToken ct)
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

        // Resolve grounding exactly as the generate handler does.
        var criteria = await GenerateHandler.LoadCriteriaContextAsync(currentDir, suite, config, ct);
        var profileFormat = ProfileFormatLoader.LoadFormat(currentDir);
        var templateLoader = new PromptTemplateLoader(currentDir);

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
