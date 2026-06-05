# 05 — Orchestration artifacts (Area E)

> **Purpose.** Inventory the skills/agents/config that orchestrate the model today, locate
> them precisely (they are **not** where the migration prompt assumed), and flag every
> Copilot-ism that needs translation to `.claude/skills/<name>/SKILL.md` (ARCHITECTURE-v2
> §79–98).
>
> Investigation only. Confirmed claims cite `file:line` / path; gaps stated explicitly.

---

## 1. Where the artifacts are NOT

- `.github/skills/` — **does not exist** (searched: `.github/`). `.github/` itself exists but
  holds only `ISSUE_TEMPLATE/`, `dependabot.yml`, `pull_request_template.md`, `workflows/`.
- `.github/agents/` — **does not exist**.
- `.vscode/` (and `.vscode/mcp.json`) — **does not exist** in the repo.

So the migration prompt's Area E targets are empty. The real orchestration artifacts are
**bundled embedded resources shipped by the CLI**, below.

---

## 2. Where the artifacts actually are

Bundled under `src/Spectra.CLI/Skills/Content/`, loaded as embedded resources and installed
by `spectra update-skills` / `spectra init`:

- **Agents (2):** `Content/Agents/spectra-generation.agent.md`,
  `Content/Agents/spectra-execution.agent.md`.
- **Skills (14):** `Content/Skills/` — `spectra-coverage`, `spectra-criteria`,
  `spectra-dashboard`, `spectra-delete`, `spectra-docs`, `spectra-generate`, `spectra-help`,
  `spectra-init-profile`, `spectra-list`, `spectra-prompts`, `spectra-quickstart`,
  `spectra-suite`, `spectra-update`, `spectra-validate` (`.md` each).
- **Profile (1):** `Content/Profiles/_default.yaml`.

Loaders: `src/Spectra.CLI/Skills/AgentContent.cs:8–11` (`SkillResourceLoader.GetAllAgents()`,
exposes `ExecutionAgent`/`GenerationAgent`); `SkillsManifest.cs` (installed-file hash
tracking for update detection); `SkillResourceLoader.cs`.

