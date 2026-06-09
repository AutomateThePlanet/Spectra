# Research: ATP Shared-Namespace `init` Contract — SPECTRA conformance

## R1 — Reuse pattern for merge-by-key shared config

**Decision**: Mirror `ClaudeSettingsInstaller` (`src/Spectra.CLI/Skills/ClaudeSettingsInstaller.cs`)
exactly: a pure `static string Ensure…(string? existingJson)` that parses to a `JsonObject`, ensures
the nested object/array exists, adds the tool's own key only if absent, and returns indented JSON;
plus a `static Task<string> EnsureInstalledAsync(workingDirectory, ct)` that does the
read-modify-write and returns the path.

**Rationale**: Proven, tested (`McpAllowlistTests.cs`), constitution-aligned (no new abstraction —
the third merge-by-key use case is what would justify a shared helper, and we are only at the second;
YAGNI says copy the shape, do not generalize yet). Same `System.Text.Json` node API, no new dependency.

**Alternatives considered**:
- Generalize a single `JsonKeyMerger<T>` now — rejected (YAGNI; only two call sites).
- Use a JSONC-preserving library to keep user comments — rejected (new dependency; comment
  round-tripping is out of proportion to the value; see R3).

## R2 — Merge target and key

**Decision**: Merge into `servers` object, key `spectra`, value `{ "command": "spectra-mcp", "args": ["."] }`
(the exact current first-run payload). Preserve every other `servers.*` key untouched.

**Rationale**: Matches FR-013 (key = MCP server name `spectra`) and the observed shared-repo file
where `bellatrix-web-mcp`, `bellatrix-desktop-mcp`, `spectra`, `testimize` coexist. Also handle a
top-level shape with no `servers` object (create it) and an existing `inputs` array (preserve it).

**Alternatives considered**: Replace whole `servers` object — rejected (FR-014 forbids whole-file/whole-section replace; that is the silent-loss bug).

## R3 — JSONC tolerance (comments / trailing commas)

**Decision**: Parse with tolerant options
(`JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }` via
`JsonNode.Parse(json, nodeOptions, documentOptions)`). On a genuine parse failure (`JsonException`),
**fail loud** with an actionable message naming the file (FR-015) — do NOT overwrite. When writing
back, emit clean indented JSON (comments are NOT preserved). To minimize comment loss in the common
case, **skip the write entirely when `servers.spectra` is already present and equal** to the desired
value (the file is already conformant) — this makes re-runs touch nothing and keeps user comments.

**Rationale**: `.vscode/mcp.json` is JSONC in practice (the shared repo's file has `//` comments). A
strict parser would fail-loud on every real VS Code config — wrong. System.Text.Json cannot
round-trip comments through a node tree, so the honest contract is "keys/values preserved, comments
not" — acceptable per FR-013 (preserve foreign *keys*, not formatting). The no-op-when-present
optimization preserves comments whenever no change is needed, which is the dominant re-run case.

**Alternatives considered**:
- Strict parse + fail-loud on comments — rejected (breaks real VS Code files).
- Preserve comments via regex/AST surgery — rejected (fragile, over-engineered).

**Known limitation (documented)**: If the `spectra` server entry must be *added or changed* in a file
that contains comments, the rewritten file loses comments. Surfaced in `quickstart.md` and the init log.

## R4 — Manifest scope (R6 vs R7 boundary)

**Decision**: `.vscode/mcp.json` is NOT recorded in `.spectra/skills-manifest.json` and is NOT
hash-tracked. It remains merge-managed only.

**Rationale**: The contract separates *self-owned* files (R6: hash-tracked, the tool may rewrite/clean
them) from *shared config* (R7: merge-by-key, never owned). Recording a shared file in the manifest
would invite the tool to "reconcile" or overwrite it — exactly what R7 forbids. Current SPECTRA code
already excludes it from the manifest; we keep that.

**Alternatives considered**: Track our own `servers.spectra` sub-entry hash — rejected (adds state for
no behavioral gain; the merge is already idempotent by value comparison).

## R5 — `--force` and re-run interaction

**Decision**: The merge writer ignores `--force` (shared config is never force-reset; FR-017). Re-run
is idempotent by value (R3 no-op). This matches the existing intent that `.vscode/mcp.json` was never
force-clobbered even before this change.

**Rationale**: FR-017 — `--force` is namespace-local; shared config is foreign-inclusive, so force must
not touch other tools' keys. Merge-by-key already guarantees that regardless of `--force`.

## R6 — Behavior-preservation for already-compliant SPECTRA paths (FR-019)

**Decision**: No code change to skills (`.claude/skills/spectra-*`), the `spectra-critic` subagent,
the manifest-scoped hash-tracked `update-skills`, or `abort-without-force`. Add/confirm assertions so a
regression is caught, but do not refactor.

**Rationale**: SC-001/SC-002/SC-004/SC-005/SC-007 already hold for SPECTRA; the contract work is to keep
them holding while fixing only the one shared-config writer. Minimizes blast radius.
