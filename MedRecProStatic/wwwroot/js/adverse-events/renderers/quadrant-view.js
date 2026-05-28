/**************************************************************/
/**
 * Risk-vs-precision quadrant renderer.
 */
/**************************************************************/

import { h, renderEmptyState } from '../dom.js';
import { formatEventRate, formatRR } from '../formatters.js';

/**************************************************************/
/**
 * Renders a quadrant chart from API-provided points.
 *
 * @param {Object} quadrant Quadrant view model.
 * @returns {HTMLElement} Quadrant element.
 */
/**************************************************************/
export function renderQuadrantView(quadrant) {
    const points = quadrant?.points || [];

    if (points.length === 0) {
        return renderEmptyState('No quadrant points found.', 'Try a different comparator or fragile-row setting.');
    }

    const plot = h('div', { className: 'ae-quadrant' },
        quadrantCell('tl', 'Investigate', 'Big but uncertain'),
        quadrantCell('tr', 'Warn', 'Big and certain'),
        quadrantCell('bl', 'Ignore', 'Small and noisy'),
        quadrantCell('br', 'Reassure', 'Small and certain')
    );

    points.forEach(point => {
        const signal = point.signal;
        const x = boundedPercent(point.precisionX);
        const y = 100 - boundedPercent(point.magnitudeY);
        const size = Math.max(8, Math.min(34, Number(point.bubbleSize) || 10));
        const direction = point.direction === 'not-significant'
            ? 'ns'
            : point.direction || signal?.riskSignificance || 'ns';

        const dot = h('button', {
            type: 'button',
            className: `ae-q-dot ${direction}${signal?.prec === 'fragile' ? ' is-fragile' : ''}`,
            style: {
                left: `${x}%`,
                top: `${y}%`,
                width: `${size}px`,
                height: `${size}px`
            },
            title: signal?.name || 'Adverse event'
        });

        dot.appendChild(h('span', { className: 'ae-q-tooltip' },
            h('strong', { text: signal?.name || 'Adverse event' }),
            h('small', { text: `RR ${formatRR(signal?.rr)} [${formatRR(signal?.rrL)}-${formatRR(signal?.rrH)}]` }),
            h('small', { text: formatEventRate(signal?.eT, signal?.armN) })
        ));
        plot.appendChild(dot);
    });

    return h('div', { className: 'ae-quadrant-view' },
        h('div', { className: 'ae-axis-y', text: 'Effect magnitude' }),
        plot,
        h('div', { className: 'ae-quadrant-axes' },
            h('span', { text: 'Lower precision' }),
            h('span', { text: 'Higher precision' })
        )
    );
}

function quadrantCell(className, title, body) {
    return h('div', { className: `ae-q-cell ${className}` },
        h('span', { className: 'ae-q-cell-title', text: title }),
        h('span', { className: 'ae-q-cell-body', text: body })
    );
}

function boundedPercent(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return 50;
    }

    return Math.max(4, Math.min(96, number * 100));
}
