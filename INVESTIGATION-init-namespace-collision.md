# Investigation: `init` namespace collision between SPECTRA and BELLATRIX CLIs

**Scope:** Read-only. No file in any target repo was modified; no `init`/write command
was run inside a real repo. Evidence is sourced primarily from code reads (file:line),
corroborated by on-disk inspection of one shared repo.

**Targets**
- A. SPECTRA framework source — `C:/SourceCode/Spectra`
- B. BELLATRIX CLI source — `C:/SourceCode/BELLATRIX-AI-Agents`
- C. Repo where both have run — `C:/SourceCode/AutomateThePlanet_SystemTests`

**Headline finding:** At **current source level the two `init` commands do NOT collide** —
their write namespaces are disjoint (SPECTRA → `.claude/…`; BELLATRIX → `.bellatrix/<target>/`).
The real, **confirmed collision is forward-looking**: BELLATRIX is mid-migration and already
stages byte-named copies of SPECTRA's entire skill+agent bundle (`spectra-*`) for install
into `.claude/`, with no hash-tracking and unconditional overwrite. Once that scaffolder is
wired up it will silently clobber SPECTRA's files and duplicate identical subagent `name:`
values. A secondary present-tense gap: SPECTRA's `.vscode/mcp.json` writer is skip-if-exists
(no merge), an asymmetric silent-loss.

---

## CONFIRMED (file:line evidence)

### A. SPECTRA `spectra init` (`C:/SourceCode/Spectra`)

Handler: `src/Spectra.CLI/Commands/Init/InitHandler.cs`. Install layout:
`src/Spectra.CLI/Skills/SkillInstallLayout.cs`.

**A1 — Files/dirs it creates or modifies** (`InitHandler.cs`):
- `spectra.config.json` (always overwrites, no existence check)
- `.claude/skills/<name>/SKILL.md` — 15 authoring skills (see A3)
- `.claude/skills/spectra-generation/SKILL.md`, `.claude/skills/spectra-execution/SKILL.md`
  (the generation/execution agents routed as main-session skills — `SkillInstallLayout.cs:21-22`)
- `.claude/agents/spectra-critic.agent.md` (the only file in `.claude/agents/` — `SkillInstallLayout.cs:23`)
- `.claude/settings.json` (merge — `ClaudeSettingsInstaller.cs:64-77`)
- `.vscode/mcp.json` (skip-if-exists — `InitHandler.cs:694-713`)
- `.github/skills/test-generation/SKILL.md` (one legacy file — `InitHandler.cs:25,549`)
- `USAGE.md`, `CUSTOMIZATION.md`, `profiles/_default.yaml`
- `.spectra/prompts/*.md` (5 templates), `.spectra/skills-manifest.json` (`SkillsManifest.cs:33`)
- `docs/criteria/_criteria_index.yaml`, `docs/criteria/sample.criteria.yaml`
- `.github/workflows/deploy-dashboard.yml`, `templates/bug-report.md`
- `.gitignore` (idempotent append of missing patterns), `docs/_index/_manifest.yaml`
- Items in the skills/agents/templates set are skipped with `--skip-skills`.

