#!/usr/bin/env node
// Search test for the built SPECTRA docs: typing a query in the header search shows results, also when the first
// request for lunr.min.js fails (the "lunr is not defined" race: assets/js/just-the-docs.js must wait for lunr and
// request it again), and when the query is typed before the search index has loaded.
//   cd docs/scripts/code-blocks && npm ci && npx playwright install chromium   (once)
//   node check-search.mjs <path-to-_site> --base /Spectra [--runs 20]
// Exits 1 when any run fails.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { chromium } from 'playwright';

const args = process.argv.slice(2);
const site = path.resolve(args.find((a) => !a.startsWith('--') && !/^\d+$/.test(a)) || '_site');
const opt = (k, d) => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : d; };
const base = (opt('--base', '') || '').replace(/\/$/, '');
const runs = Number(opt('--runs', 20));
const types = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.webp': 'image/webp', '.woff2': 'font/woff2' };
const server = http.createServer((req, res) => {
  let rel = decodeURIComponent(req.url.split('?')[0]);
  if (base && rel.startsWith(base)) rel = rel.slice(base.length);
  let file = path.join(site, rel);
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!file.startsWith(site) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const origin = `http://127.0.0.1:${server.address().port}`;

const browser = await chromium.launch();
const failures = [];
async function searchOnce(run, dropFirstLunr, slowIndex = false) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  let dropped = false;
  if (dropFirstLunr) {
    await page.route('**/vendor/lunr.min.js*', (route) => {
      if (!dropped) { dropped = true; return route.abort('connectionreset'); }
      return route.continue();
    });
  }
  if (slowIndex) {
    // the index arrives 1.5 s late: the query is typed before search is ready and must still get results
    await page.route('**/search-data.json*', async (route) => { await new Promise((r) => setTimeout(r, 1500)); return route.continue(); });
  }
  try {
    await page.goto(`${origin}${base}/getting-started.html`, { waitUntil: slowIndex ? 'domcontentloaded' : 'load' });
    const input = page.locator('#search-input');
    await input.click();
    await input.pressSequentially('coverage', { delay: 20 }); // real key presses (the theme listens to keyup)
    await page.locator('#search-results .search-result').first().waitFor({ state: 'visible', timeout: 8000 });
    const n = await page.locator('#search-results .search-result').count();
    if (!n) failures.push(`run ${run}${dropFirstLunr ? ' (first lunr request dropped)' : slowIndex ? ' (typed before the index loaded)' : ''}: no results`);
    if (dropFirstLunr && !dropped) failures.push(`run ${run}: lunr request was not intercepted`);
  } catch (e) {
    failures.push(`run ${run}${dropFirstLunr ? ' (first lunr request dropped)' : slowIndex ? ' (typed before the index loaded)' : ''}: ${String(e.message).split('\n')[0]}`);
  }
  errors.forEach((e) => failures.push(`run ${run}${dropFirstLunr ? ' (first lunr request dropped)' : slowIndex ? ' (typed before the index loaded)' : ''}: JS error: ${e}`));
  await ctx.close();
}
for (let i = 1; i <= runs; i++) { await searchOnce(i, false); await searchOnce(i, true); await searchOnce(i, false, true); }
await browser.close();
server.close();
failures.forEach((f) => console.log(f));
console.log(`search: ${runs} runs x 3 (normal, first lunr request dropped, query typed before the index loaded), ${failures.length} failure(s)`);
process.exit(failures.length ? 1 : 0);
