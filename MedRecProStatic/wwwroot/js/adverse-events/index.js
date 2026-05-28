/**************************************************************/
/**
 * Adverse-event dashboard browser application.
 */
/**************************************************************/

import {
    getFavoriteProducts,
    getForest,
    getInterchange,
    getProducts,
    getQuadrant,
    getTriage,
    reverseLookup,
    setFavoriteProduct
} from './api-client.js';
import { h, icon, renderDisabledFeatureState, renderEmptyState, renderInlineError, renderLoading, setChildren } from './dom.js';
import { formatComparatorLabel, formatInteger } from './formatters.js';
import { dashboardState, interchangeCacheKey, productViewCacheKey, reverseCacheKey } from './state.js';
import { loadRecents, pushRecent } from './storage.js';
import { renderForestView } from './renderers/forest-view.js';
import { renderInterchangePanel } from './renderers/interchange-panel.js';
import { renderKpiStrip } from './renderers/kpi-strip.js';
import { renderProductHeader } from './renderers/product-picker.js';
import { renderQuadrantView } from './renderers/quadrant-view.js';
import { renderReverseLookupPanel } from './renderers/reverse-lookup-panel.js';
import { renderTriageView } from './renderers/triage-view.js';

const root = document.getElementById('aeDashboardApp');
const elements = {};

const VIEW_TEXT = {
    triage: {
        title: 'Counseling priority',
        subtitle: 'Adverse events sorted into action tiers - most likely harm first.'
    },
    forest: {
        title: 'Forest plot',
        subtitle: 'Relative risk with confidence intervals on a log scale.'
    },
    quadrant: {
        title: 'Risk-vs-precision quadrant',
        subtitle: 'Effect magnitude on the y-axis, estimate precision on the x-axis.'
    }
};

initialize();

/**************************************************************/
/**
 * Initializes the dashboard.
 */
/**************************************************************/
async function initialize() {
    parseUrlState();
    dashboardState.recents = loadRecents();
    renderLayout();
    renderAll();

    try {
        await loadProductCatalog('');
        await loadFavorites();
        await selectInitialProduct();
    } catch (error) {
        if (error?.isFeatureDisabled) {
            dashboardState.featureDisabled = true;
        } else {
            dashboardState.errors.products = error;
        }
        renderAll();
    }

    window.addEventListener('popstate', async () => {
        parseUrlState();
        await selectProduct(dashboardState.selectedDocumentGuid, false);
    });
}

/**************************************************************/
/**
 * Renders the static dashboard layout containers.
 */
/**************************************************************/
function renderLayout() {
    root.replaceChildren(
        h('div', { className: 'ae-dashboard' },
            renderTopBar(),
            h('main', { className: 'ae-main' },
                h('section', { id: 'aeProductHeader' }),
                h('section', { id: 'aeKpiStrip' }),
                h('section', { id: 'aePrimaryPanel' }),
                h('div', { className: 'ae-section-heading' },
                    h('span', { text: 'Cross-product tools' }),
                    h('span', { className: 'line' })
                ),
                h('section', { id: 'aeReverseLookup' }),
                h('section', { id: 'aeInterchange' }),
                h('p', { className: 'ae-foot-note' },
                    'Data shown comes from the live adverse-event dashboard API. ',
                    h('code', { text: 'DocumentGUID' }),
                    ' is used as the product key; encrypted source row IDs stay out of URLs.'
                )
            ),
            h('div', { id: 'aeToast', className: 'ae-toast', role: 'status', 'aria-live': 'polite' })
        )
    );

    elements.productHeader = document.getElementById('aeProductHeader');
    elements.kpiStrip = document.getElementById('aeKpiStrip');
    elements.primaryPanel = document.getElementById('aePrimaryPanel');
    elements.reverseLookup = document.getElementById('aeReverseLookup');
    elements.interchange = document.getElementById('aeInterchange');
    elements.toast = document.getElementById('aeToast');
}

/**************************************************************/
/**
 * Renders all dashboard sections from state.
 */
