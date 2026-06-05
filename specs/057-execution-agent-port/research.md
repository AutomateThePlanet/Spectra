# Phase 0 Research: Execution Agent Port

All unknowns resolved. Each decision cites the in-repo evidence that grounds it (the execution
investigation `04`, the live files, and the preceding spec's `SkillInstallLayout`).

---

## D1 — How many execution-agent copies exist, and which is canonical

**Decision**: There are **three** bundled execution-agent artifacts; consolidate to **one** canonical
Claude Code skill (`Skills/Content/Agents/spectra-execution.agent.md`), and delete the other two.

- `Skills/Content/Agents/spectra-execution.agent.md` (126 lines) — installed via `AgentContent.All` →
  `SkillInstallLayout` (the preceding spec routes it to `.github/` today). **Canonical** going forward.
- `Agent/Resources/spectra-execution.agent.md` (389 lines) — installed via
  `AgentResourceLoader.GetExecutionAgentPrompt` → `InitHandler.InstallAgentFilesAsync` →
  `.github/agents/`. **Redundant duplicate** (collides with the canonical at the same `.github/` path).
- `Agent/Resources/SKILL.md` — installed via `AgentResourceLoader.GetSkillContent` →
  `.github/skills/spectra-execution/`. Already carries **0 Copilot-isms** (a short skill stub). **Redundant.**

**Rationale**: `AgentResourceLoader` is referenced **only** by `InitHandler` (grep-confirmed; no tests,
no other code), and the two `Agent/Resources/` files are explicit `EmbeddedResource` entries in the
csproj (lines 49–50). Keeping three copies in sync is exactly the duplication the spec's edge case
flags; collapsing to one makes SC-002 ("0 Copilot-isms across every bundled copy") hold by
construction and is a net simplification (Constitution V).

**Alternatives considered**:
- *Keep all three, de-Copilot each* — rejected: leaves a collision at `.github/agents/...` and three
  files to keep in sync forever; contradicts the spec's "reconcile/de-duplicate" and YAGNI.
- *Make the 389-line `Agent/Resources` copy canonical* — rejected: the canonical install path already
  flows through `SkillInstallLayout`/`AgentContent` (the mechanism the preceding spec standardized);
  routing the odd-one-out loader through that would be more churn than deleting it.

---

## D2 — Where the execution agent installs under Claude Code

**Decision**: `.claude/skills/spectra-execution/SKILL.md` — a **main-session skill**, via
`SkillInstallLayout.AgentPath("spectra-execution.agent.md")`.

**Rationale**: The execution agent is interactive orchestration the tester drives in the main session
(present test → collect verdict → advance), exactly like the generation agent the preceding spec
mapped to `.claude/skills/spectra-generation/SKILL.md`. Investigation `05` §5 maps
`spectra-execution.agent.md → .claude/skills/<exec>/SKILL.md (main session) + MCP`. The preceding spec
deliberately left execution on `.github/` via the `_` default case in `SkillInstallLayout`; this spec
changes that `_` default to an explicit execution mapping into `.claude/skills/`.

**Alternatives considered**:
- *`.claude/agents/` (subagent)* — rejected: the execution agent is not an isolated `context: fork`
  verifier; it is the main-session driver with human-in-the-loop turns. Subagent isolation would break
  the conversational verdict loop.

---

## D3 — Replacing Copilot Spaces doc-lookup with native file reads

**Decision**: Replace the "Documentation Assistance via Copilot Spaces" section with a "Documentation
lookup (read the source docs)" section: when the tester asks about a step/expected result, the agent
**reads the test case's `source_refs` documentation file(s) directly** (the Read tool) and answers
concisely. Remove the `github/get_copilot_space` / `github/list_copilot_spaces` tools from the tool
list and the `execution.copilot_space` config reference.

**Rationale**: Copilot Spaces has no Claude Code equivalent (`04` F-2) — this is a genuine feature
substitution, not a deletion. The test case already carries `source_refs` (the same field the Spaces
flow used to "find relevant documentation"), and Claude Code has native file-read; so the replacement
uses what already exists, introducing no new index or service (spec Assumptions).

**Alternatives considered**:
- *Re-home onto the docs index v2 (`docs/_index/`)* — rejected: out of scope (the spec's Out-of-Scope
  bars a new lookup mechanism); direct `source_refs` file reads are simpler and sufficient.
- *Drop doc-lookup entirely* — rejected: it is a real capability testers use mid-run; FR-003 requires a
  native replacement, not removal of the capability.

---

## D4 — The client-side MCP allowlist (`.claude/settings.json`)

**Decision**: Add a small, pure `ClaudeSettingsInstaller` that idempotently ensures
`.claude/settings.json` has `permissions.allow` containing `"mcp__spectra__*"`, preserving any existing
entries; `spectra init` calls it (creating `.claude/settings.json` if absent, merging if present), and
the Spectra repo's own `.claude/settings.json` is committed with the same entry. It is **distinct** from
the `Bash(spectra-mcp:*)` entry that lives in `.claude/settings.local.json`.

**Rationale**: FR-005 specifies `.claude/settings.json` (the committed/shared project settings), not
`settings.local.json` (local/personal). The allowlist is client-side only — `ToolRegistry.InvokeAsync`
has no server-side gate (`04` §5/F-1), so this is purely a Claude Code permission pre-approval. A
merge-safe writer (modeled on the existing idempotent `UpdateGitIgnoreAsync` in `InitHandler`) avoids
clobbering a user's existing settings. A single wildcard `mcp__spectra__*` covers all 25 tools' method
names (surfaced to the client as `mcp__spectra__<name>`).

**Alternatives considered**:
- *Write to `settings.local.json` (where `Bash(spectra-mcp:*)` already lives)* — rejected: FR-005 says
  `settings.json`; the shared project allowlist belongs in the committed file so every collaborator
  gets the friction-free loop, and the spec is explicit the two must not be conflated.
- *Enumerate all 25 `mcp__spectra__<name>` entries* — rejected: the wildcard is the documented Claude
  Code form and avoids drift when the tool set changes; the server still exposes only its 25 tools.

---

## D5 — Removing the dead `ExecutionConfig` fields safely

**Decision**: Delete `CopilotSpace` and `CopilotSpaceOwner` from `ExecutionConfig`. No back-compat shim
is needed for deserialization.

**Rationale**: Grep confirms **no C# reads** `CopilotSpace`/`CopilotSpaceOwner` (only the class declares
them); the only references were prose in the agent `.md` (removed in D3). `System.Text.Json` ignores
unknown JSON properties by default, so an existing `spectra.config.json` that still carries
`execution.copilot_space` / `copilot_space_owner` deserializes without error after the fields are gone
(SC-004). A net-new Core test pins this back-compat.

