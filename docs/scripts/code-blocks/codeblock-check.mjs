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
// buttons) anywhere on the page.

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
    if (cb.getBoundingClientRect().right > vw + 1) problems.push(`${at}: wider than the viewport`);
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
  return problems;
}
