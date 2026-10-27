---
name: bellatrix-web-page-objects-and-patterns
description: Reference skill for BELLATRIX web page object structure, the 3-file partial pattern, reusable sections, models, and design patterns.
---

# BELLATRIX Web Page Objects and Patterns Reference

## CRITICAL: Required Syntax

**Every page object MUST follow these rules to avoid build errors:**

### 1. File-Scoped Namespaces (C# 10+)

```csharp
// CORRECT - file-scoped namespace (no braces)
namespace MyProject.WebTests.Pages;

public partial class LoginPage : WebPage
{
    // ...
}

// WRONG - block-scoped namespace
namespace MyProject.WebTests.Pages
{
    public partial class LoginPage : WebPage { }
}
```

### 2. Use the `partial` Keyword

All files of one page (`.Actions.cs`, `.Map.cs`, `.Assertions.cs`) declare the SAME `public partial class`. Declare the base class (`: WebPage`) once — in the Actions file — the other parts may omit it.

### 3. Use the `WebPage` Base Class and `override` Url + Open

```csharp
// CORRECT — WebPage is NON-generic
public partial class LoginPage : WebPage
{
    public override string Url => "https://example.com/login";

    public override void Open()
    {
        App.Navigation.Navigate(Url);
        App.Browser.WaitUntilReady();
    }
}

// WRONG
// (also wrong: any generic WebPage with type arguments — WebPage is non-generic)
public partial class LoginPage : DesktopPage { }             // desktop only
public partial class LoginPage : NavigatablePage { }         // obsolete base class
```

`WebPage` (namespace `Bellatrix.Web`) gives you the `App` property, the abstract `Url`, a virtual `Open()`, and built-in URL assertions: `AssertLandedOnPage(partialUrl)`, `AssertNotLandedOnPage(partialUrl)`, `AssertUrl(fullUrl)`, `AssertUrlPath(path)`, `AssertUrlPathAndQuery(pathAndQuery)`.

### 4. Use `Validate*` Methods in Assertions (NOT Assert.That)

See `bellatrix-web-validations-and-assertions`.

### 5. `using Bellatrix.Web;` in every file

Components (`Button`, `TextField`, …), factories (`CreateByCss`, `CreateByXpath`, …), waits (`ToBeVisible`, …), and `Validate*` methods are all in `Bellatrix.Web`.

---

## Project Structure (3-File Pattern)

```
Pages/
  LoginPage/
    LoginPage.Actions.cs       # : WebPage, Url, Open(), workflow methods
    LoginPage.Map.cs           # component properties (locators only)
    LoginPage.Assertions.cs    # Assert* methods using Validate*
  CartPage/
    CartPage.Actions.cs
    CartPage.Map.cs
    CartPage.Assertions.cs
Sections/
  HeaderSection/
    HeaderSection.Actions.cs   # : WebSection
    HeaderSection.Map.cs
    HeaderSection.Assertions.cs
Models/
  LoginCredentials.cs
  BillingInfo.cs
Tests/
  Login/LoginTests.cs
  Cart/CartTests.cs
```

---

## Complete 3-File Example: LoginPage

### LoginPage.Map.cs

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Pages;

public partial class LoginPage
{
    // ===== FORM FIELDS =====
    public TextField EmailField =>
        App.Components.CreateByCss<TextField>("[data-testid='email']");

    public Password PasswordField =>
        App.Components.CreateById<Password>("password");

    public CheckBox RememberMeCheckBox =>
        App.Components.CreateByCss<CheckBox>("[name='remember']");

    // ===== BUTTONS / LINKS =====
    public Button LoginButton =>
        App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']").ToBeClickable();

    public Anchor ForgotPasswordLink =>
        App.Components.CreateByLinkText<Anchor>("Forgot password?");

    // ===== MESSAGES =====
    public Div ErrorMessage =>
        App.Components.CreateByCss<Div>("[data-testid='login-error']");

    public Heading PageHeading =>
        App.Components.CreateByTag<Heading>("h1");
}
```

### LoginPage.Actions.cs

```csharp
using Bellatrix.Web;
using MyProject.WebTests.Models;

namespace MyProject.WebTests.Pages;

public partial class LoginPage : WebPage
{
    public override string Url => "https://example.com/login";

    public override void Open()
    {
        App.Navigation.Navigate(Url);
        App.Browser.WaitUntilReady();
    }

