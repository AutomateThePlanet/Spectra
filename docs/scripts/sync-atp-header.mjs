// Regenerates the SPECTRA docs header as a 1:1 copy of the automatetheplanet.com header.
//   node docs/scripts/sync-atp-header.mjs [path-to-Automate-The-Planet-Website-v2]
// Writes (never edit them by hand):
//   docs/_includes/atp_header.html  - Header.astro markup; links absolute to automatetheplanet.com; Tools and its
//                                     SPECTRA item marked current; the docs search (just-the-docs) added to the menu
//                                     as the expanding search icon; the site's dark/light theme toggle left out
//   docs/assets/css/atp-header.css  - the CSS that markup needs: the rules of thrive-combined.css that match it
//                                     (PurgeCSS) + the header's own <style> blocks, every selector scoped under
//                                     .atp-site-header so nothing leaks into the docs
//   docs/assets/images/atp/*        - the logo and the images that CSS references
// Uses purgecss + postcss from the ATP repo's node_modules (no install in this repo).
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const ATP = path.resolve(process.argv[2] || 'C:/Users/angel/OneDrive/WORKING_COWORK/Automate-The-Planet-Website-v2');
const DOCS = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..');
const require = createRequire(path.join(ATP, 'package.json'));
const { PurgeCSS } = require('purgecss');
const postcss = require('postcss');
const SITE = 'https://www.automatetheplanet.com';
const SCOPE = '.atp-site-header';

// ---------- markup ----------
let html = fs.readFileSync(path.join(ATP, 'src/components/layout/Header.astro'), 'utf8').replace(/^---[\s\S]*?---\s*/, '');
const styles = [];
html = html.replace(/<style[^>]*>([\s\S]*?)<\/style>/g, (_, css) => { styles.push(css); return ''; });
// the dark/light theme toggle drives the ATP blog theme; the docs have no dark theme
html = html.replace(/<li>\s*<label class="theme-toggle[\s\S]*?<\/label>\s*<\/li>/, '');
if (/theme-toggle/.test(html)) throw new Error('theme toggle not removed');
html = html.replace(/(href)="\/(?!\/)/g, `$1="${SITE}/`).replace(/href="\/"/g, `href="${SITE}/"`);
// Tools = current section; SPECTRA = current item
const toolsLi = /(<li id="menu-item-1520" class="[^"]*?)("\s*>\s*<a href="#">Tools<\/a>)/;
if (!toolsLi.test(html)) throw new Error('Tools item not found');
html = html.replace(toolsLi, '$1 current-menu-item$2');
const spectraLi = /(<li class="[^"]*?)("[^>]*>\s*<a href="https:\/\/www\.automatetheplanet\.com\/spectra\/")>/;
if (!spectraLi.test(html)) throw new Error('SPECTRA item not found');
html = html.replace(spectraLi, '$1 current-menu-item$2 aria-current="page">');
// docs search (just-the-docs ids #search-input / #search-results) as the expanding search icon
const SEARCH_ICON = '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path fill="currentColor" d="M15.5 14h-.79l-.28-.27A6.47 6.47 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/></svg>';
const searchLi = `<li class="menu-item menu-search docs-search"><button type="button" class="menu-search-toggle" aria-label="Search the documentation" aria-expanded="false" aria-controls="atp-docs-search">${SEARCH_ICON}</button><div class="menu-search-form" id="atp-docs-search">{% include components/search_header.html %}</div></li>`;
const contactLi = '<li class="menu-download menu-item small">';
if (!html.includes(contactLi)) throw new Error('Contact Us item not found');
html = html.replace(contactLi, searchLi + contactLi);
// logo
const logoSrc = (html.match(/<img src="(\/[^"]+)"/) || [])[1];
if (!logoSrc) throw new Error('logo not found');
const imgDir = path.join(DOCS, 'assets/images/atp');
fs.rmSync(imgDir, { recursive: true, force: true });
fs.mkdirSync(imgDir, { recursive: true });
fs.copyFileSync(path.join(ATP, 'public', logoSrc), path.join(imgDir, path.basename(logoSrc)));
html = html.replace(`src="${logoSrc}"`, `src="{{ '/assets/images/atp/${path.basename(logoSrc)}' | relative_url }}"`);
html = html.replace(/\s+\n/g, '\n').trim();

// ---------- CSS ----------
const thrive = fs.readFileSync(path.join(ATP, 'public/css/thrive-combined.css'), 'utf8');
const purged = await new PurgeCSS().purge({
  content: [{ raw: html.replace(/\{%[\s\S]*?%\}/g, '') + '<input class="search-input"><div class="search-results"></div>', extension: 'html' }],
  css: [{ raw: thrive }],
  safelist: { standard: ['opened', 'toggled', 'open', 'is-open', 'current-menu-item', 'current-menu-ancestor', 'current-menu-parent', 'html', 'body'], greedy: [/^tat-/] },
  fontFace: false, keyframes: true, variables: true,
});
const own = styles.join('\n');
const scopeSel = (sel) => {
  const s = sel.trim();
  if (!s || s.startsWith(SCOPE)) return s;
  if (/^(:root|html|body)$/.test(s)) return SCOPE;
  const m = s.match(/^(html|body)(\.[\w-]+)*\s+(.*)$/);
  if (m) return `${SCOPE} ${m[3]}`;
  return `${SCOPE} ${s}`;
};
const scoper = {
  postcssPlugin: 'scope',
  Rule(rule) {
    if (rule.parent && rule.parent.type === 'atrule' && /keyframes/i.test(rule.parent.name)) return;
    if (rule.selector.includes(SCOPE)) return;
    rule.selectors = rule.selectors.map(scopeSel);
  },
};
const assets = new Set();
const rewriteUrls = {
  postcssPlugin: 'urls',
  Declaration(decl) {
    decl.value = decl.value.replace(/url\((['"]?)(\/[^'")]+)\1\)/g, (all, q, u) => {
      const file = path.join(ATP, 'public', u.split('?')[0]);
      if (!fs.existsSync(file)) return all;
      assets.add(file);
      return `url(${q}../images/atp/${path.basename(file)}${q})`;
    });
  },
};
// no @import (Thrive pulls Google Fonts the header does not use)
const dropImports = { postcssPlugin: 'drop-imports', AtRule: { import(at) { at.remove(); } } };
const css = (await postcss([dropImports, scoper, rewriteUrls]).process(purged[0].css + '\n' + own, { from: undefined })).css;
for (const f of assets) fs.copyFileSync(f, path.join(imgDir, path.basename(f)));
const banner = `/* GENERATED by docs/scripts/sync-atp-header.mjs from Automate-The-Planet-Website-v2 (Header.astro +\n   the matching rules of public/css/thrive-combined.css), scoped under ${SCOPE}. Never edit by hand. */\n`;
fs.mkdirSync(path.join(DOCS, 'assets/css'), { recursive: true });
fs.writeFileSync(path.join(DOCS, 'assets/css/atp-header.css'), banner + css);
const include = `{%- comment -%}GENERATED by docs/scripts/sync-atp-header.mjs from the automatetheplanet.com header\n(Automate-The-Planet-Website-v2/src/components/layout/Header.astro). Never edit by hand.{%- endcomment -%}\n<div class="atp-site-header">\n${html}\n</div>\n`;
fs.writeFileSync(path.join(DOCS, '_includes/atp_header.html'), include);
console.log(`atp_header.html ${include.length} B, atp-header.css ${css.length} B (from ${thrive.length} B), ${assets.size + 1} images`);
