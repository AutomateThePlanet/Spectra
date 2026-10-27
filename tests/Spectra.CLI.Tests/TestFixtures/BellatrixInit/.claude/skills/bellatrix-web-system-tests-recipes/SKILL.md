---
name: bellatrix-web-system-tests-recipes
description: Reference skill with complete test examples and common patterns for BELLATRIX web system tests (login, forms, dropdowns, tables, dialogs, frames, tabs, AJAX, data-driven, page objects).
---

# BELLATRIX Web System Tests Recipes

> **Note:** These recipes show direct component usage so each pattern is visible in one place. For production code, always put locators and workflows in **Page Objects** (see `bellatrix-web-page-objects-and-patterns`); tests then call page-object methods only.
>
> **Page Object Structure:**
> ```
> Pages/{PageName}/{PageName}.Actions.cs     - : WebPage, Url, Open(), workflows
> Pages/{PageName}/{PageName}.Map.cs         - component properties
> Pages/{PageName}/{PageName}.Assertions.cs  - Assert* methods using Validate*
> ```

---

## Login Flow Recipe

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
        // Arrange
        App.Navigation.Navigate("https://example.com/login");
        var emailField = App.Components.CreateByCss<TextField>("[data-testid='email']");
        var passwordField = App.Components.CreateById<Password>("password");
        var loginButton = App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']");

        // Act
        emailField.SetText("user@example.com");
        passwordField.SetPassword("Password123");
        loginButton.Click();

        // Assert
        App.Navigation.WaitForPartialUrl("/dashboard");
        var welcome = App.Components.CreateByCss<Heading>("[data-testid='welcome']");
        welcome.ValidateInnerTextContains("Welcome");
    }

    [Test]
    [Category("Regression")]
    public void Login_WithInvalidPassword_ShowsErrorMessage()
    {
        App.Navigation.Navigate("https://example.com/login");
        App.Components.CreateByCss<TextField>("[data-testid='email']").SetText("user@example.com");
        App.Components.CreateById<Password>("password").SetPassword("wrong");
        App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']").Click();

        var error = App.Components.CreateByCss<Div>("[data-testid='login-error']");
        error.ValidateIsVisible();
        error.ValidateInnerTextIs("Invalid email or password.");
    }
}
```

---

## Form Submission Recipe

```csharp
[Test]
public void Register_WithValidData_ShowsSuccessMessage()
{
    App.Navigation.Navigate("https://example.com/register");

    App.Components.CreateById<TextField>("name").SetText("John Doe");
    App.Components.CreateById<Email>("email").SetEmail("john@example.com");
    App.Components.CreateById<Phone>("phone").SetPhone("+359888123456");
    App.Components.CreateById<Date>("birthdate").SetDate(1990, 5, 17);
    App.Components.CreateById<Select>("country").SelectByText("United States");
    App.Components.CreateByCss<RadioButton>("input[name='plan'][value='pro']").Click();
    App.Components.CreateByCss<CheckBox>("[data-testid='terms']").Check();
    App.Components.CreateById<TextArea>("notes").SetText("Please call after 5 PM.");
    App.Components.CreateByXpath<Button>("//button[@type='submit']").Click();

    var success = App.Components.CreateByCss<Div>("[data-testid='success']");
    success.ValidateInnerTextIs("Registration successful");
}
```

---

## Field Validation Recipe

```csharp
[Test]
public void Register_WithEmptyRequiredFields_ShowsValidationErrors()
{
    App.Navigation.Navigate("https://example.com/register");

    var email = App.Components.CreateById<Email>("email");
    email.ValidateIsRequired();
    email.ValidatePlaceholderIs("you@example.com");

    App.Components.CreateByXpath<Button>("//button[@type='submit']").Click();

    App.Components.CreateByXpath<Span>("//input[@id='email']/following-sibling::span[contains(@class,'error')]")
        .ValidateInnerTextIs("Email is required.");
    App.Components.CreateByXpath<Button>("//button[@type='submit']").ValidateIsVisible();
}
```

---

## Dropdown (Select) Recipe

```csharp
[Test]
public void SortProducts_ByPriceLowToHigh_SelectsOption()
{
    App.Navigation.Navigate("https://demos.bellatrix.solutions/");

    var sortDropDown = App.Components.CreateByNameEndingWith<Select>("orderby");
    sortDropDown.SelectByText("Sort by price: low to high");

    sortDropDown.GetSelected().ValidateInnerTextIs("Sort by price: low to high");   // Select has no InnerText — validate the Option
}
```

### Custom (non-`<select>`) dropdown

```csharp
App.Components.CreateByCss<Button>("[data-testid='currency-trigger']").Click();
App.Components.CreateByXpath<Button>("//*[@role='option' and normalize-space()='EUR']").Click();
App.Components.CreateByCss<Span>("[data-testid='currency-value']").ValidateInnerTextIs("EUR");
```

---

## Table / Row Selection Recipe

```csharp
[Test]
public void EditOrder_FromOrdersTable_OpensOrderDetails()
{
    App.Navigation.Navigate("https://example.com/account/orders");

    // Locate the action inside the row identified by its visible text
    var viewLink = App.Components.CreateByXpath<Anchor>(
        "//tr[td[normalize-space()='#4021']]//a[normalize-space()='View']");
    viewLink.Click();

    App.Components.CreateByTag<Heading>("h1").ValidateInnerTextContains("Order #4021");
}

