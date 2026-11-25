---
title: Contributing
nav_order: 6
description: "Build SPECTRA from source, run its tests, preview these docs and refresh their screenshots. Links to the contributing rules and the full developer guide."
---

# Contributing

How to build SPECTRA, run its tests and work on this documentation site. The rules for issues and pull requests are
in [CONTRIBUTING.md](https://github.com/AutomateThePlanet/Spectra/blob/main/CONTRIBUTING.md). The full developer
guide (project structure, running locally, troubleshooting) is
[DEVELOPMENT-GUIDE.md](https://github.com/AutomateThePlanet/Spectra/blob/main/docs/contributing/DEVELOPMENT-GUIDE.md)
on GitHub.

## Build and test

You need the .NET 8 SDK.

```bash
dotnet build                                    # warnings are errors
dotnet test                                     # all test projects
dotnet run --project src/Spectra.CLI -- --help  # the CLI from source
```

Golden tests compare output byte for byte. After an intended change, re-record them with
`SPECTRA_GOLDEN_RECORD=1 dotnet test` and review the diff.

## Preview the documentation

The site is Jekyll with the just-the-docs theme, in `docs/`. With Ruby 3.3 and bundler:

```bash
cd docs
bundle install
bundle exec jekyll serve --baseurl /Spectra      # http://127.0.0.1:4000/Spectra/
```

Before you commit a docs change, build the site and check that every page's Markdown was converted:

```bash
bundle exec jekyll build
node scripts/check-rendered-markdown.js _site
```

The check fails on raw `###`, table separators, code fences or `<s>` outside code. The usual cause is a line that
starts with a tag, such as a code span wrapped so the next line begins with `<path>`.

The docs workflow also checks colour contrast (axe-core, WCAG AA) on every page at 1440 and 390 px. For now it only
warns. To run it locally: `cd docs/scripts/a11y && npm ci && npx playwright install chromium`, then
`node check-contrast.js ../../_site --base /Spectra`.

`spectra init` installs its own copies of `USAGE.md` and `CUSTOMIZATION.md` from `src/Spectra.CLI/Templates/`. The
website pages are separate; when a workflow changes, update both.

## Refresh the screenshots

The screenshots are real captures from a demo repository; never edit or mock them. To re-create them:

```bash
cd docs/scripts/docs-screenshots
npm install
npx playwright install chromium
node capture.js --demo <path-to-demo-repo> --spectra <path-to-the-spectra-build-you-document>
```

Add `--console` for the run console (it needs an active run) and `--seam <label>` for the generate or criteria
progress page during a real agent run. Add `--rename "<your name>=demo-user"` so no real name appears in the run screenshots. In a page, always use the include:

```liquid
{% raw %}{% include screenshot.html file="dashboard-coverage" alt="What the image shows" %}{% endraw %}
```

The options (narrow, phone frame, side-by-side pairs) and how sizes are recorded are in the
[developer guide](https://github.com/AutomateThePlanet/Spectra/blob/main/docs/contributing/DEVELOPMENT-GUIDE.md#documentation-screenshots).

## Release check in each AI agent

Automated tests check every file `spectra init --agent` writes. Whether each agent actually loads and follows them is
checked by hand once per release, with the
[live verification checklist](https://github.com/AutomateThePlanet/Spectra/blob/main/docs/contributing/LIVE-VERIFICATION-CHECKLIST.md)
on GitHub.
