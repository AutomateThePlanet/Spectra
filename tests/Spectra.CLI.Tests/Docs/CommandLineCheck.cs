using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using System.Text.RegularExpressions;

namespace Spectra.CLI.Tests.Docs;

/// <summary>
/// Spec 100 (research R4): checks a documented <c>spectra …</c> line against the CLI's own command tree
/// (<see cref="Program.CreateRootCommand"/>), so no guard keeps a hand-written command list.
/// </summary>
/// <remarks>
/// <para><b>Structural</b> (synopsis lines): the leading words that name subcommands are the command path
/// (<c>a|b</c> at a command position means each alternative); every <c>-x</c>/<c>--x</c> token, with brackets and
/// parentheses stripped and split on <c>|</c>, must be an option of the command, of one alternative, or of an
/// ancestor. Placeholders (<c>&lt;…&gt;</c>, <c>{…}</c>, <c>…</c>), values and <c>#</c> comments are ignored.</para>
/// <para><b>Parse</b> (concrete lines): the real parser must report no errors.</para>
/// </remarks>
internal static class CommandLineCheck
{
    private static readonly HashSet<string> BuiltInOptions = new(StringComparer.Ordinal) { "--help", "-h", "-?", "--version" };

    private static readonly Lazy<RootCommand> Tree = new(Program.CreateRootCommand);

    public static RootCommand Root => Tree.Value;

    /// <summary>Null when the line is fine, else what is missing.</summary>
    public static string? Structural(string line)
    {
        var tokens = Tokens(line);
        if (tokens.Count == 0 || tokens[0] != "spectra")
        {
            return $"not a spectra line: {line}";
        }

        var path = new List<IReadOnlyList<Command>> { new[] { (Command)Root } };
        var current = (IReadOnlyList<Command>)new[] { (Command)Root };
        var inPath = true;

        foreach (var raw in tokens.Skip(1))
        {
            if (raw.StartsWith('#'))
            {
                break;
            }

            var token = raw.Trim('[', ']', '(', ')');
            if (token.Length == 0 || token == "|")
            {
                continue;
            }

            if (inPath && !token.StartsWith('-') && !IsPlaceholder(token))
            {
                var parts = token.Split('|', StringSplitOptions.RemoveEmptyEntries);
                var next = parts.Select(p => current.SelectMany(c => c.Subcommands).FirstOrDefault(s => s.Name == p || s.Aliases.Contains(p))).ToList();
                if (next.All(n => n is not null))
                {
                    current = next!;
                    path.Add(current);
                    continue;
                }

                // A plain word where the command only has subcommands (no arguments) names a missing command.
                if (current.All(c => c.Subcommands.Count > 0 && c.Arguments.Count == 0))
                {
                    var missing = parts.First(p => current.SelectMany(c => c.Subcommands).All(s => s.Name != p && !s.Aliases.Contains(p)));
                    return $"no command '{missing}' under '{Describe(current)}' in: {line}";
                }

                inPath = false;
                continue;
            }

            if (IsPlaceholder(token))
            {
                inPath = false;
                continue;
            }

            if (!token.StartsWith('-'))
            {
                continue;
            }

            inPath = false;
            foreach (var part in token.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var option = part.Trim('[', ']', '(', ')').Split('=')[0].TrimEnd(',', ';', '.');
                if (!option.StartsWith('-') || option.Length < 2 || BuiltInOptions.Contains(option))
                {
                    continue;
                }

                var known = path.SelectMany(level => level).SelectMany(c => c.Options).Any(o => o.HasAlias(option));
                if (!known)
                {
                    return $"no option '{option}' on '{Describe(current)}' in: {line}";
                }
            }
        }

        return null;
    }

    /// <summary>Null when the real parser accepts the line, else its errors.</summary>
    public static string? Parse(string line)
    {
        var tokens = Tokens(line);
        if (tokens.Count == 0 || tokens[0] != "spectra")
        {
            return $"not a spectra line: {line}";
        }

        var args = tokens.Skip(1).TakeWhile(t => !t.StartsWith('#')).ToArray();
        var result = Root.Parse(args);
        return result.Errors.Count == 0 ? null : $"{string.Join("; ", result.Errors.Select(e => e.Message))} in: {line}";
    }

    /// <summary>Every command path in the tree below the root, e.g. <c>["ai", "update-triage"]</c>.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> CommandPaths()
    {
        var result = new List<IReadOnlyList<string>>();

        void Walk(Command command, List<string> prefix)
        {
            foreach (var sub in command.Subcommands)
            {
                var path = new List<string>(prefix) { sub.Name };
                result.Add(path);
                Walk(sub, path);
            }
        }

        Walk(Root, []);
        return result;
    }

    /// <summary>Shell-like split: whitespace separates, single and double quotes group (quotes removed).</summary>
    public static IReadOnlyList<string> Tokens(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        char? quote = null;
        var any = false;
        foreach (var ch in line.Trim())
        {
            if (quote is not null)
            {
                if (ch == quote)
                {
                    quote = null;
                }
                else
                {
                    sb.Append(ch);
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                any = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 || any)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                    any = false;
                }

                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0 || any)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }

    private static bool IsPlaceholder(string token) =>
        token.Contains('<') || token.Contains('{') || token.Contains('…') || token.Contains("...") ||
        Regex.IsMatch(token, "^[A-Z][A-Z0-9_-]*$");

    private static string Describe(IReadOnlyList<Command> commands) => string.Join("|", commands.Select(c => c.Name));
}
