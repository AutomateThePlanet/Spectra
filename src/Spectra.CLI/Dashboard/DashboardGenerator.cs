using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Dashboard;

namespace Spectra.CLI.Dashboard;

/// <summary>
/// Generates static dashboard HTML from collected data.
/// </summary>
public sealed class DashboardGenerator
{
    private readonly string _templatePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public DashboardGenerator(string? templatePath = null)
    {
        _templatePath = templatePath ?? GetDefaultTemplatePath();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Generates the dashboard to the specified output directory.
    /// </summary>
    public async Task GenerateAsync(DashboardData data, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        // Ensure output directory exists
        Directory.CreateDirectory(outputPath);

        // Generate HTML with embedded JSON data
        var html = await GenerateHtmlAsync(data);
        var indexPath = Path.Combine(outputPath, "index.html");
        await File.WriteAllTextAsync(indexPath, html);

        // Copy static assets
        await CopyStaticAssetsAsync(outputPath);
    }

    /// <summary>
    /// Generates the main HTML file with embedded dashboard data.
    /// </summary>
    private async Task<string> GenerateHtmlAsync(DashboardData data)
    {
        var template = await LoadTemplateAsync();
        var jsonData = JsonSerializer.Serialize(data, _jsonOptions);

        // Replace placeholder with actual data
        return template.Replace("{{DASHBOARD_DATA}}", jsonData);
    }

    /// <summary>
    /// Loads the HTML template.
    /// </summary>
    private async Task<string> LoadTemplateAsync()
    {
        if (File.Exists(_templatePath))
        {
            return await File.ReadAllTextAsync(_templatePath);
        }

        // Return default template if file doesn't exist
        return GetDefaultTemplate();
    }

    /// <summary>
    /// Copies static assets (CSS, JS) to output directory.
    /// </summary>
    private async Task CopyStaticAssetsAsync(string outputPath)
    {
        var templateDir = Path.GetDirectoryName(_templatePath);
        if (string.IsNullOrEmpty(templateDir) || !Directory.Exists(templateDir))
        {
            // Create default assets
            await CreateDefaultAssetsAsync(outputPath);
            return;
        }

        // Copy styles directory
        var stylesSource = Path.Combine(templateDir, "styles");
        var stylesDest = Path.Combine(outputPath, "styles");
        if (Directory.Exists(stylesSource))
        {
            CopyDirectory(stylesSource, stylesDest);
        }
        else
        {
            Directory.CreateDirectory(stylesDest);
            await File.WriteAllTextAsync(
                Path.Combine(stylesDest, "main.css"),
                GetDefaultCss());
        }

        // Copy scripts directory
        var scriptsSource = Path.Combine(templateDir, "scripts");
        var scriptsDest = Path.Combine(outputPath, "scripts");
        if (Directory.Exists(scriptsSource))
        {
            CopyDirectory(scriptsSource, scriptsDest);
        }
        else
        {
            Directory.CreateDirectory(scriptsDest);
            await File.WriteAllTextAsync(
                Path.Combine(scriptsDest, "app.js"),
                GetDefaultJs());
        }
    }

    /// <summary>
    /// Creates default CSS and JS assets.
    /// </summary>
    private async Task CreateDefaultAssetsAsync(string outputPath)
    {
        var stylesPath = Path.Combine(outputPath, "styles");
        var scriptsPath = Path.Combine(outputPath, "scripts");

        Directory.CreateDirectory(stylesPath);
        Directory.CreateDirectory(scriptsPath);

        await File.WriteAllTextAsync(
            Path.Combine(stylesPath, "main.css"),
            GetDefaultCss());

        await File.WriteAllTextAsync(
            Path.Combine(scriptsPath, "app.js"),
            GetDefaultJs());
    }

    /// <summary>
    /// Recursively copies a directory.
    /// </summary>
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    /// <summary>
    /// Gets the default template path from dashboard-site directory.
    /// </summary>
    private static string GetDefaultTemplatePath()
    {
        // Look for dashboard-site in current directory or parent directories
        var current = Environment.CurrentDirectory;
        while (current is not null)
        {
            var templatePath = Path.Combine(current, "dashboard-site", "index.html");
            if (File.Exists(templatePath))
            {
                return templatePath;
            }
            current = Path.GetDirectoryName(current);
        }

        return Path.Combine(Environment.CurrentDirectory, "dashboard-site", "index.html");
    }

    /// <summary>
    /// Returns the default HTML template.
    /// </summary>
    private static string GetDefaultTemplate() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>SPECTRA Dashboard</title>
            <link rel="stylesheet" href="styles/main.css">
        </head>
        <body>
            <div id="app">
                <header class="header">
                    <h1>SPECTRA Dashboard</h1>
                    <nav class="nav">
                        <button class="nav-btn active" data-view="suites">Suites</button>
                        <button class="nav-btn" data-view="tests">Tests</button>
                        <button class="nav-btn" data-view="runs">Run History</button>
                        <button class="nav-btn" data-view="coverage">Coverage</button>
                    </nav>
                </header>
                <main class="main">
                    <aside class="sidebar">
                        <div class="filters">
                            <h3>Filters</h3>
                            <div class="filter-group">
                                <label>Priority</label>
                                <select id="filter-priority">
                                    <option value="">All</option>
                                    <option value="high">High</option>
                                    <option value="medium">Medium</option>
                                    <option value="low">Low</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label>Component</label>
                                <select id="filter-component">
                                    <option value="">All</option>
                                </select>
                            </div>
                            <div class="filter-group">
                                <label>Search</label>
                                <input type="text" id="filter-search" placeholder="Search by ID or title...">
                            </div>
                            <div class="filter-group">
                                <label>Tags</label>
                                <div id="filter-tags" class="tag-filter"></div>
                            </div>
                        </div>
                    </aside>
                    <section class="content" id="content">
                        <!-- Dynamic content loaded by JavaScript -->
                    </section>
                </main>
            </div>

            <script id="dashboard-data" type="application/json">
            {{DASHBOARD_DATA}}
            </script>

            <script src="scripts/app.js"></script>
        </body>
        </html>
        """;

    /// <summary>
    /// Returns the default CSS styles.
    /// </summary>
    private static string GetDefaultCss() => """
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #f5f5f5;
            color: #333;
            line-height: 1.6;
        }

