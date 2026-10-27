using System.Text;

namespace Spectra.CLI.Install;

/// <summary>
/// Spec 104 (R1/R3): registers SPECTRA in a human-owned instructions file (<c>AGENTS.md</c>, <c>GEMINI.md</c>) with ONE
/// marker block holding a single line. Bytes outside the block are never rewritten; a block whose line changed is replaced
/// in place (keeping the file's line endings); a missing block is appended. The markers differ from BELLATRIX Agent's
/// (<c>bellatrix</c>), so both tools keep their own block in the same file.
/// <code>
/// &lt;!-- &gt;&gt;&gt; spectra &gt;&gt;&gt; --&gt;
/// @./.spectra/spectra.instructions.md
/// &lt;!-- &lt;&lt;&lt; spectra &lt;&lt;&lt; --&gt;
/// </code>
/// </summary>
public static class MarkerBlock
{
    public const string Open = "<!-- >>> spectra >>> -->";

    public const string Close = "<!-- <<< spectra <<< -->";

    public static string Block(string line) => $"{Open}\n{line}\n{Close}\n";

    /// <summary>Writes (or plans, when <paramref name="dryRun"/>) the block; returns "created", "added", "updated" or "unchanged".</summary>
    public static string Register(string hostPath, string line, bool dryRun)
    {
        var block = Block(line);
        if (!File.Exists(hostPath) || new FileInfo(hostPath).Length == 0)
        {
            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hostPath)!);
                File.WriteAllText(hostPath, block);
            }

            return "created";
        }

        var original = File.ReadAllText(hostPath);
        var open = original.IndexOf(Open, StringComparison.Ordinal);
        var close = open < 0 ? -1 : original.IndexOf(Close, open, StringComparison.Ordinal);
        if (open >= 0 && close > open)
        {
            var end = close + Close.Length;
            if (original[open..end].Replace("\r\n", "\n") + "\n" == block)
            {
                return "unchanged";
            }

            var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            if (!dryRun)
            {
                File.WriteAllText(hostPath, original[..open] + block.TrimEnd('\n').Replace("\n", newline) + original[end..]);
            }

            return "updated";
        }

        var append = new StringBuilder();
        if (!original.EndsWith('\n'))
        {
            append.Append('\n');
        }

        append.Append('\n').Append(block);
        if (!dryRun)
        {
            File.AppendAllText(hostPath, append.ToString());
        }

        return "added";
    }
}
