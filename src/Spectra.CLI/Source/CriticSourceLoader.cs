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
/// Spec 102 (F2): refs are grouped by file with <see cref="SourceRefResolver.PathComparer"/>, the comparer the resolver
/// deduplicates files with, so refs that differ only in case (on Windows / macOS) reach the critic together.
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

        return await LoadAsync(resolution, SourceRefResolver.PathComparer, note, ct);
    }

    /// <summary>
    /// Spec 102 (F2): the documents of a resolved ref set, grouping refs by file with <paramref name="comparer"/> (first-
    /// reference order); each file is read from its first ref's full path. The comparer is a parameter so tests can run
    /// the case-insensitive rule on every OS.
    /// </summary>
    internal static async Task<IReadOnlyList<SourceDocument>> LoadAsync(
        SourceRefResolution resolution, StringComparer comparer, Action<string>? note, CancellationToken ct)
    {
        var docs = new List<SourceDocument>();
        foreach (var group in resolution.Resolved.GroupBy(r => r.FullPath, comparer))
        {
            var refs = group.ToList();
            var fullPath = refs[0].FullPath;
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

    /// <summary>
    /// The text the critic gets for one document. Spec 102 (F7): each anchor is matched once and the document is
    /// sliced once — a ref is absorbed when its heading lies inside another cited section (the same set
    /// <see cref="SectionExtractor.CoveredHeadings"/> gives). F5: the ambiguous-anchor note is only for a section that is
    /// given. <paramref name="match"/> and <paramref name="lineStarts"/> default to <see cref="SectionExtractor"/>'s; tests
    /// pass counting wrappers.
    /// </summary>
    internal static string Content(
        string raw,
        IReadOnlyList<string> refs,
        Action<string>? note,
        Func<IReadOnlyList<MarkdownHeading>, string?, HeadingMatch?>? match = null,
        Func<string, IReadOnlyList<int>>? lineStarts = null)
    {
        match ??= SectionExtractor.Match;
        lineStarts ??= SectionExtractor.LineStarts;

        var anchors = refs.Select(r => SectionExtractor.AnchorOf(r)).ToList();
        if (anchors.Any(string.IsNullOrEmpty))
        {
            return raw;
        }

        var text = SectionExtractor.NormalizeNewlines(raw);
        var headings = SectionExtractor.Headings(text);
        var matches = anchors.Select(a => match(headings, a)).ToList();
        var unmatched = anchors.Where((_, i) => matches[i] is null).Distinct(StringComparer.Ordinal).ToList();
        if (unmatched.Count > 0)
        {
            return string.Concat(unmatched.Select(a =>
                $"> Note: the anchor \"#{a}\" was not found in this document, so the whole document is given.\n")) + raw;
        }

        // A section runs from its heading to the next heading of the same or a higher level (or the end of the file).
        var starts = matches.Select(m => m!.Index).ToList();
        var ends = starts.Select(start =>
        {
            var end = start + 1;
            while (end < headings.Count && headings[end].Level > headings[start].Level)
            {
                end++;
            }

            return end;
        }).ToList();

        var kept = new List<int>();
        for (var i = 0; i < starts.Count; i++)
        {
            var absorbed = Enumerable.Range(0, starts.Count).Any(j => starts[j] < starts[i] && starts[i] < ends[j]);
            if (!absorbed && !kept.Any(k => starts[k] == starts[i]))
            {
                kept.Add(i);
            }
        }

        var keptStarts = kept.Select(k => starts[k]).ToHashSet();
        var noted = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < starts.Count; i++)
        {
            if (matches[i]!.Ambiguous && keptStarts.Contains(starts[i]) && noted.Add(refs[i]))
            {
                note?.Invoke($"{refs[i]}: more than one heading shares the anchor key '{AnchorKey.FromAnchor(anchors[i])}'; the first was used");
            }
        }

        var offsets = lineStarts(text);
        var sections = kept.Select(k =>
        {
            var from = offsets[headings[starts[k]].StartLine];
            var to = ends[k] < headings.Count ? offsets[headings[ends[k]].StartLine] : text.Length;
            return text[from..to].TrimEnd('\n');
        });
        return string.Join("\n\n", sections) + "\n";
    }
}