/**************************************************************/
function renderAll() {
    if (dashboardState.featureDisabled) {
        root.replaceChildren(renderDisabledFeatureState());
        return;
    }

    const selectedProduct = getSelectedProduct();
    renderProductHeader(elements.productHeader, productPickerOptions({ product: selectedProduct }));
    renderKpiStrip(elements.kpiStrip, selectedProduct);
    renderPrimaryPanel();
    renderReverse();
    renderInterchange();
}

function renderTopBar() {
    return h('header', { className: 'ae-topbar' },
        h('div', { className: 'ae-topbar-inner' },
            h('a', { className: 'ae-brand', href: '/' },
                h('span', { className: 'ae-brand-logo' }, h('img', { src: '/favicon.svg', alt: 'MedRecPro' })),
                h('span', { text: 'MedRecPro' })
            ),
            h('span', { className: 'ae-brand-sep' }),
            h('span', { className: 'ae-brand-sub', text: 'Adverse Events' }),
            h('span', { className: 'ae-topbar-spacer' }),
            h('button', { type: 'button', className: 'ae-topbar-action', title: 'Save view URL', onClick: saveCurrentUrl },
                icon('bookmark'),
                h('span', { text: 'Save' })
            ),
            h('button', { type: 'button', className: 'ae-topbar-action', title: 'Export loaded dashboard JSON', onClick: exportLoadedData },
                icon('download'),
                h('span', { text: 'Export' })
            )
        )
    );
}

function renderPrimaryPanel() {
    const viewText = VIEW_TEXT[dashboardState.activeView] || VIEW_TEXT.triage;
    const selectedProduct = getSelectedProduct();
    const payload = currentViewPayload();
    const signals = currentSignals(payload);
    const allCount = selectedProduct?.rowCount || signals.length || 0;
    const placeboCount = signals.filter(signal => signal.isPlac).length;
    const activeCount = signals.filter(signal => !signal.isPlac).length;
    const fragileCount = signals.filter(signal => signal.prec === 'fragile').length;

    const viewBody = h('div', { className: 'ae-view-body' });
    if (!selectedProduct && !dashboardState.selectedDocumentGuid) {
        viewBody.appendChild(renderEmptyState('No product selected.', 'Choose a dashboard-ready product from the picker.'));
    } else if (dashboardState.loading.view) {
        viewBody.appendChild(renderLoading(`Loading ${formatComparatorLabel(dashboardState.comparator).toLowerCase()} ${dashboardState.activeView} data`));
    } else if (dashboardState.errors.view) {
        viewBody.appendChild(renderInlineError(dashboardState.errors.view, () => loadActiveView(true)));
    } else {
        viewBody.appendChild(renderCurrentView(payload));
    }

    elements.primaryPanel.replaceChildren(h('section', { className: 'ae-panel', 'data-screen-label': 'AE primary view' },
        h('header', { className: 'ae-panel-header' },
            h('span', {},
                h('h2', { text: viewText.title }),
                h('p', { text: viewText.subtitle })
            ),
            h('div', { className: 'ae-tabs', role: 'tablist' },
                viewButton('triage', 'Triage'),
                viewButton('forest', 'Forest'),
                viewButton('quadrant', 'Quadrant')
            )
        ),
        h('div', { className: 'ae-filter-row' },
            h('span', { className: 'ae-filter-label', text: 'Comparator' }),
            comparatorButton('all', `All (${formatInteger(allCount)})`),
            comparatorButton('placebo', placeboCount ? `Placebo (${formatInteger(placeboCount)})` : 'Placebo'),
            comparatorButton('active', activeCount ? `Active comparator (${formatInteger(activeCount)})` : 'Active comparator'),
            h('button', {
                type: 'button',
                className: `ae-chip-toggle${dashboardState.includeFragile ? ' on' : ''}`,
                onClick: async () => {
                    dashboardState.includeFragile = !dashboardState.includeFragile;
                    updateUrl(false);
                    await loadActiveView(true);
                }
            },
                h('span', { className: 'ae-switch' }),
                h('span', { text: `Show fragile rows (${formatInteger(fragileCount)})` })
            )
        ),
        viewBody
    ));
}

