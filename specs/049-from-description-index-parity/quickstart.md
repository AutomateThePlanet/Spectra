# Quickstart: Verifying From-Description Write & Index Parity

**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

This is the manual verification path a reviewer or QA engineer can run end-to-end to confirm the spec's acceptance criteria are met after implementation. Each step maps to one or more spec FRs / SCs.

## Prerequisites

- Local clone of Spectra on branch `049-from-description-index-parity` with this spec's implementation merged in.
- Working `dotnet` 8+ toolchain on PATH.
- A Spectra workspace to test against. You can use the existing `Spectra_Demo/test_app_documentation` workspace or initialize a fresh one:
  ```bash
  mkdir -p /tmp/spectra-qa-049 && cd /tmp/spectra-qa-049
  dotnet run --project <repo>/src/Spectra.CLI -- init
  ```
- An OpenAI / Azure / Anthropic key configured (the from-description path invokes the Copilot SDK; for tests that do not require an actual AI call, the unit-test suite stubs the generator — see step 6).

## Steps

### Step 1 — Build & run unit tests

```bash
dotnet build
dotnet test tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj
dotnet test tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj
```

**Expect**: All tests green, including:
- `TestPersistenceServiceTests.*` (new — Q1–Q7, failure modes)
- `GenerateHandlerFromDescriptionIndexTests.*` (new — FR-001, FR-002, FR-003, FR-004)
- `BatchIndexEquivalenceTests.*` (new — FR-009, SC-005)
- `IndexHandlerRebuildTests.RecoversUnindexedFromDescriptionTest` (new — FR-006, SC-003)
- `FromDescriptionDiscoveryTests.*` (new — FR-008, SC-002)

**Verifies**: SC-001 partial (unit-level), SC-005, SC-006, SC-007.

### Step 2 — From-description against an empty suite

```bash
cd /tmp/spectra-qa-049
dotnet run --project <repo>/src/Spectra.CLI -- ai generate \
    --suite checkout \
    --from-description "verify guest checkout shows shipping estimate before payment" \
    --auto-complete --output-format json
```

**Expect**:
- Exit code 0.
- A new file `test-cases/checkout/TC-XXX.md` exists.
- `test-cases/checkout/_index.json` exists and contains exactly one `tests[]` entry whose `id` matches the file's frontmatter id.

```bash
cat test-cases/checkout/_index.json
```

**Verifies**: FR-001, FR-002, US1 scenarios 1–2.

### Step 3 — From-description against a suite with existing tests

```bash
# Pre-populate batch tests
dotnet run --project <repo>/src/Spectra.CLI -- ai generate checkout \
    --auto-complete --count 3 --output-format json

# Capture the pre-state index
cp test-cases/checkout/_index.json /tmp/pre-add.json

# Add a from-description test
dotnet run --project <repo>/src/Spectra.CLI -- ai generate \
    --suite checkout \
    --from-description "verify checkout rejects expired cards inline" \
    --auto-complete --output-format json

# Diff
diff <(jq '.tests | length' /tmp/pre-add.json) \
     <(jq '.tests | length' test-cases/checkout/_index.json)
```

**Expect**: The diff shows `4` vs `3` (one more entry). Every id from the pre-state is still present:

```bash
jq -r '.tests[].id' /tmp/pre-add.json | sort > /tmp/pre-ids
jq -r '.tests[].id' test-cases/checkout/_index.json | sort > /tmp/post-ids
comm -23 /tmp/pre-ids /tmp/post-ids   # → empty (no dropped entries)
comm -13 /tmp/pre-ids /tmp/post-ids   # → exactly the new id
```

**Verifies**: FR-003, US1 scenarios 1, 4. SC-006.

### Step 4 — High-priority filter includes the new test

Assuming step 2's or step 3's generated test was given `priority: high` (the from-description generator chooses based on context; if not, hand-edit one entry to `priority: high` for this validation).

Start an MCP server session and query:

```bash
# Invoke the find_test_cases tool via the MCP harness
dotnet run --project <repo>/src/Spectra.MCP -- \
    tools call find_test_cases '{"suite":"checkout","priorities":["high"]}'
```