> These are authored in **GitHub Copilot's** agent/skill format (YAML frontmatter with
> `tools:`, `model:`, `disable-model-invocation:`), not Claude Code's `.claude/skills/<name>/
> SKILL.md` format — the format itself is part of what migrates.

---

## 3. Existing Claude footprint in the repo

- `CLAUDE.md` (repo root) — project guidelines; line 6 still declares
  "GitHub Copilot SDK (sole AI runtime)". This will need revision post-migration (recorded,
  not edited).
- `.claude/settings.local.json` — a permissions **allowlist** (~9 KB). It pre-approves CLI
  invocations like `Bash(spectra:*)` (`:56`) and `Bash(spectra-mcp:*)` (`:49`) but contains
  **no `mcp__spectra__*` tool pre-approval** — the v2 setup requirement (ARCHITECTURE-v2 §97)
  is not yet present. (`spectra-mcp` here is a Bash command name, not the MCP tool namespace.)
- `.claude/commands/` — **9** speckit command files (`speckit.analyze.md` … 
  `speckit.taskstoissues.md`). These are the legacy `.claude/commands/` format
  (ARCHITECTURE-v2 §81 notes it still works but is deprecated in favour of skills), and they
  are SpecKit tooling, unrelated to Spectra's own orchestration.
- No `.claude/skills/` directory exists yet.

---

## 4. Copilot-isms per artifact (with translation target)

### `spectra-generation.agent.md` (generation/authoring → maps to a main-session generation skill)
- `tools: [{{GENERATION_TOOLS}}]` — `:4` (placeholder expanded at install time).
- `model: GPT-4o` — `:5` → Claude via config / removed (model chosen by the interactive
  session in v2).
- `disable-model-invocation: true` — `:6` (Copilot auto-invocation guard; reason disappears
  under Claude Code).
- `runInTerminal` / `awaitTerminal` "do NOTHING while waiting" discipline — `:15`, and Steps
  3/6 (`:67, 78`). Ports in spirit to Claude Code's terminal model (ARCHITECTURE-v2 §86–88).
- "Do NOT ask the user clarifying questions about count or scope" — `:124` (Copilot
  confirmation-avoidance; translate to Claude Code's confirmation model, don't copy).
- **Role:** generation + authoring + a delegation table to the other skills (`:132–146`).
  Maps to a `.claude/skills/<generation>/SKILL.md` running in the **main session**.

### `spectra-generate.md` (skill; same Copilot frontmatter)
- `tools: [{{GENERATE_TOOLS}}]` `:4`; `model: GPT-4o` `:5`; `disable-model-invocation: true`
  `:6`; runInTerminal-based body (`:11+`). Same translation as above.

### `spectra-execution.agent.md` (execution → keeps MCP; agent file re-authored)
(Full detail in `04-execution.md` §4.) `model: GPT-4o` `:13`; `disable-model-invocation`
`:14`; `github/get_copilot_space` + `github/list_copilot_spaces` tools `:6–7`; `askQuestion`/
`askForConfirmation` ban `:25`; `runInTerminal`/`awaitTerminal` `:110, 125`; Copilot Spaces
doc-lookup section `:91–93`.

### The other 12 skills
`spectra-coverage/criteria/dashboard/delete/docs/help/init-profile/list/prompts/quickstart/
suite/update/validate` — all share the same frontmatter shape (`model: GPT-4o`,
`disable-model-invocation`, `{{…TOOLS}}` placeholder) and `runInTerminal` discipline. They
are **authoring/CLI-task** skills → each maps to a `.claude/skills/<name>/SKILL.md` in the
main session. None is a critic.

### The critic skill (net-new)
There is **no** critic skill or agent today — the critic is in-process C# (`02-critic.md`).
The v2 `context: fork` critic skill (ARCHITECTURE-v2 §84) is net-new orchestration, invoked
as a mandatory explicit step inside the generation skill.

---

## 5. Mapping summary

| Copilot artifact | v2 target | Role |
|---|---|---|
| `spectra-generation.agent.md` | `.claude/skills/<gen>/SKILL.md` (main session) | generation + authoring, mandates critic step |
| `spectra-execution.agent.md` | `.claude/skills/<exec>/SKILL.md` (main session) + MCP | execution loop; drop `github/*`, GPT-4o |
| `spectra-generate.md` + 13 skills | `.claude/skills/<name>/SKILL.md` each | authoring / CLI-task skills |
| (none today) | `.claude/skills/<critic>/SKILL.md` via `context: fork` | net-new isolated critic |

Every artifact carries the same three translatable Copilot-isms: `model: GPT-4o`,
`disable-model-invocation`, and the `{{…TOOLS}}` placeholder + `runInTerminal/awaitTerminal`
discipline.

---

## 6. Findings / gaps (recorded, not fixed)

- **F-1 — Migration-prompt Area E paths are empty.** `.github/skills/`, `.github/agents/`,
  `.vscode/mcp.json` do not exist; the artifacts are CLI-bundled embedded resources
  (`src/Spectra.CLI/Skills/Content/`). Any spec must target the bundled-resource pipeline
  (`SkillResourceLoader`/`SkillsManifest`), not `.github/`.
- **F-2 — `INVESTIGATION-claude-code-mechanics.md` is absent.** ARCHITECTURE-v2 §115/§136
  cites it as the source for the four GREEN/handled mechanics, but no such file exists in the
  repo (verified). Its conclusions survive only inline in ARCHITECTURE-v2 §113–122. Decision
  needed: author the file or treat §113–122 as canonical (see `00`).
- **F-3 — `CLAUDE.md` still names Copilot as "sole AI runtime"** (`CLAUDE.md:6`). Stale once
  v2 lands; recorded, not edited.
- **F-4 — No `mcp__spectra__*` pre-approval** in `.claude/settings.local.json`. Required by
  ARCHITECTURE-v2 §97 for a friction-free execution loop; absent today.

---

## 7. Conclusion

Orchestration is the **largest translation surface** but the **lowest-risk**: 2 agents + 14
skills + 1 profile, all CLI-bundled, all carrying the same handful of Copilot-isms
(`model: GPT-4o`, `disable-model-invocation`, `{{…TOOLS}}`/`runInTerminal`). Generation and
authoring skills map to main-session `.claude/skills/<name>/SKILL.md`; execution keeps MCP
with a re-authored agent file shorn of `github/*` and GPT-4o; the critic skill is net-new
(`context: fork`). Three repo-level setup items are missing and must be added: the
`mcp__spectra__*` pre-approval, the `.claude/skills/` tree, and a refreshed `CLAUDE.md`.
