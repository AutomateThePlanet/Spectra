using System.Text;
using Spectra.Core.Models;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Index;

/// <summary>
/// Writes a per-suite <c>groups/{id}.index.md</c> file (Spec 040 v2 layout) atomically.
/// Same per-document entry shape as the legacy single-file index but scoped to one
/// suite, with no checksum block (checksums live in <see cref="ChecksumStore"/>).
/// </summary>
public sealed class SuiteIndexFileWriter
{
    public async Task WriteAsync(string path, SuiteIndexFile file, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(file);

        var content = Render(file);
        await AtomicFileWriter.WriteAllTextAsync(path, content, ct);
    }

    public static string Render(SuiteIndexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrEmpty(file.SuiteId))
        {
            throw new InvalidOperationException("SuiteIndexFile.SuiteId is empty.");
        }
        if (file.DocumentCount != file.Entries.Count)
        {
            throw new InvalidOperationException(
                $"SuiteIndexFile.DocumentCount ({file.DocumentCount}) does not match Entries.Count ({file.Entries.Count}).");
        }

        var sb = new StringBuilder();

        // Header — single-suite scoped.
        sb.AppendLine($"# {file.SuiteId}");
        sb.AppendLine();
        sb.AppendLine(
            $"> Group: {file.SuiteId} | {file.DocumentCount} documents | ~{file.TokensEstimated:N0} tokens");
        sb.AppendLine($"> Last indexed: {file.GeneratedAt:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine();

        // Entries sorted by Path (ordinal) for deterministic output.
        var sortedEntries = file.Entries
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ToList();

        foreach (var entry in sortedEntries)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"### {entry.Path}");
            sb.AppendLine($"- **Title:** {entry.Title}");
            sb.AppendLine(
                $"- **Size:** {entry.SizeKb} KB | **Words:** {entry.WordCount:N0} | **Tokens:** ~{entry.EstimatedTokens:N0}");
            sb.AppendLine($"- **Last Modified:** {entry.LastModified:yyyy-MM-dd}");

            if (entry.KeyEntities.Count > 0)
            {
                sb.AppendLine($"- **Key Entities:** {string.Join(", ", entry.KeyEntities)}");
            }

            sb.AppendLine();

            if (entry.Sections.Count > 0)
            {
                sb.AppendLine("| Section | Summary |");
                sb.AppendLine("|---------|---------|");
                foreach (var section in entry.Sections)
                {
                    var prefix = section.Level > 2
                        ? new string(' ', (section.Level - 2) * 2) + "↳ "
                        : "";
                    var escapedSummary = section.Summary
                        .Replace("|", "\\|")
                        .Replace("\n", " ");
                    sb.AppendLine($"| {prefix}{section.Heading} | {escapedSummary} |");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
