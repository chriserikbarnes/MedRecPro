/**************************************************************/
/**
 * DOM helpers for the adverse-event dashboard.
 *
 * @fileoverview Provides small, dependency-free helpers for safe node creation
 * and shared loading, empty, disabled, and error states.
 */
/**************************************************************/

/**************************************************************/
/**
 * Creates a DOM element and appends children safely.
 *
 * @param {string} tag Element tag name.
 * @param {Object} props Element properties, attributes, events, and dataset.
 * @param {...any} children Child nodes or primitive text values.
 * @returns {HTMLElement} Created element.
 */
/**************************************************************/
export function h(tag, props = {}, ...children) {
    const element = document.createElement(tag);

    Object.entries(props || {}).forEach(([key, value]) => {
        if (value === null || value === undefined || value === false) {
            return;
        }

        if (key === 'className') {
            element.className = value;
        } else if (key === 'text') {
            element.textContent = value;
        } else if (key === 'html') {
            element.innerHTML = value;
        } else if (key === 'dataset') {
            Object.entries(value).forEach(([dataKey, dataValue]) => {
                if (dataValue !== null && dataValue !== undefined) {
                    element.dataset[dataKey] = String(dataValue);
                }
            });
        } else if (key === 'style' && typeof value === 'object') {
            Object.assign(element.style, value);
        } else if (key.startsWith('on') && typeof value === 'function') {
            element.addEventListener(key.slice(2).toLowerCase(), value);
        } else if (key === 'disabled') {
            element.disabled = Boolean(value);
        } else if (key === 'checked') {
            element.checked = Boolean(value);
        } else if (key === 'value') {
            element.value = value;
        } else {
            element.setAttribute(key, String(value));
        }
    });

    appendChildren(element, children);
    return element;
}

/**************************************************************/
/**
 * Replaces a node's content.
 *
 * @param {HTMLElement} element Element to update.
 * @param {...any} children Replacement children.
 */
/**************************************************************/
export function setChildren(element, ...children) {
    element.replaceChildren();
    appendChildren(element, children);
}

/**************************************************************/
/**
 * Removes all child nodes from an element.
 *
 * @param {HTMLElement} element Element to clear.
 */
/**************************************************************/
export function clear(element) {
    element.replaceChildren();
}

/**************************************************************/
/**
 * Creates an SVG icon from a small local icon set.
 *
 * @param {string} name Icon name.
 * @param {string} className Optional CSS class.
 * @returns {HTMLElement} Icon span containing SVG markup.
 */
/**************************************************************/
export function icon(name, className = 'ae-icon') {
    const icons = {
        bookmark: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z"/></svg>',
        download: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>',
        search: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/></svg>',
        star: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"><path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z"/></svg>',
        starFilled: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2l3.09 6.26 6.91 1-5 4.87 1.18 6.88L12 17.77 5.82 21l1.18-6.88-5-4.87 6.91-1L12 2z"/></svg>',
        chevron: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>',
        close: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>'
    };

    return h('span', { className, html: icons[name] || '' });
}

/**************************************************************/
/**
 * Renders a loading state.
 *
 * @param {string} label Loading label.
 * @returns {HTMLElement} Loading element.
 */
/**************************************************************/
export function renderLoading(label = 'Loading dashboard data') {
    return h('div', { className: 'ae-state ae-state-loading' },
        h('span', { className: 'ae-spinner', 'aria-hidden': 'true' }),
        h('span', { text: label })
    );
}

/**************************************************************/
/**
 * Renders an empty state.
 *
 * @param {string} title Empty-state title.
 * @param {string} body Empty-state body.
 * @returns {HTMLElement} Empty-state element.
 */
/**************************************************************/
export function renderEmptyState(title, body) {
    return h('div', { className: 'ae-state ae-state-empty' },
        h('strong', { text: title }),
        h('span', { text: body })
    );
}

/**************************************************************/
/**
 * Renders the dashboard feature-disabled state.
 *
 * @returns {HTMLElement} Disabled-state element.
 */
/**************************************************************/
export function renderDisabledFeatureState() {
    return h('div', { className: 'ae-disabled-state' },
        h('img', { src: '/favicon.svg', alt: 'MedRecPro', className: 'ae-disabled-logo' }),
        h('h1', { text: 'Adverse events dashboard is disabled' }),
        h('p', { text: 'The API feature flag returned 503, so this dashboard cannot load live AE data right now.' })
    );
}

/**************************************************************/
/**
 * Renders an inline API error with an optional retry action.
 *
 * @param {Error} error Error to display.
 * @param {Function} retryCallback Optional retry callback.
 * @returns {HTMLElement} Error-state element.
 */
/**************************************************************/
export function renderInlineError(error, retryCallback = null) {
    const status = error && Number.isFinite(error.status) ? `HTTP ${error.status}. ` : '';
    const element = h('div', { className: 'ae-state ae-state-error' },
        h('strong', { text: 'Unable to load this section' }),
        h('span', { text: `${status}${error?.message || 'Please try again.'}` })
    );

    if (retryCallback) {
        element.appendChild(h('button', {
            type: 'button',
            className: 'ae-link-button',
            onClick: retryCallback,
            text: 'Retry'
        }));
    }

    return element;
}

/**************************************************************/
/**
 * Appends a flattened list of child values to an element.
 *
 * @param {HTMLElement} element Parent element.
 * @param {Array} children Children to append.
 */
/**************************************************************/
function appendChildren(element, children) {
    children.flat(Infinity).forEach((child) => {
        if (child === null || child === undefined || child === false) {
            return;
        }

        if (child instanceof Node) {
            element.appendChild(child);
        } else {
            element.appendChild(document.createTextNode(String(child)));
        }
    });
}
