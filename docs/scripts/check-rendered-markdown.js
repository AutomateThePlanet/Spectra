#!/usr/bin/env node
// Checks a built Jekyll site for Markdown that kramdown did not convert.
//
// Why: if a line in a page starts with an HTML tag (for example a wrapped code span
// whose second line begins with `<path>`), kramdown treats it as the start of a raw
// HTML block and stops parsing Markdown for the rest of the block. Headings, tables,
// fences and lists then show up as plain text, and placeholders such as `<s>` turn
// into real <s> strikethrough elements.
//
// Usage:  node docs/scripts/check-rendered-markdown.js <path-to-_site>
// Exit code 1 when a page has unconverted Markdown, 0 otherwise. No dependencies.

const fs = require('fs');
const path = require('path');

const root = process.argv[2];
if (!root || !fs.existsSync(root)) {
  console.error('usage: node check-rendered-markdown.js <path-to-_site>');
  process.exit(2);
}

function htmlFiles(dir) {
  const out = [];
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    if (fs.statSync(p).isDirectory()) out.push(...htmlFiles(p));
    else if (name.endsWith('.html')) out.push(p);
  }
  return out;
}

const decode = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');

let failed = 0;
for (const file of htmlFiles(root)) {
  const html = fs.readFileSync(file, 'utf8');
  const main = (html.match(/<main>([\s\S]*?)<\/main>/) || [null, ''])[1];
  if (!main) continue;
  // Text outside code: drop <pre>…</pre> and <code>…</code>, then all tags.
  const text = decode(main
    .replace(/<pre[\s\S]*?<\/pre>/g, '\n')
    .replace(/<code[\s\S]*?<\/code>/g, ' ')
    .replace(/<[^>]+>/g, '\n'));
  const problems = [];
  for (const line of text.split('\n')) {
    const t = line.trim();
    if (/^#{1,6} \S/.test(t)) problems.push('heading marks: ' + t.slice(0, 70));
    else if (/\|\s*:?-{3,}/.test(t)) problems.push('table separator: ' + t.slice(0, 70));
    else if (t.includes('```')) problems.push('code fence: ' + t.slice(0, 70));
  }
  const strike = (main.match(/<(s|del|strike)>/g) || []).length;
  if (strike) problems.push(strike + ' strikethrough element(s) <s>/<del> (often a `<placeholder>` read as HTML)');
  if (problems.length) {
    failed++;
    console.log(path.relative(root, file));
    for (const p of problems.slice(0, 5)) console.log('  - ' + p);
    if (problems.length > 5) console.log('  … ' + (problems.length - 5) + ' more');
  }
}

console.log(failed ? `\n${failed} page(s) with unconverted Markdown` : 'OK: no unconverted Markdown found');
process.exit(failed ? 1 : 0);