**Alternatives considered**:
- *Keep the fields `[Obsolete]`* — rejected: YAGNI; nothing reads them and unknown-key tolerance already
  guarantees back-compat, so deprecation markers would be dead ceremony.

---

## D6 — Preserving the verdict pause + translating the Copilot discipline

**Decision**: Keep the human-verdict guardrails **verbatim in intent**: present the result, ask in plain
text, wait; never fabricate a verdict/notes; never auto-advance; for FAIL/BLOCKED/SKIP ask first. Map
the Copilot-specific lines: the `askQuestion`/`askForConfirmation` ban → "use plain text" (the agent
already says this); `runInTerminal`/`awaitTerminal` "do NOTHING while waiting" → the Claude Code
Bash-tool discipline ("run the command and wait; don't poll while it runs"); `show preview <report>` →
"open `<report>`".

**Rationale**: FR-004 requires the pause to survive the port and the Copilot terminal/confirmation
phrasing to be translated, not copied. The ban and the pause are the integrity guarantee of manual
execution; they are client-neutral in substance (a plain-text question + wait), so only the Copilot
*tool names/verbs* change, not the behavior.

**Alternatives considered**:
- *Copy the Copilot phrasing verbatim* — rejected: `runInTerminal`/`askQuestion` are Copilot verbs with
  no Claude Code meaning; FR-004/FR-005 require translation in spirit.
- *Auto-advance on an obvious "pass"* — rejected outright: that is exactly the no-auto-advance guardrail
  the verdict pause exists to enforce (FR-004 / SC-006).
