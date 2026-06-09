# Queued feature 048 — Critic boundary value detection

> **Investigation-only.** No production code, specs, configs, or skills were modified.
> Every claim about current behavior is cited `file:line`. Hypotheses are marked `INFERRED`
> with what would confirm them.

## Two preliminaries

- **No draft file exists** for this feature. Searches for "boundary value" and a
  `queued/`/`backlog/`/`draft/` directory returned nothing. Intent below is reconstructed from the
  one-line summary: *"the critic should catch missing boundary values / edge cases."*
- **Numbering collision.** The repo already ships an *implemented, unrelated* spec
  `specs/048-criteria-coverage-guards` (v1.52.6). The number `048` here is only a working label.

## Reconstructed intent

Have the verification critic flag tests that omit boundary values and edge cases (min/max,
off-by-one, empty/null, overflow, timeout) — i.e. catch *under-testing* of edges, not just
ungrounded claims.

## 1. Does the problem still exist? — **Yes, but the ground shifted underneath it**

The migrated critic checks **grounding only** — whether each claim in a test traces to
documentation — and explicitly does not assess completeness or whether edge cases are missing:

- The critic subagent's verdict vocabulary is `grounded` / `partial` / `hallucinated`, defined
  purely in terms of whether claims trace to docs
  (`src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:42-69`).
- Its isolation contract scopes it to "the test artifact ... and the selected source documents" and
  forbids reasoning beyond them; an unsupported claim becomes `unverified`, never "incomplete"
  (`spectra-critic.agent.md:17-26`).
- The ingest boundary enforces exactly that enum and nothing about coverage/completeness:
  `VerdictIngestor.Classify` accepts only `grounded`/`partial`/`hallucinated`/`manual` and reads a
  `findings[]` of grounded/unverified/hallucinated items
  (`src/Spectra.CLI/Verification/VerdictIngestor.cs:102-168`).

So nothing in today's critic detects a *missing* boundary value. The gap stands.

**However**, the migration introduced a new home for edge-case concerns that did not exist in the
pre-migration architecture the original feature assumed: a dedicated **behavior-analysis phase** that
applies the ISTQB techniques, including Boundary Value Analysis, *at generation time*:

- The generation agent applies "six ISTQB techniques (EP, BVA, DT, ST, EG, UC)" and the
  `ingest-analysis` recommendation carries a `technique_breakdown` map so the user sees, e.g., "how
  many BVA boundary test cases will be generated"
  (`src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md:13`).

This means boundary *coverage* is now partly addressed proactively (generate the boundary cases)
rather than only retroactively (critic complains they are missing).

## 2. Where is the seam now?

The original feature assumed an **in-process critic provider**. That no longer exists — the
in-process critic is retired and the factory always reports it unavailable:
`CriticFactory.TryCreate` returns *"In-process critic retired (Spec 058); verification runs as the
spectra-critic subagent"* (`src/Spectra.CLI/Agent/Critic/CriticFactory.cs:100-117`). Verification of
record is now a `context: fork` subagent. So the seam is one of two places:

- **(a) Expand the critic subagent's mandate.** Instructions live in
  `spectra-critic.agent.md` (the subagent prompt) and the CLI-side prompt compiler
  (`CriticPromptBuilder` / `CriticPromptCompiler`, invoked via `spectra ai compile-critic-prompt`,
  `spectra-critic.agent.md:30-34`). Adding boundary-completeness would require a **new verdict
  dimension orthogonal to grounding** — completeness is "what *should* be tested," which the current
  isolation contract (judge only what is in front of you) deliberately excludes. It would also need
  the verdict schema and `VerdictIngestor` (`VerdictIngestor.cs:102-116`) to carry the new
  signal, since today they only model grounded/partial/hallucinated.

- **(b) Place it in the analysis phase.** The `compile-analysis-prompt` → in-session analysis →
  `ingest-analysis` seam already reasons about BVA and emits a `technique_breakdown`
  (`spectra-generation.agent.md:13`). A "boundary coverage gap" check fits naturally here — it is a
  generation-time completeness concern, not a per-artifact grounding concern.

The doc-worthy point: in the old world "the critic catches missing boundary values" was a single
in-process step; post-migration that intent splits along an architectural seam (grounding-critic vs.
analysis-phase completeness) that did not exist when the feature was sketched.

## 3. Verdict — **SURVIVES-WITH-REWRITE**

The intent (don't let edge cases slip through) is still wanted, but the mechanism must change:

- the in-process critic it assumed is gone (`CriticFactory.cs:100-117`);
- the surviving critic is grounding-scoped by contract and would need a genuinely new completeness
  dimension to take this on (`spectra-critic.agent.md:17-26`, `:42-69`);
- and a better-fitting home (the analysis phase with native BVA) now exists that the original could
  not have referenced.

A spec must pick seam (a) or (b) — that choice is the rewrite.

## 4. Dependencies / risk

- **Regression net:** seam (a) touches the verdict schema + `VerdictIngestor` (the fail-loud ingest
  boundary, `VerdictIngestor.cs`); seam (b) touches `ingest-analysis`. Both are
  `Spectra.CLI`-side ingest boundaries, not `Spectra.Core`/persistence, so the blast radius is
  moderate.
- **Provider/SDK retirement:** the critic's SDK use was already retired in Spec 058
  (`CriticFactory.cs:100-117`), and the analysis phase runs in the interactive session. Therefore
  this feature is **not blocked** by the still-pending provider/SDK retirement.
- **Conceptual risk:** boundary-completeness contradicts the critic's current isolation contract
  ("treat what you weren't given as unverified, not a defect"). Folding completeness into the critic
  risks eroding that clean grounding/quality separation — a reason seam (b) may be preferable.
