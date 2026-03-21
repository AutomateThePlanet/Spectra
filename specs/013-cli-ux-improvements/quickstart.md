# Quickstart: CLI UX Improvements

**Feature**: 013-cli-ux-improvements | **Date**: 2026-03-21

## What This Feature Adds

1. **Next-step hints** — contextual suggestions after every command
2. **Init automation dirs** — configure where your automation code lives
3. **Init critic model** — enable grounding verification during setup
4. **Interactive continuation** — switch suites without restarting the CLI

## Verification

### 1. Next-Step Hints

```bash
spectra validate
```

After output, verify you see dimmed "Next steps:" suggestions.

```bash
spectra validate --verbosity quiet
```

Verify no hints are shown.

### 2. Automation Directory Setup

```bash
# During init
spectra init
# → Respond to "Where is your automation test code?" prompt
# → Verify spectra.config.json has coverage.automation_dirs updated

# After init
spectra config add-automation-dir ../new-tests
spectra config list-automation-dirs
# → Verify ../new-tests appears with [exists] or [missing]

spectra config remove-automation-dir ../new-tests
spectra config list-automation-dirs
# → Verify ../new-tests is gone
```

### 3. Critic Configuration

```bash
spectra init
# → After provider setup, select "Yes" for grounding verification
# → Pick "google" as critic provider
# → Accept default GOOGLE_API_KEY
# → Verify spectra.config.json has ai.critic section with enabled: true
```

### 4. Interactive Continuation

```bash
spectra ai generate
# → Complete generation for one suite
# → See continuation menu
# → Select "Switch to a different suite"
# → Pick another suite and generate
# → Select "Done — exit"
# → Verify session summary shows both suites
```

## Key Files

| File | Changes |
|------|---------|
| `src/Spectra.CLI/Output/NextStepHints.cs` | New hint helper |
| `src/Spectra.CLI/Commands/Init/InitHandler.cs` | Automation dirs + critic prompts |
| `src/Spectra.CLI/Commands/Config/ConfigCommand.cs` | Automation dir subcommands |
| `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` | Automation dir CRUD |
| `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | Hints + continuation |
| `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` | Hints |
| `src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs` | Hints |
| `src/Spectra.CLI/Commands/Validate/ValidateHandler.cs` | Hints |
| `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` | Hints |
| `src/Spectra.CLI/Commands/Index/IndexHandler.cs` | Hints |
