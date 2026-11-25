#!/usr/bin/env node
// Re-creates the documentation screenshots from a SPECTRA demo repository.
//
//   cd docs/scripts/docs-screenshots && npm install && npx playwright install chromium
//   node capture.js --demo C:/SourceCode/AutomateThePlanet_SystemTests --spectra "spectra"
//
// Options
//   --demo <path>      demo repository (an initialised SPECTRA workspace with tests)        [required]
//   --spectra <cmd>    the SPECTRA command or executable to run (use the build you document)  [default: spectra]
//   --test <id>        test case for the detail panel                                         [default: first test]
//   --console          also capture the execution web console (run-console.webp). Needs an active run
//                      (`spectra run start <suite>`); the console is started and stopped by the script.
//   --rename <a>=<b>   replace the text <a> with <b> in every captured page before the screenshot, for
//                      privacy (for example a real name in the runs' "Executed By"). Nothing else changes.
//   --seam <label>     also capture the generate/criteria progress page (.spectra/seam-progress.html)
//                      in its current state as progress-<label>.webp. That page only changes during a
//                      real agent run, so run this option while one is in progress.
//
// What it runs in the demo repository (it writes there):
//   <spectra> ai analyze --coverage --auto-link    -> .spectra-progress.html (coverage), .spectra-result.json
//   <spectra> dashboard --output <temp dir>        -> the dashboard is built OUTSIDE the demo repository
//
// Every image is captured at deviceScaleFactor 2, cropped to the relevant part, saved as WebP of at most
// 200 KB, and its display size is written to docs/_data/screenshots.yml (see images.js).

const { chromium } = require('playwright');
const { execSync } = require('child_process');
const fs = require('fs');
const http = require('http');
const os = require('os');
const path = require('path');
const { saveWebp, cropToContent, SCALE } = require('./images');

function arg(name, fallback) {
  const i = process.argv.indexOf('--' + name);
  return i > 0 ? process.argv[i + 1] : fallback;
}

const demo = arg('demo');
if (!demo || !fs.existsSync(path.join(demo, 'spectra.config.json'))) {
  console.error('--demo must point to a SPECTRA workspace (spectra.config.json not found)');
  process.exit(2);
}
const spectra = arg('spectra', 'spectra');
const testId = arg('test');
const seamLabel = arg('seam');
const rename = arg('rename') ? arg('rename').split('=') : null;

/** Replaces the --rename text in the page (text nodes and input values) right before a screenshot. */
async function applyRename(page) {
  if (!rename) return;
  await page.evaluate(([from, to]) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) {
      if (walker.currentNode.nodeValue.includes(from)) walker.currentNode.nodeValue = walker.currentNode.nodeValue.split(from).join(to);
    }
    document.querySelectorAll('input, textarea').forEach(e => { if (e.value.includes(from)) e.value = e.value.split(from).join(to); });
  }, rename);
}

const run = cmd => execSync(`${spectra.includes(' ') ? `"${spectra}"` : spectra} ${cmd}`, { cwd: demo, stdio: 'pipe' }).toString();

/** A static file server on a free port. */
function serve(root) {
  const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png' };
  const server = http.createServer((req, res) => {
    const rel = decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html';
    const file = path.join(root, rel);
    if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server)));
}

/** Captures one element, at most maxHeight CSS px tall. belowHeader: start under the page's sticky .header. */
async function shot(page, selector, name, maxHeight = 1000, belowHeader = false) {
  await applyRename(page);
  const el = await page.$(selector);
  if (!el) { console.warn(`skipped ${name}: ${selector} not found`); return; }
  const box = await el.boundingBox();
  // Start below a page header that overlaps the element (the dashboard's), never above the element.
  const headerBottom = !belowHeader ? 0 : await page.evaluate(() => { const h = document.querySelector('.header'); return h ? h.getBoundingClientRect().bottom + window.scrollY : 0; });
  const y = box.y < headerBottom && box.y + box.height > headerBottom ? headerBottom + 8 : box.y;
  const png = await page.screenshot({ clip: { x: box.x, y, width: box.width, height: Math.min(box.y + box.height - y, maxHeight) }, fullPage: true });
  await saveWebp(png, name);
}

