# Quickstart: verifying Spec 085

Each fix's regression test must fail on `2a43298` and pass after. As in 080–083, run the new tests in a detached
worktree at `2a43298` with the test files copied in. A test that uses a new API or option fails to compile there,
which counts as failing. T2, T9 and the all-parsed half of T4 are guards and pass on both.

```bash
dotnet test --filter "FullyQualifiedName~SectionExtractor|FullyQualifiedName~HeadingMatch"   # T1, T2, T3 (Core)
dotnet test --filter "FullyQualifiedName~ChangeClassifier"                                    # T6
dotnet test --filter "FullyQualifiedName~Commands.Baseline"                                   # T3–T5, T7–T9 (CLI)
dotnet test --filter "FullyQualifiedName~SkillsManifest"                                       # F5 skill assertions
```

Manual check with the built CLI, in a scratch git repo:

1. Run `spectra init`. Add a doc with `## Create customer records <a href="#create-customer-records" id="create-customer-records"></a>`, `## Title {#custom-id}`, `## [Payments](payments.md)` and two `## Setup` headings. Add one test per anchor (`#create-customer-records`, `#custom-id`, `#payments`, `#setup-1`).
2. Run `source-baseline --init --all-suites`, then `update-triage`. Expect every test UNCHANGED and no REMOVED.
3. Corrupt one test file's frontmatter, delete another test, and run `update-triage` after a cosmetic edit. The corrupt test's entry is still in `_source-baseline.json`, and the warning names its file.
4. Delete a section a test references, then run `ingest-update` on that test with an unrelated edit. The next triage still shows REMOVED.
5. Change "can delete" to "can’t delete" in a section, then triage: LOAD, `modal: ∅→can't`.
6. With a LOAD change pending, repair the test via `ingest-update --keep-baseline`. The next triage still shows LOAD.
7. In `cmd.exe`, run `chcp`, then `spectra list`, then `chcp` again: the code page is unchanged. Then `spectra ai update-triage --suite s --output-format json > out.json`: the file is UTF-8 and the `→` is intact.
