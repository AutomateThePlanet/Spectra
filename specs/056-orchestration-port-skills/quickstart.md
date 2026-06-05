# Quickstart: Authoring Orchestration Port

Manual verification that the authoring orchestration installs as Claude Code skills, carries no
Copilot-isms, and mandates the critic step. All checks are model-free (no token spend).

## Prerequisites

```bash
dotnet build
```

## 1. Install lands under `.claude/`, not `.github/` (FR-001)

```bash
# In a scratch workspace:
spectra init --no-interaction
ls .claude/skills/                       # expect spectra-coverage/, spectra-generation/, … (SKILL.md each)
ls .claude/agents/                       # expect spectra-critic.agent.md
test ! -e .github/skills/spectra-coverage # authoring skill is NOT under .github/
ls .github/agents/spectra-execution.agent.md  # execution agent IS still under .github/ (excluded)
```

Re-run idempotently:

```bash
spectra update-skills                    # unchanged → "Unchanged"; user-modified → "Skipped"
```

## 2. No Copilot-isms remain (FR-002)

```bash
# Expect NO matches across the ported set:
grep -R "model: GPT-4o" .claude/skills/ ; echo "exit=$?"          # expect no matches
grep -R "disable-model-invocation" .claude/skills/ ; echo "exit=$?"  # expect no matches
grep -R "{{" .claude/skills/ ; echo "exit=$?"                    # expect no unexpanded placeholders
grep -R "runInTerminal\|awaitTerminal" .claude/skills/ ; echo "exit=$?"  # expect no Copilot verbs
```

The critic subagent legitimately keeps `model: claude-sonnet-4-6` + `disable-model-invocation: true`
(a `context: fork` subagent pins its model and is explicit-only):

```bash
grep "model:" .claude/agents/spectra-critic.agent.md   # claude-sonnet-4-6 (kept)
```

## 3. Generation skill mandates the critic + drives the loop (FR-003/FR-004)

Inspect `.claude/skills/spectra-generation/SKILL.md`:

- A required numbered step invokes the `spectra-critic` subagent **before** persistence — no
  "skip"/"if enabled"/auto-invocation branch.
- The procedure compiles the prompt, takes generated content, calls the model-free ingest/validate
  boundary, and on a fail-loud error regenerates with the specific error, **bounded by the retry
  limit** (stops and surfaces at the limit).
- `tools` includes `Task` (to invoke the subagent).

## 4. CLAUDE.md no longer names Copilot as the runtime (FR-006/FR-007)

```bash
grep -c "sole AI runtime" CLAUDE.md      # expect 0
```

## 5. Regression net green (SC-007)

```bash
dotnet test                              # Spectra.Core + CLI-verb tests unchanged and green;
                                         # rewritten InitCommandTests asserts .claude/ targets;
                                         # net-new install/no-copilot-ism/generation-skill tests pass
```

## Success

- Authoring skills install as `.claude/skills/<name>/SKILL.md`; critic subagent under
  `.claude/agents/`; execution agent untouched on `.github/`.
- 0 Copilot-isms in the ported set; CLAUDE.md off "sole AI runtime".
- Generation skill mandates the critic and drives the bounded loop.
- `Spectra.Core` + CLI-verb corpus unchanged and green.
