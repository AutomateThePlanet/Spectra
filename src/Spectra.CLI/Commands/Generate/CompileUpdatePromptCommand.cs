using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 063: <c>spectra ai compile-update-prompt</c>. Deterministic, model-free compiler for the
/// inverted update seam. Emits — to stdout, writing nothing — an <i>edit</i> prompt for ONE
/// OUTDATED test: the existing test (serialized) + the changed source/criteria + explicit
/// "edit, don't regenerate; preserve id/structure/manual fields" directives. Refuses (exit 4)
/// when a required input is missing (suite, test id, the test itself, or any grounding context).
/// </summary>
public sealed class CompileUpdatePromptCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitRefused = 4;
    private const int ExitError = 1;

    public CompileUpdatePromptCommand()
        : base("compile-update-prompt", "Compile an edit prompt for one OUTDATED test (deterministic, no model call)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite containing the test to update");
        var testIdOption = new Option<string?>("--test-id", "Id of the OUTDATED test to compile an update prompt for");

        AddOption(suiteOption);
        AddOption(testIdOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var testId = context.ParseResult.GetValueForOption(testIdOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, testId, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? suite, string? testId, bool json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            EmitRefusal(json, "suite", "A target suite is required.");
            return ExitRefused;
        }
        if (string.IsNullOrWhiteSpace(testId))
        {
            EmitRefusal(json, "test-id", "A target test id is required.");
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
            config = JsonSerializer.Deserialize<SpectraConfig>(
                await File.ReadAllTextAsync(configPath, ct),
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

        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var original = await LoadTestByIdAsync(testsPath, suite, testId, ct);
        if (original is null)
        {
            EmitRefusal(json, "test-id", $"Test '{testId}' was not found in suite '{suite}'.");
            return ExitRefused;
        }

        // Resolve grounding the same way the generation compile does. The criteria/source context
        // is what the edit reconciles the test against.
        var criteria = await CriteriaContextLoader.LoadCriteriaContextAsync(currentDir, suite, config, ct);
        var profileFormat = ProfileFormatLoader.LoadFormat(currentDir);
        var templateLoader = new PromptTemplateLoader(currentDir);

        var result = UpdatePromptCompiler.Compile(
            originalTest: original,
            sourceContext: null,
            criteriaContext: criteria.Context,
            templateLoader: templateLoader,
            profileFormat: profileFormat);

        if (!result.IsSuccess)
        {
            EmitRefusal(json, result.MissingInput!, result.Message!);
            return ExitRefused;
        }

        Console.Out.Write(result.Prompt);
        if (!result.Prompt!.EndsWith('\n')) Console.Out.WriteLine();
        return ExitSuccess;
    }

    private static async Task<TestCase?> LoadTestByIdAsync(
        string testsPath, string suite, string testId, CancellationToken ct)
    {
        var suitePath = Path.Combine(testsPath, suite);
        if (!Directory.Exists(suitePath))
            return null;

        var parser = new TestCaseParser();
        foreach (var file in Directory.GetFiles(suitePath, "*.md")
                     .Where(f => !Path.GetFileName(f).StartsWith('_')))
        {
            var relativePath = Path.GetRelativePath(testsPath, file);
            var parsed = parser.Parse(await File.ReadAllTextAsync(file, ct), relativePath);
            if (parsed.IsSuccess && string.Equals(parsed.Value!.Id, testId, StringComparison.OrdinalIgnoreCase))
                return parsed.Value;
        }
        return null;
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
