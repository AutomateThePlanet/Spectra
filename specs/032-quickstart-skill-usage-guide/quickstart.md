# Quickstart — Verifying feature 032 locally

How to confirm the Quickstart SKILL & Usage Guide feature works after implementation.

## Prerequisites

- Local build of `Spectra.CLI` from this branch (`032-quickstart-skill-usage-guide`).
- A throwaway test directory.

## Verification steps

### 1. Build the CLI

```bash
dotnet build src/Spectra.CLI
```

Expect: build succeeds, embedded resource `Spectra.CLI.Skills.Content.Skills.spectra-quickstart.md` and `Spectra.CLI.Skills.Content.Docs.USAGE.md` are bundled into the assembly.

### 2. Init a fresh project

```bash
mkdir /tmp/spectra-032-test && cd /tmp/spectra-032-test
dotnet run --project <repo>/src/Spectra.CLI -- init
```

Expect:
- `.github/skills/spectra-quickstart/SKILL.md` exists.
- `USAGE.md` exists at the project root.
- `.spectra/skills-manifest.json` contains entries for both files with non-empty SHA-256 hashes.

### 3. Inspect the bundled content

```bash
cat .github/skills/spectra-quickstart/SKILL.md | head -20
cat USAGE.md | head -20
```

Expect:
- The SKILL begins with the YAML frontmatter (`name: SPECTRA Quickstart`).
- USAGE.md begins with `# SPECTRA Usage Guide — VS Code Copilot Chat`.
- Neither file contains placeholder tokens like `{{...}}`.

### 4. Verify all 11 workflows are present

```bash
grep -c "^## Workflow" .github/skills/spectra-quickstart/SKILL.md
```

Expect: `11`.

### 5. Verify USAGE.md is offline-clean

```bash
grep -E "runInTerminal|awaitTerminal|browser/openBrowserPage" USAGE.md && echo "FAIL" || echo "OK"
```

Expect: `OK` (no in-chat tool references).

### 6. Test `update-skills` refresh on unmodified files

```bash
dotnet run --project <repo>/src/Spectra.CLI -- update-skills
```

Expect: both new files reported as `up-to-date` (or `updated` if hashes drift between rebuilds).

### 7. Test `update-skills` preserves customizations

```bash
echo "MY EDIT" >> USAGE.md
dotnet run --project <repo>/src/Spectra.CLI -- update-skills
grep "MY EDIT" USAGE.md
```

Expect: `MY EDIT` line still present (file was skipped because hash changed). Update-skills output should show `skipped (customized)` for `USAGE.md`.

### 8. Test `--skip-skills` does not write the new files

```bash
mkdir /tmp/spectra-032-skip && cd /tmp/spectra-032-skip
dotnet run --project <repo>/src/Spectra.CLI -- init --skip-skills
ls .github/skills/spectra-quickstart 2>/dev/null && echo "FAIL" || echo "OK"
ls USAGE.md 2>/dev/null && echo "FAIL" || echo "OK"
```

Expect: both `OK` (neither file created).

### 9. Run the test suite

```bash
dotnet test
```

Expect: all existing tests pass + new tests for this feature pass.

### 10. Manual Copilot Chat smoke test (optional)

In a project with bundled SKILLs installed, open VS Code Copilot Chat and type:

> "help me get started"

Expect: Copilot invokes the spectra-quickstart SKILL and presents the 11-workflow overview with example prompts.
