# SPECTRA development guide

The full contributor guide. The short version is on the documentation site: [Contributing](../DEVELOPMENT.md).

Building, testing, and running SPECTRA locally.

Related: [Getting Started](../getting-started.md) | [CLI Reference](../cli-reference.md) | [Architecture](../architecture/overview.md)

---

> As of v2, the `Spectra.MCP` project was removed entirely, so execution is now CLI-only
> (`spectra run`). If you're looking for MCP server build/run instructions from an older version of
> this page, they no longer apply. See
> [Claude Code v2 vs. the GitHub Copilot SDK v1](../claude-code-v2-migration.md).

## Quick Start

```bash
# Build and install the tool
dotnet build -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -p:NoWarn=CA1062
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI

# Verify
spectra --help
```

## Prerequisites

- .NET 8.0+ SDK
- Git
- [Claude Code](https://claude.com/claude-code), which is needed to actually drive generation/analysis/
  criteria/update/verification turns, since the CLI alone only does the deterministic bookkeeping

## Project Structure

```text
src/
├── Spectra.CLI/        # CLI: authoring commands (compile-*/ingest-* seam) + spectra run
├── Spectra.Core/        # Shared library (parsing, validation, models, coverage)
├── Spectra.Execution/    # Transport-neutral execution engine, driven solely by spectra run
└── Spectra.GitHub/       # GitHub integration (future)

tests/
├── Spectra.CLI.Tests/          # CLI unit/integration tests
├── Spectra.Core.Tests/         # Core library tests
├── Spectra.Execution.Tests/    # Execution engine tests
├── Spectra.Integration.Tests/  # Cross-spec generation→persistence→execution
└── TestFixtures/                # Sample test data
    ├── docs/              # Sample documentation
    └── test-cases/        # Sample test case suites with _index.json
```

## Building

```bash
dotnet build                                    # Entire solution
dotnet build src/Spectra.CLI/Spectra.CLI.csproj # Specific project
dotnet build -c Release                         # Release mode
```

## Running Tests

```bash
dotnet test                          # All tests
dotnet test tests/Spectra.CLI.Tests/ # Specific project
dotnet test -v n                     # Verbose output
```

## Running Locally

### CLI (from source)

```bash
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures
dotnet run --project src/Spectra.CLI -- dashboard --output ./site
dotnet run --project src/Spectra.CLI -- run start checkout --priorities high
```

There is no `dotnet run … -- ai generate …` anymore, because generation is skill-driven from inside
Claude Code, not a standalone CLI command. See
[Generation (in-session via the `spectra-generate` skill)](../cli-reference.md#generation-in-session-via-the-spectra-generate-skill).

### Install as a Global Tool

```bash
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release
dotnet tool uninstall -g Spectra.CLI 2>/dev/null
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI

# Run from anywhere
spectra validate
```

### Dashboard (from source)

```bash
# Generate
dotnet run --project src/Spectra.CLI -- dashboard --output ./site

# Serve
cd site && python -m http.server 8080
# or: npx serve site
# or: dotnet serve -d site -p 8080
```

### Documentation site (from source)

The docs are a Jekyll site in `docs/` (theme just-the-docs). With Ruby 3.3 and bundler installed:

```bash
cd docs
bundle install
bundle exec jekyll serve --baseurl /Spectra   # http://127.0.0.1:4000/Spectra/
```

After a build, check that every page's Markdown was converted:

```bash
bundle exec jekyll build
node scripts/check-rendered-markdown.js _site
```

The check fails when a page shows raw `###`, table separators, code fences or `<s>` strikethrough
outside code. The usual cause is a line that starts with an HTML-like tag, for example a code span
wrapped so the next line begins with `<path>`. kramdown then treats the rest of the block as raw HTML.
Keep such code spans on one line.

### USAGE.md and CUSTOMIZATION.md: two separate copies

`spectra init` writes `USAGE.md` and `CUSTOMIZATION.md` into a user's repository, where the user's AI agent reads
them (`spectra update-skills` refreshes `CUSTOMIZATION.md`). Their source is `src/Spectra.CLI/Templates/USAGE.md` and
`src/Spectra.CLI/Templates/CUSTOMIZATION.md`, embedded in the CLI (`Spectra.CLI.csproj`).

The website pages `docs/usage.md` and `docs/customization.md` are separate, shorter pages for people. Editing a
website page does not change what `init` installs, and the other way round. When a workflow changes, update both.
The init goldens (`tests/Spectra.CLI.Tests/Golden/`) hash the templates, so a template edit needs a golden
re-capture (`SPECTRA_GOLDEN_RECORD=1`).

### Documentation screenshots

The images in `docs/assets/images/screenshots/` are real captures from a SPECTRA demo repository (an initialised
workspace with tests, for example the Windows Calculator demo). Never edit or mock them; re-capture them instead.
`docs/scripts/docs-screenshots/capture.js` does that with Playwright and saves each image as a cropped WebP of at
most 200 KB:

```bash
cd docs/scripts/docs-screenshots
npm install
npx playwright install chromium
node capture.js --demo <path-to-demo-repo> --spectra <path-to-the-spectra-build-you-document>
```

| Option | What it adds |
|---|---|
| (none) | `progress-coverage-done`, and the dashboard: `dashboard-suites`, `-test-detail`, `-coverage`, `-runs`, `-run-report`, `-suites-mobile` |
| `--test TC-123` | The test shown in the detail panel (default: the first test) |
| `--console` | `run-console`, the execution web console. Needs an active run (`spectra run start <suite>`) |
| `--seam <label>` | `progress-<label>`, the generate or criteria progress page in its current state |

The script writes to the demo repository: `ai analyze --coverage --auto-link` updates its coverage files and
`.spectra-progress.html`; the dashboard is built in a temporary folder. The generate and criteria progress pages
(`progress-generate-*`, `progress-criteria-*`) change only while an agent runs, so run `--seam` once per phase
during a real "generate test cases" or "extract criteria" conversation. To re-crop a raw PNG of such a capture:
`node images.js crop <in.png> <name>`.

Every image is captured at device scale factor 2 and cropped to the relevant part (the progress pages to their cards,
the dashboard to its content area). `images.js` writes each image's display size (half its pixel size) to
`docs/_data/screenshots.yml`; the page include reads it for the `width` and `height` attributes, so the browser
reserves the right space and never shows an image larger than it was captured.

Use the include, never a bare `<img>`:

```liquid
{% include screenshot.html file="dashboard-coverage" alt="What the image shows" caption="Optional." %}
{% include screenshot.html narrow=true file="progress-generate-critic" alt="…" %}

<div class="screenshot-pair">
{% include screenshot.html file="dashboard-suites" alt="…" %}
{% include screenshot.html phone=true file="dashboard-suites-mobile" alt="…" %}
</div>
```

Desktop images show at most 760 px wide (`narrow`: 560 px); phone captures (`phone=true`) at most 340 px, in a phone
frame; a `screenshot-pair` puts them side by side and stacks them on small screens. Each image links to its full-size
file, which `assets/js/screenshot-lightbox.js` opens in a lightbox (Esc or the close button closes it). Write every
new image's alt text in the page that uses it.

## Using TestFixtures

The `tests/TestFixtures/` folder contains ready-to-use sample data:

```bash
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures
dotnet run --project src/Spectra.CLI -- run list-suites --path tests/TestFixtures
```

Available suites in fixtures: auth (3 test cases), checkout (1 test case).

## Troubleshooting

### Build errors (CA1062 warnings)

```bash
dotnet build -p:NoWarn=CA1062
```

### "Suite not found"

Ensure your test case folder has `test-cases/` with suite subdirectories containing valid `_index.json` files.

### "Index stale"

```bash
spectra index --rebuild
```

### "No spectra.config.json found"

```bash
spectra init
```

### A generation/analysis/criteria/update turn doesn't seem to be running

There's no AI provider to check, since SPECTRA makes no model calls of its own. Make sure you're
actually driving the flow from inside Claude Code (e.g. "generate test cases for the checkout
suite"), not calling `spectra ai compile-prompt`/`ingest-tests` outside a session with nothing to
answer the compiled prompt.
