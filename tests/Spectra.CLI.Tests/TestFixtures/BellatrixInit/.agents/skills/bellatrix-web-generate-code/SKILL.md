---
name: bellatrix-web-generate-code
description: Skill for generating or editing BELLATRIX web test code (page objects, sections, models, tests) using locators validated during live scenario execution.
---

# Generate Web Test Code

Generate production-ready BELLATRIX web test code using the locators validated during scenario execution (`bellatrix-web-execute-scenario`). This skill handles both **creating new files** and **editing/expanding existing ones**.

**Input:** the execution output — validated `{ strategy, selector }` pairs, element → component mapping, page transitions, observed texts, and the waits/obstacles you hit. **Never invent a locator.** Every locator in a Map file must trace to one that resolved against a live snapshot and was exercised with a `bellatrix web` verb. If a needed locator is missing, run a new CLI session for it — do not guess.

**Target project:** the BELLATRIX web test project recorded in `bellatrix-web-project-structure` (written below as `MyProject.WebTests` — substitute the real name).
- **Namespaces**: `MyProject.WebTests.Pages`, `MyProject.WebTests.Sections`, `MyProject.WebTests.Components`, `MyProject.WebTests.Models`, `MyProject.WebTests.Tests`
- **Build command**: `dotnet build MyProject.WebTests/MyProject.WebTests.csproj`
- **Language**: C# with file-scoped namespaces; NUnit + `Bellatrix.Web.NUnit.WebTest` unless the project already uses MSTest

---

## CRITICAL: Read Reference Documentation First

**BEFORE generating any code**, load these skills:

| Skill | Content |
|-------|---------|
| `bellatrix-web-project-structure` | **As-is tree of the test project — ground every CREATE vs EDIT decision here** |
| `bellatrix-web-components` | Component types, methods, factory methods |
| `bellatrix-web-locators-and-waits` | Find strategies, locator ladder, waits |
| `bellatrix-web-validations-and-assertions` | Validate* methods and which component supports which |
| `bellatrix-web-test-structure-and-lifecycle` | Test classes, Browser attribute, lifecycle, hooks |
| `bellatrix-web-page-objects-and-patterns` | Page object structure, sections, models |
| `bellatrix-web-app-services` | App services (Components, Navigation, Browser, Dialogs, …) |
| `bellatrix-web-system-tests-recipes` | Complete test examples |

---

## Step 1: Scan Existing Project Structure

Before generating code, **always check what already exists** — start with `bellatrix-web-project-structure`, then confirm on disk:

```
Existing page objects:   Pages/*/
Existing sections:       Sections/*/
Existing tests:          Tests/**/*
Existing models:         Models/*
Existing components:     Components/*
```

### Decision Matrix

| Situation | Action |
|-----------|--------|
| Page object folder exists for this page | **EDIT** — add new members to the existing partial files |
| Page object folder does NOT exist | **CREATE** — new folder with the 3 partial files |
| A new modal / drawer / wizard step appears | **CREATE** its own page object (or a section when shared) — don't stuff it into an unrelated page |
| Same block + workflow already on ≥2 pages | **CREATE** a section and embed it by composition |
| Test class exists for this feature area | **EDIT** — add new test methods to the existing class |
| Test class does NOT exist | **CREATE** — new test file |
| Model exists with matching properties | **REUSE** as-is |
| Model exists but needs more properties | **EDIT** — add the properties |
| Model does NOT exist | **CREATE** — new model file |

### IMPORTANT: When Editing Existing Files

- **Read the entire file first** before making changes
- **Preserve all existing code** — never remove or rewrite existing members
- **Follow the same patterns** — naming, formatting, locator style, region comments
- **Append** new members after existing ones
- **Keep the partial class name identical** across the page's files
- If an existing locator conflicts with one you just validated live, keep both visible to the user: update it only when the live run proved the old one broken, and say so in the hand-off summary

After CREATING a page folder, section, component, model, or tests sub-folder, update `bellatrix-web-project-structure` in the same change.

---

## Step 2: Generate Page Objects (3-File Pattern)

Each page (or modal / wizard step) gets a dedicated folder with three partial parts:

```
Pages/
  {PageName}/
    {PageName}.Actions.*      — page base class, Url, Open(), workflow methods
    {PageName}.Map.*          — component properties (validated locators only)
    {PageName}.Assertions.*   — Assert* methods built from Validate* methods
```

Name pages after what the user sees: `LoginPage`, `CartPage`, `CheckoutPage`, `AddressModal`.

### Map File (`{PageName}.Map.cs`)

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Pages;

public partial class {PageName}
{
    // PREFERRED — direct factories for single-attribute locators
    public TextField EmailField =>
        App.Components.CreateByCss<TextField>("[data-testid='email']");

    public Select CountrySelect =>
        App.Components.CreateById<Select>("billing_country");

    public TextField UsernameField =>
        App.Components.CreateByCss<TextField>("[name='username']");      // no CreateByName in web

    // XPath for text-based / structural locators (lowercase "p": CreateByXpath)
    public Button SubmitButton =>
        App.Components.CreateByXpath<Button>("//button[normalize-space()='Place order']").ToBeClickable();

    // Dynamic locator — a method, not a property
    public Anchor RemoveLinkFor(string productName) =>
        App.Components.CreateByXpath<Anchor>($"//tr[td[normalize-space()='{productName}']]//a[contains(@class,'remove')]");

    // Collections
    public ComponentsList<Div> ProductCards =>
        App.Components.CreateAllByCss<Div>("[data-testid='product-card']");
}
```

**Locator factory rule:** `[data-testid]` / any CSS → `CreateByCss`; static id → `CreateById`; `name` → `CreateByCss("[name='…']")`; exact link text → `CreateByLinkText`; text/structural → `CreateByXpath`. See `bellatrix-web-locators-and-waits` for the full mapping from a validated CLI `{ strategy, selector }`.

**Component rule:** pick the type from the element recorded during execution (see the HTML → component table in `bellatrix-web-execute-scenario`). Remember `Div`, `Span`, `Label`, `Heading` have no `Click()` — type clickable non-buttons as `Button`.

### Actions File (`{PageName}.Actions.cs`)

```csharp
using Bellatrix.Web;
using MyProject.WebTests.Models;

namespace MyProject.WebTests.Pages;

public partial class {PageName} : WebPage
{
    public override string Url => "https://example.com/checkout/";

    public override void Open()
    {
        App.Navigation.Navigate(Url);
        App.Browser.WaitUntilReady();
    }

    public void FillBillingInfo(BillingInfo billingInfo)
    {
        EmailField.SetText(billingInfo.Email);
        CountrySelect.SelectByText(billingInfo.Country);
    }

    public OrderReceivedPage PlaceOrder()
    {
        SubmitButton.Click();
        App.Navigation.WaitForPartialUrl("/order-received");
        return App.Create<OrderReceivedPage>();
    }
}
```

Every wait you needed during live execution (spinner, AJAX, URL change) belongs in the action method that triggers it.

### Assertions File (`{PageName}.Assertions.cs`)

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Pages;

public partial class {PageName}
{
    public void AssertPageLoaded()
    {
        SubmitButton.ValidateIsVisible();
        EmailField.ValidateIsVisible();
    }

    public void AssertErrorMessageIs(string expected)
    {
        ErrorMessage.ValidateIsVisible();
        ErrorMessage.ValidateInnerTextIs(expected);
    }

    public void AssertOnPage()
    {
        AssertLandedOnPage("/checkout");
    }
}
```

Use the exact texts observed during live execution. `Select` has no inner-text validation — validate `CountrySelect.GetSelected()` instead.

---

## Step 2b: Custom Components (When Built-ins Don't Fit)

If a built-in component is missing a method you need, or misbehaves for a specific widget (custom dropdown, date picker, toast, rich-text editor), add a custom component under `Components/` instead of working around it in page objects or tests. Never modify BELLATRIX source; keep components control-level (business workflows stay in Actions); one file per component.

### Option A — New component inheriting `Component`

```csharp
using Bellatrix.Web;
using Bellatrix.Web.Contracts;

namespace MyProject.WebTests.Components;

public class Toast : Component, IComponentInnerText
{
    public virtual string InnerText => WrappedElement.Text;

    public void Dismiss() => this.CreateByCss<Button>("button[aria-label='Close']").Click();
}
```

