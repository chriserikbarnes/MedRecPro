/**************************************************************/
/**
 * Therapeutic interchange panel renderer.
 */
/**************************************************************/

import { h, renderEmptyState, renderInlineError, renderLoading } from '../dom.js';
import { formatRR } from '../formatters.js';
import { xPct } from './forest-view.js';
import { renderCompactPicker } from './product-picker.js';

/**************************************************************/
/**
 * Renders the interchange panel.
 *
 * @param {HTMLElement} container Host element.
 * @param {Object} options Rendering options and callbacks.
 */
/**************************************************************/
export function renderInterchangePanel(container, options) {
    const state = options.state;
    const productA = options.getProduct(state.documentGuidA);
    const productB = options.getProduct(state.documentGuidB);

    container.replaceChildren(h('section', { className: 'ae-panel', 'data-screen-label': 'Interchange' },
        h('header', { className: 'ae-panel-header' },
            h('span', {},
                h('h2', { text: 'Therapeutic interchange differential' }),
                h('p', { text: 'Compare live AE rows for two dashboard-ready products.' })
            )
        ),
        h('div', { className: 'ae-ic-pickers' },
            h('div', { className: 'ae-ic-picker' },
                h('span', { className: 'ae-ic-label a', text: 'From - product A' }),
                renderCompactPicker({
                    ...options.productPickerOptions,
                    product: productA,
                    currentDocumentGuid: state.documentGuidA,
                    disabledGuids: [state.documentGuidB].filter(Boolean),
                    accent: 'var(--color-primary)',
                    onSelect: options.onPickA
                })
            ),
            h('div', { className: 'ae-ic-arrow', text: 'to' }),
            h('div', { className: 'ae-ic-picker' },
                h('span', { className: 'ae-ic-label b', text: 'To - product B' }),
                renderCompactPicker({
                    ...options.productPickerOptions,
                    product: productB,
                    currentDocumentGuid: state.documentGuidB,
                    disabledGuids: [state.documentGuidA].filter(Boolean),
                    accent: 'var(--color-teal-deep)',
                    onSelect: options.onPickB
                })
            ),
            h('button', {
                type: 'button',
                className: `ae-chip-toggle${state.differencesOnly ? ' on' : ''}`,
                onClick: options.onToggleDifferences,
            },
                h('span', { className: 'ae-switch' }),
                h('span', { text: 'Differences only' })
            )
        ),
        renderInterchangeContent(options)
    ));
}

function renderInterchangeContent(options) {
    if (!options.state.documentGuidA || !options.state.documentGuidB) {
        return renderEmptyState('Choose two products.', 'The comparison runs after both product pickers have a different selection.');
    }

    if (options.loading) {
        return renderLoading('Comparing adverse-event profiles');
    }

    if (options.error) {
        return renderInlineError(options.error, options.onReload);
    }

    const result = options.state.result;
    if (!result) {
        return renderEmptyState('Comparison is ready.', 'Pick two different products to compare their AE profiles.');
    }

    return h('div', { className: 'ae-ic-content' },
        h('div', { className: 'ae-ic-summary' },
            summaryCell('a', result.aWorseCount, `${result.productA?.name || 'Product A'} worse`),
            summaryCell('', result.similarCount, 'Similar'),
            summaryCell('b', result.bWorseCount, `${result.productB?.name || 'Product B'} worse`),
            summaryCell('only', result.onlyACount + result.onlyBCount, 'Only on one side')
        ),
        result.classMismatchWarning || result.comparatorMismatchWarning
            ? h('div', { className: 'ae-ic-warn' },
                result.classMismatchWarning ? h('p', { text: result.classMismatchWarning }) : null,
                result.comparatorMismatchWarning ? h('p', { text: result.comparatorMismatchWarning }) : null
            )
            : null,
        renderInterchangeAxis(),
        renderInterchangeGroups(result)
    );
}

function renderInterchangeGroups(result) {
    const groups = [
        { id: 'a-worse', label: `${result.productA?.name || 'Product A'} worse`, rows: result.rows.filter(row => row.classification === 'a-worse') },
        { id: 'b-worse', label: `${result.productB?.name || 'Product B'} worse`, rows: result.rows.filter(row => row.classification === 'b-worse') },
        { id: 'similar', label: 'Similar AE profile', rows: result.rows.filter(row => row.classification === 'similar') },
        { id: 'only-a', label: `Only on ${result.productA?.name || 'product A'}`, rows: result.rows.filter(row => row.classification === 'only-a') },
        { id: 'only-b', label: `Only on ${result.productB?.name || 'product B'}`, rows: result.rows.filter(row => row.classification === 'only-b') }
    ];

    return h('div', { className: 'ae-ic-groups' },
        groups.filter(group => group.rows.length > 0).map(group => h('div', { className: 'ae-ic-group' },
            h('div', { className: 'ae-ic-divider' },
                h('span', { text: `${group.label} - ${group.rows.length}` }),
                h('span', { className: 'line' })
            ),
            group.rows.map(row => renderInterchangeRow(row))
        ))
    );
}

function renderInterchangeRow(row) {
    return h('article', { className: 'ae-ic-row' },
        h('div', { className: 'ae-ic-name', title: row.name },
            h('strong', { text: row.name }),
            h('small', { text: row.soc })
        ),
        h('div', { className: 'ae-ic-track' },
            h('span', { className: 'ae-ic-refline', style: { left: `${xPct(1)}%` } }),
            renderSignalHalf(row.signalA, 'a'),
            renderSignalHalf(row.signalB, 'b')
        ),
        h('div', { className: `ae-ic-delta ${row.classification}`, text: row.deltaLabel || deltaFallback(row.classification) })
    );
}

function renderSignalHalf(signal, side) {
    if (!signal) {
        return h('div', { className: `ae-ic-half ${side} is-empty` });
    }

    const point = xPct(signal.rr);
    const low = xPct(signal.rrL);
    const high = xPct(signal.rrH);

    return h('div', {
        className: `ae-ic-half ${side}${signal.sig ? '' : ' ns'}`,
        title: `RR ${formatRR(signal.rr)} [${formatRR(signal.rrL)}-${formatRR(signal.rrH)}]`
    },
        low !== null && high !== null
            ? [
                h('span', { className: 'ae-ic-ci', style: { left: `${Math.min(low, high)}%`, width: `${Math.abs(high - low)}%` } }),
                h('span', { className: 'ae-ic-ci-cap', style: { left: `${low}%` } }),
                h('span', { className: 'ae-ic-ci-cap', style: { left: `${high}%` } })
            ]
            : null,
        point !== null ? h('span', { className: 'ae-ic-point', style: { left: `${point}%` } }) : null
    );
}

function renderInterchangeAxis() {
    const ticks = [0.1, 0.5, 1, 2, 10];
    return h('div', { className: 'ae-ic-axis' },
        h('span'),
        h('div', { className: 'ae-ic-axis-ticks' },
            ticks.map(tick => h('span', {
                className: tick === 1 ? 'ref' : '',
                style: { left: `${xPct(tick)}%` },
                text: String(tick)
            }))
        ),
        h('span', { text: 'Difference' })
    );
}

function summaryCell(kind, count, label) {
    return h('div', { className: 'ae-ic-summary-cell' },
        h('strong', { className: kind, text: String(count) }),
        h('span', { text: label })
    );
}

function deltaFallback(classification) {
    return {
        'only-a': 'Only product A has this signal',
        'only-b': 'Only product B has this signal',
        'a-worse': 'Higher RR on product A',
        'b-worse': 'Higher RR on product B',
        similar: 'Similar AE profile'
    }[classification] || 'Similar AE profile';
}
