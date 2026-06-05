# Quickstart: Execution Agent Port

Manual verification that the execution agent is de-Copilot'd, installs under `.claude/`, the allowlist
is present, and the MCP engine is untouched. All checks are static/local (no model, no run needed).

## Prerequisites

```bash
dotnet build
```

## 1. The engine is untouched (FR-001 / SC-001)

```bash
git diff --name-only origin/claude-code-v2... | grep '^src/Spectra.MCP/' ; echo "exit=$?"  # expect NO matches
dotnet test tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj   # expect all green, unchanged
```

## 2. One canonical execution skill under `.claude/`, no duplicates (FR-007)

```bash
spectra init --no-interaction
test -f .claude/skills/spectra-execution/SKILL.md           # exists
test ! -e .github/agents/spectra-execution.agent.md         # gone
test ! -e .github/skills/spectra-execution/SKILL.md         # gone
test ! -e src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md  # deleted from repo
```

## 3. No Copilot-isms in the execution skill (FR-002 / FR-003 / SC-002)

```bash
exec=src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md
grep -nE "model: GPT-4o|disable-model-invocation|get_copilot_space|list_copilot_spaces|copilot_space|runInTerminal|awaitTerminal" $exec ; echo "exit=$?"  # expect NO matches
grep -nE "source_refs|read the .*doc|documentation file" $exec   # native file-read doc-lookup present
```

## 4. Dead config gone, legacy config still loads (FR-003 / SC-004)

```bash
grep -n "copilot_space" src/Spectra.Core/Models/Config/ExecutionConfig.cs ; echo "exit=$?"  # expect NO matches
# back-compat covered by the net-new Core test: a config carrying execution.copilot_space deserializes.
```

## 5. MCP allowlist present, distinct from Bash(spectra-mcp:*) (FR-005 / SC-005)

```bash
grep -n "mcp__spectra__\*" .claude/settings.json            # present under permissions.allow
grep -n "spectra-mcp" .claude/settings.local.json           # the OLD, distinct Bash entry — unchanged
```

## 6. Verdict pause preserved (FR-004 / SC-006)

Inspect `.claude/skills/spectra-execution/SKILL.md`:
- presents one result at a time and asks for the verdict in plain text, then waits;
- never fabricates a verdict/notes and never auto-advances;
- FAIL / BLOCKED / SKIP ask for the reason before recording; BLOCKED uses `advance_test_case`.

## 7. Regression net + new tests green

```bash
dotnet test    # Spectra.MCP unchanged & green; rewritten InitCommandTests + net-new
               # ExecutionAgentPort / McpAllowlist / config-back-compat tests pass
```

## Success

- 0 `Spectra.MCP` changes; engine + 25 tools unchanged & green.
- One canonical execution skill under `.claude/skills/`; duplicates deleted.
- 0 Copilot-isms; doc-lookup via native file reads; dead config removed (legacy configs still load).
- `mcp__spectra__*` allowlist present, distinct from `Bash(spectra-mcp:*)`.
- Verdict pause preserved; screenshot-by-path unchanged.
