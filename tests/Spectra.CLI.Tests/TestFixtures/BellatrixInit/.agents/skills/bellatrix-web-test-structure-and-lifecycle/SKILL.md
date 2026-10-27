---
name: bellatrix-web-test-structure-and-lifecycle
description: Reference skill for BELLATRIX web test classes, the App object, the Browser attribute and lifecycle configuration, setup hooks, and naming conventions.
---

# BELLATRIX Web Test Structure and Lifecycle Reference

## Test Base Classes

### NUnit (default)

```csharp
using Bellatrix.Web;
using Bellatrix.Web.NUnit;
using NUnit.Framework;

namespace MyProject.WebTests.Tests;

[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]
public class LoginTests : WebTest
{
    [Test]
    [Category("Smoke")]
    public void Login_WithValidCredentials_OpensDashboard()
    {
        // Test code here
    }
}
```

### MSTest

```csharp
using Bellatrix.Web;
using Bellatrix.Web.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyProject.WebTests.Tests;

[TestClass]
[Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]
public class LoginTests : WebTest
{
    [TestMethod]
    [TestCategory("Smoke")]
    public void Login_WithValidCredentials_OpensDashboard()
    {
        // Test code here
    }
}
```

`WebTest` exists in both `Bellatrix.Web.NUnit` and `Bellatrix.Web.MSTest` — import exactly ONE of them. `BrowserAttribute`, `BrowserType`, and `Lifecycle` live in `Bellatrix.Web`.

---

## The App Object

`App` (type `Bellatrix.Web.App`) is the entry point for all web interactions. It is a property of `WebTest` in test classes and of `WebPage` / `WebSection` in page objects and sections.

| Service | Access | Purpose |
|---------|--------|---------|
| Components | `App.Components` | Create and find components (`CreateByCss`, `CreateById`, `CreateByXpath`, …) |
| Navigation | `App.Navigation` | `Navigate(url)`, `WaitForPartialUrl(…)`, `NavigateToLocalPage(…)` |
| Browser | `App.Browser` | `Url`, `Title`, `Back/Forward/Refresh`, page-level waits, frames, tabs, storage |
| JavaScript | `App.JavaScript` | Execute scripts |
| Dialogs | `App.Dialogs` | Native alert / confirm / prompt |
| Cookies | `App.Cookies` | Add / read / delete cookies |
| Interactions | `App.Interactions` | Hover chains, drag-and-drop, keyboard chords |
| Assert | `App.Assert` | Framework-neutral assertions |
| Create | `App.Create<TPage>()` | Page object instance (no navigation) |
| GoTo | `App.GoTo<TPage>()` | Page object instance + `Open()` |

There is no `App.Wait` or `App.AppService` in web. See `bellatrix-web-app-services`.

---

## Browser Attribute and Lifecycle

```csharp
[Browser(BrowserType browser, Lifecycle lifecycle = Lifecycle.NotSet)]
[Browser(BrowserType browser, int width, int height, Lifecycle behavior = Lifecycle.NotSet)]
```

```csharp
[Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]
[Browser(BrowserType.Firefox, Lifecycle.ReuseIfStarted)]
[Browser(BrowserType.Edge, Lifecycle.RestartOnFail)]
[Browser(BrowserType.ChromeHeadless, Lifecycle.RestartEveryTime)]
[Browser(BrowserType.Chrome, 1280, 800, Lifecycle.ReuseIfStarted)]   // fixed window size
```

`BrowserType`: `Chrome`, `ChromeHeadless`, `Firefox`, `FirefoxHeadless`, `Edge`, `EdgeHeadless`, `Safari`, `Opera`, `InternetExplorer`.

| Lifecycle | Behavior |
|-----------|----------|
| `Lifecycle.RestartEveryTime` | A new browser for every test — fresh cookies, cache, storage (cleanest, slowest) |
| `Lifecycle.ReuseIfStarted` | Reuses the running browser unless the browser configuration changes (type, window size, execution type, capture-traffic/disable-JS/scroll flags) (fastest; cookies/storage carry over) |
| `Lifecycle.RestartOnFail` | Restarts only after a failed test |

The attribute on the class applies to all its tests; the attribute on a single test method overrides the class for that test only:

```csharp
[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]          // class default — fast
public class CatalogTests : WebTest
{
    [Test]
    public void SortByPrice_LowToHigh_OrdersProducts() { /* ... */ }

    // RestartEveryTime: this test logs in and must not leak the auth cookie
    [Test]
    [Browser(BrowserType.Chrome, Lifecycle.RestartEveryTime)]
    public void Login_WithValidCredentials_ShowsAccountMenu() { /* ... */ }
}
```

### Config-Based Default

Without an attribute, BELLATRIX uses `webSettings.executionSettings` in `testFrameworkSettings.<Configuration>.json`:

```json
{
  "webSettings": {
    "executionSettings": {
      "executionType": "regular",
      "defaultBrowser": "chrome",
      "defaultLifeCycle": "restart every time",
      "url": "http://127.0.0.1:4444/wd/hub"
    }
  }
}
```

---

## Setup and Teardown Hooks

Use the BELLATRIX overrides (they run inside the browser lifecycle) or plain NUnit attributes:

```csharp
public class CartTests : WebTest
{
    private CartPage _cartPage;

    // Runs before each test, after the browser is ready
    public override void TestInit()
    {
        _cartPage = App.Create<CartPage>();
    }

    // Runs after each test
    public override void TestCleanup()
    {
        App.Cookies.DeleteAllCookies();
    }
}
```

Also available: `TestsArrange()` / `TestsAct()` (once per class) and `TestsCleanup()`. An NUnit `[SetUp]` method in the test class works too.

### Assembly Bootstrap — `TestsInitialize.cs`

One `[SetUpFixture]` per test project disposes the browser after the run:

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

---

## Test Naming Convention

Use the pattern `Action_Condition_ExpectedResult`:

```csharp
// Good
public void Login_WithValidCredentials_OpensDashboard()
public void ApplyCoupon_WithExpiredCode_ShowsErrorMessage()
public void AddToCart_WhenOutOfStock_DisablesButton()

// Bad
public void TestLogin()
public void Test1()
public void VerifyCartWorks()
```

---

## Test Categories

```csharp
[Category("Smoke")]       // critical path, every build
[Category("Regression")]  // full suite
[Category("CI")]          // fast, stable tests for CI pipelines
```

MSTest: `[TestCategory("Smoke")]`.

---

## Complete Test Class Template

```csharp
using Bellatrix.Web;
using Bellatrix.Web.NUnit;
using MyProject.WebTests.Models;
using MyProject.WebTests.Pages;
using NUnit.Framework;

namespace MyProject.WebTests.Tests;

[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]
public class CheckoutTests : WebTest
{
    [Test]
    [Category("Smoke")]
    public void ApplyCoupon_WithValidCode_ReducesTotal()
    {
        var homePage = App.GoTo<HomePage>();
        homePage.AddProductById(28);
        homePage.ViewCart();

        var cartPage = App.Create<CartPage>();
        cartPage.ApplyCoupon("happybirthday");

        cartPage.AssertCouponApplied();
        cartPage.AssertTotalPrice("54.00");
    }

    [Test]
    [Category("Regression")]
    public void PlaceOrder_WithValidBillingInfo_ShowsOrderReceived()
    {
        var checkoutPage = App.GoTo<CheckoutPage>();
        checkoutPage.FillBillingInfo(new BillingInfo
        {
            FirstName = "In",
            LastName = "Deepthought",
            Country = "Bulgaria",
            Phone = "+00359894646464",
            Email = "info@example.com",
        });
        checkoutPage.PlaceOrder();

        checkoutPage.AssertOrderReceived();
    }
}
```

Tests contain only page-object calls — no `App.Components` in test methods.

---

## Required Using Directives

```csharp
// Core: components, factories, waits, Validate*, Browser attribute, enums
using Bellatrix.Web;

// Test base class (choose ONE)
using Bellatrix.Web.NUnit;     // NUnit WebTest
using Bellatrix.Web.MSTest;    // MSTest WebTest

// Test attributes (choose ONE)
using NUnit.Framework;                               // [TestFixture], [Test], [Category]
using Microsoft.VisualStudio.TestTools.UnitTesting;  // [TestClass], [TestMethod], [TestCategory]

// Project
using MyProject.WebTests.Pages;
using MyProject.WebTests.Models;
```
