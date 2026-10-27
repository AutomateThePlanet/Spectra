---
name: bellatrix-web-app-services
description: Reference skill for BELLATRIX web App services — Components, Navigation, Browser, JavaScript, Dialogs, Cookies, Interactions, page object factories, and configuration.
---

# BELLATRIX Web App Services Reference

The `App` object (`Bellatrix.Web.App`) is the entry point to every web service. Tests get it from the `WebTest` base class (`App` property); page objects (`WebPage`) and sections (`WebSection`) expose the same `App` property.

## Common Mistakes

| WRONG | CORRECT |
|-------|---------|
| `App.Browser.NavigateToUrl(url)` | `App.Navigation.Navigate(url)` |
| `new LoginPage()` | `App.Create<LoginPage>()` (or `App.GoTo<LoginPage>()` to also open it) |
| `App.Components.CreateByXPath<T>()` | `App.Components.CreateByXpath<T>()` (web is lowercase **p**) |
| `App.Components.CreateByDataTestId<T>("x")` | `App.Components.CreateByCss<T>("[data-testid='x']")` |
| `App.Components.CreateByName<T>("x")` | `App.Components.CreateByCss<T>("[name='x']")` |
| `App.Wait.…` | Web `App` has no `Wait` service — use fluent waits on components (`ToBeVisible()`, …) and `App.Browser.Wait…` |
| `App.AppService.…` | Desktop only — web uses `App.Browser` |
| `App.Browser.TakeScreenshot()` | Not on the browser service — screenshots on failure are automatic (`screenshotsSettings`); for an element use `component.TakeScreenshot()` |
| `Assert.That(App.Browser.Url, Does.Contain("/cart"))` | `App.Browser.Url` is a `Uri` — use `App.Browser.Url.ToString()` (or `page.AssertLandedOnPage("/cart")`) |

---

## ComponentCreateService (`App.Components`)

### Single Component Creation

```csharp
App.Components.CreateByCss<Button>("[data-testid='login']")          // PREFERRED (data-testid)
App.Components.CreateById<TextField>("email")                        // static id
App.Components.CreateByCss<TextField>("[name='username']")           // name attribute
App.Components.CreateByXpath<Button>("//button[normalize-space()='Login']")
App.Components.CreateByLinkText<Anchor>("Sign in")
App.Components.CreateByLinkTextContaining<Anchor>("Sign")
App.Components.CreateByClass<Div>("alert-danger")
App.Components.CreateByClassContaining<Div>("woocommerce-message")
App.Components.CreateByTag<Heading>("h1")
App.Components.CreateByInnerTextContaining<Heading>("Billing details")
App.Components.CreateByAttributesContaining<Anchor>("data-product_id", "28")
App.Components.CreateByValueContaining<Button>("Apply coupon")
App.Components.CreateByIdContaining<TextField>("email")
App.Components.CreateByIdEndingWith<TextField>("_email")
App.Components.CreateByNameEndingWith<Select>("orderby")
```

### Collection Creation (returns `ComponentsList<T>`)

```csharp
App.Components.CreateAllByCss<Anchor>("nav a")
App.Components.CreateAllByXpath<Div>("//div[@data-testid='product-card']")
App.Components.CreateAllByClassContaining<Number>("input-text qty text")
App.Components.CreateAllByTag<Heading>("h2")
App.Components.CreateAllByInnerTextContaining<Anchor>("Read more")
```

Every single-element factory takes an optional `shouldCacheElement` flag (the `CreateAllBy*` collections call it `shouldCacheFoundElements`; default `false` — the element is re-located on each use, which is what you want for dynamic pages).

---

## NavigationService (`App.Navigation`)

| Member | Purpose |
|--------|---------|
| `Navigate(string url)` / `Navigate(Uri uri)` | Open a URL |
| `NavigateToLocalPage(string filePath)` | Open a local HTML file copied to the output folder |
| `WaitForPartialUrl(string partialUrl)` | Wait until the current URL contains the fragment |
| `GetQueryParameter(string name)` | Read a query-string parameter of the current URL |

```csharp
App.Navigation.Navigate("https://example.com/login");
App.Navigation.WaitForPartialUrl("/dashboard");
var orderId = App.Navigation.GetQueryParameter("orderId");
```

---

## BrowserService (`App.Browser`)

| Member | Returns | Purpose |
|--------|---------|---------|
| `Url` | `Uri` | Current URL |
| `Title` | `string` | Page title |
| `HtmlSource` | `string` | Full page source (debugging only) |
| `Back()` / `Forward()` / `Refresh()` | `void` | History navigation |
| `Maximize()` | `void` | Maximize the window |
| `WaitUntilReady()` | `void` | Wait for the document to be ready |
| `WaitForAjax()` | `void` | Wait until no XMLHttpRequests are open |
| `WaitForAjaxRequest(string partialUrl, int additionalTimeoutInSeconds = 0)` | `void` | Wait for a specific request |
| `WaitForJavaScriptAnimations()` | `void` | Wait for animations to finish |
| `WaitForAngular()` | `void` | Wait for Angular to stabilize |
| `WaitForUserInteraction(int timeoutMilliseconds = 1000)` | `void` | Short pause for UI reaction (prefer a real wait) |
| `SwitchToFrame(Frame frame)` / `SwitchToDefault()` | `void` | Enter / leave an iframe |
| `SwitchToLastTab()` / `SwitchToFirstBrowserTab()` / `SwitchToTab(string handle)` | `void` | Tabs and windows |
| `ClearLocalStorage()` / `ClearSessionStorage()` | `void` | Reset client-side state |
| `GetConsoleOutput()` / `PrintConsoleOutput()` | | Browser console logs |

