using Spectra.CLI.Prompts;

namespace Spectra.CLI.Commands.Prompts;

public sealed class PromptsShowHandler
{
    public Task<int> ExecuteAsync(string templateId, bool raw, CancellationToken ct = default)
    {
        if (!BuiltInTemplates.AllTemplateIds.Contains(templateId))
        {
            Console.Error.WriteLine($"Unknown template: {templateId}");
            Console.Error.WriteLine($"Available: {string.Join(", ", BuiltInTemplates.AllTemplateIds)}");
            return Task.FromResult(1);
        }

        var workDir = Directory.GetCurrentDirectory();
        var loader = new PromptTemplateLoader(workDir);

        // Always show raw since there's no runtime context
        var template = loader.LoadTemplate(templateId);
        var source = template.IsUserCustomized ? "(user-customized)" : "(built-in default)";

        Console.WriteLine($"# {templateId} {source}");
        Console.WriteLine($"# {template.Description}");
        Console.WriteLine($"# Placeholders: {string.Join(", ", template.Placeholders.Select(p => p.Name))}");
        Console.WriteLine();
        Console.WriteLine(template.Body);

        return Task.FromResult(0);
    }
}
