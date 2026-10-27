---
name: bellatrix-web-project-structure
description: As-is snapshot of the BELLATRIX web test project tree plus the CREATE-vs-EDIT decision table. Ground every CREATE vs EDIT decision on it and refresh it whenever a Pages/, Sections/, Components/, Models/, or Tests/ entry is added.
---

# MyProject.WebTests — Current Project Structure

This file is the authoritative as-is snapshot of what exists in the web test project. Use it to decide whether to CREATE a new file/folder or EDIT an existing one. It is NOT aspirational — only add an entry after the folder/file is actually on disk.

> **First use:** replace `MyProject.WebTests` with the real project name/path and fill the tables from the actual tree (search `Pages/*/`, `Sections/*/`, `Components/*.cs`, `Models/*.cs`, `Tests/**/*.cs`).
>
> **Maintenance rule:** whenever you add a page object folder, section, custom component, model, or tests sub-folder during a generation workflow, update the matching table here in the same change. The agent owns keeping this tree accurate.

---

## Top-Level Layout

```
MyProject.WebTests/
├─ MyProject.WebTests.csproj
├─ TestsInitialize.cs                 # NUnit [SetUpFixture] — disposes the BELLATRIX App after the run
├─ ManualTestCaseAttribute.cs         # [ManualTestCase("TC-XXX")] for SPECTRA traceability
├─ testFrameworkSettings.Debug.json   # webSettings — browser, lifecycle, timeouts, (grid) url
├─ Components/                        # Custom BELLATRIX components (overrides / new)
├─ Models/                            # Data classes and enums used by page objects
├─ Pages/{PageName}/                  # Page objects — 3-file partial class pattern
├─ Sections/{SectionName}/            # Reusable page sections (WebSection) — header, footer, nav
└─ Tests/{FeatureArea}/               # Test classes grouped by feature area
```

### Key facts
- **Base URL:** `https://example.com` (update to the system under test)
- **Browser / driver:** local browser (`executionType: regular`) or a Selenium Grid via `webSettings.executionSettings.url`
- **Global lifecycle (config):** `restart every time`
- **Class-level lifecycle (generated classes default):** `Lifecycle.ReuseIfStarted` — overridden per class/test when state must not leak
- **Page base class:** `WebPage` (non-generic); **section base class:** `WebSection`; **test base class:** `WebTest` (`Bellatrix.Web.NUnit`)
- **Locator preference:** `data-testid` → static id → name → aria-label → label/placeholder → tag+text → structural CSS → positional XPath

### Required project wiring

```xml
<PropertyGroup>
  <!-- Match the TargetFramework of the Bellatrix.Web you reference (net10.0 for current BELLATRIX source). -->
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings> <!-- if disabled, add `using System;` / `using System.Linq;` yourself -->
  <IsPackable>false</IsPackable>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="…" />
  <PackageReference Include="NUnit" Version="…" />
  <PackageReference Include="NUnit3TestAdapter" Version="…" />
  <PackageReference Include="Unity" Version="…" /> <!-- BELLATRIX's DI container; the GettingStarted samples reference it -->
  <!-- plus a reference to Bellatrix.Web: NuGet package or ProjectReference to Bellatrix.Web.csproj (+ Bellatrix.Core) -->
</ItemGroup>

<ItemGroup>
  <None Update="testFrameworkSettings.$(Configuration).json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### `TestsInitialize.cs`

```csharp
using Bellatrix;
using Bellatrix.Web;
using NUnit.Framework;

[SetUpFixture]
public class TestsInitialize
{
    [OneTimeTearDown]
    public void AssemblyCleanUp()
    {
        var app = ServicesCollection.Current.Resolve<App>();
        app?.Dispose();
    }
}
```

### `ManualTestCaseAttribute.cs`

```csharp
using System;

namespace MyProject.WebTests;