    public void LoginWith(string email, string password)
    {
        EmailField.SetText(email);
        PasswordField.SetPassword(password);
        LoginButton.Click();
    }

    public void LoginWith(LoginCredentials credentials)
    {
        LoginWith(credentials.Email, credentials.Password);
    }

    public void LoginWithRememberMe(string email, string password)
    {
        EmailField.SetText(email);
        PasswordField.SetPassword(password);
        RememberMeCheckBox.Check();
        LoginButton.Click();
    }

    public DashboardPage LoginAndGoToDashboard(string email, string password)
    {
        LoginWith(email, password);
        App.Navigation.WaitForPartialUrl("/dashboard");
        return App.Create<DashboardPage>();
    }
}
```

### LoginPage.Assertions.cs

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Pages;

public partial class LoginPage
{
    public void AssertPageLoaded()
    {
        PageHeading.ValidateInnerTextIs("Log in");
        EmailField.ValidateIsVisible();
        PasswordField.ValidateIsVisible();
        LoginButton.ValidateIsNotDisabled();
    }

    public void AssertErrorMessageContains(string expectedText)
    {
        ErrorMessage.ValidateIsVisible();
        ErrorMessage.ValidateInnerTextContains(expectedText);
    }

    public void AssertStillOnLoginPage()
    {
        AssertLandedOnPage("/login");
    }
}
```

---

## Design Patterns

### 1. Property-Based Component Definitions (Recommended)

Expression-bodied properties are lazy — the element is located on each access, so they survive re-renders:

```csharp
public Button PlaceOrderButton => App.Components.CreateById<Button>("place_order");
```

### 2. Method-Based for Dynamic Elements

When the locator depends on runtime data, expose a method with an interpolated locator:

```csharp
public Anchor AddToCartButton(int productId) =>
    App.Components.CreateByAttributesContaining<Anchor>("data-product_id", productId.ToString()).ToBeClickable();

public Button RemoveButtonFor(string productName) =>
    App.Components.CreateByXpath<Button>($"//tr[td[normalize-space()='{productName}']]//a[@aria-label='Remove this item']");
```

Prefer a direct factory when only one attribute varies; use `CreateByXpath` only when the dynamic locator is genuinely structural or text-based.

### 3. Collections for Repeated Elements

```csharp
public ComponentsList<Number> QuantityBoxes =>
    App.Components.CreateAllByClassContaining<Number>("input-text qty text");

public void UpdateProductQuantity(int productNumber, int newQuantity)
{
    if (productNumber > QuantityBoxes.Count())
    {
        throw new System.ArgumentException("There are fewer items in the cart than the requested product number.");
    }

    QuantityBoxes[productNumber - 1].SetNumber(newQuantity);
    UpdateCartButton.Click();
    App.Browser.WaitForAjax();
}
```

### 4. Navigation Methods Return Page Objects

```csharp
public CheckoutPage ProceedToCheckout()
{
    ProceedToCheckoutButton.Click();
    App.Navigation.WaitForPartialUrl("/checkout");
    return App.Create<CheckoutPage>();
}
```

### 5. Fluent Interface for Chaining

```csharp
public LoginPage WithEmail(string email)
{
    EmailField.SetText(email);
    return this;
}

public LoginPage WithPassword(string password)
{
    PasswordField.SetPassword(password);
    return this;
}

// Usage: loginPage.WithEmail("user@example.com").WithPassword("secret").LoginButton.Click();
```

### 6. Organizing Complex Pages with Regions

Group locators with `// ===== REGION =====` comments inside the Map file:

```csharp
public partial class CheckoutPage
{
    // ===== BILLING DETAILS =====
    public TextField BillingFirstName => App.Components.CreateById<TextField>("billing_first_name");
    public TextField BillingLastName => App.Components.CreateById<TextField>("billing_last_name");
    public Select BillingCountry => App.Components.CreateById<Select>("billing_country");
    public Select BillingState => App.Components.CreateById<Select>("billing_state").ToBeVisible().ToBeClickable();
    public Phone BillingPhone => App.Components.CreateById<Phone>("billing_phone");
    public Email BillingEmail => App.Components.CreateById<Email>("billing_email");

    // ===== PAYMENT =====
    public RadioButton CheckPaymentsRadioButton =>
        App.Components.CreateByAttributesContaining<RadioButton>("for", "payment_method_cheque");

    // ===== ACTIONS =====
    public Button PlaceOrderButton => App.Components.CreateById<Button>("place_order");
}
```

