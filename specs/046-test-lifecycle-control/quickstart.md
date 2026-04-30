# Quickstart: Test Lifecycle & Process Control

**Phase**: 1 (Design & Contracts)
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This is a hands-on walkthrough for two audiences: developers implementing Spec 040, and end-users adopting the new commands once the feature ships. Run all commands from the workspace root.

---

## A. End-user walkthrough

### A.1 Audit your workspace before adopting Spec 040

```bash
spectra doctor ids
```

Expected output (human mode):

```
Test ID Audit (workspace: ~/code/my-tests)
  Total tests:        247
  Unique IDs:         245   ⚠ 2 duplicates
  High-water-mark:    TC-247
  Next ID:            TC-248

Duplicates:
  TC-142
    test-cases/checkout/TC-142.md  (modified 2026-03-15)
    test-cases/auth/TC-142.md      (modified 2026-04-20)

Index mismatches: 0

Run `spectra doctor ids --fix` to renumber later occurrences.
```

If duplicates are reported, decide:

- **Keep them** (do nothing — the new allocator only prevents *new* duplicates).
- **Auto-fix** — run `spectra doctor ids --fix`. The older file keeps its ID; the newer file is reassigned at `HWM + 1`. Incoming `depends_on` references and literal-string `[TestCase("TC-NNN")]` patterns are updated best-effort.

### A.2 Delete a single test

Always preview first:

```bash
spectra delete TC-142 --dry-run
```

Output:

```
Would delete TC-142 "Checkout with expired card" from suite checkout
  File: test-cases/checkout/TC-142.md
  ⚠ Stranded automation: tests/e2e/CheckoutTests.cs
  Cleanup: TC-150, TC-151 will lose 'TC-142' from their depends_on
This is a hard delete. Use Git to recover.
```

Then commit:

```bash
spectra delete TC-142 --force --no-automation-check
```

Result: file gone, indexes updated, dependents cleaned up. `git status` shows the diff; `git restore` undoes everything.

### A.3 Rename a suite

```bash
spectra suite rename checkout payments --dry-run
spectra suite rename checkout payments
```

Test IDs are unchanged. Saved selections that referenced `checkout` now reference `payments`. Any per-suite config block is re-keyed.

### A.4 Stop a runaway operation

In one terminal:

```bash
spectra ai generate --suite checkout --count 80
# … producing 30 tests, 60 tests, …
```

In another terminal (or via Copilot Chat: "stop the generation"):

```bash
spectra cancel
```

Output:

```
Cancellation requested for ai generate (PID 12345)
Cooperative shutdown completed in 1.4s.
Wrote .spectra-result.json (status=cancelled, tests_written=37).
```

The progress page (`.spectra-progress.html`) refreshes once more to the `Cancelled` terminal state, then stops auto-refreshing. The 37 tests already written remain on disk.

### A.5 Hard-cancel when cooperative isn't enough

```bash
spectra cancel --force
```

Skips the 5 s grace and process-tree-kills immediately. Use when you know the running operation isn't responding (e.g., wedged HTTP call).

---

## B. Developer walkthrough — implementing Spec 040

### B.1 Phase 1 — ID allocator persistence

Implement in this order:

1. **`Spectra.Core/IdAllocation/HighWaterMarkStore.cs`** — read/write `.spectra/id-allocator.json`. Tests: round-trip, missing file, corrupted file, version mismatch, atomic write.
2. **`Spectra.Core/IdAllocation/FileLockHandle.cs`** — `IDisposable` wrapping a `FileShare.None` `FileStream`. Tests: held-lock contention, release on dispose, retry timeout.
3. **`Spectra.Core/IdAllocation/TestCaseFrontmatterScanner.cs`** — walk `test-cases/**/*.md`, parse frontmatter ID via the existing `Spectra.Core/Parsing/MarkdownFrontmatterParser`. Cached per-process via `Lazy<Task<int>>`. Tests: zero tests, malformed frontmatter (skip + warn), stale index detection.
4. **`Spectra.Core/IdAllocation/PersistentTestIdAllocator.cs`** — orchestrate steps 1–3 + delegate to existing `Spectra.Core.Index.TestIdAllocator` for actual ID format. Tests: concurrent allocation across two threads (using a real lock file in a temp workspace), HWM monotonicity, deleted-ID-never-reused, `id_start` floor, version mismatch on HWM, lock timeout.
5. **Wire** — replace the call sites that currently use `TestIdAllocator.AllocateMany(count)` to go through `PersistentTestIdAllocator.AllocateAsync(count, prefix, idStart, ct)`. The two known sites:
   - `src/Spectra.CLI/Agent/Tools/GetNextTestIdsTool.cs`
   - `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (and downstream agent paths that allocate IDs)
6. **`spectra doctor ids` command** — `DoctorCommand.cs` parent, `DoctorIdsHandler.cs` for the `ids` subcommand. Implement read-only path first; add `--fix` second.
7. **`.gitignore`** — extend `InitHandler.UpdateGitIgnoreAsync()` to add the four new entries. Existing workspaces: `spectra init` is idempotent — running it again adds the missing entries.

### B.2 Phase 2 — Cancellation infrastructure

1. **`Spectra.CLI/Cancellation/PidFileManager.cs`** — write/validate/clean `.spectra/.pid`. Stale-PID detection per Decision 11. Tests: write-roundtrip, stale (no such PID), stale (wrong process name), live + valid.
2. **`Spectra.CLI/Cancellation/SentinelWatcher.cs`** — start a 200 ms polling task that triggers a callback when `.spectra/.cancel` appears. Tests: appears within poll window, absent, removed mid-watch.
3. **`Spectra.CLI/Cancellation/CancellationManager.cs`** — singleton, `RegisterCommandAsync(name, externalToken) → linked CTS`, `Token` property, `ThrowIfCancellationRequested()`, `UnregisterCommandAsync()`. Tests: idempotent register/unregister, sentinel triggers token, external Ctrl+C triggers token, double-register throws.
4. **`spectra cancel` command** — `CancelCommand.cs`, `CancelHandler.cs`. State machine per `data-model.md` §9b. Tests: no_active_run, cooperative success, force, hard kill after grace, kill on Windows path, stale PID cleanup.
5. **`ProgressPageWriter` extension** — accept a new phase value `Cancelled` and treat it as terminal (alongside `Completed`). Tests: terminal flag set, embedded JS stops auto-refresh.
6. **Wire into long-running handlers** — for each:
   - At command start: `using var registration = await CancellationManager.Instance.RegisterCommandAsync(commandName, ct);`
   - At every batch boundary: `CancellationManager.Instance.ThrowIfCancellationRequested();`
   - In `catch (OperationCanceledException)`: write the cancelled result + cancelled progress page, return exit code 130.
   - In `finally`: `await CancellationManager.Instance.UnregisterCommandAsync();`
   - The six handlers per `plan.md` Project Structure.
7. **Result classes** — extend each long-running command's result class with the new common fields (`cancelled_at`, `phase`, `phase_progress`, `files`, `message` — `tests_written` etc. is per-command and may already exist).
8. **Integration tests** — one per long-running command: start the command in-process with a mocked Copilot SDK that delays per-batch; trigger cancellation via the `CancellationManager.Instance` token; assert the result artifact has `status: "cancelled"` and the artifacts on disk match the expected partial state.

### B.3 Phase 3 — Delete & suite management

1. **`Spectra.CLI/Commands/Delete/DeleteCommand.cs` + `DeleteHandler.cs`** per `cli-commands.md` §1 and `result-json.md` §2.
   - Resolve phase reuses the existing index reader and frontmatter parser.
   - Atomic writes use the existing temp-and-rename helper.
   - Cascade `depends_on` cleanup: scan all suites, find dependents, rewrite atomically.
   - Tests: single, bulk, missing, automation-linked refusal, automation-linked force, depends_on cleanup, dry-run, mixed (one ID found, one not).
2. **`Spectra.CLI/Commands/Suite/SuiteCommand.cs` (parent)**, `SuiteListHandler.cs`, `SuiteRenameHandler.cs`, `SuiteDeleteHandler.cs` per `cli-commands.md` §2.
   - Rename: snapshot config → atomic dir-move → atomic index rewrite → atomic config rewrite. Rollback per Decision 8.
   - Delete: cascade depends_on cleanup → recursive directory delete → config + selections rewrite.
   - Tests per Phase 3 in plan.md.
3. **Register all four parents** in `Program.cs::CreateRootCommand()`: `DeleteCommand`, `SuiteCommand`, `CancelCommand`, `DoctorCommand`.

### B.4 Phase 4 — SKILL & doc updates

1. **New SKILLs**:
   - `src/Spectra.CLI/Skills/Content/Skills/spectra-delete.md` per spec §"New SKILL: spectra-delete".
   - `src/Spectra.CLI/Skills/Content/Skills/spectra-suite.md` per spec §"New SKILL: spectra-suite".
2. **Add to `SkillContent.All`** so they're picked up by `InitHandler.CreateBundledSkillFilesAsync()` and `update-skills`.
3. **Modify existing SKILLs** (six long-running + quickstart + help) per spec §"Updates to existing SKILLs".
4. **Modify `spectra-generation.agent.md`** — update the delegation table to route delete/suite/cancel/doctor user phrases to the right SKILL.
5. **Docs**: update `CLAUDE.md`, `cli-reference.md`, `test-format.md`, `CHANGELOG.md` per spec §"Implementation Phases — Phase 4".
6. **Version**: `Directory.Build.props` line 19 — `1.51.4` → `1.52.0`.

### B.5 Manual verification once everything is in

Run, in order:

```bash
# Build + tests
dotnet build
dotnet test

