# Data Model: Workspace Safety and Hygiene

## Skills manifest — `.spectra/skills-manifest.json` (per clone, gitignored)

```json
{
  "version": "1.0",
  "files": {
    ".claude/skills/spectra-generate/SKILL.md": "<sha256>",
    ".spectra/prompts/test-generation.md": "<sha256>",
    "profiles/_default.yaml": "<sha256>",
    "CUSTOMIZATION.md": "<sha256>"
  },
  "gitignore_offered": ["…"]
}
```

| Rule | |
|---|---|
| key | repo-relative, `/` separators (`ManifestKey.For(root, path)`) |
| load | absolute keys under the root → relative; `\` → `/`; absolute keys outside the root re-rooted by their install-path tail (`.claude/skills/<name>/SKILL.md`, `.claude/agents/<file>`, `.spectra/prompts/<id>.md`, `profiles/_default.yaml`, `CUSTOMIZATION.md`, `USAGE.md`; last segments, case-insensitive on Windows); unrecognised foreign keys dropped with one summary warning, "N manifest entries from another location were dropped (was the workspace moved?)" |
| duplicates | keys that normalise to one path keep **all** their hashes as candidates; the file is unedited when its hash equals any candidate, else customized |
| save | only normalised keys, one entry per key: the matching candidate, or the bundled hash after an update |
| `gitignore_offered` | legacy (090–095): read once to migrate to markers; no longer written |

## update-skills decision per installed file

| File on disk | Manifest record | Bundled vs file | Action | Report |
|---|---|---|---|---|
| missing | any | — | write, record | Updated |
| present | none | equal | record | Unchanged |
| present | none | differs | **none** | "skipped (no record — it may have been edited); run spectra prompts reset <id> or delete it to get the new version" (templates) / "…; delete it to get the new version" (others) |
| present | no candidate = file hash | — | none | Skipped (customized by user) |
| present | a candidate = file hash | equal | record that hash | Unchanged |
| present | a candidate = file hash | differs | write, record the bundled hash | Updated |

## Offered marker — `.gitignore` (committed)

```
# SPECTRA
.spectra/progress.json
…
# spectra-offered: .spectra/progress.json, …, .spectra/verdicts/, .spectra/dropped-tests.json
```

- A pattern is offered when any `# spectra-offered:` line names it. Patterns are separated by `, `.
- `init` writes one marker line naming all patterns. `update-skills` appends one marker line per run for the patterns
  that are newly offered or migrated.
- Pattern presence and negation rules are the 090 ones.

## Suite delete result additions

| Field | Meaning |
|---|---|
| `dependents_cleaned` | `{id, file, removed}` per scalar dependent in other suites |
| `warnings` | list or unparseable dependents (not edited), manifest/IO notes |
| `leftovers_removed` (dry run: `leftovers`) | repo-relative paths: `.spectra/updates/<suite>/`, `.spectra/repairs/<suite>/`, `.spectra/generation-plan/<suite>.json`, `.spectra/verdicts/critic-verdict-<id>.json`, `.spectra/verdicts/critic-input-<id>.json` |

`external_dependency_cleanup` keeps its shape and is now derived from the deletion service's plan.

## validate

`status: "failed"`, `message: "<n> error(s) found."`, exit 1 when the validator reports errors or when any test failed
to parse or any profile error was counted.
