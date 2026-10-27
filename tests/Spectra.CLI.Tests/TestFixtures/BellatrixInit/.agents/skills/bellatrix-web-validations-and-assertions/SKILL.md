---
name: bellatrix-web-validations-and-assertions
description: Reference skill for BELLATRIX web Validate* methods (which component supports which), assertion patterns, and when to fall back to the test framework's own asserts.
---

# BELLATRIX Web Validations and Assertions Reference

## CRITICAL: Use Native Validate Methods Over Assert.That

For element-level assertions, **always prefer** BELLATRIX `Validate*` extension methods (namespace `Bellatrix.Web`). They retry until the condition holds or `webSettings.timeoutSettings.validationsTimeout` expires, produce readable failure messages, and integrate with BDD logging and bug reporting.

```csharp
// CORRECT - BELLATRIX Validate methods (retry built in)
messageAlert.ValidateIsVisible();
messageAlert.ValidateInnerTextIs("Coupon code applied successfully.");
totalSpan.ValidateInnerTextContains("114.00");

// WRONG - raw assertions on elements (no retry, flaky on async pages)
Assert.That(messageAlert.IsVisible, Is.True);
Assert.That(totalSpan.InnerText, Does.Contain("114.00"));
```

Every `Validate*` takes optional `timeout` and `sleepInterval` arguments **in seconds**: `TotalSpan.ValidateInnerTextIs("114.00€", 15);`.

---

## A Validate Method Compiles Only on Components That Support It

`Validate*` methods are generic extensions constrained by component interfaces. Calling one on a component that lacks the interface is a **build error**:

| Validate family | Available on |
|-----------------|--------------|
| `ValidateIsVisible`, `ValidateIsNotVisible`, `ValidateCssClass*`, `ValidateStyle*`, `ValidateTitle*`, `ValidateTabIndex*`, `ValidateAccessKey*`, `ValidateDir*`, `ValidateLang*` | every component |
| `ValidateInnerTextIs/IsNot/Contains/NotContains` | `Anchor`, `Button`, `Div`, `Heading`, `Label`, `Span`, `Option`, `TextField`, `TextArea`, `Reset`, `Output`, `Progress`, `TableCell` — **NOT** `Select`, `Password`, `CheckBox`, `Image` |
| `ValidateInnerHtmlIs/Contains/NotContains` | `Anchor`, `Div`, `Label`, `Span`, `TextField`, `Output`, table parts |
| `ValidateValueIs/Contains/IsNull` | `TextField`, `Button`, `CheckBox`, `RadioButton`, `Option`, `Password`, `Email`, `Phone`, `Search`, `Url`, `Number`, `Range`, `Date`, `Time`, … |
| `ValidateIsDisabled`, `ValidateIsNotDisabled` | `Button`, `TextField`, `TextArea`, `CheckBox`, `RadioButton`, `Select`, `Option`, `Password`, HTML5 inputs |
| `ValidateIsChecked`, `ValidateIsNotChecked` | `CheckBox`, `RadioButton` |
| `ValidateIsSelected`, `ValidateIsNotSelected` | `Option` |
| `ValidateIsRequired/NotRequired` | `TextField`, `TextArea`, `Select`, `InputFile`, `Password`, `Email`, `Phone`, `Search`, `Url`, `Number`, `Range`, `Date`, `Time`, `Month`, `Week`, `DateTimeLocal`, `Color` (not `CheckBox`, `RadioButton`) |
| `ValidateIsReadonly/NotReadonly` | the same list minus `Range`, `Color`, `InputFile` |
| `ValidatePlaceholderIs/IsNull` | `TextField`, `TextArea`, `Password`, `Email`, `Phone`, `Search`, `Url`, `Number` |
| `ValidateHrefIs`, `ValidateHrefIsSet`, `ValidateTargetIs`, `ValidateRelIs` | `Anchor` |
| `ValidateSrcIs`, `ValidateAltIs`, `ValidateWidthIsNull`/`ValidateWidthIsNotNull`, `ValidateHeightIsNull`/`ValidateHeightIsNotNull` (no `ValidateWidthIs(int)`) | `Image` |
| `ValidateTextIs`, `ValidateTextContains`, `ValidateTextIsNull` | `TextArea` |
| `ValidateEmailIs`, `ValidatePhoneIs`, `ValidateSearchIs`, `ValidateUrlIs`, `ValidatePasswordIs`, `ValidateNumberIs(double)`, `ValidateRangeIs(int)`, `ValidateDateIs`, `ValidateTimeIs`, `ValidateColorIs`, `ValidateMonthIs`, `ValidateWeekIs` | the matching HTML5 input component |
| `ValidateMaxIs(int)`, `ValidateMinIs(int)` | `Number`, `Range` |
| `ValidateMaxTextIs(string)`, `ValidateMinTextIs(string)` | `Date`, `Time`, `Month`, `Week`, `DateTimeLocal` (`Progress`: `ValidateMaxTextIs` only) |
| `ValidateMaxLengthIs(int)`, `ValidateMinLengthIs(int)` | `TextField`, `TextArea`, `Password`, `Email`, `Phone`, `Search`, `Url` |
| `ValidateStepIs(int)` | `Number`, `Range`, `Date`, `Time`, `Month`, `Week`, `DateTimeLocal` |
| `ValidateForIs` | `Label`, `Output` |