**Expect**: Response includes the from-description test id.

**Verifies**: FR-008, US1 scenario 2, SC-002, SC-001 (discoverability).

### Step 5 — `smoke` saved selection counts the new test

```bash
dotnet run --project <repo>/src/Spectra.MCP -- \
    tools call list_saved_selections '{"suite":"checkout"}'
```

**Expect**: `smoke` selection's `match_count` includes the new high-priority test.

**Verifies**: FR-008, US1 scenario 3, SC-002.

### Step 6 — Backfill an unindexed test via `index --rebuild`

```bash
# Simulate a pre-fix state: copy an existing test, delete its index entry
cp test-cases/checkout/TC-001.md test-cases/checkout/TC-999.md
# Hand-edit TC-999.md to change id: TC-001 → id: TC-999
sed -i 's/^id: TC-001/id: TC-999/' test-cases/checkout/TC-999.md
# Confirm it's NOT in the index
jq '.tests | map(select(.id == "TC-999")) | length' test-cases/checkout/_index.json
# → 0

# Rebuild
dotnet run --project <repo>/src/Spectra.CLI -- index --rebuild

# Confirm it IS in the index now
jq '.tests | map(select(.id == "TC-999")) | length' test-cases/checkout/_index.json
# → 1
```

**Expect**: Pre-rebuild count is 0, post-rebuild count is 1.

**Verifies**: FR-006, FR-007 (loop continues even if one file had been malformed; verify by also adding a deliberately broken `TC-XX.md` and confirming the rebuild reports it but produces an index with all valid entries). US2 scenarios 1, 2. SC-003.

### Step 7 — Start execution run includes the from-description test

```bash
dotnet run --project <repo>/src/Spectra.MCP -- \
    tools call start_execution_run '{"suite":"checkout"}'
```

**Expect**: Returned run state's `queue` includes the from-description test id.

**Verifies**: FR-001 (executable side), US1 narrative, SC-001.

### Step 8 — Idempotent re-run

```bash
# Re-run the same from-description command
dotnet run --project <repo>/src/Spectra.CLI -- ai generate \
    --suite checkout \
    --from-description "verify guest checkout shows shipping estimate before payment" \
    --auto-complete --output-format json

# Index entry count for the re-generated id should not grow on each run
jq '.tests | group_by(.id) | map(select(length > 1)) | length' \
    test-cases/checkout/_index.json
# → 0  (no duplicates)
```

**Verifies**: FR-004, US1 scenario 5, SC-007.

### Step 9 — Static check: one and only one write path

Run:

```bash
# Find all call sites of TestFileWriter.WriteAsync outside the new service
grep -rn "TestFileWriter()" src/Spectra.CLI/ | grep -v "TestPersistenceService.cs"
grep -rn "\.WriteAsync(.*testCase" src/Spectra.CLI/ | grep -v "TestPersistenceService.cs"
```

**Expect**: No matches in `GenerateHandler.cs` or any other generation orchestrator. Matches in `TestPersistenceServiceTests.cs` (constructing the service) are fine.

**Verifies**: FR-005, US3 scenario 1, SC-004.

## Cleanup

```bash
rm -rf /tmp/spectra-qa-049
```

## Mapping back to spec acceptance criteria

| Spec | Verified by step |
|------|------------------|
| US1 / FR-001, FR-002, FR-003, FR-004 | Steps 2, 3, 8 |
| US1 / FR-008 (filter symptom resolved) | Steps 4, 5 |
| US2 / FR-006, FR-007 | Step 6 |
| US3 / FR-005 (single write path) | Step 9 |
| US3 / FR-011 (failures surfaced) | Step 1 unit tests (failure-mode rows) |
| FR-009 (batch regression) | Step 1 (`BatchIndexEquivalenceTests`) |
| FR-010 (no MCP / filter / index-model change) | Code-review verification, not a runtime check |
| SC-001 | Steps 2–5, 7 |
| SC-002 | Steps 4, 5 |
| SC-003 | Step 6 |
| SC-004 | Step 9 |
| SC-005 | Step 1 |
| SC-006 | Step 3 |
| SC-007 | Step 8 |
