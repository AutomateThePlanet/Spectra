using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Spectra.CLI.Progress;

/// <summary>
/// Generates a self-contained HTML progress page that auto-refreshes to show live CLI status.
/// The JSON data is embedded inline (no fetch needed), and <meta refresh> handles polling.
/// </summary>
public static class ProgressPageWriter
{
    /// <summary>
    /// Writes a self-contained HTML progress page with embedded JSON data.
    /// </summary>
    /// <param name="htmlPath">Path to write the HTML file.</param>
    /// <param name="jsonData">Current result JSON to embed inline.</param>
    /// <param name="isTerminal">True if status is completed/failed/cancelled — omits auto-refresh.</param>
    public static void WriteProgressPage(string htmlPath, string jsonData, bool isTerminal, string? title = null)
    {
        try
        {
            var workspaceRoot = Path.GetDirectoryName(htmlPath) ?? Directory.GetCurrentDirectory();
            var html = BuildHtml(jsonData, isTerminal, workspaceRoot, title);
            var tempPath = htmlPath + ".tmp";
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(fs, Encoding.UTF8))
            {
                writer.Write(html);
                writer.Flush();
                fs.Flush(true);
            }
            File.Move(tempPath, htmlPath, overwrite: true);
        }
        catch
        {
            // Non-critical — don't fail the CLI command
        }
    }

    /// <summary>
    /// No-op. The user opens the progress page by asking Copilot Chat
    /// "open spectra progress" which uses VS Code's Simple Browser (inline).
    /// </summary>
    public static void OpenInBrowser(string htmlPath)
    {
    }

    private static string BuildHtml(string jsonData, bool isTerminal, string workspaceRoot, string? title = null)
    {
        var refreshTag = ""; // Refresh handled by JavaScript below
        var escapedJson = HttpUtility.HtmlEncode(jsonData);
        var escapedRoot = HttpUtility.HtmlEncode(workspaceRoot.Replace('\\', '/'));

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <meta http-equiv="cache-control" content="no-cache">
                <meta http-equiv="pragma" content="no-cache">
                {{refreshTag}}
                <title>SPECTRA — {{Escape(title ?? "Progress")}}</title>
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
                        --color-failed-bg: #fee2e2;
                        --color-bg: #F9FAFB;
                        --color-card: #ffffff;
                        --color-border: #E5E7EB;
                        --color-text: #1e293b;
                        --color-text-muted: #64748b;
                        --color-primary: #3b82f6;
                        --color-primary-light: #eff6ff;
                        --color-warning: #d97706;
                        --color-warning-bg: #fffbeb;
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
                    .nav-logo {
                        color: white;
                        font-size: 1.25rem;
                        font-weight: 700;
                        letter-spacing: 0.05em;
                    }
                    .nav-title {
                        color: rgba(255,255,255,0.7);
                        font-size: 0.9rem;
                        font-weight: 400;
                    }
                    .container { max-width: 800px; margin: 0 auto; padding: 2rem; }

                    /* Phase Stepper */
                    .stepper {
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        gap: 0;
                        margin-bottom: 2rem;
                        padding: 1.5rem;
                        background: var(--color-card);
                        border-radius: 12px;
                        border: 1px solid var(--color-border);
                    }
                    .step {
                        display: flex;
                        align-items: center;
                        gap: 8px;
                        font-size: 0.85rem;
                        color: var(--color-text-muted);
                        font-weight: 500;
                    }
                    .step.active { color: var(--color-primary); font-weight: 600; }
                    .step.done { color: var(--color-passed); }
                    .step.error { color: var(--color-failed); }
                    .step-dot {
                        width: 12px;
                        height: 12px;
                        border-radius: 50%;
                        background: var(--color-border);
                        flex-shrink: 0;
                    }
                    .step.active .step-dot { background: var(--color-primary); box-shadow: 0 0 0 4px rgba(59,130,246,0.2); }
                    .step.done .step-dot { background: var(--color-passed); }
                    .step.error .step-dot { background: var(--color-failed); }
                    .step-line {
                        width: 40px;
                        height: 2px;
                        background: var(--color-border);
                        margin: 0 4px;
                        flex-shrink: 0;
                    }
                    .step-line.done { background: var(--color-passed); }

                    /* Status Card */
                    .status-card {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 2rem;
                        text-align: center;
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
                    .status-badge.analyzing, .status-badge.scanning, .status-badge.scanning-tests, .status-badge.scanning-docs, .status-badge.classifying { background: var(--color-primary-light); color: var(--color-primary); }
                    .status-badge.analyzed { background: var(--color-passed-bg); color: var(--color-passed); }
                    .status-badge.generating, .status-badge.indexing, .status-badge.extracting-criteria, .status-badge.extracting, .status-badge.updating, .status-badge.verifying, .status-badge.analyzing-docs, .status-badge.analyzing-criteria, .status-badge.analyzing-automation, .status-badge.building-index, .status-badge.collecting-data, .status-badge.generating-html { background: var(--color-warning-bg); color: var(--color-warning); }
                    .status-badge.completed, .status-badge.success { background: var(--color-passed-bg); color: var(--color-passed); }
                    .status-badge.failed { background: var(--color-failed-bg); color: var(--color-failed); }
                    .status-badge.cancelled { background: #fef3c7; color: #b45309; }
                    .step.cancelled { color: #b45309; }
                    .step.cancelled .step-dot { background: #f59e0b; background-image: repeating-linear-gradient(45deg, transparent, transparent 3px, rgba(0,0,0,0.15) 3px, rgba(0,0,0,0.15) 6px); }
                    .suite-name { font-size: 1.5rem; font-weight: 700; margin-bottom: 0.5rem; }
                    .status-message {
                        color: var(--color-text-muted);
                        font-size: 0.95rem;
                        margin-bottom: 0.75rem;
                        min-height: 1.5em;
                    }
                    .status-time { color: var(--color-text-muted); font-size: 0.8rem; opacity: 0.7; }

                    /* Spinner */
                    @keyframes spin { to { transform: rotate(360deg); } }
                    .spinner {
                        display: inline-block;
                        width: 16px;
                        height: 16px;
                        border: 2px solid currentColor;
                        border-top-color: transparent;
                        border-radius: 50%;
                        animation: spin 0.8s linear infinite;
                        vertical-align: middle;
                    }

                    /* Summary Grid */
                    .summary-grid {
                        display: grid;
                        grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
                        gap: 1rem;
                        margin-bottom: 1.5rem;
                    }
                    .summary-card {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1.25rem;
                        text-align: center;
                        border: 1px solid var(--color-border);
                        box-shadow: 0 1px 2px rgba(0,0,0,0.05);
                    }
                    .summary-number {
                        font-size: 2.25rem;
                        font-weight: 700;
                        line-height: 1;
                        color: var(--color-primary);
                    }
                    .summary-number.green { color: var(--color-passed); }
                    .summary-number.red { color: var(--color-failed); }
                    .summary-number.muted { color: var(--color-text-muted); }
                    .summary-label {
                        color: var(--color-text-muted);
                        margin-top: 0.5rem;
                        text-transform: uppercase;
                        font-size: 0.7rem;
                        letter-spacing: 0.05em;
                        font-weight: 600;
                    }

                    /* Analysis Breakdown */
                    .breakdown {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1.5rem;
                        border: 1px solid var(--color-border);
                        margin-bottom: 1.5rem;
                    }
                    .breakdown h3 {
                        font-size: 0.9rem;
                        font-weight: 600;
                        margin-bottom: 1rem;
                        color: var(--color-text);
                    }
                    .breakdown-row {
                        display: flex;
                        justify-content: space-between;
                        padding: 6px 0;
                        border-bottom: 1px solid var(--color-border);
                        font-size: 0.9rem;
                    }
                    .breakdown-row:last-child { border-bottom: none; }
                    .breakdown-count { font-weight: 600; color: var(--color-primary); }

                    /* Files List */
                    .files-section {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1.5rem;
                        border: 1px solid var(--color-border);
                        margin-bottom: 1.5rem;
                    }
                    .files-section h3 {
                        font-size: 0.9rem;
                        font-weight: 600;
                        margin-bottom: 1rem;
                    }
                    .file-item {
                        font-family: 'JetBrains Mono', monospace;
                        font-size: 0.8rem;
                        padding: 6px 10px;
                        background: var(--color-bg);
                        border-radius: 6px;
                        margin-bottom: 4px;
                        color: var(--color-text);
                        display: flex;
                        align-items: center;
                        gap: 8px;
                    }
                    .file-icon { opacity: 0.5; }
                    a.file-item {
                        text-decoration: none;
                        cursor: pointer;
                        transition: background 0.15s ease;
                    }
                    a.file-item:hover {
                        background: var(--color-primary-light);
                        color: var(--color-primary);
                    }

                    /* Spec 041: Live progress bars (generation + verification) */
                    .progress-section {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1rem 1.5rem;
                        border: 1px solid var(--color-border);
                        margin-bottom: 0.75rem;
                        transition: opacity 0.3s ease;
                    }
                    .progress-section.dimmed { opacity: 0.4; }
                    .progress-label {
                        display: flex;
                        justify-content: space-between;
                        font-size: 0.85rem;
                        font-weight: 600;
                        color: var(--color-text);
                        margin-bottom: 0.5rem;
                    }
                    .progress-label-count {
                        font-family: 'JetBrains Mono', monospace;
                        color: var(--color-text-muted);
                        font-weight: 500;
                    }
                    .progress-bar-track {
                        width: 100%;
                        height: 10px;
                        background: var(--color-bg);
                        border-radius: 5px;
                        overflow: hidden;
                        border: 1px solid var(--color-border);
                    }
                    .progress-bar-fill {
                        height: 100%;
                        background: linear-gradient(90deg, var(--color-primary) 0%, #5fa3ff 100%);
                        border-radius: 5px;
                        transition: width 0.4s ease;
                    }
                    .progress-bar-fill.verifying {
                        background: linear-gradient(90deg, #14b8a6 0%, #5eead4 100%);
                    }
                    .progress-detail {
                        font-size: 0.75rem;
                        color: var(--color-text-muted);
                        margin-top: 0.4rem;
                        font-family: 'JetBrains Mono', monospace;
                    }

                    /* Verification progress */
                    .verification-section {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1.5rem;
                        border: 1px solid var(--color-border);
                        margin-bottom: 1.5rem;
                    }
                    .verification-section h3 {
                        font-size: 0.9rem;
                        font-weight: 600;
                        margin-bottom: 1rem;
                    }
                    .verification-item {
                        font-size: 0.8rem;
                        padding: 8px 12px;
                        border-radius: 6px;
                        margin-bottom: 4px;
                        display: flex;
                        flex-wrap: wrap;
                        align-items: center;
                        gap: 8px;
                        border-left: 3px solid transparent;
                    }
                    .verdict-grounded { background: var(--color-passed-bg); border-left-color: var(--color-passed); }
                    .verdict-partial { background: #fef9c3; border-left-color: #ca8a04; }
                    .verdict-hallucinated { background: var(--color-failed-bg); border-left-color: var(--color-failed); }
                    .verdict-icon { font-weight: 700; font-size: 0.9rem; }
                    .verdict-grounded .verdict-icon { color: var(--color-passed); }
                    .verdict-partial .verdict-icon { color: #ca8a04; }
                    .verdict-hallucinated .verdict-icon { color: var(--color-failed); }
                    .verdict-id {
                        font-family: 'JetBrains Mono', monospace;
                        font-weight: 600;
                        color: var(--color-text);
                    }
                    .verdict-title { color: var(--color-text); flex: 1; min-width: 0; }
                    .verdict-badge {
                        font-size: 0.7rem;
                        font-weight: 600;
                        text-transform: uppercase;
                        padding: 2px 8px;
                        border-radius: 10px;
                        letter-spacing: 0.03em;
                    }
                    .verdict-grounded .verdict-badge { background: var(--color-passed); color: white; }
                    .verdict-partial .verdict-badge { background: #ca8a04; color: white; }
                    .verdict-hallucinated .verdict-badge { background: var(--color-failed); color: white; }
                    .verdict-reason {
                        width: 100%;
                        font-size: 0.75rem;
                        color: var(--color-text-muted);
                        font-style: italic;
                        padding-left: 24px;
                    }

                    /* Rejected tests */
                    .rejected-section {
                        background: var(--color-card);
                        border-radius: 12px;
                        padding: 1.5rem;
                        border: 1px solid var(--color-border);
                        margin-bottom: 1.5rem;
                    }
                    .rejected-section h3 {
                        font-size: 0.9rem;
                        font-weight: 600;
                        margin-bottom: 1rem;
                        color: var(--color-failed);
                    }
                    .rejected-item {
                        font-size: 0.8rem;
                        padding: 8px 10px;
                        background: var(--color-failed-bg);
                        border-radius: 6px;
                        margin-bottom: 4px;
                        border-left: 3px solid var(--color-failed);
                    }
                    .rejected-id {
                        font-family: 'JetBrains Mono', monospace;
                        font-weight: 600;
                        color: var(--color-failed);
                    }
                    .rejected-title {
                        color: var(--color-text);
                    }
                    .rejected-reason {
                        font-size: 0.75rem;
                        color: var(--color-text-muted);
                        margin-top: 4px;
                        font-style: italic;
                    }

                    /* Error */
                    .error-card {
                        background: var(--color-failed-bg);
                        border: 1px solid var(--color-failed);
                        border-radius: 12px;
                        padding: 1.5rem;
                        margin-bottom: 1.5rem;
                    }
                    .error-card h3 { color: var(--color-failed); margin-bottom: 0.5rem; font-size: 1rem; }
                    .error-card p { color: var(--color-text); font-size: 0.9rem; }

                    /* Footer */
                    .footer {
                        text-align: center;
                        padding: 1.5rem;
                        color: var(--color-text-muted);
                        font-size: 0.8rem;
                    }
                    @keyframes pulse { 0%, 100% { opacity: 0.4; } 50% { opacity: 1; } }
                    .refresh-indicator {
                        display: inline-flex;
                        align-items: center;
                        gap: 6px;
                    }
                    .refresh-dot {
                        width: 6px;
                        height: 6px;
                        border-radius: 50%;
                        background: var(--color-primary);
                        animation: pulse 2s ease-in-out infinite;
                    }

                    /* Responsive — narrow VS Code panels and small screens */
                    @media (max-width: 600px) {
                        .container { padding: 1rem; }
                        .nav { padding: 10px 16px; }
                        .nav-logo { font-size: 1.1rem; }
                        .nav-title { font-size: 0.8rem; }

                        .stepper {
                            flex-wrap: wrap;
                            gap: 4px 0;
                            padding: 1rem;
                            justify-content: flex-start;
                        }
                        .step { font-size: 0.75rem; gap: 5px; }
                        .step-line { width: 20px; margin: 0 2px; }
                        .step-dot { width: 10px; height: 10px; }

                        .status-card { padding: 1.25rem; }
                        .suite-name { font-size: 1.2rem; }
                        .status-message { font-size: 0.85rem; }

                        .summary-grid { grid-template-columns: repeat(auto-fit, minmax(100px, 1fr)); gap: 0.5rem; }
                        .summary-card { padding: 0.75rem; }
                        .summary-number { font-size: 1.5rem; }
                        .summary-label { font-size: 0.65rem; }

                        .breakdown { padding: 1rem; }
                        .files-section { padding: 1rem; }
                        .file-item { font-size: 0.7rem; padding: 5px 8px; }
                    }

                    @media (max-width: 400px) {
                        .container { padding: 0.5rem; }
                        .stepper { padding: 0.75rem; }
                        .step { font-size: 0.7rem; }
                        .step-line { width: 12px; }
                        .status-card { padding: 1rem; }
                        .summary-grid { grid-template-columns: 1fr 1fr; }
                    }
                    }
                </style>
            </head>
            <body>
                <nav class="nav">
                    <span class="nav-logo">SPECTRA</span>
                    <span class="nav-title">{{Escape(title ?? "Progress")}}</span>
                </nav>
                <div class="container" data-workspace="{{escapedRoot}}">
                    {{BuildBody(jsonData, isTerminal, workspaceRoot)}}
                </div>
                <script>
                    // Restore scroll position if we just auto-reloaded.
                    // sessionStorage is per-tab so two parallel runs in different
                    // tabs don't fight each other.
                    (function() {
                        try {
                            var key = 'spectra-progress-scroll';
                            var saved = sessionStorage.getItem(key);
                            if (saved !== null) {
                                window.scrollTo(0, parseInt(saved, 10) || 0);
                            }
                        } catch (e) { /* sessionStorage may be unavailable */ }
                    })();

                    // Auto-refresh policy:
                    //   - In-progress pages: poll every 1.5s, saving scroll position
                    //     before each reload so the user doesn't get bounced to top.
                    //   - Terminal pages (completed/failed): poll every 5s. v1.43.0
                    //     fix: previously this case stopped polling entirely, which
                    //     meant a stale Completed view stayed stale even after the
                    //     user kicked off a new run that rewrote .spectra-progress.html.
                    //     Slow polling lets the page auto-detect the new run without
                    //     requiring the agent to re-open the browser.
                    (function() {
                        var isTerminal = {{(isTerminal ? "true" : "false")}};
                        if (isTerminal) {
                            try { sessionStorage.removeItem('spectra-progress-scroll'); } catch (e) {}
                        }
                        setInterval(function() {
                            try {
                                sessionStorage.setItem('spectra-progress-scroll', String(window.scrollY));
                            } catch (e) {}
                            var base = window.location.pathname;
                            window.location.replace(base + '?_=' + Date.now());
                        }, isTerminal ? 5000 : 1500);
                    })();

                    // File links: open vscode:// URIs via JavaScript click handler
                    // (browsers block direct <a href="vscode://..."> from file:// pages)
                    document.addEventListener('click', function(e) {
                        var link = e.target.closest('[data-vscode-uri]');
                        if (link) {
                            e.preventDefault();
                            window.location.href = link.getAttribute('data-vscode-uri');
                        }
                    });
                </script>
            </body>
            </html>
            """;
    }

    private static string BuildBody(string jsonData, bool isTerminal, string workspaceRoot)
    {
        // Parse JSON to extract fields for server-side rendering
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown";
            var command = root.TryGetProperty("command", out var cmd) ? cmd.GetString() ?? "" : "";
            var suite = root.TryGetProperty("suite", out var su) ? su.GetString() ?? "" : "";
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            var error = root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            var timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "";

            var sb = new StringBuilder();

            // Phase stepper
            sb.Append(BuildStepper(status, command));

            // Spec 041: live progress bars (generation + verification or update)
            if (root.TryGetProperty("progress", out var progress)
                && progress.ValueKind == JsonValueKind.Object)
            {
                sb.Append(RenderProgressBars(progress));
            }

            // Status card
            sb.Append(BuildStatusCard(status, suite, message, timestamp));

            // Analysis breakdown (when analyzed or later)
            if (root.TryGetProperty("analysis", out var analysis))
            {
                sb.Append(BuildAnalysisSection(analysis));
            }

            // Generation summary cards
            if (root.TryGetProperty("generation", out var gen))
            {
                var written = gen.TryGetProperty("tests_written", out var tw) ? tw.GetInt32() : 0;
                var generated = gen.TryGetProperty("tests_generated", out var tg) ? tg.GetInt32() : 0;
                var rejected = gen.TryGetProperty("tests_rejected_by_critic", out var tr) ? tr.GetInt32() : 0;
                var requested = gen.TryGetProperty("tests_requested", out var treq) ? treq.GetInt32() : 0;

                if (written > 0 || generated > 0 || requested > 0)
                {
                    sb.Append(BuildGenerationCards(requested, generated, written, rejected));
                }
            }

            // Verification progress
            if (root.TryGetProperty("verification", out var verification) && verification.GetArrayLength() > 0)
            {
                sb.Append(BuildVerificationSection(verification));
            }

            // Docs index summary cards
            if (root.TryGetProperty("documents_total", out var dt2))
            {
                var docsTotal = dt2.GetInt32();
                var docsIndexed = root.TryGetProperty("documents_indexed", out var di) ? di.GetInt32() : 0;
                var docsSkipped = root.TryGetProperty("documents_skipped", out var ds) ? ds.GetInt32() : 0;
                var criteriaExtracted = root.TryGetProperty("criteria_extracted", out var ce) ? ce.GetInt32() : (int?)null;

                sb.Append("""<div class="summary-grid">""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{docsTotal}</div><div class="summary-label">Documents Found</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number green">{docsIndexed}</div><div class="summary-label">Indexed</div></div>""");
                if (docsSkipped > 0)
                    sb.Append($"""<div class="summary-card"><div class="summary-number muted">{docsSkipped}</div><div class="summary-label">Skipped</div></div>""");
                if (criteriaExtracted.HasValue)
                    sb.Append($"""<div class="summary-card"><div class="summary-number">{criteriaExtracted.Value}</div><div class="summary-label">Criteria Extracted</div></div>""");
                sb.Append("</div>");
            }

            // Coverage summary cards
            if (root.TryGetProperty("documentationCoverage", out var docCov))
            {
                var docPct = docCov.TryGetProperty("percentage", out var dp) ? dp.GetDecimal() : 0;
                var critPct = root.TryGetProperty("acceptanceCriteriaCoverage", out var critCov)
                    && critCov.TryGetProperty("percentage", out var cp) ? cp.GetDecimal() : 0;
                var autoPct = root.TryGetProperty("automationCoverage", out var autoCov)
                    && autoCov.TryGetProperty("percentage", out var ap) ? ap.GetDecimal() : 0;

                sb.Append("""<div class="summary-grid">""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{docPct:F0}%</div><div class="summary-label">Doc Coverage</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{critPct:F0}%</div><div class="summary-label">Criteria Coverage</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{autoPct:F0}%</div><div class="summary-label">Automation</div></div>""");
                sb.Append("</div>");
            }

            // Update summary cards
            if (root.TryGetProperty("testsUpdated", out var tu))
            {
                var updated = tu.GetInt32();
                var removed = root.TryGetProperty("testsRemoved", out var tr2) ? tr2.GetInt32() : 0;
                var unchanged = root.TryGetProperty("testsUnchanged", out var tun) ? tun.GetInt32() : 0;

                sb.Append("""<div class="summary-grid">""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{updated}</div><div class="summary-label">Updated</div></div>""");
                if (removed > 0)
                    sb.Append($"""<div class="summary-card"><div class="summary-number red">{removed}</div><div class="summary-label">Removed</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number muted">{unchanged}</div><div class="summary-label">Unchanged</div></div>""");
                sb.Append("</div>");
            }

            // Extract criteria summary cards
            if (root.TryGetProperty("criteriaNew", out var cn))
            {
                var newCriteria = cn.GetInt32();
                var updatedCriteria = root.TryGetProperty("criteriaUpdated", out var cu) ? cu.GetInt32() : 0;
                var totalCriteria = root.TryGetProperty("totalCriteria", out var tc) ? tc.GetInt32() : 0;
                var docsProcessed = root.TryGetProperty("documentsProcessed", out var dpx) ? dpx.GetInt32() : 0;

                sb.Append("""<div class="summary-grid">""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{docsProcessed}</div><div class="summary-label">Docs Processed</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number green">{newCriteria}</div><div class="summary-label">New Criteria</div></div>""");
                if (updatedCriteria > 0)
                    sb.Append($"""<div class="summary-card"><div class="summary-number">{updatedCriteria}</div><div class="summary-label">Updated</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{totalCriteria}</div><div class="summary-label">Total Criteria</div></div>""");
                sb.Append("</div>");
            }

            // Dashboard summary cards
            if (root.TryGetProperty("suitesIncluded", out var si))
            {
                var suites = si.GetInt32();
                var tests = root.TryGetProperty("testsIncluded", out var ti) ? ti.GetInt32() : 0;

                sb.Append("""<div class="summary-grid">""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{suites}</div><div class="summary-label">Suites</div></div>""");
                sb.Append($"""<div class="summary-card"><div class="summary-number">{tests}</div><div class="summary-label">Tests</div></div>""");
                sb.Append("</div>");
            }

            // Spec 040: Token usage summary (generate/update)
            if (root.TryGetProperty("token_usage", out var tokenUsage))
            {
                sb.Append(BuildTokenUsageSection(tokenUsage));
            }

            // Error section
            if (status == "failed" && !string.IsNullOrEmpty(error))
            {
                sb.Append($"""
                    <div class="error-card">
                        <h3>Generation Failed</h3>
                        <p>{Escape(error)}</p>
                    </div>
                    """);
            }

            // Rejected tests
            if (root.TryGetProperty("rejected_tests", out var rejectedTests) && rejectedTests.GetArrayLength() > 0)
            {
                sb.Append(BuildRejectedTestsSection(rejectedTests));
            }

            // Files created
            if (root.TryGetProperty("files_created", out var files) && files.GetArrayLength() > 0)
            {
                sb.Append(BuildFilesSection(files, workspaceRoot));
            }

            // Footer — show refresh indicator only on in-progress pages.
            // Terminal pages stop polling so we say so explicitly.
            if (isTerminal)
            {
                sb.Append("""
                    <div class="footer">
                        <span class="refresh-indicator">
                            Live updates stopped — run complete. Scroll freely.
                        </span>
                    </div>
                    """);
            }
            else
            {
                sb.Append("""
                    <div class="footer">
                        <span class="refresh-indicator">
                            <span class="refresh-dot"></span>
                            Auto-refreshing every 1.5 seconds
                        </span>
                    </div>
                    """);
            }

            return sb.ToString();
        }
        catch
        {
            return """<div class="status-card"><p>Loading...</p></div>""";
        }
    }

    private static string[] PhasesForCommand(string command)
    {
        return command switch
        {
            "generate" => ProgressPhases.Generate,
            "update" => ProgressPhases.Update,
            "docs-index" => ProgressPhases.DocsIndex,
            "analyze-coverage" => ProgressPhases.Coverage,
            "extract-criteria" => ProgressPhases.ExtractCriteria,
            "dashboard" => ProgressPhases.Dashboard,
            _ => ProgressPhases.Generate
        };
    }

    private static (string[] phases, int currentIdx) ResolvePhases(string status, string command = "")
    {
        return status switch
        {
            // Generate
            "analyzing" => (ProgressPhases.Generate, 0),
            "analyzed" => (ProgressPhases.Generate, 1),
            "generating" => (ProgressPhases.Generate, 2),
            // Docs index
            "scanning" => (ProgressPhases.DocsIndex, 0),
            "indexing" => (ProgressPhases.DocsIndex, 1),
            "extracting-criteria" => (ProgressPhases.DocsIndex, 2),
            // Update
            "classifying" => (ProgressPhases.Update, 0),
            "updating" => (ProgressPhases.Update, 1),
            "verifying" => (ProgressPhases.Update, 2),
            // Coverage
            "scanning-tests" => (ProgressPhases.Coverage, 0),
            "analyzing-docs" => (ProgressPhases.Coverage, 1),
            "analyzing-criteria" => (ProgressPhases.Coverage, 2),
            "analyzing-automation" => (ProgressPhases.Coverage, 3),
            // Extract criteria
            "scanning-docs" => (ProgressPhases.ExtractCriteria, 0),
            "extracting" => (ProgressPhases.ExtractCriteria, 1),
            "building-index" => (ProgressPhases.ExtractCriteria, 2),
            // Dashboard
            "collecting-data" => (ProgressPhases.Dashboard, 0),
            "generating-html" => (ProgressPhases.Dashboard, 1),
            // Terminal states — use command field to determine correct phases
            "completed" or "success" => (PhasesForCommand(command), PhasesForCommand(command).Length - 1),
            "failed" or "partial" => (PhasesForCommand(command), -1),
            _ => (PhasesForCommand(command), -1)
        };
    }

    // Spec 040: renders the Token Usage section from the token_usage block of
    // .spectra-result.json. Called live during long-running runs so totals
    // climb as batches complete, and at the end for the final snapshot.
    private static string BuildTokenUsageSection(JsonElement tokenUsage)
    {
        if (!tokenUsage.TryGetProperty("total", out var total)) return "";
        var calls = total.TryGetProperty("calls", out var c) ? c.GetInt32() : 0;
        if (calls == 0) return "";

        var tokensIn = total.TryGetProperty("tokens_in", out var ti) ? ti.GetInt32() : 0;
        var tokensOut = total.TryGetProperty("tokens_out", out var to) ? to.GetInt32() : 0;
        var totalTokens = total.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
        var elapsed = total.TryGetProperty("elapsed_seconds", out var es) ? es.GetDouble() : 0;
        var costDisplay = tokenUsage.TryGetProperty("cost_display", out var cd) ? cd.GetString() ?? "" : "";

        var sb = new StringBuilder();
        sb.Append("""<div class="summary-grid">""");
        sb.Append($"""<div class="summary-card"><div class="summary-number">{calls}</div><div class="summary-label">AI Calls</div></div>""");
        sb.Append($"""<div class="summary-card"><div class="summary-number">{FormatKTokens(tokensIn)}</div><div class="summary-label">Tokens In</div></div>""");
        sb.Append($"""<div class="summary-card"><div class="summary-number">{FormatKTokens(tokensOut)}</div><div class="summary-label">Tokens Out</div></div>""");
        sb.Append($"""<div class="summary-card"><div class="summary-number">{FormatKTokens(totalTokens)}</div><div class="summary-label">Total Tokens</div></div>""");
        sb.Append($"""<div class="summary-card"><div class="summary-number">{FormatSeconds(elapsed)}</div><div class="summary-label">AI Time</div></div>""");
        sb.Append("</div>");

        if (!string.IsNullOrEmpty(costDisplay))
        {
            sb.Append($"""<p style="text-align:center;margin-top:0.5rem;color:var(--color-muted);font-size:0.9rem;">Estimated cost: {Escape(costDisplay)}</p>""");
        }

        return sb.ToString();
    }

    private static string FormatKTokens(int tokens)
    {
        if (tokens == 0) return "-";
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000.0:F1}M";
        if (tokens >= 1_000) return $"{tokens / 1_000.0:F1}K";
        return tokens.ToString();
    }

    private static string FormatSeconds(double seconds)
    {
        if (seconds <= 0) return "-";
        if (seconds < 60) return $"{seconds:F1}s";
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes}m{ts.Seconds:D2}s";
    }

    private static string BuildStepper(string status, string command = "")
    {
        var (phases, currentIdx) = ResolvePhases(status, command);

        var sb = new StringBuilder();
        sb.Append("""<div class="stepper">""");

        for (var i = 0; i < phases.Length; i++)
        {
            if (i > 0)
            {
                var lineDone = i <= currentIdx ? " done" : "";
                sb.Append($"""<div class="step-line{lineDone}"></div>""");
            }

            var stepClass = i < currentIdx ? "done"
                : i == currentIdx ? "active"
                : status == "failed" && i == 0 ? "error"
                : "";

            var label = phases[i] switch
            {
                "analyzing" => "Analyzing",
                "analyzed" => "Analyzed",
                "generating" => "Generating",
                "scanning" => "Scanning",
                "indexing" => "Indexing",
                "extracting-criteria" => "Extracting Criteria",
                "classifying" => "Classifying",
                "updating" => "Updating",
                "verifying" => "Verifying",
                "scanning-tests" => "Scanning Tests",
                "analyzing-docs" => "Analyzing Docs",
                "analyzing-criteria" => "Analyzing Criteria",
                "analyzing-automation" => "Analyzing Automation",
                "scanning-docs" => "Scanning Docs",
                "extracting" => "Extracting",
                "building-index" => "Building Index",
                "collecting-data" => "Collecting Data",
                "generating-html" => "Generating HTML",
                "completed" => "Completed",
                _ => phases[i]
            };

            sb.Append($"""<div class="step {stepClass}"><div class="step-dot"></div>{label}</div>""");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildStatusCard(string status, string suite, string message, string timestamp)
    {
        var spinnerHtml = status is "completed" or "failed" or "analyzed"
            ? ""
            : """<span class="spinner"></span>""";

        var statusLabel = status switch
        {
            "analyzing" => "Analyzing Documentation",
            "analyzed" => "Analysis Complete",
            "generating" => "Generating Tests",
            "scanning" => "Scanning Documents",
            "indexing" => "Indexing Documents",
            "extracting-criteria" => "Extracting Acceptance Criteria",
            "classifying" => "Classifying Tests",
            "updating" => "Updating Tests",
            "verifying" => "Verifying Tests",
            "scanning-tests" => "Scanning Test Suites",
            "analyzing-docs" => "Analyzing Documentation Coverage",
            "analyzing-criteria" => "Analyzing Acceptance Criteria Coverage",
            "analyzing-automation" => "Analyzing Automation Coverage",
            "scanning-docs" => "Scanning Documentation",
            "extracting" => "Extracting Acceptance Criteria",
            "building-index" => "Building Criteria Index",
            "collecting-data" => "Collecting Dashboard Data",
            "generating-html" => "Generating Dashboard HTML",
            "completed" => "Complete",
            "failed" => "Failed",
            _ => status
        };

        var timeDisplay = "";
        if (DateTime.TryParse(timestamp, out var dt))
        {
            timeDisplay = $"Last updated: {dt:HH:mm:ss}";
        }

        return $$"""
            <div class="status-card">
                <div class="status-badge {{status}}">{{spinnerHtml}} {{Escape(statusLabel)}}</div>
                <div class="suite-name">{{Escape(suite)}}</div>
                <div class="status-message">{{Escape(message)}}</div>
                <div class="status-time">{{Escape(timeDisplay)}}</div>
            </div>
            """;
    }

    private static string BuildAnalysisSection(System.Text.Json.JsonElement analysis)
    {
        var sb = new StringBuilder();
        var total = analysis.TryGetProperty("total_behaviors", out var tb) ? tb.GetInt32() : 0;
        var covered = analysis.TryGetProperty("already_covered", out var ac) ? ac.GetInt32() : 0;
        var recommended = analysis.TryGetProperty("recommended", out var rec) ? rec.GetInt32() : 0;

        // Summary cards for analysis
        sb.Append("""<div class="summary-grid">""");
        sb.Append($"""
            <div class="summary-card"><div class="summary-number">{total}</div><div class="summary-label">Behaviors Found</div></div>
            <div class="summary-card"><div class="summary-number green">{covered}</div><div class="summary-label">Already Covered</div></div>
            <div class="summary-card"><div class="summary-number" style="color:var(--color-primary)">{recommended}</div><div class="summary-label">Recommended</div></div>
            """);
        sb.Append("</div>");

        // Breakdown by category
        if (analysis.TryGetProperty("breakdown", out var breakdown) && breakdown.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            sb.Append("""<div class="breakdown"><h3>Category Breakdown</h3>""");
            foreach (var prop in breakdown.EnumerateObject())
            {
                var label = prop.Name switch
                {
                    "HappyPath" => "Happy Path",
                    "EdgeCase" => "Edge Case",
                    _ => prop.Name
                };
                sb.Append($"""<div class="breakdown-row"><span>{Escape(label)}</span><span class="breakdown-count">{prop.Value.GetInt32()}</span></div>""");
            }
            sb.Append("</div>");
        }

        // Technique breakdown (ISTQB) — rendered beneath category breakdown.
        // Suppressed when the map is empty (e.g. legacy AI responses).
        if (analysis.TryGetProperty("technique_breakdown", out var techBreakdown)
            && techBreakdown.ValueKind == System.Text.Json.JsonValueKind.Object
            && techBreakdown.EnumerateObject().Any())
        {
            sb.Append("""<div class="breakdown"><h3>Technique Breakdown</h3>""");
            // Render in fixed display order BVA, EP, DT, ST, EG, UC; unknowns last.
            string[] order = ["BVA", "EP", "DT", "ST", "EG", "UC"];
            var labels = new Dictionary<string, string>
            {
                ["BVA"] = "Boundary Value Analysis",
                ["EP"] = "Equivalence Partitioning",
                ["DT"] = "Decision Table",
                ["ST"] = "State Transition",
                ["EG"] = "Error Guessing",
                ["UC"] = "Use Case",
            };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in order)
            {
                if (techBreakdown.TryGetProperty(code, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    sb.Append($"""<div class="breakdown-row"><span>{Escape(labels[code])}</span><span class="breakdown-count">{v.GetInt32()}</span></div>""");
                    seen.Add(code);
                }
            }
            foreach (var prop in techBreakdown.EnumerateObject())
            {
                if (seen.Contains(prop.Name)) continue;
                if (prop.Value.ValueKind != System.Text.Json.JsonValueKind.Number) continue;
                sb.Append($"""<div class="breakdown-row"><span>{Escape(prop.Name)}</span><span class="breakdown-count">{prop.Value.GetInt32()}</span></div>""");
            }
            sb.Append("</div>");
        }

        return sb.ToString();
    }

    private static string BuildGenerationCards(int requested, int generated, int written, int rejected)
    {
        var sb = new StringBuilder();
        sb.Append("""<div class="summary-grid">""");

        if (requested > 0)
            sb.Append($"""<div class="summary-card"><div class="summary-number">{requested}</div><div class="summary-label">Requested</div></div>""");

        sb.Append($"""<div class="summary-card"><div class="summary-number">{generated}</div><div class="summary-label">Generated</div></div>""");
        sb.Append($"""<div class="summary-card"><div class="summary-number green">{written}</div><div class="summary-label">Written</div></div>""");

        if (rejected > 0)
            sb.Append($"""<div class="summary-card"><div class="summary-number red">{rejected}</div><div class="summary-label">Rejected</div></div>""");

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildFilesSection(System.Text.Json.JsonElement files, string workspaceRoot)
    {
        var sb = new StringBuilder();
        sb.Append("""<div class="files-section"><h3>Generated Files</h3>""");

        foreach (var file in files.EnumerateArray())
        {
            var relativePath = file.GetString() ?? "";
            var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath)).Replace('\\', '/');
            var vscodeUri = $"vscode://file/{Uri.EscapeDataString(fullPath).Replace("%2F", "/")}";

            sb.Append($"""<a class="file-item file-link" href="{Escape(vscodeUri)}" data-vscode-uri="{Escape(vscodeUri)}" title="Open in VS Code"><span class="file-icon">📄</span>{Escape(relativePath)}</a>""");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// Spec 041: renders live progress bars from the in-flight <c>progress</c>
    /// object in <c>.spectra-result.json</c>. For generate runs, shows two bars
    /// (generation + verification, the second dimmed until generation completes).
    /// For update runs, shows a single "Updating tests" bar.
    /// </summary>
    private static string RenderProgressBars(JsonElement progress)
    {
        var phase = progress.TryGetProperty("phase", out var ph) ? (ph.GetString() ?? "") : "";
        var target = progress.TryGetProperty("testsTarget", out var tt) ? tt.GetInt32() : 0;
        var generated = progress.TryGetProperty("testsGenerated", out var tg) ? tg.GetInt32() : 0;
        var verified = progress.TryGetProperty("testsVerified", out var tv) ? tv.GetInt32() : 0;
        var currentBatch = progress.TryGetProperty("currentBatch", out var cb) ? cb.GetInt32() : 0;
        var totalBatches = progress.TryGetProperty("totalBatches", out var tb) ? tb.GetInt32() : 0;
        var lastTestId = progress.TryGetProperty("lastTestId", out var lti) ? lti.GetString() : null;
        var lastVerdict = progress.TryGetProperty("lastVerdict", out var lv) ? lv.GetString() : null;

        // Spec 044: coverage snapshot fields
        var existingTestCount = progress.TryGetProperty("existingTestCount", out var etc) ? etc.GetInt32() : 0;
        var criteriaCoverage = progress.TryGetProperty("criteriaCoverage", out var cc) ? cc.GetString() : null;
        var analysisMode = progress.TryGetProperty("analysisMode", out var am) ? am.GetString() : null;

        if (target <= 0) return "";

        var sb = new StringBuilder();

        if (string.Equals(phase, "Updating", StringComparison.OrdinalIgnoreCase))
        {
            var pct = ClampPct((double)currentBatch / Math.Max(1, totalBatches) * 100.0);
            var detail = totalBatches > 0
                ? $"Proposal {currentBatch} of {totalBatches}"
                : "";
            sb.Append($"""<div class="progress-section">""");
            sb.Append($"""<div class="progress-label"><span>Updating tests</span><span class="progress-label-count">{currentBatch} / {totalBatches}</span></div>""");
            sb.Append($"""<div class="progress-bar-track"><div class="progress-bar-fill" style="width: {pct:F1}%"></div></div>""");
            if (!string.IsNullOrEmpty(detail))
                sb.Append($"""<div class="progress-detail">{Escape(detail)}</div>""");
            sb.Append("</div>");
            return sb.ToString();
        }

        // Spec 044: Coverage snapshot summary cards
        if (existingTestCount > 0)
        {
            sb.Append("""<div class="summary-cards">""");
            sb.Append($"""<div class="summary-card"><div class="summary-number">{existingTestCount}</div><div class="summary-label">Existing Tests</div></div>""");
            if (!string.IsNullOrEmpty(criteriaCoverage))
                sb.Append($"""<div class="summary-card"><div class="summary-number">{Escape(criteriaCoverage)}</div><div class="summary-label">Criteria Coverage</div></div>""");
            if (!string.IsNullOrEmpty(analysisMode))
                sb.Append($"""<div class="summary-card"><div class="summary-number">{Escape(analysisMode)}</div><div class="summary-label">Analysis Mode</div></div>""");
            sb.Append("</div>");
        }

        // Generate flow: two bars (generation + verification).
        var genPct = ClampPct((double)generated / target * 100.0);
        var genDetail = totalBatches > 0
            ? $"Batch {Math.Max(1, currentBatch)} of {totalBatches}"
            : "";
        sb.Append($"""<div class="progress-section">""");
        sb.Append($"""<div class="progress-label"><span>Generating tests</span><span class="progress-label-count">{generated} / {target}</span></div>""");
        sb.Append($"""<div class="progress-bar-track"><div class="progress-bar-fill" style="width: {genPct:F1}%"></div></div>""");
        if (!string.IsNullOrEmpty(genDetail))
            sb.Append($"""<div class="progress-detail">{Escape(genDetail)}</div>""");
        sb.Append("</div>");

        var isVerifying = string.Equals(phase, "Verifying", StringComparison.OrdinalIgnoreCase);
        var verifyClass = isVerifying ? "progress-section" : "progress-section dimmed";
        var verifyPct = ClampPct((double)verified / target * 100.0);
        var verifyDetail = isVerifying && !string.IsNullOrEmpty(lastTestId)
            ? $"{lastTestId}{(string.IsNullOrEmpty(lastVerdict) ? "" : " — " + lastVerdict)}"
            : "";

        sb.Append($"""<div class="{verifyClass}">""");
        sb.Append($"""<div class="progress-label"><span>Verifying tests</span><span class="progress-label-count">{verified} / {target}</span></div>""");
        sb.Append($"""<div class="progress-bar-track"><div class="progress-bar-fill verifying" style="width: {verifyPct:F1}%"></div></div>""");
        if (!string.IsNullOrEmpty(verifyDetail))
            sb.Append($"""<div class="progress-detail">{Escape(verifyDetail)}</div>""");
        sb.Append("</div>");

        return sb.ToString();
    }

    private static double ClampPct(double pct) => pct < 0 ? 0 : pct > 100 ? 100 : pct;

    private static string BuildVerificationSection(System.Text.Json.JsonElement verification)
    {
        var sb = new StringBuilder();
        sb.Append("""<div class="verification-section"><h3>Critic Verification</h3><div class="verification-list">""");

        foreach (var test in verification.EnumerateArray())
        {
            var id = test.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var title = test.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var verdict = test.TryGetProperty("verdict", out var verdictProp) ? verdictProp.GetString() ?? "" : "";
            var score = test.TryGetProperty("score", out var scoreProp) ? scoreProp.GetDouble() : 0;
            var reason = test.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

            var verdictClass = verdict switch
            {
                "grounded" => "verdict-grounded",
                "partial" => "verdict-partial",
                "hallucinated" => "verdict-hallucinated",
                _ => "verdict-unknown"
            };

            var verdictIcon = verdict switch
            {
                "grounded" => "&#x2713;",     // checkmark
                "partial" => "&#x25CB;",       // circle
                "hallucinated" => "&#x2717;",  // cross
                _ => "?"
            };

            sb.Append($"""<div class="verification-item {verdictClass}">""");
            sb.Append($"""<span class="verdict-icon">{verdictIcon}</span>""");
            sb.Append($"""<span class="verdict-id">{Escape(id)}</span>""");
            sb.Append($"""<span class="verdict-title">{Escape(title)}</span>""");
            sb.Append($"""<span class="verdict-badge">{Escape(verdict)}</span>""");
            if (!string.IsNullOrEmpty(reason))
                sb.Append($"""<div class="verdict-reason">{Escape(reason)}</div>""");
            sb.Append("</div>");
        }

        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string BuildRejectedTestsSection(System.Text.Json.JsonElement rejected)
    {
        var sb = new StringBuilder();
        sb.Append("""<div class="rejected-section"><h3>Rejected by Critic</h3>""");

        foreach (var test in rejected.EnumerateArray())
        {
            var id = test.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var title = test.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
            var verdict = test.TryGetProperty("verdict", out var verdictProp) ? verdictProp.GetString() ?? "" : "";
            var reason = test.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

            sb.Append($"""<div class="rejected-item"><span class="rejected-id">{Escape(id)}</span> <span class="rejected-title">{Escape(title)}</span>""");
            if (!string.IsNullOrEmpty(reason))
                sb.Append($"""<div class="rejected-reason">{Escape(reason)}</div>""");
            sb.Append("</div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Escape(string text) => HttpUtility.HtmlEncode(text);
}
