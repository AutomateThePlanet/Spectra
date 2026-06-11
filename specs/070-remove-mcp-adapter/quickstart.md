# Quickstart / Manual Verification: Remove the Spectra.MCP Execution Adapter

These steps validate the acceptance criteria after implementation. Run from repo root.

## 1. Build with `Spectra.MCP` gone (SC-004)

```bash
dotnet build Spectra.slnx
```

Expect: success, no `Spectra.MCP` project built, no dangling references.

```bash
# No Spectra.MCP reference remains anywhere that ships/builds
grep -rln "Spectra\.MCP\b\|spectra-mcp\|mcp__spectra" \
  --include=*.csproj --include=*.cs --include=*.md --include=*.json --include=*.slnx \
  src/ tests/ docs/ Spectra.slnx | grep -v /obj/ | grep -v /bin/
```

Expect: matches only for the **cosmetic `Spectra.MCP.*` namespaces inside `Spectra.Execution`** and the
test files that reference those namespaces (FR-017 — rename out of scope). **No** match for the deleted
`Spectra.MCP` project files, `spectra-mcp` tool, init MCP installers, or `mcp__spectra__*` in shipped skill/agent/docs.

## 2. Full test suite green, canary unmodified (SC-004/SC-006/SC-007)

```bash
dotnet test Spectra.slnx
```

Expect: green. Confirm git shows **no modifications** to `Spectra.Core.Tests`, `TestPersistenceService`
tests, or the **pre-existing** `Spectra.Execution.Tests` files (only additions from relocation/porting).

## 3. Full execution lifecycle with no MCP server (SC-001 / User Story 1)

In a scratch workspace with only the `spectra` CLI:

```bash
spectra init                         # see step 4 — no MCP wiring emitted
spectra run start checkout --output-format json
spectra run advance --status pass
spectra run advance --status fail --notes "defect at step 3"
spectra run pause
spectra run resume
spectra run screenshot-clipboard
spectra run bulk-record --status skip --remaining --reason "env down"
spectra run finalize --output-format json
```

Expect: each step succeeds; an HTML report is written under `.execution/reports/`; **no MCP process** is
started or required at any point.

## 4. `spectra init` emits no MCP wiring (SC-002 / User Story 2)

```bash
spectra init
test ! -f .vscode/mcp.json && echo "OK: no .vscode/mcp.json"
grep -q "mcp__spectra__" .claude/settings.json 2>/dev/null && echo "FAIL: allowlist present" || echo "OK: no allowlist entry"
```

Expect: `OK` on both. If a peer `.vscode/mcp.json` already existed (e.g. a BELLATRIX server), confirm it is
left untouched (SPECTRA neither wrote to nor removed it).

## 5. Skill + agent reference only `spectra run` (SC-003 / User Story 3)

```bash
grep -i "mcp" src/Spectra.CLI/Skills/Content/Skills/spectra-execute.md
grep -i "spectra.*mcp\|mcp__spectra" src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md
```

Expect: no SPECTRA-MCP **execution path** guidance. References to the **separate** SUT-driving MCP
(BELLATRIX/Nova) and the bug-logging MCP (Azure DevOps) may remain. The `ExecuteSkillTests` /
`ExecutionAgentPortTests` encode this as automated assertions.

## 6. Coverage mapping table complete (SC-005)

Confirm `research.md` R4 has a disposition for every `tests/Spectra.MCP.Tests/` file and that each
RETIRE/PORT row names its surviving equivalent. No row may be deleted without an equivalent.

## 7. Architecture docs updated (FR-015)

```bash
grep -n "Execution → MCP, only here\|Do not unify\|stateful session → MCP" \
  docs/architecture/ARCHITECTURE-v2.md docs/specs/ARCHITECTURE-v2.md
```

Expect: no matches (claims removed/rewritten to the single CLI execution surface).
