/******** IMPORTANT : npm --prefix "..\MedRecProReact" run build *********/

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from './api/apiError';
import { AdverseEventClient } from './api/adverseEventClient';
import { ClassPageHeader } from './components/ClassPageHeader';
import { ClassPicker } from './components/ClassPicker';
import { FocusSwitch } from './components/FocusSwitch';
import { PageHeader } from './components/PageHeader';
import { CompactProductPicker, ProductPicker } from './components/ProductPicker';
import { SystemPageHeader } from './components/SystemPageHeader';
import { SystemPicker } from './components/SystemPicker';
import { DisabledFeature } from './components/common/DisabledFeature';
import { EmptyState } from './components/common/EmptyState';
import { InlineError } from './components/common/InlineError';
import { Loading } from './components/common/Loading';
import { ClassCorrelationSurface } from './components/correlation/ClassCorrelationSurface';
import { SystemCorrelationSurface } from './components/correlation/SystemCorrelationSurface';
import { useDebouncedValue } from './hooks/useDebouncedValue';
import { useFavorites } from './hooks/useFavorites';
import { useMediaQuery } from './hooks/useMediaQuery';
import { useProducts } from './hooks/useProducts';
import { useRecents } from './hooks/useRecents';
import {
    COMPACT_FOREST_TICKS,
    COMPACT_FOREST_TICKS_NARROW,
    DEFAULT_FOREST_TICKS,
    formatForestTick,
    getForestScaleDomain,
    getForestTicks,
    getForestXPercent,
    MAX_FOREST_TICKS,
} from './lib/forestScale';
import { formatDecimal, formatDose, formatInteger } from './lib/formatters';
import {
    mergeReverseLookupResults,
    normalizeCorrelationCellDetail,
    normalizeCorrelationClassPage,
    normalizeCorrelationHeatmap,
    normalizeCorrelationMap,
    normalizeCorrelationSystemPage,
    normalizeForest,
    normalizeInterchange,
    normalizeQuadrant,
    normalizeReverseLookup,
    normalizeSystemCorrelationCellDetail,
    normalizeSystemCorrelationHeatmap,
    normalizeSystemCorrelationMap,
    normalizeTriage,
} from './lib/normalizers';

// Supported dashboard views mirror the prototype tab names.
const DASHBOARD_VIEWS = new Set(['triage', 'forest', 'quadrant']);

// Supported comparator filters mirror the API enum through client-safe tokens.
const COMPARATOR_FILTERS = new Set(['all', 'placebo', 'active']);

// Supported top-level dashboard focuses preserve the existing product default.
const DASHBOARD_FOCUSES = new Set(['product', 'class', 'system']);

// Supported class-mode tabs.
const CLASS_DASHBOARD_VIEWS = new Set(['map', 'heatmap']);

// Supported system-mode tabs.
const SYSTEM_DASHBOARD_VIEWS = new Set(['map', 'heatmap']);

// Supported class-mode enum filters mirror the backend query values.
const CLASS_COMPARATORS = new Set(['Placebo', 'Active', 'Both']);
const CLASS_CORRELATION_METHODS = new Set(['Spearman', 'Pearson']);
const CLASS_CORRELATION_AGGREGATIONS = new Set(['MedianLogRr', 'MeanLogRr']);

const DEFAULT_SYSTEM_CLASS_PAGE_SIZE = 40;
const DEFAULT_SYSTEM_DRUG_PAGE_SIZE = 50;
const DEFAULT_SYSTEM_TERM_PAIR_PAGE_SIZE = 100;

// View copy is centralized so tabs and panel headings stay in sync.
const VIEW_COPY = {
    triage: {
        title: 'Counseling priority',
        subtitle: 'Adverse events sorted into action tiers, with the most actionable harm signals first.',
    },
    forest: {
        title: 'Forest plot',
        subtitle: 'Relative risk with confidence intervals on a log scale.',
    },
    quadrant: {
        title: 'Risk-vs-precision quadrant',
        subtitle: 'Effect magnitude on the y-axis and estimate precision on the x-axis.',
    },
};

// Known serious SOC values are display-only tags that match the server metadata.
const SERIOUS_SOC = new Set([
    'Cardiac',
    'Hepatobiliary',
    'Renal & Urinary',
    'Blood & Lymphatic',
    'Immune System',
    'Vascular',
    'Neoplasms',
]);

// Known flag text mirrors the API metadata while allowing unknown flags to render as-is.
const FLAG_TEXT = {
    ZeroCellCorrected: 'Zero events in one arm; Haldane 0.5 correction applied.',
    ZERO_CELL_CORRECTED: 'Zero events in one arm; Haldane 0.5 correction applied.',
    SocRemap: 'MedDRA System Organ Class was remapped by Stage 5 processing.',
    SOC_REMAP: 'MedDRA System Organ Class was remapped by Stage 5 processing.',
    WideCi: 'Confidence interval spans more than two orders of magnitude.',
    WIDE_CI: 'Confidence interval spans more than two orders of magnitude.',
    LowEventCount: 'Fewer than 10 total events.',
    LOW_EVENT_COUNT: 'Fewer than 10 total events.',
};

// Reverse-lookup verdict labels come from server enum values.
const REVERSE_LOOKUP_VERDICT_COPY = {
    plausiblycausal: 'Plausibly causal',
    protective: 'Protective',
    notsignificantlyelevated: 'Not significantly elevated',
    lowconfidence: 'Low confidence',
};

// Interchange groups keep API-derived classifications visually organized.
const INTERCHANGE_GROUPS = [
    {
        id: 'a-concern',
        label: 'Higher concern on product A',
        classes: new Set(['aworse', 'onlya']),
    },
    {
        id: 'b-concern',
        label: 'Higher concern on product B',
        classes: new Set(['bworse', 'onlyb']),
    },
    {
        id: 'similar',
        label: 'Similar or shared signal profile',
        classes: new Set(['similar']),
    },
];

// Empty interchange state keeps render paths simple before the first comparison.
const EMPTY_INTERCHANGE_VIEW = {
    productA: null,
    productB: null,
    rows: [],
    onlyACount: 0,
    onlyBCount: 0,
    similarCount: 0,
    aWorseCount: 0,
    bWorseCount: 0,
    classMismatchWarning: '',
    comparatorMismatchWarning: '',
};

/**************************************************************/
/**
 * Reads a boolean query value with a default.
 *
 * @param {URLSearchParams} searchParams - Query parameters.
 * @param {string} name - Query key.
 * @param {boolean} fallback - Fallback value.
 * @returns {boolean} Parsed boolean value.
 */
function readBooleanQuery(searchParams, name, fallback) {
    const value = searchParams.get(name);

    if (value === null) {
        return fallback;
    }

    return value !== 'false';
}

/**************************************************************/
/**
 * Reads a bounded integer query value with a default.
 *
 * @param {URLSearchParams} searchParams - Query parameters.
 * @param {string} name - Query key.
 * @param {number} fallback - Fallback value.
 * @param {number} min - Minimum accepted value.
 * @returns {number} Parsed integer value.
 */
function readIntegerQuery(searchParams, name, fallback, min = 0) {
    const value = Number(searchParams.get(name));

    if (!Number.isInteger(value) || value < min) {
        return fallback;
    }

    return value;
}

/**************************************************************/
/**
 * Reads an enum-like query value against an allow-list.
 *
 * @param {URLSearchParams} searchParams - Query parameters.
 * @param {string} name - Query key.
 * @param {Set<string>} allowedValues - Allowed values.
 * @param {string} fallback - Fallback value.
 * @returns {string} Parsed value.
 */
function readEnumQuery(searchParams, name, allowedValues, fallback) {
    const value = searchParams.get(name) ?? fallback;

    return allowedValues.has(value) ? value : fallback;
}

/**************************************************************/
/**
 * Reads the current query string into dashboard state.
 *
 * @returns {object} URL state.
 */
function readDashboardUrlState() {
    // Non-browser imports use the default dashboard state.
    if (!globalThis.window?.location) {
        return {
            focus: 'product',
            productGuid: '',
            view: 'triage',
            comparator: 'all',
            fragile: true,
            classCode: '',
            classView: 'map',
            classComparator: 'Placebo',
            includeNonSignificant: true,
            excludeFragile: true,
            minDrugsPerCell: 4,
            method: 'Spearman',
            aggregation: 'MedianLogRr',
            excludeCombos: false,
            minEvents: 0,
            systems: [],
            systemSearch: '',
            systemView: 'map',
            systemComparator: 'Placebo',
            systemIncludeNonSignificant: true,
            systemExcludeFragile: true,
            systemExcludeCombos: false,
            minTermsPerCell: 4,
            systemMethod: 'Spearman',
            systemAggregation: 'MedianLogRr',
            systemMinEvents: 0,
            systemClassPageNumber: 1,
            systemClassPageSize: DEFAULT_SYSTEM_CLASS_PAGE_SIZE,
            systemDrugPageNumber: 1,
            systemDrugPageSize: DEFAULT_SYSTEM_DRUG_PAGE_SIZE,
            systemTermPairPageNumber: 1,
            systemTermPairPageSize: DEFAULT_SYSTEM_TERM_PAIR_PAGE_SIZE,
            systemFullMatrix: false,
        };
    }

    // URLSearchParams keeps parsing aligned with normal browser query behavior.
    const searchParams = new URLSearchParams(globalThis.window.location.search);

    // Product state is optional and can be hydrated from triage when off-page.
    const productGuid = searchParams.get('product') ?? '';

    // Product mode remains the default when no focus is supplied.
    const requestedFocus = searchParams.get('focus') ?? 'product';

    // The requested view is validated so stale URLs cannot select unknown tabs.
    const requestedView = searchParams.get('view') ?? 'triage';

    // The requested comparator is validated before it reaches API calls.
    const requestedComparator = searchParams.get('comparator') ?? 'all';

    // Fragile rows are visible by default because the prototype exposes them with muted styling.
    const fragile = (searchParams.get('fragile') ?? 'true') !== 'false';

    return {
        focus: DASHBOARD_FOCUSES.has(requestedFocus) ? requestedFocus : 'product',
        productGuid,
        view: DASHBOARD_VIEWS.has(requestedView) ? requestedView : 'triage',
        comparator: COMPARATOR_FILTERS.has(requestedComparator) ? requestedComparator : 'all',
        fragile,
        classCode: searchParams.get('class') ?? '',
        classView: readEnumQuery(searchParams, 'classView', CLASS_DASHBOARD_VIEWS, 'map'),
        classComparator: readEnumQuery(searchParams, 'classComparator', CLASS_COMPARATORS, 'Placebo'),
        includeNonSignificant: readBooleanQuery(searchParams, 'includeNonSignificant', true),
        excludeFragile: readBooleanQuery(searchParams, 'excludeFragile', true),
        minDrugsPerCell: readIntegerQuery(searchParams, 'minDrugsPerCell', 4, 3),
        method: readEnumQuery(searchParams, 'method', CLASS_CORRELATION_METHODS, 'Spearman'),
        aggregation: readEnumQuery(searchParams, 'aggregation', CLASS_CORRELATION_AGGREGATIONS, 'MedianLogRr'),
        excludeCombos: readBooleanQuery(searchParams, 'excludeCombos', false),
        minEvents: readIntegerQuery(searchParams, 'minEvents', 0, 0),
        systems: searchParams.getAll('systems').map((system) => system.trim()).filter(Boolean),
        systemSearch: searchParams.get('systemSearch') ?? '',
        systemView: readEnumQuery(searchParams, 'systemView', SYSTEM_DASHBOARD_VIEWS, 'map'),
        systemComparator: readEnumQuery(searchParams, 'systemComparator', CLASS_COMPARATORS, 'Placebo'),
        systemIncludeNonSignificant: readBooleanQuery(searchParams, 'systemIncludeNonSignificant', true),
        systemExcludeFragile: readBooleanQuery(searchParams, 'systemExcludeFragile', true),
        systemExcludeCombos: readBooleanQuery(searchParams, 'systemExcludeCombos', false),
        minTermsPerCell: readIntegerQuery(searchParams, 'minTermsPerCell', 4, 3),
        systemMethod: readEnumQuery(searchParams, 'systemMethod', CLASS_CORRELATION_METHODS, 'Spearman'),
        systemAggregation: readEnumQuery(
            searchParams,
            'systemAggregation',
            CLASS_CORRELATION_AGGREGATIONS,
            'MedianLogRr',
        ),
        systemMinEvents: readIntegerQuery(searchParams, 'systemMinEvents', 0, 0),
        systemClassPageNumber: readIntegerQuery(searchParams, 'systemClassPageNumber', 1, 1),
        systemClassPageSize: readIntegerQuery(
            searchParams,
            'systemClassPageSize',
            DEFAULT_SYSTEM_CLASS_PAGE_SIZE,
            1,
        ),
        systemDrugPageNumber: readIntegerQuery(searchParams, 'systemDrugPageNumber', 1, 1),
        systemDrugPageSize: readIntegerQuery(
            searchParams,
            'systemDrugPageSize',
            DEFAULT_SYSTEM_DRUG_PAGE_SIZE,
            1,
        ),
        systemTermPairPageNumber: readIntegerQuery(searchParams, 'systemTermPairPageNumber', 1, 1),
        systemTermPairPageSize: readIntegerQuery(
            searchParams,
            'systemTermPairPageSize',
            DEFAULT_SYSTEM_TERM_PAIR_PAGE_SIZE,
            1,
        ),
        systemFullMatrix: readBooleanQuery(searchParams, 'systemFullMatrix', false),
    };
}

/**************************************************************/
/**
 * Writes focus-aware dashboard state to the URL.
 *
 * @param {object} args - URL state to write.
 * @param {boolean} shouldPush - Whether to push or replace browser history.
 */
function writeDashboardUrlState({
    focus,
    productGuid,
    view,
    comparator,
    fragile,
    classCode,
    classView,
    classComparator,
    includeNonSignificant,
    excludeFragile,
    minDrugsPerCell,
    method,
    aggregation,
    excludeCombos,
    minEvents,
    systems,
    systemSearch,
    systemView,
    systemComparator,
    systemIncludeNonSignificant,
    systemExcludeFragile,
    systemExcludeCombos,
    minTermsPerCell,
    systemMethod,
    systemAggregation,
    systemMinEvents,
    systemClassPageNumber,
    systemClassPageSize,
    systemDrugPageNumber,
    systemDrugPageSize,
    systemTermPairPageNumber,
    systemTermPairPageSize,
    systemFullMatrix,
}, shouldPush) {
    // URL state is browser-only.
    if (!globalThis.window?.history) {
        return;
    }

    // The current URL provides path and any unknown query values.
    const nextUrl = new URL(globalThis.window.location.href);
    const currentState = readDashboardUrlState();
    const resolvedFocus = focus ?? currentState.focus;
    const resolvedProductGuid = productGuid ?? currentState.productGuid;
    const resolvedView = view ?? currentState.view;
    const resolvedComparator = comparator ?? currentState.comparator;
    const resolvedFragile = fragile ?? currentState.fragile;
    const resolvedClassCode = classCode ?? currentState.classCode;
    const resolvedClassView = classView ?? currentState.classView;
    const resolvedClassComparator = classComparator ?? currentState.classComparator;
    const resolvedIncludeNonSignificant = includeNonSignificant ?? currentState.includeNonSignificant;
    const resolvedExcludeFragile = excludeFragile ?? currentState.excludeFragile;
    const resolvedMinDrugsPerCell = minDrugsPerCell ?? currentState.minDrugsPerCell;
    const resolvedMethod = method ?? currentState.method;
    const resolvedAggregation = aggregation ?? currentState.aggregation;
    const resolvedExcludeCombos = excludeCombos ?? currentState.excludeCombos;
    const resolvedMinEvents = minEvents ?? currentState.minEvents;
    const resolvedSystems = systems ?? currentState.systems;
    const resolvedSystemSearch = systemSearch ?? currentState.systemSearch;
    const resolvedSystemView = systemView ?? currentState.systemView;
    const resolvedSystemComparator = systemComparator ?? currentState.systemComparator;
    const resolvedSystemIncludeNonSignificant =
        systemIncludeNonSignificant ?? currentState.systemIncludeNonSignificant;
    const resolvedSystemExcludeFragile = systemExcludeFragile ?? currentState.systemExcludeFragile;
    const resolvedSystemExcludeCombos = systemExcludeCombos ?? currentState.systemExcludeCombos;
    const resolvedMinTermsPerCell = minTermsPerCell ?? currentState.minTermsPerCell;
    const resolvedSystemMethod = systemMethod ?? currentState.systemMethod;
    const resolvedSystemAggregation = systemAggregation ?? currentState.systemAggregation;
    const resolvedSystemMinEvents = systemMinEvents ?? currentState.systemMinEvents;
    const resolvedSystemClassPageNumber = systemClassPageNumber ?? currentState.systemClassPageNumber;
    const resolvedSystemClassPageSize = systemClassPageSize ?? currentState.systemClassPageSize;
    const resolvedSystemDrugPageNumber = systemDrugPageNumber ?? currentState.systemDrugPageNumber;
    const resolvedSystemDrugPageSize = systemDrugPageSize ?? currentState.systemDrugPageSize;
    const resolvedSystemTermPairPageNumber =
        systemTermPairPageNumber ?? currentState.systemTermPairPageNumber;
    const resolvedSystemTermPairPageSize = systemTermPairPageSize ?? currentState.systemTermPairPageSize;
    const resolvedSystemFullMatrix = systemFullMatrix ?? currentState.systemFullMatrix;

    nextUrl.searchParams.set('focus', resolvedFocus);

    // Product GUID is removed when no product is selected.
    if (resolvedProductGuid) {
        nextUrl.searchParams.set('product', resolvedProductGuid);
    } else {
        nextUrl.searchParams.delete('product');
    }

    nextUrl.searchParams.set('view', resolvedView);
    nextUrl.searchParams.set('comparator', resolvedComparator);
    nextUrl.searchParams.set('fragile', String(resolvedFragile));

    if (resolvedClassCode) {
        nextUrl.searchParams.set('class', resolvedClassCode);
    } else {
        nextUrl.searchParams.delete('class');
    }

    nextUrl.searchParams.set('classView', resolvedClassView);
    nextUrl.searchParams.set('classComparator', resolvedClassComparator);
    nextUrl.searchParams.set('includeNonSignificant', String(resolvedIncludeNonSignificant));
    nextUrl.searchParams.set('excludeFragile', String(resolvedExcludeFragile));
    nextUrl.searchParams.set('minDrugsPerCell', String(resolvedMinDrugsPerCell));
    nextUrl.searchParams.set('method', resolvedMethod);
    nextUrl.searchParams.set('aggregation', resolvedAggregation);
    nextUrl.searchParams.delete('seriousSocOnly');
    nextUrl.searchParams.set('excludeCombos', String(resolvedExcludeCombos));
    nextUrl.searchParams.set('minEvents', String(resolvedMinEvents));
    nextUrl.searchParams.delete('systems');
    for (const system of resolvedSystems) {
        if (system) {
            nextUrl.searchParams.append('systems', system);
        }
    }
    if (resolvedSystemSearch) {
        nextUrl.searchParams.set('systemSearch', resolvedSystemSearch);
    } else {
        nextUrl.searchParams.delete('systemSearch');
    }
    nextUrl.searchParams.set('systemView', resolvedSystemView);
    nextUrl.searchParams.set('systemComparator', resolvedSystemComparator);
    nextUrl.searchParams.set('systemIncludeNonSignificant', String(resolvedSystemIncludeNonSignificant));
    nextUrl.searchParams.set('systemExcludeFragile', String(resolvedSystemExcludeFragile));
    nextUrl.searchParams.set('systemExcludeCombos', String(resolvedSystemExcludeCombos));
    nextUrl.searchParams.set('minTermsPerCell', String(resolvedMinTermsPerCell));
    nextUrl.searchParams.set('systemMethod', resolvedSystemMethod);
    nextUrl.searchParams.set('systemAggregation', resolvedSystemAggregation);
    nextUrl.searchParams.set('systemMinEvents', String(resolvedSystemMinEvents));
    nextUrl.searchParams.set('systemClassPageNumber', String(resolvedSystemClassPageNumber));
    nextUrl.searchParams.set('systemClassPageSize', String(resolvedSystemClassPageSize));
    nextUrl.searchParams.set('systemDrugPageNumber', String(resolvedSystemDrugPageNumber));
    nextUrl.searchParams.set('systemDrugPageSize', String(resolvedSystemDrugPageSize));
    nextUrl.searchParams.set('systemTermPairPageNumber', String(resolvedSystemTermPairPageNumber));
    nextUrl.searchParams.set('systemTermPairPageSize', String(resolvedSystemTermPairPageSize));
    nextUrl.searchParams.set('systemFullMatrix', String(resolvedSystemFullMatrix));

    // Product selection should be bookmarkable as a navigation event.
    if (shouldPush) {
        globalThis.window.history.pushState({}, '', nextUrl);
    } else {
        globalThis.window.history.replaceState({}, '', nextUrl);
    }
}

