# Quickstart: verifying Spec 080

Every review item has a regression test. To see it fail before the fix, run the new test on commit `1ba172a` (or
stash the fix) and confirm it fails. Then confirm it passes on the fix commit.

```bash
dotnet test --filter "FullyQualifiedName~UpdateHandlerFilePathTests"   # A1
dotnet test --filter "FullyQualifiedName~SourceRefResolverTests"       # A2
dotnet test --filter "FullyQualifiedName~FrontmatterKeysGuardTests"    # A3
dotnet test --filter "FullyQualifiedName~TestCaseLayoutTests"          # A4
dotnet test --filter "FullyQualifiedName~ConfigLoader"                 # A5
dotnet test --filter "FullyQualifiedName~SharedCriteriaFileTests"      # B1–B3
dotnet test --filter "FullyQualifiedName~CriteriaChangesetTests"       # B4
dotnet test --filter "FullyQualifiedName~SkillsManifestTests"          # B5
dotnet test --filter "FullyQualifiedName~IngestCriteriaIdentityTests"  # B6
dotnet test --filter "FullyQualifiedName~CompileExtractionPromptExistingTests"  # B7
dotnet test --filter "FullyQualifiedName~CriteriaScanTests"            # B8
dotnet test --filter "FullyQualifiedName~TextNormalizerTests"          # B9
```

Manual spot checks in a scratch workspace:

1. **A1.** Rename `TC-100.md` to `TC-100-login.md`, delete its only doc, and run
   `spectra ai update <suite> --no-review`. Only `TC-100-login.md` changes.
2. **A2.** Set `"source": {"local_dir": "../product-docs/docs"}` and run `spectra ai update <suite> --dry-run`. There
   are no "source not found" orphans.
3. **A5.** Break `spectra.config.json` (a trailing brace) and run `spectra docs changed` and
   `spectra ai ingest-criteria …`. Both exit 1 with `spectra.config.json is invalid: …`.