### Option B — Child component overriding a built-in

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Components;

public class ScrollingButton : Button
{
    public override void Click()
    {
        ScrollToVisible();
        base.Click();
    }
}
```

### Using custom components in Map files

```csharp
using MyProject.WebTests.Components;

public Toast SuccessToast => App.Components.CreateByCss<Toast>("[data-testid='toast']");
public ScrollingButton PlaceOrderButton => App.Components.CreateById<ScrollingButton>("place_order");
```

Inside a component, locate children with the scoped factories (`this.CreateByCss<T>(…)`, `this.CreateByXpath<T>(".//…")`) or the protected `ComponentCreateService` — never with raw `WrappedDriver.FindElement`.

---

## Step 2c: Reusable Sections (Header, Navigation, Footer, Cookie Banner)

When the same locator block AND workflow appear on two or more pages, extract a section under `Sections/{SectionName}/` with the same 3-part split, and embed it in each page by composition. Keep the public action names on the pages unchanged.

```csharp
// Sections/HeaderSection/HeaderSection.Map.cs
using Bellatrix.Web;

namespace MyProject.WebTests.Sections;

public partial class HeaderSection
{
    public Anchor CartLink => App.Components.CreateByCss<Anchor>("[data-testid='header-cart']");
    public Span CartCount => App.Components.CreateByCss<Span>("[data-testid='cart-count']");
}

// Sections/HeaderSection/HeaderSection.Actions.cs
using Bellatrix.Web;

namespace MyProject.WebTests.Sections;

public partial class HeaderSection : WebSection
{
    public void OpenCart()
    {
        CartLink.Click();
        App.Browser.WaitUntilReady();
    }
}

// Pages/HomePage/HomePage.Map.cs — embed by composition
public HeaderSection Header => new HeaderSection();

// Pages/HomePage/HomePage.Actions.cs — delegate
public void OpenCart() => Header.OpenCart();
```

`WebSection` provides `App` but no `Url`/`Open()`.

---

## Step 3: Generate Model Classes (If Needed)

Place data models in `Models/` for structured form data (billing info, credentials, addresses) and enums for fixed option sets (sort orders, payment methods). Models are pure data — no component types.

```csharp
namespace MyProject.WebTests.Models;

public class BillingInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public bool ShouldCreateAccount { get; set; }
}

public enum ProductSort
{
    Popularity,
    PriceLowToHigh,
    PriceHighToLow,
}
```

Map enum values to visible option texts inside the page object (a `switch` in the action method), not in the model.

---

## Step 4: Generate Test Classes

### Test File Organization (MANDATORY)

Do NOT put all tests into a single file. Organize test classes by **logical feature area** using the test case's tags/component. Each distinct feature area gets its own test class file in its own folder.

**Naming convention:** `Tests/{FeatureArea}/{FeatureArea}Tests.*`

| Tags on the test case | Test class file |
|-----------------------|-----------------|
| `login`, `authentication` | `Tests/Login/LoginTests` |
| `registration`, `sign-up` | `Tests/Registration/RegistrationTests` |
| `cart`, `coupon` | `Tests/Cart/CartTests` |
| `checkout`, `billing`, `payment` | `Tests/Checkout/CheckoutTests` |
| `search`, `filter`, `sorting` | `Tests/Catalog/CatalogTests` |
| `account`, `profile`, `addresses` | `Tests/Account/AccountTests` |
| `navigation`, `header`, `footer` | `Tests/Navigation/NavigationTests` |

**How to decide:** use the most specific *feature* tag (ignore technique tags such as `EP`, `BVA`, `negative-test`, `smoke`). When a test has both a specific tag (`coupon`) and a broader one (`cart`), group by the broader one. When the file already exists, add the new test method to it. Aim for roughly 10–30 tests per file; split by sub-feature beyond ~50.

### Test Class Template

```csharp
using Bellatrix.Web;
using Bellatrix.Web.NUnit;
using MyProject.WebTests.Models;
using MyProject.WebTests.Pages;
using NUnit.Framework;

namespace MyProject.WebTests.Tests;

