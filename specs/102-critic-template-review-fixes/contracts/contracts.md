# Contracts: Review Fixes for 101 and the Critic Prompt Template

## C1 — `spectra ai compile-critic-prompt` (no new option)

Unchanged: options (`--suite`, `--test`, `--docs`, `--full-docs`), exit codes (0 / 4 / 1), refusal shape, the
`critic-input-<id>.json` record, human output (one prompt raw; several with `===== CRITIC PROMPT: <id> =====`), JSON
output (`{"prompt": …}` legacy form; `{"prompts":[{"id","prompt"}]}` suite form; nothing else on stdout).

Changed: the system part of every prompt.

| Form / mode | System part |
|---|---|
| `--suite S --test ID` (cited sections) | `critic-verification` template |
| `--suite S --test ID --full-docs` | same |
| `--suite S` (all tests) | same, for each prompt |
| `--suite S … --docs PATH` | same |
| `--test PATH [--docs PATH]` (legacy) | same |

Template resolution: `.spectra/prompts/critic-verification.md` if it parses (frontmatter + non-empty body), else the
bundled template. A broken workspace copy is not a refusal.

Golden: for the `CriticSourceWorkspace` golden workspace, `--full-docs` output (LF) = `full-docs-102.txt`; its slice from
`\n\n---\n\n## Test Case to Verify` on = the same slice of `full-docs-6a4c41a.txt`.

## C2 — internal C# seams (not public API)

```csharp
// Spectra.CLI.Agent.Critic
public sealed class CriticPromptBuilder
{
    public CriticPromptBuilder(PromptTemplateLoader templateLoader);   // required; SetTemplateLoader removed
    public string BuildSystemPrompt();                                  // template only; no criteria argument
    public string BuildUserPrompt(TestCase test, IReadOnlyList<SourceDocument> documents);   // unchanged
}

// Spectra.CLI.Verification
public static class CriticPromptCompiler
{
    public static CriticPromptCompileResult Compile(TestCase? test, IReadOnlyList<SourceDocument>? documents,
        PromptTemplateLoader templateLoader);
    public static string Assemble(TestCase test, IReadOnlyList<SourceDocument> documents,
        PromptTemplateLoader templateLoader);
}

// Spectra.Core.Coverage
public sealed class SourceRefResolver
{
    public static StringComparer PathComparer { get; }   // Ordinal on Linux, else OrdinalIgnoreCase
}

// Spectra.CLI.Source
public static class CriticSourceLoader
{
    public static Task<IReadOnlyList<SourceDocument>> LoadAsync(IReadOnlyList<string> sourceRefs, string repoRoot,
        string sourceDir, Action<string>? note, CancellationToken ct);                     // unchanged signature
    internal static Task<IReadOnlyList<SourceDocument>> LoadAsync(SourceRefResolution resolution,
        StringComparer comparer, Action<string>? note, CancellationToken ct);              // grouping seam (F2)
    internal static string Content(string raw, IReadOnlyList<string> refs, Action<string>? note,
        Func<IReadOnlyList<MarkdownHeading>, string?, HeadingMatch?>? match = null,
        Func<string, IReadOnlyList<int>>? lineStarts = null);                              // counting seam (F7)
}

// Spectra.Core.Baseline
public static partial class SectionExtractor
{
    internal static List<int> LineStarts(string text);   // was private
}
```

## C3 — agent result lines

Generator final message (F4):
```
<id> | created: <title>            # one per created test
baseline: <summary>
warning: <text>
<id> | verdict <verdict> <score>   # or critic-disagrees / unverified, or the one line `critic: not run`
failed <error_code>: <first error>
stop: <reason>
```

Repairer step 5 (F3), with `verify: yes` (the default):
- the critic for each final `written` id, one at a time;
- `spectra ai ingest-verdict --suite <suite> --all --ids <written and unchanged ids> --output-format json`;
- the existing retry for an id with no `results` entry;
- one verdict line per `written` and `unchanged` id.

The verify block shared by the three agents: from "`` `spectra-critic` subagent with the Agent tool, passing only ``" to
"routes each test by your verdict line.", identical in all three. Allowed differences, all in the heading paragraph
before it:

| Agent | Step | Ids verified | `verify:` rule |
|---|---|---|---|
| `spectra-generator` | 4 | each id in `ids` | none |
| `spectra-repairer` | 5 | final `written` lines; `unchanged` ids only go to the ingest | "Skip it with `verify: no`" |
| `spectra-updater` | 6 | final `written` lines, excluding manual-verdict-kept, `unchanged`, `unchanged_pending`, `failed` | "Only with `verify: yes`" |

## C4 — skill routing (F3, F4)

- `spectra-generate` Step 7 reads `<id> | created: <title>` as a created test. 8b, the re-critic routing and the
  fallback treat an `unchanged` repair's verdict line like a `written` one. The fallback runs the critic only for
  `written` ids. `--repaired --ids` = written or unchanged, minus dropped, never failed.
- `spectra-update` repair section: the same routing and fallback rule. Still partial → flagged for review; still
  hallucinated → the user's choice.

## C5 — tool name (F9)

- Rendered `{{GENERATE_TOOLS}}` = `Read, Write, Edit, Bash, Glob, Grep, Agent`.
- No bundled skill, reference or agent says "Task tool", "Task call(s)" or "Task prompt". The `| Task | SKILL | CLI command |`
  table header (a user's task) is not the tool and stays.
