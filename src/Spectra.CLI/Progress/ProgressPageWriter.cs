using System.Diagnostics;
using System.Text;
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
    /// <param name="isTerminal">True if status is completed/failed — omits auto-refresh.</param>
    public static void WriteProgressPage(string htmlPath, string jsonData, bool isTerminal)
    {
        try
        {
            var workspaceRoot = Path.GetDirectoryName(htmlPath) ?? Directory.GetCurrentDirectory();
            var html = BuildHtml(jsonData, isTerminal, workspaceRoot);
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

    private static string BuildHtml(string jsonData, bool isTerminal, string workspaceRoot)
    {
        var refreshTag = isTerminal ? "" : """<meta http-equiv="refresh" content="2">""";
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
                <title>SPECTRA — Progress</title>
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
                    .status-badge.analyzing, .status-badge.scanning { background: var(--color-primary-light); color: var(--color-primary); }
                    .status-badge.analyzed { background: var(--color-passed-bg); color: var(--color-passed); }
                    .status-badge.generating, .status-badge.indexing, .status-badge.extracting-criteria { background: var(--color-warning-bg); color: var(--color-warning); }
                    .status-badge.completed { background: var(--color-passed-bg); color: var(--color-passed); }
                    .status-badge.failed { background: var(--color-failed-bg); color: var(--color-failed); }
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
                </style>
            </head>
            <body>
                <nav class="nav">
                    <span class="nav-logo">SPECTRA</span>
                    <span class="nav-title">Progress</span>
                </nav>
                <div class="container" data-workspace="{{escapedRoot}}">
                    {{BuildBody(jsonData, isTerminal, workspaceRoot)}}
                </div>
                <script>
                    // VS Code Live Preview auto-reloads when the file changes on disk.
                    // No custom polling needed — the CLI rewrites this HTML on every progress update.
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
            var suite = root.TryGetProperty("suite", out var su) ? su.GetString() ?? "" : "";
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            var error = root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            var timestamp = root.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "";

            var sb = new StringBuilder();

            // Phase stepper
            sb.Append(BuildStepper(status));

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

            // Files created
            if (root.TryGetProperty("files_created", out var files) && files.GetArrayLength() > 0)
            {
                sb.Append(BuildFilesSection(files, workspaceRoot));
            }

            // Footer
            if (!isTerminal)
            {
                sb.Append("""
                    <div class="footer">
                        <span class="refresh-indicator">
                            <span class="refresh-dot"></span>
                            Auto-refreshing every 2 seconds
                        </span>
                    </div>
                    """);
            }
            else
            {
                sb.Append("""<div class="footer">Generated by SPECTRA</div>""");
            }

            return sb.ToString();
        }
        catch
        {
            return """<div class="status-card"><p>Loading...</p></div>""";
        }
    }

    private static bool IsDocsIndexCommand(string status) =>
        status is "scanning" or "indexing" or "extracting-criteria";

    private static string BuildStepper(string status)
    {
        // Determine which phase set to use based on status
        var isDocsIndex = IsDocsIndexCommand(status);
        var phases = isDocsIndex
            ? new[] { "scanning", "indexing", "extracting-criteria", "completed" }
            : new[] { "analyzing", "analyzed", "generating", "completed" };

        var currentIdx = status switch
        {
            // Generate phases
            "analyzing" => 0,
            "analyzed" => 1,
            "generating" => 2,
            // Docs index phases
            "scanning" => 0,
            "indexing" => 1,
            "extracting-criteria" => 2,
            // Shared
            "completed" => 3,
            "failed" => -1,
            _ => -1
        };

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
        var spinnerHtml = status is "analyzing" or "generating" or "scanning" or "indexing" or "extracting-criteria"
            ? """<span class="spinner"></span>"""
            : "";

        var statusLabel = status switch
        {
            "analyzing" => "Analyzing Documentation",
            "analyzed" => "Analysis Complete",
            "generating" => "Generating Tests",
            "scanning" => "Scanning Documents",
            "indexing" => "Indexing Documents",
            "extracting-criteria" => "Extracting Acceptance Criteria",
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
            var vscodeUri = $"vscode://file/{fullPath}";

            sb.Append($"""<a class="file-item file-link" href="{Escape(vscodeUri)}" title="Open in VS Code"><span class="file-icon">📄</span>{Escape(relativePath)}</a>""");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Escape(string text) => HttpUtility.HtmlEncode(text);
}
