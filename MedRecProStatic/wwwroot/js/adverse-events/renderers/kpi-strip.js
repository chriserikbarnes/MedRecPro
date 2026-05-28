/**************************************************************/
/**
 * KPI strip renderer for the AE dashboard.
 */
/**************************************************************/

import { h } from '../dom.js';
import { formatInteger } from '../formatters.js';

/**************************************************************/
/**
 * Renders product KPI cards.
 *
 * @param {HTMLElement} container Host element.
 * @param {Object|null} product Selected product.
 */
/**************************************************************/
export function renderKpiStrip(container, product) {
    if (!product) {
        container.replaceChildren();
        return;
    }

    container.replaceChildren(h('div', { className: 'ae-kpi-strip' },
        kpiCard('AE rows',
            formatInteger(product.rowCount),
            `${formatInteger(product.armN)} pts - vs ${formatInteger(product.comparatorN)} comparator`),
        kpiCard('Significant signals',
            formatInteger(product.significant),
            h('span', { className: 'ae-kpi-pips' },
                h('span', { className: 'ae-kpi-pip orange', text: `${formatInteger(product.significantElevated)} elevated` }),
                h('span', { className: 'ae-kpi-pip teal', text: `${formatInteger(product.significantProtective)} protective` })
            )),
        kpiCard('Comparator mix',
            comparatorMix(product),
            comparatorDescription(product)),
        kpiCard('Chart-worthiness',
            h('span', {},
                h('span', { text: formatInteger(product.score) }),
                h('small', { text: '/100' })
            ),
            scoreBar(product.score))
    ));
}

function kpiCard(label, value, sub) {
    return h('section', { className: 'ae-kpi-card' },
        h('div', { className: 'ae-kpi-label', text: label }),
        h('div', { className: 'ae-kpi-value' }, value),
        h('div', { className: 'ae-kpi-sub' }, sub)
    );
}

function comparatorMix(product) {
    if (product.placeboCoverage && product.activeCoverage) {
        return h('span', {}, h('span', { text: 'Both' }), h('small', { text: ' strata' }));
    }

    return product.placeboCoverage ? 'Placebo' : 'Active';
}

function comparatorDescription(product) {
    if (product.placeboCoverage && product.activeCoverage) {
        return 'Placebo + active comparator both present';
    }

    return product.placeboCoverage
        ? 'Placebo-only comparator coverage'
        : 'Active-comparator only';
}

function scoreBar(score) {
    const segments = 10;
    const filled = Math.round((Math.max(0, Math.min(100, Number(score) || 0)) / 100) * segments);
    return h('div', { className: 'ae-score-bar' },
        Array.from({ length: segments }, (_, index) => h('span', {
            className: `ae-score-seg${index < filled ? ' is-on' : ''}`
        }))
    );
}