**A2 — CLAUDE.md: NOT written.** CONFIRMED by grep over `C:/SourceCode/Spectra/src` for
`CLAUDE\.md|copilot-instructions` → **No matches**. `spectra init` never creates, overwrites,
or appends `CLAUDE.md` or `.github/copilot-instructions.md` in a target repo. (The `CLAUDE.md`
at the Spectra repo root is the framework's own developer doc, not an init output.)

**A3 — Skills format:** `.claude/skills/<name>/SKILL.md`
(`SkillInstallLayout.cs:12-13` → `Path.Combine(root, ".claude", "skills", skillName, "SKILL.md")`).
All 15 prefixed `spectra-`: `generate, update, coverage, dashboard, validate, list,
init-profile, help, criteria, docs, prompts, quickstart, delete, suite, execute`.
No `.claude/commands/` path is used by current source.

**A4 — Subagents → `.claude/agents/`:** exactly one file,
`.claude/agents/spectra-critic.agent.md`, frontmatter `name: spectra-critic`
(`SkillInstallLayout.cs:23`). `spectra-generation`/`spectra-execution` route to `.claude/skills/…`
(A1), not to `.claude/agents/`.

**A5 — MCP config:**
- `.vscode/mcp.json`: **skip-if-exists, no merge** — `InitHandler.cs:694-698` returns early if the
  file exists. On first run writes server key `"spectra"` → `{"command":"spectra-mcp","args":["."]}`.
  ⚠️ If the file already exists, SPECTRA never registers its server (silent no-op).
- No `.mcp.json` write path exists.
- `.claude/settings.json`: **merge** of `mcp__spectra__*` into `permissions.allow`, preserving
  existing entries (`ClaudeSettingsInstaller.cs:27-57,64-77`).

**A6 — Safe-update scope:** `UpdateSkillsHandler.cs:44-78,134-141` iterates only its own known
sets (`SkillContent.All`, `AgentContent.All`, `BuiltInTemplates.AllTemplateIds`, `_default.yaml`,
`CUSTOMIZATION.md`), keyed by the absolute paths it recorded in `.spectra/skills-manifest.json`.
It never enumerates the filesystem and skips any file whose on-disk hash differs from the recorded
hash. It **cannot touch files it did not author** — scoped to SPECTRA's own files only.

**A7 — Re-run / `--force`:** Without `--force`, init aborts if `spectra.config.json` exists
(`InitHandler.cs:53-58`, `ExitCodes.Error`). `--force` (`InitCommand.cs:19-22`) clobbers: config,
all `.claude/skills/*/SKILL.md`, `.claude/agents/spectra-critic.agent.md`, `USAGE.md`,
`.spectra/prompts/*`, `_default.yaml`, `CUSTOMIZATION.md`; merges `settings.json`. **Even with
`--force` it never clobbers** `.vscode/mcp.json`, `deploy-dashboard.yml`, `templates/bug-report.md`,
the criteria templates, or `.gitignore` (unconditional exists-guards that ignore `--force`).

### B. BELLATRIX CLI `init` (`C:/SourceCode/BELLATRIX-AI-Agents`)

Language: **C#/.NET** (`src/Bellatrix.Cli/`). Dispatch: `CliRunner.cs`. Implementation:
`Commands/ScaffoldCommand.cs`.

**B0 — `init` is a thin alias for `scaffold`, and incomplete.** `CliRunner.cs:44-46`:
`"init" or "update-skills" => ScaffoldCommand.Run(ctx)`. `ScaffoldCommand.Run` calls
`ctx.RequireOpt("target")` (`ScaffoldCommand.cs:18`), so bare `bellatrix init` (no `--target`)
**throws a usage `CliException` and writes nothing.**

**B1 — Files it writes** (`ScaffoldCommand.cs:24-31`), only with `--target <claude|codex|gemini|copilot>`:
- `.bellatrix/<target>/bellatrix-desktop-locator-specialist.md`
- `.bellatrix/<target>/bellatrix-desktop.md`

That is the **entire** write surface — disjoint from SPECTRA's namespace.

**B2 — CLAUDE.md: NOT written.** CONFIRMED by grep over `C:/SourceCode/BELLATRIX-AI-Agents/src` →
only a doc *reference* in `Bellatrix.Web.Mcp.Server/PROMPTS_GUIDE.md:495`, no init write. No
`.github/copilot-instructions.md` write either.

**B3 — Skills format:** flat `.md` files under `.bellatrix/<target>/` (`ScaffoldCommand.cs:24,27-28`)
— NOT `.claude/skills/<name>/SKILL.md`, NOT `.claude/commands/`, NOT `.github/skills/`. Two file
names, frontmatter `name:` = `bellatrix-desktop-locator-specialist` / `bellatrix-desktop`
(`ScaffoldCommand.cs:49,75`).

**B4 — Subagents → `.claude/agents/`: none.** Current init writes no subagent files anywhere.

**B5 — MCP config: not touched.** No `.mcp.json` / `.vscode/mcp.json` write path in the CLI.
(BELLATRIX MCP servers — `bellatrix-web-mcp`, `bellatrix-desktop-mcp` — are separate executables;
their registration is manual.)

**B6 — Safe-update: none.** `File.WriteAllText` is an **unconditional overwrite**
(`ScaffoldCommand.cs:30-31`). No hash/markers/manifest.

**B7 — Re-run / `--force`:** No `--force`. The `--regenerate` flag named in the XML doc
(`ScaffoldCommand.cs:9`) is **not checked** in `Run()` — re-runs silently overwrite the two files.

**B8 — Forward-looking (DECISIVE):** the repo stages a full SPECTRA bundle under
`newest-agents-skills/` (confirmed by glob), laid out as `skills/<name>/SKILL.md` and
`agents/<name>.agent.md` — i.e. ready to install into `.claude/skills/` & `.claude/agents/`:
- `newest-agents-skills/agents/spectra-generation.agent.md`, `…/spectra-execution.agent.md`
- `newest-agents-skills/skills/spectra-{coverage,criteria,dashboard,delete,docs,execution,generate,
  help,init-profile,list,prompts,quickstart,suite,update,validate}/SKILL.md`
- `newest-agents-skills/skills/test-generation/SKILL.md`
- plus `bifrost-*` skills and `bifrost-desktop-system-tests-generator.agent.md`

These are **the same names SPECTRA's own init emits.** Not yet wired into `ScaffoldCommand`, but a
migration plan (`08-cli-migration-plan.md`, per exploration) targets `.claude/agents/` + `.claude/skills/`.
When wired, BELLATRIX init will write `spectra-*` files into the exact paths SPECTRA owns.

### C. Shared repo on disk (`C:/SourceCode/AutomateThePlanet_SystemTests`, read-only)

**C8 — CLAUDE.md:** exactly one, at repo root (no nested CLAUDE.md). Integrates **both** tools by
design: `CLAUDE.md:1-5` ("SPECTRA manual test cases and BELLATRIX automated desktop system tests"),
a BELLATRIX skills table (`:13-29`) and a SPECTRA Integration section (`:31+`). No `@import` lines.
A manual "Sync notice" mirrors it to `.github/copilot-instructions.md` (`CLAUDE.md:90`). Neither
tool's init authored this file (per A2/B2) — it is hand- or `/init`-authored. **No overwrite
collision visible.**

**C9 — `.claude/`:** `.claude/skills/` and `.claude/agents/` do **NOT exist**. Present instead:
`.claude/commands/` (5 bridge commands: `automate-suite, automate-test, check-coverage,
execute-scenario, generate-tests`) and a nested `BELLATRIX/.claude/settings.local.json`. All actual
skills/agents live under `.github/`:
- `.github/agents/`: `bellatrix-desktop-system-tests-generator.agent.md`
  (`name: BELLATRIX Desktop System Tests Generator`), `spectra-execution.agent.md`
  (`name: spectra-execution`), `spectra-generation.agent.md` (`name: spectra-generation`).
  **No two share a `name:`** → no silent-discard on disk here.
- `.github/skills/`: BELLATRIX `bellatrix-desktop-*.skill.md` (flat) coexisting with SPECTRA
  `spectra-*/SKILL.md` (subdirs) + a neutral `test-generation/`.

**C10 — `.vscode/mcp.json`** (read directly): all four servers coexist — `bellatrix-web-mcp`,
`bellatrix-desktop-mcp` (`:3-46`), `spectra` (`:47-50` → `spectra-mcp .`), `testimize` (`:51-54`).
**Both tools registered; no overwrite.** No root `.mcp.json`. Coexistence is consistent with manual
editing, since neither current init merges into this file.

**C11 — Legacy Copilot-era artifacts:** `chatmodes_legacy/` holds BELLATRIX `.chatmode.md` /
`.prompt.md` / `.skill.md` personas (README dated 2025-01-28, BELLATRIX 3.9.0+). One,
`bellatrix-desktop-system-tests-generator.agent.md` (`name: BELLATRIX Desktop System Tests Generator`),
corresponds to the promoted `.github/agents/` copy. The `.chatmode.md` files have no `.github/agents/`
counterpart. SPECTRA's `spectra-*` agents have no legacy equivalents.

**Cross-cutting observation:** target C's on-disk layout (`.github/skills/spectra-*`, `.claude/commands/`)
does **NOT** match what current SPECTRA source emits (`.claude/skills/`, `.claude/agents/`). CONFIRMED
that C reflects an **older SPECTRA build** + legacy Copilot-era BELLATRIX — not the current init code
paths of either tool.

---

## INFERRED (not confirmed from code/disk)

- BELLATRIX *intends* to install the staged `newest-agents-skills/` bundle into `.claude/` (migration
  plan referenced during exploration; the wiring is absent from `ScaffoldCommand`). `08-cli-migration-plan.md`
  was not read first-hand in this pass — treat the exact target paths and `--force`/overwrite semantics of
  the future BELLATRIX scaffolder as **unknown**.
- The four coexisting servers in C's `.vscode/mcp.json` were *likely* merged manually (no current init
  merges that file), but the authoring event is not recoverable from the repo.
