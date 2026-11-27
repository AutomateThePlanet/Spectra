#!/usr/bin/env node
// Code-block test for a built Jekyll site: every page with a <pre>, at 1440 and 390 px, must use the shared code
// blocks correctly (see codeblock-check.mjs). Exits 1 when any block fails.
//   npm ci && npx playwright install chromium           (once, in this folder)
//   node check-code-blocks.mjs <path-to-_site> [--base /Spectra] [--workers 4]
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { chromium } from 'playwright';
import { checkCodeBlocks, installClipboardSpy } from './codeblock-check.mjs';

const args = process.argv.slice(2);
const site = path.resolve(args.find((a) => !a.startsWith('--') && !/^\d+$/.test(a)) || '_site');
const opt = (k, d) => { const i = args.indexOf(k); return i >= 0 ? args[i + 1] : d; };
const base = (opt('--base', '') || '').replace(/\/$/, '');
const workers = Number(opt('--workers', 4));
const types = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.mjs': 'text/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.gif': 'image/gif', '.woff2': 'font/woff2', '.json': 'application/json' };

const server = http.createServer((req, res) => {
  let rel = decodeURIComponent(req.url.split('?')[0]);
  if (base && rel.startsWith(base)) rel = rel.slice(base.length);
  let file = path.join(site, rel);
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (!fs.existsSync(file) && fs.existsSync(file + '.html')) file += '.html';
  if (!file.startsWith(site) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const origin = `http://127.0.0.1:${server.address().port}`;

const pages = [];
(function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    if (fs.statSync(p).isDirectory()) { walk(p); continue; }
    if (!f.endsWith('.html')) continue;
    const html = fs.readFileSync(p, 'utf8').replace(/<!--[\s\S]*?-->|<script[\s\S]*?<\/script>/g, '');
    if (!/<pre[\s>]/.test(html)) continue;
    const rel = '/' + path.relative(site, p).split(path.sep).join('/');
    pages.push(base + rel.replace(/index\.html$/, ''));
  }
})(site);

const browser = await chromium.launch();
const queue = [];
for (const url of pages) for (const w of [1440, 390]) queue.push({ url, w });
const failures = [];
let checked = 0;
await Promise.all(Array.from({ length: workers }, async () => {
  while (queue.length) {
    const { url, w } = queue.shift();
    const ctx = await browser.newContext({ viewport: { width: w, height: 900 }, isMobile: w < 500, hasTouch: w < 500 });
    await installClipboardSpy(ctx);
    const page = await ctx.newPage();
    const errors = [];
    page.on('pageerror', (e) => errors.push(e.message));
    try {
      // one retry: the local file server can drop a connection under load
      await page.goto(origin + url, { waitUntil: 'load' }).catch(() => page.goto(origin + url, { waitUntil: 'load' }));
      await page.waitForTimeout(300);
      const problems = await checkCodeBlocks(page);
      errors.forEach((e) => problems.push(`JS error: ${e}`));
      problems.forEach((p) => failures.push(`${url} @${w}px: ${p}`));
    } catch (e) {
      failures.push(`${url} @${w}px: ${String(e.message).split('\n')[0]}`);
    }
    checked++;
    await ctx.close();
  }
}));
await browser.close();
server.close();
failures.forEach((f) => console.log(f));
console.log(`code blocks: ${pages.length} pages with code, ${checked} page checks (1440 + 390), ${failures.length} problem(s)`);
process.exit(failures.length ? 1 : 0);
