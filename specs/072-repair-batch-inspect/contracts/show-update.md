# Contract: `spectra show {id}` — `file` field addition

**Spec 072 FR3** | One-line change | `ShowResult.TestDetail` + `ShowHandler`

---

## Change

Add `file` (working-dir-relative path to the test `.md`) to `TestDetail` in the JSON output of `spectra show {id} --output-format json`.

---

## Before (existing output)

```json
{
  "command": "show",
  "status": "success",
  "test": {
    "id": "TC-100",
    "title": "Verify meter to feet conversion",
    "priority": "high",
    "suite": "unit-converter",
    "component": "UnitConverter",
    "tags": ["conversion", "metric"],
    "source_refs": ["docs/unit-converter.md"],
    "steps": ["Navigate to converter", "Enter 1 in Meter field"],
    "expected_results": ["Bottom output shows approximately 3.28084"]
  }
}
```

## After (with file field)

```json
{
  "command": "show",
  "status": "success",
  "test": {
    "id": "TC-100",
    "title": "Verify meter to feet conversion",
    "priority": "high",
    "suite": "unit-converter",
    "file": "test-cases/unit-converter/TC-100.md",
    "component": "UnitConverter",
    "tags": ["conversion", "metric"],
    "source_refs": ["docs/unit-converter.md"],
    "steps": ["Navigate to converter", "Enter 1 in Meter field"],
    "expected_results": ["Bottom output shows approximately 3.28084"]
  }
}
```

---

## Implementation

**`ShowResult.cs`** — add one property to `TestDetail`:
```csharp
[JsonPropertyName("file")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? File { get; init; }
```

**`ShowHandler.cs`** — `DisplayTestAsync` gains a `string? filePath` parameter:
- Caller (`ExecuteAsync`) passes `Path.GetRelativePath(basePath, testPath)`
- `TestDetail` initialization adds `File = filePath`

Human-readable output (`--output-format human`) is unchanged.

---

## Test contract

```
ShowHandlerFileFieldTests:
  JsonOutput_IncludesFileField → test.file != null and ends with ".md"
  FileField_IsRelativeToWorkingDirectory → does not start with drive letter or "/"
  HumanOutput_Unchanged → stdout does not contain "file:" line
  FileField_NullWhenNotFound → not applicable (error result returned instead)
```
