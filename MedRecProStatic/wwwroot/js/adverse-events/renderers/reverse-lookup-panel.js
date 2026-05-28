/**************************************************************/
/**
 * Reverse lookup panel renderer.
 */
/**************************************************************/

import { h, renderEmptyState, renderInlineError, renderLoading } from '../dom.js';
import { renderNumberNeededBox, renderPrecisionPill, renderSignalMeta } from './triage-view.js';

/**************************************************************/
/**
 * Renders the reverse lookup panel.
 *
 * @param {HTMLElement} container Host element.
 * @param {Object} options Rendering options and callbacks.
 */
/**************************************************************/
export function renderReverseLookupPanel(container, options) {
    const state = options.state;
    const suggestions = Array.from(state.suggestions || []).sort((a, b) => a.localeCompare(b)).slice(0, 12);
    const scopeProducts = options.getScopeProducts();

    const input = h('input', {
        className: 'ae-search-input',
        type: 'text',
        value: state.symptom || '',
        list: 'aeSymptomSuggestions',
        placeholder: 'Enter exact adverse-event term, such as Headache',
        onInput: event => options.onSymptomInput(event.target.value)
    });

    const form = h('form', {
        className: 'ae-reverse-form',
        onSubmit: event => {
            event.preventDefault();
            options.onSubmit();
        }
    },
        h('div', { className: 'ae-search-wrap' }, input),
        h('button', { type: 'submit', className: 'ae-button primary', text: 'Lookup' })
    );

    const datalist = h('datalist', { id: 'aeSymptomSuggestions' },
        suggestions.map(term => h('option', { value: term }))
    );

    container.replaceChildren(h('section', { className: 'ae-panel', 'data-screen-label': 'Reverse lookup' },
        h('header', { className: 'ae-panel-header' },
            h('span', {},
                h('h2', { text: 'Symptom to drug reverse lookup' }),
                h('p', { text: 'Submit an exact adverse-event term. Suggestions come from already loaded live signal rows.' })
            )
        ),
        form,
        datalist,
        suggestions.length
            ? h('div', { className: 'ae-filter-row' },
                h('span', { className: 'ae-filter-label', text: 'Suggestions' }),
                suggestions.map(term => h('button', {
                    type: 'button',
                    className: `ae-chip${term === state.symptom ? ' active' : ''}`,
                    onClick: () => {
                        options.onSymptomInput(term);
                        options.onSubmit();
                    },
                    text: term
                }))
            )
            : h('p', { className: 'ae-help-text', text: 'No live suggestions are loaded yet. Type the full AE term exactly as it appears in a label.' }),
        scopeProducts.length
            ? h('div', { className: 'ae-filter-row' },
                h('span', { className: 'ae-filter-label', text: 'Scope' }),
                scopeProducts.map(product => h('button', {
                    type: 'button',
                    className: `ae-chip${state.scopeDocumentGuids.includes(product.documentGuid) ? ' active' : ''}`,
                    onClick: () => options.onToggleScope(product.documentGuid),
                    text: product.name
                }))
            )
            : null,
        renderReverseResult(options)
    ));
}

function renderReverseResult(options) {
    if (options.loading) {
        return renderLoading('Looking up adverse-event matches');
    }

    if (options.error) {
        return renderInlineError(options.error, options.onSubmit);
    }

    const result = options.state.result;
    if (!result) {
        return renderEmptyState('Reverse lookup is ready.', 'Choose a loaded suggestion or enter an exact AE term to search across dashboard products.');
    }

    if (result.matches.length === 0) {
        return renderEmptyState('No matching products found.', `No dashboard rows matched "${result.symptom}". The lookup is exact-term matching.`);
    }

    return h('div', { className: 'ae-rl-results' },
        result.allReassuring
            ? h('div', { className: 'ae-rl-banner' },
                h('strong', { text: 'No significantly elevated match.' }),
                h('span', { text: ' Consider non-drug causes after checking the listed evidence.' })
            )
            : null,
        result.matches.map(match => renderReverseRow(match))
    );
}

function renderReverseRow(match) {
    const signal = match.signal;
    const drug = match.drug;
    const verdict = verdictLabel(match.verdict);

    return h('article', { className: `ae-rl-row ${signal?.prec === 'fragile' ? 'is-fragile' : ''}` },
        h('div', { className: 'ae-rl-drug' },
            h('strong', { text: drug?.name || 'Unknown product' }),
            h('small', { text: drug?.pharmClass || '' })
        ),
        signal ? renderNumberNeededBox(signal) : null,
        h('div', { className: 'ae-rl-meta' },
            h('span', { className: `ae-verdict ${match.verdict}`, text: verdict }),
            signal ? renderSignalMeta(signal) : null,
            signal ? renderPrecisionPill(signal.prec) : null
        )
    );
}

function verdictLabel(verdict) {
    return {
        'plausibly-causal': 'Plausibly causal',
        protective: 'Protective',
        'not-significantly-elevated': 'Not significantly elevated',
        'low-confidence': 'Low-confidence signal'
    }[verdict] || 'Not significantly elevated';
}
