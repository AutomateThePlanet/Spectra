/**
 * SPECTRA Dashboard Application
 * Client-side rendering for test suite dashboard
 */

// Load dashboard data from embedded JSON
const data = JSON.parse(document.getElementById('dashboard-data').textContent);

// Application state
let currentView = 'suites';
let filters = {
    priority: '',
    component: '',
    search: ''
};

// Cache original tests for filtering
const allTests = [...data.tests];

/**
 * Initialize the dashboard
 */
document.addEventListener('DOMContentLoaded', () => {
    setupNavigation();
    setupFilters();
    populateComponentFilter();
    updateSummary();
    updateGeneratedAt();
    render();
});

/**
 * Set up navigation button handlers
 */
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

/**
 * Set up filter change handlers
 */
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

/**
 * Populate component filter dropdown
 */
function populateComponentFilter() {
    const components = new Set();
    allTests.forEach(t => {
        if (t.component) {
            components.add(t.component);
        }
    });

    const select = document.getElementById('filter-component');
    [...components].sort().forEach(c => {
        const option = document.createElement('option');
        option.value = c;
        option.textContent = c;
        select.appendChild(option);
    });
}

/**
 * Update summary statistics in sidebar
 */
function updateSummary() {
    const summary = document.getElementById('summary');
    if (!summary) return;

    const totalTests = allTests.length;
    const totalSuites = data.suites.length;
    const totalRuns = data.runs.length;
    const automatedTests = allTests.filter(t => t.has_automation).length;
    const automationRate = totalTests > 0 ? ((automatedTests / totalTests) * 100).toFixed(1) : 0;

    summary.innerHTML = `
        <h3>Summary</h3>
        <div class="summary-stat">
            <span class="summary-stat-label">Test Suites</span>
            <span class="summary-stat-value">${totalSuites}</span>
        </div>
        <div class="summary-stat">
            <span class="summary-stat-label">Total Tests</span>
            <span class="summary-stat-value">${totalTests}</span>
        </div>
        <div class="summary-stat">
            <span class="summary-stat-label">Execution Runs</span>
            <span class="summary-stat-value">${totalRuns}</span>
        </div>
        <div class="summary-stat">
            <span class="summary-stat-label">Automation</span>
            <span class="summary-stat-value">${automationRate}%</span>
        </div>
    `;
}

/**
 * Update generated timestamp in footer
 */
function updateGeneratedAt() {
    const el = document.getElementById('generated-at');
    if (el && data.generated_at) {
        const date = new Date(data.generated_at);
        el.textContent = `Generated: ${date.toLocaleString()}`;
    }
}

/**
 * Main render function - routes to view-specific renderers
 */
function render() {
    const content = document.getElementById('content');
    switch (currentView) {
        case 'suites':
            content.innerHTML = renderSuites();
            break;
        case 'tests':
            content.innerHTML = renderTests();
            break;
        case 'runs':
            content.innerHTML = renderRuns();
            break;
        case 'coverage':
            content.innerHTML = renderCoverage();
            break;
    }
}

/**
 * Render suite cards
 */