- Why C uses `.github/skills/` for SPECTRA while current source uses `.claude/skills/` is *inferred* to be
  a version gap (older spectra), not directly dated.

---

## Phase 4 — Collision matrix

| Shared path | SPECTRA init | BELLATRIX init (current) | BELLATRIX (staged/planned) | Collision class | Evidence |
|---|---|---|---|---|---|
| `CLAUDE.md` | not written | not written | unknown | **no-collision (now)** | grep: no matches in either src; `CLAUDE.md:1-5,90` |
| `.claude/skills/<name>/SKILL.md` | writes 17 `spectra-*` | not written (writes `.bellatrix/<target>/`) | stages identical `spectra-*` + `bifrost-*` | **silent-loss (future)** | `SkillInstallLayout.cs:12-13`; `ScaffoldCommand.cs:24-31`; `newest-agents-skills/skills/spectra-*` |
| `.claude/agents/*.agent.md` | `spectra-critic.agent.md` | not written | stages `spectra-{generation,execution}.agent.md` | **silent-loss / name-collision (future)** | `SkillInstallLayout.cs:23`; `newest-agents-skills/agents/spectra-*` |
| Subagent `name:` (`spectra-generation`/`-execution`) | emitted (as skills) | — | identical names staged | **name-collision (future)** | C9 names; `newest-agents-skills/agents/` |
| `.vscode/mcp.json` | skip-if-exists, key `spectra` | not touched | unknown | **silent-loss (asymmetric, now)** — if anyone writes the file first, SPECTRA never registers | `InitHandler.cs:694-713`; `mcp.json:47-54` |
| `.claude/settings.json` | merge `mcp__spectra__*` | not touched | unknown | **merge-safe** | `ClaudeSettingsInstaller.cs:27-57` |
| `.github/skills/` | one file `test-generation/` | not written | stages `bifrost-*`+`spectra-*` | **duplication (legacy domain)** | `InitHandler.cs:25,549`; C9 |
| `.bellatrix/<target>/` | not touched | writes 2 files | — | **no-collision** | `ScaffoldCommand.cs:24-31` |

