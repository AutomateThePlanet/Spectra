/**
 * SPECTRA Dashboard - Coverage Map Visualization
 * Uses D3.js to render an interactive coverage relationship graph
 */

// Coverage visualization state
let coverageMapState = {
    svg: null,
    simulation: null,
    data: null
};

/**
 * Initialize and render the coverage map visualization.
 * Called from app.js when the coverage view is activated.
 */
function renderCoverageMap() {
    const coverage = window.dashboardData?.coverage;
    if (!coverage || !coverage.nodes || coverage.nodes.length === 0) {
        return '<div class="empty-state"><h2>No Coverage Data</h2><p>Run coverage analysis to generate visualization data.</p></div>';
    }

    // Return container HTML - D3 will render into this
    return `
        <div class="coverage-map-container">
            <div class="coverage-map-header">
                <h2>Coverage Relationships</h2>
                <div class="coverage-legend">
                    <span class="legend-item"><span class="legend-dot covered"></span> Covered</span>
                    <span class="legend-item"><span class="legend-dot partial"></span> Partial</span>
                    <span class="legend-item"><span class="legend-dot uncovered"></span> Uncovered</span>
                </div>
            </div>
            <div class="coverage-map-controls">
                <button class="control-btn" onclick="resetCoverageZoom()">Reset Zoom</button>
                <button class="control-btn" onclick="toggleCoverageLabels()">Toggle Labels</button>
            </div>
            <div id="coverage-map-svg"></div>
            <div id="coverage-node-detail" class="coverage-detail-panel"></div>
        </div>
    `;
}

/**
 * Initialize the D3 force-directed graph after DOM is ready.
 */
function initCoverageMap() {
    const coverage = window.dashboardData?.coverage;
    if (!coverage || !coverage.nodes || coverage.nodes.length === 0) {
        return;
    }

    const container = document.getElementById('coverage-map-svg');
    if (!container) return;

    // Clear any existing SVG
    container.innerHTML = '';

    // Calculate dimensions
    const width = container.clientWidth || 800;
    const height = 500;

    // Create SVG
    const svg = d3.select(container)
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .attr('viewBox', [0, 0, width, height])
        .attr('class', 'coverage-svg');

    // Add zoom behavior
    const g = svg.append('g');
    const zoom = d3.zoom()
        .scaleExtent([0.2, 3])
        .on('zoom', (event) => {
            g.attr('transform', event.transform);
        });
    svg.call(zoom);

    coverageMapState.svg = svg;
    coverageMapState.zoom = zoom;

    // Prepare data
    const nodes = coverage.nodes.map(n => ({...n}));
    const links = coverage.links.map(l => ({
        source: l.source,
        target: l.target,
        type: l.type,
        status: l.status
    }));

    // Create force simulation
    const simulation = d3.forceSimulation(nodes)
        .force('link', d3.forceLink(links).id(d => d.id).distance(80))
        .force('charge', d3.forceManyBody().strength(-200))
        .force('center', d3.forceCenter(width / 2, height / 2))
        .force('collision', d3.forceCollide().radius(30));

    coverageMapState.simulation = simulation;
    coverageMapState.data = { nodes, links };

    // Draw links
    const link = g.append('g')
        .attr('class', 'coverage-links')
        .selectAll('line')
        .data(links)
        .join('line')
        .attr('class', d => `coverage-link link-${d.type} link-status-${d.status}`)
        .attr('stroke-width', 2);

    // Draw nodes
    const node = g.append('g')
        .attr('class', 'coverage-nodes')
        .selectAll('g')
        .data(nodes)
        .join('g')
        .attr('class', d => `coverage-node node-${d.type} node-${d.status}`)
        .call(drag(simulation))
        .on('click', (event, d) => showNodeDetail(d));

    // Node circles
    node.append('circle')
        .attr('r', d => getNodeRadius(d.type))
        .attr('class', d => `node-circle node-${d.status}`);

    // Node icons
    node.append('text')
        .attr('class', 'node-icon')
        .attr('text-anchor', 'middle')
        .attr('dominant-baseline', 'central')
        .text(d => getNodeIcon(d.type));

    // Node labels
    node.append('text')
        .attr('class', 'node-label')
        .attr('dy', d => getNodeRadius(d.type) + 12)
        .attr('text-anchor', 'middle')
        .text(d => truncateLabel(d.name, 15));

    // Update positions on tick
    simulation.on('tick', () => {
        link
            .attr('x1', d => d.source.x)
            .attr('y1', d => d.source.y)
            .attr('x2', d => d.target.x)
            .attr('y2', d => d.target.y);

        node.attr('transform', d => `translate(${d.x},${d.y})`);
    });
}