function renderCurrentView(payload) {
    if (dashboardState.activeView === 'forest') {
        return renderForestView(payload);
    }

    if (dashboardState.activeView === 'quadrant') {
        return renderQuadrantView(payload);
    }

    return renderTriageView(payload, dashboardState.expandedSignals, (signalId) => {
        if (dashboardState.expandedSignals.has(signalId)) {
            dashboardState.expandedSignals.delete(signalId);
        } else {
            dashboardState.expandedSignals.add(signalId);
        }
        renderPrimaryPanel();
    });
}

function renderReverse() {
    renderReverseLookupPanel(elements.reverseLookup, {
        state: dashboardState.reverseLookup,
        loading: dashboardState.loading.reverseLookup,
        error: dashboardState.errors.reverseLookup,
        getScopeProducts,
        onSymptomInput: (value) => {
            dashboardState.reverseLookup.symptom = value;
        },
        onToggleScope: (documentGuid) => {
            const scope = dashboardState.reverseLookup.scopeDocumentGuids;
            dashboardState.reverseLookup.scopeDocumentGuids = scope.includes(documentGuid)
                ? scope.filter(guid => guid !== documentGuid)
                : [...scope, documentGuid];
            renderReverse();
        },
        onSubmit: runReverseLookup
    });
}

function renderInterchange() {
    renderInterchangePanel(elements.interchange, {
        state: dashboardState.interchange,
        loading: dashboardState.loading.interchange,
        error: dashboardState.errors.interchange,
        getProduct: documentGuid => dashboardState.productsByGuid.get(documentGuid) || null,
        productPickerOptions: productPickerOptions(),
        onPickA: async (documentGuid) => {
            if (documentGuid === dashboardState.interchange.documentGuidB) {
                return;
            }
            dashboardState.interchange.documentGuidA = documentGuid;
            await runInterchange();
        },
        onPickB: async (documentGuid) => {
            if (documentGuid === dashboardState.interchange.documentGuidA) {
                return;
            }
            dashboardState.interchange.documentGuidB = documentGuid;
            await runInterchange();
        },
        onToggleDifferences: async () => {
            dashboardState.interchange.differencesOnly = !dashboardState.interchange.differencesOnly;
            await runInterchange();
        },
        onReload: () => runInterchange(true)
    });
}

/**************************************************************/
/**
 * Loads the product catalog.
 */
/**************************************************************/
async function loadProductCatalog(search) {
    abortRequest('products');
    const controller = new AbortController();
    dashboardState.activeRequests.products = controller;
    dashboardState.loading.products = true;
    dashboardState.errors.products = null;

    const products = await getProducts({
        productSearch: search,
        pageNumber: 1,
        pageSize: 50,
        signal: controller.signal
    });

    dashboardState.products = products;
    mergeProducts(products);
    dashboardState.loading.products = false;
    return products;
}

async function loadFavorites() {
    try {
        const favorites = await getFavoriteProducts();
        mergeProducts(favorites);
        dashboardState.favorites = new Set(favorites.map(product => product.documentGuid));
        dashboardState.favoriteAccessDenied = false;
    } catch (error) {
        if (error?.isAuthFailure) {
            dashboardState.favoriteAccessDenied = true;
        } else if (error?.isFeatureDisabled) {
            dashboardState.featureDisabled = true;
        } else {
            console.warn('[AE Dashboard] Favorite hydration failed.', error);
        }
    }
}

async function selectInitialProduct() {
    const initialGuid = dashboardState.selectedDocumentGuid || dashboardState.products[0]?.documentGuid;
    if (!initialGuid) {
        renderAll();
        return;
    }

    if (!dashboardState.interchange.documentGuidA) {
        dashboardState.interchange.documentGuidA = initialGuid;
    }

    if (!dashboardState.interchange.documentGuidB) {
        const fallbackB = dashboardState.products.find(product => product.documentGuid !== initialGuid);
        dashboardState.interchange.documentGuidB = fallbackB?.documentGuid || null;
    }

    await selectProduct(initialGuid, false);
    await runInterchange();
}

