using Spectra.CLI.Infrastructure;
using Spectra.CLI.Prompts;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Commands.Prompts;

public sealed class PromptsResetHandler
{
    public async Task<int> ExecuteAsync(string? templateId, bool all, CancellationToken ct = default)
    {
        var workDir = Directory.GetCurrentDirectory();
        var promptsDir = Path.Combine(workDir, ".spectra", "prompts");

        if (!all && string.IsNullOrWhiteSpace(templateId))
        {
            Console.Error.WriteLine("Specify a template-id or use --all");
            return 1;
        }

        var templatesToReset = all
            ? BuiltInTemplates.AllTemplateIds.ToList()
            : [templateId!];

        Directory.CreateDirectory(promptsDir);

        // Load manifest for hash updates
        var manifestStore = new SkillsManifestStore(workDir);
        var manifest = await manifestStore.LoadAsync(ct);

        foreach (var id in templatesToReset)
        {
            var content = BuiltInTemplates.GetRawContent(id);
            if (content is null)
            {
                Console.Error.WriteLine($"Unknown template: {id}");
                continue;
            }

            var filePath = Path.Combine(promptsDir, $"{id}.md");
            await File.WriteAllTextAsync(filePath, content, ct);

            // Update manifest hash
            var relativePath = Path.Combine(".spectra", "prompts", $"{id}.md");
            manifest.Files[relativePath] = FileHasher.ComputeHash(content);

            Console.WriteLine($"✓ Reset {id}");
        }

        await manifestStore.SaveAsync(manifest, ct);
        return 0;
    }
}
