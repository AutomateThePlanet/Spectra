# Quickstart: Universal Progress/Result

## What Changed

Every SKILL-wrapped CLI command now writes `.spectra-result.json` with structured results. Long-running commands also write `.spectra-progress.html` with live-updating progress.

## For SKILL Authors

All SKILLs follow this 5-step pattern:

```markdown
1. show preview .spectra-progress.html        # Open progress page (long-running only)
2. runInTerminal: spectra <cmd> --no-interaction --output-format json --verbosity quiet
3. awaitTerminal                               # Wait for completion
4. readFile .spectra-result.json               # Read structured result
5. Present results readably                    # Format for user
```

## For CLI Handler Developers

Add progress support to a new handler:

```csharp
// 1. Create manager with command name and phases
var progress = new ProgressManager("my-command", ProgressPhases.MyCommand);

// 2. Clean up stale files
progress.Reset();

// 3. Start progress page
await progress.StartAsync("My Command Title");

// 4. Update during execution
await progress.UpdateAsync("phase-name", "Doing something (3/10)...", summaryData);

// 5. Complete or fail
await progress.CompleteAsync(myTypedResult);
// OR
await progress.FailAsync("Something went wrong", partialResult);
```

## Verification

After any SKILL-wrapped command completes:
- `.spectra-result.json` exists with `"status": "completed"` or `"status": "failed"`
- For long-running commands: `.spectra-progress.html` exists with auto-refresh removed
