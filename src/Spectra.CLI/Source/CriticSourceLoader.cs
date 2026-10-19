using Spectra.Core.Baseline;
using Spectra.Core.Coverage;
using Spectra.Core.Models;

namespace Spectra.CLI.Source;

/// <summary>
/// Spec 101 F1 (FR-001, FR-002; research R1): the documents the critic is given — the sections a test cites, not the
/// whole files. One <see cref="SourceDocument"/> per cited document, in first-reference order, so the critic prompt
/// builder (its cut and document limit) is unchanged. Refs resolve like <see cref="SourceRefDocumentLoader"/> (same
/// refusal for a missing or outside-root ref).
/// <list type="bullet">
/// <item>Any anchor-less ref to a document → the whole file, read as it is on disk.</item>
/// <item>Any anchor that matches no heading (the 085 matcher) → the whole file, after one note line per such anchor.</item>
/// <item>Otherwise each cited section (heading, body and subsections), in first-reference order, joined by a blank
/// line; a section inside another cited section of the same document is absorbed (094's rule), and duplicates count
/// once.</item>
/// </list>
/// </summary>
public static class CriticSourceLoader
{
    /// <param name="note">Receives a line for each ambiguous anchor (the first matching heading is used).</param>
    public static async Task<IReadOnlyList<SourceDocument>> LoadAsync(
        IReadOnlyList<string> sourceRefs,
        string repoRoot,
        string sourceDir,
        Action<string>? note,
        CancellationToken ct)
    {
        var resolver = new SourceRefResolver(repoRoot, sourceDir);
        var resolution = resolver.Resolve(sourceRefs);
        if (resolution.Missing.FirstOrDefault(m => m.Reason != MissingReason.Empty) is { } missing)
        {
            throw new FileNotFoundException(SourceRefDocumentLoader.MissingMessage(missing), missing.Ref);
        }

        var docs = new List<SourceDocument>();
        foreach (var fullPath in resolution.DistinctFullPaths)
        {
            var refs = resolution.Resolved.Where(r => r.FullPath == fullPath).ToList();
            var raw = await File.ReadAllTextAsync(fullPath, ct);
            docs.Add(new SourceDocument
            {
                Path = refs[0].RelativePath,
                Title = Path.GetFileNameWithoutExtension(fullPath),
                Content = Content(raw, refs.Select(r => r.Ref).ToList(), note)
            });
        }

        return docs;
    }

    private static string Content(string raw, IReadOnlyList<string> refs, Action<string>? note)
    {
        var anchors = refs.Select(r => SectionExtractor.AnchorOf(r)).ToList();
        if (anchors.Any(string.IsNullOrEmpty))
        {
            return raw;
        }

        var text = SectionExtractor.NormalizeNewlines(raw);
        var headings = SectionExtractor.Headings(text);
        var matches = anchors.Select(a => SectionExtractor.Match(headings, a)).ToList();
        var unmatched = anchors.Where((_, i) => matches[i] is null).Distinct(StringComparer.Ordinal).ToList();
        if (unmatched.Count > 0)
        {
            return string.Concat(unmatched.Select(a =>
                $"> Note: the anchor \"#{a}\" was not found in this document, so the whole document is given.\n")) + raw;
        }

        var kept = new List<int>();
        var noted = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < matches.Count; i++)
        {
            var position = matches[i]!.Index;
            if (matches[i]!.Ambiguous && noted.Add(refs[i]))
            {
                note?.Invoke($"{refs[i]}: more than one heading shares the anchor key '{AnchorKey.FromAnchor(anchors[i])}'; the first was used");
            }

            var absorbed = Enumerable.Range(0, matches.Count).Any(j => matches[j]!.Index != position
                && SectionExtractor.CoveredHeadings(headings, anchors[j]).Contains(position));
            if (!absorbed && !kept.Contains(position))
            {
                kept.Add(position);
            }
        }

        var sections = kept.Select(position =>
            SectionExtractor.Extract(text, headings, anchors[matches.FindIndex(m => m!.Index == position)]).RawText!.TrimEnd('\n'));
        return string.Join("\n\n", sections) + "\n";
    }
}
