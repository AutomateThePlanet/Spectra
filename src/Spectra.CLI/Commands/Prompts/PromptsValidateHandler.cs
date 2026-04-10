using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Prompts;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Prompts;

public sealed class PromptsValidateHandler
{
    private readonly OutputFormat _outputFormat;

    public PromptsValidateHandler(OutputFormat outputFormat)
    {
        _outputFormat = outputFormat;
    }

    public Task<int> ExecuteAsync(string templateId, CancellationToken ct = default)
    {
        if (!BuiltInTemplates.AllTemplateIds.Contains(templateId))
        {
            Console.Error.WriteLine($"Unknown template: {templateId}");
            return Task.FromResult(1);
        }

        var workDir = Directory.GetCurrentDirectory();
        var loader = new PromptTemplateLoader(workDir);

        var template = loader.LoadTemplate(templateId);
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check syntax
        var syntaxErrors = PlaceholderResolver.ValidateSyntax(template.Body);
        errors.AddRange(syntaxErrors);

        // Check placeholder names against declared list
        var usedNames = PlaceholderResolver.ExtractPlaceholderNames(template.Body);
        var declaredNames = template.Placeholders.Select(p => p.Name).ToHashSet();

        foreach (var name in usedNames)
        {
            if (!declaredNames.Contains(name))
                warnings.Add($"Unknown placeholder '{{{{{{name}}}}}}' not declared in frontmatter");
        }

        var valid = errors.Count == 0;

        if (_outputFormat == OutputFormat.Json)
        {
            var result = new PromptsValidateResult
            {
                Command = "prompts validate",
                Status = valid ? "success" : "error",
                TemplateId = templateId,
                Valid = valid,
                Placeholders = template.Placeholders.Count,
                Warnings = warnings,
                Errors = errors
            };
            JsonResultWriter.Write(result);
        }
        else
        {
            if (valid)
            {
                Console.WriteLine($"✓ {templateId}: valid ({template.Placeholders.Count} placeholders, {warnings.Count} warnings)");
                foreach (var w in warnings)
                    Console.WriteLine($"  ⚠ {w}");
            }
            else
            {
                Console.Error.WriteLine($"✗ {templateId}: {errors.Count} errors");
                foreach (var e in errors)
                    Console.Error.WriteLine($"  ✗ {e}");
                foreach (var w in warnings)
                    Console.Error.WriteLine($"  ⚠ {w}");
            }
        }

        return Task.FromResult(valid ? 0 : 2);
    }
}
