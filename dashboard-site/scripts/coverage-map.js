/**
 * SPECTRA Dashboard - Coverage Treemap Visualization
 * Uses D3.js to render an interactive treemap of suite automation coverage
 */

/**
 * Render treemap visualization showing suites sized by test count, colored by automation %.
 * Returns HTML string with a container; D3 renders into it after DOM update.
 */
function renderTreemap(coverageSummary) {
    const suites = window.dashboardData?.suites;
    if (!suites || suites.length === 0) return '';

    // Build automation % lookup from coverage summary
    const autoDetails = coverageSummary?.automation?.details || [];
    const autoPctMap = {};
    for (const d of autoDetails) {
        autoPctMap[d.suite.toLowerCase()] = d.percentage;
    }

    // Build treemap data
    const children = suites
        .filter(s => s.test_count > 0)
        .map(s => ({
            name: s.name,
            value: s.test_count,
            autoPct: autoPctMap[s.name.toLowerCase()] ?? 0
        }));

    if (children.length === 0) return '';

    return `
        <div class="coverage-treemap-container">
            <h3>Suite Coverage Treemap</h3>
            <div class="coverage-treemap" id="coverage-treemap"></div>
            <div class="treemap-legend">
                <span class="treemap-legend-item"><span class="treemap-legend-dot" style="background: var(--color-success)"></span>&ge; 50% automated</span>
                <span class="treemap-legend-item"><span class="treemap-legend-dot" style="background: var(--color-warning)"></span>&gt; 0% automated</span>
                <span class="treemap-legend-item"><span class="treemap-legend-dot" style="background: var(--color-danger)"></span>0% automated</span>
            </div>
            <div class="treemap-tooltip" id="treemap-tooltip"></div>
        </div>
    `;
}

/**
 * Initialize the treemap using D3 after DOM is ready.
 */
function initTreemap() {
    const container = document.getElementById('coverage-treemap');
    if (!container || typeof d3 === 'undefined') return;

    const suites = window.dashboardData?.suites;
    const coverageSummary = window.dashboardData?.coverage_summary;
    if (!suites || suites.length === 0) return;

    const autoDetails = coverageSummary?.automation?.details || [];
    const autoPctMap = {};
    for (const d of autoDetails) {
        autoPctMap[d.suite.toLowerCase()] = d.percentage;
    }

    const children = suites
        .filter(s => s.test_count > 0)
        .map(s => ({
            name: s.name,
            value: s.test_count,
            autoPct: autoPctMap[s.name.toLowerCase()] ?? 0
        }));

    if (children.length === 0) return;

    const width = container.clientWidth || 600;
    const height = Math.max(250, Math.min(400, children.length * 60));
    container.style.height = height + 'px';

    const root = d3.hierarchy({ children })
        .sum(d => d.value)
        .sort((a, b) => b.value - a.value);

    d3.treemap()
        .size([width, height])
        .padding(3)
        .round(true)(root);

    const tooltip = document.getElementById('treemap-tooltip');

    // Render blocks as positioned divs
    for (const leaf of root.leaves()) {
        const d = leaf.data;
        const blockW = leaf.x1 - leaf.x0;
        const blockH = leaf.y1 - leaf.y0;

        if (blockW < 2 || blockH < 2) continue;

        const color = d.autoPct >= 50 ? 'var(--color-success)'
            : d.autoPct > 0 ? 'var(--color-warning)'
            : 'var(--color-danger)';

        const block = document.createElement('div');
        block.className = 'treemap-block';
        block.style.left = leaf.x0 + 'px';
        block.style.top = leaf.y0 + 'px';
        block.style.width = blockW + 'px';
        block.style.height = blockH + 'px';
        block.style.background = color;

        // Labels (only if block is big enough)
        if (blockW > 40 && blockH > 30) {
            const labelEl = document.createElement('span');
            labelEl.className = 'treemap-block-label';
            labelEl.textContent = d.name;
            block.appendChild(labelEl);

            if (blockH > 45) {
                const countEl = document.createElement('span');
                countEl.className = 'treemap-block-count';
                countEl.textContent = `${d.value} tests, ${d.autoPct.toFixed(0)}%`;
                block.appendChild(countEl);
            }
        }

        // Tooltip on hover
        block.addEventListener('mouseenter', (e) => {
            if (tooltip) {
                tooltip.innerHTML = `<strong>${escapeHtml(d.name)}</strong><br>${d.value} tests, ${d.autoPct.toFixed(1)}% automated`;
                tooltip.style.display = 'block';
            }
        });
        block.addEventListener('mousemove', (e) => {
            if (tooltip) {
                tooltip.style.left = (e.clientX + 12) + 'px';
                tooltip.style.top = (e.clientY - 10) + 'px';
            }
        });
        block.addEventListener('mouseleave', () => {
            if (tooltip) tooltip.style.display = 'none';
        });

        // Click to navigate to suite tests
        block.addEventListener('click', () => {
            if (typeof showSuiteTests === 'function') {
                showSuiteTests(d.name);
            }
        });

        container.appendChild(block);
    }
}

// Export for use in app.js
window.renderTreemap = renderTreemap;
window.initTreemap = initTreemap;