        #app {
            display: flex;
            flex-direction: column;
            min-height: 100vh;
        }

        .header {
            background: #1a1a2e;
            color: white;
            padding: 1rem 2rem;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .header h1 {
            font-size: 1.5rem;
            font-weight: 600;
        }

        .nav {
            display: flex;
            gap: 0.5rem;
        }

        .nav-btn {
            background: transparent;
            border: 1px solid rgba(255,255,255,0.3);
            color: white;
            padding: 0.5rem 1rem;
            border-radius: 4px;
            cursor: pointer;
            transition: all 0.2s;
        }

        .nav-btn:hover, .nav-btn.active {
            background: rgba(255,255,255,0.1);
            border-color: white;
        }

        .main {
            display: flex;
            flex: 1;
        }

        .sidebar {
            width: 280px;
            background: white;
            border-right: 1px solid #e0e0e0;
            padding: 1.5rem;
        }

        .filters h3 {
            margin-bottom: 1rem;
            font-size: 0.9rem;
            text-transform: uppercase;
            color: #666;
        }

        .filter-group {
            margin-bottom: 1rem;
        }

        .filter-group label {
            display: block;
            font-size: 0.85rem;
            color: #666;
            margin-bottom: 0.25rem;
        }

        .filter-group select, .filter-group input {
            width: 100%;
            padding: 0.5rem;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 0.9rem;
        }

        .tag-filter {
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            max-height: 150px;
            overflow-y: auto;
            padding: 0.5rem;
            background: white;
            border: 1px solid #ddd;
            border-radius: 4px;
        }

        .tag-filter-item {
            display: flex;
            align-items: center;
            gap: 0.25rem;
        }

        .tag-filter-item input[type="checkbox"] {
            width: auto;
            margin: 0;
        }

        .tag-filter-item label {
            font-size: 0.8rem;
            cursor: pointer;
            margin: 0;
        }

        .content {
            flex: 1;
            padding: 2rem;
            overflow-y: auto;
        }

        .card-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 1.5rem;
        }

        .card {
            background: white;
            border-radius: 8px;
            padding: 1.5rem;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .card:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.15);
        }

        .card-title {
            font-size: 1.1rem;
            font-weight: 600;
            margin-bottom: 0.5rem;
        }

        .card-meta {
            display: flex;
            gap: 0.5rem;
            flex-wrap: wrap;
            margin-bottom: 0.5rem;
        }

        .badge {
            font-size: 0.75rem;
            padding: 0.2rem 0.5rem;
            border-radius: 4px;
            background: #e0e0e0;
        }

        .badge.high { background: #ffebee; color: #c62828; }
        .badge.medium { background: #fff8e1; color: #f57f17; }
        .badge.low { background: #e8f5e9; color: #2e7d32; }

        .stat {
            display: flex;
            justify-content: space-between;
            padding: 0.5rem 0;
            border-top: 1px solid #eee;
        }

        .stat-label { color: #666; }
        .stat-value { font-weight: 600; }

        .run-row {
            background: white;
            padding: 1rem 1.5rem;
            margin-bottom: 0.5rem;
            border-radius: 8px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .run-info { display: flex; flex-direction: column; gap: 0.25rem; }
        .run-suite { font-weight: 600; }
        .run-date { font-size: 0.85rem; color: #666; }

        .run-stats { display: flex; gap: 1rem; }
        .run-stat { text-align: center; }
        .run-stat-value { font-size: 1.25rem; font-weight: 600; }
        .run-stat-label { font-size: 0.75rem; color: #666; }
        .run-stat.passed .run-stat-value { color: #2e7d32; }
        .run-stat.failed .run-stat-value { color: #c62828; }

        .empty-state {
            text-align: center;
            padding: 3rem;
            color: #666;
        }

        .empty-state h2 { margin-bottom: 0.5rem; }
        """;

    /// <summary>
    /// Returns the default JavaScript.
    /// </summary>
    private static string GetDefaultJs() => """
        // Load dashboard data
        const data = JSON.parse(document.getElementById('dashboard-data').textContent);
        const allTests = [...data.tests];
        let currentView = 'suites';
        let filters = { priority: '', component: '', search: '', tags: [] };

        // Initialize
        document.addEventListener('DOMContentLoaded', () => {
            setupNavigation();
            setupFilters();
            populateComponentFilter();
            populateTagFilter();
            render();
        });

        function setupNavigation() {
            document.querySelectorAll('.nav-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
                    btn.classList.add('active');
                    currentView = btn.dataset.view;
                    render();
                });
            });
        }

        function setupFilters() {
            document.getElementById('filter-priority').addEventListener('change', (e) => {
                filters.priority = e.target.value;
                render();
            });
            document.getElementById('filter-component').addEventListener('change', (e) => {
                filters.component = e.target.value;
                render();
            });
            document.getElementById('filter-search').addEventListener('input', (e) => {
                filters.search = e.target.value.toLowerCase();
                render();
            });
        }

        function populateComponentFilter() {
            const components = new Set();
            allTests.forEach(t => t.component && components.add(t.component));
            const select = document.getElementById('filter-component');
            [...components].sort().forEach(c => {
                const option = document.createElement('option');
                option.value = c;
                option.textContent = c;
                select.appendChild(option);
            });
        }

        function populateTagFilter() {
            const tags = new Set();
            allTests.forEach(t => (t.tags || []).forEach(tag => tags.add(tag)));
            const container = document.getElementById('filter-tags');
            if (!container || tags.size === 0) return;
            container.innerHTML = [...tags].sort().map(tag => `
                <div class="tag-filter-item">
                    <input type="checkbox" id="tag-${tag}" value="${tag}" onchange="handleTagFilter(this)">
                    <label for="tag-${tag}">${tag}</label>
                </div>
            `).join('');
        }

        function handleTagFilter(cb) {
            if (cb.checked) { if (!filters.tags.includes(cb.value)) filters.tags.push(cb.value); }
            else { filters.tags = filters.tags.filter(t => t !== cb.value); }
            render();
        }

        function render() {
            const content = document.getElementById('content');
            switch (currentView) {
                case 'suites': content.innerHTML = renderSuites(); break;
                case 'tests': content.innerHTML = renderTests(); break;
                case 'runs': content.innerHTML = renderRuns(); break;
                case 'coverage': content.innerHTML = renderCoverage(); break;
            }
        }

        function renderSuites() {
            if (!data.suites.length) {
                return '<div class="empty-state"><h2>No test suites found</h2><p>Run spectra index to generate suite indexes.</p></div>';
            }
            return '<div class="card-grid">' + data.suites.map(s => `
                <div class="card" onclick="showSuiteTests('${s.name}')">
                    <div class="card-title">${s.name}</div>
                    <div class="card-meta">
                        ${Object.entries(s.by_priority || {}).map(([k,v]) =>
                            `<span class="badge ${k}">${k}: ${v}</span>`
                        ).join('')}
                    </div>
                    <div class="stat">
                        <span class="stat-label">Total Tests</span>
                        <span class="stat-value">${s.test_count}</span>
                    </div>
                    <div class="stat">
                        <span class="stat-label">Automation</span>
                        <span class="stat-value">${s.automation_coverage.toFixed(1)}%</span>
                    </div>
                </div>
            `).join('') + '</div>';
        }

        function renderTests() {
            let tests = allTests.filter(t => {
                if (filters.priority && t.priority !== filters.priority) return false;
                if (filters.component && t.component !== filters.component) return false;
                if (filters.search && !t.id.toLowerCase().includes(filters.search) &&
                    !t.title.toLowerCase().includes(filters.search)) return false;
                if (filters.tags.length > 0 && !filters.tags.every(tag => (t.tags || []).includes(tag))) return false;
                return true;
            });
            if (!tests.length) {
                return '<div class="empty-state"><h2>No tests found</h2><p>Try adjusting your filters.</p></div>';
            }
            return '<div class="card-grid">' + tests.map(t => `
                <div class="card" onclick="showTestDetail('${t.id}')">
                    <div class="card-title">${t.id}: ${t.title}</div>
                    <div class="card-meta">
                        <span class="badge ${t.priority}">${t.priority}</span>
                        ${t.component ? `<span class="badge">${t.component}</span>` : ''}
                        ${t.has_automation ? '<span class="badge">Automated</span>' : ''}
                    </div>
                    <div style="font-size:0.85rem;color:#666;">Suite: ${t.suite}</div>
                </div>
            `).join('') + '</div>';
        }

        function renderRuns() {
            if (!data.runs.length) {
                return '<div class="empty-state"><h2>No execution runs</h2><p>Execute tests to see run history.</p></div>';
            }
            return data.runs.map(r => `
                <div class="run-row">
                    <div class="run-info">
                        <span class="run-suite">${r.suite}</span>
                        <span class="run-date">${new Date(r.started_at).toLocaleString()} by ${r.started_by}</span>
                    </div>
                    <div class="run-stats">
                        <div class="run-stat passed"><div class="run-stat-value">${r.passed}</div><div class="run-stat-label">Passed</div></div>
                        <div class="run-stat failed"><div class="run-stat-value">${r.failed}</div><div class="run-stat-label">Failed</div></div>
                        <div class="run-stat"><div class="run-stat-value">${r.skipped}</div><div class="run-stat-label">Skipped</div></div>
                        <div class="run-stat"><div class="run-stat-value">${r.total}</div><div class="run-stat-label">Total</div></div>
                    </div>
                </div>
            `).join('');
        }

        function renderCoverage() {
            if (!data.coverage || !data.coverage.nodes) {
                return '<div class="empty-state"><h2>No coverage data</h2><p>Run coverage analysis to visualize relationships.</p></div>';
            }
            return '<div class="empty-state"><h2>Coverage Visualization</h2><p>D3.js visualization will be loaded here.</p></div>';
        }

        function showSuiteTests(suite) {
            filters = { priority: '', component: '', search: '', tags: [] };
            document.getElementById('filter-priority').value = '';
            document.getElementById('filter-component').value = '';
            document.getElementById('filter-search').value = '';
            document.querySelectorAll('#filter-tags input').forEach(cb => cb.checked = false);
            currentView = 'tests';
            document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
            document.querySelector('[data-view="tests"]').classList.add('active');
            // Filter to this suite only
            data.tests = allTests.filter(t => t.suite === suite);
            render();
            // Reset filter
            data.tests = allTests;
        }

        function showTestDetail(id) {
            const test = allTests.find(t => t.id === id);
            if (!test) return;
            const content = document.getElementById('content');
            content.innerHTML = `
                <div class="card" style="max-width:800px;">
                    <button onclick="render()" style="margin-bottom:1rem;">← Back</button>
                    <h2 style="margin-bottom:1rem;">${test.id}: ${test.title}</h2>
                    <div class="card-meta" style="margin-bottom:1rem;">
                        <span class="badge ${test.priority}">${test.priority}</span>
                        ${test.component ? `<span class="badge">${test.component}</span>` : ''}
                        ${test.tags.map(t => `<span class="badge">${t}</span>`).join('')}
                    </div>
                    <div class="stat"><span class="stat-label">Suite</span><span class="stat-value">${test.suite}</span></div>
                    <div class="stat"><span class="stat-label">File</span><span class="stat-value">${test.file}</span></div>
                    ${test.automated_by ? `<div class="stat"><span class="stat-label">Automated By</span><span class="stat-value">${test.automated_by}</span></div>` : ''}
                    ${test.source_refs.length ? `<div class="stat"><span class="stat-label">Source Refs</span><span class="stat-value">${test.source_refs.join(', ')}</span></div>` : ''}
                </div>
            `;
        }
        """;
}