/**************************************************************/
/**
 * Merges favorite state from the server favorite set into visible products.
 *
 * @param {object} product - Product view model.
 * @param {Set<string>} favoriteGuids - Lowercase favorite GUID lookup.
 * @returns {object} Product with favorite state applied.
 */
function applyFavoriteLookup(product, favoriteGuids) {
    // Missing products pass through as-is.
    if (!product?.documentGuid) {
        return product;
    }

    // API catalog rows already carry IsFavorite, but favorite hydration can refresh it.
    const lookupKey = product.documentGuid.toLowerCase();

    return {
        ...product,
        isFavorite: product.isFavorite || favoriteGuids.has(lookupKey),
    };
}

/**************************************************************/
/**
 * Flattens tiered triage signals into one list for counts and export.
 *
 * @param {object[]} tiers - Triage tiers.
 * @returns {object[]} Signal list.
 */
function flattenTriageSignals(tiers) {
    // Flat output preserves tier order by appending each tier's signals in sequence.
    const signals = [];

    // Every tier can be empty, so each collection is checked defensively.
    for (const tier of tiers) {
        // Missing signal arrays are ignored.
        if (!Array.isArray(tier?.signals)) {
            continue;
        }

        // Each signal is appended without mutation.
        for (const signal of tier.signals) {
            signals.push(signal);
        }
    }

    return signals;
}

/**************************************************************/
/**
 * Applies comparator and fragile filters to one signal.
 *
 * @param {object} signal - Signal view model.
 * @param {string} comparator - Comparator filter token.
 * @param {boolean} showFragile - Whether fragile signals should render.
 * @returns {boolean} Whether the signal should render.
 */
function shouldShowSignal(signal, comparator, showFragile) {
    // Fragile rows are suppressed only when the user toggles them off.
    if (!showFragile && signal.prec === 'fragile') {
        return false;
    }

    // Placebo filter keeps only placebo-controlled rows.
    if (comparator === 'placebo') {
        return Boolean(signal.isPlac);
    }

    // Active filter keeps non-placebo comparator rows.
    if (comparator === 'active') {
        return !signal.isPlac;
    }

    return true;
}

/**************************************************************/
/**
 * Applies dashboard filters to tiered triage output.
 *
 * @param {object[]} tiers - Triage tiers.
 * @param {string} comparator - Comparator filter token.
 * @param {boolean} showFragile - Whether fragile signals should render.
 * @returns {object[]} Filtered tier list.
 */
function filterTriageTiers(tiers, comparator, showFragile) {
    // Tiers keep their server-provided metadata while filtering child rows.
    return tiers.map((tier) => ({
        ...tier,
        signals: tier.signals.filter((signal) => shouldShowSignal(signal, comparator, showFragile)),
    }));
}

/**************************************************************/
/**
 * Counts triage signals for comparator chips and fragile toggle labels.
 *
 * @param {object[]} signals - Flat signal list.
 * @returns {{ all: number, placebo: number, active: number, fragile: number }} Counts.
 */
function countSignals(signals) {
    // The accumulator starts at zero so empty payloads render cleanly.
    const counts = {
        all: 0,
        placebo: 0,
        active: 0,
        fragile: 0,
    };

    // Each signal contributes to total, comparator, and optional fragile counts.
    for (const signal of signals) {
        counts.all += 1;

        // Comparator split mirrors the prototype controls.
        if (signal.isPlac) {
            counts.placebo += 1;
        } else {
            counts.active += 1;
        }

        // Precision class is already derived by the API.
        if (signal.prec === 'fragile') {
            counts.fragile += 1;
        }
    }

    return counts;
}

/**************************************************************/
/**
 * Converts a server tier value into the prototype class suffix.
 *
 * @param {object} tier - Triage tier view model.
 * @returns {string} Tier class suffix.
 */
function getTierToken(tier) {
    // Server enum strings and display names are both normalized through lowercase text.
    const source = `${tier?.tier ?? ''} ${tier?.name ?? ''}`.toLowerCase();

    // Counsel rows use the prototype's first action tier.
    if (source.includes('counsel')) {
        return 'counsel';
    }

    // Watch rows are rare-but-serious risk signals.
    if (source.includes('watch')) {
        return 'watch';
    }

    // Fragile rows are low-confidence rows.
    if (source.includes('fragile') || source.includes('low confidence')) {
        return 'fragile';
    }

    // Reassure is the safest fallback for unknown tier labels.
    return 'reassure';
}

/**************************************************************/
/**
 * Formats a number-needed value with a clinical fallback.
 *
 * @param {number | null | undefined} value - Number-needed value.
 * @returns {string} Formatted number-needed value.
 */
function formatNumberNeededValue(value) {
    // Missing values mean the interval did not support NNH or NNT.
    if (value === null || value === undefined || !Number.isFinite(Number(value))) {
        return '-';
    }

    // Very large bounds are clearer as an infinity-style endpoint.
    if (Number(value) >= 9999) {
        return 'inf';
    }

    return formatInteger(value);
}

/**************************************************************/
/**
 * Formats one event count and denominator pair.
 *
 * @param {number | null | undefined} events - Event count.
 * @param {number | null | undefined} denominator - Denominator count.
 * @returns {string} Event count display.
 */
function formatEvents(events, denominator) {
    // Missing values render as a dash so zero is never implied.
    if (events === null || events === undefined || denominator === null || denominator === undefined) {
        return '-';
    }

    // Event counts can be fractional after continuity correction.
    return `${formatDecimal(events, 1)} / ${formatInteger(denominator)}`;
}

/**************************************************************/
/**
 * Formats one event rate percentage.
 *
 * @param {number | null | undefined} events - Event count.
 * @param {number | null | undefined} denominator - Denominator count.
 * @returns {string} Event rate display.
 */
function formatEventRate(events, denominator) {
    // A positive denominator is required for a meaningful rate.
    if (!Number.isFinite(Number(events)) || !Number.isFinite(Number(denominator)) || Number(denominator) <= 0) {
        return '-';
    }

    return `${formatDecimal((Number(events) / Number(denominator)) * 100, 1)}%`;
}

/**************************************************************/
/**
 * Gets the prototype direction class for one signal.
 *
 * @param {object} signal - Signal view model.
 * @returns {string} Direction class.
 */
function getSignalDirection(signal) {
    // Not-significant rows render neutral even when RR is above or below one.
    if (!signal.sig) {
        return 'ns';
    }

    // Protective rows use the teal direction.
    if (signal.prot) {
        return 'protective';
    }

    // Significant non-protective rows are elevated risk.
    return 'elevated';
}

/**************************************************************/
/**
 * Builds a unique product list for cross-product controls.
 *
 * @param {object[]} productGroups - Product collections in priority order.
 * @returns {object[]} Dedupe product list.
 */
function buildUniqueProductList(...productGroups) {
    const usedDocumentGuids = new Set();
    const uniqueProducts = [];

    // Preserve first-seen order so the selected product and favorites stay near the top.
    for (const group of productGroups) {
        if (!Array.isArray(group)) {
            continue;
        }

        for (const product of group) {
            if (!product?.documentGuid) {
                continue;
            }

            const lookupKey = product.documentGuid.toLowerCase();
            if (usedDocumentGuids.has(lookupKey)) {
                continue;
            }

            usedDocumentGuids.add(lookupKey);
            uniqueProducts.push(product);
        }
    }

    return uniqueProducts;
}

/**************************************************************/
/**
 * Builds exact AE-term suggestions from loaded signal rows only.
 *
 * @param {object[]} signalGroups - Loaded signal collections.
 * @returns {string[]} Suggestion terms.
 */
function buildAeTermSuggestions(...signalGroups) {
    const usedTerms = new Set();
    const suggestions = [];

    // Suggestions intentionally come only from already-loaded live data.
    for (const group of signalGroups) {
        if (!Array.isArray(group)) {
            continue;
        }

        for (const signal of group) {
            const term = signal?.name?.trim();
            if (!term) {
                continue;
            }

            const lookupKey = term.toLowerCase();
            if (usedTerms.has(lookupKey)) {
                continue;
            }

            usedTerms.add(lookupKey);
            suggestions.push(term);
        }
    }

    return suggestions.slice(0, 12);
}

/**************************************************************/
/**
 * Builds the scoped product GUIDs for reverse lookup.
 *
 * @param {object[]} products - Product view models to scope.
 * @returns {string[]} Unique document GUIDs.
 */
function buildReverseLookupScope(products) {
    const usedDocumentGuids = new Set();
    const documentGuids = [];

    // Repeated query values are emitted later by the API client.
    for (const product of products) {
        if (!product?.documentGuid) {
            continue;
        }

        const lookupKey = product.documentGuid.toLowerCase();
        if (usedDocumentGuids.has(lookupKey)) {
            continue;
        }

        usedDocumentGuids.add(lookupKey);
        documentGuids.push(product.documentGuid);
    }

    return documentGuids;
}

/**************************************************************/
/**
 * Builds a de-duplicated exact-term list for reverse lookup.
 *
 * @param {Array<string | string[]>} termGroups - Term values or lists.
 * @returns {string[]} Unique display terms.
 */
function buildReverseLookupTerms(...termGroups) {
    const usedTerms = new Set();
    const terms = [];

    // Text input may contain pasted comma/semicolon lists; chips pass arrays.
    for (const group of termGroups) {
        const candidates = Array.isArray(group) ? group : [group];

        for (const candidate of candidates) {
            const splitTerms = String(candidate ?? '')
                .split(/[;,]/)
                .map((term) => term.trim())
                .filter(Boolean);

            for (const term of splitTerms) {
                const lookupKey = term.toLowerCase();

                if (usedTerms.has(lookupKey)) {
                    continue;
                }

                usedTerms.add(lookupKey);
                terms.push(term);
            }
        }
    }

    return terms;
}

/**************************************************************/
/**
 * Converts an interchange classification into a delta CSS class.
 *
 * @param {string} classification - API classification token.
 * @returns {string} Delta CSS token.
 */
function getInterchangeDeltaClass(classification) {
    if (classification === 'aworse') {
        return 'a-worse';
    }

    if (classification === 'bworse') {
        return 'b-worse';
    }

    if (classification === 'onlya' || classification === 'onlyb') {
        return 'only';
    }

    return 'similar';
}

/**************************************************************/
/**
 * Gets the compact comparator label for an interchange signal.
 *
 * @param {object | null} signal - Normalized AE signal.
 * @param {boolean} compact - Whether to abbreviate active-comparator text.
 * @returns {string} Comparator label.
 */
function getSignalComparatorLabel(signal, compact = false) {
    if (!signal) {
        return 'No signal';
    }

    if (signal.isPlac) {
        return 'Placebo';
    }

    return compact ? 'Active' : 'Active comparator';
}

/**************************************************************/
/**
 * Builds tooltip content for an interchange signal point.
 *
 * @param {object | null} signal - Normalized AE signal.
 * @param {string} productLabel - Product display label.
 * @returns {{ heading: string, lines: string[] } | null} Tooltip content.
 */
function getInterchangeSignalTooltipContent(signal, productLabel) {
    if (!signal) {
        return null;
    }

    const direction = signal.sig
        ? (signal.prot ? 'Significant protective' : 'Significant elevated')
        : 'Not significant';
    const numberNeeded = signal.nnh ?? signal.nnt;
    const numberNeededLabel = signal.nnt !== null && signal.nnt !== undefined ? 'NNT' : 'NNH';
    const hasNumberNeeded = numberNeeded !== null
        && numberNeeded !== undefined
        && Number.isFinite(Number(numberNeeded));

    return {
        heading: `${productLabel}: ${signal.name}`,
        lines: [
            getSignalComparatorLabel(signal),
            signal.studyContext ? `Study: ${signal.studyContext}` : null,
            signal.population ? `Population: ${signal.population}` : null,
            signal.subpopulation ? `Subpopulation: ${signal.subpopulation}` : null,
            `RR ${formatDecimal(signal.rr, 2)} [${formatDecimal(signal.rrL, 2)}-${formatDecimal(signal.rrH, 2)}]`,
            `${formatEvents(signal.eT, signal.armN)} vs ${formatEvents(signal.eC, signal.comparatorN)}`,
            hasNumberNeeded ? `${numberNeededLabel} ${formatNumberNeededValue(numberNeeded)}` : null,
            direction,
        ].filter(Boolean),
    };
}

/**************************************************************/
/**
 * Builds accessible text for an interchange signal point.
 *
 * @param {object | null} signal - Normalized AE signal.
 * @param {string} productLabel - Product display label.
 * @returns {string} Accessible label.
 */
function getInterchangeSignalAriaLabel(signal, productLabel) {
    const content = getInterchangeSignalTooltipContent(signal, productLabel);

    if (!content) {
        return `${productLabel}: no signal`;
    }

    return [content.heading, ...content.lines].join('. ');
}

/**************************************************************/
/**
 * Renders the styled interchange signal tooltip.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Tooltip element.
 */
function InterchangeSignalTooltip({ signal, productLabel, className = '', style = null }) {
    const content = getInterchangeSignalTooltipContent(signal, productLabel);

    if (!content) {
        return null;
    }

    return (
        <div className={`ic-tooltip${className}`} style={style} aria-hidden="true">
            <strong>{content.heading}</strong>
            {content.lines.map((line) => (
                <div key={line} className="small">{line}</div>
            ))}
        </div>
    );
}

/**************************************************************/
/**
 * Gets short context text for the metadata line under an interchange track.
 *
 * @param {object} signal - Normalized AE signal.
 * @returns {string} Context text.
 */
function getInterchangeContextText(signal) {
    return signal.studyContext || signal.population || signal.subpopulation || 'Study context not listed';
}

/**************************************************************/
/**
 * Gets the display label for a reverse-lookup verdict.
 *
 * @param {string} verdict - Server verdict token.
 * @returns {string} Display label.
 */
function getReverseLookupVerdictLabel(verdict) {
    return REVERSE_LOOKUP_VERDICT_COPY[verdict] ?? 'Signal reviewed';
}

/**************************************************************/
/**
 * Extracts all signals from an interchange view.
 *
 * @param {object} interchangeView - Interchange view model.
 * @returns {object[]} Signal list.
 */
function getInterchangeSignals(interchangeView) {
    const signals = [];

    // Both product signal columns contribute to the shared RR scale.
    for (const row of interchangeView.rows) {
        if (row.signalA) {
            signals.push(row.signalA);
        }

        if (row.signalB) {
            signals.push(row.signalB);
        }
    }

    return signals;
}

/**************************************************************/
/*
 * The masthead (logo, primary nav, subtitle, hamburger toggle, and the
 * Save/Export actions) is no longer rendered by React. It is server-rendered
 * once by the shared _Masthead.cshtml partial and styled by masthead.css — a
 * single source of truth across every server-rendered page and this React
 * island. The dashboard host view (Views/AdverseEventDashboard/Index.cshtml)
 * injects the Save/Export buttons via ViewData["MastheadActions"]; React owns
 * only their behavior, wiring #aeSaveBtn / #aeExportBtn by id in the
 * masthead-actions effect inside App (search "aeSaveBtn"). The hamburger toggle
 * is handled by the partial's own inline script.
 *
 * NOTE: the standalone Vite dev page (index.html, #root) has no masthead because
 * it does not render the Razor partial; the masthead appears only when
 * MVC-hosted at /adverse-events.
 */

/**************************************************************/
/**
 * Renders one adverse-event signal row in the triage panel.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Triage signal row.
 */
function AeRow({ signal, tierToken, expanded, onToggle }) {
    // NNT rows are protective; all other number-needed rows are NNH.
    const isNnt = signal.type?.toUpperCase?.() === 'NNT' || signal.nnt !== null;

    // The point estimate chooses the matching NNH or NNT value.
    const numberNeeded = isNnt ? signal.nnt : signal.nnh;

    // The lower bound chooses the matching NNH or NNT value.
    const numberNeededLower = isNnt ? signal.nntL : signal.nnhL;

    // The upper bound chooses the matching NNH or NNT value.
    const numberNeededUpper = isNnt ? signal.nntH : signal.nnhH;

    // A row reports an NNH/NNT only when it is statistically significant: the RR
    // 95% CI must exclude 1. When the CI brackets 1 (or the server classifies the
    // row as not significant) the point estimate is not meaningful, so the row
    // renders "NS" instead — matching the forest plot's neutral classification.
    const hasRrCi = signal.rrL !== null && signal.rrH !== null;
    const rrCiExcludesOne = hasRrCi ? signal.rrL > 1 || signal.rrH < 1 : true;
    const showNumberNeeded = numberNeeded !== null && signal.sig && rrCiExcludesOne;

    // Serious SOC is a display tag, while tier assignment remains server-owned.
    const isSerious = SERIOUS_SOC.has(signal.soc) && signal.sig;

    // Study context identifies the trial or analysis setting behind this row.
    const hasStudyContext = Boolean(signal.studyContext);

    // Population identifies the analysis population behind this row.
    const hasPopulation = Boolean(signal.population);

    return (
        <div
            className={`ae-row ${tierToken}${signal.prec === 'fragile' ? ' fragile' : ''}${expanded ? ' expanded' : ''}`}
            role="button"
            tabIndex={0}
            onClick={onToggle}
            onKeyDown={(event) => {
                // Enter and Space mirror native button activation for keyboard users.
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    onToggle();
                }
            }}
        >
            <div className="ae-nnh">
                {showNumberNeeded ? (
                    <>
                        <div className="ae-nnh-label">{isNnt ? 'NNT - benefit 1 in' : 'NNH - harm 1 in'}</div>
                        {/* Color follows harm/benefit type (NNH orange, NNT green), not the tier. */}
                        <div className={`ae-nnh-value ${isNnt ? 'is-nnt' : 'is-nnh'}`}>
                            <span className="ae-nnh-prefix">~</span>
                            {formatNumberNeededValue(numberNeeded)}
                        </div>
                        <div className="ae-nnh-bounds">
                            [{formatNumberNeededValue(numberNeededLower)} - {formatNumberNeededValue(numberNeededUpper)}]
                        </div>
                    </>
                ) : (
                    /* Not statistically significant: the RR CI includes 1, so no NNH/NNT is reported. */
                    <div className="ae-nnh-ns" title="Not statistically significant — the 95% confidence interval for RR includes 1">
                        NS
                        <span className="ae-nnh-ns-sub">not significant</span>
                    </div>
                )}
            </div>

            <div className="ae-body">
                <div className="ae-name">{signal.name}</div>
                <div className="ae-meta">
                    <span className="ae-tag soc">{signal.soc}</span>
                    <span className="ae-tag rr">
                        RR {formatDecimal(signal.rr, 2)} [{formatDecimal(signal.rrL, 2)}-{formatDecimal(signal.rrH, 2)}]
                    </span>
                    {/* Dose distinguishes otherwise-duplicate rows reported at different strengths. */}
                    {signal.dose !== null ? (
                        <span className="ae-tag dose">{formatDose(signal.dose, signal.doseUnit)}</span>
                    ) : null}
                    {/* Study context only renders when the API identifies the trial or analysis setting. */}
                    {hasStudyContext ? (
                        <span className="ae-tag study-context" title={signal.studyContext}>
                            Study: {signal.studyContext}
                        </span>
                    ) : null}
                    {/* Population only renders when the API supplies a cohort descriptor. */}
                    {hasPopulation ? (
                        <span className="ae-tag population" title={signal.population}>
                            Population: {signal.population}
                        </span>
                    ) : null}
                    {isSerious ? <span className="ae-tag serious">Serious SOC</span> : null}
                    {!signal.isPlac ? <span className="ae-tag">vs active comparator</span> : null}
                    {signal.combo ? <span className="ae-tag combo">Combination product</span> : null}
                </div>
            </div>

            <div className="ae-right">
                <span className={`precision-pill ${signal.prec}`}>
                    <span className="pip" />
                    {signal.prec}
                </span>
                <span className="ae-expand">{expanded ? '-' : '+'} details</span>
            </div>

            {expanded ? (
                <div className="ae-detail">
                    <div className="ae-detail-cell">
                        <span className="lbl">Treatment events</span>
                        <span className="val">
                            {formatEvents(signal.eT, signal.armN)}
                            <span> ({formatEventRate(signal.eT, signal.armN)})</span>
                        </span>
                    </div>
                    <div className="ae-detail-cell">
                        <span className="lbl">Comparator events</span>
                        <span className="val">
                            {formatEvents(signal.eC, signal.comparatorN)}
                            <span> ({formatEventRate(signal.eC, signal.comparatorN)})</span>
                        </span>
                    </div>
                    <div className="ae-detail-cell">
                        <span className="lbl">Risk type</span>
                        <span className="val">{signal.prot ? 'Protective' : signal.sig ? 'Elevated' : 'Not significant'}</span>
                    </div>
                    <div className="ae-detail-cell">
                        <span className="lbl">Comparator</span>
                        <span className="val">{signal.isPlac ? 'Placebo' : 'Active'}</span>
                    </div>
                    {signal.flags.length > 0 ? (
                        <div className="ae-detail-cell wide-cell">
                            <span className="lbl">Why this row is low confidence</span>
                            <span className="val wide">
                                {signal.flags.map((flag) => FLAG_TEXT[flag] || flag).join(' ')}
                            </span>
                        </div>
                    ) : null}
                </div>
            ) : null}
        </div>
    );
}

