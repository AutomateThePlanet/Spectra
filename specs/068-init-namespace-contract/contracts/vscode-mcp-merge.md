# Contract: `VsCodeMcpConfigInstaller` (merge-by-key writer for `.vscode/mcp.json`)

Mirrors `ClaudeSettingsInstaller`. Two members; both pure-ish (the first is a pure function).

## `static string EnsureSpectraServer(string? existingJson)`

Pure function. Returns the JSON text that `.vscode/mcp.json` should contain so that
`servers.spectra = { "command": "spectra-mcp", "args": ["."] }`, merged into `existingJson`.

| Case | Input | Output / behavior |
|---|---|---|
| Fresh | `null` / whitespace | `{ "servers": { "spectra": { "command": "spectra-mcp", "args": ["."] } } }` |
| No `servers` object | `{ "inputs": [] }` | adds `servers.spectra`; preserves `inputs` |
| Foreign servers present | `{ "servers": { "bellatrix-web-mcp": {…} } }` | adds `servers.spectra`; **preserves** `bellatrix-web-mcp` |
| Already present & equal | contains `servers.spectra` == desired | returns equivalent JSON (caller detects no-op) |
| Already present & different | `servers.spectra` has other value | overwrites `servers.spectra` only; foreign keys preserved |
| JSONC with comments/trailing commas | parseable with tolerant options | parsed; re-emitted as clean JSON (comments dropped) |
| Unparseable | malformed JSON | throws `InvalidMcpConfigException` (or documented typed error) — caller fails loud |

**Invariants**: never removes/modifies a foreign `servers.*` key; never removes top-level keys
(`inputs`, etc.); output is deterministic for a given input; idempotent
(`EnsureSpectraServer(EnsureSpectraServer(x))` ≡ `EnsureSpectraServer(x)`).

## `static Task<string> EnsureInstalledAsync(string workingDirectory, CancellationToken ct)`

Read-modify-write. Returns the file path (`<wd>/.vscode/mcp.json`).

- Creates `.vscode/` if missing.
- Reads existing file if present.
- Computes the merged JSON via `EnsureSpectraServer`.
- **No-op optimization**: if the file already exists and `servers.spectra` already deep-equals the
  desired value, does NOT rewrite the file (preserves user comments; true idempotence).
- Otherwise writes the merged JSON.
- On unparseable existing file: propagates the typed exception (caller surfaces an actionable error,
  non-zero exit; file left untouched).

## Caller change — `InitHandler.CreateVsCodeMcpConfigAsync`

Replace the **skip-if-exists** block (current lines ~694-699) and the hardcoded write with a call to
`VsCodeMcpConfigInstaller.EnsureInstalledAsync(_workingDirectory, ct)`. Keep the existing debug logging
and the init-summary line. On the typed parse exception, log an actionable error and surface it as an
init failure (do not swallow, do not overwrite).

## Test contract

Unit (`VsCodeMcpConfigInstallerTests`, mirrors `McpAllowlistTests`):
1. `EnsureSpectraServer(null)` → contains `servers.spectra` with `spectra-mcp`.
2. Foreign server preserved + `spectra` added.
3. Idempotent (apply twice → single `spectra` key, equal output).
4. Preserves top-level `inputs`.
5. JSONC input with `//` comments parses and merges (no throw).
6. Malformed JSON → throws the typed exception.
7. `EnsureInstalledAsync` writes file to `.vscode/mcp.json`; no-op (no rewrite) when already equal.

Integration (`InitVsCodeMcpMergeTests`):
8. `init` into a dir whose `.vscode/mcp.json` already has a foreign server → after init, foreign server
   present AND `spectra` present (SC-003).