### 7. Reusable Sections Across Pages (Composition with `WebSection`)

Headers, navigation bars, footers, cookie banners, and mini-carts appear on many pages. Model each as a **section** — a 3-file partial class inheriting `WebSection` (namespace `Bellatrix.Web`), which exposes the same `App` property as a page but has no `Url`/`Open()`:

```csharp
// Sections/HeaderSection/HeaderSection.Map.cs
using Bellatrix.Web;

namespace MyProject.WebTests.Sections;

public partial class HeaderSection
{
    public Search SearchField => App.Components.CreateByCss<Search>("[data-testid='header-search']");
    public Anchor CartLink => App.Components.CreateByCss<Anchor>("[data-testid='header-cart']");
    public Span CartCount => App.Components.CreateByCss<Span>("[data-testid='cart-count']");
    public Anchor MenuItem(string text) => App.Components.CreateByLinkText<Anchor>(text);
}

// Sections/HeaderSection/HeaderSection.Actions.cs
using Bellatrix.Web;

namespace MyProject.WebTests.Sections;

public partial class HeaderSection : WebSection
{
    public void SearchFor(string term)
    {
        SearchField.SetSearch(term);
        App.Interactions.SendKeys(SearchField, OpenQA.Selenium.Keys.Enter).Perform();   // Enter
        App.Browser.WaitUntilReady();
    }

    public void OpenMenu(string text)
    {
        MenuItem(text).Click();
        App.Browser.WaitUntilReady();
    }
}

// Sections/HeaderSection/HeaderSection.Assertions.cs
using Bellatrix.Web;

namespace MyProject.WebTests.Sections;

public partial class HeaderSection
{
    public void AssertCartCountIs(int expected) => CartCount.ValidateInnerTextIs(expected.ToString());
}
```

Embed the section in each page's Map file and delegate from Actions:

```csharp
// Pages/HomePage/HomePage.Map.cs
using MyProject.WebTests.Sections;

public partial class HomePage
{
    public HeaderSection Header => new HeaderSection();
}

// Pages/HomePage/HomePage.Actions.cs
public partial class HomePage : WebPage
{
    public override string Url => "https://example.com/";

    public void SearchFor(string term) => Header.SearchFor(term);
}
```

Tests may also create a section directly: `var header = new HeaderSection();`.

**Rules of thumb:**
- Extract a section only after the duplication is real (≥2 pages). Don't pre-extract for hypothetical reuse.
- Keep the public action names on each page unchanged — the section is an implementation detail.
- A section owns its own locators AND its own workflow methods; a pure locator group stays on the page as a region.
- If the shared block's locators differ per page, do NOT force a section.

---

## Model Classes

Place data models in `Models/`:

```csharp
namespace MyProject.WebTests.Models;

public class BillingInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Country { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public bool ShouldCreateAccount { get; set; }
}
```

Usage in a page object:

```csharp
public void FillBillingInfo(BillingInfo billingInfo)
{
    BillingFirstName.SetText(billingInfo.FirstName);
    BillingLastName.SetText(billingInfo.LastName);
    BillingCountry.SelectByText(billingInfo.Country);
    BillingPhone.SetPhone(billingInfo.Phone);
    BillingEmail.SetEmail(billingInfo.Email);

    if (billingInfo.ShouldCreateAccount)
    {
        CreateAccountCheckBox.Check();
    }
}
```

Enums for fixed option sets (sort orders, payment methods) also go in `Models/` and are mapped to visible texts inside the page object.

---

## Best Practices Summary

1. **One folder per page** with 3 partial files (Map, Actions, Assertions); one folder per section under `Sections/`
2. **Map file**: component properties and locator methods only — no logic
3. **Actions file**: `: WebPage`, `override Url`, `override Open()`, business workflows — no assertions (a `Validate*` used as a synchronization point is acceptable)
4. **Assertions file**: `Assert*` methods built from `Validate*` — no actions
5. **`App.Create<TPage>()` / `App.GoTo<TPage>()`** — never `new` a page
6. **No direct `App.Components`** in test files — always go through page objects and sections
7. **Descriptive, business-level method names** (`ApplyCoupon`, `FillBillingInfo`), not UI mechanics (`ClickButton3`)