[Test]
public void CartRows_AfterAddingTwoProducts_ShowsTwoRows()
{
    App.Navigation.Navigate("https://example.com/cart");

    var rows = App.Components.CreateAllByCss<Div>("tr.cart_item");
    Assert.That(rows.Count(), Is.EqualTo(2));
}
```

---

## Dialog Confirmation Recipe (native alert / confirm)

```csharp
[Test]
public void DeleteAddress_WhenConfirmed_RemovesAddress()
{
    App.Navigation.Navigate("https://example.com/account/addresses");

    App.Components.CreateByXpath<Button>("//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").Click();

    Assert.That(App.Dialogs.GetText(), Is.EqualTo("Delete this address?"));
    App.Dialogs.Handle();                                   // OK

    App.Components.CreateByXpath<Span>("//span[normalize-space()='Home']").ToNotExists().WaitToBe();
}

[Test]
public void DeleteAddress_WhenCancelled_KeepsAddress()
{
    App.Navigation.Navigate("https://example.com/account/addresses");

    App.Components.CreateByXpath<Button>("//li[.//span[normalize-space()='Home']]//button[normalize-space()='Delete']").Click();
    App.Dialogs.Handle(dialogButton: DialogButton.Cancel);

    App.Components.CreateByXpath<Span>("//span[normalize-space()='Home']").ValidateIsVisible();
}
```

### HTML modal (not a native dialog)

```csharp
App.Components.CreateByCss<Button>("[data-testid='open-delete-modal']").Click();
var modal = App.Components.CreateByCss<Div>("[role='dialog']").ToBeVisible();
modal.CreateByXpath<Button>(".//button[normalize-space()='Confirm']").Click();
App.Components.CreateByCss<Div>("[role='dialog']").ToNotExists().WaitToBe();
```

---

## Wait / AJAX Recipe

```csharp
[Test]
public void ApplyCoupon_WithValidCode_ShowsSuccessAndNewTotal()
{
    App.Navigation.Navigate("https://demos.bellatrix.solutions/cart/");

    App.Components.CreateById<TextField>("coupon_code").SetText("happybirthday");
    App.Components.CreateByValueContaining<Button>("Apply coupon").Click();

    var message = App.Components.CreateByClassContaining<Div>("woocommerce-message");
    message.ToHasContent().ToBeVisible().WaitToBe();
    message.ValidateInnerTextIs("Coupon code applied successfully.");

    App.Browser.WaitForAjax();
    App.Components.CreateByXpath<Span>("//*[@class='order-total']//span").ValidateInnerTextIs("54.00€");
}
```

---

## Hover Menu Recipe

```csharp
[Test]
public void OpenSubmenu_ByHover_NavigatesToCategory()
{
    App.Navigation.Navigate("https://example.com/");

    var productsMenu = App.Components.CreateByLinkText<Anchor>("Products");
    productsMenu.Hover();

    var rocketsLink = App.Components.CreateByLinkText<Anchor>("Rockets").ToBeVisible();
    rocketsLink.Click();

    App.Navigation.WaitForPartialUrl("/category/rockets");
}
```

---

## iframe Recipe

```csharp
[Test]
public void PayByCard_InsidePaymentFrame_ShowsConfirmation()
{
    App.Navigation.Navigate("https://example.com/checkout/payment");

    var paymentFrame = App.Components.CreateByCss<Frame>("iframe[title='Secure payment input frame']");
    App.Browser.SwitchToFrame(paymentFrame);
    App.Components.CreateByCss<TextField>("[name='cardnumber']").SetText("4242424242424242");
    App.Browser.SwitchToDefault();

    App.Components.CreateByCss<Button>("[data-testid='pay']").Click();
    App.Components.CreateByCss<Heading>("[data-testid='payment-status']").ValidateInnerTextIs("Payment received");
}
```

---

## New Tab Recipe

```csharp
[Test]
public void OpenTerms_InNewTab_ShowsTermsPage()
{
    App.Navigation.Navigate("https://example.com/register");

    var termsLink = App.Components.CreateByLinkText<Anchor>("Terms and Conditions");
    termsLink.ValidateTargetIs("_blank");
    termsLink.Click();

    App.Browser.SwitchToLastTab();
    App.Browser.WaitUntilReady();
    Assert.That(App.Browser.Url.ToString(), Does.Contain("/terms"));

    App.Browser.SwitchToFirstBrowserTab();
}
```

---

## File Upload Recipe

```csharp
[Test]
public void UploadAvatar_WithPng_ShowsPreview()
{
    App.Navigation.Navigate("https://example.com/account/profile");

    var filePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "TestData", "avatar.png");
    App.Components.CreateByCss<InputFile>("input[type='file']").Upload(filePath);

    App.Components.CreateByCss<Image>("[data-testid='avatar-preview']").ValidateSrcIsNotNull();
}
```

---

## Data-Driven Test Recipe (non-SPECTRA tests only)

```csharp
[Test]
[TestCase("admin@example.com", "admin123", "Welcome, Admin")]
[TestCase("user@example.com", "user123", "Welcome, User")]
public void Login_WithDifferentRoles_ShowsCorrectWelcome(string email, string password, string expected)
{
    var loginPage = App.GoTo<LoginPage>();
    loginPage.LoginWith(email, password);

    var dashboard = App.Create<DashboardPage>();
    dashboard.AssertWelcomeMessageIs(expected);
}
```

**SPECTRA-traced tests never use `[TestCase]`** — each SPECTRA TC becomes its own method with `[ManualTestCase("TC-XXX")]`.

---

## Page Objects End-to-End Recipe

```csharp
using Bellatrix.Web;
using Bellatrix.Web.NUnit;
using MyProject.WebTests.Models;
using MyProject.WebTests.Pages;
using NUnit.Framework;

