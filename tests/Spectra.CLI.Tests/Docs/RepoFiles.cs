using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100: reads repository files for the documentation guards (README, docs/, CLAUDE.md, bundled content) and splits
/// Markdown into paragraphs — a paragraph is a run of non-blank lines; a list item, a table row and a fenced code block
/// are always their own paragraph (data-model.md "AllowRule").
/// </summary>
internal static class RepoFiles
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    private static readonly Regex ItemStart = new(@"^\s*([-*+]|\d+\.)\s|^\s*\|", RegexOptions.CultureInvariant);

    public static string Root => RootPath.Value;

    public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public static string Read(string relative) => File.ReadAllText(PathOf(relative));

    public static bool Exists(string relative) => File.Exists(PathOf(relative)) || Directory.Exists(PathOf(relative));

    /// <summary>Repo-relative paths (with <c>/</c>) of the <c>*.md</c> files under <paramref name="folder"/>, sorted.</summary>
    public static IReadOnlyList<string> MarkdownUnder(string folder) =>
        Directory.EnumerateFiles(PathOf(folder), "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<string> Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

    /// <summary>Paragraphs with the 1-based line each starts on.</summary>
    public static IReadOnlyList<(int Line, string Text, bool Fenced)> Paragraphs(string text)
    {
        var lines = Lines(text);
        var result = new List<(int, string, bool)>();
        var current = new List<string>();
        var start = 0;
        string? fence = null;

        void Flush(bool fenced)
        {
            if (current.Count > 0)
            {
                result.Add((start, string.Join('\n', current), fenced));
                current.Clear();
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (fence is null && (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)))
            {
                Flush(fenced: false);
                fence = trimmed[..3];
                start = i + 1;
                current.Add(line);
                continue;
            }

            if (fence is not null)
            {
                current.Add(line);
                if (trimmed.StartsWith(fence, StringComparison.Ordinal) && current.Count > 1)
                {
                    Flush(fenced: true);
                    fence = null;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                Flush(fenced: false);
                continue;
            }

            // A list item or a table row starts its own paragraph: a removal marker in one item never covers another.
            if (current.Count > 0 && ItemStart.IsMatch(line))
            {
                Flush(fenced: false);
            }

            if (current.Count == 0)
            {
                start = i + 1;
            }

            current.Add(line);
        }

        Flush(fenced: fence is not null);
        return result;
    }

    /// <summary>The text of the Markdown section whose heading line is <paramref name="heading"/>, up to the next heading
    /// of the same or a higher level; null when absent.</summary>
    public static string? Section(string text, string heading)
    {
        var lines = Lines(text);
        var level = heading.TakeWhile(c => c == '#').Count();
        var startIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd() == heading)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            return null;
        }

        var end = lines.Count;
        var inFence = false;
        for (var i = startIndex + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }

            if (inFence)
            {
                continue;
            }

            var m = Regex.Match(lines[i], "^(#+) ");
            if (m.Success && m.Groups[1].Length <= level)
            {
                end = i;
                break;
            }
        }

        return string.Join('\n', lines.Skip(startIndex).Take(end - startIndex));
    }

    /// <summary>The lines inside fenced code blocks of <paramref name="text"/>.</summary>
    public static IEnumerable<string> FencedLines(string text)
    {
        var inFence = false;
        foreach (var line in Lines(text))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                yield return line;
            }
        }
    }

    /// <summary>Inline code spans (<c>`…`</c>) outside fenced blocks.</summary>
    public static IEnumerable<string> InlineCode(string text)
    {
        var inFence = false;
        foreach (var line in Lines(text))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            foreach (Match m in Regex.Matches(line, "`([^`]+)`"))
            {
                yield return m.Groups[1].Value;
            }
        }
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }
}
