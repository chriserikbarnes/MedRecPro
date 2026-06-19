import { ApiError, readErrorPayload } from './apiError';
import { getAdverseEventApiBase, getFetchOptions } from './apiConfig';

/**************************************************************/
/**
 * Appends query parameters while preserving repeated values such as documentGuids.
 *
 * @param {URL} url - URL object to mutate.
 * @param {Record<string, unknown>} params - Query values to append.
 */
function appendQueryParameters(url, params = {}) {
  // Each entry is inspected so null values can be omitted from the contract.
  for (const [key, value] of Object.entries(params)) {
    // Null, undefined, and empty strings mean the query parameter should be absent.
    if (value === null || value === undefined || value === '') {
      continue;
    }

    // Arrays produce repeated query keys, which the reverse-lookup endpoint expects.
    if (Array.isArray(value)) {
      // Each array value is appended independently to preserve MVC model binding.
      for (const item of value) {
        // Empty array items are ignored rather than serializing invalid query values.
        if (item !== null && item !== undefined && item !== '') {
          url.searchParams.append(key, String(item));
        }
      }

      continue;
    }

    url.searchParams.set(key, String(value));
  }
}

/**************************************************************/
/**
 * Converts the client comparator token into the ASP.NET Core enum name.
 *
 * @param {string} comparator - Client comparator token.
 * @returns {string | null} API comparator value or null for all rows.
 */
function normalizeComparatorParameter(comparator) {
  // The "all" tab is represented by omitting the nullable enum query value.
  if (!comparator || comparator === 'all') {
    return null;
  }

  // Placebo maps directly to the AeComparatorMix.Placebo enum member.
  if (comparator === 'placebo') {
    return 'Placebo';
  }

  // Active maps directly to the AeComparatorMix.Active enum member.
  if (comparator === 'active') {
    return 'Active';
  }

  // Unknown tokens are omitted so bad URL state does not create 400s.
  return null;
}

/**************************************************************/
/**
 * Converts the class-correlation comparator into the ASP.NET Core enum name.
 *
 * @param {string} comparator - Class comparator token or enum value.
 * @returns {'Placebo' | 'Active' | 'Both'} API comparator value.
 */
function normalizeClassComparatorParameter(comparator) {
  // Class mode has no "all" tab; bad URL tokens fall back to the API default.
  const token = String(comparator ?? 'Placebo').trim().toLowerCase();

  if (token === 'active') {
    return 'Active';
  }

  if (token === 'both') {
    return 'Both';
  }

  return 'Placebo';
}

/**************************************************************/
/**
 * Normalizes a selected-system query value to the backend's single-system contract.
 *
 * @param {unknown} systems - Selected system value or array.
 * @returns {string[]} Zero or one system query values.
 */
function normalizeSingleSystemParameter(systems) {
  // Older callers may still pass an array; keep the first non-empty value only.
  const values = Array.isArray(systems) ? systems : [systems];
  const selectedSystem = values
    .map((system) => String(system ?? '').trim())
    .find(Boolean);

  return selectedSystem ? [selectedSystem] : [];
}

/**************************************************************/
/**
 * Converts the configured API base plus endpoint path into a fetchable URL.
 *
 * @param {string} path - Endpoint path beneath /api/AdverseEvent.
 * @param {Record<string, unknown>} params - Query values.
 * @returns {string} Absolute or same-origin URL.
 */
export function buildAdverseEventUrl(path = '', params = {}) {
  // The base can be absolute in Vite dev or relative when MVC-hosted.
  const apiBase = getAdverseEventApiBase();

  // URL requires an origin when the base is relative.
  const origin = globalThis.window?.location?.origin ?? 'http://localhost';

  // Trim separators so callers can pass either "products" or "/products".
  const normalizedPath = path ? `/${path.replace(/^\/+/, '')}` : '';

  // Compose against the browser origin when apiBase is a same-origin path.
  const url = new URL(`${apiBase}${normalizedPath}`, origin);

  appendQueryParameters(url, params);

  // Same-origin production URLs should remain relative for virtual-app friendliness.
  if (apiBase.startsWith('/')) {
    return `${url.pathname}${url.search}`;
  }

  return url.toString();
}

/**************************************************************/
/**
 * Executes a JSON request and converts non-success statuses into ApiError.
 *
 * @param {string} url - URL to request.
 * @param {RequestInit} options - Fetch options.
 * @returns {Promise<unknown>} Parsed JSON payload or null for 204.
 */
async function requestJson(url, options = {}) {
  // Fetch is intentionally direct so credentials and AbortController behavior stay transparent.
  const response = await fetch(url, options);

  // 204 favorite mutations intentionally return no content.
  if (response.status === 204) {
    return null;
  }

  // Any non-2xx status becomes a typed error for consistent UI handling.
  if (!response.ok) {
    const { message, details } = await readErrorPayload(response);
    throw new ApiError(message, response, details);
  }

  // Empty JSON responses are treated as null rather than throwing a syntax error.
  if (response.headers.get('content-length') === '0') {
    return null;
  }

  return response.json();
}

