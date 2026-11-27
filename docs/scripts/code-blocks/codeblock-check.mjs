// Checks the shared code blocks (codeblock.css / codeblock.js) on one loaded page. The same file in every repo that
// uses them; the repo's test runs it on every built page with a <pre> at 1440 and 390 px.
//   import { checkCodeBlocks, installClipboardSpy } from './codeblock-check.mjs';
//   await installClipboardSpy(context);            // before page.goto: records what the copy button writes
//   const problems = await checkCodeBlocks(page);  // [] when every block behaves
// Checked for every <pre> (Mermaid sources and anything inside [data-cb-skip] are skipped; bespoke designs opt out
// that way): wrapped in .cb; copy button present, visible, not over the code, copies the exact text and announces
// "Copied"; more than 20 lines: collapsed with a fade and "Show more ▾", Enter expands ("Show less ▴", no fade,
// aria-expanded="true"), Space collapses again; 20 lines or fewer: no button, no fade; monospace font; long lines
// scroll inside the block; the block is not wider than the viewport; no old controls (Expand buttons, theme copy
// buttons) anywhere on the page; a block in a known language has highlighted tokens; no token is bold.

export async function installClipboardSpy(context) {
  await context.addInitScript(() => {
    window.__cbCopied = [];
    const fake = { writeText: (t) => { window.__cbCopied.push(t); return Promise.resolve(); }, readText: () => Promise.resolve(window.__cbCopied[window.__cbCopied.length - 1] || '') };
    try { Object.defineProperty(navigator, 'clipboard', { value: fake, configurable: true }); } catch (e) { /* ignore */ }
  });
}

