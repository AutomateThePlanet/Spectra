namespace Spectra.CLI.Commands.Run.WebConsole;

/// <summary>
/// Spec 066: the console's single interactive page. It BORROWS the report styling tokens from
/// <c>ReportWriter.cs:413-431</c> (navy + per-status palette + card CSS) as the shared visual language
/// (FR-007), but is a NEW interactive render — not an extension of <c>ReportWriter</c>'s static assembly.
///
/// Invariant (FR-002/FR-003, research R4): the browser is a VIEW + WRITE-BACK CALLER, never a store.
/// The JS holds NO authoritative run state — no localStorage/sessionStorage. Every render is derived
/// solely from the latest <c>GET /current</c> payload (polled on an interval and re-fetched after every
/// write), so a refresh or reopen loses nothing because SQLite is the source of truth.
/// </summary>
public static class ConsolePage
{
    public static string Render() => Html;

    private const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Spectra — Execution Console</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
<style>
  :root {
    --color-navy: #1B2A4A; --color-navy-light: #2D3F5E;
    --color-passed: #16a34a; --color-passed-bg: #dcfce7;
    --color-failed: #dc2626; --color-failed-bg: #fee2e2;
    --color-skipped: #6b7280; --color-skipped-bg: #f3f4f6;
    --color-blocked: #d97706; --color-blocked-bg: #f3e8ff;
    --color-bg: #F9FAFB; --color-card: #ffffff; --color-border: #E5E7EB;
    --color-text: #1e293b; --color-text-muted: #64748b;
    --color-primary: #3b82f6; --color-primary-light: #eff6ff;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
         background: var(--color-bg); color: var(--color-text); line-height: 1.6; }
  .nav { background: linear-gradient(135deg, var(--color-navy) 0%, var(--color-navy-light) 100%);
         padding: 12px 24px; color: #fff; display: flex; align-items: center; justify-content: space-between; }
  .nav h1 { font-size: 1.1rem; font-weight: 700; }
  .nav .meta { font-size: .8rem; opacity: .85; font-family: 'JetBrains Mono', monospace; }
  .container { max-width: 980px; margin: 0 auto; padding: 1.5rem; }
  .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px,1fr)); gap: .75rem; margin-bottom: 1.5rem; }
  .card { background: var(--color-card); border: 1px solid var(--color-border); border-radius: 10px;
          padding: .9rem; text-align: center; border-left: 4px solid transparent; }
  .card.passed { border-left-color: var(--color-passed); } .card.passed .n { color: var(--color-passed); }
  .card.failed { border-left-color: var(--color-failed); } .card.failed .n { color: var(--color-failed); }
  .card.skipped { border-left-color: var(--color-skipped); } .card.skipped .n { color: var(--color-skipped); }
  .card.blocked { border-left-color: var(--color-blocked); } .card.blocked .n { color: var(--color-blocked); }
  .card.total { border-left-color: var(--color-primary); } .card.total .n { color: var(--color-primary); }
  .card .n { font-size: 1.8rem; font-weight: 700; line-height: 1; }
  .card .l { color: var(--color-text-muted); margin-top: .35rem; text-transform: uppercase; font-size: .65rem; letter-spacing: .05em; }
  .test { background: var(--color-card); border: 1px solid var(--color-border); border-radius: 12px; padding: 1.5rem; }
  .test h2 { font-size: 1.3rem; margin-bottom: .25rem; }
  .test .tid { font-family: 'JetBrains Mono', monospace; color: var(--color-text-muted); font-size: .85rem; }
  .field { margin-top: 1rem; } .field .k { font-weight: 600; font-size: .8rem; text-transform: uppercase; color: var(--color-text-muted); }
  .field .v { white-space: pre-wrap; margin-top: .25rem; }
  .btns { display: flex; gap: .6rem; margin-top: 1.5rem; flex-wrap: wrap; }
  button { font: inherit; font-weight: 600; border: none; border-radius: 8px; padding: .7rem 1.4rem; cursor: pointer; color: #fff; }
  button:disabled { opacity: .5; cursor: not-allowed; }
  .pass { background: var(--color-passed); } .fail { background: var(--color-failed); }
  .blocked { background: var(--color-blocked); } .skip { background: var(--color-skipped); }
  .finalize { background: var(--color-primary); }
  textarea { width: 100%; margin-top: 1rem; border: 1px solid var(--color-border); border-radius: 8px;
             padding: .7rem; font: inherit; resize: vertical; min-height: 70px; }
  .drop { margin-top: 1rem; border: 2px dashed var(--color-border); border-radius: 8px; padding: 1rem;
          text-align: center; color: var(--color-text-muted); font-size: .85rem; }
  .drop.over { border-color: var(--color-primary); background: var(--color-primary-light); }
  .shots { display: flex; gap: .5rem; flex-wrap: wrap; margin-top: .75rem; }
  .shots span { font-family: 'JetBrains Mono', monospace; font-size: .75rem; background: var(--color-bg); padding: .2rem .5rem; border-radius: 4px; }
  .msg { margin-top: 1rem; padding: .7rem 1rem; border-radius: 8px; font-size: .9rem; display: none; }
  .msg.err { display: block; background: var(--color-failed-bg); color: var(--color-failed); }
  .msg.ok { display: block; background: var(--color-passed-bg); color: var(--color-passed); }
  .empty { text-align: center; padding: 3rem; color: var(--color-text-muted); }
</style>
</head>
<body>
  <div class="nav"><h1>Spectra — Execution Console</h1><div class="meta" id="meta"></div></div>
  <div class="container">
    <div class="summary-grid" id="summary"></div>
    <div id="panel"><div class="empty">Loading…</div></div>
    <div class="msg" id="msg"></div>
  </div>
<script>
// The browser holds NO run state (FR-002/FR-003). Every render derives from the latest /current payload.
let busy = false;
const $ = (id) => document.getElementById(id);

function showMsg(text, kind) {
  const m = $('msg'); m.textContent = text; m.className = 'msg ' + kind;
  if (kind === 'ok') setTimeout(() => { m.className = 'msg'; }, 2500);
}

async function refresh() {
  try {
    const r = await fetch('/current');
    const d = await r.json();
    render(d);
  } catch (e) { /* transient; next poll retries */ }
}

function counts(d) {
  const c = d.counts || {};
  const cell = (cls, n, l) => `<div class="card ${cls}"><div class="n">${n}</div><div class="l">${l}</div></div>`;
  $('summary').innerHTML =
    cell('total', d.total || 0, 'Total') +
    cell('passed', c.PASSED || 0, 'Passed') +
    cell('failed', c.FAILED || 0, 'Failed') +
    cell('blocked', c.BLOCKED || 0, 'Blocked') +
    cell('skipped', c.SKIPPED || 0, 'Skipped');
}

function render(d) {
  $('meta').textContent = d.runId ? `${d.suite} · ${d.runStatus} · ${d.runId.slice(0,8)}` : '';
  counts(d);
  const panel = $('panel');
  if (d.runStatus === 'none') { panel.innerHTML = `<div class="empty">${d.message || 'No active run.'}</div>`; return; }
  if (!d.current) {
    panel.innerHTML = `<div class="test"><div class="empty">All tests recorded.</div>
      <div class="btns"><button class="finalize" onclick="finalize()">Finalize run &amp; generate report</button></div></div>`;
    return;
  }
  const t = d.current;
  const fld = (k, v) => v ? `<div class="field"><div class="k">${k}</div><div class="v">${esc(v)}</div></div>` : '';
  const shots = (t.screenshotPaths || []).map(p => `<span>${esc(p)}</span>`).join('');
  panel.innerHTML = `<div class="test">
    <h2>${esc(t.title || t.testId)}</h2>
    <div class="tid">${esc(t.testId)}${t.priority ? ' · ' + esc(t.priority) : ''}${t.component ? ' · ' + esc(t.component) : ''}</div>
    ${fld('Preconditions', t.preconditions)}
    ${fld('Steps', t.steps)}
    ${fld('Expected result', t.expectedResult)}
    <textarea id="comment" placeholder="Comment / observations (required for FAIL, BLOCKED, SKIP)"></textarea>
    <div class="btns">
      <button class="pass" onclick="advance('pass')">PASS</button>
      <button class="fail" onclick="advance('fail')">FAIL</button>
      <button class="blocked" onclick="advance('blocked')">BLOCKED</button>
      <button class="skip" onclick="advance('skip')">SKIP</button>
    </div>
    <div class="drop" id="drop">Drop or paste a screenshot here to attach it</div>
    <div class="shots">${shots}</div>
  </div>`;
  wireDrop();
}

function esc(s) { return String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }

async function post(path, body) {
  const r = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const d = await r.json().catch(() => ({}));
  return { ok: r.ok, status: r.status, d };
}

async function advance(status) {
  if (busy) return; busy = true;
  const notes = ($('comment') || {}).value || '';
  const res = await post('/advance', { status, notes });
  busy = false;
  if (!res.ok) { showMsg(res.d.message || res.d.error_code || 'Error', 'err'); return; }
  showMsg('Recorded ' + status.toUpperCase(), 'ok');
  if (res.d.current) render(res.d.current); else refresh();
}

async function finalize() {
  if (busy) return; busy = true;
  const res = await post('/finalize', { force: false });
  busy = false;
  if (!res.ok) { showMsg(res.d.message || 'Error', 'err'); return; }
  showMsg('Run finalized: ' + (res.d.report || ''), 'ok');
  refresh();
}

function wireDrop() {
  const drop = $('drop'); if (!drop) return;
  drop.addEventListener('dragover', e => { e.preventDefault(); drop.classList.add('over'); });
  drop.addEventListener('dragleave', () => drop.classList.remove('over'));
  drop.addEventListener('drop', e => { e.preventDefault(); drop.classList.remove('over');
    const f = e.dataTransfer.files[0]; if (f) sendFile(f); });
}

document.addEventListener('paste', e => {
  const items = (e.clipboardData || {}).items || [];
  for (const it of items) if (it.type && it.type.startsWith('image/')) { const f = it.getAsFile(); if (f) sendFile(f); }
});

function sendFile(file) {
  const reader = new FileReader();
  reader.onload = async () => {
    const res = await post('/screenshot', { dataUrl: reader.result });
    if (!res.ok) { showMsg(res.d.message || 'Screenshot failed', 'err'); return; }
    showMsg('Screenshot attached', 'ok'); refresh();
  };
  reader.readAsDataURL(file);
}

refresh();
setInterval(refresh, 1800);
</script>
</body>
</html>
""";
}
