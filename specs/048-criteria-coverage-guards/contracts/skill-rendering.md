# Contract: SKILL rendering of the new signals

**Files**:
- `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md`
- `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`

Note: these are the **bundled source-of-truth** SKILL files in this repo. After release, `spectra update-skills` copies them into the consumer project's `.github/skills/` directory; the user's input referenced that install path, but edits must land on the bundled files. See `MEMORY.md` for the related "sync demo skills" instruction.

## `spectra-docs.md` — render `criteria_warning`

The skill already documents how to read `.spectra-result.json` after `spectra docs index`. Add a short section instructing the agent to surface `criteria_warning` when present.

### Required content (additive)

A "Criteria warning surfacing" subsection that says, in substance:

> After `docs index` completes, if the result's `criteria_warning` field is present, surface it to the user as a non-blocking warning. The field is a single sentence telling the user the exact command to run; render it verbatim. The presence of `criteria_warning` does NOT mean `docs index` failed — the command's `status` will still be `completed`.

The skill MUST NOT prompt the user; it just surfaces the message.

## `spectra-generate.md` — render `notes`

The skill already documents the structure of `.spectra-result.json` after `ai generate`. Add a short section instructing the agent to surface each entry in `notes` when present.

### Required content (additive)

A "Notes surfacing" subsection that says, in substance:

> After `ai generate` completes, if the result's `notes` collection is present and non-empty, render each entry as a short note immediately after the results summary. Notes describe situations the user should know about (e.g. no criteria matched the suite) but are NOT failures — the `status` will still be `completed`.

The skill MUST NOT prompt or block on a note.

## Backward compatibility for SKILLs

- Both fields are absent when the conditions don't hold — the skill rendering is a no-op on existing happy-path results.
- SKILL versions that pre-date Spec 048 (i.e., have not been re-bundled into a release) simply don't render the new fields — they remain in the JSON for any other consumer to pick up. No SKILL version bump is required.

## Test contract

SKILL renderings are non-code and validated by reading the prose, not by automated tests. The contract is enforced via two human checks during code review:

| Check | What |
|---|---|
| `spectra-docs.md` contains the surfacing instruction for `criteria_warning` | Manual review of the modified skill file |
| `spectra-generate.md` contains the surfacing instruction for `notes` | Manual review of the modified skill file |

A future spec may add automated rendering snapshot tests for SKILL files; that is out of scope for 048.
