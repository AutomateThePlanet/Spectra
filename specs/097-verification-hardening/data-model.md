# Data Model: Verification Hardening (Phase 6, Part 2)

No new files. No change to how verdict files are written. The changes are in config, the record's role, JSON shapes and
the installed critic agent.

## Fingerprint record — `.spectra/verdicts/critic-input-<id>.json` (unchanged format)

```json
{ "id": "TC-101", "suite": "storage", "file": "test-cases/storage/TC-101.md", "fingerprint": "sha256:…",
  "compiled_at": "2026-10-14T09:12:03.1234567+00:00" }
```

| Field | Role in 097 |
|-------|-------------|
| `fingerprint` | unchanged: a test whose content changed since → stale (`ContentChanged`) |
| `compiled_at` | marks a 091+ record (the time rule applies); its value is used only when the record file's time can't be read |
| record file's last-write time | **new reference**: a verdict file older than it by more than 2 s → stale (`OlderThanPrompt`) |

Rewritten (temp file + move) on every `compile-critic-prompt` for the test, so its file time marks the compile.

### Freshness decision (per verdict)

```text
no record                         → NoRecord (accepted)
fingerprint ≠ current             → Stale (ContentChanged)
record has compiled_at
  and the verdict is a file:
    reference = record file time (fallback compiled_at)
    verdict file time < reference − 2 s → Stale (OlderThanPrompt)
otherwise                         → Fresh
```

## Audit entry (additive)

| Field | Change |
|-------|--------|
| `stale` | new, `true` only on a verdict-file entry whose verdict is stale; omitted otherwise |
| `action_needed` | `critic` when stale (overrides the block-based value) |

Summary: new `stale` count; all counts cover the whole suite (before `--action` / `--ids`). Result: new `not_found`
(only when `--ids` was given).

## Config — `ai.critic` (optional)

```json
"ai": {
  "critic": { "model": "sonnet" }
}
```

| Key | Type | Default | Rule |
|-----|------|---------|------|
| `ai.critic.model` | string | `sonnet` | `^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}$`; else `CONFIG_INVALID` (`update-skills`: warning, model line kept) |

Other `ai.critic.*` keys: the retired provider keys keep the validate notice; the old in-process keys (`enabled`,
`timeout_seconds`, `max_concurrent`, …) are ignored.

## Installed critic agent — `.claude/agents/spectra-critic.agent.md`

Frontmatter `model:` = the effective `ai.critic.model` at install (init / update-skills). The manifest records the hash
of the rendered file; the bundled content compared by `update-skills` is the file rendered with the current setting.
With an invalid config, `update-skills` keeps the installed `model:` line (`sonnet` when the agent isn't installed) and
warns (FR-022a).

## Removed

`CriticFactory`, `CriticModelResolver` (CLI `Agent/Critic/`), the unused members of `CriticConfig`.
