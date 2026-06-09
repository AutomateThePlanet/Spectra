# Feature Specification: ATP Shared-Namespace `init` Contract (v2)

**Feature Branch**: `068-init-namespace-contract`
**Created**: 2026-06-09
**Status**: Draft
**Input**: User description: "ATP Shared-Namespace `init` Contract (v2) — a governance contract so every ATP CLI that scaffolds into a user repo (SPECTRA, BELLATRIX, and any future tool) can coexist in one repo with zero overwrites, zero silent loss, and each tool able to verify and update only what it authored."

## Overview

Multiple ATP command-line tools scaffold AI-assistant configuration into the same user
repository (skills, subagents, instruction fragments, and shared MCP/permission config).
Today their `init` commands have no shared rules: one tool can clobber another's files,
register a shared config key by whole-file replacement, or stage and install another tool's
exact file names. This feature defines a **binding contract** — a set of ownership and
write-discipline rules every ATP `init` MUST obey — and brings the two existing tools
(SPECTRA, BELLATRIX) into conformance with it.

The driving risk is concrete and confirmed: BELLATRIX currently stages a full copy of
SPECTRA's exact skill+subagent bundle for unconditional-overwrite install into the shared
`.claude/` namespace (same skill names, same subagent identities). The moment that path is
wired up it silently destroys SPECTRA's files and triggers silent subagent discard. The
contract eliminates this at the root by mandating that **each tool owns and installs only
its own namespace**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Two tools coexist in one repo without collision (Priority: P1)

A user has a repository and runs `spectra init` and `bellatrix init` (in either order). Both
tools install their skills, subagents, and shared-config entries. Nothing either tool wrote
is destroyed, replaced, or silently discarded by the other.

**Why this priority**: This is the core promise and the fix for the confirmed data-loss risk
(vendoring/clobber + silent subagent discard). If only this story ships, the repository is
safe to use with both tools — the MVP.

**Independent Test**: In a throwaway repo, run both tools' `init` in both orders. Confirm that
every skill directory, every subagent file, and every shared-config entry authored by each
tool is present and valid after the second tool runs, and that no subagent identity is shared
across the two tools.

**Acceptance Scenarios**:

1. **Given** an empty repo, **When** `spectra init` then `bellatrix init` run, **Then** both
   tools' prefixed skill directories and uniquely-named subagents exist, and the shared MCP
   config contains both tools' server entries.
2. **Given** a repo where `spectra init` already ran, **When** `bellatrix init` runs, **Then**
   no `spectra-*` skill, subagent, or config entry is modified or removed.
3. **Given** a repo where one tool already wrote a shared config file (e.g. `.vscode/mcp.json`),
   **When** the other tool's `init` runs, **Then** the second tool's server key is added and the
   first tool's key is preserved (no skip-if-exists, no whole-file replacement).
4. **Given** both tools have run, **When** the assistant loads subagents, **Then** no two
   subagents share a `name`, so none is silently discarded.
5. **Given** a tool's own bundle, **When** `init` runs, **Then** the tool installs only files in
   its own prefixed namespace and does not write any file named with another tool's prefix.

---

### User Story 2 - Self-scoped safe-update preserves user edits and foreign files (Priority: P2)

A user has customized some generated files and has files authored by another tool. A tool's
update/refresh path runs. It refreshes only the files it originally authored, skips any file
the user has edited since install, and never touches files it did not author.

**Why this priority**: Without this, "update" becomes a second collision vector — re-running a
refresh could re-clobber a sibling tool or overwrite a user's hand edits. It protects the
coexistence guarantee over time, after the initial install.

**Independent Test**: Author files with two tools, hand-edit one tracked file, then run one
tool's update. Confirm the edited file is left untouched, the foreign tool's files are
untouched, and only the un-edited self-authored files are refreshed.

**Acceptance Scenarios**:

1. **Given** a tool installed files and recorded them in its own manifest, **When** its update
   runs, **Then** only paths in that manifest are considered.
2. **Given** a self-authored file whose on-disk content differs from the recorded hash (a user
   edit), **When** update runs, **Then** that file is skipped and preserved.
3. **Given** files authored by another tool, **When** a tool's update or cleanup runs, **Then**
   those files are never enumerated, hashed, deleted, or reconciled.
4. **Given** a tool that has no record of a file, **When** update runs, **Then** the tool does
   not walk the filesystem to discover or act on it.

---

### User Story 3 - Human-owned CLAUDE.md composed via imports (Priority: P3)

