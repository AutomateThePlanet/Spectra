# Data Model: Token Economy Follow-up

No persisted format changes. The entities below are in-memory shapes, or installed files that already have a defined
format.

## Critic source item (in memory)

The existing `SourceDocument` (`Path`, `Title`, `Content`), one per cited document; `CriticPromptBuilder` renders it
unchanged.

| Field | Section mode | `--full-docs` |
|---|---|---|
| `Path` | resolved path relative to the repo (as today) | as today |
| `Title` | file name without extension (as today) | as today |
| `Content` | the cited sections joined by a blank line; or the whole file (with note lines when an anchor didn't match) | the whole file |

Rules (research R1):
- Documents are in first-reference order; each appears once.
- An anchor-less ref to a document → its whole file, read as today (bytes unchanged).
- An anchor that matches no heading → the whole file, with one note line per unmatched anchor as the content's first
  lines: `> Note: the anchor "#<anchor>" was not found in this document, so the whole document is given.`
- Matched anchors → `SectionExtractor.Extract` raw text (heading and subsections), first-reference order. A section
  covered by another cited section of the same document is absorbed; duplicates count once.
- The builder then applies today's limits: at most 5 documents, 8,000 characters per document.

## Verdict line (agent result text)

One line per test a work agent wrote and sent to the critic (contract C3):

| Form | Meaning | Main-session routing (unchanged from 093/097/098) |
|---|---|---|
| `<id> \| verdict grounded <score>` | the CLI classified grounded; the critic's line agrees | kept-grounded → `ingest-grounding --all --ids` |
| `<id> \| verdict partial <score>` | the CLI classified partial | one repair |
| `<id> \| verdict hallucinated <score>` | the CLI classified hallucinated; the critic's line agrees | `record-drop` + `delete` (generate); one repair (update, as today) |
| `<id> \| critic-disagrees <cli verdict> (critic: <critic's line>)` | the CLI's verdict and the critic's line differ | unverified, both reported; never dropped |
| `<id> \| unverified: <reason>` | no `results` entry after the one retry | unverified, with the reason and next step |
| `critic: not run` (once, no id) | the agent couldn't start the critic | the main session runs the critic per test (fallback) |

Agreement (the 093 rule): the critic's line agrees when it names the same verdict as the CLI's result, **or when the
critic gave no final line**. Then the agent reports `<id> | verdict <v> <score>` from the CLI result.
`critic-disagrees` is only for a critic line that names a different verdict than the CLI.

A test the agent reported `written` (or created) without a verdict line, and without `critic: not run`, is handled
like `critic: not run` for that test.

## Re-verify setting (update flow)

`verify: yes|no`, passed by the update skill to every `spectra-updater` and `spectra-repairer` call. It is asked once per
run, before the first updater call. `no` → no critic runs and no verdict lines; the `written` tests are reported
unverified.

## Reference file (installed)

| Aspect | Value |
|---|---|
| Source | `src/Spectra.CLI/Skills/Content/References/<skill>.md` (embedded) |
| Installed | `.claude/skills/<skill>/reference.md` |
| Manifest key | `.claude/skills/<skill>/reference.md`, SHA-256 of the content (as for every bundled file) |
| Lifecycle | 096 rules: written when missing (listed "Updated"); refreshed when unedited; never overwritten when edited ("Skipped (customized by user)"); not installed with `--skip-skills` |
| Linked from | the skill's SKILL.md: `[reference.md](reference.md)`, with what each part holds and when to read it |

## Agent frontmatter (installed and bundled)

| Field | Value |
|---|---|
| `name` | the agent name (unchanged) |
| `description` | starts `Only for use by the <skill> skill —` (or `skills` for several), ends `Do not delegate to it directly; use the <skill> skill.` |
| `tools` | analyst, extractor: `[Bash, Read, Write]`; generator, updater, repairer: `[Bash, Read, Write, Agent]`; critic: `[{{READONLY_TOOLS}}, Write]` |
| `model` | `inherit`; critic: `{{CRITIC_MODEL}}` rendered from `ai.critic.model` (097) |

No other field is allowed (FR-022).