[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]   // default for generated classes — speed optimization
public class CheckoutTests : WebTest
{
    [Test]
    [ManualTestCase("TC-210")]
    [Category("Smoke")]
    public void PlaceOrder_WithValidBillingInfo_ShowsOrderReceived()
    {
        var checkoutPage = App.GoTo<CheckoutPage>();
        checkoutPage.AssertPageLoaded();

        checkoutPage.FillBillingInfo(new BillingInfo
        {
            FirstName = "In",
            LastName = "Deepthought",
            Email = "info@example.com",
            Country = "Bulgaria",
        });
        var orderReceivedPage = checkoutPage.PlaceOrder();

        orderReceivedPage.AssertOrderReceived();
    }
}
```

- `App.GoTo<TPage>()` opens the page; `App.Create<TPage>()` wraps a page already displayed.
- `[ManualTestCase("TC-XXX")]` only when the test automates a SPECTRA case (see `bellatrix-web-automate-spectra-test`).
- Lifecycle: class default `Lifecycle.ReuseIfStarted`; switch a class or a single method to `Lifecycle.RestartEveryTime` when the test logs in, changes account/cart state, or needs a clean browser — and comment why.

### One Test Case = One Test Method (MANDATORY)

Every SPECTRA test case MUST produce exactly ONE test method. Do NOT combine multiple test cases into one parameterized method, and do NOT stack multiple TC-ID traceability markers on one method.

### Tests Use Page Objects Only

Test methods call page-object and section methods only — no direct component creation in tests. Every test starts from a known state (open the page, or reset state in setup) so it can run alone and in any order.

---

## Code Rules (MUST FOLLOW)

### Syntax — Avoid Build Errors
1. **File-scoped namespaces**: `namespace X;` (NOT `namespace X { }`)
2. **Partial classes**: all page-object and section files use `public partial class`
3. **`WebPage` base** (non-generic) on the Actions part; sections use `WebSection`. NOT `DesktopPage`, NOT a generic WebPage with type arguments, NOT the obsolete `NavigatablePage`/`AssertedPage`
4. **`override`** on `Url` (`public override string Url => "…";`) and `Open()`
5. **`CreateByXpath`** — lowercase **p** (`CreateByXPath` does not exist in web); `CreateAllByXpath` likewise
6. **No `CreateByDataTestId` / `CreateByName`** — use `CreateByCss("[data-testid='…']")` / `CreateByCss("[name='…']")`
7. **Validate methods**: native `Validate*` (`ValidateIsVisible()`, `ValidateInnerTextIs()`, `ValidateInnerTextContains()`, `ValidateValueIs()`, `ValidateIsChecked()`, …) instead of `Assert.That(...)` — and only on components that support them (`Select`/`Password` have no `ValidateInnerText*`)
8. **`using Bellatrix.Web;`** in every page, section, component, and test file

### Architecture
9. **Page instantiation**: `App.Create<TPage>()` / `App.GoTo<TPage>()` (NOT `new TPage()`)
10. **Lifecycle**: generated test classes default to `[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]`; the global config stays `restart every time`. Override per class or per method with `Lifecycle.RestartEveryTime` when state (auth cookies, cart, local storage) would leak — see the generator agent for the full policy
11. **No direct components in tests**: tests use only page-object and section methods
12. **Generic methods for dynamic elements**: parameters for product names, row ids, menu texts
13. **Navigation**: `App.Navigation.Navigate(url)` (NOT `App.Browser.NavigateToUrl`)
14. **Never raw Selenium** (`WrappedDriver.FindElement(s)`, `By.*`, `WebDriverWait`, `Thread.Sleep`) in test-project code — BELLATRIX factories and waits only

### Always Brace Control Flow (MANDATORY)

Every `if`, `else`, `for`, `foreach`, `while`, `do`, and `using` block MUST use `{ }` braces — even for single-statement bodies. Braces on their own line (Allman style).

```csharp
// WRONG
foreach (var card in ProductCards)
{
    if (!card.IsVisible) continue;
    card.ValidateInnerTextContains("€");
}

