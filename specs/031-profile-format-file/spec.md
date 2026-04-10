# Feature Specification: Default Profile Visibility & Customization Guide

**Feature Branch**: `031-profile-format-file`
**Created**: 2026-04-10
**Status**: Draft
**Depends on**: 030-prompt-templates, 004-test-generation-profile

## Problem

The `test-generation.md` prompt template references a `{{profile_format}}` placeholder that is silently filled by a hardcoded JSON schema baked into `GenerationAgent.BuildFullPrompt`. Users have no visibility into what the AI is asked to produce, no starting point to customize the output schema, and no single document explaining all the customization surfaces SPECTRA exposes.

## User Scenarios & Testing

### User Story 1 - Discoverable default format (Priority: P1)

Test architects want to see — and edit — the JSON schema the AI uses for test generation, without grepping source code.

**Why this priority**: This is the foundational visibility gap that drives the rest of the feature.

**Independent Test**: After running `spectra init`, the file `profiles/_default.yaml` exists, contains a `format` field with the JSON schema, and includes inline documentation comments. Editing the `format` field changes what the AI receives on the next `spectra ai generate` run.

**Acceptance Scenarios**:

1. **Given** a fresh project, **When** the user runs `spectra init`, **Then** `profiles/_default.yaml` exists with a documented `format` field.
2. **Given** a project with `profiles/_default.yaml` modified by the user, **When** generation runs, **Then** the AI prompt's `{{profile_format}}` placeholder resolves to the contents of the user's `format` field.
3. **Given** `profiles/_default.yaml` is missing, **When** generation runs, **Then** the system uses the embedded built-in default and does not fail.

### User Story 2 - One-stop customization guide (Priority: P1)

Users want a single curated reference document explaining every place SPECTRA can be customized: profiles, prompt templates, behavior categories, config, branding, SKILLs, and agents.

**Why this priority**: Customization surfaces already exist; without one map, they are effectively undiscoverable.

**Independent Test**: After `spectra init`, `CUSTOMIZATION.md` exists at the project root and lists all customization surfaces with file paths and worked examples.

**Acceptance Scenarios**:

1. **Given** a fresh project, **When** the user runs `spectra init`, **Then** `CUSTOMIZATION.md` exists at the project root.
2. **Given** an existing project where the user has not edited `CUSTOMIZATION.md`, **When** they run `spectra update-skills`, **Then** the file is refreshed to the latest version.
3. **Given** an existing project where the user has edited `CUSTOMIZATION.md`, **When** they run `spectra update-skills`, **Then** the file is left untouched.

### User Story 3 - Safe upgrade path (Priority: P2)

Users want both new files tracked by the same hash-based update mechanism as SKILL files and prompt templates, so upgrades never silently overwrite their customizations.

**Acceptance Scenarios**:

1. **Given** `profiles/_default.yaml` matches the current built-in hash, **When** `spectra update-skills` runs, **Then** the file is updated to the latest built-in.
2. **Given** the user has modified `profiles/_default.yaml`, **When** `spectra update-skills` runs, **Then** the file is preserved and reported as user-modified.

### Edge Cases

- `profiles/_default.yaml` exists but has malformed YAML → fall back to built-in embedded default and log a warning.
- `profiles/_default.yaml` exists but has no `format` field → fall back to built-in embedded default.
- Project does not have a `profiles/` directory at generation time → use built-in embedded default.

## Requirements

### Functional Requirements

- **FR-001**: System MUST ship a built-in default profile YAML as an embedded resource that contains a `format` field whose value is the JSON schema sent to the AI as `{{profile_format}}`.
- **FR-002**: `spectra init` MUST create `profiles/_default.yaml` from the embedded default unless the file already exists (without `--force`).
- **FR-003**: When resolving `{{profile_format}}`, the system MUST check `profiles/_default.yaml` on disk before falling back to the embedded default.
- **FR-004**: System MUST ship a built-in `CUSTOMIZATION.md` as an embedded resource covering all user-facing customization surfaces.
- **FR-005**: `spectra init` MUST create `CUSTOMIZATION.md` at the project root from the embedded default unless the file already exists.
- **FR-006**: Both `profiles/_default.yaml` and `CUSTOMIZATION.md` MUST be tracked in `.spectra/skills-manifest.json` so `spectra update-skills` can detect user modifications and preserve them.
- **FR-007**: If `profiles/_default.yaml` is malformed or missing the `format` field, the system MUST gracefully fall back to the embedded built-in default and never crash test generation.

### Key Entities

- **ProfileFormatDocument**: A YAML document with a top-level `format` field (string containing the JSON schema sent to the AI) and an optional `fields` section documenting each output field for human readers.

## Success Criteria

- **SC-001**: A user can change the JSON schema sent to the AI for test generation by editing exactly one file (`profiles/_default.yaml`) without writing any code.
- **SC-002**: A new user can locate every customization surface in SPECTRA from a single document at the project root in under 60 seconds.
- **SC-003**: 100% of `dotnet test` runs (including new tests added by this feature) pass after implementation.
- **SC-004**: `spectra update-skills` never overwrites a user-modified `profiles/_default.yaml` or `CUSTOMIZATION.md`.
