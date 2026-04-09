# Research: SPECTRA Update SKILL + Documentation Sync

**Date**: 2026-04-10 | **Branch**: `029-update-skill-docs-sync`

## R1: SKILL Embedded Resource Pattern

**Decision**: Create `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` as an embedded resource.

**Rationale**: `SkillResourceLoader` auto-discovers all `.md` files under the `Spectra.CLI.Skills.Content.Skills.` namespace. Adding a new `.md` file to this directory automatically makes it available in `SkillContent.All`. `InitHandler.CreateBundledSkillFilesAsync()` loops through `SkillContent.All` — no code change needed there.

**Alternatives considered**:
- String constant in `SkillContent.cs`: Rejected — all 9 existing SKILLs use embedded resources, not inline strings.
- Separate file outside embedded resources: Rejected — would break the auto-discovery mechanism.

## R2: Tools List in SKILL Frontmatter

**Decision**: Use `tools: [{{READONLY_TOOLS}}]` template variable, matching all 9 existing SKILLs.

**Rationale**: The spec's original SKILL content hardcodes the tools list. However, all existing SKILLs use the `{{READONLY_TOOLS}}` placeholder which is replaced at write time. Following the established pattern ensures consistency.

**Alternatives considered**:
- Hardcoded tools list: Rejected — inconsistent with all existing SKILLs; would break if tools change.

## R3: UpdateResult Missing Fields

**Decision**: Add `TotalTests`, `TestsFlagged`, `FlaggedTests`, `Duration`, and `Success` fields to `UpdateResult`.

**Rationale**: The SKILL needs to present total tests analyzed, flagged test details, and duration. Current `UpdateResult` only has `TestsUpdated`, `TestsRemoved`, `TestsUnchanged`, `Classification`, `FilesModified`, `FilesDeleted`. The `CommandResult` base class provides `Command`, `Status`, `Timestamp`, `Message` but no `Success` or `Duration`.

**Fields to add**:
- `totalTests` (int): Sum of all classification counts (convenience field for SKILL presentation)
- `testsFlagged` (int): Count of orphaned + redundant tests requiring review
- `flaggedTests` (list): Detailed entries with id, title, classification, reason
- `duration` (string): Formatted duration "HH:mm:ss"
- `success` (bool): Whether the command completed without errors

**Alternatives considered**:
- Compute totalTests in SKILL: Rejected — SKILLs should read data directly, not compute. Other result models include totals.
- Add duration/success to CommandResult base: Rejected — scope creep; other commands may not need them. Can be added later if pattern emerges.

## R4: Agent Delegation Strategy

**Decision**: In `spectra-generation.agent.md`, move the existing "Update tests" section (lines 56-67) into the delegation table and replace with a SKILL reference. In `spectra-execution.agent.md`, add a new delegation row.

**Rationale**: The generation agent currently has inline update instructions (Step 1-4 with CLI commands). Per spec 027's delegation model, this should be a delegation entry pointing to the SKILL. The execution agent has no update entry at all — it needs one added.

**Alternatives considered**:
- Keep inline update in generation agent: Rejected — duplicates SKILL content, violates spec 027 dedup principle.
- Only update execution agent: Rejected — generation agent should also delegate to maintain consistency.

## R5: Documentation SKILL Count

**Decision**: Update PROJECT-KNOWLEDGE.md (SKILL count + table), CLAUDE.md (recent changes), README.md (SKILL count), CHANGELOG.md (new entry).

**Rationale**: Multiple documentation files reference SKILL counts. All must be consistent at "10 Bundled SKILLs."

**Discovery**: Need to search for exact locations of "9 Bundled SKILLs", "9 SKILL", or similar count references in all docs.

## R6: Test Strategy

**Decision**: Extend existing `SkillsManifestTests.cs` with SKILL count update and spectra-update specific assertions. Add agent delegation tests.

**Rationale**: Spec 027 established test patterns for SKILL consistency (flags, format, wait instruction, tools list). The existing tests check "all 9 SKILLs" — these need updating to "10." New tests specific to spectra-update follow the same assertion patterns.

**Test categories**:
1. SKILL count assertions (9 → 10)
2. spectra-update content assertions (flags, format, wait, tools)
3. Agent delegation assertions (both agents have update entry, no raw CLI blocks)
4. UpdateResult field assertions (new fields present in JSON output)
