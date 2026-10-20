# Data Model: Review Fixes for 101 and the Critic Prompt Template

No persisted format changes. The entities below are the in-memory and text shapes 102 touches.

## Critic prompt

`{system}\n\n---\n\n{user}` (unchanged join).

| Part | Source after 102 | Before 102 |
|---|---|---|
| system | `critic-verification` template: `.spectra/prompts/critic-verification.md` when it parses with a non-empty body, else the bundled one; HTML comments removed, placeholders resolved to empty, trimmed | the hard-coded string in `CriticPromptBuilder` (no 037 / 075 sections), whatever the workspace template said |
| user | `## Test Case to Verify` … `Verify this test case against the documentation. Return JSON only:` — unchanged | same |

Rules:
- One system part per run: the loader is built once per `compile-critic-prompt` invocation, for both forms and modes.
- Placeholders `test_case`, `source_document`, `acceptance_criteria`: declared in the template's frontmatter, resolved
  to `""`. The test and documents are only in the user part.
- Determinism: the same test, documents and template give the same bytes.

## Critic prompt template (`critic-verification.md`)

| Field | Value |
|---|---|
| `template_id` | `critic-verification` |
| `placeholders` | `test_case`, `source_document`, `acceptance_criteria` (kept; `spectra prompts validate` stays green) |
| body | persona, output JSON shape, verdict rules, "Be strict but fair", `## Technique Verification` (037), `## Arithmetic Verification` (075) |

## Path comparer

`SourceRefResolver.PathComparer`: `StringComparer.Ordinal` on Linux, else `StringComparer.OrdinalIgnoreCase`. It is the
only rule for whether two resolved refs name one file. It is used by `Resolve` (`DistinctFullPaths`) and by
`CriticSourceLoader` (grouping).

## Cited section (per document, all anchors matched)

| Value | Definition |
|---|---|
| `start_i` | `Match(headings, anchor_i).Index` (one call per ref) |
| `end_i` | first heading index after `start_i` with level ≤ `headings[start_i].Level`, else `headings.Count` |
| absorbed(i) | ∃ j: `start_j < start_i < end_j` |
| kept | distinct `start_i` of refs not absorbed, first-reference order |
| text_i | `text[lineStarts[headings[start_i].StartLine] .. (end_i < count ? lineStarts[headings[end_i].StartLine] : text.Length)]`, `TrimEnd('\n')` |
| warning | for each ref (once per ref text) with an ambiguous match whose `start_i` is kept |

## Agent result lines

`<id> | <keyword> …`. The keyword decides the line kind; the free text (a title, a reason) always comes after it.

| Agent | Line | Change |
|---|---|---|
| generator | `<id> \| created: <title>` | was `<id> \| <title>` (F4) |
| updater, repairer | `<id> \| written`, `<id> \| unchanged`, `<id> \| failed <error_code>: <first error>` | unchanged |
| updater | `<id> \| written (manual verdict kept)`, `<id> \| unchanged_pending <BUCKET>: <why>` | unchanged (each already starts with a fixed keyword) |
| all three | `<id> \| verdict <verdict> <score>`, `<id> \| critic-disagrees <verdict> (critic: <line>)`, `<id> \| unverified: <reason>`, `critic: not run` | unchanged |
| repairer | verdict line per `written` **and** `unchanged` id; critic run only for `written` | F3: no critic for `unchanged` |

## Repair outcome → verdict source (F3)

| Repair outcome | Critic runs | Verdict line comes from | Routing |
|---|---|---|---|
| `written` | yes (once; retry once on no entry) | the new verdict | as 101 |
| `unchanged` | no (unless no `results` entry → the one retry) | the verdict that sent it to repair | partial → flagged-partial (generate) / flagged for review (update); hallucinated → the existing hallucinated path |
| `failed` | no | — | as 101 (unverified / waiting for the user) |
| any, `verify: no` | no | — | reported unverified, as 101 |
