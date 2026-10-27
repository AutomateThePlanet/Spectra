---
name: bellatrix-web-components
description: Reference skill for BELLATRIX web component types, the HTML element to component mapping, component methods and properties, and creation patterns.
---

# BELLATRIX Web Components Reference

A BELLATRIX web component is a typed wrapper around one HTML element. The component type decides which actions and validations are available, so pick it from the element's tag, `type`, and ARIA role as recorded during scenario execution — never guess it.

## Component Hierarchy

All web components inherit from `Bellatrix.Web.Component` and live in the `Bellatrix.Web` namespace — one `using Bellatrix.Web;` covers components, factories, waits, and `Validate*` methods.

## Web Components

### Common Controls

| Component | HTML | Key Methods | Key Properties |
|-----------|------|-------------|----------------|
| `Button` | `button`, `input type=submit/button`, any clickable element | `Click()`, `Hover()` | `InnerText`, `Value`, `IsDisabled` |
| `Reset` | `input type=reset` | `Click()`, `Hover()` | `InnerText`, `Value`, `IsDisabled` |
| `Anchor` | `a` | `Click()`, `Hover()` | `Href`, `InnerText`, `InnerHtml`, `Target`, `Rel` |
| `TextField` | `input type=text` | `SetText(string)`, `Hover()` | `Value`, `Placeholder`, `IsDisabled`, `IsReadonly`, `IsRequired`, `MaxLength`, `MinLength` |
| `TextArea` | `textarea` | `SetText(string)`, `GetText()`, `Hover()` | `Placeholder`, `IsDisabled`, `IsReadonly`, `IsRequired`, `Rows`, `Cols` |
| `CheckBox` | `input type=checkbox` | `Check(bool isChecked = true)`, `Uncheck()`, `Hover()` | `IsChecked`, `IsDisabled`, `Value` |
| `RadioButton` | `input type=radio` | `Click()`, `Hover()` | `IsChecked`, `IsDisabled`, `Value` |
| `Select` | `select` | `SelectByText(string)`, `SelectByIndex(int)`, `GetSelected()`, `GetAllOptions()`, `Hover()` | `IsDisabled`, `IsRequired`, `IsReadonly` |
| `Option` | `option` | — | `InnerText`, `Value`, `IsSelected`, `IsDisabled` |
| `Label` | `label` | `Hover()` | `InnerText`, `InnerHtml`, `For` |
| `Heading` | `h1`–`h6` | `Hover()` | `InnerText` |
| `Span` | `span` | `Hover()` | `InnerText`, `InnerHtml` |
| `Div` | `div` and other text containers (`p`, `li`, `section`, `td`, `form`) | `Hover()` | `InnerText`, `InnerHtml` |
| `Image` | `img` | `Click()`, `Hover()` | `Src`, `Alt`, `Height`, `Width`, `SrcSet`, `Sizes` |
| `InputFile` | `input type=file` | `Upload(string filePath)` | `IsRequired`, `IsMultiple`, `Accept` |

### HTML5 Input Controls

| Component | HTML | Key Methods | Key Properties |
|-----------|------|-------------|----------------|
| `Password` | `input type=password` | `SetPassword(string)`, `GetPassword()`, `Hover()` | `Value`, `Placeholder`, `IsDisabled`, `IsRequired` |
| `Email` | `input type=email` | `SetEmail(string)`, `GetEmail()`, `Hover()` | `Value`, `Placeholder`, `IsRequired` |
| `Phone` | `input type=tel` | `SetPhone(string)`, `GetPhone()`, `Hover()` | `Value`, `Placeholder` |
| `Search` | `input type=search` | `SetSearch(string)`, `GetSearch()`, `Hover()` | `Value`, `Placeholder` |
| `Url` | `input type=url` | `SetUrl(string)`, `GetUrl()`, `Hover()` | `Value`, `Placeholder` |
| `Number` | `input type=number` | `SetNumber(double)`, `GetNumber()`, `Hover()` | `Value`, `Max`, `Min`, `Step` |
| `Range` | `input type=range` | `SetRange(int)`, `GetRange()`, `Hover()` | `Value`, `Max`, `Min`, `Step` |
| `Date` | `input type=date` | `SetDate(int year, int month, int day)`, `GetDate()`, `Hover()` | `Value`, `Max`, `Min` |
| `Time` | `input type=time` | `SetTime(int hours, int minutes)`, `GetTime()`, `Hover()` | `Value`, `Max`, `Min` |
| `DateTimeLocal`, `Month`, `Week` | `input type=datetime-local/month/week` | type-specific setters | `Value` |
| `Color` | `input type=color` | `SetColor(string)`, `GetColor()`, `Hover()` | `Value` |
| `Output` / `Progress` | `output` / `progress` | — | `Output`: `InnerText`, `InnerHtml`, `For`; `Progress`: `Value`, `Max` |