async function selectProduct(documentGuid, pushUrl = true) {
    if (!documentGuid) {
        return;
    }

    dashboardState.selectedDocumentGuid = documentGuid;
    dashboardState.expandedSignals.clear();
    if (pushUrl) {
        updateUrl(true);
    }

    const knownProduct = dashboardState.productsByGuid.get(documentGuid);
    if (knownProduct) {
        dashboardState.recents = pushRecent(dashboardState.recents, knownProduct);
    }

    renderAll();
    await loadView('triage', false);

    const selectedProduct = getSelectedProduct();
    if (selectedProduct) {
        dashboardState.recents = pushRecent(dashboardState.recents, selectedProduct);
    }

    if (dashboardState.activeView !== 'triage') {
        await loadActiveView(false);
    } else {
        renderAll();
    }
}

async function loadActiveView(force = false) {
    await loadView(dashboardState.activeView, force);
}

async function loadView(view, force = false) {
    const documentGuid = dashboardState.selectedDocumentGuid;
    if (!documentGuid) {
        return;
    }

    const key = productViewCacheKey(view, documentGuid, dashboardState.comparator, dashboardState.includeFragile);
    if (!force && dashboardState.viewCache.has(key)) {
        dashboardState.errors.view = null;
        renderAll();
        return dashboardState.viewCache.get(key);
    }

    abortRequest('view');
    const controller = new AbortController();
    dashboardState.activeRequests.view = controller;
    dashboardState.loading.view = true;
    dashboardState.errors.view = null;
    renderAll();

    try {
        const payload = await fetchView(view, documentGuid, controller.signal);
        dashboardState.viewCache.set(key, payload);
        if (payload?.product) {
            mergeProducts([payload.product]);
            dashboardState.recents = pushRecent(dashboardState.recents, payload.product);
        }
        addSuggestionsFromPayload(payload);
        dashboardState.loading.view = false;
        dashboardState.errors.view = null;
        renderAll();
        return payload;
    } catch (error) {
        if (error.name === 'AbortError') {
            return null;
        }

        dashboardState.loading.view = false;
        if (error?.isFeatureDisabled) {
            dashboardState.featureDisabled = true;
        } else if (error?.status === 404) {
            dashboardState.selectedDocumentGuid = null;
            dashboardState.errors.view = error;
        } else {
            dashboardState.errors.view = error;
        }
        renderAll();
        return null;
    }
}

function fetchView(view, documentGuid, signal) {
    if (view === 'forest') {
        return getForest(documentGuid, dashboardState.comparator, dashboardState.includeFragile, signal);
    }

    if (view === 'quadrant') {
        return getQuadrant(documentGuid, dashboardState.comparator, dashboardState.includeFragile, signal);
    }

    return getTriage(documentGuid, dashboardState.comparator, dashboardState.includeFragile, signal);
}

async function runReverseLookup() {
    const symptom = dashboardState.reverseLookup.symptom.trim();
    if (!symptom) {
        showToast('Enter an exact adverse-event term first.');
        return;
    }

    const scope = dashboardState.reverseLookup.scopeDocumentGuids;
    const key = reverseCacheKey(symptom, scope);
    updateUrl(false);
    if (dashboardState.viewCache.has(key)) {
        dashboardState.reverseLookup.result = dashboardState.viewCache.get(key);
        renderReverse();
        return;
    }

    dashboardState.loading.reverseLookup = true;
    dashboardState.errors.reverseLookup = null;
    renderReverse();

    try {
        const result = await reverseLookup(symptom, scope);
        dashboardState.reverseLookup.result = result;
        dashboardState.viewCache.set(key, result);
        addSuggestionsFromPayload(result);
        mergeProducts(result.matches.map(match => match.drug).filter(Boolean));
        dashboardState.loading.reverseLookup = false;
        renderReverse();
    } catch (error) {
        dashboardState.loading.reverseLookup = false;
        dashboardState.errors.reverseLookup = error;
        renderReverse();
    }
}

