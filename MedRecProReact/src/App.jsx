/******** IMPORTANT : npm --prefix "..\MedRecProReact" run build *********/

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from './api/apiError';
import { AdverseEventClient } from './api/adverseEventClient';
import { PageHeader } from './components/PageHeader';
import { CompactProductPicker, ProductPicker } from './components/ProductPicker';
import { DisabledFeature } from './components/common/DisabledFeature';
import { EmptyState } from './components/common/EmptyState';
import { InlineError } from './components/common/InlineError';
import { Loading } from './components/common/Loading';
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
    normalizeForest,
    normalizeInterchange,
    normalizeQuadrant,
    normalizeReverseLookup,
    normalizeTriage,
} from './lib/normalizers';

// Supported dashboard views mirror the prototype tab names.
const DASHBOARD_VIEWS = new Set(['triage', 'forest', 'quadrant']);

// Supported comparator filters mirror the API enum through client-safe tokens.
const COMPARATOR_FILTERS = new Set(['all', 'placebo', 'active']);

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
 * Reads the current query string into dashboard state.
 *
 * @returns {{ productGuid: string, view: string, comparator: string, fragile: boolean }} URL state.
 */
function readDashboardUrlState() {
    // Non-browser imports use the default dashboard state.
    if (!globalThis.window?.location) {
        return {
            productGuid: '',
            view: 'triage',
            comparator: 'all',
            fragile: true,
        };
    }

    // URLSearchParams keeps parsing aligned with normal browser query behavior.
    const searchParams = new URLSearchParams(globalThis.window.location.search);

    // Product state is optional and can be hydrated from triage when off-page.
    const productGuid = searchParams.get('product') ?? '';

    // The requested view is validated so stale URLs cannot select unknown tabs.
    const requestedView = searchParams.get('view') ?? 'triage';

    // The requested comparator is validated before it reaches API calls.
    const requestedComparator = searchParams.get('comparator') ?? 'all';

    // Fragile rows are visible by default because the prototype exposes them with muted styling.
    const fragile = (searchParams.get('fragile') ?? 'true') !== 'false';

    return {
        productGuid,
        view: DASHBOARD_VIEWS.has(requestedView) ? requestedView : 'triage',
        comparator: COMPARATOR_FILTERS.has(requestedComparator) ? requestedComparator : 'all',
        fragile,
    };
}

/**************************************************************/
/**
 * Writes the selected product and filter state to the URL.
 *
 * @param {object} args - URL state to write.
 * @param {boolean} shouldPush - Whether to push or replace browser history.
 */
