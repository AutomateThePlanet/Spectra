/*
 * Shared code blocks (v1) — automatetheplanet.com, bellatrix.solutions, SPECTRA docs, BELLATRIX docs.
 * The same file in every repo (see codeblock.css). Load it deferred, with the content roots to scan:
 *   <script defer src="/js/codeblock.js" data-cb-roots=".blog-prose, #page-content"></script>
 * For every <pre> inside a root (Shiki `pre.astro-code`, Rouge `div.highlighter-rouge`, plain pre):
 *  - wraps it in div.cb (grey block, blue left bar, monospace, horizontal scroll inside the block)
 *  - adds a copy button (top-right, "Copied" for screen readers, keyboard-accessible)
 *  - blocks with more than 20 lines start collapsed: bottom fade + "Show more ▾" / "Show less ▴"
 *    (a real <button> with aria-expanded/aria-controls, so Enter and Space work); shorter blocks
 *    get neither the button nor the fade
 *  - inline <code> in running text gets the shared inline style (class cb-inline)
 *  - Rouge bash/sh blocks: the command word, flags and values get span.cb-sh-cmd / .cb-sh-flag / .cb-sh-arg (Rouge leaves them plain)
 *  - removes older expand controls (pre.expand + .expand-button, pre.is-collapsed + .code-expand-btn)
 * Mermaid sources (pre[data-language="mermaid"], pre.mermaid) and anything inside [data-cb-skip]
 * (bespoke designs such as line-numbered or diff listings) are left alone.
 * Prompts to an AI agent are not code: a fence tagged `prompt` (```prompt) becomes a prompt block instead
 * (figure.cb-prompt: "You → agent" label, the sentence in the body font, copy button, no highlighting).
 * Pages can also render figure.cb-prompt themselves (one <p class="cb-prompt-text"> each); it gets the copy button.
 */