// CORRECT
foreach (var card in ProductCards)
{
    if (!card.IsVisible)
    {
        continue;
    }

    card.ValidateInnerTextContains("€");
}
```

Applies everywhere — page objects, actions, assertions, sections, tests, custom components, helpers.

### Common Mistakes to Avoid

| WRONG | CORRECT |
|-------|---------|
| `App.Components.CreateByXPath<T>()` | `App.Components.CreateByXpath<T>()` (lowercase p) |
| `App.Components.CreateByDataTestId<T>("x")` | `App.Components.CreateByCss<T>("[data-testid='x']")` |
| `App.Browser.NavigateToUrl(url)` | `App.Navigation.Navigate(url)` |
| `new LoginPage()` | `App.Create<LoginPage>()` |
| `public string Url => …` | `public override string Url => …` |
| `Assert.That(el.IsVisible, Is.True)` | `el.ValidateIsVisible()` |
| `countrySelect.ValidateInnerTextIs("X")` | `countrySelect.GetSelected().ValidateInnerTextIs("X")` |
| `Assert.That(App.Browser.Url, Does.Contain("/x"))` | `Assert.That(App.Browser.Url.ToString(), Does.Contain("/x"))` or `page.AssertLandedOnPage("/x")` |
| `//Button[@Name='OK']` (desktop syntax) | `//button[normalize-space()='OK']` |
| `namespace X { }` | `namespace X;` |
| `Thread.Sleep(2000)` | `ToBeVisible()` / `App.Browser.WaitForAjax()` |

---

## Step 5: Verify Build

After generating/editing all files, build the test project and fix BUILD errors until it succeeds.

```bash
dotnet build MyProject.WebTests/MyProject.WebTests.csproj
```

Common build errors:
- Missing `using Bellatrix.Web;` (components, factories, `Validate*` all live there)
- `CreateByXPath` / `CreateByName` / `CreateByDataTestId` — these do not exist in web
- `Validate*` on a component without that capability (e.g. `Select.ValidateInnerTextIs`)
- Missing `override` on `Url` / `Open()`, or two parts declaring different base classes
- Namespace mismatch between the page parts or with the test's `using`

The command the user runs afterwards (include it in the hand-off; do NOT run it yourself):

```bash
dotnet test MyProject.WebTests/MyProject.WebTests.csproj --filter "FullyQualifiedName~{TestMethodName}" --no-build --logger "console;verbosity=detailed"
```

---

## Step 6: Hand Off to User — Do NOT Run Tests Yourself

After the build succeeds, **STOP**. Do NOT run the generated tests. The user runs tests and returns with feedback.

Post a concise summary that includes:
- Files created/edited
- TC-IDs covered (if applicable)
- The command the user can run to execute the new test(s) (see Step 5)
- Any assumptions you made (locator choices, lifecycle picked, waits added, custom components or sections added)

Then wait. When the user returns with specific failures, make targeted fixes:
- **"Locator failed for Y"** → new CLI session, fresh snapshot, re-resolve via the locator specialist, fix the Map file
- **"Assertion expected X got Y"** → re-observe the text live if needed, fix the assertion
- **"Timing issue on Z"** → add the appropriate wait

After each fix iteration, rebuild, then hand back. Do NOT speculatively rerun tests on your own.

**Exception:** if the user has explicitly said "run them and fix until green" in this conversation, follow that instruction for as long as it applies.

---

## Output Format

### Files Created/Modified

```
[CREATED] Pages/{PageName}/{PageName}.Map.cs
[CREATED] Pages/{PageName}/{PageName}.Actions.cs
[CREATED] Pages/{PageName}/{PageName}.Assertions.cs
[EDITED]  Pages/{ExistingPage}/{ExistingPage}.Map.cs — added 3 new properties
[EDITED]  Pages/{ExistingPage}/{ExistingPage}.Actions.cs — added 2 new methods
[CREATED] Sections/{SectionName}/{SectionName}.Map.cs (+ Actions, Assertions)
[CREATED] Models/{ModelName}.cs
[CREATED] Tests/{FeatureArea}/{FeatureArea}Tests.cs
[EDITED]  bellatrix-web-project-structure skill — added {PageName} to the Pages table
```

### Build Verification
```
Build succeeded: dotnet build MyProject.WebTests/MyProject.WebTests.csproj
```
