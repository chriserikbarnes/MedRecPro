/**************************************************************/
/**
 * Counseling-priority triage renderer.
 */
/**************************************************************/

import { h, renderEmptyState } from '../dom.js';
import { formatEventRate, formatNumberNeeded, formatRR } from '../formatters.js';

const FLAG_TEXT = {
    ZERO_CELL_CORRECTED: 'Zero events in one arm; Haldane correction was applied.',
    SOC_REMAP: 'The System Organ Class was remapped during Stage 5 processing.',
    WIDE_CI: 'The confidence interval is wide; treat the point estimate cautiously.',
    LOW_EVENT_COUNT: 'Fewer than 10 total events; the estimate is unstable.'
};

/**************************************************************/
/**
 * Renders the triage view from API-provided tiers.
 *
 * @param {Object} triage Triage view model.
 * @param {Set<string>} expandedSignals Expanded signal IDs.
 * @param {Function} onToggleExpanded Expansion callback.
 * @returns {HTMLElement} Triage view element.
 */
/**************************************************************/
export function renderTriageView(triage, expandedSignals, onToggleExpanded) {
    const tiers = (triage?.tiers || []).filter(tier => tier.signals.length > 0);

    if (tiers.length === 0) {
        return renderEmptyState('No actionable AE signals found.', 'Try a different comparator filter or include fragile rows.');
    }

    return h('div', { className: 'ae-triage-view' },
        tiers.map(tier => h('section', { className: `ae-tier ae-tier-${tier.id}` },
            h('header', { className: 'ae-tier-header' },
                h('span', { className: 'ae-tier-marker' }),
                h('span', { className: 'ae-tier-copy' },
                    h('strong', { text: tier.name }),
                    h('small', { text: tier.description })
                ),
                h('span', { className: 'ae-tier-count', text: String(tier.signals.length) })
            ),
            tier.signals.map(signal => renderSignalRow(
                signal,
                expandedSignals.has(signal.id),
                () => onToggleExpanded(signal.id)
            ))
        ))
    );
}

/**************************************************************/
/**
 * Renders a reusable signal row.
 *
 * @param {Object} signal Signal view model.
 * @param {boolean} expanded Whether details are expanded.
 * @param {Function} onClick Toggle callback.
 * @returns {HTMLElement} Signal row element.
 */
/**************************************************************/
export function renderSignalRow(signal, expanded = false, onClick = null) {
    const row = h('article', {
        className: [
            'ae-signal-row',
            signal.prec === 'fragile' ? 'is-fragile' : '',
            expanded ? 'is-expanded' : ''
        ].filter(Boolean).join(' '),
        onClick: onClick || undefined
    },
        renderNumberNeededBox(signal),
        h('div', { className: 'ae-signal-body' },
            h('strong', { className: 'ae-signal-name', text: signal.name }),
            renderSignalMeta(signal)
        ),
        h('div', { className: 'ae-signal-right' },
            renderPrecisionPill(signal.prec),
            h('span', { className: 'ae-expand-label', text: `${expanded ? '-' : '+'} details` })
        )
    );

    if (expanded) {
        row.appendChild(renderSignalDetails(signal));
    }

    return row;
}

/**************************************************************/
/**
 * Renders signal metadata chips.
 *
 * @param {Object} signal Signal view model.
 * @returns {HTMLElement} Metadata element.
 */
/**************************************************************/
export function renderSignalMeta(signal) {
    const chips = [
        h('span', { className: 'ae-tag soc', text: signal.soc }),
        h('span', { className: 'ae-tag rr', text: `RR ${formatRR(signal.rr)} [${formatRR(signal.rrL)}-${formatRR(signal.rrH)}]` })
    ];

    if (signal.sig && isSeriousSoc(signal.soc)) {
        chips.push(h('span', { className: 'ae-tag serious', text: 'Serious SOC' }));
    }

    if (!signal.isPlac) {
        chips.push(h('span', { className: 'ae-tag', text: 'vs active comparator' }));
    }

    if (signal.combo) {
        chips.push(h('span', { className: 'ae-tag combo', text: 'Combination product' }));
    }

    (signal.flags || []).forEach(flag => chips.push(h('span', { className: 'ae-tag flag', text: flag })));

    return h('div', { className: 'ae-signal-meta' }, chips);
}

/**************************************************************/
/**
 * Renders the NNH/NNT display block.
 *
 * @param {Object} signal Signal view model.
 * @returns {HTMLElement} Number-needed element.
 */
/**************************************************************/
export function renderNumberNeededBox(signal) {
    const isBenefit = signal.type === 'NNT';
    const value = isBenefit ? signal.nnt : signal.nnh;
    const lower = isBenefit ? signal.nntL : signal.nnhL;
    const upper = isBenefit ? signal.nntH : signal.nnhH;

    if (value === null || value === undefined) {
        return h('div', { className: 'ae-number-needed is-empty' },
            h('span', { text: isBenefit ? 'No NNT' : 'No NNH' }),
            h('small', { text: 'not significant' })
        );
    }

    return h('div', { className: `ae-number-needed${isBenefit ? ' is-benefit' : ''}` },
        h('span', { className: 'ae-number-label', text: isBenefit ? 'NNT - benefit 1 in' : 'NNH - harm 1 in' }),
        h('strong', {}, h('span', { text: '~' }), formatNumberNeeded(value)),
        h('small', { text: `[${formatNumberNeeded(lower)} - ${formatNumberNeeded(upper)}]` })
    );
}

/**************************************************************/
/**
 * Renders a precision pill.
 *
 * @param {string} precision Precision class.
 * @returns {HTMLElement} Pill element.
 */
/**************************************************************/
export function renderPrecisionPill(precision) {
    return h('span', { className: `ae-precision-pill ${precision || 'tight'}` },
        h('span', { className: 'ae-pip' }),
        h('span', { text: (precision || 'tight').toUpperCase() })
    );
}

function renderSignalDetails(signal) {
    return h('div', { className: 'ae-signal-detail' },
        detailCell('Treatment events', formatEventRate(signal.eT, signal.armN)),
        detailCell('Comparator events', formatEventRate(signal.eC, signal.comparatorN || signal.armN)),
        detailCell('Risk type', signal.prot ? 'Protective' : signal.sig ? 'Elevated' : 'Not significant'),
        detailCell('Comparator', signal.isPlac ? 'Placebo' : 'Active'),
        signal.studyContext ? detailCell('Study context', signal.studyContext) : null,
        signal.population ? detailCell('Population', signal.population) : null,
        signal.flags?.length
            ? h('div', { className: 'ae-detail-cell wide' },
                h('span', { className: 'lbl', text: 'Low-confidence notes' }),
                h('span', { className: 'val', text: signal.flags.map(flag => FLAG_TEXT[flag] || flag).join(' ') })
            )
            : null
    );
}

function detailCell(label, value) {
    return h('div', { className: 'ae-detail-cell' },
        h('span', { className: 'lbl', text: label }),
        h('span', { className: 'val', text: value })
    );
}

function isSeriousSoc(soc) {
    return new Set([
        'cardiac',
        'hepatobiliary',
        'renal & urinary',
        'blood & lymphatic',
        'immune system',
        'vascular',
        'neoplasms'
    ]).has(String(soc || '').toLowerCase());
}