### Structural Components

| Component | HTML | Notes |
|-----------|------|-------|
| `Frame` | `iframe` | Switch into it with `App.Browser.SwitchToFrame(frame)`, back with `App.Browser.SwitchToDefault()` |
| `Table` | `table` | Row/cell API (`GetRow`, `GetCell`, `GetItems<T>()`) after `.SetColumn("Header")` calls |

### There Is No…

`H1`, `ListItem`, `Paragraph`, `ComboBox`, `Link`, `Checkbox` (lowercase b), or `Dropdown` in BELLATRIX Web. Use `Heading`, `Div`/`Span`, `Select`, `Anchor`, `CheckBox`.

### Only Some Components Can Be Clicked

`Div`, `Span`, `Label`, `Heading`, and `Option` have **no `Click()`**. To click a `div`/`span`/`li` that acts as a button (role `button`, `tab`, `menuitem`, custom checkbox), type it as `Button` — any element can be wrapped as a `Button` — or write a custom component (below).

---

## Driving the Same Controls Live with the CLI

During scenario execution (`bellatrix-web-execute-scenario`) the same controls are exercised with `bellatrix web` verbs. Map each intent to the verb that matches the control:

| Intent | CLI verb | Notes |
|--------|----------|-------|
| Click a button / link / tab / checkbox / radio | `click --locator <s>` | Toggles a checkbox |
| Enter text | `type-text --locator <s> --text "…"` | Clears the field first, then types |
| Clear a field | `clear --locator <s>` | |
| Choose a `<select>` option | `select --locator <s> --value "<visible text>"` | `--value` is the option's visible text |
| Read text | `get-text --locator <s>` | The element's visible text |
| Read an attribute / property | `get-attribute --locator <s> --attribute <name>` | e.g. `value`, `checked`, `href`, `disabled`, `aria-expanded` |
| Visibility / enabled state | `is-visible` / `is-enabled` | |
| Wait until present | `wait-for-element --locator <s> [--timeout-ms N]` | Check `found` in the response |

All element verbs accept `--strategy css|xpath|id|class|name|tag|linktext|partiallinktext` (CSS by default; XPath auto-detected when the locator starts with `/` or `(/`).

### Reading the Element Type from a Snapshot

Each snapshot node carries `Tag` (lowercase), `Type`, and `Role` — use them to choose the component:

| Snapshot | Means |
|----------|-------|
| `Tag: input`, `Type: password` | Password field |
| `Tag: input`, `Type: checkbox` | Checkbox |
| `Tag: div`, `Role: button` | Clickable non-button element — type it as a clickable component |
| `Tag: a`, `Href: …` | Link |
| `Tag: select` | Native dropdown — the CLI `select` verb works; a custom dropdown needs click + re-snapshot + click |

`--interactive-only` keeps tags `button`, `input`, `select`, `a`, `textarea`, `option` and roles `button`, `link`, `checkbox`, `radio`, `textbox`, `combobox`, `menuitem`, `tab`, `switch`.

Always resolve a locator from a fresh snapshot, interact, and re-snapshot after the DOM changes. Prefer `data-testid`; fall back per the ladder in `bellatrix-web-locators-and-waits`.

## Creating Components (C#)

### Single Component (factory methods on `App.Components`)

```csharp
// data-testid (PREFERRED)
App.Components.CreateByCss<Button>("[data-testid='login-submit']")

// Static id
App.Components.CreateById<TextField>("email")

// name attribute (there is no CreateByName in BELLATRIX Web)
App.Components.CreateByCss<TextField>("[name='username']")

// Text / structural (lowercase "p" in Xpath!)
App.Components.CreateByXpath<Button>("//button[normalize-space()='Log In']")

// Links
App.Components.CreateByLinkText<Anchor>("Sign in")

// Attribute / text / class fragments
App.Components.CreateByAttributesContaining<Anchor>("data-product_id", "28")
App.Components.CreateByInnerTextContaining<Heading>("Billing details")
App.Components.CreateByClassContaining<Div>("woocommerce-message")
App.Components.CreateByValueContaining<Button>("Update cart")
```

See `bellatrix-web-locators-and-waits` for the full factory table and which locator to prefer.

### Component Collections (`CreateAllBy*`)

```csharp
// Returns ComponentsList<T> (IEnumerable<T>)
ComponentsList<Number> quantityBoxes = App.Components.CreateAllByClassContaining<Number>("input-text qty text");
ComponentsList<Anchor> navLinks = App.Components.CreateAllByCss<Anchor>("nav a");

var count = quantityBoxes.Count();
var first = quantityBoxes[0];
quantityBoxes.ForEach(q => q.SetNumber(1));
foreach (var link in navLinks)
{
    link.ValidateIsVisible();
}

IEnumerable<string> linkTexts = navLinks.SelectInnerText();   // for components with InnerText
```