function writeDashboardUrlState({ productGuid, view, comparator, fragile }, shouldPush) {
    // URL state is browser-only.
    if (!globalThis.window?.history) {
        return;
    }

    // The current URL provides path and any unknown query values.
    const nextUrl = new URL(globalThis.window.location.href);

    // Product GUID is removed when no product is selected.
    if (productGuid) {
        nextUrl.searchParams.set('product', productGuid);
    } else {
        nextUrl.searchParams.delete('product');
    }

    nextUrl.searchParams.set('view', view);
    nextUrl.searchParams.set('comparator', comparator);
    nextUrl.searchParams.set('fragile', String(fragile));

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
function InterchangeTrackHalf({ signal, side, scaleDomain }) {
    const direction = signal ? getSignalDirection(signal) : 'ns';
    const pointLeft = getForestXPercent(signal?.rr, scaleDomain);

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
        <div className={`ic-track-half ${side}${direction === 'ns' ? ' ns' : ''}`}>
            {lowerLeft !== null && upperLeft !== null ? (
                <>
                    <div className="ic-ci" style={{ left: `${lowerLeft}%`, width: `${intervalWidth}%` }} />
                    <div className="ic-ci-cap" style={{ left: `${lowerLeft}%` }} />
                    <div className="ic-ci-cap" style={{ left: `${upperLeft}%` }} />
                </>
            ) : null}
            {pointLeft !== null ? <div className="ic-pt" style={{ left: `${pointLeft}%` }} /> : null}
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
function InterchangeTrack({ row, scaleDomain }) {
    const referenceLeft = getForestXPercent(1, scaleDomain);

    return (
        <div className="ic-track" aria-hidden="true">
            <div className="ic-refline" style={{ left: `${referenceLeft}%` }} />
            <InterchangeTrackHalf signal={row.signalA} side="a" scaleDomain={scaleDomain} />
            <InterchangeTrackHalf signal={row.signalB} side="b" scaleDomain={scaleDomain} />
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
function InterchangeRow({ row, scaleDomain }) {
    const deltaClass = getInterchangeDeltaClass(row.classification);

    return (
        <div className="ic-row">
            <div className="ic-name" title={row.parameterName}>
                {row.parameterName}
                <span className="sub">{row.parameterCategory || 'SOC not listed'}</span>
            </div>
            <InterchangeTrack row={row} scaleDomain={scaleDomain} />
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
    differencesOnly,
    comparison,
    isLoading,
    error,
    onChangeProductA,
    onChangeProductB,
    onToggleDifferencesOnly,
    onRetry,
}) {
    const comparisonSignals = useMemo(() => getInterchangeSignals(comparison), [comparison]);
    const scaleDomain = useMemo(() => getForestScaleDomain(comparisonSignals), [comparisonSignals]);
    const axisTicks = useMemo(() => getForestTicks(scaleDomain, COMPACT_FOREST_TICKS), [scaleDomain]);
    const hasRunnablePair = Boolean(productA?.documentGuid && productB?.documentGuid)
        && productA.documentGuid.toLowerCase() !== productB.documentGuid.toLowerCase();
    const aConcernCount = comparison.aWorseCount + comparison.onlyACount;
    const bConcernCount = comparison.bWorseCount + comparison.onlyBCount;

    return (
        <section className="panel" aria-labelledby="interchange-title">
            <div className="panel-header">
                <div className="panel-heading">
                    <div id="interchange-title" className="panel-title">Therapeutic interchange comparison</div>
                    <div className="panel-sub">Compare API-derived AE signals across two dashboard products.</div>
                </div>
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

                    <div className="ic-summary">
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num a">{formatInteger(aConcernCount)}</div>
                            <div className="ic-summary-lbl">Higher or unique on product A</div>
                        </div>
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num">{formatInteger(comparison.similarCount)}</div>
                            <div className="ic-summary-lbl">Similar signal rows</div>
                        </div>
                        <div className="ic-summary-cell">
                            <div className="ic-summary-num b">{formatInteger(bConcernCount)}</div>
                            <div className="ic-summary-lbl">Higher or unique on product B</div>
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
                differencesOnly={props.differencesOnly}
                comparison={props.interchangeComparison}
                isLoading={props.isInterchangeLoading}
                error={props.interchangeError}
                onChangeProductA={props.onChangeInterchangeProductA}
                onChangeProductB={props.onChangeInterchangeProductB}
                onToggleDifferencesOnly={props.onToggleDifferencesOnly}
                onRetry={props.onRetryInterchange}
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

    // Selected product drives the header and KPI strip.
    const [selectedProduct, setSelectedProduct] = useState(null);

    // Hydration errors are specific to off-page URL-selected products.
    const [hydrationError, setHydrationError] = useState(null);

    // Active visualization view starts from URL state.
    const [activeView, setActiveView] = useState(initialUrlState.view);

    // Comparator filter starts from URL state.
    const [comparatorFilter, setComparatorFilter] = useState(initialUrlState.comparator);

    // Fragile-row visibility starts from URL state.
    const [showFragile, setShowFragile] = useState(initialUrlState.fragile);

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

    // A 503 from the catalog means the whole dashboard should render the disabled state.
    const isFeatureDisabled = productError instanceof ApiError && productError.isFeatureDisabled;

    // Favorite state is overlaid at render time so async favorite hydration updates the header.
    const selectedProductWithFavoriteState = selectedProduct
        ? applyFavoriteLookup(selectedProduct, favoriteGuids)
        : null;

    // The selected document GUID is the shared key for visualization requests.
    const selectedDocumentGuid = selectedProductWithFavoriteState?.documentGuid ?? '';

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
        interchangeDocumentGuidA,
        interchangeDocumentGuidB,
        interchangeReloadToken,
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
        // Export requires a selected product.
        if (!selectedProductWithFavoriteState) {
            return;
        }

        // The payload intentionally includes only client-safe API view models.
        const exportPayload = {
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

        // Blob URLs avoid server round trips for a client-side export.
        const exportBlob = new Blob([JSON.stringify(exportPayload, null, 2)], {
            type: 'application/json',
        });

        // The object URL is revoked after the synthetic click completes.
        const exportUrl = URL.createObjectURL(exportBlob);

        // The hidden anchor lets browsers use their native download behavior.
        const exportLink = document.createElement('a');

        exportLink.href = exportUrl;
        exportLink.download = `${selectedProductWithFavoriteState.name}-ae-dashboard.json`.replace(/[^a-z0-9._-]+/gi, '-');
        exportLink.click();
        URL.revokeObjectURL(exportUrl);
    }, [
        activeView,
        comparatorFilter,
        filteredTiers,
        forestView,
        interchangeComparison,
        quadrantView,
        reverseLookupResult,
        selectedProductWithFavoriteState,
        showFragile,
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

        // Mirror the disabled rules the in-React top bar previously owned.
        saveButton.disabled = !selectedProductWithFavoriteState || selectedProductWithFavoriteState.isFavorite;
        exportButton.disabled = !selectedProductWithFavoriteState;

        saveButton.addEventListener('click', handleSaveProduct);
        exportButton.addEventListener('click', handleExportDashboard);
        return () => {
            saveButton.removeEventListener('click', handleSaveProduct);
            exportButton.removeEventListener('click', handleExportDashboard);
        };
    }, [selectedProductWithFavoriteState, handleSaveProduct, handleExportDashboard]);

    // Feature-disabled state owns the entire page.
    if (isFeatureDisabled) {
        return <DisabledFeature />;
    }

    return (
        <main className="ae-dashboard-page">
            <div className="app" data-screen-label="AE Dashboard">
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
                    differencesOnly={differencesOnly}
                    interchangeComparison={interchangeComparison}
                    isInterchangeLoading={isInterchangeLoading}
                    interchangeError={interchangeError}
                    onChangeInterchangeProductA={handleChangeInterchangeProductA}
                    onChangeInterchangeProductB={handleChangeInterchangeProductB}
                    onToggleDifferencesOnly={handleToggleDifferencesOnly}
                    onRetryInterchange={retryInterchange}
                />

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
                        Data shown: <code>Database</code> projection for the selected product.
                        Fragile rows render desaturated and can be hidden from the visualization controls.
                    </p>
                </div>
            </div>
        </main>
    );
}

export default App;
