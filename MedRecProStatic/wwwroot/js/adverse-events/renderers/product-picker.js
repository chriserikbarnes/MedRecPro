/**************************************************************/
/**
 * Product picker renderer for the AE dashboard.
 */
/**************************************************************/

import { h, icon } from '../dom.js';
import { formatInteger, formatPercent } from '../formatters.js';

let activePickerCleanup = null;

/**************************************************************/
/**
 * Renders the selected product header and product picker trigger.
 *
 * @param {HTMLElement} container Host element.
 * @param {Object} options Rendering options and callbacks.
 */
/**************************************************************/
export function renderProductHeader(container, options) {
    const product = options.product;

    container.replaceChildren(
        h('div', { className: 'ae-page-header' },
            h('div', { className: 'ae-crumbs' },
                h('span', { text: 'Inventory' }),
                h('span', { text: '/' }),
                h('span', { text: 'Per-product view' }),
                h('span', { text: '/' }),
                h('span', { text: 'Adverse events' })
            ),
            product
                ? renderLargePicker(product, options)
                : h('div', { className: 'ae-selection-required' },
                    h('h1', { text: 'Select an AE dashboard product' }),
                    renderLargePicker(null, options)
                ),
            product ? renderCoverageRow(product) : null
        )
    );
}

/**************************************************************/
/**
 * Renders a compact picker used by interchange controls.
 *
 * @param {Object} options Rendering options and callbacks.
 * @returns {HTMLElement} Compact picker node.
 */
/**************************************************************/
export function renderCompactPicker(options) {
    const product = options.product || null;
    const wrapper = h('div', { className: 'ae-compact-picker-wrap' });
    const button = h('button', {
        type: 'button',
        className: 'ae-compact-picker',
        style: options.accent ? { borderLeftColor: options.accent } : {},
        'aria-haspopup': 'listbox',
        'aria-expanded': 'false',
        onClick: () => openPicker(wrapper, { ...options, compact: true }, button)
    },
        h('span', { className: 'ae-compact-picker-text' },
            h('strong', { text: product?.name || options.placeholder || 'Select product' }),
            product ? h('small', { text: product.generic || product.pharmClass || '' }) : null
        ),
        icon('chevron', 'ae-picker-chev')
    );

    wrapper.appendChild(button);
    return wrapper;
}

/**************************************************************/
/**
 * Renders the KPI-adjacent product picker trigger.
 */
/**************************************************************/
function renderLargePicker(product, options) {
    const wrapper = h('div', { className: 'ae-drug-title-wrap' });
    const button = h('button', {
        type: 'button',
        className: 'ae-drug-title',
        'aria-haspopup': 'listbox',
        'aria-expanded': 'false',
        onClick: () => openPicker(wrapper, options, button)
    },
        h('span', { text: product?.name || 'Choose product' }),
        icon('chevron', 'ae-picker-chev')
    );

    wrapper.appendChild(button);

    if (product) {
        wrapper.appendChild(h('div', { className: 'ae-drug-meta' },
            h('span', { text: product.generic }),
            h('span', { text: product.pharmClass }),
            h('span', { text: product.moiety })
        ));
    }

    return wrapper;
}

/**************************************************************/
/**
 * Opens a searchable product picker panel.
 */