### Nested (Scoped) Component Creation

Every factory is also available on a component, searching only inside it:

```csharp
var cartRow = App.Components.CreateByXpath<Div>("//tr[td[normalize-space()='Falcon 9']]");
var removeLink = cartRow.CreateByCss<Anchor>("a.remove");
var priceCell = cartRow.CreateByXpath<Span>(".//td[@class='product-price']//span");   // ".//" = relative
removeLink.Click();
```

Use `.//` at the start of a scoped XPath — a plain `//` searches the whole document again.

### MANDATORY: Use BELLATRIX Factories — Never Drop to the Raw Driver

Inside page objects, actions, sections, tests, and custom components, ALWAYS locate elements through `App.Components.Create*` / `CreateAllBy*` or the scoped component factories. **Never** call `WrappedDriver.FindElement(s)(By.…)` or build raw Selenium `By` locators in test-project code.

**Why:** factory-returned components carry wait strategies, `Validate*` methods, BDD logging, highlighting, screenshots on failure, and the retry pipeline. Raw `FindElements` bypasses all of it.

#### Anti-pattern (DO NOT DO THIS)

```csharp
// WRONG — raw Selenium bypasses BELLATRIX
var rows = App.Browser.WrappedDriver.FindElements(By.CssSelector("tr.cart_item"));
rows[0].FindElement(By.CssSelector("a.remove")).Click();
```

#### Correct — BELLATRIX factories

```csharp
var removeLinks = App.Components.CreateAllByCss<Anchor>("tr.cart_item a.remove");
if (removeLinks.Count() > 0)
{
    removeLinks[0].Click();
}
```

#### Inside custom components (no `App` in scope)

A custom component inheriting `Component` has the protected `ComponentCreateService` field and the scoped `this.Create*` factories:

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Components;

public class ProductCard : Component
{
    // scoped to this card
    public Anchor AddToCartButton => this.CreateByCss<Anchor>("a.add_to_cart_button");

    public Heading Title => this.CreateByTag<Heading>("h2");

    // page-wide lookup from inside a component
    public ComponentsList<Anchor> AllAddToCartButtons()
        => ComponentCreateService.CreateAllByCss<Anchor>("a.add_to_cart_button");
}
```

#### The only times raw driver access is acceptable

Genuinely low-level operations BELLATRIX does not expose — and even then never to find or click elements. Prefer the services first: `App.Browser` (tabs, frames, storage), `App.Interactions` (keyboard chords, drag-and-drop, hover chains), `App.JavaScript` (scripts), `App.Dialogs` (native alerts), `App.Cookies`.

---

## Base Component Members (available on all components)

```csharp
// Presence and visibility — return false instead of throwing, but first wait up to elementToExistTimeout (plus registered To* waits)
component.IsPresent        // bool — exists in the DOM
component.IsVisible        // bool — displayed

// Identity and state
component.TagName          // e.g. "button"
component.CssClass         // class attribute
component.GetAttribute("aria-expanded")
component.GetCssValue("color")
component.GetTitle()       // title attribute
component.GetStyle()       // style attribute

// Geometry
component.Location         // System.Drawing.Point
component.Size             // System.Drawing.Size

// Actions available on every component
component.ScrollToVisible();
component.Focus();
component.Hide();
component.SetAttribute("value", "x");   // DOM attribute (JavaScript) — test setup only
component.WaitToBe();                    // force the registered waits now
component.TakeScreenshot();              // element screenshot

// Logging names
component.ComponentName
component.PageName
```

---

## Custom Components

Add a custom component under `Components/` when a built-in is missing behavior or behaves wrongly for a specific widget. Never modify BELLATRIX source.

### Option A — New component inheriting `Component`

```csharp
using Bellatrix.Web;
using Bellatrix.Web.Contracts;

namespace MyProject.WebTests.Components;

// Implementing IComponentInnerText makes ValidateInnerTextIs/Contains available on it
public class Toast : Component, IComponentInnerText
{
    public virtual string InnerText => WrappedElement.Text;

    public bool IsError => CssClass.Contains("toast-error");

    public void Dismiss() => this.CreateByCss<Button>("button[aria-label='Close']").Click();
}
```

### Option B — Child component overriding a built-in

```csharp
using Bellatrix.Web;

namespace MyProject.WebTests.Components;

public class ScrollingButton : Button
{
    // Sticky headers sometimes cover the button — scroll it into view first
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

Rules: one file per component, filename = class name; keep components control-level (business workflow stays in `.Actions.cs`); reuse them across pages.
