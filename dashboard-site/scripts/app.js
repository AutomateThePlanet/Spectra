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
    search: '',
    tags: [] // Multi-select with AND logic
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
    populateTagFilter();
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
 * Populate tag filter with checkboxes (multi-select, AND logic)
 */
function populateTagFilter() {
    const tags = new Set();
    allTests.forEach(t => {
        (t.tags || []).forEach(tag => tags.add(tag));
    });

    const container = document.getElementById('filter-tags');
    if (!container) return;

    if (tags.size === 0) {
        container.innerHTML = '<span class="tag-filter-empty">No tags available</span>';
        return;
    }

    container.innerHTML = [...tags].sort().map(tag => `
        <div class="tag-filter-item">
            <input type="checkbox" id="tag-${escapeHtml(tag)}" value="${escapeHtml(tag)}" onchange="handleTagFilter(this)">
            <label for="tag-${escapeHtml(tag)}">${escapeHtml(tag)}</label>
        </div>
    `).join('');
}

/**
 * Handle tag checkbox change (AND logic - all selected tags must be present)
 */
function handleTagFilter(checkbox) {
    if (checkbox.checked) {
        if (!filters.tags.includes(checkbox.value)) {
            filters.tags.push(checkbox.value);
        }
    } else {
        filters.tags = filters.tags.filter(t => t !== checkbox.value);
    }
    render();
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
            // Initialize D3 visualization after DOM is updated
            if (typeof window.initCoverageMap === 'function') {
                setTimeout(() => window.initCoverageMap(), 0);
            }
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
        // Tag filtering with AND logic - all selected tags must be present
        if (filters.tags.length > 0) {
            const testTags = t.tags || [];
            if (!filters.tags.every(tag => testTags.includes(tag))) {
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
        <div class="test-list">
            ${tests.map(t => `
                <div class="test-row" onclick="showTestDetail('${escapeHtml(t.id)}')">
                    <div class="test-row-main">
                        <span class="test-row-id">${escapeHtml(t.id)}</span>
                        <span class="test-row-title">${escapeHtml(t.title)}</span>
                    </div>
                    <div class="test-row-meta">
                        <span class="badge ${t.priority}">${t.priority}</span>
                        ${t.component ? `<span class="badge">${escapeHtml(t.component)}</span>` : ''}
                        ${t.has_automation ? '<span class="badge automated">Automated</span>' : ''}
                        <span class="test-row-suite">${escapeHtml(t.suite)}</span>
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

/**
 * Render execution run history with table and trend chart
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

    const trendChart = renderTrendChart();
    const trendSummary = renderTrendSummary();

    return `
        ${trendChart}
        ${trendSummary}
        <div class="section-header">
            <h3 class="section-title">Run History</h3>
            <span class="section-count">${data.runs.length} runs</span>
        </div>
        <div class="run-table-container">
            <table class="run-table">
                <thead>
                    <tr>
                        <th>Suite</th>
                        <th>Status</th>
                        <th>Date</th>
                        <th>Duration</th>
                        <th>Pass Rate</th>
                        <th class="num-col">Passed</th>
                        <th class="num-col">Failed</th>
                        <th class="num-col">Skipped</th>
                        <th class="num-col">Blocked</th>
                        <th class="num-col">Total</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.runs.map(r => {
                        const passRate = r.total > 0 ? ((r.passed / r.total) * 100).toFixed(1) : 0;
                        const duration = r.duration_seconds ? formatDuration(r.duration_seconds) : '-';
                        const dateStr = new Date(r.started_at).toLocaleString();
                        const statusClass = r.status === 'completed' ? 'completed' :
                                           r.status === 'failed' ? 'failed' : 'other';
                        return `
                        <tr class="run-table-row" onclick="showRunDetail('${escapeHtml(r.run_id)}')">
                            <td class="suite-cell">${escapeHtml(r.suite)}</td>
                            <td><span class="status-badge ${statusClass}">${escapeHtml(r.status)}</span></td>
                            <td class="date-cell">${dateStr}</td>
                            <td>${duration}</td>
                            <td>
                                <div class="pass-rate-cell">
                                    <div class="mini-progress">
                                        <div class="mini-progress-bar" style="width: ${passRate}%"></div>
                                    </div>
                                    <span>${passRate}%</span>
                                </div>
                            </td>
                            <td class="num-col passed-cell">${r.passed}</td>
                            <td class="num-col failed-cell">${r.failed}</td>
                            <td class="num-col">${r.skipped}</td>
                            <td class="num-col">${r.blocked}</td>
                            <td class="num-col">${r.total}</td>
                        </tr>
                    `}).join('')}
                </tbody>
            </table>
        </div>
    `;
}

/**
 * Render trend chart (simple SVG bar chart)
 */
function renderTrendChart() {
    const trends = data.trends;
    if (!trends || !trends.points || trends.points.length === 0) {
        return '';
    }

    const points = trends.points.slice(-14); // Last 14 days
    const maxRate = 100;
    const chartHeight = 120;
    const chartWidth = 100;
    const barWidth = chartWidth / points.length;

    const bars = points.map((p, i) => {
        const height = (p.pass_rate / maxRate) * chartHeight;
        const x = i * barWidth;
        const y = chartHeight - height;
        const color = p.pass_rate >= 80 ? 'var(--color-success)' :
                      p.pass_rate >= 50 ? 'var(--color-warning)' : 'var(--color-danger)';
        const dateStr = new Date(p.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        return `
            <g class="trend-bar" data-date="${dateStr}" data-rate="${p.pass_rate}%">
                <rect x="${x}%" y="${y}" width="${barWidth - 1}%" height="${height}"
                      fill="${color}" rx="2"/>
            </g>
        `;
    }).join('');

    return `
        <div class="trend-chart-container">
            <h3 class="section-title">Pass Rate Trend (Last 14 Days)</h3>
            <div class="trend-chart">
                <svg viewBox="0 0 100 ${chartHeight}" preserveAspectRatio="none" class="trend-svg">
                    ${bars}
                </svg>
                <div class="trend-chart-labels">
                    ${points.length > 0 ? `
                        <span>${new Date(points[0].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</span>
                        <span>${new Date(points[points.length - 1].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</span>
                    ` : ''}
                </div>
            </div>
        </div>
    `;
}

/**
 * Render trend summary with direction indicator
 */
function renderTrendSummary() {
    const trends = data.trends;
    if (!trends) return '';

    const directionIcon = trends.direction === 'improving' ? '↑' :
                          trends.direction === 'declining' ? '↓' : '→';
    const directionClass = trends.direction === 'improving' ? 'trend-up' :
                           trends.direction === 'declining' ? 'trend-down' : 'trend-stable';

    return `
        <div class="trend-summary">
            <div class="trend-overall">
                <span class="trend-label">Overall Pass Rate</span>
                <span class="trend-value">${trends.overall_pass_rate}%</span>
                <span class="trend-direction ${directionClass}">${directionIcon} ${trends.direction}</span>
            </div>
            ${trends.by_suite && trends.by_suite.length > 0 ? `
                <div class="trend-by-suite">
                    ${trends.by_suite.map(s => {
                        const changeIcon = s.change > 0 ? '↑' : s.change < 0 ? '↓' : '→';
                        const changeClass = s.change > 0 ? 'trend-up' : s.change < 0 ? 'trend-down' : 'trend-stable';
                        return `
                            <div class="suite-trend">
                                <span class="suite-trend-name">${escapeHtml(s.suite)}</span>
                                <span class="suite-trend-rate">${s.pass_rate}%</span>
                                <span class="suite-trend-change ${changeClass}">${changeIcon} ${Math.abs(s.change).toFixed(1)}%</span>
                                <span class="suite-trend-runs">${s.run_count} runs</span>
                            </div>
                        `;
                    }).join('')}
                </div>
            ` : ''}
        </div>
    `;
}

/**
 * Show detailed run view with individual test results
 */
function showRunDetail(runId) {
    const run = data.runs.find(r => r.run_id === runId);
    if (!run) return;

    const passRate = run.total > 0 ? ((run.passed / run.total) * 100).toFixed(1) : 0;
    const duration = run.duration_seconds ? formatDuration(run.duration_seconds) : 'N/A';

    const content = document.getElementById('content');
    content.innerHTML = `
        <div class="card run-detail">
            <button class="back-btn" onclick="render()">← Back to Runs</button>
            <h2>Run: ${escapeHtml(run.run_id)}</h2>
            <div class="run-detail-meta">
                <div class="stat">
                    <span class="stat-label">Suite</span>
                    <span class="stat-value">${escapeHtml(run.suite)}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Status</span>
                    <span class="stat-value badge ${run.status}">${escapeHtml(run.status)}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Started</span>
                    <span class="stat-value">${new Date(run.started_at).toLocaleString()}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Duration</span>
                    <span class="stat-value">${duration}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Executed By</span>
                    <span class="stat-value">${escapeHtml(run.started_by)}</span>
                </div>
            </div>
            <div class="run-detail-summary">
                <div class="run-progress large">
                    <div class="run-progress-bar">
                        <div class="run-progress-passed" style="width: ${passRate}%"></div>
                    </div>
                    <span class="run-progress-label">${passRate}% Pass Rate</span>
                </div>
                <div class="run-detail-stats">
                    <div class="run-stat large passed">
                        <div class="run-stat-value">${run.passed}</div>
                        <div class="run-stat-label">Passed</div>
                    </div>
                    <div class="run-stat large failed">
                        <div class="run-stat-value">${run.failed}</div>
                        <div class="run-stat-label">Failed</div>
                    </div>
                    <div class="run-stat large">
                        <div class="run-stat-value">${run.skipped}</div>
                        <div class="run-stat-label">Skipped</div>
                    </div>
                    <div class="run-stat large">
                        <div class="run-stat-value">${run.blocked}</div>
                        <div class="run-stat-label">Blocked</div>
                    </div>
                    <div class="run-stat large">
                        <div class="run-stat-value">${run.total}</div>
                        <div class="run-stat-label">Total</div>
                    </div>
                </div>
            </div>
            ${run.results && run.results.length > 0 ? `
                <h3 class="section-title">Test Results</h3>
                <div class="result-list">
                    ${run.results.map(r => `
                        <div class="result-row ${r.status}">
                            <span class="result-status badge ${r.status}">${r.status}</span>
                            <span class="result-id">${escapeHtml(r.test_id)}</span>
                            <span class="result-title">${escapeHtml(r.title || '')}</span>
                            ${r.duration_ms ? `<span class="result-duration">${r.duration_ms}ms</span>` : ''}
                        </div>
                    `).join('')}
                </div>
            ` : `
                <div class="empty-state small">
                    <p>Detailed test results not available for this run.</p>
                </div>
            `}
        </div>
    `;
}

/**
 * Format duration in seconds to human readable string
 */
function formatDuration(seconds) {
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    if (minutes < 60) return `${minutes}m ${secs}s`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
}

/**
 * Render coverage visualization
 */
function renderCoverage() {
    if (!data.coverage || !data.coverage.nodes || data.coverage.nodes.length === 0) {
        return `
            <div class="empty-state">
                <h2>No coverage data</h2>
                <p>Run <code>spectra ai analyze --coverage</code> to generate coverage analysis.</p>
            </div>
        `;
    }

    // Use the coverage map visualization from coverage-map.js
    if (typeof window.renderCoverageMap === 'function') {
        return window.renderCoverageMap();
    }

    // Fallback if coverage-map.js not loaded
    return `
        <div class="empty-state">
            <h2>Coverage Visualization</h2>
            <p>Coverage map script not loaded.</p>
        </div>
    `;
}

/**
 * Navigate to tests view filtered by suite
 */
function showSuiteTests(suite) {
    // Reset filters
    filters = { priority: '', component: '', search: '', tags: [] };
    document.getElementById('filter-priority').value = '';
    document.getElementById('filter-component').value = '';
    document.getElementById('filter-search').value = '';
    // Reset tag checkboxes
    document.querySelectorAll('#filter-tags input[type="checkbox"]').forEach(cb => cb.checked = false);

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
 * Show detailed test view in slide-out panel
 */
function showTestDetail(id) {
    const test = allTests.find(t => t.id === id);
    if (!test) return;

    const panel = document.getElementById('test-detail-panel');
    const overlay = document.getElementById('detail-panel-overlay');
    const title = document.getElementById('detail-panel-title');
    const content = document.getElementById('detail-panel-content');

    title.textContent = `${test.id}: ${test.title}`;

    content.innerHTML = `
        <div class="detail-badges">
            <span class="badge ${test.priority}">${test.priority}</span>
            ${test.component ? `<span class="badge">${escapeHtml(test.component)}</span>` : ''}
            ${(test.tags || []).map(t => `<span class="badge tag">${escapeHtml(t)}</span>`).join('')}
            ${test.has_automation ? '<span class="badge automated">Automated</span>' : '<span class="badge manual">Manual</span>'}
        </div>

        <div class="detail-section">
            <h4>Location</h4>
            <div class="detail-row">
                <span class="detail-label">Suite:</span>
                <span class="detail-value">${escapeHtml(test.suite)}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">File:</span>
                <span class="detail-value code">${escapeHtml(test.file)}</span>
            </div>
        </div>

        ${test.automated_by ? `
        <div class="detail-section">
            <h4>Automation</h4>
            <div class="detail-row">
                <span class="detail-label">Automated By:</span>
                <span class="detail-value code">${escapeHtml(test.automated_by)}</span>
            </div>
        </div>
        ` : ''}

        ${(test.source_refs || []).length > 0 ? `
        <div class="detail-section">
            <h4>Source References</h4>
            <ul class="source-ref-list">
                ${test.source_refs.map(ref => `<li class="code">${escapeHtml(ref)}</li>`).join('')}
            </ul>
        </div>
        ` : ''}

        ${test.content ? `
        <div class="detail-section">
            <h4>Test Content</h4>
            <div class="test-content-preview">
                <pre>${escapeHtml(test.content)}</pre>
            </div>
        </div>
        ` : ''}
    `;

    panel.classList.add('open');
    overlay.classList.add('open');
    document.body.classList.add('panel-open');
}

/**
 * Close the detail panel
 */
function closeDetailPanel() {
    const panel = document.getElementById('test-detail-panel');
    const overlay = document.getElementById('detail-panel-overlay');

    panel.classList.remove('open');
    overlay.classList.remove('open');
    document.body.classList.remove('panel-open');
}

/**
 * Close panel on Escape key
 */
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeDetailPanel();
    }
});

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(str) {
    if (typeof str !== 'string') return str;
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}