/**************************************************************/
/**
 * Renders the prototype-style triage view.
 *
 * @param {{ tiers: object[] }} props - Component props.
 * @returns {JSX.Element} Triage view.
 */
function TriageView({ tiers }) {
    // Expanded state tracks one signal id across all tiers.
    const [expandedSignalId, setExpandedSignalId] = useState(null);

    // Empty tiers are hidden just like the original prototype.
    const visibleTiers = tiers.filter((tier) => tier.signals.length > 0);

    // Successful empty payloads get a quiet empty state.
    if (visibleTiers.length === 0) {
        return <EmptyState title="No adverse-event rows match these filters." />;
    }

    return (
        <div className="triage-view">
            {visibleTiers.map((tier) => {
                // Tier token drives the colored left rail and marker.
                const tierToken = getTierToken(tier);

                return (
                    <div key={tier.tier || tier.name} className={`tier tier-${tierToken}`}>
                        <div className="tier-header">
                            <div className="tier-marker" aria-hidden="true" />
                            <div className="tier-meta">
                                <div className="tier-name">{tier.name}</div>
                                <div className="tier-desc">{tier.description}</div>
                            </div>
                            <div className="tier-count">{tier.signals.length}</div>
                        </div>

                        {tier.signals.map((signal) => {
                            // Signal id is encrypted when available and term-based as a fallback.
                            const signalId = signal.id || `${tier.tier}-${signal.name}`;

                            return (
                                <AeRow
                                    key={signalId}
                                    signal={signal}
                                    tierToken={tierToken}
                                    expanded={expandedSignalId === signalId}
                                    onToggle={() => {
                                        // Clicking the open row closes it; otherwise the clicked row opens.
                                        setExpandedSignalId((currentId) => (currentId === signalId ? null : signalId));
                                    }}
                                />
                            );
                        })}
                    </div>
                );
            })}
        </div>
    );
}

/**************************************************************/
/**
 * Builds a compact accessible label for a forest row.
 *
 * @param {object} signal - Signal view model.
 * @param {{ min: number, max: number }} scaleDomain - Current forest scale domain.
 * @returns {string} Row label with actual RR and CI values.
 */
function getForestRowLabel(signal, scaleDomain) {
    const scaleLabel = `${formatForestTick(scaleDomain.min)} to ${formatForestTick(scaleDomain.max)}`;

    return `${signal.name}; ${signal.soc}; RR ${formatDecimal(signal.rr, 2)} [${formatDecimal(signal.rrL, 2)}-${formatDecimal(signal.rrH, 2)}]; log scale ${scaleLabel}.`;
}

/**************************************************************/
/**
 * Renders the prototype-style forest plot.
 *
 * @param {{ signals: object[] }} props - Component props.
 * @returns {JSX.Element} Forest view.
 */