const newPage = (browser, width, height) => browser.newPage({ viewport: { width, height }, deviceScaleFactor: SCALE });

(async () => {
  const browser = await chromium.launch();

  // 1. Coverage analysis and its progress page (cropped to the cards).
  run('ai analyze --coverage --auto-link');
  const progress = await newPage(browser, 1000, 700);
  await progress.goto('file:///' + path.join(demo, '.spectra-progress.html').split(path.sep).join('/'));
  await progress.waitForTimeout(500);
  await saveWebp(await cropToContent(await progress.screenshot()), 'progress-coverage-done');
  await progress.close();

  // 2. Dashboard, built outside the demo repository. 1280 wide: the content column is about 960 CSS px.
  const site = fs.mkdtempSync(path.join(os.tmpdir(), 'spectra-dashboard-'));
  run(`dashboard --output "${site}"`);
  const server = await serve(site);
  const base = `http://127.0.0.1:${server.address().port}/`;

  const page = await newPage(browser, 1280, 900);
  await page.goto(base, { waitUntil: 'networkidle' });
  // The dashboard header is sticky; in a full-page clip it would cover the top of #content.
  await page.addStyleTag({ content: '.header { position: static !important; }' });
  await shot(page, '#content', 'dashboard-suites', 530, true);

  await page.click('[data-view="tests"]');
  await page.waitForTimeout(300);
  const id = testId || await page.$eval('.test-row', el => (el.getAttribute('onclick') || '').match(/'([^']+)'/)[1]);
  await page.evaluate(i => window.showTestDetail(i), id);
  await page.waitForTimeout(400);
  await shot(page, '#test-detail-panel', 'dashboard-test-detail', 900);
  await page.evaluate(() => window.closeDetailPanel && window.closeDetailPanel());

  await page.click('[data-view="coverage"]');
  await page.waitForTimeout(800);
  await shot(page, '#content', 'dashboard-coverage', 625, true);

  await page.click('[data-view="runs"]');
  await page.waitForTimeout(500);
  await shot(page, '#content', 'dashboard-runs', 1000, true);
  // Prefer a completed run: a running one has no per-test results yet.
  const rows = await page.$$('.run-table-row');
  let row = rows[0];
  for (const r of rows) {
    if (/completed/i.test(await r.innerText())) { row = r; break; }
  }
  if (row) {
    await row.click();
    await page.waitForTimeout(500);
    await shot(page, '#content', 'dashboard-run-report', 1300, true);
  } else {
    console.warn('skipped dashboard-run-report: no runs in this workspace');
  }

  const mobile = await newPage(browser, 390, 844);
  await mobile.goto(base, { waitUntil: 'networkidle' });
  await applyRename(mobile);
  await saveWebp(await mobile.screenshot(), 'dashboard-suites-mobile');
  server.close();

  // 3. Optional: the execution web console for the active run (it shows the run's real state).
  if (process.argv.includes('--console')) {
    run('run console');
    const { Url } = JSON.parse(fs.readFileSync(path.join(demo, '.execution', 'console.json'), 'utf8'));
    const con = await newPage(browser, 1280, 900);
    await con.goto(Url, { waitUntil: 'networkidle' });
    await con.waitForTimeout(1500);
    await shot(con, '.container', 'run-console', 1300);
    run('run console --stop');
  }

  // 4. Optional: the live generate/criteria progress page, in its current state.
  if (seamLabel) {
    const seamServer = await serve(path.join(demo, '.spectra'));
    const seam = await newPage(browser, 1000, 700);
    await seam.goto(`http://127.0.0.1:${seamServer.address().port}/seam-progress.html`, { waitUntil: 'networkidle' });
    await seam.waitForTimeout(2500); // the page polls progress.json every 1.5 s
    await saveWebp(await cropToContent(await seam.screenshot()), 'progress-' + seamLabel);
    seamServer.close();
  }

  await browser.close();
  fs.rmSync(site, { recursive: true, force: true });
})();