async function runInterchange(force = false) {
    const state = dashboardState.interchange;
    if (!state.documentGuidA || !state.documentGuidB || state.documentGuidA === state.documentGuidB) {
        renderInterchange();
        return;
    }

    const key = interchangeCacheKey(state.documentGuidA, state.documentGuidB, state.differencesOnly);
    if (!force && dashboardState.viewCache.has(key)) {
        state.result = dashboardState.viewCache.get(key);
        renderInterchange();
        return;
    }

    dashboardState.loading.interchange = true;
    dashboardState.errors.interchange = null;
    renderInterchange();

    try {
        const result = await getInterchange(state.documentGuidA, state.documentGuidB, state.differencesOnly);
        state.result = result;
        dashboardState.viewCache.set(key, result);
        mergeProducts([result.productA, result.productB].filter(Boolean));
        addSuggestionsFromPayload(result);
        dashboardState.loading.interchange = false;
        renderInterchange();
    } catch (error) {
        dashboardState.loading.interchange = false;
        dashboardState.errors.interchange = error;
        renderInterchange();
    }
}

function productPickerOptions(overrides = {}) {
    return {
        product: overrides.product,
        currentDocumentGuid: overrides.currentDocumentGuid || dashboardState.selectedDocumentGuid,
        getProducts: () => dashboardState.products,
        getFavoriteProducts: () => Array.from(dashboardState.favorites)
            .map(guid => dashboardState.productsByGuid.get(guid))
            .filter(Boolean),
        getRecents: () => dashboardState.recents,
        isFavorite: guid => dashboardState.favorites.has(guid),
        favoriteAccessDenied: dashboardState.favoriteAccessDenied,
        onSearch: async (query) => await loadProductCatalog(query),
        onSelect: async (documentGuid) => await selectProduct(documentGuid, true),
        onToggleFavorite: async (product) => await toggleFavorite(product),
        ...overrides
    };
}

async function toggleFavorite(product) {
    if (dashboardState.favoriteAccessDenied) {
        showToast('Sign in with API access to favorite products.');
        return;
    }

    const next = !dashboardState.favorites.has(product.documentGuid);
    try {
        await setFavoriteProduct(product.documentGuid, next);
        if (next) {
            dashboardState.favorites.add(product.documentGuid);
        } else {
            dashboardState.favorites.delete(product.documentGuid);
        }
        product.isFavorite = next;
        mergeProducts([product]);
        renderAll();
    } catch (error) {
        if (error?.isAuthFailure) {
            dashboardState.favoriteAccessDenied = true;
            showToast('Sign in with API access to favorite products.');
        } else {
            showToast(error?.message || 'Favorite update failed.');
        }
        renderAll();
    }
}

function currentViewPayload() {
    const key = productViewCacheKey(
        dashboardState.activeView,
        dashboardState.selectedDocumentGuid,
        dashboardState.comparator,
        dashboardState.includeFragile
    );
    return dashboardState.viewCache.get(key);
}

function currentSignals(payload) {
    if (!payload) {
        return [];
    }

    if (dashboardState.activeView === 'forest') {
        return payload.signals || [];
    }

    if (dashboardState.activeView === 'quadrant') {
        return (payload.points || []).map(point => point.signal).filter(Boolean);
    }

    return (payload.tiers || []).flatMap(tier => tier.signals || []);
}

function getSelectedProduct() {
    return dashboardState.productsByGuid.get(dashboardState.selectedDocumentGuid) || null;
}

function getScopeProducts() {
    const guids = [
        dashboardState.selectedDocumentGuid,
        dashboardState.interchange.documentGuidA,
        dashboardState.interchange.documentGuidB
    ].filter(Boolean);

    return Array.from(new Set(guids))
        .map(guid => dashboardState.productsByGuid.get(guid))
        .filter(Boolean);
}

function mergeProducts(products) {
    products.filter(Boolean).forEach(product => {
        const existing = dashboardState.productsByGuid.get(product.documentGuid) || {};
        const merged = { ...existing, ...product };
        merged.isFavorite = dashboardState.favorites.has(product.documentGuid) || product.isFavorite;
        dashboardState.productsByGuid.set(product.documentGuid, merged);
        if (merged.isFavorite) {
            dashboardState.favorites.add(product.documentGuid);
        }
    });
}

