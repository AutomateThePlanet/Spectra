#!/usr/bin/env node
// Colour-contrast check (axe-core "color-contrast" rule) of every page in a built Jekyll site, at 1440 and 390 px.
//
//   cd docs/scripts/a11y && npm ci && npx playwright install chromium
//   node check-contrast.js ../../_site --base /Spectra            # warn only: always exits 0
//   node check-contrast.js ../../_site --base /Spectra --strict   # exits 1 when anything fails
//
// In GitHub Actions each failure is printed as a ::warning annotation, so it shows on the run without failing it.

const { chromium } = require('playwright');
const axeSource = require('axe-core').source;
const fs = require('fs');
const http = require('http');
const path = require('path');

const args = process.argv.slice(2);
const site = path.resolve(args.find(a => !a.startsWith('--')) || '_site');
const base = ((i => (i >= 0 ? args[i + 1] : ''))(args.indexOf('--base')) || '').replace(/\/$/, '');
const strict = args.includes('--strict');
const inCi = !!process.env.GITHUB_ACTIONS;

const types = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.webp': 'image/webp', '.woff2': 'font/woff2', '.json': 'application/json' };
function serve() {
  const server = http.createServer((req, res) => {
    let rel = decodeURIComponent(req.url.split('?')[0]);
    if (base && rel.startsWith(base)) rel = rel.slice(base.length);
    let file = path.join(site, rel);
    if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
    if (!fs.existsSync(file) && fs.existsSync(file + '.html')) file += '.html';
    if (!file.startsWith(site) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream' });
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server)));
}

function pages() {
  const out = [];
  (function walk(d) {
    for (const f of fs.readdirSync(d)) {
      const p = path.join(d, f);
      if (fs.statSync(p).isDirectory()) { if (f !== 'assets') walk(p); }
      else if (f.endsWith('.html') && f !== '404.html') out.push(path.relative(site, p).split(path.sep).join('/'));
    }
  })(site);
  return out.sort();
}

(async () => {
  const server = await serve();
  const root = `http://127.0.0.1:${server.address().port}${base}/`;
  const browser = await chromium.launch();
  let failures = 0;
  for (const width of [1440, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    for (const rel of pages()) {
      await page.goto(root + rel, { waitUntil: 'networkidle' });
      await page.addScriptTag({ content: axeSource });
      const violations = await page.evaluate(async () =>
        (await window.axe.run(document, { runOnly: { type: 'rule', values: ['color-contrast'] } })).violations);
      for (const v of violations) {
        for (const node of v.nodes) {
          failures++;
          const msg = `${rel} @${width}px: ${node.target.join(' ')} — ${(node.any[0] && node.any[0].message) || v.help}`;
          console.log(inCi ? `::warning file=docs/${rel.replace(/\.html$/, '.md')}::contrast ${msg}` : msg);
        }
      }
    }
    await page.close();
  }
  await browser.close();
  server.close();
  console.log(`color-contrast: ${failures} element(s) below WCAG AA${strict ? '' : ' (warn only)'}`);
  process.exit(strict && failures ? 1 : 0);
})();