function structural() {
  const MONO = /mono|consol|courier|cascadia|menlo/i;
  const problems = [];
  const old = ['.expand-button', '.code-expand-btn', 'button[aria-label="Copy code to clipboard"]', '.bx2-code-wrap', 'pre.expand', 'pre.is-collapsed', '.highlighter-rouge > button'];
  old.forEach((s) => { if (document.querySelector(s)) problems.push(`old control on the page: ${s}`); });
  document.querySelectorAll('.bx2-copy').forEach((b) => { if (!b.closest('.bx2-prompt')) problems.push('old control on the page: .bx2-copy on code'); });
  const vw = document.documentElement.clientWidth;
  const blocks = [];
  document.querySelectorAll('pre').forEach((pre, i) => {
    if (pre.closest('script,template,noscript,[data-cb-skip]')) return;
    if (pre.getAttribute('data-language') === 'mermaid' || pre.classList.contains('mermaid') || pre.closest('.mermaid')) return;
    const at = `block ${blocks.length + 1}`;
    const text = pre.textContent.replace(/\r\n?/g, '\n').replace(/\n$/, '');
    const lines = pre.querySelectorAll('.line').length || text.split('\n').length;
    const cb = pre.closest('.cb');
    blocks.push({ i, text, lines });
    if (!cb) { problems.push(`${at}: not a shared code block (no .cb)`); return; }
    const copy = cb.querySelector(':scope > .cb-copy');
    if (!copy) problems.push(`${at}: no copy button`);
    else {
      const cr = copy.getBoundingClientRect(), cs = getComputedStyle(copy), pr = pre.getBoundingClientRect();
      if (cr.width < 10 || cr.height < 10 || cs.visibility === 'hidden' || cs.display === 'none' || +cs.opacity < 0.5) problems.push(`${at}: copy button not visible`);
      if (pr.right > cr.left + 1 && pr.top < cr.bottom && pr.bottom > cr.top) problems.push(`${at}: copy button over the code`);
    }
    const span = pre.querySelector('span');
    const f1 = getComputedStyle(pre).fontFamily.split(',')[0], f2 = span ? getComputedStyle(span).fontFamily.split(',')[0] : f1;
    if (!MONO.test(f1) || !MONO.test(f2)) problems.push(`${at}: not monospace (${f1} / ${f2})`);
    const toggle = cb.querySelector(':scope > .cb-toggle');
    const fade = getComputedStyle(cb, '::after').content;
    const hasFade = fade !== 'none' && fade !== '';
    if (lines > 20) {
      if (!toggle) problems.push(`${at}: ${lines} lines but no "Show more" button`);
      else {
        if (toggle.tagName !== 'BUTTON') problems.push(`${at}: toggle is not a <button>`);
        if (!cb.classList.contains('is-collapsed') || !hasFade) problems.push(`${at}: not collapsed with a fade`);
        if (toggle.getAttribute('aria-expanded') !== 'false') problems.push(`${at}: aria-expanded is not "false" when collapsed`);
        if (toggle.textContent.trim() !== 'Show more ▾') problems.push(`${at}: label "${toggle.textContent.trim()}"`);
      }
    } else {
      if (toggle) problems.push(`${at}: ${lines} lines but has a toggle`);
      if (hasFade) problems.push(`${at}: short block has a fade`);
    }
    if (pre.scrollWidth > pre.clientWidth + 1 && !/auto|scroll/.test(getComputedStyle(pre).overflowX)) problems.push(`${at}: long lines clipped`);
    // highlighting: a block in a known language has coloured tokens; no token is bold
    const lang = (pre.getAttribute('data-language') || ((pre.closest('[class*="language-"]') || pre.querySelector('code[class*="language-"]') || {}).className || '').match(/language-([\w#+-]+)/)?.[1] || '').toLowerCase();
    const plainLangs = ['', 'text', 'txt', 'plaintext', 'plain', 'none', 'output', 'log'];
    // shell, terminal and command-prompt blocks: plain monospace by owner decision (no token colours, no italics)
    const shellLangs = ['bash', 'sh', 'shell', 'zsh', 'powershell', 'ps', 'ps1', 'pwsh', 'cmd', 'bat', 'batch', 'console', 'shellsession', 'shell-session', 'terminal'];
    const base = getComputedStyle(pre).color;
    const spans = [...pre.querySelectorAll('span')].filter((s) => s.textContent.trim());
    if (!lang) problems.push(`${at}: code block without a language`);
    if (lang === 'prompt') problems.push(`${at}: prompt left as a code block`);
    // tokenized = the highlighter split the code into tokens: a coloured token, or (code that is only names and
    // punctuation, e.g. `[TestFixture]`) a line of 2+ token spans (Shiki) / token spans at all (Rouge). Fails for raw
    // <pre> text and for Shiki's plain fallback (one span per line).
    const lineEls = [...pre.querySelectorAll('.line')];
    const tokenized = spans.some((s) => getComputedStyle(s).color !== base) || (lineEls.length
      ? lineEls.some((l) => [...l.querySelectorAll('span')].filter((s) => s.textContent.trim()).length >= 2)
      : spans.some((s) => s.className));
    if (shellLangs.includes(lang) && spans.some((t) => getComputedStyle(t).color !== base || getComputedStyle(t).fontStyle === 'italic')) problems.push(`${at}: ${lang} block has token colours (shell blocks are plain)`);
    if (!plainLangs.includes(lang) && !shellLangs.includes(lang) && lang !== 'prompt' && !tokenized) problems.push(`${at}: ${lang} block without highlighted tokens`);
    if (spans.some((s) => parseInt(getComputedStyle(s).fontWeight, 10) >= 600)) problems.push(`${at}: bold tokens`);
    if (cb.getBoundingClientRect().right > vw + 1) problems.push(`${at}: wider than the viewport`);
  });
  // prompts to an AI agent: the prompt style, body font, no code tokens, a copy button
  document.querySelectorAll('figure.cb-prompt').forEach((fig, n) => {
    const at = `prompt ${n + 1}`;
    const text = fig.querySelector('.cb-prompt-text');
    if (!fig.querySelector('.cb-prompt-label')) problems.push(`${at}: no "You → agent" label`);
    if (!text || !text.textContent.trim()) problems.push(`${at}: empty`);
    if ([...fig.querySelectorAll('pre, code, span[class], span[style]')].some((e) => !e.closest('.cb-copy'))) problems.push(`${at}: contains code markup or tokens`);
    if (text && MONO.test(getComputedStyle(text).fontFamily.split(',')[0])) problems.push(`${at}: monospace font`);
    if (!fig.querySelector(':scope > .cb-copy')) problems.push(`${at}: no copy button`);
    // readable on its own panel (page prose colours, e.g. a dark theme's light-grey <p>, must not leak in)
    const lum = (c) => { const [r, g, b] = (c.match(/[\d.]+/g) || [0, 0, 0]).slice(0, 3).map((v) => { v = +v / 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
    const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
    const panel = getComputedStyle(fig).backgroundColor;
    [['text', text], ['label', fig.querySelector('.cb-prompt-label')]].forEach(([what, el]) => {
      if (!el) return;
      const k = ratio(getComputedStyle(el).color, panel);
      if (k < 4.5) problems.push(`${at}: ${what} contrast ${k.toFixed(2)}:1 (needs 4.5)`);
    });
  });
  return { problems, blocks };
}

export async function checkCodeBlocks(page) {
  await page.evaluate(() => document.querySelectorAll('details').forEach((d) => { d.open = true; }));
  const { problems, blocks } = await page.evaluate(structural);
  const cbs = page.locator('.cb');
  const n = await cbs.count();
  for (let k = 0; k < n; k++) {
    const cb = cbs.nth(k);
    const idx = await cb.evaluate((el) => [...document.querySelectorAll('pre')].indexOf(el.querySelector('pre')));
    const b = blocks.find((x) => x.i === idx);
    if (!b) continue;
    const at = `block ${blocks.indexOf(b) + 1}`;
    try {
      await cb.scrollIntoViewIfNeeded();
      const copy = cb.locator(':scope > .cb-copy');
      if (await copy.count()) {
        await copy.click({ timeout: 4000 });
        await page.waitForTimeout(50);
        const got = await page.evaluate(() => (window.__cbCopied || []).slice(-1)[0]);
        if (got !== b.text) problems.push(`${at}: copy button copied different text`);
        if ((await copy.locator('.cb-sr').textContent()) !== 'Copied') problems.push(`${at}: no "Copied" announcement`);
      }
      const t = cb.locator(':scope > .cb-toggle');
      if (await t.count()) {
        await t.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(50);
        const st = await cb.evaluate((el) => { const p = el.querySelector('pre'); const f = getComputedStyle(el, '::after').content; return { aria: el.querySelector('.cb-toggle').getAttribute('aria-expanded'), label: el.querySelector('.cb-toggle').textContent.trim(), fade: f !== 'none' && f !== '', full: p.scrollHeight <= p.clientHeight + 2 }; });
        if (st.aria !== 'true' || st.label !== 'Show less ▴' || st.fade || !st.full) problems.push(`${at}: Enter does not expand it (${JSON.stringify(st)})`);
        await t.focus(); await page.keyboard.press(' '); await page.waitForTimeout(50);
        if ((await t.getAttribute('aria-expanded')) !== 'false') problems.push(`${at}: Space does not collapse it`);
      }
    } catch (e) {
      problems.push(`${at}: ${String(e.message).split('\n')[0].slice(0, 120)}`);
    }
  }
  // prompt copy buttons copy the prompt text
  const prompts = page.locator('figure.cb-prompt');
  for (let k = 0; k < await prompts.count(); k++) {
    const fig = prompts.nth(k);
    try {
      await fig.scrollIntoViewIfNeeded();
      const copy = fig.locator(':scope > .cb-copy');
      if (!(await copy.count())) continue;
      await copy.click({ timeout: 4000 });
      await page.waitForTimeout(50);
      const got = await page.evaluate(() => (window.__cbCopied || []).slice(-1)[0]);
      const want = (await fig.locator('.cb-prompt-text').textContent()).trim();
      if (got !== want) problems.push(`prompt ${k + 1}: copy button copied different text`);
    } catch (e) { problems.push(`prompt ${k + 1}: ${String(e.message).split('\n')[0].slice(0, 120)}`); }
  }
  return problems;
}

// Fast mode: a representative set of pages instead of every page with code. At least one page per page type, then
// more pages until every kind of block is covered: each language, blocks over 20 lines (collapse), long lines
// (horizontal scroll), Mermaid sources, [data-cb-skip] listings, inline code. Input: [{ url, html }] of the built pages
// that have a <pre>; output: the urls to check. The full crawl (every page) stays available in every repo.
export function pickRepresentative(pages) {
  const featuresOf = ({ url, html }) => {
    const f = new Set();
    const clean = html.replace(/<!--[\s\S]*?-->|<script[\s\S]*?<\/script>/g, '');
    const type = /class="[^"]*\bblog-prose\b/.test(clean) ? 'blog-post'
      : /class="atp2[\s"]/.test(clean) ? 'atp2-page'
      : (url.split('/').filter(Boolean).length > 1 ? url.split('/').filter(Boolean)[0] : 'top-level');
    f.add('type:' + type);
    for (const m of clean.matchAll(/data-language="([^"]+)"|\blanguage-([\w#+-]+)/g)) f.add('lang:' + (m[1] || m[2]));
    for (const m of clean.matchAll(/<pre\b[^>]*>([\s\S]*?)<\/pre>/g)) {
      const text = m[1].replace(/<[^>]+>/g, '');
      const lines = text.split('\n');
      if (lines.length > 21) f.add('collapsible');
      if (lines.some((l) => l.length > 110)) f.add('long-lines');
    }
    if (/data-language="mermaid"|class="mermaid/.test(clean)) f.add('mermaid');
    if (/data-cb-skip/.test(clean)) f.add('bespoke');
    if (/<(p|li|td)[^>]*>[^<]*<code/.test(clean)) f.add('inline-code');
    return { type, f };
  };
  const all = pages.map((p) => ({ url: p.url, ...featuresOf(p) }));
  const chosen = []; const covered = new Set();
  const take = (p) => { chosen.push(p.url); p.f.forEach((x) => covered.add(x)); };
  // most-featured page of every type first
  const types = [...new Set(all.map((p) => p.type))];
  for (const t of types) take(all.filter((p) => p.type === t).sort((a, b) => b.f.size - a.f.size)[0]);
  // then greedily until every feature seen anywhere is covered
  for (;;) {
    let best = null, gain = 0;
    for (const p of all) {
      if (chosen.includes(p.url)) continue;
      const g = [...p.f].filter((x) => !covered.has(x)).length;
      if (g > gain) { gain = g; best = p; }
    }
    if (!best) break;
    take(best);
  }
  return chosen.sort();
}
