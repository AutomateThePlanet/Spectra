# Contracts: Test Inventory & Deliverables

This feature exposes no new external interface (no new CLI command, no new MCP tool). Its "contracts" are the test inventory (each test's identity, location, and the observable assertion it guarantees) and the documentation deliverables. The tables below are the acceptance surface.

## Part A — `EndToEndScenarios` (project: `tests/Spectra.Integration.Tests/EndToEndScenarios.cs`)

| Test method | Spans | Observable assertion (contract) |
|-------------|-------|---------------------------------|
| `FromDescriptionHighPriority_RunsViaFilter_EndToEnd` | 049+050+051 | After from-description create (priority high) → `_index.json` contains the new id; the persisted test's `Criteria` is non-empty; real `start_execution_run` with `priorities:["high"]` returns `test_count` == count of high tests and the run queue contains the new id. |
| `BatchGeneration_FromExtractedCriteria_PopulatesCriteriaField` | 047+050 | Extracted criteria for the suite are loaded; the fake agent receives non-null `criteriaContext`; generated test's `Criteria` reflects the extracted ids. |
| `LargeCorpusExtraction_NoSilentSkip_AfterPartialFailure` | 047 | With one doc set to fail-then-succeed, first pass leaves it uncached/failed; re-run re-attempts and succeeds (not skipped). |
| `IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt` | 049 | After from-description create, real `find_test_cases` returns the new test with no manual rebuild. |
| `FilterSilentDrop_NoLongerOccurs` | 051 | The three Path C requests each filter correctly (#2) or return an actionable field-named error (#1,#3); none returns `test_count` == full suite size. |
| `CoverageGuards_FireOnRealisticZeroCorpus` | 048 | Indexing a corpus whose extractions are all inconclusive exits success and the result's `criteria_warning` is populated. |
| `GenerationNote_AppearsWhenNoCriteriaMatch` | 048 | Generating against a no-matching-criteria suite yields a result whose `notes` contains the no-criteria message; present even at quiet verbosity (assert on result object). |

## Part B — `OriginalBugRegression` (project: `tests/Spectra.Integration.Tests/OriginalBugRegression.cs`)

| Test method | `DisplayName` (MUST match exactly) | Guards |
|-------------|-----------------------------------|--------|
| `ExtractCriteriaOnGeneration_PopulatesCriteriaField` | `Original bug: extract-criteria on generation not working` | 050 criteria injection |
| `HighPriorityFilter_FromSuite_ReturnsOnlyHighPriority` | `Original bug: high priority filter from a suite returns whole suite` | 051 filter binding |
| `FromDescriptionTest_AppearsInIndexWithSameShape` | `Original bug: from-description test has different format and is missing from index` | 049 index parity |
| `BigProjectFirstIndex_WarnsWhenZeroCriteria` | `Original bug: first big-project index produced zero criteria silently` | 048 zero-criteria warning |
| `ParseFailure_DoesNotPoisonCache` | `Original bug: cache poisoning on parse failure` | 047 cache gating |

Contract: reverting the matching fix makes exactly that named test fail (SC-003).

## Part C — `ScaleTests` (project: `tests/Spectra.CLI.Tests/Extraction/ScaleTests.cs`)

| Test method | Trait | Contract |
|-------------|-------|----------|
| `LargeCorpus_PerDocumentDeadline_NotCorpusWide` | `Category=Scale` | N=30 synthetic docs, mixed fast/slow latency; succeeded count > 0; only designated slow docs time out; elapsed consistent with per-document (not shared) deadline. |
| `LargeCorpus_SlowDocument_DoesNotAbortRemaining` | `Category=Scale` | A slow doc times out without aborting subsequent docs (they still extract). |

Fast-feedback exclusion: `dotnet test --filter "Category!=Scale"`. Full CI runs without the filter.

## Documentation deliverables

| Deliverable | Path | Acceptance |
|-------------|------|-----------|
| Doc-audit report | `docs/specs/052-doc-audit-report.md` | every audited file listed once with disposition; "updated" ⇒ modified (FR-019/22, SC-005) |
| SKILL transcripts | `docs/specs/052-skill-transcripts.md` | one section per SKILL/agent file in scope; post-051 behavior shown; no pre-047 wording (FR-024/25, SC-006) |
| Consolidated changelog | `CHANGELOG.md` | exactly one consolidated 047–051 entry, Fixed/Added/Changed, user-facing (FR-026, SC-007) |
| Project-knowledge | `PROJECT-KNOWLEDGE.md` | Spec 052 row + silent-failure-pattern learning (FR-027, SC-007) |

## Non-contract (explicitly out)

- No new CLI command, flag, MCP tool, or production type.
- No production behavior change. If a test reveals a real defect, it is recorded (in the doc-audit report's follow-ups), not fixed here (FR-028).
