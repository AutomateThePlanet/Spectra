# Implementation Plan: 031 Profile Format File & Customization Guide

## Architecture

Two new embedded resources, one new loader class, and additions to three existing files.

### New embedded resources

```
src/Spectra.CLI/Skills/Content/
├── Profiles/_default.yaml        ← built-in default profile
└── Docs/CUSTOMIZATION.md         ← customization guide
```

Both registered in `Spectra.CLI.csproj` via `<EmbeddedResource>`.

### New code

- `src/Spectra.CLI/Profile/ProfileFormatLoader.cs`
  - Static helper `LoadFormat(string workingDirectory)` returns the JSON schema string used for `{{profile_format}}`.
  - Resolution order:
    1. `profiles/_default.yaml` on disk → parse YAML, extract `format` field.
    2. Embedded resource `Spectra.CLI.Skills.Content.Profiles._default.yaml` → parse, extract `format`.
  - On parse failure or missing `format`, log warning and fall back to embedded.
  - Static helpers `LoadEmbeddedDefaultYaml()` and `LoadEmbeddedCustomizationGuide()` return raw text for use by init/update-skills.

### Modified code

- `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`
  - `BuildFullPrompt` accepts an optional `profileFormat` parameter (string). When non-null, it replaces the hardcoded `jsonExample`. The legacy fallback path and template-loader path both use it.
  - `GenerateTestsAsync` calls `ProfileFormatLoader.LoadFormat(_basePath)` and passes the result.

- `src/Spectra.CLI/Commands/Init/InitHandler.cs`
  - New step `CreateDefaultProfileAsync` writes `profiles/_default.yaml`.
  - New step `CreateCustomizationGuideAsync` writes `CUSTOMIZATION.md` at the project root.
  - Both register their hashes in the skills manifest so `update-skills` can track them.

- `src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs`
  - Include `profiles/_default.yaml` and `CUSTOMIZATION.md` in the managed-file enumeration.

- `src/Spectra.CLI/Spectra.CLI.csproj`
  - Add `<EmbeddedResource Include="Skills\Content\Profiles\*.yaml" />` and `<EmbeddedResource Include="Skills\Content\Docs\*.md" />`.

## Tests

Added to `tests/Spectra.CLI.Tests/`:

- `Profile/ProfileFormatLoaderTests.cs`
  - `LoadFormat_ReturnsEmbeddedDefault_WhenNoFileExists`
  - `LoadFormat_ReturnsFileContent_WhenDefaultFileExists`
  - `LoadFormat_FallsBackToEmbedded_WhenFileIsMalformed`
  - `LoadFormat_FallsBackToEmbedded_WhenFormatFieldMissing`
- Extend `Commands/InitCommandTests.cs`
  - `HandleAsync_CreatesDefaultProfile`
  - `HandleAsync_CreatesCustomizationGuide`
  - Verify both files appear in skills manifest.

## Risks & decisions

- The existing `ProfileConfig` in `Spectra.Core` is unrelated (it governs repository/suite markdown profiles). We deliberately do NOT add an `ai.profile` config field — the spec resolution narrows to `profiles/_default.yaml` on disk + embedded fallback. Named profiles via config are out of scope for this addendum and can be added later without breaking changes.
- YAML parsing uses YamlDotNet (already a transitive dependency via `Spectra.Core`).