/**************************************************************/
/**
 * Reads a positive integer response header when the server provides one.
 *
 * @param {Headers} headers - Fetch response headers.
 * @param {string} name - Header name.
 * @returns {number | null} Parsed header value or null.
 */
function readIntegerHeader(headers, name) {
  const rawValue = headers.get(name);
  const parsedValue = Number.parseInt(rawValue ?? '', 10);

  return Number.isFinite(parsedValue) && parsedValue >= 0 ? parsedValue : null;
}

/**************************************************************/
/**
 * Executes a paged JSON request and preserves pagination headers.
 *
 * @param {string} url - URL to request.
 * @param {RequestInit} options - Fetch options.
 * @param {{ pageNumber?: number, pageSize?: number }} fallback - Fallback paging values.
 * @returns {Promise<{ items: unknown[], totalCount: number, chartableCount: number, pageNumber: number, pageSize: number }>} Paged payload.
 */
async function requestJsonPage(url, options = {}, fallback = {}) {
  // Class picker pagination needs headers, so this mirrors requestJson without changing other callers.
  const response = await fetch(url, options);

  if (!response.ok) {
    const { message, details } = await readErrorPayload(response);
    throw new ApiError(message, response, details);
  }

  const body = response.status === 204 || response.headers.get('content-length') === '0'
    ? []
    : await response.json();
  const items = Array.isArray(body) ? body : [];
  const pageNumber = readIntegerHeader(response.headers, 'X-Page-Number') ?? fallback.pageNumber ?? 1;
  const pageSize = readIntegerHeader(response.headers, 'X-Page-Size') ?? fallback.pageSize ?? items.length;
  const totalCount = readIntegerHeader(response.headers, 'X-Total-Count') ?? items.length;
  const chartableCount = readIntegerHeader(response.headers, 'X-Chartable-Count')
    ?? items.filter((item) => item?.HasRenderableMap === true || item?.hasRenderableMap === true).length;

  return {
    items,
    totalCount,
    chartableCount,
    pageNumber,
    pageSize,
  };
}

/**************************************************************/
/**
 * API client for the live AdverseEventController dashboard surface.
 */
