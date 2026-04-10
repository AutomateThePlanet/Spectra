# Tasks: 031 Profile Format File & Customization Guide

## T1 — Add embedded default profile YAML
- Create `src/Spectra.CLI/Skills/Content/Profiles/_default.yaml` with documented `format` field + `fields` reference section.
- Update `Spectra.CLI.csproj` to include `Skills\Content\Profiles\*.yaml` as embedded resource.

## T2 — Add embedded customization guide
- Create `src/Spectra.CLI/Skills/Content/Docs/CUSTOMIZATION.md`.
- Update `Spectra.CLI.csproj` to include `Skills\Content\Docs\*.md` as embedded resource.

## T3 — Implement ProfileFormatLoader
- New file `src/Spectra.CLI/Profile/ProfileFormatLoader.cs`.
- Methods: `LoadFormat(string workingDirectory)`, `LoadEmbeddedDefaultYaml()`, `LoadEmbeddedCustomizationGuide()`.
- 3-tier fallback with malformed-file safety.

## T4 — Wire ProfileFormatLoader into GenerationAgent
- Update `BuildFullPrompt` to accept optional `profileFormat` string and use it in both code paths.
- Update `GenerateTestsAsync` to load and pass it.

## T5 — Init handler creates new files
- Add `CreateDefaultProfileAsync` and `CreateCustomizationGuideAsync` to `InitHandler`.
- Both register hashes in `.spectra/skills-manifest.json`.

## T6 — update-skills tracks new files
- Update `UpdateSkillsHandler` to include both new files in its managed list.

## T7 — Tests
- Add `tests/Spectra.CLI.Tests/Profile/ProfileFormatLoaderTests.cs` (4 tests).
- Add init tests for new files in `Commands/InitCommandTests.cs` (2 tests).

## T8 — Verify
- Run `dotnet test` and confirm all tests pass.