namespace MyProject.WebTests.Tests;

[TestFixture]
[Browser(BrowserType.Chrome, Lifecycle.ReuseIfStarted)]
public class PurchaseTests : WebTest
{
    [Test]
    [Category("Smoke")]
    public void PurchaseRocket_WithCouponAndBillingInfo_PlacesOrder()
    {
        var homePage = App.GoTo<HomePage>();
        homePage.FilterProducts(ProductFilter.Popularity);
        homePage.AddProductById(28);
        homePage.ViewCartButton.Click();

        var cartPage = App.Create<CartPage>();
        cartPage.ApplyCoupon("happybirthday");
        cartPage.UpdateProductQuantity(1, 2);
        cartPage.AssertTotalPrice("114.00");

        var checkoutPage = cartPage.ProceedToCheckout();
        checkoutPage.FillBillingInfo(new BillingInfo
        {
            FirstName = "In",
            LastName = "Deepthought",
            Country = "Bulgaria",
            Phone = "+00359894646464",
            Email = "info@bellatrix.solutions",
            ShouldCreateAccount = true,
        });
        checkoutPage.PlaceOrder();
        checkoutPage.AssertOrderReceived();
    }
}
```

---

## Common Web Locator Patterns

```csharp
// data-testid (PREFERRED)
App.Components.CreateByCss<Button>("[data-testid='submit']")

// Static id / name
App.Components.CreateById<TextField>("email")
App.Components.CreateByCss<TextField>("[name='username']")

// ARIA
App.Components.CreateByCss<Button>("[aria-label='Close']")

// Label → input
App.Components.CreateByXpath<TextField>("//label[normalize-space()='Email']/following-sibling::input")

// Button / link by visible text
App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']")
App.Components.CreateByLinkText<Anchor>("Sign in")

// Element in a row identified by text
App.Components.CreateByXpath<Anchor>("//tr[td[normalize-space()='Product A']]//a[normalize-space()='Edit']")

// Dynamic (inside a page object method)
App.Components.CreateByAttributesContaining<Anchor>("data-product_id", productId.ToString())
```

---

## Required Using Directives

```csharp
using Bellatrix.Web;            // components, factories, waits, Validate*, Browser attribute
using Bellatrix.Web.NUnit;      // WebTest base (or Bellatrix.Web.MSTest)
using NUnit.Framework;          // [TestFixture], [Test], [Category], Assert.That
using MyProject.WebTests.Pages;
using MyProject.WebTests.Models;
```