export const AdverseEventClient = {
  /**************************************************************/
  /**
   * Gets dashboard-ready products for the picker and KPI strip.
   *
   * @param {{ productSearch?: string, pageNumber?: number, pageSize?: number, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API product summary payload.
   */
  getProducts({ productSearch = '', pageNumber = 1, pageSize = 25, signal = null } = {}) {
    // Keep pagination explicit because X-Total-Count is page count only.
    const url = buildAdverseEventUrl('products', {
      productSearch,
      pageNumber,
      pageSize,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the slim, cached product catalog for the picker.
   *
   * @param {{ productSearch?: string, pageNumber?: number, pageSize?: number, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API slim product catalog payload.
   */
  getProductCatalog({ productSearch = '', pageNumber = 1, pageSize = 25, signal = null } = {}) {
    // The catalog endpoint is served from a shared cache, so repeat opens are fast.
    const url = buildAdverseEventUrl('products/catalog', {
      productSearch,
      pageNumber,
      pageSize,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the real distinct-product inventory count for the picker badge.
   *
   * @param {{ signal?: AbortSignal }} args - Request options.
   * @returns {Promise<number>} Distinct product count.
   */
  getProductCount({ signal = null } = {}) {
    // The count reflects actual inventory, independent of search or paging.
    const url = buildAdverseEventUrl('products/count');

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets favorite dashboard products for the authenticated user.
   *
   * @param {{ pageNumber?: number, pageSize?: number, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API favorite product payload.
   */
  getFavorites({ pageNumber = 1, pageSize = 25, signal = null } = {}) {
    // Favorites are policy-gated and may fail with 401 or 403.
    const url = buildAdverseEventUrl('products/favorites', {
      pageNumber,
      pageSize,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Adds one product to the authenticated user's favorites.
   *
   * @param {string} documentGuid - Product document GUID.
   * @returns {Promise<null>} Null on 204 success.
   */
  addFavorite(documentGuid) {
    // The endpoint is idempotent, so the client can update local state after 204.
    const url = buildAdverseEventUrl(`products/${documentGuid}/favorite`);

    return requestJson(
      url,
      getFetchOptions(null, {
        method: 'PUT',
      }),
    );
  },

  /**************************************************************/
  /**
   * Removes one product from the authenticated user's favorites.
   *
   * @param {string} documentGuid - Product document GUID.
   * @returns {Promise<null>} Null on 204 success.
   */
  removeFavorite(documentGuid) {
    // The endpoint is idempotent, so the client can update local state after 204.
    const url = buildAdverseEventUrl(`products/${documentGuid}/favorite`);

    return requestJson(
      url,
      getFetchOptions(null, {
        method: 'DELETE',
      }),
    );
  },

  /**************************************************************/
  /**
   * Gets triage for product-context hydration when no product endpoint exists.
   *
   * @param {string} documentGuid - Product document GUID.
   * @param {{ comparator?: string, includeFragile?: boolean, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API triage payload.
   */
  getTriage(documentGuid, { comparator = 'all', includeFragile = true, signal = null } = {}) {
    // Client tokens are converted into server enum names before serialization.
    const comparatorValue = normalizeComparatorParameter(comparator);

    const url = buildAdverseEventUrl(`products/${documentGuid}/triage`, {
      comparator: comparatorValue,
      includeFragile,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the forest-plot payload for one dashboard product.
   *
   * @param {string} documentGuid - Product document GUID.
   * @param {{ comparator?: string, includeFragile?: boolean, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API forest payload.
   */
  getForest(documentGuid, { comparator = 'all', includeFragile = true, signal = null } = {}) {
    // Client tokens are converted into server enum names before serialization.
    const comparatorValue = normalizeComparatorParameter(comparator);

    const url = buildAdverseEventUrl(`products/${documentGuid}/forest`, {
      comparator: comparatorValue,
      includeFragile,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the quadrant payload for one dashboard product.
   *
   * @param {string} documentGuid - Product document GUID.
   * @param {{ comparator?: string, includeFragile?: boolean, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API quadrant payload.
   */
  getQuadrant(documentGuid, { comparator = 'all', includeFragile = true, signal = null } = {}) {
    // Client tokens are converted into server enum names before serialization.
    const comparatorValue = normalizeComparatorParameter(comparator);

    const url = buildAdverseEventUrl(`products/${documentGuid}/quadrant`, {
      comparator: comparatorValue,
      includeFragile,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets reverse-lookup matches for one exact adverse-event term.
   *
   * @param {{ symptom: string, documentGuids?: string[], signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API reverse-lookup payload.
   */
  getReverseLookup({ symptom, documentGuids = [], signal = null } = {}) {
    // documentGuids intentionally serializes as repeated query keys.
    const url = buildAdverseEventUrl('reverse-lookup', {
      symptom,
      documentGuids,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets a therapeutic-interchange comparison for two dashboard products.
   *
   * @param {{ documentGuidA: string, documentGuidB: string, differencesOnly?: boolean, sharedSignalsOnly?: boolean, comparator?: string, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API interchange payload.
   */
  getInterchange({
    documentGuidA,
    documentGuidB,
    differencesOnly = false,
    sharedSignalsOnly = false,
    comparator = 'all',
    signal = null,
  } = {}) {
    // Client tokens are converted into server enum names before serialization.
    const comparatorValue = normalizeComparatorParameter(comparator);

    // The controller rejects missing or identical GUIDs; the UI blocks those first.
    const url = buildAdverseEventUrl('interchange', {
      documentGuidA,
      documentGuidB,
      differencesOnly,
      sharedSignalsOnly,
      comparator: comparatorValue,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets pharmacologic classes that have AE rows for correlation views.
   *
   * @param {{ classSearch?: string, pageNumber?: number, pageSize?: number, comparator?: string, includeNonSignificant?: boolean, excludeFragile?: boolean, excludeCombos?: boolean, minEvents?: number, minDrugsPerCell?: number, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<{ items: unknown[], totalCount: number, chartableCount: number, pageNumber: number, pageSize: number }>} API class-picker page.
   */
  getCorrelationClasses({
    classSearch = '',
    pageNumber = 1,
    pageSize = 50,
    comparator = undefined,
    includeNonSignificant = undefined,
    excludeFragile = undefined,
    excludeCombos = undefined,
    minEvents = undefined,
    minDrugsPerCell = undefined,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/classes', {
      classSearch,
      pageNumber,
      pageSize,
      comparator: comparator === undefined ? undefined : normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      excludeCombos,
      minEvents,
      minDrugsPerCell,
    });

    return requestJsonPage(url, getFetchOptions(signal), { pageNumber, pageSize });
  },

  /**************************************************************/
  /**
   * Gets MedDRA System Organ Classes that have AE rows for inverse correlation views.
   *
   * @param {{ systemSearch?: string, pageNumber?: number, pageSize?: number, comparator?: string, includeNonSignificant?: boolean, excludeFragile?: boolean, excludeCombos?: boolean, minEvents?: number, minTermsPerCell?: number, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<{ items: unknown[], totalCount: number, chartableCount: number, pageNumber: number, pageSize: number }>} API system-picker page.
   */
  getCorrelationSystems({
    systemSearch = '',
    pageNumber = 1,
    pageSize = 50,
    comparator = undefined,
    includeNonSignificant = undefined,
    excludeFragile = undefined,
    excludeCombos = undefined,
    minEvents = undefined,
    minTermsPerCell = undefined,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/systems', {
      systemSearch,
      pageNumber,
      pageSize,
      comparator: comparator === undefined ? undefined : normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      excludeCombos,
      minEvents,
      minTermsPerCell,
    });

    return requestJsonPage(url, getFetchOptions(signal), { pageNumber, pageSize });
  },

  /**************************************************************/
  /**
   * Gets the SOC by SOC correlation map for one pharmacologic class.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API correlation-map payload.
   */
  getCorrelationMap({
    pharmClassCode,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    minDrugsPerCell = 4,
    method = 'Spearman',
    aggregation = 'MedianLogRr',
    seriousSocOnly = false,
    excludeCombos = false,
    minEvents = 0,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation', {
      pharmClassCode,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      minDrugsPerCell,
      method,
      aggregation,
      seriousSocOnly,
      excludeCombos,
      minEvents,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets a pharmacologic-class by pharmacologic-class map scoped to one selected MedDRA system.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API system correlation-map payload.
   */
  getSystemCorrelationMap({
    systems = [],
    classSearch = '',
    classPageNumber = 1,
    classPageSize = 20,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    minTermsPerCell = 4,
    method = 'Spearman',
    aggregation = 'MedianLogRr',
    excludeCombos = false,
    minEvents = 0,
    includeFullMatrix = false,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/systems/map', {
      systems: normalizeSingleSystemParameter(systems),
      classSearch,
      classPageNumber,
      classPageSize,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      minTermsPerCell,
      method,
      aggregation,
      excludeCombos,
      minEvents,
      includeFullMatrix,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the sparse SOC by drug heatmap for one pharmacologic class.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API correlation-heatmap payload.
   */
  getCorrelationHeatmap({
    pharmClassCode,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    aggregation = 'MedianLogRr',
    seriousSocOnly = false,
    excludeCombos = false,
    minEvents = 0,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/heatmap', {
      pharmClassCode,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      aggregation,
      seriousSocOnly,
      excludeCombos,
      minEvents,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets a sparse pharmacologic-class by drug heatmap scoped to one selected MedDRA system.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API system heatmap payload.
   */
  getSystemCorrelationHeatmap({
    systems = [],
    classSearch = '',
    drugSearch = '',
    classPageNumber = 1,
    classPageSize = 40,
    drugPageNumber = 1,
    drugPageSize = 50,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    aggregation = 'MedianLogRr',
    excludeCombos = false,
    minEvents = 0,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/systems/heatmap', {
      systems: normalizeSingleSystemParameter(systems),
      classSearch,
      drugSearch,
      classPageNumber,
      classPageSize,
      drugPageNumber,
      drugPageSize,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      aggregation,
      excludeCombos,
      minEvents,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets the per-drug drill-down for one SOC by SOC correlation cell.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API correlation-cell payload.
   */
  getCorrelationCell({
    pharmClassCode,
    socX,
    socY,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    minDrugsPerCell = 4,
    method = 'Spearman',
    aggregation = 'MedianLogRr',
    seriousSocOnly = false,
    excludeCombos = false,
    minEvents = 0,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/cell', {
      pharmClassCode,
      socX,
      socY,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      minDrugsPerCell,
      method,
      aggregation,
      seriousSocOnly,
      excludeCombos,
      minEvents,
    });

    return requestJson(url, getFetchOptions(signal));
  },

  /**************************************************************/
  /**
   * Gets shared selected-system term detail for one class-pair correlation cell.
   *
   * @param {object} args - Request options.
   * @returns {Promise<unknown>} API system cell-detail payload.
   */
  getSystemCorrelationCell({
    systems = [],
    classX,
    classY,
    comparator = 'Placebo',
    includeNonSignificant = true,
    excludeFragile = true,
    minTermsPerCell = 4,
    method = 'Spearman',
    aggregation = 'MedianLogRr',
    excludeCombos = false,
    minEvents = 0,
    pageNumber = 1,
    pageSize = 100,
    signal = null,
  } = {}) {
    const url = buildAdverseEventUrl('correlation/systems/cell', {
      systems: normalizeSingleSystemParameter(systems),
      classX,
      classY,
      comparator: normalizeClassComparatorParameter(comparator),
      includeNonSignificant,
      excludeFragile,
      minTermsPerCell,
      method,
      aggregation,
      excludeCombos,
      minEvents,
      pageNumber,
      pageSize,
    });

    return requestJson(url, getFetchOptions(signal));
  },
};
