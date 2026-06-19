# Contract: `spectra ai review-flagged`

**Spec**: 071 | **Phase**: 3

## Purpose

Lists tests with `flagged_for_review: true` in their grounding block, and in interactive mode allows the reviewer to accept or delete each one. Retry-repair is delegated to the `spectra-review-flagged` skill (requires agent inference).

## Invocation

```
spectra ai review-flagged [--suite <suite>] [--no-interaction] [--output-format json|human]
```

### Options

| Option | Description |
|--------|-------------|
| `--suite <suite>` | Scope to one suite (default: all suites) |
| `--no-interaction` | List flagged tests and exit; no disposition |
| `--output-format json` | Machine-readable output |

### Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success (list only, or all dispositions completed) |
| 1 | Error |
| 2 | Flagged tests remain undisposed (--no-interaction exit or partial review) |

## Behaviour (interactive)

1. Scans `test-cases/{suite}/*.md` files (or all suites) for tests with `grounding.flagged_for_review: true`.
2. If none found: prints "No flagged tests." and exits 0.
3. For each flagged test, displays:
   ```
   ── TC-113  [partial, score: 0.72, attempts: 1]
      Suite: file-management
      Title: Verify file size conversion from bytes to kilobytes
      Flagged findings:
        • Step 3 — Conversion factor not verbatim in documentation
        • Expected Result — Error message text not found in docs

   [A]ccept as-is  [D]elete  [S]kip  [Q]uit
   ```
4. **Accept**: Calls `AcceptFlaggedHandler` which:
   - Reads the test, clears `flagged_for_review: true` from `GroundingMetadata`, rewrites via `TestFileWriter`.
   - Preserves all other grounding fields (verdict stays `partial`, findings kept as acknowledgement).
   - Does NOT modify `_index.json` (test still present).
5. **Delete**: Calls `ReviewFlaggedHandler.DeleteAsync` which:
   - Runs `record-drop --suite {s} --test {id} --reason user_decided`.
   - Runs `spectra delete {id} --force --no-interaction` (three-phase: index, depends_on, file).
6. **Skip**: Leaves test unchanged, moves to next.
7. **Quit**: Stops review, exits 2 (remaining tests undisposed).

## Retry-repair

Retry-repair is NOT available as an inline option in this command (requires agent inference). Instead, after the review session, the command prints:

```
To retry repair for any flagged test, run:
  spectra ai compile-repair-prompt --suite <suite> --test <id>
  (then follow the repair flow in the spectra-review-flagged skill)
```

The `spectra-review-flagged` skill drives the retry-repair cycle for a named test.

## Output (JSON mode / --no-interaction)

```json
{
  "flagged_count": 3,
  "tests": [
    {
      "id": "TC-113",
      "suite": "file-management",
      "title": "Verify file size conversion from bytes to kilobytes",
      "score": 0.72,
      "repair_attempts": 1,
      "condensed_findings": [
        { "element": "Step 3", "reason": "Conversion factor not verbatim in documentation" }
      ]
    }
  ]
}
```

## Invariants

- `review-flagged` NEVER changes verdict or score — it only clears the `flagged_for_review` flag (accept) or deletes the test (delete).
- Accepting a partial test does NOT upgrade it to grounded — the test remains partial with `flagged_for_review` cleared. This is intentional: accept = "acknowledged and I'm OK with it."
- Delete path goes through `record-drop` + `DeleteHandler` — same consistency contract as critic-drop (file + index + depends_on kept in sync).
