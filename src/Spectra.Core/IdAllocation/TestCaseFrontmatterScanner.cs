using System.Text.RegularExpressions;

namespace Spectra.Core.IdAllocation;

/// <summary>
/// Walks <c>test-cases/**/*.md</c> and extracts test IDs directly from YAML
/// frontmatter, returning the maximum numeric suffix per prefix. Used by
/// <see cref="PersistentTestIdAllocator"/> as a defense against stale or
/// missing per-suite <c>_index.json</c> files.
/// </summary>
/// <remarks>
/// Spec 040 / Decision 1: this is the filesystem leg of the
/// (HWM, indexMax, filesystemMax, idStart-1) union. Lightweight regex
/// extraction — does not invoke the full <c>TestCaseParser</c>.
/// </remarks>
public sealed class TestCaseFrontmatterScanner
{
    private static readonly Regex IdLineRegex = new(
        @"^\s*id:\s*[""']?(?<value>[A-Za-z][A-Za-z0-9_-]*-\d+)[""']?\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly string _testCasesRoot;

    public TestCaseFrontmatterScanner(string testCasesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testCasesRoot);
        _testCasesRoot = testCasesRoot;
    }

    /// <summary>
    /// Returns the maximum numeric suffix among all test IDs whose prefix
    /// matches <paramref name="idPrefix"/> (case-insensitive). Returns 0 if
    /// the directory does not exist or no matching IDs were found.
    /// </summary>
    public async Task<int> GetMaxIdNumberAsync(string idPrefix, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idPrefix);

        if (!Directory.Exists(_testCasesRoot))
        {
            return 0;
        }

        var maxNumber = 0;

        foreach (var file in EnumerateTestCaseFiles())
        {
            ct.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }

            // Frontmatter is the first --- block; we limit the regex to that
            // window to avoid matching `id:` strings that might appear in body.
            var frontmatter = ExtractFrontmatter(content);
            if (frontmatter is null)
            {
                continue;
            }

            foreach (Match match in IdLineRegex.Matches(frontmatter))
            {
                var id = match.Groups["value"].Value;
                var dashIndex = id.LastIndexOf('-');
                if (dashIndex < 0)
                {
                    continue;
                }

                var prefix = id[..dashIndex];
                if (!prefix.Equals(idPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(id[(dashIndex + 1)..], out var n) && n > maxNumber)
                {
                    maxNumber = n;
                }
            }
        }

        return maxNumber;
    }

    /// <summary>
    /// Returns every test ID found across the workspace (frontmatter scan)
    /// alongside the file path it came from. Used by
    /// <c>spectra doctor ids</c> to detect duplicates and index mismatches.
    /// </summary>
    public async Task<IReadOnlyList<(string Id, string File)>> EnumerateAllIdsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_testCasesRoot))
        {
            return Array.Empty<(string, string)>();
        }

        var results = new List<(string Id, string File)>();

        foreach (var file in EnumerateTestCaseFiles())
        {
            ct.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                continue;
            }

            var frontmatter = ExtractFrontmatter(content);
            if (frontmatter is null)
            {
                continue;
            }

            foreach (Match match in IdLineRegex.Matches(frontmatter))
            {
                results.Add((match.Groups["value"].Value, file));
            }
        }

        return results;
    }

    private IEnumerable<string> EnumerateTestCaseFiles()
    {
        return Directory
            .EnumerateFiles(_testCasesRoot, "*.md", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).StartsWith('_'));
    }

    private static string? ExtractFrontmatter(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        // Skip any leading whitespace/BOM
        var i = 0;
        while (i < content.Length && char.IsWhiteSpace(content[i]))
        {
            i++;
        }

        if (i + 3 > content.Length || content[i] != '-' || content[i + 1] != '-' || content[i + 2] != '-')
        {
            return null;
        }

        // Find the closing fence
        var startOfBody = i + 3;
        var lineEnd = content.IndexOf('\n', startOfBody);
        if (lineEnd < 0)
        {
            return null;
        }

        var searchStart = lineEnd + 1;
        var fenceIndex = content.IndexOf("\n---", searchStart, StringComparison.Ordinal);
        if (fenceIndex < 0)
        {
            return null;
        }

        return content[searchStart..fenceIndex];
    }
}