A user wants each tool's standing instructions available in their assistant session without any
tool rewriting their root `CLAUDE.md`. Each tool keeps its instructions in a fragment file it
owns, and the root `CLAUDE.md` references those fragments by import.

**Why this priority**: Protects the most human-curated file in the repo and makes instruction
composition additive and reversible. Lower priority because no tool overwrites `CLAUDE.md`
today — this is adopted proactively to keep future automation safe.

**Independent Test**: Run a tool's `init` in a repo with an existing `CLAUDE.md` and in a repo
with none. Confirm the existing file's body is never modified (the import line is surfaced for
the human to paste), and that when absent a minimal `CLAUDE.md` containing only the import line
may be created.

**Acceptance Scenarios**:

1. **Given** an existing root `CLAUDE.md`, **When** a tool's `init` runs, **Then** the file body
   is unchanged and the tool's init summary prints the exact import line to paste.
2. **Given** no root `CLAUDE.md`, **When** a tool's `init` runs, **Then** the tool may create a
   minimal `CLAUDE.md` whose only content is its import line.
3. **Given** a tool's `init` runs, **When** it writes its instruction fragment, **Then** the
   fragment lives in the tool's own dotfolder (not under the document corpus directory) and is
   not named `CLAUDE.md`.
4. **Given** a tool re-runs `init`, **When** it refreshes its own fragment, **Then** only the
   tool's own fragment file is rewritten.

---

### User Story 4 - Idempotent re-run with namespace-local `--force` (Priority: P3)

A user re-runs a tool's `init`, sometimes with `--force`. Re-running changes nothing beyond
refreshing the tool's own namespace, and `--force` only ever resets that tool's own files.

**Why this priority**: Makes the tools predictable and safe to re-run in any repo state, and
prevents `--force` from becoming a cross-tool wrecking ball. Builds on the coexistence and
self-scope guarantees above.

**Independent Test**: Run a tool's `init` twice (plain, then `--force`) in a repo containing a
sibling tool's files and a user-edited shared config. Confirm idempotence and that the sibling
tool's namespace and the root `CLAUDE.md` body are untouched in both runs.

**Acceptance Scenarios**:

1. **Given** an already-initialized repo, **When** plain `init` re-runs, **Then** only the
   tool's own fragment, skills, subagents, and own config entries are refreshed; everything
   else is byte-unchanged.
2. **Given** `--force`, **When** `init` runs, **Then** it may reset the tool's own namespace but
   never a foreign namespace and never the root `CLAUDE.md` body.
3. **Given** a sibling tool's files and a user-edited shared config, **When** `--force` runs,
   **Then** the sibling's files and the foreign config keys are preserved.

---

### Edge Cases

- **Shared config exists but is malformed** (invalid JSON, comments/trailing commas): the tool
  MUST fail loud with an actionable message rather than overwrite or silently drop foreign keys.
- **Root `CLAUDE.md` already contains the tool's import line**: re-run must not duplicate it (no
  second import line, no edit if surfacing-only).
- **A tool's own bundle would emit a duplicate subagent `name`** (within its own set): treated
  as an authoring error and rejected before install — never shipped as a silent-discard hazard.
- **A self-authored file is missing on disk** at update time (user deleted it): the tool may
  recreate it (it owns the path) but MUST NOT touch anything outside its manifest.
- **Another tool wrote the shared config first** with formatting the tool didn't produce: merge
  by key while preserving the existing file's foreign entries; do not normalize/replace the
  whole document.
- **A skill directory exists with the tool's prefix but is not in the manifest** (user-created
  or stale): the tool MUST NOT delete it during update; it acts only on manifest-recorded paths.

## Requirements *(mandatory)*

### Functional Requirements

**Ownership model**

