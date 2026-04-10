# Quickstart: Verifying Testimize Integration

**Feature**: 038-testimize-integration
**Audience**: Reviewer / QA verifying the implementation

## Prerequisites

- A built `spectra` CLI from this branch
- (Optional) `Testimize.MCP.Server` installed via `dotnet tool install --global Testimize.MCP.Server` for Scenarios A, B, D, E
- A configured AI provider (any Copilot SDK provider)

## Scenario Z — Default disabled (no Testimize installed) — MOST IMPORTANT

This is the regression-protection scenario. It must work on every machine.

```bash
mkdir scratch && cd scratch
spectra init
cat spectra.config.json | jq '.testimize'
# Expect: {"enabled": false, "mode": "exploratory", "strategy": "HybridArtificialBeeColony", ...}

spectra ai generate --suite x --analyze-only --no-interaction --output-format json --verbosity quiet
# Expect: exit code 0, no Testimize-related output, no child processes spawned for testimize-mcp
```

**Pass criteria**:
- [ ] `spectra.config.json` contains a `testimize` section with `enabled: false`
- [ ] No process named `testimize-mcp` was spawned (verify with `ps aux | grep testimize` on Linux/macOS or Task Manager on Windows)
- [ ] Exit code 0
- [ ] No "Testimize" string anywhere in the output

## Scenario A — Health check, tool not installed

```bash
# Make sure Testimize is NOT installed
dotnet tool uninstall --global Testimize.MCP.Server 2>/dev/null || true

# Enable testimize in config (manual edit or `spectra config set` if available)
# .. set testimize.enabled = true ..

spectra testimize check
```

**Pass criteria**:
- [ ] Output reports `Enabled: true`, `Installed: NOT FOUND`
- [ ] Output includes the line `dotnet tool install --global Testimize.MCP.Server`
- [ ] Exit code 0 (this is a status command, not a failure)

```bash
spectra testimize check --output-format json
```

**Pass criteria**:
- [ ] Valid JSON
- [ ] Contains fields `enabled`, `installed`, `healthy`
- [ ] `installed: false`, `healthy: false`, `install_command` is non-null

## Scenario B — Health check, tool installed

```bash
dotnet tool install --global Testimize.MCP.Server
spectra testimize check
```

**Pass criteria**:
- [ ] Output reports `Enabled: true`, `Installed: yes`, `Healthy: ✓`
- [ ] Output reports the `Mode`, `Strategy`, and (if configured) `Settings file` status
- [ ] Command returns within 5 seconds (SC-007)

## Scenario C — Generation with Testimize disabled (regression protection)

```bash
# Disable Testimize again
# .. set testimize.enabled = false ..

mkdir -p docs
cat > docs/form.md <<'EOF'
# Username field
The username must be 3 to 20 characters.
EOF

spectra docs index --no-interaction
spectra ai generate --suite signup --no-interaction --output-format json --verbosity quiet > before.json
```

Save `before.json`. Generation behavior with Testimize disabled is the baseline (SC-003).

## Scenario D — Generation with Testimize enabled

```bash
# Enable Testimize again with the tool installed
spectra ai generate --suite signup --no-interaction --output-format json --verbosity quiet > after.json
```

**Pass criteria**:
- [ ] At least one generated test file in `tests/signup/` contains the literal value `2` or `21` (boundary values for the 3–20 range) in step text (SC-001)
- [ ] No errors, no Testimize warnings
- [ ] Comparing `after.json` to `before.json`: `analysis.breakdown.boundary` is at least 50% higher in `after` (SC-009)

## Scenario E — Tool crash mid-generation (fault injection)

This requires manual intervention or a stub MCP server. To simulate without a stub:
1. Start `spectra ai generate --suite signup` with Testimize enabled
2. While the command is running, find and kill the `testimize-mcp` child process
3. Verify SPECTRA continues, prints a warning, and produces tests with AI-approximated values

**Pass criteria**:
- [ ] Generation run completes successfully
- [ ] A warning message about Testimize being unavailable appears
- [ ] Exit code 0
- [ ] Generated tests are written for all requested behaviors (some with Testimize values, some with AI fallback)

## Scenario F — Cleanup verification

```bash
spectra ai generate --suite signup --no-interaction --output-format json --verbosity quiet
# After the command exits:
ps aux | grep testimize-mcp   # Linux/macOS
# or Get-Process testimize-mcp # Windows PowerShell
```

**Pass criteria**:
- [ ] No `testimize-mcp` process is still running after `spectra` exits (SC-006, FR-015)

## Acceptance gate

All of the following must be true:

- [ ] Scenario Z passes on every supported OS (regression protection)
- [ ] Scenario A reports installed=false and includes install instructions
- [ ] Scenario B reports healthy=true within 5s
- [ ] Scenario C produces a baseline result file
- [ ] Scenario D shows boundary count growth ≥ 50% over Scenario C
- [ ] Scenario E completes despite child process death
- [ ] Scenario F shows no orphan child processes
- [ ] `dotnet test` passes with at least 20 net new tests