function renderSuites() {
    if (!data.suites.length) {
        return `
            <div class="empty-state">
                <h2>No test suites found</h2>
                <p>Run <code>spectra index</code> to generate suite indexes.</p>
            </div>
        `;
    }

    return `
        <div class="card-grid">
            ${data.suites.map(s => `
                <div class="card" onclick="showSuiteTests('${escapeHtml(s.name)}')">
                    <div class="card-title">${escapeHtml(s.name)}</div>
                    <div class="card-meta">
                        ${Object.entries(s.by_priority || {}).map(([k, v]) =>
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
                    <div class="progress-bar">
                        <div class="progress-bar-fill" style="width: ${s.automation_coverage}%"></div>
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

/**
 * Render test cards with filtering
 */
function renderTests() {
    let tests = allTests.filter(t => {
        if (filters.priority && t.priority !== filters.priority) return false;
        if (filters.component && t.component !== filters.component) return false;
        if (filters.search) {
            const searchLower = filters.search.toLowerCase();
            if (!t.id.toLowerCase().includes(searchLower) &&
                !t.title.toLowerCase().includes(searchLower)) {
                return false;
            }
        }
        return true;
    });

    if (!tests.length) {
        return `
            <div class="empty-state">
                <h2>No tests found</h2>
                <p>Try adjusting your filters.</p>
            </div>
        `;
    }

    return `
        <div class="card-grid">
            ${tests.map(t => `
                <div class="card" onclick="showTestDetail('${escapeHtml(t.id)}')">
                    <div class="card-title">${escapeHtml(t.id)}: ${escapeHtml(t.title)}</div>
                    <div class="card-meta">
                        <span class="badge ${t.priority}">${t.priority}</span>
                        ${t.component ? `<span class="badge">${escapeHtml(t.component)}</span>` : ''}
                        ${t.has_automation ? '<span class="badge automated">Automated</span>' : ''}
                    </div>
                    <div style="font-size: 0.85rem; color: var(--text-muted);">
                        Suite: ${escapeHtml(t.suite)}
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

/**
 * Render execution run history
 */
function renderRuns() {
    if (!data.runs.length) {
        return `
            <div class="empty-state">
                <h2>No execution runs</h2>
                <p>Execute tests to see run history.</p>
            </div>
        `;
    }

    return `
        <div class="run-list">
            ${data.runs.map(r => `
                <div class="run-row">
                    <div class="run-info">
                        <span class="run-suite">${escapeHtml(r.suite)}</span>
                        <span class="run-date">
                            ${new Date(r.started_at).toLocaleString()} by ${escapeHtml(r.started_by)}
                        </span>
                    </div>
                    <div class="run-stats">
                        <div class="run-stat passed">
                            <div class="run-stat-value">${r.passed}</div>
                            <div class="run-stat-label">Passed</div>
                        </div>
                        <div class="run-stat failed">
                            <div class="run-stat-value">${r.failed}</div>
                            <div class="run-stat-label">Failed</div>
                        </div>
                        <div class="run-stat">
                            <div class="run-stat-value">${r.skipped}</div>
                            <div class="run-stat-label">Skipped</div>
                        </div>
                        <div class="run-stat">
                            <div class="run-stat-value">${r.total}</div>
                            <div class="run-stat-label">Total</div>
                        </div>
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

/**
 * Render coverage visualization placeholder
 */
function renderCoverage() {
    if (!data.coverage || !data.coverage.nodes) {
        return `
            <div class="empty-state">
                <h2>No coverage data</h2>
                <p>Run <code>spectra ai analyze --coverage</code> to generate coverage analysis.</p>
            </div>
        `;
    }

    // Placeholder for D3.js visualization (Phase 8)
    return `
        <div class="empty-state">
            <h2>Coverage Visualization</h2>
            <p>D3.js visualization will be loaded here.</p>
        </div>
    `;
}

/**
 * Navigate to tests view filtered by suite
 */
function showSuiteTests(suite) {
    // Reset filters
    filters = { priority: '', component: '', search: '' };
    document.getElementById('filter-priority').value = '';
    document.getElementById('filter-component').value = '';
    document.getElementById('filter-search').value = '';

    // Switch to tests view
    currentView = 'tests';
    document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
    document.querySelector('[data-view="tests"]').classList.add('active');

    // Filter tests to show only this suite
    data.tests = allTests.filter(t => t.suite === suite);
    render();

    // Reset to all tests after rendering
    data.tests = allTests;
}

/**
 * Show detailed test view
 */
function showTestDetail(id) {
    const test = allTests.find(t => t.id === id);
    if (!test) return;

    const content = document.getElementById('content');
    content.innerHTML = `
        <div class="card test-detail">
            <button class="back-btn" onclick="render()">← Back to Tests</button>
            <h2>${escapeHtml(test.id)}: ${escapeHtml(test.title)}</h2>
            <div class="card-meta" style="margin-bottom: 1.5rem;">
                <span class="badge ${test.priority}">${test.priority}</span>
                ${test.component ? `<span class="badge">${escapeHtml(test.component)}</span>` : ''}
                ${(test.tags || []).map(t => `<span class="badge">${escapeHtml(t)}</span>`).join('')}
            </div>
            <div class="metadata">
                <div class="stat">
                    <span class="stat-label">Suite</span>
                    <span class="stat-value">${escapeHtml(test.suite)}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">File</span>
                    <span class="stat-value">${escapeHtml(test.file)}</span>
                </div>
                ${test.automated_by ? `
                    <div class="stat">
                        <span class="stat-label">Automated By</span>
                        <span class="stat-value">${escapeHtml(test.automated_by)}</span>
                    </div>
                ` : ''}
                ${(test.source_refs || []).length ? `
                    <div class="stat">
                        <span class="stat-label">Source Refs</span>
                        <span class="stat-value">${test.source_refs.map(escapeHtml).join(', ')}</span>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(str) {
    if (typeof str !== 'string') return str;
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}
