using System.Text;

namespace Spectra.CLI.Progress;

// THROWAWAY: superseded when the execution console (Spec 066/067) generalises to authoring.
// Writes a lightweight HTML poller that reads .spectra/progress.json and renders a live phase list.
// Reuses ProgressPageWriter's CSS palette so it looks identical to the PM progress page.
public static class SeamProgressPageWriter
{
    public static async Task WriteAsync(string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var html = BuildHtml();
        var tempPath = outputPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, html, Encoding.UTF8);
        File.Move(tempPath, outputPath, overwrite: true);
    }

    private static string BuildHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <meta http-equiv="cache-control" content="no-cache">
            <title>SPECTRA — Seam Progress</title>
            <link rel="preconnect" href="https://fonts.googleapis.com">
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
            <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
            <style>
                :root {
                    --color-navy: #1B2A4A;
                    --color-navy-light: #2D3F5E;
                    --color-passed: #16a34a;
                    --color-passed-bg: #dcfce7;
                    --color-failed: #dc2626;
                    --color-bg: #F9FAFB;
                    --color-card: #ffffff;
                    --color-border: #E5E7EB;
                    --color-text: #1e293b;
                    --color-text-muted: #64748b;
                    --color-primary: #3b82f6;
                    --color-primary-light: #eff6ff;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    background: var(--color-bg);
                    color: var(--color-text);
                    line-height: 1.6;
                }
                .nav {
                    background: linear-gradient(135deg, var(--color-navy) 0%, var(--color-navy-light) 100%);
                    padding: 12px 24px;
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    box-shadow: 0 2px 4px rgba(0,0,0,0.15);
                }
                .nav-logo { color: white; font-size: 1.25rem; font-weight: 700; letter-spacing: 0.05em; }
                .nav-title { color: rgba(255,255,255,0.7); font-size: 0.9rem; font-weight: 400; }
                .container { max-width: 640px; margin: 0 auto; padding: 2rem; }
                .status-card {
                    background: var(--color-card);
                    border-radius: 12px;
                    padding: 2rem;
                    border: 1px solid var(--color-border);
                    box-shadow: 0 1px 3px rgba(0,0,0,0.08);
                    margin-bottom: 1.5rem;
                }
                .status-badge {
                    display: inline-flex;
                    align-items: center;
                    gap: 8px;
                    padding: 6px 16px;
                    border-radius: 20px;
                    font-size: 0.8rem;
                    font-weight: 600;
                    text-transform: uppercase;
                    letter-spacing: 0.05em;
                    margin-bottom: 1rem;
                }
                .status-badge.running { background: var(--color-primary-light); color: var(--color-primary); }
                .status-badge.completed { background: var(--color-passed-bg); color: var(--color-passed); }
                .status-badge.waiting { background: #f1f5f9; color: var(--color-text-muted); }
                @keyframes spin { to { transform: rotate(360deg); } }
                .spinner {
                    display: inline-block;
                    width: 14px; height: 14px;
                    border: 2px solid currentColor;
                    border-top-color: transparent;
                    border-radius: 50%;
                    animation: spin 0.8s linear infinite;
                    vertical-align: middle;
                }
                .phase-list { list-style: none; margin-top: 1.5rem; }
                .phase-item {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    padding: 10px 0;
                    border-bottom: 1px solid var(--color-border);
                    font-size: 0.9rem;
                    color: var(--color-text-muted);
                }
                .phase-item:last-child { border-bottom: none; }
                .phase-item.done { color: var(--color-passed); }
                .phase-item.active { color: var(--color-primary); font-weight: 600; }
                .phase-dot {
                    width: 10px; height: 10px;
                    border-radius: 50%;
                    background: var(--color-border);
                    flex-shrink: 0;
                }
                .phase-item.done .phase-dot { background: var(--color-passed); }
                .phase-item.active .phase-dot { background: var(--color-primary); box-shadow: 0 0 0 3px rgba(59,130,246,0.2); }
                .phase-label { flex: 1; }
                .phase-loop { font-size: 0.8rem; color: var(--color-text-muted); font-family: 'JetBrains Mono', monospace; }
                .phase-item.active .phase-loop { color: var(--color-primary); }
                .footer { text-align: center; margin-top: 2rem; color: var(--color-text-muted); font-size: 0.75rem; }
                #error { color: var(--color-failed); font-size: 0.85rem; padding: 1rem; text-align: center; }
            </style>
        </head>
        <body>
            <nav class="nav">
                <span class="nav-logo">SPECTRA</span>
                <span class="nav-title">Seam Progress</span>
            </nav>
            <div class="container">
                <div class="status-card">
                    <div id="badge" class="status-badge waiting">Waiting</div>
                    <ul id="phase-list" class="phase-list"></ul>
                    <div id="error"></div>
                </div>
                <div class="footer">Auto-refreshes every 1.5 s &mdash; powered by <strong>.spectra/progress.json</strong></div>
            </div>
            <script>
                let lastJson = '';
                function render(data) {
                    const phases = data.phases || [];
                    const active = typeof data.active === 'number' ? data.active : -1;
                    const loop = data.loop || null;
                    const done = active >= phases.length;

                    const badge = document.getElementById('badge');
                    badge.className = 'status-badge ' + (done ? 'completed' : active >= 0 ? 'running' : 'waiting');
                    badge.innerHTML = done
                        ? '&#10003; Complete'
                        : (active >= 0 ? '<span class="spinner"></span> Running' : 'Waiting');

                    const list = document.getElementById('phase-list');
                    list.innerHTML = phases.map((p, i) => {
                        const cls = i < active ? 'done' : i === active ? 'active' : '';
                        const icon = i < active ? '&#10003;' : i === active ? '<span class="spinner"></span>' : '&#9675;';
                        const loopLabel = (i === active && loop) ? `<span class="phase-loop">${loop.label || ''}</span>` : '';
                        return `<li class="phase-item ${cls}"><span class="phase-dot"></span><span class="phase-label">${p}</span>${loopLabel}</li>`;
                    }).join('');
                }

                async function poll() {
                    try {
                        const r = await fetch('progress.json?' + Date.now());
                        if (!r.ok) { document.getElementById('error').textContent = 'Waiting for progress.json…'; return; }
                        const text = await r.text();
                        if (text === lastJson) return;
                        lastJson = text;
                        document.getElementById('error').textContent = '';
                        render(JSON.parse(text));
                    } catch (e) {
                        document.getElementById('error').textContent = 'Waiting for progress.json…';
                    }
                }
                poll();
                setInterval(poll, 1500);
            </script>
        </body>
        </html>
        """;
}