/// <summary>
/// Links an automated test to a SPECTRA manual test case for traceability.
/// Unlike NUnit's [TestCase], this attribute does not affect test parameterization —
/// it is purely a metadata marker used by SPECTRA coverage scanning.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ManualTestCaseAttribute : Attribute
{
    public string TestCaseId { get; }

    public ManualTestCaseAttribute(string testCaseId)
    {
        TestCaseId = testCaseId;
    }
}
```

Test classes in `MyProject.WebTests.Tests` (or deeper) see it without a `using`, because it lives in the enclosing namespace.

---

## `Pages/` — Page Objects (3-file partial class pattern)

Each page gets its own folder with `.Map.cs` (locators), `.Actions.cs` (`: WebPage`, `Url`, `Open()`, workflows), `.Assertions.cs` (`Assert*` methods using `Validate*`).

| Folder | URL | Purpose |
|--------|-----|---------|
| _(none yet — add a row when you create a page)_ | | |

**Namespace:** `MyProject.WebTests.Pages`
**Base class:** `WebPage`

---

## `Sections/` — Reusable Page Sections

Sections are 3-file partial classes inheriting `WebSection` (NOT `WebPage`) that group locators and workflows shared by several pages (header, navigation, footer, cookie banner, mini-cart). Pages embed them via a property on the Map partial — `public HeaderSection Header => new HeaderSection();` — and delegate from their action methods.

| Folder | Used by | Purpose |
|--------|---------|---------|
| _(none yet)_ | | |

**Namespace:** `MyProject.WebTests.Sections`
**When to CREATE a new section:** when two or more pages expose the same locator set AND the same workflow around it. A single-page duplicate stays on the page as a region.

---

## `Components/` — Custom Components

Custom BELLATRIX components live here when a built-in lacks behavior or needs an override. New component = inherit `Bellatrix.Web.Component`; behavior tweak = inherit an existing component (e.g. `Button`) and override its virtual members.

| File | Extends | Why |
|------|---------|-----|
| _(none yet)_ | | |

**Namespace:** `MyProject.WebTests.Components`
**Usage in Map files:** `App.Components.CreateByCss<MyComponent>("[data-testid='…']")`.

---

## `Models/` — Data Models and Enums

Plain data classes and enums shared across page objects. No component or BELLATRIX types here — pure data.

| File | Purpose |
|------|---------|
| _(none yet)_ | |

**Namespace:** `MyProject.WebTests.Models`

---

## `Tests/` — Test Classes (grouped by feature area)

One file per feature area. One test method per SPECTRA TC. Every traced method carries `[ManualTestCase("TC-XXX")]`.

| Folder | File | Feature area |
|--------|------|--------------|
| _(none yet)_ | | |

**Namespace:** `MyProject.WebTests.Tests` (or nested by folder — check an existing file before deciding)

---

## CREATE vs EDIT Decision Table

| Situation | Action | Location |
|-----------|--------|----------|
| Test case targets a page NOT in the Pages table above | **CREATE** `Pages/{PageName}/` with 3 partial files | `Pages/` |
| Test case targets an existing page | **EDIT** that page's Map/Actions/Assertions; append new members | `Pages/{PageName}/` |
| A modal / drawer / wizard step with its own locators | **CREATE** its own page object (or a section if it appears on several pages) | `Pages/` or `Sections/` |
| Test case's feature area is NOT in the Tests table above | **CREATE** `Tests/{FeatureArea}/{FeatureArea}Tests.cs` | `Tests/` |
| Test case's feature area exists | **EDIT** — add a new `[Test]` method to the existing class | `Tests/{FeatureArea}/` |
| Need a behavior a BELLATRIX built-in doesn't provide | **CREATE** custom component inheriting `Component` or an existing component | `Components/` |
| Same locator block + workflow duplicated across ≥2 pages | **CREATE** a `WebSection` and embed it via composition on each page | `Sections/` |
| Need a shared enum / data class | **EDIT** an existing model if it fits; otherwise **CREATE** a new file | `Models/` |

---

## Sync

Your agent's instructions may reference this skill. When you add a new folder, page, section, component, model, or test class, ALSO update this skill's tables in the same change.
