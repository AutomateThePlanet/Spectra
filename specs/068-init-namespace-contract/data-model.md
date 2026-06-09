# Data Model: ATP Shared-Namespace `init` Contract — SPECTRA conformance

This feature is config-merge logic, not a new persistent schema. The "entities" are the JSON shapes
the merge reads and writes plus the conceptual contract entities from the spec.

## `.vscode/mcp.json` (shared config file)

```jsonc
{
  "servers": {
    "<server-key>": { "command": "<cmd>", "args": [ ... ], "env": { ... } },
    ...
  },
  "inputs": [ ... ]            // optional; preserved verbatim if present
}
```

- **servers** (object, required-after-merge): map of MCP server key → server definition. SPECTRA owns
  exactly the `spectra` key; all other keys are **foreign** and MUST be preserved.
- **spectra** (SPECTRA-owned entry): `{ "command": "spectra-mcp", "args": ["."] }`.
- **Foreign keys** (e.g. `bellatrix-web-mcp`, `bellatrix-desktop-mcp`, `testimize`): never read,
  never modified, never removed.
- **inputs / any other top-level keys**: preserved as-is.

### Validation / invariants

- If the file is absent → create with `{ "servers": { "spectra": {…} } }`.
- If present and parseable → ensure `servers` exists; set/insert only `servers.spectra`.
- If `servers.spectra` already deep-equals the desired value → **no write** (idempotent; preserves comments).
- If present but unparseable (after tolerant JSONC parse) → **fail loud**, do not write.

### State transitions (writer)

```
absent ──init──▶ { servers: { spectra } }
present(no spectra) ──init──▶ present(+ spectra)            [foreign keys preserved]
present(spectra == desired) ──init──▶ present (unchanged)   [no write]
present(spectra != desired) ──init──▶ present(spectra updated) [foreign keys preserved; comments lost]
present(unparseable) ──init──▶ ERROR (file untouched)
```

## Contract entities (from spec, for reference)

- **Tool namespace** — set of artifacts owned by one prefix (`spectra`). For this branch the only
  shared-config touch point is `servers.spectra`.
- **Shared config file** — `.vscode/mcp.json` (this fix), `.claude/settings.json` (already merged),
  `.mcp.json` (SPECTRA does not write it — no change).
- **Authoring manifest** — `.spectra/skills-manifest.json`; explicitly does NOT include shared config
  (see research R4). Unchanged by this feature.

## Non-entities (explicitly unchanged)

- Skills layout (`.claude/skills/spectra-*/SKILL.md`), subagent `spectra-critic`, prompt templates,
  profiles, `.gitignore` patterns — all already contract-compliant; no schema change.
