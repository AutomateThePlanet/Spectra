# Quickstart: Verdict Disposition Policy (Spec 071)

**For**: developers implementing or testing this spec
**Date**: 2026-06-19

---

## End-to-end flow (after Spec 071)

### Normal generate run

```bash
# 1. Analyze and approve (unchanged)
spectra ai compile-analysis-prompt --suite file-management --doc-suite file-management
# ... agent step 2 ...
spectra ai ingest-analysis --suite file-management --from .spectra/analysis.json

# 2. Generate (unchanged)
spectra ai compile-prompt --suite file-management --count 5
# ... agent step 6 ...
spectra ai ingest-tests file-management --from .spectra/generated.json --output-format json
# → { "persisted": 5, "ids": ["TC-110","TC-111","TC-112","TC-113","TC-114"] }

# 3. Critic for TC-110 (grounded) — NEW Step 8 flow
spectra ai compile-critic-prompt --suite file-management --test TC-110
# ... critic subagent writes .spectra/verdicts/critic-verdict-TC-110.json ...
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-110.json --output-format json
# → { "verdict": "grounded", "score": 0.95, "drop": false }
spectra ai ingest-grounding --suite file-management --test TC-110 --from .spectra/verdicts/critic-verdict-TC-110.json
# → { "success": true, "verdict": "grounded", "score": 0.95 }
# TC-110.md now has grounding: block

# 4. Critic for TC-113 (partial) — repair cycle
spectra ai compile-critic-prompt --suite file-management --test TC-113
# ... critic writes .spectra/verdicts/critic-verdict-TC-113.json ...
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-113.json --output-format json
# → { "verdict": "partial", "score": 0.72, "drop": false }

# Repair attempt 1
spectra ai compile-repair-prompt --suite file-management --test TC-113
# → [plain text repair prompt emitted to stdout]
# ... agent reads prompt, patches test, writes .spectra/repaired.json ...
spectra ai ingest-update file-management --test-id TC-113 --from .spectra/repaired.json --output-format json
# → { "success": true, "persisted": 1, "ids": ["TC-113"] }

# Re-critic on repaired TC-113
spectra ai compile-critic-prompt --suite file-management --test TC-113
# ... critic writes .spectra/verdicts/critic-verdict-TC-113.json (new verdict) ...
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-113.json --output-format json
# → { "verdict": "grounded", "score": 0.93, "drop": false }
spectra ai ingest-grounding --suite file-management --test TC-113 --from .spectra/verdicts/critic-verdict-TC-113.json --repaired --repair-attempts 1
# TC-113.md now has: verdict: grounded, repaired: true, repair_attempts: 1

# 5. Critic for TC-114 (still partial after repair)
# ... same repair cycle ...
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-114.json --output-format json
# → { "verdict": "partial", "score": 0.68, "drop": false }
spectra ai ingest-grounding --suite file-management --test TC-114 --from .spectra/verdicts/critic-verdict-TC-114.json --repair-attempts 1
# TC-114.md: verdict: partial, flagged_for_review: true, repair_attempts: 1

# 6. Critic for TC-138 (hallucinated) — trail + delete
spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-TC-138.json --output-format json
# → { "verdict": "hallucinated", "score": 0.30, "drop": true }
spectra ai record-drop --suite file-management --test TC-138 --from .spectra/verdicts/critic-verdict-TC-138.json
# → { "success": true, "trail_file": ".spectra/dropped-tests.json" }
spectra delete TC-138 --force --no-interaction --output-format json
# → { "deleted": ["TC-138"], ... }
```

### Final report (Step 9)

```
Generated 4 verified test cases (1 dropped as hallucinated, 1 flagged for review).
  Kept grounded:      TC-110, TC-111, TC-112      (3)
  Repaired to grounded: TC-113                    (1)
  Flagged partial:    TC-114                      (1)
  Dropped hallucinated: TC-138                    (1)

TC-114 is flagged for review. Run: spectra ai review-flagged --suite file-management
```

---

## Human review phase

```bash
# List flagged tests (non-interactive)
spectra ai review-flagged --suite file-management --no-interaction --output-format json

# Interactive review
spectra ai review-flagged --suite file-management
# → Shows TC-114 with condensed findings
# → [A]ccept  [D]elete  [S]kip  [Q]uit

# If user accepts TC-114:
# → flagged_for_review cleared; test stays partial; coverage counts it

# If user deletes TC-114:
# → record-drop (user_decided) + DeleteHandler
```

---

## What tests look like now (examples)

**Grounded test** (`test-cases/file-management/TC-110.md` frontmatter):
```yaml
---
id: TC-110
priority: high
...
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:00:00Z
---
```

**Repaired test** (`TC-113.md`):
```yaml
---
id: TC-113
priority: medium
...
grounding:
  verdict: grounded
  score: 0.93
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:05:00Z
  repaired: true
  repair_attempts: 1
---
```

**Flagged partial test** (`TC-114.md`):
```yaml
---
id: TC-114
priority: medium
...
grounding:
  verdict: partial
  score: 0.68
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:07:00Z
  flagged_for_review: true
  repair_attempts: 1
  condensed_findings:
    - element: "Expected Result"
      reason: "Error message text not found in source documentation"
---
```

---

## Checking the drop trail

```bash
cat .spectra/dropped-tests.json
# {"id":"TC-138","suite":"file-management","title":"Verify 1 KB file size display","drop_reason":"hallucinated",...}
```

---

## Phase gates (for implementers)

**After Phase 1** (before Phase 2): Run a generate batch and verify:
1. Every kept test has a `grounding:` block in its `.md` frontmatter
2. Per-test verdict files exist in `.spectra/verdicts/`
3. `dropped-tests.json` has one entry per hallucinated test
4. Hallucinated tests are gone from disk and from `_index.json`
5. `dotnet test` green (Core 557+ / CLI 1150+)

**After Phase 2** (before Phase 3): Run a generate batch with a known-partial test and verify:
1. Repair prompt is emitted correctly for a partial test
2. After repair, the test's grounding block shows the final verdict
3. Batch ran to completion without prompting
4. Final report shows counts: kept-grounded / repaired-to-grounded / flagged-partial / dropped

**After Phase 3** (before Phase 4): Run `review-flagged` on a suite with flagged tests:
1. Accept clears `flagged_for_review` while keeping the partial block
2. Delete runs trail + three-phase clean delete
3. No dangling references after any action
