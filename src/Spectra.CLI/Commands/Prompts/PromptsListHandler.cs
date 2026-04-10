using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Prompts;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Prompts;

public sealed class PromptsListHandler
{
    private readonly OutputFormat _outputFormat;

    public PromptsListHandler(OutputFormat outputFormat)
    {
        _outputFormat = outputFormat;
    }

    public Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var workDir = Directory.GetCurrentDirectory();
        var loader = new PromptTemplateLoader(workDir);

        var entries = new List<TemplateStatusEntry>();

        foreach (var templateId in BuiltInTemplates.AllTemplateIds)
        {
            var status = loader.GetTemplateStatus(templateId);
            var builtIn = BuiltInTemplates.GetTemplate(templateId);
            var filePath = Path.Combine(".spectra", "prompts", $"{templateId}.md");

            entries.Add(new TemplateStatusEntry
            {
                TemplateId = templateId,
                Status = status switch
                {
                    TemplateFileStatus.Customized => "customized",
                    TemplateFileStatus.Default => "default",
                    TemplateFileStatus.Missing => "missing",
                    _ => "unknown"
                },
                FilePath = status != TemplateFileStatus.Missing ? filePath : null,
                Description = builtIn?.Description ?? ""
            });
        }

        if (_outputFormat == OutputFormat.Json)
        {
            var result = new PromptsListResult
            {
                Command = "prompts list",
                Status = "success",
                Templates = entries
            };
            JsonResultWriter.Write(result);
        }
        else
        {
            foreach (var entry in entries)
            {
                var icon = entry.Status switch
                {
                    "customized" => "✓",
                    "default" => "○",
                    _ => "✗"
                };
                var path = entry.FilePath ?? "(using built-in)";
                Console.WriteLine($"{entry.TemplateId,-25} {icon} {entry.Status,-12} {path}");
            }
        }

        return Task.FromResult(0);
    }
}