- **FR-001 (Model 1)**: Each tool MUST install only files within its own namespace. A tool MUST
  NOT vendor, stage for install, or write any file belonging to another tool's namespace
  (another tool's prefix or identity).
- **FR-002**: A tool's namespace MUST be identified by a single canonical tool prefix
  (e.g. `spectra`, `bellatrix`) applied uniformly to its skills, subagents, fragment file,
  dotfolder, and shared-config keys.

**Root CLAUDE.md (human-owned)**

- **FR-003 (R1)**: A tool MUST NOT overwrite or rewrite the body of an existing root `CLAUDE.md`.
- **FR-004 (R3)**: If no root `CLAUDE.md` exists, a tool MAY create a minimal one containing only
  its import line. If one exists, the tool MUST surface the exact import line in its init summary
  for the human to add, and MUST NOT edit the file.
- **FR-005 (R2)**: A tool MUST write all of its persistent instructions into exactly one fragment
  file it exclusively owns, located in the tool's own dotfolder, NOT under the document-corpus
  directory, and NOT named `CLAUDE.md`. The tool MAY overwrite this fragment freely on each run.

**Skills**

- **FR-006 (R4)**: A tool MUST emit skills in the current skill format
  (`.claude/skills/<prefix>-<name>/SKILL.md`), with every skill directory carrying the tool
  prefix. Legacy command-file layout MUST NOT be used for new emission.
- **FR-007**: A tool MUST write or update only skill directories matching its own prefix.

**Subagents**

- **FR-008 (R5)**: A tool MUST emit subagents to the shared subagents directory with a `name`
  that is prefixed and globally unique across all tools. Subagent identity derives from the
  `name`, not the file path.
- **FR-009**: A tool MUST treat any duplicate subagent `name` (within its own emitted set) as a
  rejectable authoring error, because the host silently discards duplicate-named subagents.

**Self-scoped safe-update**

- **FR-010 (R6)**: A tool's update, refresh, cleanup, or hash-tracking MUST enumerate and mutate
  only paths recorded in its own manifest (a record of paths the tool authored, with a content
  hash per path).
- **FR-011**: A tool MUST NOT walk the filesystem to discover files to act on, and MUST NOT
  delete, reconcile, or hash-check any file it did not author.
- **FR-012**: A tool MUST skip (preserve) any self-authored file whose on-disk content hash
  diverges from the recorded hash (interpreted as a user edit).

**Shared config (merge by key)**

- **FR-013 (R7)**: A tool MUST treat shared config files (e.g. `.mcp.json`, `.vscode/mcp.json`,
  `.claude/settings.json`) as read-modify-write, keyed by entry name. It MUST add or update only
  its own entries and MUST preserve all foreign keys.
- **FR-014**: A tool MUST NOT replace a shared config file wholesale and MUST NOT skip writing
  when the file already exists (skip-if-exists is silent loss when another tool wrote first).
- **FR-015**: If a shared config file exists but cannot be parsed, the tool MUST fail loud with
  an actionable message rather than overwrite it or drop entries.

**Re-run and force**

- **FR-016 (R8)**: Re-running `init` MUST be idempotent: it changes nothing beyond refreshing the
  tool's own fragment, skills, subagents, and own shared-config entries.
- **FR-017**: `--force` MUST be namespace-local: it MAY reset the tool's own namespace but MUST
  NOT reset a foreign namespace and MUST NOT alter the root `CLAUDE.md` body.

**Per-tool conformance — SPECTRA**

- **FR-018**: SPECTRA `init` MUST register its MCP server entry in `.vscode/mcp.json` by
  merge-by-key (server `spectra`) instead of the current skip-if-exists behavior, satisfying
  FR-013/FR-014. (SPECTRA's `.claude/settings.json` already merges and remains as-is.)
- **FR-019**: SPECTRA's existing conformant behaviors MUST be preserved: prefixed `spectra-*`
  skills (FR-006/FR-007), the single uniquely-named `spectra-critic` subagent (FR-008),
  manifest-scoped hash-tracked update (FR-010–FR-012), and abort-without-`--force` (FR-016).

**Per-tool conformance — BELLATRIX**

- **FR-020**: BELLATRIX MUST NOT install SPECTRA's bundle. The staged copy of SPECTRA's
  skill+subagent set MUST be removed from BELLATRIX so it can never be emitted (satisfies
  FR-001).
- **FR-021**: BELLATRIX `init` MUST emit only `bellatrix-*` skills into the contract skills path
  (FR-006) and only `bellatrix-*`, uniquely-named subagents into the shared subagents directory
  (FR-008), instead of the current flat per-target file layout.
- **FR-022**: BELLATRIX `init` MUST record an authoring manifest with per-path content hashes and
  replace unconditional overwrites with author-scoped, hash-aware writes (FR-010–FR-012).
- **FR-023**: BELLATRIX `init` MUST register its MCP servers (`bellatrix-web-mcp`,
  `bellatrix-desktop-mcp`) into the shared MCP config and permission config by merge-by-key
  (FR-013).
- **FR-024**: BELLATRIX `init` MUST be idempotent on re-run and MUST honor its force/regenerate
  flag as a namespace-local reset (FR-016/FR-017).
- **FR-025**: BELLATRIX `init` MUST write its own instruction fragment and surface its import
  line per FR-003–FR-005.

### Key Entities *(include if feature involves data)*

- **Tool namespace**: The complete set of artifacts a single tool owns, identified by one
  canonical prefix — its dotfolder, instruction fragment, prefixed skills, prefixed subagent
  identities, and its keyed entries in shared config.
- **Instruction fragment**: A single tool-owned markdown file holding that tool's standing
  instructions, referenced from the root `CLAUDE.md` by import; freely rewritten by its owner.
- **Skill**: A prefixed unit installed as a directory containing a skill definition; invokable
  by name and usable autonomously.
- **Subagent**: A delegated assistant persona identified by a globally-unique, prefixed `name`;
  duplicate names are silently discarded by the host.
- **Shared config file**: A repo-level config (MCP server registry, assistant settings) owned by
  no single tool, mutated only by key-scoped merge.
- **Authoring manifest**: A tool-private record of the paths it installed and a content hash per
  path, the sole basis for that tool's update/cleanup decisions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After running both tools' `init` in either order in a fresh repo, 100% of each
  tool's authored skills, subagents, fragments, and shared-config entries are present and valid
  (zero overwrites, zero silent loss).
- **SC-002**: Across both tools' full emitted sets, there are zero duplicate subagent `name`
  values, so no subagent is silently discarded.
- **SC-003**: After both tools run, the shared MCP config retains 100% of foreign server keys
  (no key dropped by either tool), verified for both write orders.
- **SC-004**: BELLATRIX emits zero files named with another tool's prefix (no `spectra-*` or
  foreign-prefixed file installed), confirming the vendoring risk is eliminated.
- **SC-005**: Re-running either tool's `init` (plain) produces zero changes to any file outside
  that tool's own namespace, and zero changes to a user-edited self-authored file.
- **SC-006**: `--force` on either tool leaves 100% of the sibling tool's namespace and the root
  `CLAUDE.md` body byte-unchanged.
- **SC-007**: Each tool's update path acts only on manifest-recorded paths in 100% of runs and
  preserves every file whose on-disk hash diverges from the recorded hash.
- **SC-008**: In a repo with a pre-existing root `CLAUDE.md`, neither tool's `init` modifies that
  file's body in any run; the import line is surfaced instead.

## Assumptions

- **Single host assumed**: The contract targets Claude Code as the assistant host (skills,
  subagents, imports, settings semantics). Other assistants are out of scope for normative rules.
- **Open item 1 — tool prefix is canonical, contract is prefix-agnostic**: The contract requires
  each tool to use one unique canonical prefix but does not mandate which string BELLATRIX uses.
  Whether BELLATRIX ships under `bellatrix-` or a rebrand prefix (`bifrost-`) is a naming decision
  for the BELLATRIX team; the contract is satisfied as long as the chosen prefix is unique and
  applied uniformly. Resolved during BELLATRIX conformance, not by this contract.
- **Open item 2 — pre-existing hand-authored bridge artifacts are foreign files**: Hand-authored
  integration artifacts already in a repo (e.g. legacy bridge command files) are not owned by any
  tool's `init` and are therefore protected by FR-011 (tools must not touch unauthored files).
  Whether to migrate or retire them is a separate, human-driven decision outside this contract.
- **Open item 3 — Copilot mirror is out of scope**: Manual mirrors of `CLAUDE.md` to other
  assistants' instruction files are unaffected; no ATP `init` reads or writes them. Whether to
  retain such a mirror is a human decision, not governed here.
- **Manifest format is per-tool**: Each tool keeps its own manifest in its own dotfolder; the
  contract specifies the behavior (path + hash scoping), not a shared manifest schema.
- **Import approval prompt is expected**: First-time external imports trigger a one-time host
  approval prompt; this is normal and not an error condition the tool must suppress.

## Per-Tool Conformance Snapshot (current state, non-normative)

| Rule | SPECTRA today | BELLATRIX today |
|---|---|---|
| FR-001 own-namespace-only | Compliant | **Violates** — stages SPECTRA's bundle for install |
| FR-006/007 prefixed skills | Compliant (`spectra-*`) | Non-conformant — flat per-target files |
| FR-008/009 unique subagent name | Compliant (`spectra-critic`) | Not emitting subagents yet |
| FR-010–012 self-scoped update | Compliant (manifest + hash) | **Missing** — unconditional overwrite |
| FR-013–015 merge shared config | Partial — `settings.json` merges; `.vscode/mcp.json` skip-if-exists | **Missing** — does not touch shared config |
| FR-016/017 idempotent / local force | Compliant (aborts without force) | **Missing** — force flag declared but unchecked |
| FR-003–005 CLAUDE.md / fragment | N/A today (no fragment) | Missing |

This snapshot scopes the conformance work (FR-018–FR-025); it is informational and not part of
the binding contract.