```csharp
App.Browser.WaitUntilReady();
Assert.That(App.Browser.Title, Is.EqualTo("Cart – Bellatrix Demos"));
Assert.That(App.Browser.Url.ToString(), Does.Contain("/cart"));

var frame = App.Components.CreateById<Frame>("payment-frame");
App.Browser.SwitchToFrame(frame);
App.Components.CreateByCss<TextField>("[name='cardnumber']").SetText("4242424242424242");
App.Browser.SwitchToDefault();
```

---

## Other Services

### JavaScriptService (`App.JavaScript`)

```csharp
var height = App.JavaScript.Execute("return document.body.scrollHeight;");     // object
App.JavaScript.Execute("arguments[0].scrollIntoView();", component);            // with a component
```

### DialogService (`App.Dialogs`) — native `alert` / `confirm` / `prompt`

```csharp
var text = App.Dialogs.GetText();
App.Dialogs.Handle();                                        // press OK
App.Dialogs.Handle(dialogButton: DialogButton.Cancel);       // press Cancel
App.Dialogs.Handle(alert => alertText = alert.Text);         // inspect, then OK
```

### CookiesService (`App.Cookies`)

```csharp
App.Cookies.AddCookie("cookie_consent", "accepted");
var value = App.Cookies.GetCookie("session");
App.Cookies.DeleteCookie("cart");
App.Cookies.DeleteAllCookies();
```

### InteractionsService (`App.Interactions`) — Selenium Actions, chainable

```csharp
App.Interactions.MoveToElement(menu).Click(subMenuItem).Perform();
App.Interactions.DragAndDrop(sourceCard, targetColumn).Perform();
App.Interactions.DoubleClick(row).Perform();
App.Interactions.ContextClick(row).Perform();
App.Interactions.KeyDown(OpenQA.Selenium.Keys.Control).SendKeys("a").KeyUp(OpenQA.Selenium.Keys.Control).Perform();   // Ctrl+A
```

Always finish an interactions chain with `.Perform()`.

### Assertions (`App.Assert`)

`App.Assert` is BELLATRIX's framework-neutral `IAssert` (`AreEqual`, `IsTrue`, `IsFalse`, `IsNull`, `IsNotNull`, `Fail`, `Multiple`). Use it in page-object `.Assertions.cs` files where no NUnit/MSTest `using` is present:

```csharp
App.Assert.IsTrue(App.Browser.Url.ToString().Contains("/order-received"), "Order confirmation page was not opened.");
```

---

## Page Object Factories

### `App.Create<TPage>()`

Returns a page object instance **without** navigating. Use when the page is already open (after a click or redirect):

```csharp
var cartPage = App.Create<CartPage>();
cartPage.AssertTotalPrice("114.00");
```

### `App.GoTo<TPage>()`

Returns the page object **and** calls its `Open()` (navigates to `Url`):

```csharp
var homePage = App.GoTo<HomePage>();
homePage.AddProductById(28);
```

Both require `TPage : WebPage`. Never `new` a page object.

---

## Configuration: `testFrameworkSettings.<Configuration>.json`

BELLATRIX reads `testFrameworkSettings.Debug.json` / `testFrameworkSettings.Release.json` (copied to the output folder via `<None Update="testFrameworkSettings.$(Configuration).json"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>`). The web part lives under `webSettings`:

```json
{
  "webSettings": {
    "shouldAutomaticallyScrollToVisible": "false",
    "shouldWaitUntilReadyOnElementFound": "false",
    "shouldWaitForAngular": "false",
    "shouldHighlightElements": "true",
    "fullPageScreenshotsEnabled": "true",
    "timeoutSettings": {
      "pageLoadTimeout": "5",
      "scriptTimeout": "1",
      "waitForJavaScriptAnimationsTimeout": "5",
      "waitForAngularTimeout": "5",
      "waitForAjaxTimeout": "5",
      "sleepInterval": "1",
      "waitUntilReadyTimeout": "5",
      "waitForPartialUrl": "5",
      "validationsTimeout": "5",
      "elementToBeVisibleTimeout": "5",
      "elementToExistTimeout": "5",
      "elementToNotExistTimeout": "5",
      "elementToBeClickableTimeout": "5",
      "elementNotToBeVisibleTimeout": "5",
      "elementToHaveContentTimeout": "5"
    },
    "executionSettings": {
      "executionType": "regular",
      "defaultBrowser": "chrome",
      "defaultLifeCycle": "restart every time",
      "resolution": "",
      "browserVersion": "",
      "isCloudRun": "false",
      "url": "http://127.0.0.1:4444/wd/hub",
      "arguments": [ { } ]
    }
  }
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `executionSettings.executionType` | `"regular"` (local browser) or a grid/cloud type | `regular` launches a local browser; anything else uses `executionSettings.url` |
| `executionSettings.defaultBrowser` | `chrome`, `firefox`, `edge`, … | Browser when no `[Browser]` attribute is present |
| `executionSettings.defaultLifeCycle` | `"restart every time"`, `"reuse if started"`, `"restart on fail"` | Browser lifecycle when no `[Browser]` attribute is present |
| `executionSettings.url` | URL | Selenium Grid endpoint for non-regular execution |
| `timeoutSettings.*` | seconds | Defaults for waits and `Validate*` retries |

Custom sections (e.g. `"urlSettings": { "baseUrl": "…" }`) can be read with `ConfigurationService.GetSection<UrlSettings>()` from a matching POCO.
