# 03 — Deterministic core: what is reused untouched (Area C)

> **Purpose.** Identify, with evidence, the model-free machinery that survives the v2
> migration verbatim — the "reuse maximally" backbone (ARCHITECTURE-v2 §27, §48–59) — and
> draw the line between it and the code that touches the model seam.
>
> Investigation only. Confirmed claims cite `file:line`; risks under **Findings**.

---

## 1. The decisive evidence: no model in the shared library

A repo-wide search for `GitHub.Copilot.SDK`, `CopilotService`, `IAgentRuntime`, and
`ICriticRuntime` across **`src/Spectra.Core`** returns **no files**. The entire shared core —
models, index, validation, coverage, parsing — runs no model and imports no provider SDK.
Everything below is a consequence of that single fact; the citations show *what* is reused,
the grep proves *it is model-free*.

---

## 2. Reused verbatim (model-free)

### 2.1 Persistence — the single write+index path (Spec 049)
`src/Spectra.CLI/IO/TestPersistenceService.cs` — class `:11`, `PersistAsync` `:36`. Writes
each test via `TestFileWriter.WriteAsync` (`:51`), then regenerates the suite index via
`IndexGenerator.Generate` (`:54`) + `IndexWriter.WriteAsync` (`:56`). Dependencies are only
`TestFileWriter`, `IndexGenerator`, `IndexWriter` (`:13–15`) — no model, no provider. This is
the boundary the v2 flow re-enters after the interactive agent returns content.

### 2.2 Indexing
`src/Spectra.Core/Index/` — `IndexGenerator` (`TestCase` → `MetadataIndex`, pure transform),
`IndexWriter` (`System.Text.Json` serialize/deserialize), `DocumentIndexReader` (regex/YAML
extraction). All under `Spectra.Core` → model-free by the §1 grep.

### 2.3 Validation
- `src/Spectra.CLI/Validation/DuplicateDetector.cs` — normalized **Levenshtein** similarity
  (`:6`, `ComputeSimilarity` `:48`, `LevenshteinDistance` `:63`). String metric, **no
  embeddings** — directly upholds ARCHITECTURE-v2 §55 ("embeddings would be a regression").
- `src/Spectra.Core/Validation/` — `TestValidator` (regex/enum schema checks),
  `DependsOnValidator` (HashSet graph cycle detection). Model-free by §1.

### 2.4 Coverage (lexical / ID matching only)
`src/Spectra.Core/Coverage/`:
- `AutomationScanner` — scans automation source for test IDs via compiled regex
  (`AutomationScanner.cs:63–75`), e.g. `\[(?:TestCase|Theory|Fact|Test)\s*\(\s*"(TC-\d{3,})"`
  (`:64`), comment markers (`:68`), `testId=`/`it()`/`pytest.mark.parametrize` (`:70–75`);
  patterns compiled at `:91–92`, applied at `:157`. Pure identifier capture — no semantics.
- `LinkReconciler`, `CoverageCalculator`, `AutoLinkService`, `DocCovAnalyzer`,
  `ReqCovAnalyzer`, `UnifiedCovBuilder` — bidirectional ID matching, HashSet membership,
  count aggregation. Model-free by §1.

This confirms ARCHITECTURE-v2 §48–59: all three coverage dimensions are exact-field matching,
so "index + coverage need nothing beyond the deterministic CLI."

### 2.5 Parsing (test format + docs)
`src/Spectra.Core/Parsing/` — `MarkdownFrontmatterParser` (Markdig + YamlDotNet),
`FrontmatterUpdater` (regex field replacement), `RequirementsParser` (YAML deserialize),
`DocIndexExtractor`. File-format parsing only; model-free by §1. Note ARCHITECTURE-v2 §27:
"reading docs is plain file reads, not a model call" — these parsers are that mechanism.

---

## 3. Reused vs touches-the-seam (the explicit line)

| Reused verbatim (model-free) | Key reference | Touches the seam (calls a model/provider) | Key reference |
|---|---|---|---|
| `TestPersistenceService` | `CLI/IO/TestPersistenceService.cs:36` | `CopilotGenerationAgent` | `CLI/Agent/Copilot/GenerationAgent.cs:239` |
| `IndexGenerator`/`IndexWriter`/`DocumentIndexReader` | `Core/Index/` | `CopilotService` (session factory) | `CLI/Agent/Copilot/CopilotService.cs:61,85` |
| `DuplicateDetector` (Levenshtein) | `CLI/Validation/DuplicateDetector.cs:48` | `CopilotCritic` / `GroundingAgent` | `CLI/Agent/Copilot/GroundingAgent.cs:124` |
| `TestValidator`/`DependsOnValidator` | `Core/Validation/` | `CriteriaExtractor` | `CLI/Agent/Copilot/CriteriaExtractor.cs:85` |
| Coverage (`AutomationScanner` et al.) | `Core/Coverage/AutomationScanner.cs:63` | `RequirementsExtractor` (legacy) | `CLI/Agent/Copilot/RequirementsExtractor.cs:72` |
| Parsing (`MarkdownFrontmatterParser` et al.) | `Core/Parsing/` | `ProviderMapping` (config→SDK) | `CLI/Agent/Copilot/ProviderMapping.cs` |
| Prompt **compilation** (`BuildFullPrompt`) | `CLI/Agent/Copilot/GenerationAgent.cs:448` | `AgentFactory` (agent selection) | `CLI/Agent/AgentFactory.cs` |

Note the one subtlety: `BuildFullPrompt` (`GenerationAgent.cs:448`) physically lives in the
same file as the seam call but is itself model-free — it is exactly the piece
ARCHITECTURE-v2 §67 wants to extract into a versioned, unit-testable prompt-compiler. It is
"reused" in spirit but must be *relocated out of* the Copilot agent class.

---

## 4. Findings (recorded, not fixed)

- **F-1 — Seam-side code lives under `Spectra.CLI/Agent/Copilot/`, not in `Spectra.Core`.**
  The clean split (all model code in one CLI namespace, zero in Core) is real and is the
  reason the reuse surface is large. No defect; recorded as the structural fact that makes
  the migration tractable.
- **F-2 — `BuildFullPrompt` is model-free but Copilot-coupled by location.** It sits inside
  `CopilotGenerationAgent` (`GenerationAgent.cs:448`) and is `internal static`, so extracting
  it for the v2 prompt-compiler is a move, not a rewrite — but today no non-Copilot caller can
  reach it without the class.

---

## 5. Conclusion

The reuse surface is **everything in `Spectra.Core` plus `TestPersistenceService` and the
deterministic validators/parsers in `Spectra.CLI`** — proven model-free by the §1 grep. The
migration does not touch any of it. The only generation-side code that must move but is itself
model-free is the prompt compiler (`BuildFullPrompt`, `GenerationAgent.cs:448`), which v2
promotes to a standalone artifact. Everything that actually *calls* a model is confined to
`src/Spectra.CLI/Agent/Copilot/` (the right column of §3) and is the subject of `01`/`02`.
