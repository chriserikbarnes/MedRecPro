import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from './api/apiError';
import { AdverseEventClient } from './api/adverseEventClient';
import { PageHeader } from './components/PageHeader';
import { ProductPicker } from './components/ProductPicker';
import { DisabledFeature } from './components/common/DisabledFeature';
import { EmptyState } from './components/common/EmptyState';
import { InlineError } from './components/common/InlineError';
import { Loading } from './components/common/Loading';
import { useFavorites } from './hooks/useFavorites';
import { useProducts } from './hooks/useProducts';
import { useRecents } from './hooks/useRecents';
import {
  DEFAULT_FOREST_TICKS,
  formatForestTick,
  getForestScaleDomain,
  getForestTicks,
  getForestXPercent,
} from './lib/forestScale';
import { formatDecimal, formatDose, formatInteger } from './lib/formatters';
import { normalizeForest, normalizeQuadrant, normalizeTriage } from './lib/normalizers';

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
 * Renders the MedRecPro logo used by the prototype top bar.
 *
 * @returns {JSX.Element} Logo SVG.
 */
function IconLogo() {
  return (
    <svg viewBox="0 0 95.11 71.96" width="22" height="17" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path d="M0,8l.03,63.93h14.15l-.09-71.93h-6.09C3.58,0,0,3.58,0,8Z" fill="#4a2618" />
      <polygon points="7.87 0 25.91 71.93 41.06 71.93 22.96 0 7.87 0" fill="#4a2618" />
      <polygon points="29.39 0 47.43 71.93 62.58 71.93 44.49 0 29.39 0" fill="#4a2618" />
      <path d="M51.22.03l18.04,71.93h4.89c5.21,0,9.03-4.9,7.76-9.95L66.31.03h-15.09Z" fill="#f4a126" />
      <path d="M95.11,27.68h-15.07L73.1.03h8.85c3.67,0,6.87,2.5,7.76,6.06l5.4,21.6Z" fill="#f4a126" />
    </svg>
  );
}

/**************************************************************/
/**
 * Renders the bookmark icon used by the prototype save action.
 *
 * @returns {JSX.Element} Bookmark icon.
 */
function IconBookmark() {
  return (
    <svg className="topbar-action-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" aria-hidden="true">
      <path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z" />
    </svg>
  );
}

/**************************************************************/
/**
 * Renders the download icon used by the prototype export action.
 *
 * @returns {JSX.Element} Download icon.
 */
function IconDownload() {
  return (
    <svg className="topbar-action-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" aria-hidden="true">
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="7 10 12 15 17 10" />
      <line x1="12" y1="15" x2="12" y2="3" />
    </svg>
  );
}

/**************************************************************/
/**
 * Renders the prototype-style top navigation bar.
 *
 * @param {object} props - Component props.
 * @returns {JSX.Element} Top bar.
 */
function TopBar({ selectedProduct, onSaveProduct, onExportDashboard }) {
  // Save is disabled for missing or already favorited products.
  const isSaveDisabled = !selectedProduct || selectedProduct.isFavorite;

  // Export requires a selected product context.
  const isExportDisabled = !selectedProduct;

  return (
    <div className="topbar" data-screen-label="Topbar">
      <div className="topbar-inner">
        <a className="brand" href="/adverse-events" aria-label="MedRecPro adverse events dashboard">
          <span className="brand-logo"><IconLogo /></span>
          <span>MedRecPro</span>
        </a>
        <span className="brand-sep" />
        <span className="brand-sub">Adverse Events</span>
        <div className="topbar-spacer" />
        <button
          type="button"
          className="topbar-action"
          disabled={isSaveDisabled}
          onClick={onSaveProduct}
        >
          <IconBookmark />
          <span>Save</span>
        </button>
        <button
          type="button"
          className="topbar-action"
          disabled={isExportDisabled}
          onClick={onExportDashboard}
        >
          <IconDownload />
          <span>Export</span>
        </button>
      </div>
    </div>
  );
}

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

  // Tick labels follow the expanded domain instead of trusting server bounds.
  const scaleTicks = useMemo(() => getForestTicks(scaleDomain), [scaleDomain]);

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

  // Initial selection should happen once after products or deep-link hydration become available.
  const hasResolvedInitialSelectionRef = useRef(false);

  // Product catalog state comes from the live API.
  const {
    products,
    productsByGuid,
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

  // A 503 from the catalog means the whole dashboard should render the disabled state.
  const isFeatureDisabled = productError instanceof ApiError && productError.isFeatureDisabled;

  // Favorite state is overlaid at render time so async favorite hydration updates the header.
  const selectedProductWithFavoriteState = selectedProduct
    ? applyFavoriteLookup(selectedProduct, favoriteGuids)
    : null;

  // The selected document GUID is the shared key for visualization requests.
  const selectedDocumentGuid = selectedProductWithFavoriteState?.documentGuid ?? '';

  // Flat triage signals feed counts, exports, and chip labels.
  const triageSignals = useMemo(() => flattenTriageSignals(triageView.tiers), [triageView.tiers]);

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
      // Recent-only snapshots cannot mutate favorites because they may be stale.
      if (!product?.documentGuid || product.isRecentOnly) {
        return;
      }

      // Desired state is the inverse of the current visible flag.
      const nextFavoriteState = !product.isFavorite;

      const updatedProduct = await toggleFavorite(product, nextFavoriteState);

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
    [toggleFavorite, updateProduct],
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
  }, [activeView, comparatorFilter, filteredTiers, forestView, quadrantView, selectedProductWithFavoriteState, showFragile]);

  // Feature-disabled state owns the entire page.
  if (isFeatureDisabled) {
    return <DisabledFeature />;
  }

  return (
    <main className="ae-dashboard-page">
      <TopBar
        selectedProduct={selectedProductWithFavoriteState}
        onSaveProduct={handleSaveProduct}
        onExportDashboard={handleExportDashboard}
      />
      <div className="app" data-screen-label="AE Dashboard">
        <PageHeader
          product={selectedProductWithFavoriteState}
          hydrationError={hydrationError}
          picker={(
            <ProductPicker
              products={visibleProducts}
              favoriteProducts={visibleFavoriteProducts}
              recentProducts={recentProducts}
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

        <div className="foot-note">
          <p>
            <strong>Chart-worthiness</strong> (0&ndash;100) rates how complete and chartable a
            product&apos;s adverse-event data is &mdash; not how safe the drug is. It blends placebo
            coverage (25%), elevated-signal density (25%), SOC breadth out of 17 (20%), dose-data
            coverage (15%), active-comparator coverage (5%), and AE-row volume against a 40-row
            target (10%). Higher scores mean richer, more comparable safety data; the score card
            tooltip lists the top contributors and limiters for the selected product.
          </p>
          <p>
            Data shown: <code>tmp_FlattenedAdverseEventRiskTable</code> projection for the selected product.
            Fragile rows render desaturated and can be hidden from the visualization controls.
          </p>
        </div>
      </div>
    </main>
  );
}

export default App;