function addSuggestionsFromPayload(payload) {
    if (!payload) {
        return;
    }

    const addSignal = signal => {
        if (signal?.name) {
            dashboardState.reverseLookup.suggestions.add(signal.name);
        }
    };

    if (payload.tiers) {
        payload.tiers.forEach(tier => tier.signals.forEach(addSignal));
    }

    if (payload.signals) {
        payload.signals.forEach(addSignal);
    }

    if (payload.points) {
        payload.points.forEach(point => addSignal(point.signal));
    }

    if (payload.matches) {
        payload.matches.forEach(match => addSignal(match.signal));
    }

    if (payload.rows) {
        payload.rows.forEach(row => {
            addSignal(row.signalA);
            addSignal(row.signalB);
        });
    }
}

function viewButton(view, label) {
    return h('button', {
        type: 'button',
        role: 'tab',
        className: `ae-tab${dashboardState.activeView === view ? ' active' : ''}`,
        'aria-selected': String(dashboardState.activeView === view),
        onClick: async () => {
            dashboardState.activeView = view;
            updateUrl(false);
            await loadActiveView(false);
        },
        text: label
    });
}

function comparatorButton(comparator, label) {
    return h('button', {
        type: 'button',
        className: `ae-chip${dashboardState.comparator === comparator ? ' active' : ''}`,
        onClick: async () => {
            dashboardState.comparator = comparator;
            updateUrl(false);
            await loadActiveView(false);
        },
        text: label
    });
}

function parseUrlState() {
    const params = new URLSearchParams(window.location.search);
    const view = params.get('view');
    const comparator = params.get('comparator');
    const product = params.get('product');
    const fragile = params.get('fragile');
    const symptom = params.get('symptom');

    dashboardState.selectedDocumentGuid = product || dashboardState.selectedDocumentGuid;
    dashboardState.activeView = ['triage', 'forest', 'quadrant'].includes(view) ? view : dashboardState.activeView;
    dashboardState.comparator = ['all', 'placebo', 'active'].includes(comparator) ? comparator : dashboardState.comparator;
    dashboardState.includeFragile = fragile === null ? dashboardState.includeFragile : fragile !== 'false';
    dashboardState.reverseLookup.symptom = symptom || dashboardState.reverseLookup.symptom;
}

function updateUrl(push) {
    const params = new URLSearchParams();
    if (dashboardState.selectedDocumentGuid) {
        params.set('product', dashboardState.selectedDocumentGuid);
    }
    params.set('view', dashboardState.activeView);
    params.set('comparator', dashboardState.comparator);
    params.set('fragile', String(dashboardState.includeFragile));
    if (dashboardState.reverseLookup.symptom) {
        params.set('symptom', dashboardState.reverseLookup.symptom);
    }

    const url = `${window.location.pathname}?${params.toString()}`;
    if (push) {
        history.pushState({}, '', url);
    } else {
        history.replaceState({}, '', url);
    }
}

function abortRequest(name) {
    const controller = dashboardState.activeRequests[name];
    if (controller) {
        controller.abort();
    }
}

async function saveCurrentUrl() {
    try {
        await navigator.clipboard.writeText(window.location.href);
        showToast('View URL copied.');
    } catch (error) {
        showToast('Current view is reflected in the address bar.');
    }
}

function exportLoadedData() {
    const payload = {
        product: getSelectedProduct(),
        activeView: dashboardState.activeView,
        comparator: dashboardState.comparator,
        includeFragile: dashboardState.includeFragile,
        view: currentViewPayload(),
        reverseLookup: dashboardState.reverseLookup.result,
        interchange: dashboardState.interchange.result
    };

    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = h('a', {
        href: url,
        download: 'medrecpro-ae-dashboard-export.json'
    });
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

function showToast(message) {
    if (!elements.toast) {
        return;
    }

    setChildren(elements.toast, message);
    elements.toast.classList.add('is-visible');
    setTimeout(() => elements.toast?.classList.remove('is-visible'), 2600);
}