function ForestView({ signals }) {
    // Forest rows are normalized defensively so empty/error payloads stay stable.
    const forestSignals = useMemo(() => (Array.isArray(signals) ? signals : []), [signals]);

    // The rendered rows determine one shared dynamic log domain.
    const scaleDomain = useMemo(() => getForestScaleDomain(forestSignals), [forestSignals]);

    // Narrow viewports thin the axis labels so they never overlap on phones.
    const isNarrow = useMediaQuery('(max-width: 420px)');
    const isCompact = useMediaQuery('(max-width: 620px)');
    const maxTicks = isNarrow
        ? COMPACT_FOREST_TICKS_NARROW
        : isCompact
            ? COMPACT_FOREST_TICKS
            : MAX_FOREST_TICKS;

    // Tick labels follow the expanded domain instead of trusting server bounds.
    const scaleTicks = useMemo(() => getForestTicks(scaleDomain, maxTicks), [scaleDomain, maxTicks]);

    // Empty signal payloads render a stable message.
    if (forestSignals.length === 0) {
        return <EmptyState title="No forest-plot rows match these filters." />;
    }

    return (
        <div className="forest-view">
            <div className="forest-legend">
                <span className="forest-legend-item">
                    <span className="forest-legend-dot elevated" />
                    Elevated risk
                </span>
                <span className="forest-legend-item">
                    <span className="forest-legend-dot protective" />
                    Protective
                </span>
                <span className="forest-legend-item">
                    <span className="forest-legend-dot neutral" />
                    Not significant
                </span>
                <span className="forest-legend-note">Protective left / RR=1 / Elevated right</span>
            </div>

            <div className="forest-wrap">
                <div className="forest-axis">
                    <div className="forest-axis-spacer" />
                    <div className="forest-axis-ticks">
                        {scaleTicks.map((tick) => {
                            // Each tick is positioned on the same log scale as the rows.
                            const left = getForestXPercent(tick, scaleDomain);

                            // Invalid generated ticks are ignored defensively.
                            if (left === null) {
                                return null;
                            }

                            return (
                                <span key={tick} className={`forest-tick${tick === 1 ? ' ref' : ''}`} style={{ left: `${left}%` }}>
                                    {formatForestTick(tick)}
                                </span>
                            );
                        })}
                    </div>
                </div>

                {forestSignals.map((signal) => {
                    // Direction drives point color and row class.
                    const direction = getSignalDirection(signal);

                    // Point and interval positions share the same log-scale transform.
                    const pointLeft = getForestXPercent(signal.rr, scaleDomain);

                    // CI values are sorted only for drawing so swapped bounds never invert CSS.
                    const lowerBoundValue = Number(signal.rrL);
                    const upperBoundValue = Number(signal.rrH);
                    const hasInterval = Number.isFinite(lowerBoundValue)
                        && lowerBoundValue > 0
                        && Number.isFinite(upperBoundValue)
                        && upperBoundValue > 0;
                    const intervalStartValue = hasInterval ? Math.min(lowerBoundValue, upperBoundValue) : null;
                    const intervalEndValue = hasInterval ? Math.max(lowerBoundValue, upperBoundValue) : null;

                    // CI lower bound position is nullable when the source bound is absent.
                    const lowerLeft = getForestXPercent(intervalStartValue, scaleDomain);

                    // CI upper bound position is nullable when the source bound is absent.
                    const upperLeft = getForestXPercent(intervalEndValue, scaleDomain);

                    // The RR=1 reference line remains fixed in every row.
                    const referenceLeft = getForestXPercent(1, scaleDomain);

                    // Interval width is clamped to avoid negative CSS widths.
                    const intervalWidth = lowerLeft !== null && upperLeft !== null
                        ? Math.max(0, upperLeft - lowerLeft)
                        : 0;

                    // Actual values stay exposed even when the visual domain expands.
                    const rowLabel = getForestRowLabel(signal, scaleDomain);

                    return (
                        <div
                            key={signal.id || signal.name}
                            className={`forest-row ${direction}${signal.prec === 'fragile' ? ' fragile' : ''}`}
                            title={rowLabel}
                            aria-label={rowLabel}
                        >
                            <div className="forest-label" title={signal.name}>
                                {signal.name}
                                <span className="sub">{signal.soc}</span>
                            </div>
                            <div className="forest-track">
                                <div className="forest-refline" style={{ left: `${referenceLeft}%` }} />
                                {lowerLeft !== null && upperLeft !== null ? (
                                    <>
                                        <div className="forest-ci" style={{ left: `${lowerLeft}%`, width: `${intervalWidth}%` }} />
                                        <div className="forest-ci-cap" style={{ left: `${lowerLeft}%` }} />
                                        <div className="forest-ci-cap" style={{ left: `${upperLeft}%` }} />
                                    </>
                                ) : null}
                                {pointLeft !== null ? <div className="forest-pt" style={{ left: `${pointLeft}%` }} /> : null}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}

/**************************************************************/
/**
 * Renders the prototype-style quadrant plot.
 *
 * @param {{ points: object[] }} props - Component props.
 * @returns {JSX.Element} Quadrant view.
 */
function QuadrantView({ points }) {
    // Hover state drives the small term tooltip.
    const [hoverPoint, setHoverPoint] = useState(null);

    // Empty point payloads render a stable message.
    if (points.length === 0) {
        return <EmptyState title="No quadrant rows match these filters." />;
    }

    return (
        <div className="quadrant-wrap">
            <div className="quadrant-stage">
                <div className="axis-y">Effect magnitude</div>
                <div className="quadrant">
                    <div className="q-cell tl">
                        <span className="q-cell-name">Increased Risk/Low Precision</span>
                    </div>
                    <div className="q-cell tr">
                        <span className="q-cell-name">Increased Risk/High Precision</span>
                    </div>
                    <div className="q-cell bl">
                        <span className="q-cell-name">Reduced Risk/Low Precision</span>
                    </div>
                    <div className="q-cell br">
                        <span className="q-cell-name">Reduced Risk/High Precision</span>
                    </div>

                    {points.map((point) => {
                        // Coordinates are already clamped by the API; CSS keeps dots inset.
                        const left = point.x * 92 + 4;

                        // CSS top coordinates invert the y-axis.
                        const top = (1 - point.y) * 92 + 4;

                        // Bubble size is capped so dense datasets remain readable.
                        const size = Math.max(8, Math.min(point.size, 34));

                        return (
                            <button
                                key={point.id}
                                type="button"
                                className={`q-dot ${point.direction}${point.signal.prec === 'fragile' ? ' fragile' : ''}`}
                                style={{
                                    left: `${left}%`,
                                    top: `${top}%`,
                                    width: `${size}px`,
                                    height: `${size}px`,
                                }}
                                aria-label={`${point.signal.name}, RR ${formatDecimal(point.signal.rr, 2)}`}
                                onMouseEnter={() => setHoverPoint(point)}
                                onMouseLeave={() => setHoverPoint(null)}
                                onFocus={() => setHoverPoint(point)}
                                onBlur={() => setHoverPoint(null)}
                            />
                        );
                    })}

                    {hoverPoint ? (
                        <div
                            className={`q-tooltip${hoverPoint.x > 0.66 ? ' is-left' : ' is-right'}${hoverPoint.y > 0.78 ? ' is-lower' : ''}${hoverPoint.y < 0.22 ? ' is-upper' : ''}`}
                            style={{
                                left: `${hoverPoint.x * 92 + 4}%`,
                                top: `${(1 - hoverPoint.y) * 92 + 4}%`,
                            }}
                        >
                            <strong>{hoverPoint.signal.name}</strong>
                            <div className="small">
                                RR {formatDecimal(hoverPoint.signal.rr, 2)} [{formatDecimal(hoverPoint.signal.rrL, 2)}-{formatDecimal(hoverPoint.signal.rrH, 2)}]
                            </div>
                            <div className="small">
                                {formatEvents(hoverPoint.signal.eT, hoverPoint.signal.armN)} vs {formatEvents(hoverPoint.signal.eC, hoverPoint.signal.comparatorN)}
                            </div>
                        </div>
                    ) : null}
                </div>
                <div className="quadrant-axes">
                    <span>Lower precision</span>
                    <span>Higher precision</span>
                </div>
            </div>

            <div className="forest-legend quadrant-legend">
                <span className="forest-legend-item">
                    <span className="forest-legend-dot elevated" />
                    Elevated risk
                </span>
                <span className="forest-legend-item">
                    <span className="forest-legend-dot protective" />
                    Protective
                </span>
                <span className="forest-legend-item">
                    <span className="forest-legend-dot neutral" />
                    Not significant
                </span>
                <span className="forest-legend-note">Bubble size tracks event volume</span>
            </div>
        </div>
    );
}

/**************************************************************/
/**
 * Renders one exact-term suggestion chip for reverse lookup.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Suggestion button.
 */
function ReverseLookupSuggestion({ term, isSelected, onSelect }) {
    return (
        <button
            type="button"
            className={`chip${isSelected ? ' active' : ''}`}
            aria-pressed={isSelected}
            onClick={() => onSelect(term)}
        >
            {term}
        </button>
    );
}

/**************************************************************/
/**
 * Renders the reverse-lookup tool from the live API.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Reverse-lookup panel.
 */
function ReverseLookupPanel({
    term,
    selectedTerms,
    suggestions,
    scopeProducts,
    result,
    isLoading,
    error,
    onTermChange,
    onSubmit,
    onPickSuggestion,
    onRemoveTerm,
}) {
    const scopeLabel = scopeProducts.length > 1
        ? `${scopeProducts.length} selected products`
        : scopeProducts[0]?.name ?? 'Selected product';
    const selectedTermKeys = new Set(selectedTerms.map((selectedTerm) => selectedTerm.toLowerCase()));
    const resultSymptomLabel = result?.symptoms?.length > 1
        ? `${result.symptoms.length} selected terms`
        : result?.symptom;

    return (
        <section className="panel" aria-labelledby="reverse-lookup-title">
            <div className="panel-header">
                <div className="panel-heading">
                    <div id="reverse-lookup-title" className="panel-title">Symptom reverse lookup</div>
                    <div className="panel-sub">Exact AE terms from loaded live data, scoped to {scopeLabel}.</div>
                </div>
            </div>

            <form
                className="search-wrap"
                onSubmit={(event) => {
                    event.preventDefault();
                    onSubmit(term);
                }}
            >
                <svg
                    aria-hidden="true"
                    className="search-icon"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                >
                    <circle cx="11" cy="11" r="7" />
                    <path d="m21 21-4.3-4.3" />
                </svg>
                <input
                    className="search-input"
                    type="search"
                    list="ae-term-suggestions"
                    value={term}
                    placeholder="Add exact AE term, for example Headache"
                    autoComplete="off"
                    spellCheck={false}
                    aria-label="Add reverse lookup adverse-event term"
                    onChange={(event) => onTermChange(event.target.value)}
                />
                <datalist id="ae-term-suggestions">
                    {suggestions.map((suggestion) => (
                        <option key={suggestion} value={suggestion} />
                    ))}
                </datalist>
            </form>

            {selectedTerms.length > 0 ? (
                <div className="filter-row rl-selected-row" aria-label="Selected reverse-lookup terms">
                    <span className="filter-label">Selected terms</span>
                    {selectedTerms.map((selectedTerm) => (
                        <button
                            key={selectedTerm}
                            type="button"
                            className="chip active removable"
                            aria-label={`Remove ${selectedTerm}`}
                            onClick={() => onRemoveTerm(selectedTerm)}
                        >
                            {selectedTerm}
                            <span className="chip-remove" aria-hidden="true">x</span>
                        </button>
                    ))}
                </div>
            ) : null}

            {suggestions.length > 0 ? (
                <div className="filter-row" aria-label="Loaded adverse-event term suggestions">
                    <span className="filter-label">Loaded terms</span>
                    {suggestions.slice(0, 8).map((suggestion) => (
                        <ReverseLookupSuggestion
                            key={suggestion}
                            term={suggestion}
                            isSelected={selectedTermKeys.has(suggestion.toLowerCase())}
                            onSelect={onPickSuggestion}
                        />
                    ))}
                </div>
            ) : null}

            {isLoading ? <Loading label="Running reverse lookup" /> : null}
            {error ? <InlineError error={error} /> : null}

            {!isLoading && !error && result ? (
                <div className="rl-results">
                    {result.allReassuring && result.matches.length > 0 ? (
                        <div className="rl-no-sig-banner">
                            All scoped matches are reassuring, protective, or low confidence for {resultSymptomLabel}.
                        </div>
                    ) : null}

                    {result.matches.length === 0 ? (
                        <div className="rl-empty">No scoped products matched {resultSymptomLabel || 'those terms'}.</div>
                    ) : null}

                    {result.matches.map((match) => {
                        const direction = getSignalDirection(match.signal);
                        const rowKey = `${match.drug.documentGuid}-${match.signal.id || match.signal.name}`;

                        return (
                            <div
                                key={rowKey}
                                className={`rl-row${match.signal.prec === 'fragile' ? ' fragile' : ''}`}
                            >
                                <div>
                                    <div className="rl-drug-name">{match.drug.name}</div>
                                    <div className="rl-drug-sub">{match.drug.generic} · {match.drug.pharmClass}</div>
                                </div>
                                <div className="rl-meta">
                                    <span className={`precision-pill ${match.signal.prec}`}>
                                        <span className="pip" />
                                        {match.signal.prec}
                                    </span>
                                    <span className="ae-tag rr">RR {formatDecimal(match.signal.rr, 2)}</span>
                                </div>
                                <div className="ae-body">
                                    <div className="ae-name">{match.signal.name}</div>
                                    <div className="ae-meta">
                                        <span className="ae-tag soc">{match.signal.soc}</span>
                                        <span className={`ae-tag ${direction === 'elevated' ? 'serious' : ''}`}>
                                            {getReverseLookupVerdictLabel(match.verdict)}
                                        </span>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            ) : null}
        </section>
    );
}

/**************************************************************/
/**
 * Renders a compact product select used by interchange controls.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Product select.
 */
function ProductInterchangeSelect({
    label,
    tone,
    align = 'left',
    selectedProduct,
    products,
    favoriteProducts,
    recentProducts,
    totalProductCount,
    disabledDocumentGuid,
    searchTerm,
    onSearchTermChange,
    onSelect,
    onToggleFavorite,
    favoriteBusyGuids,
    favoriteNotice,
    isLoading,
    error,
    onRetry,
}) {
    return (
        <div className="ic-picker">
            <div className={`ic-picker-label ${tone}`}>
                <span className="lbl-dot" aria-hidden="true" />
                {label}
            </div>
            <CompactProductPicker
                idPrefix={`interchange-${tone}`}
                tone={tone}
                align={align}
                products={products}
                favoriteProducts={favoriteProducts}
                recentProducts={recentProducts}
                totalProductCount={totalProductCount}
                selectedProduct={selectedProduct}
                disabledDocumentGuid={disabledDocumentGuid}
                searchTerm={searchTerm}
                onSearchTermChange={onSearchTermChange}
                onSelectProduct={onSelect}
                onToggleFavorite={onToggleFavorite}
                favoriteBusyGuids={favoriteBusyGuids}
                favoriteNotice={favoriteNotice}
                isLoading={isLoading}
                error={error}
                onRetry={onRetry}
            />
        </div>
    );
}

/**************************************************************/
/**
 * Renders one half-row in the interchange mini forest track.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Track half.
 */
function InterchangeTrackHalf({ signal, side, scaleDomain, productLabel }) {
    const direction = signal ? getSignalDirection(signal) : 'ns';
    const pointLeft = getForestXPercent(signal?.rr, scaleDomain);
    const tooltipLeft = pointLeft ?? 50;
    const tooltipSideClass = tooltipLeft > 68 ? ' is-left' : ' is-right';
    const ariaLabel = getInterchangeSignalAriaLabel(signal, productLabel);

    const lowerBoundValue = Number(signal?.rrL);
    const upperBoundValue = Number(signal?.rrH);
    const hasInterval = Number.isFinite(lowerBoundValue)
        && lowerBoundValue > 0
        && Number.isFinite(upperBoundValue)
        && upperBoundValue > 0;
    const intervalStartValue = hasInterval ? Math.min(lowerBoundValue, upperBoundValue) : null;
    const intervalEndValue = hasInterval ? Math.max(lowerBoundValue, upperBoundValue) : null;
    const lowerLeft = getForestXPercent(intervalStartValue, scaleDomain);
    const upperLeft = getForestXPercent(intervalEndValue, scaleDomain);
    const intervalWidth = lowerLeft !== null && upperLeft !== null
        ? Math.max(0, upperLeft - lowerLeft)
        : 0;

    return (
        <div
            className={`ic-track-half ${side}${direction === 'ns' ? ' ns' : ''}${signal ? ' has-context' : ''}`}
            role={signal ? 'img' : undefined}
            tabIndex={signal ? 0 : undefined}
            aria-label={ariaLabel}
        >
            {lowerLeft !== null && upperLeft !== null ? (
                <>
                    <div className="ic-ci" style={{ left: `${lowerLeft}%`, width: `${intervalWidth}%` }} />
                    <div className="ic-ci-cap" style={{ left: `${lowerLeft}%` }} />
                    <div className="ic-ci-cap" style={{ left: `${upperLeft}%` }} />
                </>
            ) : null}
            {pointLeft !== null ? <div className="ic-pt" style={{ left: `${pointLeft}%` }} /> : null}
            {signal ? (
                <InterchangeSignalTooltip
                    signal={signal}
                    productLabel={productLabel}
                    className={tooltipSideClass}
                    style={{ left: `${tooltipLeft}%` }}
                />
            ) : null}
        </div>
    );
}

/**************************************************************/
/**
 * Renders compact provenance metadata for one product's interchange signal.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Context item.
 */
function InterchangeSignalContext({ signal, side, label, productLabel }) {
    if (!signal) {
        return null;
    }

    return (
        <span className={`ic-context-item ${side}`} aria-label={getInterchangeSignalAriaLabel(signal, productLabel)}>
            <span className={`ic-comparator-badge ${side}`}>
                {label}: {getSignalComparatorLabel(signal, true)}
            </span>
            <span className="ic-context-text">{getInterchangeContextText(signal)}</span>
        </span>
    );
}

/**************************************************************/
/**
 * Renders the interchange row metadata line under the mini forest track.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element | null} Context line.
 */
function InterchangeContextLine({ row, productALabel, productBLabel }) {
    if (!row.signalA && !row.signalB) {
        return null;
    }

    return (
        <div className="ic-context-line">
            <InterchangeSignalContext signal={row.signalA} side="a" label="A" productLabel={productALabel} />
            <InterchangeSignalContext signal={row.signalB} side="b" label="B" productLabel={productBLabel} />
        </div>
    );
}

/**************************************************************/
/**
 * Renders the two-product interchange signal track.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Mini forest track.
 */
function InterchangeTrack({ row, scaleDomain, productALabel, productBLabel }) {
    const referenceLeft = getForestXPercent(1, scaleDomain);

    return (
        <div className="ic-track">
            <div className="ic-refline" style={{ left: `${referenceLeft}%` }} />
            <InterchangeTrackHalf
                signal={row.signalA}
                side="a"
                scaleDomain={scaleDomain}
                productLabel={productALabel}
            />
            <InterchangeTrackHalf
                signal={row.signalB}
                side="b"
                scaleDomain={scaleDomain}
                productLabel={productBLabel}
            />
        </div>
    );
}

/**************************************************************/
/**
 * Renders one interchange comparison row.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Interchange row.
 */
function InterchangeRow({ row, scaleDomain, productALabel, productBLabel }) {
    const deltaClass = getInterchangeDeltaClass(row.classification);

    return (
        <div className="ic-row">
            <div className="ic-name" title={row.parameterName}>
                {row.parameterName}
                <span className="sub">{row.parameterCategory || 'SOC not listed'}</span>
            </div>
            <div className="ic-plot">
                <InterchangeTrack
                    row={row}
                    scaleDomain={scaleDomain}
                    productALabel={productALabel}
                    productBLabel={productBLabel}
                />
                <InterchangeContextLine
                    row={row}
                    productALabel={productALabel}
                    productBLabel={productBLabel}
                />
            </div>
            <div className={`ic-delta ${deltaClass}`}>
                {row.deltaLabel}
            </div>
        </div>
    );
}

/**************************************************************/
/**
 * Renders grouped interchange rows and controls.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Interchange panel.
 */
function InterchangePanel({
    productA,
    productB,
    products,
    favoriteProducts,
    recentProducts,
    totalProductCount,
    productSearch,
    onProductSearchChange,
    onToggleFavorite,
    favoriteBusyGuids,
    favoriteNotice,
    isProductLoading,
    productError,
    onRetryProducts,
    sharedSignalsOnly,
    differencesOnly,
    comparison,
    isLoading,
    error,
    onChangeProductA,
    onChangeProductB,
    onToggleSharedSignalsOnly,
    onToggleDifferencesOnly,
    onRetry,
    comparatorFilter,
}) {
    const comparisonSignals = useMemo(() => getInterchangeSignals(comparison), [comparison]);
    const scaleDomain = useMemo(() => getForestScaleDomain(comparisonSignals), [comparisonSignals]);
    const axisTicks = useMemo(() => getForestTicks(scaleDomain, COMPACT_FOREST_TICKS), [scaleDomain]);
    const hasRunnablePair = Boolean(productA?.documentGuid && productB?.documentGuid)
        && productA.documentGuid.toLowerCase() !== productB.documentGuid.toLowerCase();
    const aConcernCount = comparison.aWorseCount + comparison.onlyACount;
    const bConcernCount = comparison.bWorseCount + comparison.onlyBCount;
    const isAllComparatorScope = comparatorFilter === 'all';
    const productALabel = productA?.name || 'Product A';
    const productBLabel = productB?.name || 'Product B';

    return (
        <section className="panel" aria-labelledby="interchange-title">
            <div className="panel-header">
                <div className="panel-heading">
                    <div id="interchange-title" className="panel-title">Therapeutic interchange comparison</div>
                    <div className="panel-sub">Compare API-derived AE signals across two dashboard products.</div>
                </div>
                <div className="panel-actions" aria-label="Interchange row filters">
                    <button
                        type="button"
                        className={`chip-toggle${sharedSignalsOnly ? ' on' : ''}`}
                        aria-pressed={sharedSignalsOnly}
                        onClick={onToggleSharedSignalsOnly}
                    >
                        <span className="sw" aria-hidden="true" />
                        Shared signals only
                    </button>
                    <button
                        type="button"
                        className={`chip-toggle${differencesOnly ? ' on' : ''}`}
                        aria-pressed={differencesOnly}
                        onClick={onToggleDifferencesOnly}
                    >
                        <span className="sw" aria-hidden="true" />
                        Differences only
                    </button>
                </div>
            </div>

            <div className="ic-pickers">
                <ProductInterchangeSelect
                    label="Product A"
                    tone="a"
                    selectedProduct={productA}
                    products={products}
                    favoriteProducts={favoriteProducts}
                    recentProducts={recentProducts}
                    totalProductCount={totalProductCount}
                    disabledDocumentGuid={productB?.documentGuid}
                    searchTerm={productSearch}
                    onSearchTermChange={onProductSearchChange}
                    onSelect={onChangeProductA}
                    onToggleFavorite={onToggleFavorite}
                    favoriteBusyGuids={favoriteBusyGuids}
                    favoriteNotice={favoriteNotice}
                    isLoading={isProductLoading}
                    error={productError}
                    onRetry={onRetryProducts}
                />
                <div className="ic-arrow" aria-hidden="true">→</div>
                <ProductInterchangeSelect
                    label="Product B"
                    tone="b"
                    align="right"
                    selectedProduct={productB}
                    products={products}
                    favoriteProducts={favoriteProducts}
                    recentProducts={recentProducts}
                    totalProductCount={totalProductCount}
                    disabledDocumentGuid={productA?.documentGuid}
                    searchTerm={productSearch}
                    onSearchTermChange={onProductSearchChange}
                    onSelect={onChangeProductB}
                    onToggleFavorite={onToggleFavorite}
                    favoriteBusyGuids={favoriteBusyGuids}
                    favoriteNotice={favoriteNotice}
                    isLoading={isProductLoading}
                    error={productError}
                    onRetry={onRetryProducts}
                />
            </div>

            {!hasRunnablePair ? (
                <EmptyState title="Choose two different products to compare." />
            ) : null}
            {isLoading ? <Loading label="Loading interchange comparison" /> : null}
            {error ? <InlineError error={error} onRetry={onRetry} /> : null}

            {!isLoading && !error && hasRunnablePair && comparison.rows.length > 0 ? (
                <>
                    {comparison.classMismatchWarning ? (
                        <div className="ic-warn">{comparison.classMismatchWarning}</div>
                    ) : null}
                    {comparison.comparatorMismatchWarning ? (
                        <div className="ic-warn">{comparison.comparatorMismatchWarning}</div>
                    ) : null}
                    {isAllComparatorScope ? (
                        <div className="ic-warn">
                            All comparator strata are included. Rows may summarize placebo and active-comparator evidence from different studies.
                        </div>
                    ) : null}

                    <div className="ic-summary">
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num a">{formatInteger(aConcernCount)}</div>
                            <div className="ic-summary-lbl">Less favorable or unique on product A</div>
                        </div>
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num">{formatInteger(comparison.similarCount)}</div>
                            <div className="ic-summary-lbl">Similar signal rows</div>
                        </div>
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num b">{formatInteger(bConcernCount)}</div>
                            <div className="ic-summary-lbl">Less favorable or unique on product B</div>
                        </div>
                    </div>

                    <div className="ic-axis">
                        <span>Adverse event</span>
                        <div className="ic-axis-ticks">
                            {axisTicks.map((tick) => {
                                const left = getForestXPercent(tick, scaleDomain);

                                if (left === null) {
                                    return null;
                                }

                                return (
                                    <span key={tick} className={`tk${tick === 1 ? ' ref' : ''}`} style={{ left: `${left}%` }}>
                                        {formatForestTick(tick)}
                                    </span>
                                );
                            })}
                        </div>
                        <span>Delta</span>
                    </div>

                    {INTERCHANGE_GROUPS.map((group) => {
                        const rows = comparison.rows.filter((row) => group.classes.has(row.classification));

                        if (rows.length === 0) {
                            return null;
                        }

                        return (
                            <div key={group.id}>
                                <div className="ic-divider">
                                    <span className="ic-divider-text">{group.label}</span>
                                    <span className="line" />
                                </div>
                                {rows.map((row) => (
                                    <InterchangeRow
                                        key={`${group.id}-${row.id}`}
                                        row={row}
                                        scaleDomain={scaleDomain}
                                        productALabel={productALabel}
                                        productBLabel={productBLabel}
                                    />
                                ))}
                            </div>
                        );
                    })}
                </>
            ) : null}

            {!isLoading && !error && hasRunnablePair && comparison.rows.length === 0 ? (
                <EmptyState title="No interchange rows match the current filter." />
            ) : null}
        </section>
    );
}

/**************************************************************/
/**
 * Renders the cross-product dashboard tools.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Cross-product tools.
 */
function CrossProductTools(props) {
    return (
        <>
            <div className="section-heading">
                <span className="section-heading-text">Cross-product tools</span>
                <span className="line" />
            </div>
            <ReverseLookupPanel
                term={props.reverseLookupTerm}
                selectedTerms={props.selectedReverseLookupTerms}
                suggestions={props.reverseLookupSuggestions}
                scopeProducts={props.reverseLookupScopeProducts}
                result={props.reverseLookupResult}
                isLoading={props.isReverseLookupLoading}
                error={props.reverseLookupError}
                onTermChange={props.onReverseLookupTermChange}
                onSubmit={props.onRunReverseLookup}
                onPickSuggestion={props.onPickReverseLookupSuggestion}
                onRemoveTerm={props.onRemoveReverseLookupTerm}
            />
            <InterchangePanel
                productA={props.productA}
                productB={props.productB}
                products={props.interchangeProducts}
                favoriteProducts={props.favoriteProducts}
                recentProducts={props.recentProducts}
                totalProductCount={props.totalProductCount}
                productSearch={props.productSearch}
                onProductSearchChange={props.onProductSearchChange}
                onToggleFavorite={props.onToggleFavorite}
                favoriteBusyGuids={props.favoriteBusyGuids}
                favoriteNotice={props.favoriteNotice}
                isProductLoading={props.isProductLoading}
                productError={props.productError}
                onRetryProducts={props.onRetryProducts}
                sharedSignalsOnly={props.sharedSignalsOnly}
                differencesOnly={props.differencesOnly}
                comparison={props.interchangeComparison}
                isLoading={props.isInterchangeLoading}
                error={props.interchangeError}
                onChangeProductA={props.onChangeInterchangeProductA}
                onChangeProductB={props.onChangeInterchangeProductB}
                onToggleSharedSignalsOnly={props.onToggleSharedSignalsOnly}
                onToggleDifferencesOnly={props.onToggleDifferencesOnly}
                onRetry={props.onRetryInterchange}
                comparatorFilter={props.comparatorFilter}
            />
        </>
    );
}

/**************************************************************/
/**
 * Renders the tabbed primary dashboard panel.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Dashboard panel.
 */
function DashboardPanel({
    activeView,
    comparatorFilter,
    showFragile,
    signalCounts,
    filteredTiers,
    forestView,
    quadrantView,
    isTriageLoading,
    triageError,
    onRetryTriage,
    isForestLoading,
    forestError,
    onRetryForest,
    isQuadrantLoading,
    quadrantError,
    onRetryQuadrant,
    onChangeView,
    onChangeComparator,
    onToggleFragile,
}) {
    // The selected view determines the panel title and active visualization.
    const viewCopy = VIEW_COPY[activeView] ?? VIEW_COPY.triage;

    // The active view determines which loading and error state should display.
    const activeState = {
        triage: { isLoading: isTriageLoading, error: triageError, onRetry: onRetryTriage },
        forest: { isLoading: isForestLoading, error: forestError, onRetry: onRetryForest },
        quadrant: { isLoading: isQuadrantLoading, error: quadrantError, onRetry: onRetryQuadrant },
    }[activeView];

    return (
        <section className="panel" data-screen-label="AE primary view">
            <div className="panel-header">
                <div className="panel-heading">
                    <div className="panel-title">{viewCopy.title}</div>
                    <div className="panel-sub">{viewCopy.subtitle}</div>
                </div>
                <div className="tabs" role="tablist" aria-label="Adverse-event dashboard views">
                    {Array.from(DASHBOARD_VIEWS).map((view) => (
                        <button
                            key={view}
                            type="button"
                            className={`tab${activeView === view ? ' active' : ''}`}
                            role="tab"
                            aria-selected={activeView === view}
                            onClick={() => onChangeView(view)}
                        >
                            {view === 'triage' ? 'Triage' : view === 'forest' ? 'Forest' : 'Quadrant'}
                        </button>
                    ))}
                </div>
            </div>

            <div className="filter-row">
                <span className="filter-label">Comparator</span>
                <button
                    type="button"
                    className={`chip${comparatorFilter === 'all' ? ' active' : ''}`}
                    onClick={() => onChangeComparator('all')}
                >
                    All ({formatInteger(signalCounts.all)})
                </button>
                <button
                    type="button"
                    className={`chip${comparatorFilter === 'placebo' ? ' active' : ''}`}
                    disabled={signalCounts.placebo === 0}
                    onClick={() => onChangeComparator('placebo')}
                >
                    Placebo ({formatInteger(signalCounts.placebo)})
                </button>
                <button
                    type="button"
                    className={`chip${comparatorFilter === 'active' ? ' active' : ''}`}
                    disabled={signalCounts.active === 0}
                    onClick={() => onChangeComparator('active')}
                >
                    Active comparator ({formatInteger(signalCounts.active)})
                </button>
                <button
                    type="button"
                    className={`chip-toggle${showFragile ? ' on' : ''}`}
                    onClick={onToggleFragile}
                >
                    <span className="sw" aria-hidden="true" />
                    Show fragile rows ({formatInteger(signalCounts.fragile)})
                </button>
            </div>

            {activeState.isLoading ? <Loading label={`Loading ${activeView}`} /> : null}
            {activeState.error ? <InlineError error={activeState.error} onRetry={activeState.onRetry} /> : null}
            {!activeState.isLoading && !activeState.error && activeView === 'triage' ? (
                <TriageView tiers={filteredTiers} />
            ) : null}
            {!activeState.isLoading && !activeState.error && activeView === 'forest' ? (
                <ForestView signals={forestView.signals} />
            ) : null}
            {!activeState.isLoading && !activeState.error && activeView === 'quadrant' ? (
                <QuadrantView points={quadrantView.points} />
            ) : null}
        </section>
    );
}

/**************************************************************/
/**
 * Main AE dashboard React island.
 *
 * @returns {JSX.Element} Dashboard application.
 */
function App() {
    // The parsed URL state is stable for the first selection pass.
    const [initialUrlState] = useState(readDashboardUrlState);

    // Product search text is controlled here and debounced in the products hook.
    const [productSearch, setProductSearch] = useState('');

    // The dashboard starts in product focus unless a class URL explicitly opts in.
    const [dashboardFocus, setDashboardFocus] = useState(initialUrlState.focus);

    // Class picker search is debounced before it reaches the class endpoint.
    const [classSearch, setClassSearch] = useState('');
    const debouncedClassSearch = useDebouncedValue(classSearch, 250);

    // Selected product drives the header and KPI strip.
    const [selectedProduct, setSelectedProduct] = useState(null);

    // Selected class drives the class header and correlation surface.
    const [selectedClass, setSelectedClass] = useState(null);

    // URL-selected class codes are resolved through the class picker endpoint.
    const [pendingClassCode, setPendingClassCode] = useState(initialUrlState.classCode);

    // Hydration errors are specific to off-page URL-selected products.
    const [hydrationError, setHydrationError] = useState(null);

    // Active visualization view starts from URL state.
    const [activeView, setActiveView] = useState(initialUrlState.view);

    // Comparator filter starts from URL state.
    const [comparatorFilter, setComparatorFilter] = useState(initialUrlState.comparator);

    // Fragile-row visibility starts from URL state.
    const [showFragile, setShowFragile] = useState(initialUrlState.fragile);

    // Class-mode view and filters start from URL state.
    const [classView, setClassView] = useState(initialUrlState.classView);
    const [classComparator, setClassComparator] = useState(initialUrlState.classComparator);
    const [includeNonSignificant, setIncludeNonSignificant] = useState(initialUrlState.includeNonSignificant);
    const [excludeFragileRows, setExcludeFragileRows] = useState(initialUrlState.excludeFragile);
    const [minDrugsPerCell, setMinDrugsPerCell] = useState(initialUrlState.minDrugsPerCell);
    const [correlationMethod, setCorrelationMethod] = useState(initialUrlState.method);
    const [correlationAggregation, setCorrelationAggregation] = useState(initialUrlState.aggregation);
    const [excludeCombos, setExcludeCombos] = useState(initialUrlState.excludeCombos);
    const [minEvents, setMinEvents] = useState(initialUrlState.minEvents);

    // Class picker payload is independent from the product catalog.
    const [correlationClasses, setCorrelationClasses] = useState([]);
    const [correlationClassTotalCount, setCorrelationClassTotalCount] = useState(0);
    const [correlationClassChartableCount, setCorrelationClassChartableCount] = useState(0);
    const [isClassPickerLoading, setIsClassPickerLoading] = useState(false);
    const [classPickerError, setClassPickerError] = useState(null);
    const [classPickerReloadToken, setClassPickerReloadToken] = useState(0);

    // Class correlation payloads are loaded separately so map and heatmap errors stay local.
    const [classMap, setClassMap] = useState(null);
    const [isClassMapLoading, setIsClassMapLoading] = useState(false);
    const [classMapError, setClassMapError] = useState(null);
    const [classMapReloadToken, setClassMapReloadToken] = useState(0);
    const [classHeatmap, setClassHeatmap] = useState(null);
    const [isClassHeatmapLoading, setIsClassHeatmapLoading] = useState(false);
    const [classHeatmapError, setClassHeatmapError] = useState(null);
    const [classHeatmapReloadToken, setClassHeatmapReloadToken] = useState(0);
    const [selectedCorrelationCell, setSelectedCorrelationCell] = useState(null);
    const [classCellDetail, setClassCellDetail] = useState(null);
    const [isClassCellLoading, setIsClassCellLoading] = useState(false);
    const [classCellError, setClassCellError] = useState(null);
    const [classCellReloadToken, setClassCellReloadToken] = useState(0);

    // System-mode picker search is debounced before it reaches the system endpoint.
    const [systemSearch, setSystemSearch] = useState(initialUrlState.systemSearch);
    const debouncedSystemSearch = useDebouncedValue(systemSearch, 250);

    // Selected systems can come from repeated URL values before picker metadata loads.
    const [selectedSystems, setSelectedSystems] = useState(
        () => initialUrlState.systems.map((system) => ({
            id: system,
            systemOrganClass: system,
            classCount: 0,
            drugCount: 0,
            termCount: 0,
            usableMapCellCount: 0,
            maxPairCount: 0,
            hasRenderableMap: false,
            renderabilityReason: '',
        })),
    );

    // System-mode view, filters, and paging start from URL state.
    const [systemView, setSystemView] = useState(initialUrlState.systemView);
    const [systemComparator, setSystemComparator] = useState(initialUrlState.systemComparator);
    const [systemIncludeNonSignificant, setSystemIncludeNonSignificant] =
        useState(initialUrlState.systemIncludeNonSignificant);
    const [systemExcludeFragile, setSystemExcludeFragile] = useState(initialUrlState.systemExcludeFragile);
    const [systemExcludeCombos, setSystemExcludeCombos] = useState(initialUrlState.systemExcludeCombos);
    const [minTermsPerCell, setMinTermsPerCell] = useState(initialUrlState.minTermsPerCell);
    const [systemMethod, setSystemMethod] = useState(initialUrlState.systemMethod);
    const [systemAggregation, setSystemAggregation] = useState(initialUrlState.systemAggregation);
    const [systemMinEvents, setSystemMinEvents] = useState(initialUrlState.systemMinEvents);
    const [systemClassPageNumber, setSystemClassPageNumber] = useState(initialUrlState.systemClassPageNumber);
    const [systemClassPageSize, setSystemClassPageSize] = useState(initialUrlState.systemClassPageSize);
    const [systemDrugPageNumber, setSystemDrugPageNumber] = useState(initialUrlState.systemDrugPageNumber);
    const [systemDrugPageSize, setSystemDrugPageSize] = useState(initialUrlState.systemDrugPageSize);
    const [systemTermPairPageNumber, setSystemTermPairPageNumber] =
        useState(initialUrlState.systemTermPairPageNumber);
    const [systemTermPairPageSize, setSystemTermPairPageSize] =
        useState(initialUrlState.systemTermPairPageSize);
    const [systemFullMatrix, setSystemFullMatrix] = useState(initialUrlState.systemFullMatrix);

    // System picker payload is independent from the class picker and product catalog.
    const [correlationSystems, setCorrelationSystems] = useState([]);
    const [correlationSystemTotalCount, setCorrelationSystemTotalCount] = useState(0);
    const [correlationSystemChartableCount, setCorrelationSystemChartableCount] = useState(0);
    const [isSystemPickerLoading, setIsSystemPickerLoading] = useState(false);
    const [systemPickerError, setSystemPickerError] = useState(null);
    const [systemPickerReloadToken, setSystemPickerReloadToken] = useState(0);

    // System correlation payloads are loaded separately so map, heatmap, and detail errors stay local.
    const [systemMap, setSystemMap] = useState(null);
    const [isSystemMapLoading, setIsSystemMapLoading] = useState(false);
    const [systemMapError, setSystemMapError] = useState(null);
    const [systemMapReloadToken, setSystemMapReloadToken] = useState(0);
    const [systemHeatmap, setSystemHeatmap] = useState(null);
    const [isSystemHeatmapLoading, setIsSystemHeatmapLoading] = useState(false);
    const [systemHeatmapError, setSystemHeatmapError] = useState(null);
    const [systemHeatmapReloadToken, setSystemHeatmapReloadToken] = useState(0);
    const [selectedSystemCell, setSelectedSystemCell] = useState(null);
    const [systemCellDetail, setSystemCellDetail] = useState(null);
    const [isSystemCellLoading, setIsSystemCellLoading] = useState(false);
    const [systemCellError, setSystemCellError] = useState(null);
    const [systemCellReloadToken, setSystemCellReloadToken] = useState(0);

    // Triage payload stores the tiered flagship view.
    const [triageView, setTriageView] = useState({ product: null, tiers: [] });

    // Triage loading is independent from product search loading.
    const [isTriageLoading, setIsTriageLoading] = useState(false);

    // Triage errors are recoverable through retry.
    const [triageError, setTriageError] = useState(null);

    // Forest payload stores chart-ready signals and server ticks for API compatibility.
    const [forestView, setForestView] = useState({ signals: [], axisTicks: DEFAULT_FOREST_TICKS });

    // Forest loading runs only when the tab is requested.
    const [isForestLoading, setIsForestLoading] = useState(false);

    // Forest errors are recoverable through retry.
    const [forestError, setForestError] = useState(null);

    // Quadrant payload stores chart-ready points.
    const [quadrantView, setQuadrantView] = useState({ points: [] });

    // Quadrant loading runs only when the tab is requested.
    const [isQuadrantLoading, setIsQuadrantLoading] = useState(false);

    // Quadrant errors are recoverable through retry.
    const [quadrantError, setQuadrantError] = useState(null);

    // Reload tokens allow retry buttons to re-run active requests.
    const [reloadTokens, setReloadTokens] = useState({ triage: 0, forest: 0, quadrant: 0 });

    // Reverse lookup term is exact-match because the API intentionally does not do substring search.
    const [reverseLookupTerm, setReverseLookupTerm] = useState('');

    // Reverse lookup supports several exact terms while de-duplicating casing and repeats.
    const [selectedReverseLookupTerms, setSelectedReverseLookupTerms] = useState([]);

    // Reverse lookup result stores scoped live matches.
    const [reverseLookupResult, setReverseLookupResult] = useState(null);

    // Reverse lookup loading is independent from the main tabs.
    const [isReverseLookupLoading, setIsReverseLookupLoading] = useState(false);

    // Reverse lookup validation and request errors render inside the panel.
    const [reverseLookupError, setReverseLookupError] = useState(null);

    // Product B is local to the interchange tool; product A follows the selected dashboard product.
    const [interchangeProductB, setInterchangeProductB] = useState(null);

    // Differences-only is sent to the API so the server remains the comparison authority.
    const [differencesOnly, setDifferencesOnly] = useState(false);

    // Shared-signals-only is server-side so counts match the rendered row set.
    const [sharedSignalsOnly, setSharedSignalsOnly] = useState(false);

    // Interchange comparison stores normalized rows/counts/warnings.
    const [interchangeComparison, setInterchangeComparison] = useState(EMPTY_INTERCHANGE_VIEW);

    // Interchange loading is independent from tab loading.
    const [isInterchangeLoading, setIsInterchangeLoading] = useState(false);

    // Interchange request errors are recoverable through retry.
    const [interchangeError, setInterchangeError] = useState(null);

    // Interchange retry uses a token so the selected products stay unchanged.
    const [interchangeReloadToken, setInterchangeReloadToken] = useState(0);

    // Initial selection should happen once after products or deep-link hydration become available.
    const hasResolvedInitialSelectionRef = useRef(false);

    // System default selection should happen only once; removing the last chip must stay empty.
    const hasResolvedInitialSystemSelectionRef = useRef(initialUrlState.systems.length > 0);

    // Product catalog state comes from the live API.
    const {
        products,
        productsByGuid,
        totalProductCount,
        isLoading: isProductLoading,
        error: productError,
        refresh: refreshProducts,
        updateProduct,
    } = useProducts(productSearch);

    // Favorite state is API-backed when the caller has policy access.
    const {
        favoriteProducts,
        favoriteGuids,
        favoriteNotice,
        busyDocumentGuids,
        toggleFavorite,
    } = useFavorites();

    // Recents stay local because the backend plan intentionally kept them client-side.
    const { recentProducts, recordRecentProduct } = useRecents();

    // Visible products use favorite lookup as an overlay without changing source API data.
    const visibleProducts = products.map((product) => applyFavoriteLookup(product, favoriteGuids));

    // Visible favorites also carry their own favorite state for row rendering.
    const visibleFavoriteProducts = favoriteProducts.map((product) => ({
        ...product,
        isFavorite: true,
    }));

    // Recents are overlaid with live favorite state so their star renders correctly
    // and toggling a recent computes the right next state.
    const visibleRecentProducts = recentProducts.map((product) =>
        applyFavoriteLookup(product, favoriteGuids),
    );

    // A 503 from any AE dashboard endpoint means the whole dashboard should render the disabled state.
    const isFeatureDisabled =
        (productError instanceof ApiError && productError.isFeatureDisabled)
        || (classPickerError instanceof ApiError && classPickerError.isFeatureDisabled)
        || (classMapError instanceof ApiError && classMapError.isFeatureDisabled)
        || (classHeatmapError instanceof ApiError && classHeatmapError.isFeatureDisabled)
        || (classCellError instanceof ApiError && classCellError.isFeatureDisabled)
        || (systemPickerError instanceof ApiError && systemPickerError.isFeatureDisabled)
        || (systemMapError instanceof ApiError && systemMapError.isFeatureDisabled)
        || (systemHeatmapError instanceof ApiError && systemHeatmapError.isFeatureDisabled)
        || (systemCellError instanceof ApiError && systemCellError.isFeatureDisabled);

    // Favorite state is overlaid at render time so async favorite hydration updates the header.
    const selectedProductWithFavoriteState = selectedProduct
        ? applyFavoriteLookup(selectedProduct, favoriteGuids)
        : null;

    // The selected document GUID is the shared key for visualization requests.
    const selectedDocumentGuid = selectedProductWithFavoriteState?.documentGuid ?? '';

    // The selected class code is the shared key for class-mode requests.
    const selectedClassCode = selectedClass?.pharmClassCode ?? '';

    // Class filters are grouped so requests, URL state, and export stay aligned.
    const classFilters = useMemo(
        () => ({
            comparator: classComparator,
            includeNonSignificant,
            excludeFragile: excludeFragileRows,
            minDrugsPerCell,
            method: correlationMethod,
            aggregation: correlationAggregation,
            excludeCombos,
            minEvents,
        }),
        [
            classComparator,
            correlationAggregation,
            correlationMethod,
            excludeCombos,
            excludeFragileRows,
            includeNonSignificant,
            minDrugsPerCell,
            minEvents,
        ],
    );

    // System filters are grouped separately so minTermsPerCell never leaks into class mode.
    const systemFilters = useMemo(
        () => ({
            comparator: systemComparator,
            includeNonSignificant: systemIncludeNonSignificant,
            excludeFragile: systemExcludeFragile,
            minTermsPerCell,
            method: systemMethod,
            aggregation: systemAggregation,
            excludeCombos: systemExcludeCombos,
            minEvents: systemMinEvents,
        }),
        [
            minTermsPerCell,
            systemAggregation,
            systemComparator,
            systemExcludeCombos,
            systemExcludeFragile,
            systemIncludeNonSignificant,
            systemMethod,
            systemMinEvents,
        ],
    );

    // System requests use canonical names, not picker object identity.
    const selectedSystemNames = useMemo(
        () => selectedSystems.map((system) => system.systemOrganClass).filter(Boolean),
        [selectedSystems],
    );

    // Fallback page metadata gives controls stable dimensions before the first response arrives.
    const systemMapClassPage = useMemo(
        () => ({
            pageNumber: systemClassPageNumber,
            pageSize: systemClassPageSize,
            totalCount: systemMap?.classPage?.totalCount ?? 0,
            totalPages: systemMap?.classPage?.totalPages ?? 1,
            hasPreviousPage: systemClassPageNumber > 1,
            hasNextPage: Boolean(systemMap?.classPage?.hasNextPage),
        }),
        [systemClassPageNumber, systemClassPageSize, systemMap?.classPage],
    );
    const systemHeatmapClassPage = useMemo(
        () => ({
            pageNumber: systemClassPageNumber,
            pageSize: systemClassPageSize,
            totalCount: systemHeatmap?.classPage?.totalCount ?? 0,
            totalPages: systemHeatmap?.classPage?.totalPages ?? 1,
            hasPreviousPage: systemClassPageNumber > 1,
            hasNextPage: Boolean(systemHeatmap?.classPage?.hasNextPage),
        }),
        [systemClassPageNumber, systemClassPageSize, systemHeatmap?.classPage],
    );
    const systemHeatmapDrugPage = useMemo(
        () => ({
            pageNumber: systemDrugPageNumber,
            pageSize: systemDrugPageSize,
            totalCount: systemHeatmap?.drugPage?.totalCount ?? 0,
            totalPages: systemHeatmap?.drugPage?.totalPages ?? 1,
            hasPreviousPage: systemDrugPageNumber > 1,
            hasNextPage: Boolean(systemHeatmap?.drugPage?.hasNextPage),
        }),
        [systemDrugPageNumber, systemDrugPageSize, systemHeatmap?.drugPage],
    );
    const systemTermPairPage = useMemo(
        () => ({
            pageNumber: systemTermPairPageNumber,
            pageSize: systemTermPairPageSize,
            totalCount: systemCellDetail?.termPairPage?.totalCount ?? 0,
            totalPages: systemCellDetail?.termPairPage?.totalPages ?? 1,
            hasPreviousPage: systemTermPairPageNumber > 1,
            hasNextPage: Boolean(systemCellDetail?.termPairPage?.hasNextPage),
        }),
        [systemCellDetail?.termPairPage, systemTermPairPageNumber, systemTermPairPageSize],
    );

    // Class picker searches use an initial URL code until that code is resolved.
    const effectiveClassSearch = pendingClassCode && !selectedClassCode
        ? pendingClassCode
        : debouncedClassSearch;

    // Product A follows the selected dashboard product so the comparison stays contextual.
    const interchangeProductA = selectedProductWithFavoriteState;

    // Cross-product controls reuse every product row the app has already hydrated.
    const interchangeProducts = useMemo(
        () => buildUniqueProductList(
            selectedProductWithFavoriteState ? [selectedProductWithFavoriteState] : [],
            visibleFavoriteProducts,
            visibleRecentProducts,
            visibleProducts,
        ),
        [selectedProductWithFavoriteState, visibleFavoriteProducts, visibleProducts, visibleRecentProducts],
    );

    // Product B falls back to the first available product that is not product A.
    const effectiveInterchangeProductB = useMemo(() => {
        if (!interchangeProductA?.documentGuid) {
            return null;
        }

        const productAKey = interchangeProductA.documentGuid.toLowerCase();
        const productBKey = interchangeProductB?.documentGuid?.toLowerCase() ?? '';

        if (productBKey && productBKey !== productAKey) {
            const refreshedProductB = interchangeProducts.find(
                (product) => product.documentGuid.toLowerCase() === productBKey,
            );

            if (refreshedProductB) {
                return refreshedProductB;
            }
        }

        return interchangeProducts.find(
            (product) => product.documentGuid.toLowerCase() !== productAKey,
        ) ?? null;
    }, [interchangeProductA, interchangeProductB, interchangeProducts]);

    // Reverse lookup scopes to the selected regimen products.
    const reverseLookupScopeProducts = useMemo(
        () => buildUniqueProductList(
            interchangeProductA ? [interchangeProductA] : [],
            effectiveInterchangeProductB ? [effectiveInterchangeProductB] : [],
        ),
        [effectiveInterchangeProductB, interchangeProductA],
    );

    // Interchange requests are keyed by stable GUIDs, not render-fresh product objects.
    const interchangeDocumentGuidA = interchangeProductA?.documentGuid ?? '';
    const interchangeDocumentGuidB = effectiveInterchangeProductB?.documentGuid ?? '';

    // Flat triage signals feed counts, exports, and chip labels.
    const triageSignals = useMemo(() => flattenTriageSignals(triageView.tiers), [triageView.tiers]);

    // Interchange rows can add more loaded terms once the comparison has run.
    const interchangeSignals = useMemo(() => getInterchangeSignals(interchangeComparison), [interchangeComparison]);

    // Exact-term suggestions come only from loaded live payloads.
    const reverseLookupSuggestions = useMemo(
        () => buildAeTermSuggestions(triageSignals, interchangeSignals),
        [interchangeSignals, triageSignals],
    );

    // Signal counts update whenever the full triage payload changes.
    const signalCounts = useMemo(() => countSignals(triageSignals), [triageSignals]);

    // Triage tier filters are client-side so comparator chip counts stay visible.
    const filteredTiers = useMemo(
        () => filterTriageTiers(triageView.tiers, comparatorFilter, showFragile),
        [comparatorFilter, showFragile, triageView.tiers],
    );

    /**************************************************************/
    /**
     * Writes the current product and class state to the URL with targeted overrides.
     *
     * @param {object} overrides - State values to override.
     * @param {boolean} shouldPush - Whether to push a history entry.
     */
    const writeCurrentDashboardUrlState = useCallback(
        (overrides = {}, shouldPush = false) => {
            writeDashboardUrlState(
                {
                    focus: dashboardFocus,
                    productGuid: selectedDocumentGuid,
                    view: activeView,
                    comparator: comparatorFilter,
                    fragile: showFragile,
                    classCode: selectedClassCode,
                    classView,
                    classComparator: classFilters.comparator,
                    includeNonSignificant: classFilters.includeNonSignificant,
                    excludeFragile: classFilters.excludeFragile,
                    minDrugsPerCell: classFilters.minDrugsPerCell,
                    method: classFilters.method,
                    aggregation: classFilters.aggregation,
                    excludeCombos: classFilters.excludeCombos,
                    minEvents: classFilters.minEvents,
                    systems: selectedSystemNames,
                    systemSearch,
                    systemView,
                    systemComparator: systemFilters.comparator,
                    systemIncludeNonSignificant: systemFilters.includeNonSignificant,
                    systemExcludeFragile: systemFilters.excludeFragile,
                    systemExcludeCombos: systemFilters.excludeCombos,
                    minTermsPerCell: systemFilters.minTermsPerCell,
                    systemMethod: systemFilters.method,
                    systemAggregation: systemFilters.aggregation,
                    systemMinEvents: systemFilters.minEvents,
                    systemClassPageNumber,
                    systemClassPageSize,
                    systemDrugPageNumber,
                    systemDrugPageSize,
                    systemTermPairPageNumber,
                    systemTermPairPageSize,
                    systemFullMatrix,
                    ...overrides,
                },
                shouldPush,
            );
        },
        [
            activeView,
            classFilters,
            classView,
            comparatorFilter,
            dashboardFocus,
            selectedClassCode,
            selectedDocumentGuid,
            selectedSystemNames,
            showFragile,
            systemClassPageNumber,
            systemClassPageSize,
            systemDrugPageNumber,
            systemDrugPageSize,
            systemFilters,
            systemFullMatrix,
            systemSearch,
            systemTermPairPageNumber,
            systemTermPairPageSize,
            systemView,
        ],
    );

    useEffect(() => {
        // Avoid repeating initial URL/product resolution on every catalog refresh.
        if (hasResolvedInitialSelectionRef.current) {
            return;
        }

        // Wait until the first catalog request finishes before choosing a fallback product.
        if (isProductLoading) {
            return;
        }

        // Do not attempt normal selection when the feature is disabled.
        if (isFeatureDisabled) {
            return;
        }

        // The initial product may be on the loaded page or require triage hydration.
        const initialProductGuid = initialUrlState.productGuid;

        /**************************************************************/
        /**
         * Resolves the first selected product.
         */
        async function resolveInitialSelection() {
            hasResolvedInitialSelectionRef.current = true;

            // URL product takes priority over default first-row selection.
            if (initialProductGuid) {
                // Catalog lookup handles normal on-page deep links.
                const catalogProduct = productsByGuid.get(initialProductGuid.toLowerCase());

                // A loaded catalog product can be selected immediately.
                if (catalogProduct) {
                    const productWithFavoriteState = applyFavoriteLookup(catalogProduct, favoriteGuids);
                    setSelectedProduct(productWithFavoriteState);
                    recordRecentProduct(productWithFavoriteState);
                    writeDashboardUrlState(
                        {
                            ...initialUrlState,
                            productGuid: productWithFavoriteState.documentGuid,
                        },
                        false,
                    );
                    return;
                }

                try {
                    // There is no GET /products/{guid}; triage carries product context.
                    const triagePayload = await AdverseEventClient.getTriage(initialProductGuid, {
                        comparator: 'all',
                        includeFragile: true,
                    });

                    // The normalized triage product supplies the header and KPI strip.
                    const { product } = normalizeTriage(triagePayload);

                    // A missing product is treated like a not-dashboard-ready route.
                    if (!product) {
                        throw new Error('The linked product is not available in the AE dashboard.');
                    }

                    const productWithFavoriteState = applyFavoriteLookup(product, favoriteGuids);
                    setSelectedProduct(productWithFavoriteState);
                    updateProduct(productWithFavoriteState);
                    recordRecentProduct(productWithFavoriteState);
                    setHydrationError(null);
                    writeDashboardUrlState(
                        {
                            ...initialUrlState,
                            productGuid: productWithFavoriteState.documentGuid,
                        },
                        false,
                    );
                    return;
                } catch (requestError) {
                    // Hydration failures clear stale URL selection and fall through to first product.
                    setHydrationError(requestError);
                }
            }

            // The first loaded product is the default initial selection.
            const firstProduct = visibleProducts[0];

            // Empty catalogs leave the dashboard in an empty state.
            if (!firstProduct) {
                writeDashboardUrlState(
                    {
                        ...initialUrlState,
                        productGuid: '',
                    },
                    false,
                );
                return;
            }

            setSelectedProduct(firstProduct);
            recordRecentProduct(firstProduct);
            writeDashboardUrlState(
                {
                    ...initialUrlState,
                    productGuid: firstProduct.documentGuid,
                },
                false,
            );
        }

        resolveInitialSelection();
    }, [
        favoriteGuids,
        initialUrlState,
        isFeatureDisabled,
        isProductLoading,
        productsByGuid,
        recordRecentProduct,
        updateProduct,
        visibleProducts,
    ]);

    useEffect(() => {
        // Class data is quiet until class focus is visible or a class deep link needs hydration.
        if (dashboardFocus !== 'class' && !pendingClassCode) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads class picker rows from the live correlation endpoint.
         */
        async function loadCorrelationClasses() {
            setIsClassPickerLoading(true);
            setClassPickerError(null);

            try {
                const payload = await AdverseEventClient.getCorrelationClasses({
                    classSearch: effectiveClassSearch,
                    pageNumber: 1,
                    pageSize: 50,
                    comparator: classFilters.comparator,
                    includeNonSignificant: classFilters.includeNonSignificant,
                    excludeFragile: classFilters.excludeFragile,
                    excludeCombos: classFilters.excludeCombos,
                    minEvents: classFilters.minEvents,
                    minDrugsPerCell: classFilters.minDrugsPerCell,
                    signal: abortController.signal,
                });
                const normalizedPage = normalizeCorrelationClassPage(payload);
                const normalizedClasses = normalizedPage.items;

                setCorrelationClasses(normalizedClasses);
                setCorrelationClassTotalCount(normalizedPage.totalCount);
                setCorrelationClassChartableCount(normalizedPage.chartableCount);

                if (!selectedClassCode && normalizedClasses.length > 0) {
                    const pendingLookupKey = pendingClassCode.trim().toLowerCase();
                    const exactClass = pendingLookupKey
                        ? normalizedClasses.find((item) => item.pharmClassCode.toLowerCase() === pendingLookupKey)
                        : null;
                    const fallbackClass = normalizedClasses.find((item) => item.hasRenderableMap)
                        ?? normalizedClasses[0];
                    const nextClass = exactClass ?? fallbackClass;

                    if (nextClass) {
                        setSelectedClass(nextClass);
                        setPendingClassCode('');
                        setSelectedCorrelationCell(null);
                        setClassCellDetail(null);
                        writeDashboardUrlState(
                            {
                                focus: dashboardFocus,
                                classCode: nextClass.pharmClassCode,
                            },
                            false,
                        );
                    }
                }
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setCorrelationClasses([]);
                setCorrelationClassTotalCount(0);
                setCorrelationClassChartableCount(0);
                setClassPickerError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsClassPickerLoading(false);
                }
            }
        }

        loadCorrelationClasses();

        return () => {
            abortController.abort();
        };
    }, [
        classPickerReloadToken,
        classFilters,
        dashboardFocus,
        effectiveClassSearch,
        pendingClassCode,
        selectedClassCode,
    ]);

    useEffect(() => {
        if (dashboardFocus !== 'class' || !selectedClassCode) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the class-scoped SOC by SOC correlation map.
         */
        async function loadClassMap() {
            setIsClassMapLoading(true);
            setClassMapError(null);

            try {
                const payload = await AdverseEventClient.getCorrelationMap({
                    pharmClassCode: selectedClassCode,
                    ...classFilters,
                    signal: abortController.signal,
                });

                setClassMap(normalizeCorrelationMap(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setClassMap(null);
                setClassMapError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsClassMapLoading(false);
                }
            }
        }

        loadClassMap();

        return () => {
            abortController.abort();
        };
    }, [classFilters, classMapReloadToken, dashboardFocus, selectedClassCode]);

    useEffect(() => {
        if (dashboardFocus !== 'class' || classView !== 'heatmap' || !selectedClassCode) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the class-scoped SOC by drug heatmap.
         */
        async function loadClassHeatmap() {
            setIsClassHeatmapLoading(true);
            setClassHeatmapError(null);

            try {
                const payload = await AdverseEventClient.getCorrelationHeatmap({
                    pharmClassCode: selectedClassCode,
                    comparator: classFilters.comparator,
                    includeNonSignificant: classFilters.includeNonSignificant,
                    excludeFragile: classFilters.excludeFragile,
                    aggregation: classFilters.aggregation,
                    excludeCombos: classFilters.excludeCombos,
                    minEvents: classFilters.minEvents,
                    signal: abortController.signal,
                });

                setClassHeatmap(normalizeCorrelationHeatmap(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setClassHeatmap(null);
                setClassHeatmapError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsClassHeatmapLoading(false);
                }
            }
        }

        loadClassHeatmap();

        return () => {
            abortController.abort();
        };
    }, [classFilters, classHeatmapReloadToken, classView, dashboardFocus, selectedClassCode]);

    useEffect(() => {
        if (
            dashboardFocus !== 'class'
            || classView !== 'map'
            || !selectedClassCode
            || !selectedCorrelationCell
            || selectedCorrelationCell.isDiagonal
        ) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads per-drug detail for the selected correlation map cell.
         */
        async function loadClassCellDetail() {
            setIsClassCellLoading(true);
            setClassCellError(null);

            try {
                const payload = await AdverseEventClient.getCorrelationCell({
                    pharmClassCode: selectedClassCode,
                    socX: selectedCorrelationCell.rowSoc,
                    socY: selectedCorrelationCell.columnSoc,
                    ...classFilters,
                    signal: abortController.signal,
                });

                setClassCellDetail(normalizeCorrelationCellDetail(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setClassCellDetail(null);
                setClassCellError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsClassCellLoading(false);
                }
            }
        }

        loadClassCellDetail();

        return () => {
            abortController.abort();
        };
    }, [
        classCellReloadToken,
        classFilters,
        classView,
        dashboardFocus,
        selectedClassCode,
        selectedCorrelationCell,
    ]);

    useEffect(() => {
        // System data is quiet until system focus is visible or URL-selected systems need enrichment.
        if (dashboardFocus !== 'system' && selectedSystemNames.length === 0) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads MedDRA system picker rows from the live inverse-correlation endpoint.
         */
        async function loadCorrelationSystems() {
            setIsSystemPickerLoading(true);
            setSystemPickerError(null);

            try {
                const payload = await AdverseEventClient.getCorrelationSystems({
                    systemSearch: debouncedSystemSearch,
                    pageNumber: 1,
                    pageSize: 50,
                    comparator: systemFilters.comparator,
                    includeNonSignificant: systemFilters.includeNonSignificant,
                    excludeFragile: systemFilters.excludeFragile,
                    excludeCombos: systemFilters.excludeCombos,
                    minEvents: systemFilters.minEvents,
                    minTermsPerCell: systemFilters.minTermsPerCell,
                    signal: abortController.signal,
                });
                const normalizedPage = normalizeCorrelationSystemPage(payload);
                const normalizedSystems = normalizedPage.items;

                setCorrelationSystems(normalizedSystems);
                setCorrelationSystemTotalCount(normalizedPage.totalCount);
                setCorrelationSystemChartableCount(normalizedPage.chartableCount);

                setSelectedSystems((currentSystems) => {
                    if (
                        currentSystems.length === 0
                        && normalizedSystems.length > 0
                        && !hasResolvedInitialSystemSelectionRef.current
                    ) {
                        hasResolvedInitialSystemSelectionRef.current = true;
                        const nextSystem = normalizedSystems.find((item) => item.hasRenderableMap)
                            ?? normalizedSystems[0];

                        writeDashboardUrlState(
                            {
                                ...readDashboardUrlState(),
                                focus: dashboardFocus,
                                systems: [nextSystem.systemOrganClass],
                            },
                            false,
                        );

                        return [nextSystem];
                    }

                    const byName = new Map(
                        normalizedSystems.map((system) => [system.systemOrganClass.toLowerCase(), system]),
                    );
                    let didChange = false;
                    const enrichedSystems = currentSystems.map((system) => {
                        const enrichedSystem = byName.get(system.systemOrganClass.toLowerCase());

                        if (!enrichedSystem) {
                            return system;
                        }

                        if (enrichedSystem === system) {
                            return system;
                        }

                        didChange = true;
                        return enrichedSystem;
                    });

                    return didChange ? enrichedSystems : currentSystems;
                });
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setCorrelationSystems([]);
                setCorrelationSystemTotalCount(0);
                setCorrelationSystemChartableCount(0);
                setSystemPickerError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsSystemPickerLoading(false);
                }
            }
        }

        loadCorrelationSystems();

        return () => {
            abortController.abort();
        };
    }, [
        dashboardFocus,
        debouncedSystemSearch,
        selectedSystemNames.length,
        systemFilters,
        systemPickerReloadToken,
    ]);

    useEffect(() => {
        if (dashboardFocus !== 'system' || systemView !== 'map' || selectedSystemNames.length === 0) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the MedDRA-system-scoped class by class correlation map.
         */
        async function loadSystemMap() {
            setIsSystemMapLoading(true);
            setSystemMapError(null);

            try {
                const payload = await AdverseEventClient.getSystemCorrelationMap({
                    systems: selectedSystemNames,
                    classPageNumber: systemClassPageNumber,
                    classPageSize: systemClassPageSize,
                    includeFullMatrix: systemFullMatrix,
                    ...systemFilters,
                    signal: abortController.signal,
                });

                setSystemMap(normalizeSystemCorrelationMap(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setSystemMap(null);
                setSystemMapError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsSystemMapLoading(false);
                }
            }
        }

        loadSystemMap();

        return () => {
            abortController.abort();
        };
    }, [
        dashboardFocus,
        selectedSystemNames,
        systemClassPageNumber,
        systemClassPageSize,
        systemFilters,
        systemFullMatrix,
        systemMapReloadToken,
        systemView,
    ]);

    useEffect(() => {
        if (dashboardFocus !== 'system' || systemView !== 'heatmap' || selectedSystemNames.length === 0) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the MedDRA-system-scoped class by drug heatmap.
         */
        async function loadSystemHeatmap() {
            setIsSystemHeatmapLoading(true);
            setSystemHeatmapError(null);

            try {
                const payload = await AdverseEventClient.getSystemCorrelationHeatmap({
                    systems: selectedSystemNames,
                    classPageNumber: systemClassPageNumber,
                    classPageSize: systemClassPageSize,
                    drugPageNumber: systemDrugPageNumber,
                    drugPageSize: systemDrugPageSize,
                    comparator: systemFilters.comparator,
                    includeNonSignificant: systemFilters.includeNonSignificant,
                    excludeFragile: systemFilters.excludeFragile,
                    aggregation: systemFilters.aggregation,
                    excludeCombos: systemFilters.excludeCombos,
                    minEvents: systemFilters.minEvents,
                    signal: abortController.signal,
                });

                setSystemHeatmap(normalizeSystemCorrelationHeatmap(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setSystemHeatmap(null);
                setSystemHeatmapError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsSystemHeatmapLoading(false);
                }
            }
        }

        loadSystemHeatmap();

        return () => {
            abortController.abort();
        };
    }, [
        dashboardFocus,
        selectedSystemNames,
        systemClassPageNumber,
        systemClassPageSize,
        systemDrugPageNumber,
        systemDrugPageSize,
        systemFilters,
        systemHeatmapReloadToken,
        systemView,
    ]);

    useEffect(() => {
        if (
            dashboardFocus !== 'system'
            || systemView !== 'map'
            || selectedSystemNames.length === 0
            || !selectedSystemCell
            || selectedSystemCell.isDiagonal
        ) {
            return undefined;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads per-term detail for the selected system class-pair map cell.
         */
        async function loadSystemCellDetail() {
            setIsSystemCellLoading(true);
            setSystemCellError(null);

            try {
                const payload = await AdverseEventClient.getSystemCorrelationCell({
                    systems: selectedSystemNames,
                    classX: selectedSystemCell.rowClassCode,
                    classY: selectedSystemCell.columnClassCode,
                    pageNumber: systemTermPairPageNumber,
                    pageSize: systemTermPairPageSize,
                    ...systemFilters,
                    signal: abortController.signal,
                });

                setSystemCellDetail(normalizeSystemCorrelationCellDetail(payload));
            } catch (requestError) {
                if (requestError.name === 'AbortError') {
                    return;
                }

                setSystemCellDetail(null);
                setSystemCellError(requestError);
            } finally {
                if (!abortController.signal.aborted) {
                    setIsSystemCellLoading(false);
                }
            }
        }

        loadSystemCellDetail();

        return () => {
            abortController.abort();
        };
    }, [
        dashboardFocus,
        selectedSystemCell,
        selectedSystemNames,
        systemCellReloadToken,
        systemFilters,
        systemTermPairPageNumber,
        systemTermPairPageSize,
        systemView,
    ]);

    useEffect(() => {
        // No product means visualization state should be empty and quiet.
        if (!selectedDocumentGuid) {
            return;
        }

        // AbortController prevents stale responses from replacing newer selections.
        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the full triage payload for the selected product.
         */
        async function loadTriage() {
            setIsTriageLoading(true);
            setTriageError(null);

            try {
                // Triage is intentionally fetched unfiltered so chip counts remain stable.
                const payload = await AdverseEventClient.getTriage(selectedDocumentGuid, {
                    comparator: 'all',
                    includeFragile: true,
                    signal: abortController.signal,
                });

                // Normalization tolerates server JSON naming policy differences.
                const nextTriageView = normalizeTriage(payload);

                setTriageView(nextTriageView);

                // Product context from triage includes derived score fields.
                if (nextTriageView.product) {
                    const productWithFavoriteState = applyFavoriteLookup(nextTriageView.product, favoriteGuids);
                    updateProduct(productWithFavoriteState);
                    recordRecentProduct(productWithFavoriteState);
                    setSelectedProduct((currentProduct) => {
                        // Only update the still-selected product.
                        if (currentProduct?.documentGuid?.toLowerCase() !== productWithFavoriteState.documentGuid.toLowerCase()) {
                            return currentProduct;
                        }

                        return {
                            ...currentProduct,
                            ...productWithFavoriteState,
                        };
                    });
                }
            } catch (requestError) {
                // Abort errors mean a newer request superseded this one.
                if (requestError.name === 'AbortError') {
                    return;
                }

                setTriageError(requestError);
            } finally {
                // Aborted requests should not clear the newer request's loading state.
                if (!abortController.signal.aborted) {
                    setIsTriageLoading(false);
                }
            }
        }

        loadTriage();

        // Cleanup aborts the in-flight request on product change or unmount.
        return () => {
            abortController.abort();
        };
    }, [favoriteGuids, recordRecentProduct, reloadTokens.triage, selectedDocumentGuid, updateProduct]);

    useEffect(() => {
        // Forest data is loaded lazily when its tab is visible.
        if (activeView !== 'forest' || !selectedDocumentGuid) {
            return;
        }

        // AbortController prevents stale chart responses from rendering.
        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the forest view for the selected product and filters.
         */
        async function loadForest() {
            setIsForestLoading(true);
            setForestError(null);

            try {
                const payload = await AdverseEventClient.getForest(selectedDocumentGuid, {
                    comparator: comparatorFilter,
                    includeFragile: showFragile,
                    signal: abortController.signal,
                });

                setForestView(normalizeForest(payload));
            } catch (requestError) {
                // Abort errors mean a newer request superseded this one.
                if (requestError.name === 'AbortError') {
                    return;
                }

                setForestError(requestError);
            } finally {
                // Aborted requests should not clear the newer request's loading state.
                if (!abortController.signal.aborted) {
                    setIsForestLoading(false);
                }
            }
        }

        loadForest();

        // Cleanup aborts the in-flight request on view/filter changes.
        return () => {
            abortController.abort();
        };
    }, [activeView, comparatorFilter, reloadTokens.forest, selectedDocumentGuid, showFragile]);

    useEffect(() => {
        // Quadrant data is loaded lazily when its tab is visible.
        if (activeView !== 'quadrant' || !selectedDocumentGuid) {
            return;
        }

        // AbortController prevents stale chart responses from rendering.
        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the quadrant view for the selected product and filters.
         */
        async function loadQuadrant() {
            setIsQuadrantLoading(true);
            setQuadrantError(null);

            try {
                const payload = await AdverseEventClient.getQuadrant(selectedDocumentGuid, {
                    comparator: comparatorFilter,
                    includeFragile: showFragile,
                    signal: abortController.signal,
                });

                setQuadrantView(normalizeQuadrant(payload));
            } catch (requestError) {
                // Abort errors mean a newer request superseded this one.
                if (requestError.name === 'AbortError') {
                    return;
                }

                setQuadrantError(requestError);
            } finally {
                // Aborted requests should not clear the newer request's loading state.
                if (!abortController.signal.aborted) {
                    setIsQuadrantLoading(false);
                }
            }
        }

        loadQuadrant();

        // Cleanup aborts the in-flight request on view/filter changes.
        return () => {
            abortController.abort();
        };
    }, [activeView, comparatorFilter, reloadTokens.quadrant, selectedDocumentGuid, showFragile]);

    useEffect(() => {
        // Interchange is quiet until the user has two distinct products.
        if (!interchangeDocumentGuidA || !interchangeDocumentGuidB) {
            return;
        }

        // Same-product selection is blocked before it can reach the API.
        if (interchangeDocumentGuidA.toLowerCase() === interchangeDocumentGuidB.toLowerCase()) {
            return;
        }

        const abortController = new AbortController();

        /**************************************************************/
        /**
         * Loads the live therapeutic-interchange comparison.
         */
        async function loadInterchange() {
            setIsInterchangeLoading(true);
            setInterchangeError(null);

            try {
                const payload = await AdverseEventClient.getInterchange({
                    documentGuidA: interchangeDocumentGuidA,
                    documentGuidB: interchangeDocumentGuidB,
                    differencesOnly,
                    sharedSignalsOnly,
                    comparator: comparatorFilter,
                    signal: abortController.signal,
                });

                setInterchangeComparison(normalizeInterchange(payload));
            } catch (requestError) {
                // Abort errors mean a newer comparison superseded this request.
                if (requestError.name === 'AbortError') {
                    return;
                }

                setInterchangeComparison(EMPTY_INTERCHANGE_VIEW);
                setInterchangeError(requestError);
            } finally {
                // Aborted requests should not clear a newer request's loading state.
                if (!abortController.signal.aborted) {
                    setIsInterchangeLoading(false);
                }
            }
        }

        loadInterchange();

        return () => {
            abortController.abort();
        };
    }, [
        differencesOnly,
        comparatorFilter,
        interchangeDocumentGuidA,
        interchangeDocumentGuidB,
        interchangeReloadToken,
        sharedSignalsOnly,
    ]);

    /**************************************************************/
    /**
     * Selects a product from the picker and writes bookmarkable URL state.
     *
     * @param {object} product - Product view model.
     */
    const handleSelectProduct = useCallback(
        (product) => {
            // Product selection requires a DocumentGUID.
            if (!product?.documentGuid) {
                return;
            }

            const productWithFavoriteState = applyFavoriteLookup(product, favoriteGuids);
            setSelectedProduct(productWithFavoriteState);
            setForestView({ signals: [], axisTicks: DEFAULT_FOREST_TICKS });
            setQuadrantView({ points: [] });
            recordRecentProduct(productWithFavoriteState);
            setHydrationError(null);
            writeDashboardUrlState(
                {
                    productGuid: productWithFavoriteState.documentGuid,
                    view: activeView,
                    comparator: comparatorFilter,
                    fragile: showFragile,
                },
                true,
            );
        },
        [activeView, comparatorFilter, favoriteGuids, recordRecentProduct, showFragile],
    );

    /**************************************************************/
    /**
     * Changes the dashboard focus while preserving the other focus state.
     *
     * @param {'product' | 'class' | 'system'} nextFocus - Next dashboard focus.
     */
    const handleChangeDashboardFocus = useCallback(
        (nextFocus) => {
            if (!DASHBOARD_FOCUSES.has(nextFocus)) {
                return;
            }

            setDashboardFocus(nextFocus);
            writeCurrentDashboardUrlState(
                {
                    focus: nextFocus,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Selects a pharmacologic class for class correlation views.
     *
     * @param {object} item - Class picker row.
     */
    const handleSelectClass = useCallback(
        (item) => {
            if (!item?.pharmClassCode) {
                return;
            }

            setSelectedClass(item);
            setPendingClassCode('');
            setClassMap(null);
            setClassHeatmap(null);
            setSelectedCorrelationCell(null);
            setClassCellDetail(null);
            writeCurrentDashboardUrlState(
                {
                    focus: 'class',
                    classCode: item.pharmClassCode,
                },
                true,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the active class view and persists URL state.
     *
     * @param {'map' | 'heatmap'} nextView - Next class view.
     */
    const handleChangeClassView = useCallback(
        (nextView) => {
            if (!CLASS_DASHBOARD_VIEWS.has(nextView)) {
                return;
            }

            setClassView(nextView);
            if (nextView !== 'map') {
                setSelectedCorrelationCell(null);
                setClassCellDetail(null);
            }

            writeCurrentDashboardUrlState(
                {
                    focus: 'class',
                    classView: nextView,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes one class correlation filter.
     *
     * @param {string} name - Filter name.
     * @param {unknown} value - Next filter value.
     */
    const handleChangeClassFilter = useCallback(
        (name, value) => {
            const nextOverrides = { focus: 'class' };

            if (name === 'comparator' && CLASS_COMPARATORS.has(value)) {
                setClassComparator(value);
                nextOverrides.classComparator = value;
            } else if (name === 'includeNonSignificant') {
                setIncludeNonSignificant(Boolean(value));
                nextOverrides.includeNonSignificant = Boolean(value);
            } else if (name === 'excludeFragile') {
                setExcludeFragileRows(Boolean(value));
                nextOverrides.excludeFragile = Boolean(value);
            } else if (name === 'minDrugsPerCell') {
                const nextValue = Math.max(3, Number(value) || 3);
                setMinDrugsPerCell(nextValue);
                nextOverrides.minDrugsPerCell = nextValue;
            } else if (name === 'method' && CLASS_CORRELATION_METHODS.has(value)) {
                setCorrelationMethod(value);
                nextOverrides.method = value;
            } else if (name === 'aggregation' && CLASS_CORRELATION_AGGREGATIONS.has(value)) {
                setCorrelationAggregation(value);
                nextOverrides.aggregation = value;
            } else if (name === 'excludeCombos') {
                setExcludeCombos(Boolean(value));
                nextOverrides.excludeCombos = Boolean(value);
            } else if (name === 'minEvents') {
                const nextValue = Math.max(0, Number(value) || 0);
                setMinEvents(nextValue);
                nextOverrides.minEvents = nextValue;
            } else {
                return;
            }

            setSelectedCorrelationCell(null);
            setClassCellDetail(null);
            writeCurrentDashboardUrlState(nextOverrides, false);
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Selects an off-diagonal correlation map cell.
     *
     * @param {object} cell - Map cell.
     */
    const handleSelectCorrelationCell = useCallback((cell) => {
        if (!cell || cell.isDiagonal) {
            return;
        }

        setSelectedCorrelationCell(cell);
        setClassCellDetail(null);
        setClassCellError(null);
    }, []);

    /**************************************************************/
    /**
     * Retries the class picker request.
     */
    const retryClassPicker = useCallback(() => {
        setClassPickerReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the class map request.
     */
    const retryClassMap = useCallback(() => {
        setClassMapReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the class heatmap request.
     */
    const retryClassHeatmap = useCallback(() => {
        setClassHeatmapReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the selected cell-detail request.
     */
    const retryClassCell = useCallback(() => {
        setClassCellReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Changes the system picker search and resets paged system charts.
     *
     * @param {string} nextSearch - Search text.
     */
    const handleChangeSystemSearch = useCallback(
        (nextSearch) => {
            setSystemSearch(nextSearch);
            setSystemClassPageNumber(1);
            setSystemDrugPageNumber(1);
            setSystemTermPairPageNumber(1);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemSearch: nextSearch,
                    systemClassPageNumber: 1,
                    systemDrugPageNumber: 1,
                    systemTermPairPageNumber: 1,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Adds one MedDRA system to the selected system set.
     *
     * @param {object} item - System picker row.
     */
    const handleAddSystem = useCallback(
        (item) => {
            if (!item?.systemOrganClass) {
                return;
            }

            const lookupKey = item.systemOrganClass.toLowerCase();
            const isAlreadySelected = selectedSystems.some(
                (system) => system.systemOrganClass.toLowerCase() === lookupKey,
            );

            if (isAlreadySelected) {
                return;
            }

            const nextSystems = [...selectedSystems, item];

            setSelectedSystems(nextSystems);
            setSystemMap(null);
            setSystemHeatmap(null);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            setSystemClassPageNumber(1);
            setSystemDrugPageNumber(1);
            setSystemTermPairPageNumber(1);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systems: nextSystems.map((system) => system.systemOrganClass),
                    systemClassPageNumber: 1,
                    systemDrugPageNumber: 1,
                    systemTermPairPageNumber: 1,
                },
                true,
            );
        },
        [selectedSystems, writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Removes one MedDRA system from the selected set.
     *
     * @param {string} systemOrganClass - System name to remove.
     */
    const handleRemoveSystem = useCallback(
        (systemOrganClass) => {
            const lookupKey = systemOrganClass.toLowerCase();
            const nextSystems = selectedSystems.filter(
                (system) => system.systemOrganClass.toLowerCase() !== lookupKey,
            );

            setSelectedSystems(nextSystems);
            setSystemMap(null);
            setSystemHeatmap(null);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            setSystemClassPageNumber(1);
            setSystemDrugPageNumber(1);
            setSystemTermPairPageNumber(1);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systems: nextSystems.map((system) => system.systemOrganClass),
                    systemClassPageNumber: 1,
                    systemDrugPageNumber: 1,
                    systemTermPairPageNumber: 1,
                },
                true,
            );
        },
        [selectedSystems, writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the active system view and persists URL state.
     *
     * @param {'map' | 'heatmap'} nextView - Next system view.
     */
    const handleChangeSystemView = useCallback(
        (nextView) => {
            if (!SYSTEM_DASHBOARD_VIEWS.has(nextView)) {
                return;
            }

            setSystemView(nextView);
            if (nextView !== 'map') {
                setSelectedSystemCell(null);
                setSystemCellDetail(null);
            }

            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemView: nextView,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes one system correlation filter.
     *
     * @param {string} name - Filter name.
     * @param {unknown} value - Next filter value.
     */
    const handleChangeSystemFilter = useCallback(
        (name, value) => {
            const nextOverrides = {
                focus: 'system',
                systemClassPageNumber: 1,
                systemDrugPageNumber: 1,
                systemTermPairPageNumber: 1,
            };

            if (name === 'comparator' && CLASS_COMPARATORS.has(value)) {
                setSystemComparator(value);
                nextOverrides.systemComparator = value;
            } else if (name === 'includeNonSignificant') {
                setSystemIncludeNonSignificant(Boolean(value));
                nextOverrides.systemIncludeNonSignificant = Boolean(value);
            } else if (name === 'excludeFragile') {
                setSystemExcludeFragile(Boolean(value));
                nextOverrides.systemExcludeFragile = Boolean(value);
            } else if (name === 'minTermsPerCell') {
                const nextValue = Math.max(3, Number(value) || 3);
                setMinTermsPerCell(nextValue);
                nextOverrides.minTermsPerCell = nextValue;
            } else if (name === 'method' && CLASS_CORRELATION_METHODS.has(value)) {
                setSystemMethod(value);
                nextOverrides.systemMethod = value;
            } else if (name === 'aggregation' && CLASS_CORRELATION_AGGREGATIONS.has(value)) {
                setSystemAggregation(value);
                nextOverrides.systemAggregation = value;
            } else if (name === 'excludeCombos') {
                setSystemExcludeCombos(Boolean(value));
                nextOverrides.systemExcludeCombos = Boolean(value);
            } else if (name === 'minEvents') {
                const nextValue = Math.max(0, Number(value) || 0);
                setSystemMinEvents(nextValue);
                nextOverrides.systemMinEvents = nextValue;
            } else {
                return;
            }

            setSystemClassPageNumber(1);
            setSystemDrugPageNumber(1);
            setSystemTermPairPageNumber(1);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            writeCurrentDashboardUrlState(nextOverrides, false);
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Selects an off-diagonal system class-pair map cell.
     *
     * @param {object} cell - Map cell.
     */
    const handleSelectSystemCell = useCallback(
        (cell) => {
            if (!cell || cell.isDiagonal) {
                return;
            }

            setSelectedSystemCell(cell);
            setSystemCellDetail(null);
            setSystemCellError(null);
            setSystemTermPairPageNumber(1);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemTermPairPageNumber: 1,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Toggles full-matrix loading for the system class map.
     */
    const handleToggleSystemFullMatrix = useCallback(() => {
        const nextValue = !systemFullMatrix;

        setSystemFullMatrix(nextValue);
        setSelectedSystemCell(null);
        setSystemCellDetail(null);
        writeCurrentDashboardUrlState(
            {
                focus: 'system',
                systemFullMatrix: nextValue,
            },
            false,
        );
    }, [systemFullMatrix, writeCurrentDashboardUrlState]);

    /**************************************************************/
    /**
     * Changes the map class-axis page.
     *
     * @param {number} nextPageNumber - Next page number.
     */
    const handleChangeSystemMapClassPage = useCallback(
        (nextPageNumber) => {
            const nextValue = Math.max(1, Number(nextPageNumber) || 1);

            setSystemClassPageNumber(nextValue);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemClassPageNumber: nextValue,
                    systemTermPairPageNumber: 1,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the system class-axis page size.
     *
     * @param {number} nextPageSize - Next page size.
     */
    const handleChangeSystemClassPageSize = useCallback(
        (nextPageSize) => {
            const nextValue = Math.max(1, Number(nextPageSize) || DEFAULT_SYSTEM_CLASS_PAGE_SIZE);

            setSystemClassPageSize(nextValue);
            setSystemClassPageNumber(1);
            setSelectedSystemCell(null);
            setSystemCellDetail(null);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemClassPageNumber: 1,
                    systemClassPageSize: nextValue,
                    systemTermPairPageNumber: 1,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the heatmap class-row page.
     *
     * @param {number} nextPageNumber - Next page number.
     */
    const handleChangeSystemHeatmapClassPage = useCallback(
        (nextPageNumber) => {
            const nextValue = Math.max(1, Number(nextPageNumber) || 1);

            setSystemClassPageNumber(nextValue);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemClassPageNumber: nextValue,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the heatmap drug-column page.
     *
     * @param {number} nextPageNumber - Next page number.
     */
    const handleChangeSystemDrugPage = useCallback(
        (nextPageNumber) => {
            const nextValue = Math.max(1, Number(nextPageNumber) || 1);

            setSystemDrugPageNumber(nextValue);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemDrugPageNumber: nextValue,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the system drug-column page size.
     *
     * @param {number} nextPageSize - Next page size.
     */
    const handleChangeSystemDrugPageSize = useCallback(
        (nextPageSize) => {
            const nextValue = Math.max(1, Number(nextPageSize) || DEFAULT_SYSTEM_DRUG_PAGE_SIZE);

            setSystemDrugPageSize(nextValue);
            setSystemDrugPageNumber(1);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemDrugPageNumber: 1,
                    systemDrugPageSize: nextValue,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the selected cell term-pair page.
     *
     * @param {number} nextPageNumber - Next page number.
     */
    const handleChangeSystemTermPairPage = useCallback(
        (nextPageNumber) => {
            const nextValue = Math.max(1, Number(nextPageNumber) || 1);

            setSystemTermPairPageNumber(nextValue);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemTermPairPageNumber: nextValue,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Changes the selected cell term-pair page size.
     *
     * @param {number} nextPageSize - Next page size.
     */
    const handleChangeSystemTermPairPageSize = useCallback(
        (nextPageSize) => {
            const nextValue = Math.max(1, Number(nextPageSize) || DEFAULT_SYSTEM_TERM_PAIR_PAGE_SIZE);

            setSystemTermPairPageSize(nextValue);
            setSystemTermPairPageNumber(1);
            writeCurrentDashboardUrlState(
                {
                    focus: 'system',
                    systemTermPairPageNumber: 1,
                    systemTermPairPageSize: nextValue,
                },
                false,
            );
        },
        [writeCurrentDashboardUrlState],
    );

    /**************************************************************/
    /**
     * Retries the system picker request.
     */
    const retrySystemPicker = useCallback(() => {
        setSystemPickerReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the system map request.
     */
    const retrySystemMap = useCallback(() => {
        setSystemMapReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the system heatmap request.
     */
    const retrySystemHeatmap = useCallback(() => {
        setSystemHeatmapReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Retries the selected system cell-detail request.
     */
    const retrySystemCell = useCallback(() => {
        setSystemCellReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Toggles a product favorite and updates every visible product copy.
     *
     * @param {object} product - Product view model.
     */
    const handleToggleFavorite = useCallback(
        async (product) => {
            // A document GUID is the only field the favorite endpoints require, so any
            // row that carries one (including recent snapshots) can be favorited.
            if (!product?.documentGuid) {
                return;
            }

            const lookupKey = product.documentGuid.toLowerCase();

            // Recent rows are display snapshots. Prefer the fresh catalog row when it is
            // loaded so the stored favorite carries richer metadata; otherwise keep the
            // snapshot. The next state is computed from live favorite membership so a
            // toggle is correct regardless of which copy of the row was clicked.
            const resolvedProduct = (product.isRecentOnly && productsByGuid.get(lookupKey)) || product;
            const isCurrentlyFavorite = product.isFavorite || favoriteGuids.has(lookupKey);
            const nextFavoriteState = !isCurrentlyFavorite;

            const updatedProduct = await toggleFavorite(resolvedProduct, nextFavoriteState);

            // Auth failures return null and should leave product state unchanged.
            if (!updatedProduct) {
                return;
            }

            updateProduct(updatedProduct);

            // Selected product must update even if it is off the current catalog page.
            setSelectedProduct((currentProduct) => {
                // No selected product means there is nothing to update.
                if (!currentProduct?.documentGuid) {
                    return currentProduct;
                }

                // Only the matching selected product receives the favorite change.
                if (currentProduct.documentGuid !== updatedProduct.documentGuid) {
                    return currentProduct;
                }

                return {
                    ...currentProduct,
                    isFavorite: updatedProduct.isFavorite,
                };
            });
        },
        [toggleFavorite, updateProduct, productsByGuid, favoriteGuids],
    );

    /**************************************************************/
    /**
     * Changes the active dashboard view and persists URL state.
     *
     * @param {string} nextView - Next view token.
     */
    const handleChangeView = useCallback(
        (nextView) => {
            // Unknown view tokens are ignored.
            if (!DASHBOARD_VIEWS.has(nextView)) {
                return;
            }

            setActiveView(nextView);
            writeDashboardUrlState(
                {
                    productGuid: selectedDocumentGuid,
                    view: nextView,
                    comparator: comparatorFilter,
                    fragile: showFragile,
                },
                false,
            );
        },
        [comparatorFilter, selectedDocumentGuid, showFragile],
    );

    /**************************************************************/
    /**
     * Changes the comparator filter and persists URL state.
     *
     * @param {string} nextComparator - Comparator token.
     */
    const handleChangeComparator = useCallback(
        (nextComparator) => {
            // Unknown comparator tokens are ignored.
            if (!COMPARATOR_FILTERS.has(nextComparator)) {
                return;
            }

            setComparatorFilter(nextComparator);
            writeDashboardUrlState(
                {
                    productGuid: selectedDocumentGuid,
                    view: activeView,
                    comparator: nextComparator,
                    fragile: showFragile,
                },
                false,
            );
        },
        [activeView, selectedDocumentGuid, showFragile],
    );

    /**************************************************************/
    /**
     * Toggles fragile-row visibility and persists URL state.
     */
    const handleToggleFragile = useCallback(() => {
        // The next value is calculated once so state and URL remain identical.
        const nextShowFragile = !showFragile;

        setShowFragile(nextShowFragile);
        writeDashboardUrlState(
            {
                productGuid: selectedDocumentGuid,
                view: activeView,
                comparator: comparatorFilter,
                fragile: nextShowFragile,
            },
            false,
        );
    }, [activeView, comparatorFilter, selectedDocumentGuid, showFragile]);

    /**************************************************************/
    /**
     * Runs exact-term reverse lookups scoped to selected products.
     *
     * @param {string[]} requestedTerms - AE terms to submit.
     */
    const runReverseLookupForTerms = useCallback(
        async (requestedTerms) => {
            const symptoms = buildReverseLookupTerms(requestedTerms);

            // The API requires at least one exact non-empty term.
            if (symptoms.length === 0) {
                setReverseLookupError(new Error('Add at least one exact adverse-event term before searching.'));
                setReverseLookupResult(null);
                return;
            }

            const documentGuids = buildReverseLookupScope(reverseLookupScopeProducts);

            setSelectedReverseLookupTerms(symptoms);
            setReverseLookupTerm('');
            setIsReverseLookupLoading(true);
            setReverseLookupError(null);

            try {
                const payloads = await Promise.all(
                    symptoms.map((symptom) => AdverseEventClient.getReverseLookup({
                        symptom,
                        documentGuids,
                    })),
                );
                const normalizedResults = payloads.map((payload) => normalizeReverseLookup(payload));

                setReverseLookupResult(mergeReverseLookupResults(normalizedResults, symptoms));
            } catch (requestError) {
                setReverseLookupResult(null);
                setReverseLookupError(requestError);
            } finally {
                setIsReverseLookupLoading(false);
            }
        },
        [reverseLookupScopeProducts],
    );

    /**************************************************************/
    /**
     * Runs reverse lookup from the current selected terms plus optional input.
     *
     * @param {string} requestedTerm - Optional AE term to add before submitting.
     */
    const handleRunReverseLookup = useCallback(
        (requestedTerm = '') => {
            const symptoms = buildReverseLookupTerms(selectedReverseLookupTerms, requestedTerm);

            return runReverseLookupForTerms(symptoms);
        },
        [runReverseLookupForTerms, selectedReverseLookupTerms],
    );

    /**************************************************************/
    /**
     * Submits a loaded exact-term suggestion.
     *
     * @param {string} suggestion - Suggestion term.
     */
    const handlePickReverseLookupSuggestion = useCallback(
        (suggestion) => {
            const lookupKey = suggestion.trim().toLowerCase();
            const isSelected = selectedReverseLookupTerms.some(
                (selectedTerm) => selectedTerm.toLowerCase() === lookupKey,
            );
            const symptoms = isSelected
                ? selectedReverseLookupTerms.filter((selectedTerm) => selectedTerm.toLowerCase() !== lookupKey)
                : buildReverseLookupTerms(selectedReverseLookupTerms, suggestion);

            if (symptoms.length === 0) {
                setSelectedReverseLookupTerms([]);
                setReverseLookupResult(null);
                setReverseLookupError(null);
                setReverseLookupTerm('');
                return;
            }

            runReverseLookupForTerms(symptoms);
        },
        [runReverseLookupForTerms, selectedReverseLookupTerms],
    );

    /**************************************************************/
    /**
     * Removes one selected reverse-lookup term and refreshes the merged result.
     *
     * @param {string} term - Selected AE term to remove.
     */
    const handleRemoveReverseLookupTerm = useCallback(
        (term) => {
            const lookupKey = term.trim().toLowerCase();
            const symptoms = selectedReverseLookupTerms.filter(
                (selectedTerm) => selectedTerm.toLowerCase() !== lookupKey,
            );

            if (symptoms.length === 0) {
                setSelectedReverseLookupTerms([]);
                setReverseLookupResult(null);
                setReverseLookupError(null);
                setReverseLookupTerm('');
                return;
            }

            runReverseLookupForTerms(symptoms);
        },
        [runReverseLookupForTerms, selectedReverseLookupTerms],
    );

    /**************************************************************/
    /**
     * Changes product A by selecting it as the dashboard product.
     *
     * @param {object | string} selectedProductOrGuid - Product row or document GUID.
     */
    const handleChangeInterchangeProductA = useCallback(
        (selectedProductOrGuid) => {
            const nextProduct = typeof selectedProductOrGuid === 'string'
                ? interchangeProducts.find(
                    (product) => product.documentGuid.toLowerCase() === selectedProductOrGuid.toLowerCase(),
                )
                : selectedProductOrGuid;

            if (nextProduct?.documentGuid) {
                handleSelectProduct(nextProduct);
            }
        },
        [handleSelectProduct, interchangeProducts],
    );

    /**************************************************************/
    /**
     * Changes product B while blocking same-product comparisons.
     *
     * @param {object | string} selectedProductOrGuid - Product row or document GUID.
     */
    const handleChangeInterchangeProductB = useCallback(
        (selectedProductOrGuid) => {
            const nextProduct = typeof selectedProductOrGuid === 'string'
                ? interchangeProducts.find(
                    (product) => product.documentGuid.toLowerCase() === selectedProductOrGuid.toLowerCase(),
                )
                : selectedProductOrGuid;

            if (!nextProduct?.documentGuid) {
                setInterchangeProductB(null);
                return;
            }

            // Same-product selection is disabled in the control and ignored here.
            if (
                interchangeProductA?.documentGuid
                && nextProduct.documentGuid.toLowerCase() === interchangeProductA.documentGuid.toLowerCase()
            ) {
                return;
            }

            setInterchangeProductB(nextProduct);
        },
        [interchangeProductA, interchangeProducts],
    );

    /**************************************************************/
    /**
     * Toggles server-side difference filtering for interchange.
     */
    const handleToggleDifferencesOnly = useCallback(() => {
        setDifferencesOnly((currentValue) => !currentValue);
    }, []);

    /**************************************************************/
    /**
     * Toggles server-side shared-signal filtering for interchange.
     */
    const handleToggleSharedSignalsOnly = useCallback(() => {
        setSharedSignalsOnly((currentValue) => !currentValue);
    }, []);

    /**************************************************************/
    /**
     * Retries a visualization request.
     *
     * @param {'triage' | 'forest' | 'quadrant'} view - View token.
     */
    const retryView = useCallback((view) => {
        // Incrementing a token re-runs the matching effect without changing filters.
        setReloadTokens((currentTokens) => ({
            ...currentTokens,
            [view]: currentTokens[view] + 1,
        }));
    }, []);

    /**************************************************************/
    /**
     * Retries the current interchange comparison.
     */
    const retryInterchange = useCallback(() => {
        setInterchangeReloadToken((currentToken) => currentToken + 1);
    }, []);

    /**************************************************************/
    /**
     * Saves the selected product as a favorite when possible.
     */
    const handleSaveProduct = useCallback(async () => {
        // The top-bar save action only adds favorites; it does not remove them.
        if (!selectedProductWithFavoriteState || selectedProductWithFavoriteState.isFavorite) {
            return;
        }

        await handleToggleFavorite(selectedProductWithFavoriteState);
    }, [handleToggleFavorite, selectedProductWithFavoriteState]);

    /**************************************************************/
    /**
     * Exports the current dashboard view as a JSON file.
     */
    const handleExportDashboard = useCallback(() => {
        let exportPayload;
        let exportName;

        if (dashboardFocus === 'class') {
            if (!selectedClass) {
                return;
            }

            exportPayload = {
                focus: 'class',
                selectedClass,
                state: {
                    classView,
                    ...classFilters,
                },
                map: classMap,
                heatmap: classHeatmap,
                cellDetail: classCellDetail,
                warnings: [
                    ...(classMap?.warnings ?? []),
                    ...(classHeatmap?.warnings ?? []),
                    ...(classCellDetail?.warnings ?? []),
                ],
            };
            exportName = `${selectedClass.pharmClassName}-ae-class-dashboard.json`;
        } else if (dashboardFocus === 'system') {
            if (selectedSystems.length === 0) {
                return;
            }

            exportPayload = {
                focus: 'system',
                selectedSystems,
                state: {
                    systemView,
                    ...systemFilters,
                    page: {
                        classPageNumber: systemClassPageNumber,
                        classPageSize: systemClassPageSize,
                        drugPageNumber: systemDrugPageNumber,
                        drugPageSize: systemDrugPageSize,
                        termPairPageNumber: systemTermPairPageNumber,
                        termPairPageSize: systemTermPairPageSize,
                        includeFullMatrix: systemFullMatrix,
                    },
                },
                map: systemMap,
                heatmap: systemHeatmap,
                cellDetail: systemCellDetail,
                warnings: [
                    ...(systemMap?.warnings ?? []),
                    ...(systemHeatmap?.warnings ?? []),
                    ...(systemCellDetail?.warnings ?? []),
                ],
            };
            exportName = `ae-system-correlation-${selectedSystems[0].systemOrganClass}.json`;
        } else {
            // Export requires a selected product.
            if (!selectedProductWithFavoriteState) {
                return;
            }

            // The product payload intentionally preserves the existing product-mode shape.
            exportPayload = {
                product: selectedProductWithFavoriteState,
                state: {
                    view: activeView,
                    comparator: comparatorFilter,
                    showFragile,
                },
                triage: filteredTiers,
                forest: forestView,
                quadrant: quadrantView,
                reverseLookup: reverseLookupResult,
                interchange: interchangeComparison,
            };
            exportName = `${selectedProductWithFavoriteState.name}-ae-dashboard.json`;
        }

        // Blob URLs avoid server round trips for a client-side export.
        const exportBlob = new Blob([JSON.stringify(exportPayload, null, 2)], {
            type: 'application/json',
        });

        // The object URL is revoked after the synthetic click completes.
        const exportUrl = URL.createObjectURL(exportBlob);

        // The hidden anchor lets browsers use their native download behavior.
        const exportLink = document.createElement('a');

        exportLink.href = exportUrl;
        exportLink.download = exportName.replace(/[^a-z0-9._-]+/gi, '-');
        exportLink.click();
        URL.revokeObjectURL(exportUrl);
    }, [
        activeView,
        classCellDetail,
        classFilters,
        classHeatmap,
        classMap,
        classView,
        comparatorFilter,
        dashboardFocus,
        filteredTiers,
        forestView,
        interchangeComparison,
        quadrantView,
        reverseLookupResult,
        selectedClass,
        selectedProductWithFavoriteState,
        selectedSystems,
        showFragile,
        systemCellDetail,
        systemClassPageNumber,
        systemClassPageSize,
        systemDrugPageNumber,
        systemDrugPageSize,
        systemFilters,
        systemFullMatrix,
        systemHeatmap,
        systemMap,
        systemTermPairPageNumber,
        systemTermPairPageSize,
        systemView,
    ]);

    /**************************************************************/
    /**
     * Wires the server-rendered masthead Save/Export actions to dashboard state.
     *
     * The masthead is rendered by the shared _Masthead.cshtml partial (outside the
     * React root); the dashboard host view injects the Save/Export buttons via
     * ViewData["MastheadActions"] with ids #aeSaveBtn / #aeExportBtn. React owns
     * only their behavior — keeping their disabled state in sync with the current
     * selection and dispatching the same handlers the old in-React top bar used.
     * This mirrors how the Chat page wires its #clearBtn and keeps one masthead
     * source of truth. No-ops on the standalone Vite dev page, where the partial
     * (and therefore the buttons) is absent.
     */
    useEffect(() => {
        const saveButton = document.getElementById('aeSaveBtn');
        const exportButton = document.getElementById('aeExportBtn');
        if (!saveButton || !exportButton) {
            return undefined;
        }

        // Save is product-scoped; export follows the active focus.
        saveButton.disabled =
            dashboardFocus !== 'product'
            || !selectedProductWithFavoriteState
            || selectedProductWithFavoriteState.isFavorite;
        exportButton.disabled =
            dashboardFocus === 'class'
                ? !selectedClass
                : dashboardFocus === 'system'
                    ? selectedSystems.length === 0
                    : !selectedProductWithFavoriteState;

        saveButton.addEventListener('click', handleSaveProduct);
        exportButton.addEventListener('click', handleExportDashboard);
        return () => {
            saveButton.removeEventListener('click', handleSaveProduct);
            exportButton.removeEventListener('click', handleExportDashboard);
        };
    }, [
        dashboardFocus,
        handleExportDashboard,
        handleSaveProduct,
        selectedClass,
        selectedProductWithFavoriteState,
        selectedSystems.length,
    ]);

    // Feature-disabled state owns the entire page.
    if (isFeatureDisabled) {
        return <DisabledFeature />;
    }

    return (
        <main className="ae-dashboard-page">
            <div className="app" data-screen-label="AE Dashboard">
                <FocusSwitch
                    activeFocus={dashboardFocus}
                    onChangeFocus={handleChangeDashboardFocus}
                />

                {dashboardFocus === 'product' ? (
                    <>
                        <PageHeader
                            product={selectedProductWithFavoriteState}
                            hydrationError={hydrationError}
                            picker={(
                                <ProductPicker
                                    products={visibleProducts}
                                    favoriteProducts={visibleFavoriteProducts}
                                    recentProducts={visibleRecentProducts}
                                    totalProductCount={totalProductCount}
                                    selectedProduct={selectedProductWithFavoriteState}
                                    searchTerm={productSearch}
                                    onSearchTermChange={setProductSearch}
                                    onSelectProduct={handleSelectProduct}
                                    onToggleFavorite={handleToggleFavorite}
                                    favoriteBusyGuids={busyDocumentGuids}
                                    favoriteNotice={favoriteNotice}
                                    isLoading={isProductLoading}
                                    error={productError}
                                    onRetry={refreshProducts}
                                />
                            )}
                        />

                        {!isProductLoading && !productError && visibleProducts.length === 0 ? (
                            <EmptyState title="No dashboard-ready products found." />
                        ) : null}

                        {productError && !(productError instanceof ApiError && productError.isFeatureDisabled) ? (
                            <InlineError error={productError} onRetry={refreshProducts} />
                        ) : null}

                        <DashboardPanel
                            activeView={activeView}
                            comparatorFilter={comparatorFilter}
                            showFragile={showFragile}
                            signalCounts={signalCounts}
                            filteredTiers={filteredTiers}
                            forestView={forestView}
                            quadrantView={quadrantView}
                            isTriageLoading={isTriageLoading}
                            triageError={triageError}
                            onRetryTriage={() => retryView('triage')}
                            isForestLoading={isForestLoading}
                            forestError={forestError}
                            onRetryForest={() => retryView('forest')}
                            isQuadrantLoading={isQuadrantLoading}
                            quadrantError={quadrantError}
                            onRetryQuadrant={() => retryView('quadrant')}
                            onChangeView={handleChangeView}
                            onChangeComparator={handleChangeComparator}
                            onToggleFragile={handleToggleFragile}
                        />

                        <CrossProductTools
                            reverseLookupTerm={reverseLookupTerm}
                            selectedReverseLookupTerms={selectedReverseLookupTerms}
                            reverseLookupSuggestions={reverseLookupSuggestions}
                            reverseLookupScopeProducts={reverseLookupScopeProducts}
                            reverseLookupResult={reverseLookupResult}
                            isReverseLookupLoading={isReverseLookupLoading}
                            reverseLookupError={reverseLookupError}
                            onReverseLookupTermChange={setReverseLookupTerm}
                            onRunReverseLookup={handleRunReverseLookup}
                            onPickReverseLookupSuggestion={handlePickReverseLookupSuggestion}
                            onRemoveReverseLookupTerm={handleRemoveReverseLookupTerm}
                            productA={interchangeProductA}
                            productB={effectiveInterchangeProductB}
                            interchangeProducts={interchangeProducts}
                            favoriteProducts={visibleFavoriteProducts}
                            recentProducts={visibleRecentProducts}
                            totalProductCount={totalProductCount}
                            productSearch={productSearch}
                            onProductSearchChange={setProductSearch}
                            onToggleFavorite={handleToggleFavorite}
                            favoriteBusyGuids={busyDocumentGuids}
                            favoriteNotice={favoriteNotice}
                            isProductLoading={isProductLoading}
                            productError={productError}
                            onRetryProducts={refreshProducts}
                            sharedSignalsOnly={sharedSignalsOnly}
                            differencesOnly={differencesOnly}
                            interchangeComparison={interchangeComparison}
                            isInterchangeLoading={isInterchangeLoading}
                            interchangeError={interchangeError}
                            onChangeInterchangeProductA={handleChangeInterchangeProductA}
                            onChangeInterchangeProductB={handleChangeInterchangeProductB}
                            onToggleSharedSignalsOnly={handleToggleSharedSignalsOnly}
                            onToggleDifferencesOnly={handleToggleDifferencesOnly}
                            onRetryInterchange={retryInterchange}
                            comparatorFilter={comparatorFilter}
                        />
                    </>
                ) : dashboardFocus === 'class' ? (
                    <>
                        <ClassPageHeader
                            selectedClass={selectedClass}
                            map={classMap}
                            filters={classFilters}
                            picker={(
                                <ClassPicker
                                    classes={correlationClasses}
                                    selectedClass={selectedClass}
                                    searchTerm={classSearch}
                                    onSearchTermChange={setClassSearch}
                                    onSelectClass={handleSelectClass}
                                    totalClassCount={correlationClassTotalCount}
                                    chartableClassCount={correlationClassChartableCount}
                                    isLoading={isClassPickerLoading}
                                    error={classPickerError}
                                    onRetry={retryClassPicker}
                                />
                            )}
                        />

                        {classPickerError && !(classPickerError instanceof ApiError && classPickerError.isFeatureDisabled) ? (
                            <InlineError error={classPickerError} onRetry={retryClassPicker} />
                        ) : null}

                        <ClassCorrelationSurface
                            selectedClass={selectedClass}
                            classView={classView}
                            filters={classFilters}
                            classMap={classMap}
                            classHeatmap={classHeatmap}
                            classCellDetail={classCellDetail}
                            selectedCell={selectedCorrelationCell}
                            isClassMapLoading={isClassMapLoading}
                            classMapError={classMapError}
                            onRetryClassMap={retryClassMap}
                            isClassHeatmapLoading={isClassHeatmapLoading}
                            classHeatmapError={classHeatmapError}
                            onRetryClassHeatmap={retryClassHeatmap}
                            isClassCellLoading={isClassCellLoading}
                            classCellError={classCellError}
                            onRetryClassCell={retryClassCell}
                            onChangeClassView={handleChangeClassView}
                            onChangeClassFilter={handleChangeClassFilter}
                            onSelectCell={handleSelectCorrelationCell}
                        />
                    </>
                ) : (
                    <>
                        <SystemPageHeader
                            selectedSystems={selectedSystems}
                            map={systemMap}
                            filters={systemFilters}
                            picker={(
                                <SystemPicker
                                    systems={correlationSystems}
                                    selectedSystems={selectedSystems}
                                    searchTerm={systemSearch}
                                    onSearchTermChange={handleChangeSystemSearch}
                                    onAddSystem={handleAddSystem}
                                    onRemoveSystem={handleRemoveSystem}
                                    totalSystemCount={correlationSystemTotalCount}
                                    chartableSystemCount={correlationSystemChartableCount}
                                    isLoading={isSystemPickerLoading}
                                    error={systemPickerError}
                                    onRetry={retrySystemPicker}
                                />
                            )}
                        />

                        {systemPickerError && !(systemPickerError instanceof ApiError && systemPickerError.isFeatureDisabled) ? (
                            <InlineError error={systemPickerError} onRetry={retrySystemPicker} />
                        ) : null}

                        <SystemCorrelationSurface
                            selectedSystems={selectedSystems}
                            systemView={systemView}
                            filters={systemFilters}
                            systemMap={systemMap}
                            systemHeatmap={systemHeatmap}
                            systemCellDetail={systemCellDetail}
                            selectedCell={selectedSystemCell}
                            isSystemMapLoading={isSystemMapLoading}
                            systemMapError={systemMapError}
                            onRetrySystemMap={retrySystemMap}
                            isSystemHeatmapLoading={isSystemHeatmapLoading}
                            systemHeatmapError={systemHeatmapError}
                            onRetrySystemHeatmap={retrySystemHeatmap}
                            isSystemCellLoading={isSystemCellLoading}
                            systemCellError={systemCellError}
                            onRetrySystemCell={retrySystemCell}
                            onChangeSystemView={handleChangeSystemView}
                            onChangeSystemFilter={handleChangeSystemFilter}
                            onSelectCell={handleSelectSystemCell}
                            mapClassPage={systemMapClassPage}
                            heatmapClassPage={systemHeatmapClassPage}
                            heatmapDrugPage={systemHeatmapDrugPage}
                            termPairPage={systemTermPairPage}
                            includeFullMatrix={systemFullMatrix}
                            onToggleFullMatrix={handleToggleSystemFullMatrix}
                            onChangeMapClassPage={handleChangeSystemMapClassPage}
                            onChangeMapClassPageSize={handleChangeSystemClassPageSize}
                            onChangeHeatmapClassPage={handleChangeSystemHeatmapClassPage}
                            onChangeHeatmapClassPageSize={handleChangeSystemClassPageSize}
                            onChangeHeatmapDrugPage={handleChangeSystemDrugPage}
                            onChangeHeatmapDrugPageSize={handleChangeSystemDrugPageSize}
                            onChangeTermPairPage={handleChangeSystemTermPairPage}
                            onChangeTermPairPageSize={handleChangeSystemTermPairPageSize}
                        />
                    </>
                )}

                <div className="foot-note">
                    <p>
                        <strong>Limitation:</strong> These figures do not represent every adverse outcome
                        that could be attributed to a product. Coverage is bounded by what each product&apos;s
                        labeling discloses and by what can be reliably parsed from it &mdash; events omitted
                        from the label, or reported in formats the parser cannot extract, will not appear here.
                        The absence of a signal is not evidence of its absence in practice.
                    </p>
                    <p>
                        <strong>Chart-worthiness</strong> (0&ndash;100) rates how complete and chartable a
                        product&apos;s adverse-event data is &mdash; not how safe the drug is. It blends placebo
                        coverage (25%), elevated-signal density (25%), SOC breadth out of 17 (20%), dose-data
                        coverage (15%), active-comparator coverage (5%), and AE-row volume against a 40-row
                        target (10%). Higher scores mean richer, more comparable safety data; the score card
                        tooltip lists the top contributors and limiters for the selected product.
                    </p>
                    <p>
                        Data shown: <code>Database</code> projection for the selected {dashboardFocus}.
                        Fragile rows can be hidden from the visualization controls where the active view supports them.
                    </p>
                    {dashboardFocus === 'class' ? (
                        <p>
                            <strong>Correlation methods:</strong> Spearman compares the rank order of per-drug
                            SOC values and is less sensitive to outliers or uneven spacing. Pearson compares the
                            linear relationship between the per-drug SOC values. Both methods use the currently
                            selected class, comparator, aggregation, and filter settings.
                        </p>
                    ) : null}
                    {dashboardFocus === 'system' ? (
                        <p>
                            <strong>Correlation methods:</strong> Spearman compares selected-system term-profile
                            ranks across pharmacologic classes. Pearson compares the linear relationship between
                            those class-level term profiles under the selected systems, comparator, aggregation,
                            and filter settings.
                        </p>
                    ) : null}
                </div>
            </div>
        </main>
    );
}

export default App;
