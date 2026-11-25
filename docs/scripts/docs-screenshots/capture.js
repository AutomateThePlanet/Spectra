#!/usr/bin/env node
// Re-creates the documentation screenshots from a SPECTRA demo repository.
//
//   cd docs/scripts/docs-screenshots && npm install && npx playwright install chromium
//   node capture.js --demo C:/SourceCode/AutomateThePlanet_SystemTests --spectra "spectra"
//
// Options
//   --demo <path>      demo repository (an initialised SPECTRA workspace with tests)        [required]
//   --spectra <cmd>    the SPECTRA command or executable to run (use the build you document)  [default: spectra]
//   --out <dir>        where the WebP files go                  [default: docs/assets/images/screenshots]
//   --test <id>        test case for the detail panel                                         [default: first test]
//   --seam <label>     also capture the generate/criteria progress page (.spectra/seam-progress.html)
//                      in its current state as progress-<label>.webp. That page only changes during a
//                      real agent run, so run this option while one is in progress.
//   --console          also capture the execution web console (run-console.webp). Needs an active run
//                      (`spectra run start <suite>`); the console is started and stopped by the script.
//
// What it runs in the demo repository (it writes there):
//   <spectra> ai analyze --coverage --auto-link    -> .spectra-progress.html (coverage), .spectra-result.json
//   <spectra> dashboard --output <temp dir>        -> the dashboard is built OUTSIDE the demo repository
// Every image is cropped to the relevant part and saved as WebP of at most 200 KB.

const { chromium } = require('playwright');
const sharp = require('sharp');
const { execSync } = require('child_process');
const fs = require('fs');
const http = require('http');
const os = require('os');
const path = require('path');

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
const out = path.resolve(arg('out', path.join(__dirname, '..', '..', 'assets', 'images', 'screenshots')));
const testId = arg('test');
const seamLabel = arg('seam');
fs.mkdirSync(out, { recursive: true });

const run = cmd => execSync(`${spectra.includes(' ') ? `"${spectra}"` : spectra} ${cmd}`, { cwd: demo, stdio: 'pipe' }).toString();

/** Saves a PNG buffer as WebP of at most 200 KB (lowering quality, then width, until it fits). */
async function saveWebp(png, name) {
  const file = path.join(out, name + '.webp');
  let width = Math.min((await sharp(png).metadata()).width, 1600);
  for (let quality = 82; ; quality -= 8) {
    const buf = await sharp(png).resize({ width, withoutEnlargement: true }).webp({ quality }).toBuffer();
    if (buf.length <= 200 * 1024 || (quality <= 42 && width <= 900)) {
      fs.writeFileSync(file, buf);
      console.log(`${name}.webp  ${Math.round(buf.length / 1024)} KB`);
      return;
    }
    if (quality <= 42) { quality = 90; width = Math.round(width * 0.85); }
  }
}

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

async function shot(page, selector, name, pad = 0) {
  const el = await page.$(selector);
  if (!el) { console.warn(`skipped ${name}: ${selector} not found`); return; }
  const box = await el.boundingBox();
  const png = await page.screenshot({
    clip: { x: Math.max(0, box.x - pad), y: Math.max(0, box.y - pad), width: box.width + 2 * pad, height: Math.min(box.height + 2 * pad, 1400) },
    fullPage: true,
  });
  await saveWebp(png, name);
}

(async () => {
  const browser = await chromium.launch();

  // 1. Coverage analysis and its progress page.
  run('ai analyze --coverage --auto-link');
  const progress = await browser.newPage({ viewport: { width: 1100, height: 800 }, deviceScaleFactor: 2 });
  await progress.goto('file:///' + path.join(demo, '.spectra-progress.html').split(path.sep).join('/'));
  await progress.waitForTimeout(500);
  await saveWebp(await progress.screenshot({ fullPage: true }), 'progress-coverage-done');
  await progress.close();

  // 2. Dashboard, built outside the demo repository.
  const site = fs.mkdtempSync(path.join(os.tmpdir(), 'spectra-dashboard-'));
  run(`dashboard --output "${site}"`);
  const server = await serve(site);
  const base = `http://127.0.0.1:${server.address().port}/`;

  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 });
  await page.goto(base, { waitUntil: 'networkidle' });
  await shot(page, '#app', 'dashboard-suites');

  await page.click('[data-view="tests"]');
  await page.waitForTimeout(300);
  const id = testId || await page.$eval('.test-row', el => (el.getAttribute('onclick') || '').match(/'([^']+)'/)[1]);
  await page.evaluate(i => window.showTestDetail(i), id);
  await page.waitForTimeout(400);
  await shot(page, '#test-detail-panel', 'dashboard-test-detail');
  await page.evaluate(() => window.closeDetailPanel && window.closeDetailPanel());

  await page.click('[data-view="coverage"]');
  await page.waitForTimeout(800);
  await shot(page, '#app', 'dashboard-coverage');

  await page.click('[data-view="runs"]');
  await page.waitForTimeout(500);
  await shot(page, '#app', 'dashboard-runs');
  // Prefer a completed run: a running one has no per-test results yet.
  const rows = await page.$$('.run-table-row');
  let row = rows[0];
  for (const r of rows) {
    if (/completed/i.test(await r.innerText())) { row = r; break; }
  }
  if (row) {
    await row.click();
    await page.waitForTimeout(500);
    await shot(page, '#app', 'dashboard-run-report');
  } else {
    console.warn('skipped dashboard-run-report: no runs in this workspace');
  }

  const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
  await mobile.goto(base, { waitUntil: 'networkidle' });
  await saveWebp(await mobile.screenshot({ fullPage: false }), 'dashboard-suites-mobile');
  server.close();

  // 3. Optional: the execution web console for the active run (it shows the run's real state).
  if (process.argv.includes('--console')) {
    run('run console');
    const { Url } = JSON.parse(fs.readFileSync(path.join(demo, '.execution', 'console.json'), 'utf8'));
    const con = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 });
    await con.goto(Url, { waitUntil: 'networkidle' });
    await con.waitForTimeout(1500);
    await saveWebp(await con.screenshot({ fullPage: true }), 'run-console');
    run('run console --stop');
  }

  // 4. Optional: the live generate/criteria progress page, in its current state.
  if (seamLabel) {
    const seamServer = await serve(path.join(demo, '.spectra'));
    const seam = await browser.newPage({ viewport: { width: 1000, height: 700 }, deviceScaleFactor: 2 });
    await seam.goto(`http://127.0.0.1:${seamServer.address().port}/seam-progress.html`, { waitUntil: 'networkidle' });
    await seam.waitForTimeout(2500); // the page polls progress.json every 1.5 s
    await shot(seam, 'body', 'progress-' + seamLabel);
    seamServer.close();
  }

  await browser.close();
  fs.rmSync(site, { recursive: true, force: true });
})();
