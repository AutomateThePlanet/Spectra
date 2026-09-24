# Contract: source_refs resolution (Spec 078)

`SourceRefResolver(repoRoot, sourceDir).Resolve(refs)` is model-free and deterministic. It never throws. Its only I/O is
`File.Exists`.

## Rules, applied per ref in order

1. `raw.Split('#')[0].Trim()`, then replace `\` with `/`. If the result is empty → **Missing(Empty)**.
2. If the result is fully qualified (e.g. `C:/…`, or `/…` on Unix) and its full path is under `repoRoot`, and the file
   exists → **Resolved**.
3. `repoRoot / result.TrimStart('/')`: if the file exists and the path is under `repoRoot` → **Resolved**.
4. `sourceDir / result.TrimStart('/')`: if the file exists and the path is under `repoRoot` → **Resolved**. This is the
   legacy fallback.
5. If every candidate lies outside `repoRoot` → **Missing(OutsideRepository)**. Otherwise → **Missing(NotFound)**.

"Under `repoRoot`" is decided by `Path.GetFullPath(candidate)` starting with `GetFullPath(repoRoot) + separator`.
`RelativePath` is repo-relative and uses `/`.

## Examples

These assume `repoRoot = /r`, `sourceDir = /r/docs`, and that the file `/r/docs/a.md` exists.

| Ref | Result |
|---|---|
| `docs/a.md#Section-X` | Resolved → `docs/a.md` (rule 3) |
| `docs\a.md` | Resolved → `docs/a.md` (rule 3) |
| `/docs/a.md` | Resolved → `docs/a.md` (rule 3; on Unix rule 2 fails first because `/docs/a.md` is not under `/r`) |
| `/r/docs/a.md` (fully qualified) | Resolved → `docs/a.md` (rule 2) |
| `a.md` | Resolved → `docs/a.md` (rule 4, legacy) |
| `a.md#Intro` | Resolved → `docs/a.md` (rule 4) |
| `#Only-Fragment`, `""`, `"  "` | Missing(Empty) |
| `../outside.md` | Missing(OutsideRepository) |
| `docs/gone.md` | Missing(NotFound) |

When both `/r/a.md` and `/r/docs/a.md` exist, `a.md` resolves to `/r/a.md`: rule 3 runs before rule 4.

## Consumers

| Consumer | On Missing |
|---|---|
| `spectra ai update` (`UpdateHandler` → `TestClassifier`) | Ignored per ref. A test with **no** resolved ref is classified ORPHANED "Source document not found or empty". A test is classified against the concatenated distinct contents of **all** its resolved refs. |
| `compile-critic-prompt` (with no `--docs`) | Fails loud: `Source ref not found: {ref}` on stderr, exit 1 (unchanged message and code). |
| `compile-repair-prompt` | Fails loud (unchanged). |
| `compile-repair-batch` | Skips (lenient, unchanged). |
