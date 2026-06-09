# Quickstart: verifying the SPECTRA `.vscode/mcp.json` merge-by-key fix

## What changed

`spectra init` no longer skips `.vscode/mcp.json` when it already exists. It now **merges** its
`spectra` MCP server entry by key, preserving every other tool's server entry.

## Manual end-to-end check (throwaway dir)

```bash
# 1. Set up a dir that already has a foreign MCP server (simulating BELLATRIX ran first)
mkdir -p /tmp/atp-merge-check/.vscode
cat > /tmp/atp-merge-check/.vscode/mcp.json <<'JSON'
{
  "servers": {
    "bellatrix-desktop-mcp": { "command": "bellatrix-desktop-mcp", "args": ["--mcp"] }
  },
  "inputs": []
}
JSON

# 2. Run init there
dotnet run --project src/Spectra.CLI -- init --force   # (cd into the dir or pass working dir)

# 3. Confirm BOTH servers are present
cat /tmp/atp-merge-check/.vscode/mcp.json
#   → servers.bellatrix-desktop-mcp  (preserved)
#   → servers.spectra                (added)
#   → inputs                         (preserved)
```

**Before the fix**: step 3 would show only `bellatrix-desktop-mcp` — `spectra` was silently never added.

## Idempotence / comment preservation

```bash
# Re-run init: file is already conformant → no rewrite, comments (if any) preserved
dotnet run --project src/Spectra.CLI -- init --force
git diff --stat   # .vscode/mcp.json: no change
```

## Fail-loud on malformed config

```bash
echo '{ this is not json' > /tmp/atp-merge-check/.vscode/mcp.json
dotnet run --project src/Spectra.CLI -- init --force
# → non-zero exit, actionable error naming .vscode/mcp.json; file left untouched (not overwritten)
```

## Automated verification

```bash
dotnet test tests/Spectra.CLI.Tests \
  --filter "FullyQualifiedName~VsCodeMcpConfigInstaller|FullyQualifiedName~InitVsCodeMcpMerge"
```

Expected: all green. Covers fresh-create, foreign-server preservation, idempotence, `inputs`
preservation, JSONC tolerance, malformed fail-loud, and the `init` integration (SC-003).

## Maps to success criteria

- **SC-003** (foreign server keys retained 100%, both orders) — integration test + manual check above.
- **SC-005** (re-run changes nothing outside own namespace) — idempotence no-op check.
- **FR-013/014/015/018** — unit tests for merge, no-skip-if-exists, fail-loud.
