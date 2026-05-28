/**************************************************************/
/**
 * Forest plot renderer.
 */
/**************************************************************/

import { h, renderEmptyState } from '../dom.js';
import { formatRR } from '../formatters.js';

const MIN = 0.1;
const MAX = 10;

/**************************************************************/
/**
 * Renders a forest plot from API-provided signals.
 *
 * @param {Object} forest Forest view model.
 * @returns {HTMLElement} Forest plot element.
 */
/**************************************************************/
export function renderForestView(forest) {
    const signals = forest?.signals || [];
    const ticks = forest?.axisTicks?.length ? forest.axisTicks : [0.1, 0.25, 0.5, 1, 2, 4, 10];

    if (signals.length === 0) {
        return renderEmptyState('No forest-plot rows found.', 'Try a different comparator or fragile-row setting.');
    }

    return h('div', { className: 'ae-forest-view' },
        h('div', { className: 'ae-forest-legend' },
            legendItem('elevated', 'Elevated risk'),
            legendItem('protective', 'Protective'),
            legendItem('ns', 'Not significant'),
            h('span', { className: 'ae-forest-note', text: 'Protective left of RR=1; elevated right of RR=1.' })
        ),
        h('div', { className: 'ae-forest-wrap' },
            h('div', { className: 'ae-forest-axis' },
                h('div', { className: 'ae-forest-label-spacer' }),
                h('div', { className: 'ae-forest-axis-ticks' },
                    ticks.map(tick => h('span', {
                        className: `ae-forest-tick${tick === 1 ? ' ref' : ''}`,
                        style: { left: `${xPct(tick)}%` },
                        text: String(tick)
                    }))
                )
            ),
            signals.map(signal => renderForestRow(signal))
        )
    );
}

/**************************************************************/
/**
 * Converts a relative-risk value to a log-scale percent.
 *
 * @param {number} value Relative-risk value.
 * @returns {number|null} Percent position.
 */
/**************************************************************/
export function xPct(value) {
    const number = Number(value);
    if (!Number.isFinite(number) || number <= 0) {
        return null;
    }

    const clamped = Math.min(Math.max(number, MIN), MAX);
    return ((Math.log10(clamped) - Math.log10(MIN)) / (Math.log10(MAX) - Math.log10(MIN))) * 100;
}

function renderForestRow(signal) {
    const direction = !signal.sig ? 'ns' : signal.prot ? 'protective' : 'elevated';
    const point = xPct(signal.rr);
    const low = xPct(signal.rrL);
    const high = xPct(signal.rrH);
    const ref = xPct(1);

    return h('div', { className: `ae-forest-row ${direction}${signal.prec === 'fragile' ? ' is-fragile' : ''}` },
        h('div', { className: 'ae-forest-label', title: signal.name },
            h('strong', { text: signal.name }),
            h('small', { text: signal.soc })
        ),
        h('div', { className: 'ae-forest-track' },
            h('span', { className: 'ae-forest-refline', style: { left: `${ref}%` } }),
            low !== null && high !== null
                ? [
                    h('span', { className: 'ae-forest-ci', style: { left: `${Math.min(low, high)}%`, width: `${Math.abs(high - low)}%` } }),
                    h('span', { className: 'ae-forest-ci-cap', style: { left: `${low}%` } }),
                    h('span', { className: 'ae-forest-ci-cap', style: { left: `${high}%` } })
                ]
                : null,
            point !== null ? h('span', { className: 'ae-forest-point', style: { left: `${point}%` } }) : null
        ),
        h('div', { className: 'ae-forest-value', text: `RR ${formatRR(signal.rr)} [${formatRR(signal.rrL)}-${formatRR(signal.rrH)}]` })
    );
}

function legendItem(kind, label) {
    return h('span', { className: 'ae-legend-item' },
        h('span', { className: `ae-legend-dot ${kind}` }),
        h('span', { text: label })
    );
}
