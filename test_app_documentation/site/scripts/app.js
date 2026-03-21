/**
 * SPECTRA Dashboard Application
 * Client-side rendering for test suite dashboard
 */

// Load dashboard data from embedded JSON
const data = JSON.parse(document.getElementById('dashboard-data').textContent);
window.dashboardData = data;

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
            // Initialize D3 visualizations after DOM is updated
            setTimeout(() => {
                if (typeof window.initCoverageMap === 'function') window.initCoverageMap();
                if (typeof window.initTreemap === 'function') window.initTreemap();
            }, 0);
            break;
    }
}

/**
 * Get tests belonging to a suite (by name match)
 */
function getTestsForSuite(suiteName) {
    return allTests.filter(t => t.suite === suiteName);
}

/**
 * Check if a suite matches current filters
 */
function suiteMatchesFilters(suite) {
    const suiteTests = getTestsForSuite(suite.name);
    if (filters.priority && !suiteTests.some(t => t.priority === filters.priority)) return false;
    if (filters.component && !suiteTests.some(t => t.component === filters.component)) return false;
    if (filters.search) {
        const s = filters.search.toLowerCase();
        if (!suite.name.toLowerCase().includes(s) &&
            !suiteTests.some(t => t.title.toLowerCase().includes(s) || t.id.toLowerCase().includes(s))) {
            return false;
        }
    }
    if (filters.tags.length > 0) {
        if (!suiteTests.some(t => filters.tags.every(tag => (t.tags || []).includes(tag)))) return false;
    }
    return true;
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

    const filtered = data.suites.filter(s => suiteMatchesFilters(s));

    if (!filtered.length) {
        return `
            <div class="empty-state">
                <h2>No suites match filters</h2>
                <p>Try adjusting your filters.</p>
            </div>
        `;
    }

    return `
        <div class="card-grid">
            ${filtered.map(s => `
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
 * Check if a run matches current filters (by cross-referencing suite metadata)
 */
function runMatchesFilters(run) {
    const suite = data.suites.find(s => s.name.toLowerCase() === (run.suite || '').toLowerCase());
    const suiteTests = getTestsForSuite(run.suite);

    if (filters.search) {
        const s = filters.search.toLowerCase();
        if (!(run.suite || '').toLowerCase().includes(s) &&
            !(run.run_id || '').toLowerCase().includes(s)) {
            return false;
        }
    }
    if (filters.priority && suiteTests.length > 0 && !suiteTests.some(t => t.priority === filters.priority)) return false;
    if (filters.component && suiteTests.length > 0 && !suiteTests.some(t => t.component === filters.component)) return false;
    if (filters.tags.length > 0 && suiteTests.length > 0) {
        if (!suiteTests.some(t => filters.tags.every(tag => (t.tags || []).includes(tag)))) return false;
    }
    return true;
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

    const filteredRuns = data.runs.filter(r => runMatchesFilters(r));
    const trendChart = renderTrendChart();
    const trendSummary = renderTrendSummary();

    return `
        ${trendChart}
        ${trendSummary}
        <div class="section-header">
            <h3 class="section-title">Run History</h3>
            <span class="section-count">${filteredRuns.length} runs</span>
        </div>
        ${filteredRuns.length === 0 ? `
            <div class="empty-state">
                <h2>No runs match filters</h2>
                <p>Try adjusting your filters.</p>
            </div>
        ` : `
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
                    ${filteredRuns.map(r => {
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
        `}
    `;
}

/**
 * Render trend chart (line + area chart)
 */
function renderTrendChart() {
    const trends = data.trends;
    if (!trends || !trends.points || trends.points.length === 0) {
        return '';
    }

    const points = trends.points.slice(-14); // Last 14 days
    const w = 600, h = 200;
    const pad = { top: 20, right: 20, bottom: 30, left: 45 };
    const plotW = w - pad.left - pad.right;
    const plotH = h - pad.top - pad.bottom;

    // Determine line color from trend direction
    const lineColor = trends.direction === 'improving' ? 'var(--color-success)' :
                      trends.direction === 'declining' ? 'var(--color-danger)' : 'var(--color-warning)';

    // Map points to coordinates
    const coords = points.map((p, i) => {
        const x = pad.left + (points.length === 1 ? plotW / 2 : (i / (points.length - 1)) * plotW);
        const y = pad.top + plotH - (p.pass_rate / 100) * plotH;
        return { x, y, p };
    });

    // Grid lines at 0%, 25%, 50%, 75%, 100%
    const gridLines = [0, 25, 50, 75, 100].map(pct => {
        const y = pad.top + plotH - (pct / 100) * plotH;
        return `<line x1="${pad.left}" y1="${y}" x2="${w - pad.right}" y2="${y}" stroke="#e2e8f0" stroke-dasharray="4 2"/>
                ${pct % 50 === 0 ? `<text x="${pad.left - 8}" y="${y + 4}" text-anchor="end" fill="#64748b" font-size="11">${pct}%</text>` : ''}`;
    }).join('');

    // Polyline points string
    const linePoints = coords.map(c => `${c.x},${c.y}`).join(' ');

    // Area polygon (line + bottom edge)
    const areaPoints = coords.map(c => `${c.x},${c.y}`).join(' ') +
        ` ${coords[coords.length - 1].x},${pad.top + plotH} ${coords[0].x},${pad.top + plotH}`;

    // Data point circles with tooltips
    const circles = coords.map(c => {
        const dateStr = new Date(c.p.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        return `<circle cx="${c.x}" cy="${c.y}" r="4" fill="${lineColor}" stroke="white" stroke-width="2">
                    <title>${dateStr}: ${c.p.pass_rate}% (${c.p.passed}/${c.p.total})</title>
                </circle>`;
    }).join('');

    // X-axis labels (first, middle, last)
    const xLabels = [];
    if (points.length > 0) {
        const indices = points.length <= 2 ? points.map((_, i) => i) : [0, Math.floor(points.length / 2), points.length - 1];
        indices.forEach(i => {
            const dateStr = new Date(points[i].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            xLabels.push(`<text x="${coords[i].x}" y="${h - 5}" text-anchor="middle" fill="#64748b" font-size="11">${dateStr}</text>`);
        });
    }

    return `
        <div class="trend-chart-container">
            <h3 class="section-title">Pass Rate Trend (Last 14 Days)</h3>
            <div class="trend-chart">
                <svg viewBox="0 0 ${w} ${h}" preserveAspectRatio="xMidYMid meet" class="trend-svg">
                    ${gridLines}
                    <polygon points="${areaPoints}" fill="${lineColor}" opacity="0.1"/>
                    <polyline points="${linePoints}" fill="none" stroke="${lineColor}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
                    ${circles}
                    ${xLabels.join('')}
                </svg>
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
                    <span class="stat-label">Suite:</span>
                    <span class="stat-value">${escapeHtml(run.suite)}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Status:</span>
                    <span class="stat-value badge ${run.status}">${escapeHtml(run.status)}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Started:</span>
                    <span class="stat-value">${new Date(run.started_at).toLocaleString()}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Duration:</span>
                    <span class="stat-value">${duration}</span>
                </div>
                <div class="stat">
                    <span class="stat-label">Executed By:</span>
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
                <div class="result-filters">
                    <button class="result-filter-btn active" onclick="filterResults('all', this)">All (${run.results.length})</button>
                    <button class="result-filter-btn passed" onclick="filterResults('Passed', this)">Passed (${run.results.filter(r => r.status === 'Passed').length})</button>
                    <button class="result-filter-btn failed" onclick="filterResults('Failed', this)">Failed (${run.results.filter(r => r.status === 'Failed').length})</button>
                    <button class="result-filter-btn skipped" onclick="filterResults('Skipped', this)">Skipped (${run.results.filter(r => r.status === 'Skipped').length})</button>
                    <button class="result-filter-btn blocked" onclick="filterResults('Blocked', this)">Blocked (${run.results.filter(r => r.status === 'Blocked').length})</button>
                </div>
                <div class="result-list" id="result-list">
                    ${run.results.map(r => renderResultRow(r)).join('')}
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
 * Render individual result row with expandable details.
 * Cross-references parsed test content from allTests for full detail.
 */
function renderResultRow(r) {
    const statusLower = (r.status || '').toLowerCase();
    const durationStr = r.duration_ms ? formatDurationMs(r.duration_ms) : '';

    // Cross-reference with parsed test data for full content
    const testData = allTests.find(t => t.id === r.test_id);
    const preconditions = r.preconditions || (testData && testData.preconditions);
    const steps = r.steps || (testData && testData.steps);
    const expectedResult = r.expected_result || (testData && testData.expected_result);
    const testDataStr = r.test_data || (testData && testData.test_data);
    const screenshots = r.screenshot_paths;

    const hasDetails = r.notes || r.blocked_by || r.attempt > 1 ||
                       preconditions || (steps && steps.length > 0) ||
                       expectedResult || testDataStr || (screenshots && screenshots.length > 0);

    if (hasDetails) {
        return `
            <details class="result-row ${statusLower}" data-status="${r.status}">
                <summary>
                    <span class="result-status badge ${statusLower}">${escapeHtml(r.status)}</span>
                    <span class="result-id">${escapeHtml(r.test_id)}</span>
                    <span class="result-title">${escapeHtml(r.title || (testData && testData.title) || '')}</span>
                    ${r.attempt > 1 ? `<span class="result-attempt">Attempt ${r.attempt}</span>` : ''}
                    ${durationStr ? `<span class="result-duration">${durationStr}</span>` : ''}
                </summary>
                <div class="result-details">
                    ${preconditions ? `<div class="result-section"><strong>Preconditions:</strong><div class="result-section-text">${escapeHtml(preconditions)}</div></div>` : ''}
                    ${steps && steps.length > 0 ? `<div class="result-section"><strong>Steps:</strong><ol class="result-steps-list">${steps.map(s => `<li>${escapeHtml(s)}</li>`).join('')}</ol></div>` : ''}
                    ${expectedResult ? `<div class="result-section"><strong>Expected Result:</strong><div class="result-section-text">${escapeHtml(expectedResult)}</div></div>` : ''}
                    ${testDataStr ? `<div class="result-section"><strong>Test Data:</strong><pre class="result-test-data">${escapeHtml(testDataStr)}</pre></div>` : ''}
                    ${r.notes ? `<div class="result-notes"><strong>Actual Result / Notes:</strong> ${escapeHtml(r.notes)}</div>` : ''}
                    ${r.blocked_by ? `<div class="result-blocked-by"><strong>Blocked by:</strong> ${escapeHtml(r.blocked_by)}</div>` : ''}
                    ${screenshots && screenshots.length > 0 ? `<div class="result-section"><strong>Screenshots:</strong><div class="result-screenshots">${screenshots.map(s => `<a href="${escapeHtml(s)}" target="_blank" class="screenshot-thumb"><img src="${escapeHtml(s)}" alt="Screenshot" /></a>`).join('')}</div></div>` : ''}
                </div>
            </details>
        `;
    } else {
        return `
            <div class="result-row ${statusLower}" data-status="${r.status}">
                <span class="result-status badge ${statusLower}">${escapeHtml(r.status)}</span>
                <span class="result-id">${escapeHtml(r.test_id)}</span>
                <span class="result-title">${escapeHtml(r.title || (testData && testData.title) || '')}</span>
                ${durationStr ? `<span class="result-duration">${durationStr}</span>` : ''}
            </div>
        `;
    }
}

/**
 * Filter results by status
 */
function filterResults(status, btn) {
    const resultList = document.getElementById('result-list');
    if (!resultList) return;

    // Update active button
    document.querySelectorAll('.result-filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');

    // Filter rows
    const rows = resultList.querySelectorAll('.result-row');
    rows.forEach(row => {
        if (status === 'all' || row.dataset.status === status) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    });
}

/**
 * Format duration in milliseconds to human readable string
 */
function formatDurationMs(ms) {
    if (ms < 1000) return `${ms}ms`;
    const seconds = Math.floor(ms / 1000);
    const millis = ms % 1000;
    if (seconds < 60) return `${seconds}.${Math.floor(millis / 100)}s`;
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    if (minutes < 60) return `${minutes}m ${secs}s`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
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
 * Render coverage visualization with summary panel
 */
function renderCoverage() {
    // Use coverage_summary (new three-section data) if available
    if (data.coverage_summary) {
        return renderThreeSectionCoverage(data.coverage_summary);
    }

    // Fallback: compute from old coverage nodes/links
    if (!data.coverage || !data.coverage.nodes || data.coverage.nodes.length === 0) {
        return `
            <div class="empty-state">
                <h2>No coverage data</h2>
                <p>Run <code>spectra ai analyze --coverage</code> to generate coverage analysis.</p>
            </div>
        `;
    }

    // Legacy: calculate from nodes and links
    const docNodes = data.coverage.nodes.filter(n => n.type === 'Document');
    const testNodes = data.coverage.nodes.filter(n => n.type === 'Test');
    const docToTestLinks = data.coverage.links.filter(l => l.type === 'DocumentToTest');

    const docsWithTests = new Set(docToTestLinks.map(l => l.source)).size;
    const totalDocs = docNodes.length;
    const docCoverage = totalDocs > 0 ? ((docsWithTests / totalDocs) * 100).toFixed(1) : 0;

    const testsWithAutomation = testNodes.filter(n => n.status === 'Covered').length;
    const totalTests = testNodes.length;
    const autoCoverage = totalTests > 0 ? ((testsWithAutomation / totalTests) * 100).toFixed(1) : 0;

    // Build a synthetic coverage_summary and render
    return renderThreeSectionCoverage({
        documentation: { covered: docsWithTests, total: totalDocs, percentage: parseFloat(docCoverage) },
        requirements: { covered: 0, total: 0, percentage: 0 },
        automation: { covered: testsWithAutomation, total: totalTests, percentage: parseFloat(autoCoverage) }
    });
}

/**
 * Render three stacked coverage sections with progress bars, expandable details, and empty states.
 */
function renderThreeSectionCoverage(summary) {
    let html = '';

    // Donut chart at top (if test data available)
    html += renderDonutChart();

    html += '<div class="coverage-sections">';

    // Documentation section
    html += renderCoverageSection('Documentation Coverage', summary.documentation, 'documents', renderDocDetails);

    // Requirements section
    html += renderCoverageSection('Requirements Coverage', summary.requirements, 'requirements', renderReqDetails);

    // Automation section
    html += renderCoverageSection('Automation Coverage', summary.automation, 'tests', renderAutoDetails);

    html += '</div>';

    // Treemap (rendered by coverage-map.js if available)
    if (typeof window.renderTreemap === 'function') {
        html += window.renderTreemap(summary);
    }

    // Legacy coverage map visualization
    if (typeof window.renderCoverageMap === 'function') {
        html += window.renderCoverageMap();
    }

    return html;
}

/**
 * Render a single coverage section with progress bar, empty state, and detail toggle.
 */
function renderCoverageSection(label, sectionData, unit, renderDetailsFn) {
    const pct = sectionData.percentage || 0;
    const colorClass = pct >= 80 ? 'coverage-green' : pct >= 50 ? 'coverage-yellow' : 'coverage-red';
    const covered = sectionData.covered || 0;
    const total = sectionData.total || 0;
    const sectionId = label.toLowerCase().replace(/\s+/g, '-');

    let html = `<div class="coverage-section">
        <div class="coverage-section-header">
            <span class="coverage-section-label">${label}</span>
            <span class="coverage-section-pct ${colorClass}">${pct.toFixed(1)}%</span>
        </div>
        <div class="coverage-progress-bar">
            <div class="coverage-bar-fill ${colorClass}" style="width: ${Math.min(pct, 100)}%"></div>
        </div>
        <span class="coverage-section-detail">${covered} of ${total} ${unit} covered</span>`;

    // Empty states
    const emptyHtml = getEmptyState(label, sectionData, total, pct);
    if (emptyHtml) {
        html += emptyHtml;
    } else if (sectionData.details && sectionData.details.length > 0) {
        // Expandable detail list
        html += `<button class="coverage-toggle-btn" onclick="toggleCoverageDetails('${sectionId}')">Show details</button>`;
        html += `<ul class="coverage-detail-list collapsed" id="details-${sectionId}">`;
        html += renderDetailsFn(sectionData);
        html += '</ul>';
    }

    html += '</div>';
    return html;
}

/**
 * Get empty state HTML for a coverage section, or null if data is present.
 */
function getEmptyState(label, sectionData, total, pct) {
    if (label.startsWith('Documentation')) {
        if (total === 0) return null; // No docs at all — standard progress bar is fine
        if (pct === 100 && total > 0) {
            return `<div class="coverage-empty-state success">
                <span class="empty-icon">&#10004;</span>
                <div class="empty-text"><strong>All documents have test coverage!</strong>Every documentation file is referenced by at least one test.</div>
            </div>`;
        }
        return null;
    }

    if (label.startsWith('Requirements')) {
        if (total === 0 && !sectionData.has_requirements_file) {
            return `<div class="coverage-empty-state">
                <span class="empty-icon">&#9432;</span>
                <div class="empty-text"><strong>No requirements tracked yet.</strong>
                Add a <code>requirements</code> field to test YAML frontmatter, or create a <code>_requirements.yaml</code> file in your docs directory.</div>
            </div>`;
        }
        return null;
    }

    if (label.startsWith('Automation')) {
        if (total === 0 || (sectionData.covered === 0 && (!sectionData.details || sectionData.details.length === 0))) {
            return `<div class="coverage-empty-state">
                <span class="empty-icon">&#9432;</span>
                <div class="empty-text"><strong>No automation links detected.</strong>
                Run <code>spectra ai analyze --coverage --auto-link</code> to scan your automation code and link tests automatically.</div>
            </div>`;
        }
        return null;
    }

    return null;
}

/**
 * Toggle expand/collapse of a coverage detail list.
 */
function toggleCoverageDetails(sectionId) {
    const list = document.getElementById('details-' + sectionId);
    const btn = list?.previousElementSibling;
    if (!list || !btn) return;

    if (list.classList.contains('collapsed')) {
        list.classList.remove('collapsed');
        btn.textContent = 'Hide details';
    } else {
        list.classList.add('collapsed');
        btn.textContent = 'Show details';
    }
}

/**
 * Render documentation detail list items.
 */
function renderDocDetails(section) {
    let html = '';
    for (const d of section.details) {
        const icon = d.covered
            ? '<span class="detail-icon coverage-green">&#10003;</span>'
            : '<span class="detail-icon coverage-red">&#10007;</span>';
        html += `<li>
            <span class="detail-name" title="${escapeHtml(d.doc)}">${escapeHtml(d.doc)}</span>
            <span class="detail-meta">${d.test_count} test${d.test_count !== 1 ? 's' : ''}</span>
            ${icon}
        </li>`;
    }
    return html;
}

/**
 * Render requirements detail list items.
 */
function renderReqDetails(section) {
    let html = '';
    for (const d of section.details) {
        const icon = d.covered
            ? '<span class="detail-icon coverage-green">&#10003;</span>'
            : '<span class="detail-icon coverage-red">&#10007;</span>';
        const testList = d.tests && d.tests.length > 0 ? d.tests.join(', ') : 'none';
        html += `<li>
            <span class="detail-name" title="${escapeHtml(d.title || d.id)}">${escapeHtml(d.id)}: ${escapeHtml(d.title || '')}</span>
            <span class="detail-meta">${testList}</span>
            ${icon}
        </li>`;
    }
    return html;
}

/**
 * Render automation detail list items (per-suite breakdown).
 */
function renderAutoDetails(section) {
    let html = '';
    for (const d of section.details) {
        const suiteColor = d.percentage >= 80 ? 'coverage-green' : d.percentage >= 50 ? 'coverage-yellow' : 'coverage-red';
        html += `<li>
            <span class="detail-name">${escapeHtml(d.suite)}</span>
            <span class="detail-meta ${suiteColor}">${d.automated}/${d.total} (${d.percentage.toFixed(1)}%)</span>
        </li>`;
    }

    // Show unlinked tests if available
    if (section.unlinked_tests && section.unlinked_tests.length > 0) {
        const count = section.unlinked_tests.length;
        const preview = section.unlinked_tests.slice(0, 5).map(t => t.test_id).join(', ');
        const more = count > 5 ? `, +${count - 5} more` : '';
        html += `<li style="color: var(--text-muted); font-style: italic;">
            <span class="detail-name">Unlinked: ${preview}${more}</span>
        </li>`;
    }
    return html;
}

/**
 * Render donut chart showing test health distribution.
 */
function renderDonutChart() {
    if (!allTests || allTests.length === 0) return '';

    let automated = 0, manualOnly = 0, unlinked = 0;
    for (const t of allTests) {
        if (t.has_automation || (t.automated_by && t.automated_by.length > 0)) {
            automated++;
        } else if (t.source_refs && t.source_refs.length > 0) {
            manualOnly++;
        } else {
            unlinked++;
        }
    }

    const total = allTests.length;
    if (total === 0) return '';

    const size = 180;
    const strokeWidth = 30;
    const radius = (size - strokeWidth) / 2;
    const circumference = 2 * Math.PI * radius;
    const cx = size / 2;
    const cy = size / 2;

    // Calculate segment lengths
    const segments = [
        { count: automated, pct: (automated / total) * 100, color: 'var(--color-success)', label: 'Automated' },
        { count: manualOnly, pct: (manualOnly / total) * 100, color: 'var(--color-warning)', label: 'Manual Only' },
        { count: unlinked, pct: (unlinked / total) * 100, color: 'var(--color-danger)', label: 'Unlinked' }
    ].filter(s => s.count > 0);

    let svgSegments = '';
    let offset = 0;
    for (const seg of segments) {
        const dashLen = (seg.count / total) * circumference;
        const dashGap = circumference - dashLen;
        svgSegments += `<circle class="donut-segment" cx="${cx}" cy="${cy}" r="${radius}"
            fill="none" stroke="${seg.color}" stroke-width="${strokeWidth}"
            stroke-dasharray="${dashLen} ${dashGap}"
            stroke-dashoffset="${-offset}"
            transform="rotate(-90 ${cx} ${cy})">
            <title>${seg.label}: ${seg.count} (${seg.pct.toFixed(1)}%)</title>
        </circle>`;
        offset += dashLen;
    }

    const legendItems = segments.map(s => {
        const dotClass = s.label === 'Automated' ? 'automated' : s.label === 'Manual Only' ? 'manual' : 'unlinked';
        return `<span class="donut-legend-item"><span class="donut-legend-dot ${dotClass}"></span>${s.label}: ${s.count} (${s.pct.toFixed(1)}%)</span>`;
    }).join('');

    return `
        <div class="donut-chart-container">
            <h3>Test Health Distribution</h3>
            <div class="donut-chart">
                <svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
                    <circle cx="${cx}" cy="${cy}" r="${radius}" fill="none" stroke="#e5e7eb" stroke-width="${strokeWidth}"/>
                    ${svgSegments}
                </svg>
                <div class="donut-center">
                    <span class="donut-center-count">${total}</span>
                    <span class="donut-center-label">tests</span>
                </div>
            </div>
            <div class="donut-legend">${legendItems}</div>
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

        ${test.preconditions ? `
        <div class="detail-section">
            <h4>Preconditions</h4>
            <div class="detail-text">${escapeHtml(test.preconditions)}</div>
        </div>
        ` : ''}

        ${(test.steps || []).length > 0 ? `
        <div class="detail-section">
            <h4>Steps</h4>
            <ol class="steps-list">
                ${test.steps.map(step => `<li>${escapeHtml(step)}</li>`).join('')}
            </ol>
        </div>
        ` : ''}

        ${test.expected_result ? `
        <div class="detail-section">
            <h4>Expected Result</h4>
            <div class="detail-text">${escapeHtml(test.expected_result)}</div>
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

        ${test.content && !test.steps ? `
        <div class="detail-section">
            <h4>Raw Content</h4>
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