**Q13 — commands-vs-skills drift:** Current SPECTRA source → `.claude/skills/` (`SkillInstallLayout.cs`).
Current BELLATRIX init → neither (`.bellatrix/<target>/`). Target C on disk → `.claude/commands/` +
`.github/skills/` (legacy, older builds). The drift is **temporal/legacy**, not a live disagreement
between the two *current* `init` commands.

**Q14 — subagent name collisions across tools:** None at runtime today (BELLATRIX init writes no agents).
**Guaranteed future collision** on `spectra-generation` and `spectra-execution`, which both tools' bundles
name identically (`newest-agents-skills/agents/` vs SPECTRA `AgentContent`).

---

## Verification performed (read-only)

- Source reads (file:line above): `InitHandler.cs`, `SkillInstallLayout.cs`, `ClaudeSettingsInstaller.cs`,
  `SkillsManifest.cs`, `UpdateSkillsHandler.cs` (SPECTRA); `CliRunner.cs`, `ScaffoldCommand.cs` (BELLATRIX).
- Greps: `CLAUDE\.md|copilot-instructions` over both `src` trees.
- Globs: `newest-agents-skills/**` (BELLATRIX staging); SPECTRA `SkillInstallLayout.cs`.
- Direct disk reads in target C: `.vscode/mcp.json` (full), `CLAUDE.md` (head).
- No tool was run; no file in any target repo was modified.

---

## Open / unknown (carry into the contract spec)

1. Exact future BELLATRIX scaffold target paths + overwrite policy (`08-cli-migration-plan.md` not read first-hand).
2. Whether the contract should mandate **merge** for `.vscode/mcp.json` (SPECTRA's current skip-if-exists is an asymmetric silent-loss).
3. Ownership/marker scheme for a shared `CLAUDE.md` (today purely manual + mirror to `copilot-instructions.md`).

---

## Conclusion

**Yes — we have enough evidence to write the shared-namespace init contract spec.** The contract is
justified not by a present-tense overwrite (the two *current* `init` namespaces are disjoint) but by a
**confirmed, file-named future collision**: BELLATRIX already stages SPECTRA's exact `spectra-*`
skill+agent set for install into `.claude/`, with no hash-tracking and unconditional overwrite — so once
wired it will silently clobber SPECTRA's files, and the identical subagent `name:` values
(`spectra-generation`, `spectra-execution`) create a discard hazard. Secondary confirmed gap: SPECTRA's
`.vscode/mcp.json` writer is skip-if-exists (no merge), an asymmetric silent-loss. Still unknown: the three
items listed above. **The spec is NOT written here, as instructed.**