### Validating a `<select>`

`Select` has no inner-text validation. Validate its selected `Option` instead:

```csharp
CountrySelect.GetSelected().ValidateInnerTextIs("Bulgaria");
CountrySelect.ValidateIsNotDisabled();
```

---

## Available Web Validate Methods

### Visibility

```csharp
element.ValidateIsVisible();
element.ValidateIsNotVisible();
```

There is **no** `ValidateExists`, `ValidateIsNotExisting`, or `ValidateIsPresent`. To wait for an element to disappear from the DOM, use `element.ToNotExists().WaitToBe();`; for presence without visibility, `element.ToExists().WaitToBe();`.

### Text and Content

```csharp
element.ValidateInnerTextIs("exact text");
element.ValidateInnerTextIsNot("old text");
element.ValidateInnerTextContains("partial text");      // web HAS contains (desktop does not)
element.ValidateInnerTextNotContains("Error");
element.ValidateInnerHtmlContains("<strong>");
textArea.ValidateTextIs("comment body");
```

### Values and Placeholders

```csharp
field.ValidateValueIs("john@example.com");
field.ValidateValueContains("john");
field.ValidatePlaceholderIs("House number and street name");
quantity.ValidateNumberIs(2);
```

### State

```csharp
button.ValidateIsDisabled();
button.ValidateIsNotDisabled();
checkBox.ValidateIsChecked();
checkBox.ValidateIsNotChecked();
option.ValidateIsSelected();
field.ValidateIsRequired();
field.ValidateIsReadonly();
```

### Attributes, Links, Style

```csharp
link.ValidateHrefIs("https://example.com/checkout/#");
link.ValidateHrefIsSet();
link.ValidateTargetIs("_blank");
image.ValidateSrcIs("https://example.com/logo.png");
element.ValidateCssClassIs("showlogin");
element.ValidateCssClassContains("is-active");
element.ValidateStyleContains("display: none");
element.ValidateTitleIs("Remove this item");
```

There is **no** generic `ValidateAttributeIs(name, value)`. For an arbitrary attribute (e.g. `aria-expanded`, `data-state`) wait for it with a fluent wait or assert it explicitly (see below).

---

## Usage in a Page Object Assertions File

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Pages;

public partial class CartPage
{
    public void AssertCouponApplied()
    {
        MessageAlert.ValidateIsVisible();
        MessageAlert.ValidateInnerTextIs("Coupon code applied successfully.");
    }

    public void AssertTotalPrice(string price)
    {
        TotalSpan.ValidateInnerTextIs($"{price}€");
    }

    public void AssertCartIsEmpty()
    {
        EmptyCartMessage.ValidateInnerTextContains("Your cart is currently empty");
        ProceedToCheckoutButton.ValidateIsNotVisible();
    }

    public void AssertOnCartPage()
    {
        AssertLandedOnPage("/cart");        // WebPage built-in URL assertion
    }
}
```

`WebPage` also provides `AssertNotLandedOnPage(partialUrl)`, `AssertUrl(fullUrl)`, `AssertUrlPath(path)`, and `AssertUrlPathAndQuery(pathAndQuery)`.

---

## When to Use NUnit / MSTest / `App.Assert`

Use framework assertions **only** when no `Validate*` method covers the need:

```csharp
// Browser-level values (strings, not components) — in TEST files (NUnit 4 constraint model)
Assert.That(App.Browser.Title, Is.EqualTo("Checkout – Bellatrix Demos"));
Assert.That(App.Browser.Url.ToString(), Does.Contain("/order-received"));   // Url is a Uri

// Collection counts
Assert.That(App.Components.CreateAllByCss<Div>("[data-testid='product-card']").Count(), Is.EqualTo(12));

// Arbitrary attributes not covered by Validate*
Assert.That(MenuToggle.GetAttribute("aria-expanded"), Is.EqualTo("true"));

// Dialog text
Assert.That(App.Dialogs.GetText(), Is.EqualTo("Are you sure?"));
```

In page-object `.Assertions.cs` files (no NUnit `using`), use BELLATRIX's framework-neutral assert:

```csharp
App.Assert.AreEqual("true", MenuToggle.GetAttribute("aria-expanded"), "The menu should be expanded.");
App.Assert.IsTrue(App.Browser.Url.ToString().Contains("/order-received"), "Order confirmation page was not opened.");
```

NUnit 4 removed `Assert.AreEqual`/`Assert.IsTrue` from `NUnit.Framework.Assert` (they live in `ClassicAssert`). In test files use `Assert.That(...)`; never mix `using Bellatrix.Assertions;` and `using NUnit.Framework;` in one file (`Assert` becomes ambiguous).

---

## Priority Order

1. **Native `Validate*` first** — retry, reporting, and web includes `*Contains` variants
2. **`WebPage` URL assertions** (`AssertLandedOnPage`, `AssertUrlPath`) for navigation outcomes
3. **`Assert.That` / `App.Assert`** only for browser-level strings, counts, dialog text, and attributes with no `Validate*`
4. **Never** use a raw assert on a property that has a `Validate*` method (`Assert.That(x.IsVisible, Is.True)` when `ValidateIsVisible()` exists)
