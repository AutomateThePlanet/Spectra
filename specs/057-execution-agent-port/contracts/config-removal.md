# Contract: ExecutionConfig Field Removal + Back-Compat

**Surface**: `ExecutionConfig` (`src/Spectra.Core/Models/Config/ExecutionConfig.cs`).

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | the config model | inspect | `CopilotSpace` (`copilot_space`) and `CopilotSpaceOwner` (`copilot_space_owner`) no longer exist |
| 2 | a legacy `spectra.config.json` with `execution.copilot_space` / `copilot_space_owner` set | deserialized | deserialization succeeds; the unknown keys are ignored (no exception) |
| 3 | a config with an `execution` block | deserialized | the (now-empty) `ExecutionConfig` still binds without error |

## Invariants

- No C# reads the removed fields (grep-confirmed) — removal changes no runtime behavior.
- `System.Text.Json` default unknown-key tolerance provides back-compat — no migration, no shim.
- No change to any other config field or to the config file format the user writes.