/**
 * Drag behavior for nodes.
 */
function drag(simulation) {
    function dragstarted(event) {
        if (!event.active) simulation.alphaTarget(0.3).restart();
        event.subject.fx = event.subject.x;
        event.subject.fy = event.subject.y;
    }

    function dragged(event) {
        event.subject.fx = event.x;
        event.subject.fy = event.y;
    }

    function dragended(event) {
        if (!event.active) simulation.alphaTarget(0);
        event.subject.fx = null;
        event.subject.fy = null;
    }

    return d3.drag()
        .on('start', dragstarted)
        .on('drag', dragged)
        .on('end', dragended);
}

/**
 * Get node radius based on type.
 */
function getNodeRadius(type) {
    switch (type) {
        case 'document': return 20;
        case 'test': return 15;
        case 'automation': return 12;
        default: return 15;
    }
}

/**
 * Get node icon based on type.
 */
function getNodeIcon(type) {
    switch (type) {
        case 'document': return 'D';
        case 'test': return 'T';
        case 'automation': return 'A';
        default: return '?';
    }
}

/**
 * Truncate label to max length.
 */
function truncateLabel(text, maxLength) {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength - 2) + '...';
}

/**
 * Show node detail panel.
 */
function showNodeDetail(node) {
    const panel = document.getElementById('coverage-node-detail');
    if (!panel) return;

    const typeLabel = node.type.charAt(0).toUpperCase() + node.type.slice(1);
    const statusClass = `status-${node.status}`;

    panel.innerHTML = `
        <div class="node-detail-header">
            <h3>${typeLabel}</h3>
            <span class="node-detail-status ${statusClass}">${node.status}</span>
        </div>
        <div class="node-detail-content">
            <div class="detail-row">
                <span class="detail-label">Name:</span>
                <span class="detail-value">${escapeHtml(node.name)}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Path:</span>
                <span class="detail-value">${escapeHtml(node.path)}</span>
            </div>
            ${renderNodeConnections(node)}
        </div>
    `;
    panel.classList.add('visible');
}

/**
 * Render connections for a node.
 */
function renderNodeConnections(node) {
    if (!coverageMapState.data) return '';

    const incomingLinks = coverageMapState.data.links.filter(l => l.target.id === node.id || l.target === node.id);
    const outgoingLinks = coverageMapState.data.links.filter(l => l.source.id === node.id || l.source === node.id);

    let html = '';

    if (incomingLinks.length > 0) {
        html += `<div class="detail-section">
            <span class="detail-label">Referenced by:</span>
            <ul class="connection-list">
                ${incomingLinks.map(l => {
                    const sourceId = typeof l.source === 'object' ? l.source.id : l.source;
                    return `<li>${escapeHtml(sourceId)}</li>`;
                }).join('')}
            </ul>
        </div>`;
    }

    if (outgoingLinks.length > 0) {
        html += `<div class="detail-section">
            <span class="detail-label">References:</span>
            <ul class="connection-list">
                ${outgoingLinks.map(l => {
                    const targetId = typeof l.target === 'object' ? l.target.id : l.target;
                    return `<li>${escapeHtml(targetId)}</li>`;
                }).join('')}
            </ul>
        </div>`;
    }

    return html;
}

/**
 * Escape HTML for safe display.
 */
function escapeHtmlCoverage(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Reset zoom to initial state.
 */
function resetCoverageZoom() {
    if (coverageMapState.svg && coverageMapState.zoom) {
        coverageMapState.svg.transition()
            .duration(500)
            .call(coverageMapState.zoom.transform, d3.zoomIdentity);
    }
}

/**
 * Toggle node labels visibility.
 */
function toggleCoverageLabels() {
    const labels = document.querySelectorAll('.node-label');
    labels.forEach(label => {
        label.classList.toggle('hidden');
    });
}

// Export for use in app.js
window.renderCoverageMap = renderCoverageMap;
window.initCoverageMap = initCoverageMap;
