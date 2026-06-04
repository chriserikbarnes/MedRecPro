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
   * @param {{ documentGuidA: string, documentGuidB: string, differencesOnly?: boolean, signal?: AbortSignal }} args - Request options.
   * @returns {Promise<unknown>} API interchange payload.
   */
  getInterchange({ documentGuidA, documentGuidB, differencesOnly = false, signal = null } = {}) {
    // The controller rejects missing or identical GUIDs; the UI blocks those first.
    const url = buildAdverseEventUrl('interchange', {
      documentGuidA,
      documentGuidB,
      differencesOnly,
    });

    return requestJson(url, getFetchOptions(signal));
  },
};
