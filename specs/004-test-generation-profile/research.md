# Research: Test Generation Profile

**Feature**: 004-test-generation-profile
**Date**: 2026-03-15

## Research Decisions

### RD-001: Profile File Format

**Question**: What format should the profile file use for storing preferences?

**Options Considered**:
1. **JSON** - Machine-readable, strict schema validation
2. **YAML** - Human-readable, common in config files
3. **Markdown with YAML frontmatter** - Human-readable, consistent with test case format
4. **TOML** - Simple, explicit, popular in modern tooling

**Decision**: Markdown with YAML frontmatter (spectra.profile.md)

**Rationale**:
- Consistent with the test case format already used by SPECTRA
- Human-readable and editable without special tooling
- YAML frontmatter provides structured data for machine parsing
- Markdown body allows for human-readable documentation of choices
- Existing Markdig and YamlDotNet dependencies handle parsing
- Can be version-controlled with meaningful diffs

---

### RD-002: Profile Option Categories

**Question**: What categories of preferences should the profile support?

**Options Considered**:
1. **Minimal** - Just detail level and exclusions
2. **Comprehensive** - All formatting, domain, and generation preferences
3. **Extensible** - Core options with custom key-value extension mechanism

**Decision**: Comprehensive with defined schema

**Rationale**:
- Spec defines specific option categories: detail level, formatting, domain needs, exclusions
- Extensible custom options add complexity without clear use case
- Comprehensive predefined options cover identified team needs
- Schema validation ensures consistency across repositories

---

### RD-003: Suite-Level Override Mechanism

**Question**: How should suite-level overrides merge with repository profile?

**Options Considered**:
1. **Full replacement** - Suite profile completely replaces repo profile
2. **Shallow merge** - Suite options override repo options at top level
3. **Deep merge** - Suite options merge deeply into repo options
4. **Explicit override** - Suite profile declares which options to override

**Decision**: Shallow merge at option category level

**Rationale**:
- Simple mental model: suite values replace repo values for specified options
- Unspecified suite options inherit from repository profile
- Deep merge creates complexity around array merging (e.g., exclusions)
- Explicit override requires additional syntax without clear benefit
- Shallow merge matches common configuration cascade patterns

---

### RD-004: Interactive Questionnaire Implementation

**Question**: How should the interactive questionnaire be implemented?

**Options Considered**:
1. **System.CommandLine prompts** - Built-in, simple
2. **Spectre.Console** - Rich terminal UI, progress bars, selections
3. **Custom readline loop** - Full control, minimal dependencies

**Decision**: System.CommandLine with simple prompts

**Rationale**:
- Already a project dependency; no new packages needed
- Simple question-answer flow sufficient for profile creation
- Spectre.Console adds unnecessary dependency complexity
- Custom implementation adds maintenance burden
- Performance goal (5 minutes) easily met with simple prompts

---

### RD-005: Profile Loading Strategy

**Question**: When and how should profiles be loaded during generation?

**Options Considered**:
1. **Eager loading** - Load at CLI startup, cache for session
2. **Lazy loading** - Load only when generation command runs
3. **On-demand loading** - Load fresh for each generation request

**Decision**: Lazy loading at generation command start

**Rationale**:
- No performance benefit to eager loading (profile loading is fast)
- Loading once per command ensures consistent behavior during generation
- On-demand loading per request adds unnecessary file I/O
- Lazy loading allows profile edits between commands in same session
- Spec states: "file changes don't affect in-progress generation"

---

### RD-006: Profile Validation Approach

**Question**: How strict should profile validation be?

**Options Considered**:
1. **Strict** - Reject any unknown or invalid options
2. **Lenient** - Warn on unknown options, use defaults for invalid
3. **Hybrid** - Strict for format errors, lenient for value errors

**Decision**: Lenient with warnings

**Rationale**:
- Spec states: "warns about the invalid setting and uses the default"
- Enables forward compatibility as new options are added
- Prevents profile breakage when updating SPECTRA version
- Warnings provide feedback without blocking generation
- Critical format errors (malformed YAML) still fail validation

---

### RD-007: Non-Interactive Mode Support

**Question**: How should profile creation work in CI/automation environments?

**Options Considered**:
1. **Interactive only** - Require manual creation
2. **Command-line flags** - All options as CLI parameters
3. **Template file** - Copy and edit a template
4. **Both flags and interactive** - Auto-detect or explicit flag

**Decision**: Command-line flags with `--non-interactive` mode

**Rationale**:
- Spec assumption: "can be completed non-interactively via command-line flags"
- CI environments cannot respond to prompts
- Flags allow scripted profile generation
- Auto-detect TTY to choose mode; explicit flag overrides
- Missing required options in non-interactive mode fail with clear error

---

### RD-008: Profile Version Migration

**Question**: How should profile format versions be handled?

**Options Considered**:
1. **No versioning** - Always backward compatible
2. **Version field** - Profile contains version, migrate on load
3. **Format detection** - Infer version from structure
4. **External migration tool** - Separate command for upgrades

**Decision**: Version field with automatic migration prompt

**Rationale**:
- Spec states: "detects outdated formats and prompts the user to update"
- Version field (e.g., `profile_version: 1`) enables explicit compatibility checks
- Automatic migration preserves user preferences during upgrades
- Migration prompt gives user control; no silent changes
- Future-proofs as profile format evolves

## Dependencies

| Dependency | Purpose | Status |
|------------|---------|--------|
| YamlDotNet | YAML frontmatter parsing | Existing |
| Markdig | Markdown body parsing | Existing |
| System.CommandLine | Interactive prompts, CLI | Existing |

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Profile format changes break existing profiles | Medium | High | Version field + migration support |
| Users bypass profile with direct AI prompts | Low | Low | Profile applies automatically; hard to bypass |
| Complex suite override scenarios | Low | Medium | Clear merge rules documented |
| Large profile slows generation | Very Low | Low | Profile is small text; negligible overhead |