# Re-pack and install locally (smoke check)
dotnet pack src/Spectra.CLI -c Release

# In a fresh workspace:
spectra init
spectra doctor ids                         # zero duplicates, HWM 0, next TC-100 (assuming default id_start=100)
spectra ai generate --suite checkout --count 5 --no-interaction --output-format json
spectra doctor ids                         # 5 tests, HWM 104, next TC-105
spectra delete TC-100 --dry-run            # preview
spectra delete TC-100 --force --no-interaction --output-format json
spectra suite rename checkout payments
spectra suite list
spectra ai generate --suite payments --count 2 --no-interaction &
spectra cancel
spectra suite delete payments --force --no-interaction --output-format json
spectra doctor ids                         # zero tests
```

Inspect each `.spectra-result.json` between commands. Verify:
- `git status` shows clean diffs (no orphan files).
- `.spectra/id-allocator.json` HWM is monotonic.
- Cancelled run's result artifact has `status: cancelled` and `tests_written ≥ 0`.

---

## C. Reference checklist for the SKILL surface

| Trigger phrase | SKILL | Recipe |
|---|---|---|
| "delete TC-XXX", "remove the test for X" | `spectra-delete` | Delete a single test |
| "delete TC-X and TC-Y" | `spectra-delete` | Bulk delete |
| "rename suite X to Y" | `spectra-suite` | Rename a suite |
| "delete the X suite" | `spectra-suite` | Delete a suite |
| "list all suites" | `spectra-suite` | Suite list |
| "stop", "cancel", "kill it" | any of the six long-running SKILLs | Cancel the current run |
| "duplicate test IDs?", "audit IDs" | `spectra-help` | Diagnose test ID issues |

Every SKILL recipe runs the corresponding command in `--no-interaction --output-format json --verbosity quiet` mode, reads `.spectra-result.json`, and reports back to the user. Destructive operations always preview first.