/**************************************************************/
function openPicker(wrapper, options, trigger) {
    if (activePickerCleanup) {
        activePickerCleanup();
    }

    let products = options.getProducts();
    let query = '';
    let activeIndex = 0;
    let searchTimer = null;
    const disabledGuids = new Set(options.disabledGuids || []);
    const panel = h('div', {
        className: `ae-product-picker${options.compact ? ' ae-product-picker-compact' : ''}`,
        role: 'listbox'
    });

    const input = h('input', {
        type: 'text',
        className: 'ae-picker-input',
        placeholder: 'Search by brand, generic, UNII, or class',
        autocomplete: 'off',
        spellcheck: 'false',
        onInput: () => {
            query = input.value;
            activeIndex = 0;
            clearTimeout(searchTimer);
            searchTimer = setTimeout(async () => {
                setPickerLoading(body, 'Searching products');
                try {
                    products = await options.onSearch(query);
                    renderBody();
                } catch (error) {
                    setPickerError(body, error);
                }
            }, 250);
        },
        onKeyDown: (event) => {
            const enabled = flatEnabledItems();
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                activeIndex = Math.min(activeIndex + 1, Math.max(0, enabled.length - 1));
                renderBody();
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                renderBody();
            } else if (event.key === 'Enter') {
                event.preventDefault();
                const selected = enabled[activeIndex];
                if (selected) {
                    commit(selected.documentGuid);
                }
            } else if (event.key === 'Escape') {
                close();
            }
        }
    });

    const body = h('div', { className: 'ae-picker-body' });
    const search = h('div', { className: 'ae-picker-search' }, icon('search'), input);
    panel.append(search, body, h('div', { className: 'ae-picker-foot' }, 'Up/Down move. Enter selects. Esc closes.'));
    wrapper.appendChild(panel);
    trigger?.setAttribute('aria-expanded', 'true');
    input.focus();
    input.select();
    renderBody();

    const onDocumentDown = (event) => {
        if (!wrapper.contains(event.target)) {
            close();
        }
    };

    document.addEventListener('mousedown', onDocumentDown);
    activePickerCleanup = close;

    function close() {
        clearTimeout(searchTimer);
        document.removeEventListener('mousedown', onDocumentDown);
        panel.remove();
        trigger?.setAttribute('aria-expanded', 'false');
        if (activePickerCleanup === close) {
            activePickerCleanup = null;
        }
    }

    function commit(documentGuid) {
        if (disabledGuids.has(documentGuid)) {
            return;
        }

        close();
        options.onSelect(documentGuid);
    }

    function flatEnabledItems() {
        return buildSections()
            .flatMap(section => section.items)
            .filter(item => !disabledGuids.has(item.documentGuid));
    }

    function renderBody() {
        const enabled = flatEnabledItems();
        const activeGuid = enabled[activeIndex]?.documentGuid || null;
        body.replaceChildren();

        const sections = buildSections();
        if (sections.length === 0 || sections.every(section => section.items.length === 0)) {
            body.appendChild(h('div', { className: 'ae-picker-empty' },
                query.trim() ? 'No products match this search.' : 'No dashboard-ready products found.'
            ));
            return;
        }

        sections.forEach(section => {
            if (section.items.length === 0) {
                return;
            }

            const sectionElement = h('div', { className: 'ae-picker-section' },
                h('div', { className: 'ae-picker-section-label', text: section.label })
            );

            section.items.forEach(product => {
                const isDisabled = disabledGuids.has(product.documentGuid);
                const isSelected = product.documentGuid === options.currentDocumentGuid;
                const isActive = product.documentGuid === activeGuid;
                const isFavorite = options.isFavorite(product.documentGuid);

                sectionElement.appendChild(h('div', {
                    className: [
                        'ae-picker-item',
                        isActive ? 'is-active' : '',
                        isSelected ? 'is-selected' : '',
                        isDisabled ? 'is-disabled' : ''
                    ].filter(Boolean).join(' '),
                    role: 'option',
                    'aria-selected': String(isSelected),
                    'aria-disabled': isDisabled ? 'true' : null,
                    onMouseEnter: () => {
                        const index = enabled.findIndex(item => item.documentGuid === product.documentGuid);
                        if (index >= 0) {
                            activeIndex = index;
                            renderBody();
                        }
                    },
                    onMouseDown: (event) => {
                        event.preventDefault();
                        if (!isDisabled) {
                            commit(product.documentGuid);
                        }
                    }
                },
                    h('span', { className: 'ae-picker-copy' },
                        h('strong', { text: product.name }),
                        h('small', { text: `${product.generic || 'Substance not listed'} - ${product.pharmClass || 'Class not listed'}` })
                    ),
                    h('span', { className: 'ae-picker-actions' },
                        product.score ? h('span', { className: 'ae-picker-score', text: `score ${product.score}` }) : null,
                        isDisabled ? h('span', { className: 'ae-picker-tag', text: 'in use' }) : null,
                        h('button', {
                            type: 'button',
                            className: `ae-picker-star${isFavorite ? ' is-on' : ''}`,
                            title: options.favoriteAccessDenied ? 'Sign in with API access to favorite products' : isFavorite ? 'Remove favorite' : 'Add favorite',
                            'aria-label': isFavorite ? 'Remove favorite' : 'Add favorite',
                            disabled: options.favoriteAccessDenied,
                            onMouseDown: async (event) => {
                                event.preventDefault();
                                event.stopPropagation();
                                await options.onToggleFavorite(product);
                                renderBody();
                            }
                        }, icon(isFavorite ? 'starFilled' : 'star', 'ae-star-icon'))
                    )
                ));
            });

            body.appendChild(sectionElement);
        });
    }

    function buildSections() {
        const byGuid = new Map(products.map(product => [product.documentGuid, product]));

        if (query.trim()) {
            return [{ label: `Results - ${products.length} loaded`, items: products }];
        }

        const favorites = options.getFavoriteProducts();
        const recents = options.getRecents()
            .map(recent => byGuid.get(recent.documentGuid) || recent)
            .filter(item => item && item.documentGuid);

        return [
            favorites.length ? { label: 'Favorites', items: favorites } : null,
            recents.length ? { label: `Recent - last ${recents.length}`, items: recents } : null,
            { label: `Browse - ${products.length} loaded`, items: products.slice(0, 30) }
        ].filter(Boolean);
    }
}

/**************************************************************/
/**
 * Renders product coverage badges.
 */
/**************************************************************/
function renderCoverageRow(product) {
    return h('div', { className: 'ae-coverage-row' },
        coverageBadge(product.placeboCoverage, 'Placebo-controlled'),
        coverageBadge(product.activeCoverage, 'Active comparator'),
        coverageBadge(product.doseCoverage > 0.3, `Dose data ${formatPercent(product.doseCoverage)}`),
        coverageBadge(true, `SOC breadth ${formatInteger(product.socBreadth)}/${formatInteger(product.socTotal)}`)
    );
}

function coverageBadge(isOn, label) {
    return h('span', { className: `ae-coverage-badge${isOn ? ' is-on' : ' is-off'}` },
        h('span', { className: 'ae-coverage-dot' }),
        h('span', { text: label })
    );
}

function setPickerLoading(body, label) {
    body.replaceChildren(h('div', { className: 'ae-picker-empty' }, label));
}

function setPickerError(body, error) {
    body.replaceChildren(h('div', { className: 'ae-picker-empty' }, error?.message || 'Search failed.'));
}