(function () {
  'use strict';
  var MAX_LINES = 20;
  var script = document.currentScript;
  var rootsSel = (script && script.getAttribute('data-cb-roots')) || 'main';
  var COPY_ICON = '<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path fill="currentColor" d="M16 1H4a2 2 0 0 0-2 2v14h2V3h12V1zm3 4H8a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2zm0 16H8V7h11v14z"/></svg>';
  var seq = 0;

  function isMermaid(pre) {
    return pre.getAttribute('data-language') === 'mermaid' || pre.classList.contains('mermaid') ||
      !!pre.querySelector('code.language-mermaid');
  }

  function codeText(pre) {
    return pre.textContent.replace(/\r\n?/g, '\n').replace(/\n$/, '');
  }

  function lineCount(pre) {
    var lines = pre.querySelectorAll('.line');
    if (lines.length) return lines.length;
    return codeText(pre).split('\n').length;
  }

  function cleanupOld(pre) {
    pre.classList.remove('expand', 'is-collapsed');
    var next = pre.nextElementSibling;
    if (next && /(^|\s)(expand-button|code-expand-btn)(\s|$)/.test(next.className)) next.parentNode.removeChild(next);
    var host = pre.closest('div.highlight') || pre;
    var after = host.nextElementSibling;
    if (after && /(^|\s)(expand-button|code-expand-btn)(\s|$)/.test(after.className)) after.parentNode.removeChild(after);
  }

  function addCopy(box, pre, getText) {
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'cb-copy';
    btn.setAttribute('aria-label', 'Copy code');
    btn.innerHTML = COPY_ICON + '<span class="cb-sr" role="status" aria-live="polite"></span>';
    var status = btn.querySelector('.cb-sr');
    var timer = null;
    btn.addEventListener('click', function () {
      var text = getText ? getText() : codeText(pre);
      var done = function () {
        btn.classList.add('is-copied');
        status.textContent = 'Copied';
        clearTimeout(timer);
        timer = setTimeout(function () { btn.classList.remove('is-copied'); status.textContent = ''; }, 2000);
      };
      if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(done, function () { fallbackCopy(text) && done(); });
      } else if (fallbackCopy(text)) {
        done();
      }
    });
    box.appendChild(btn);
  }

  function fallbackCopy(text) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.position = 'fixed';
    ta.style.top = '-1000px';
    document.body.appendChild(ta);
    ta.select();
    var ok = false;
    try { ok = document.execCommand('copy'); } catch (e) { ok = false; }
    document.body.removeChild(ta);
    return ok;
  }

  function addToggle(box, pre) {
    if (!pre.id) pre.id = 'cb-code-' + (++seq);
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'cb-toggle';
    btn.setAttribute('aria-controls', pre.id);
    function set(expanded) {
      box.classList.toggle('is-collapsed', !expanded);
      btn.setAttribute('aria-expanded', expanded ? 'true' : 'false');
      btn.textContent = expanded ? 'Show less ▴' : 'Show more ▾';
    }
    btn.addEventListener('click', function () {
      var expand = btn.getAttribute('aria-expanded') !== 'true';
      set(expand);
      if (!expand) {
        var r = box.getBoundingClientRect();
        if (r.top < 0) box.scrollIntoView({ block: 'start' });
      }
    });
    box.classList.add('is-collapsible');
    box.appendChild(btn);
    set(false);
  }

  function langOf(pre) {
    // Shiki: data-language; Rouge: div.language-x around the pre; a language Rouge doesn't know: <pre><code class="language-x">
    var holder = pre.closest('[class*="language-"]') || pre.querySelector('code[class*="language-"]') || {};
    return (pre.getAttribute('data-language') ||
      ((holder.className || '').match(/language-([\w#+-]+)/) || [])[1] || '').toLowerCase();
  }

  function promptCopy(fig) {
    if (fig.querySelector(':scope > .cb-copy')) return;
    var text = fig.querySelector('.cb-prompt-text');
    addCopy(fig, { textContent: '' }, function () { return text.textContent.trim(); });
    fig.querySelector(':scope > .cb-copy').setAttribute('aria-label', 'Copy prompt');
  }

  function toPrompt(pre) {
    var target = pre.closest('div.highlighter-rouge') || pre.closest('figure.highlight') || pre;
    var fig = document.createElement('figure');
    fig.className = 'cb-prompt';
    fig.innerHTML = '<figcaption class="cb-prompt-label">You → agent</figcaption><p class="cb-prompt-text"></p>';
    fig.querySelector('.cb-prompt-text').textContent = pre.textContent.replace(/\r\n?/g, '\n').trim();
    target.parentNode.replaceChild(fig, target);
    promptCopy(fig);
  }

  // Rouge (Jekyll) leaves shell commands as plain text: mark the command word at the start of a command and the
  // flags (-x / --long), the same roles the Shiki theme colours on automatetheplanet.com and bellatrix.solutions.
  var SHELL = /^(bash|sh|shell|zsh)$/;
  function shellTokens(pre) {
    if (!pre.closest('.highlighter-rouge') || !SHELL.test(langOf(pre))) return;
    var code = pre.querySelector('code') || pre;
    var atStart = true, cont = false;
    Array.prototype.slice.call(code.childNodes).forEach(function (n) {
      var t = n.textContent;
      if (n.nodeType !== 3) {
        var w = t.trim();
        if (/^(\||\|\||&&|;)$/.test(w)) atStart = true;
        else if (w) atStart = false;
        if (/\n\s*$/.test(t)) { atStart = !cont; cont = false; }
        return;
      }
      var frag = document.createDocumentFragment(), re = /\n|[^\S\n]+|[^\s]+/g, m, used = false;
      while ((m = re.exec(t))) {
        var w2 = m[0], cls = null;
        if (w2 === '\n') { atStart = !cont; cont = false; }
        else if (/^\s+$/.test(w2)) { /* spacing */ }
        else if (w2 === '\\') cont = true;
        else if (/^(\||\|\||&&|;)$/.test(w2)) atStart = true;
        else if (atStart) { cls = 'cb-sh-cmd'; atStart = false; }
        else if (/^--?[A-Za-z]/.test(w2)) cls = 'cb-sh-flag';
        else if (!/^[#$"'`]/.test(w2)) cls = 'cb-sh-arg';
        if (cls) {
          var eq = cls === 'cb-sh-flag' ? w2.indexOf('=') : -1;
          var s = document.createElement('span');
          s.className = cls;
          s.textContent = eq > 0 ? w2.slice(0, eq) : w2;
          frag.appendChild(s);
          if (eq > 0) {
            frag.appendChild(document.createTextNode('='));
            var val = document.createElement('span');
            val.className = 'cb-sh-arg';
            val.textContent = w2.slice(eq + 1);
            frag.appendChild(val);
          }
          used = true;
        } else frag.appendChild(document.createTextNode(w2));
      }
      if (used) code.replaceChild(frag, n);
    });
  }

  function enhance(pre) {
    if (pre.closest('.cb') || pre.closest('[data-cb-skip]') || isMermaid(pre)) return;
    if (langOf(pre) === 'prompt') { toPrompt(pre); return; }
    cleanupOld(pre);
    shellTokens(pre);
    // Rouge: wrap the whole div.highlighter-rouge (keeps the language class); else the pre itself
    var target = pre.closest('div.highlighter-rouge') || pre.closest('figure.highlight') || pre;
    var box = document.createElement('div');
    box.className = 'cb';
    var lang = langOf(pre);
    if (lang) box.setAttribute('data-lang', lang);
    target.parentNode.insertBefore(box, target);
    box.appendChild(target);
    if (!pre.hasAttribute('tabindex')) pre.setAttribute('tabindex', '0'); // scrollable region reachable by keyboard
    addCopy(box, pre);
    if (lineCount(pre) > MAX_LINES) addToggle(box, pre);
  }

  function init() {
    var roots = [];
    try { roots = Array.prototype.slice.call(document.querySelectorAll(rootsSel)); } catch (e) { roots = []; }
    roots.forEach(function (root) {
      Array.prototype.forEach.call(root.querySelectorAll('pre'), enhance);
      Array.prototype.forEach.call(root.querySelectorAll('figure.cb-prompt'), promptCopy);
      Array.prototype.forEach.call(root.querySelectorAll('code'), function (code) {
        if (!code.closest('pre') && !code.closest('.cb')) code.classList.add('cb-inline');
      });
    });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
