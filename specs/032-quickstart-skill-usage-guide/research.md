# Phase 0 — Research: Quickstart SKILL & Usage Guide

This is a content + wiring feature that reuses well-established patterns. The "research" consisted of reading the current bundled-skills and bundled-docs delivery mechanism to confirm the new artifacts can be plugged in without new infrastructure.

## Decisions

### D1: SKILL is auto-discovered from `Skills/Content/Skills/`

**Decision**: Drop `spectra-quickstart.md` into `src/Spectra.CLI/Skills/Content/Skills/` and the existing `SkillResourceLoader` picks it up automatically.

**Rationale**: `SkillResourceLoader.LoadAllSkills()` enumerates all embedded resources whose name starts with `Spectra.CLI.Skills.Content.Skills.` and registers them by filename (sans `.md`). Adding a new `.md` file under that directory is sufficient — no edit to the loader, no edit to the .csproj (the directory is already covered by an existing `<EmbeddedResource Include="Skills\Content\Skills\*.md">` glob, confirmed by the fact that all 11 current SKILLs work without per-file entries).

**Alternatives considered**:
- Build a separate "tutorial SKILL" loader with extra metadata. **Rejected** — premature complexity, no benefit. Auto-discovery already works.

### D2: USAGE.md follows the CUSTOMIZATION.md delivery pattern

**Decision**: Add `USAGE.md` as an embedded resource at `src/Spectra.CLI/Skills/Content/Docs/USAGE.md`, expose a `LoadEmbeddedUsageGuide()` method on `ProfileFormatLoader` (or a new dedicated loader if cleaner), and have `InitHandler` write it to the project root with hash tracking, mirroring the existing `CUSTOMIZATION.md` block.

**Rationale**: `CUSTOMIZATION.md` is already delivered exactly this way (resource id `Spectra.CLI.Skills.Content.Docs.CUSTOMIZATION.md`, loaded via `ProfileFormatLoader.LoadEmbeddedCustomizationGuide()`, written by `InitHandler` with hash tracking against `manifest.Files["CUSTOMIZATION.md"]`). Following the identical pattern keeps `update-skills` working with no changes to its core dispatch logic.

**Alternatives considered**:
- Put `USAGE.md` under `docs/` instead of project root. **Rejected** — `docs/` is the user's documentation source for test generation; mixing tooling docs into it pollutes that namespace. Project root sits next to `README.md` and `CUSTOMIZATION.md`, where adopters look first.
- Build a generic "doc bundle" abstraction. **Rejected** — only two files exist (CUSTOMIZATION.md, USAGE.md). YAGNI.

### D3: SkillContent.cs gets a `Quickstart` accessor for symmetry

**Decision**: Add `public static string Quickstart => All["spectra-quickstart"];` to `SkillContent.cs` even though `All` already exposes the value.

**Rationale**: All existing SKILLs have a typed accessor on `SkillContent`. Tests reference these accessors. Following the convention keeps the file consistent and makes the new SKILL discoverable via IntelliSense.

### D4: SkillsManifest hash entries use the same key format

**Decision**: Use `".github/skills/spectra-quickstart/SKILL.md"` as the manifest key for the SKILL (matching how other SKILLs are keyed) and `"USAGE.md"` for the doc (matching `CUSTOMIZATION.md`).

**Rationale**: Confirmed by reading `InitHandler.cs` line 622 (`const string customizationRelative = "CUSTOMIZATION.md";`) and the existing per-skill manifest entries. Using the same convention keeps `update-skills` symmetric.

### D5: Agent prompt edits are additive single lines

**Decision**: Add one delegation line to each of `spectra-generation.agent.md` and `spectra-execution.agent.md` pointing onboarding intents at the quickstart SKILL. Do not refactor either agent file.

**Rationale**: Spec 027 (skill-agent-dedup) recently shrank both agents to delegation tables. Adding one row to those tables is consistent and minimal. A larger refactor would dilute the focused scope of this feature.

## Open questions

None. All NEEDS CLARIFICATION markers from the spec template are resolved by the input description and the existing codebase patterns.
