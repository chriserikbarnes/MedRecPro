/**************************************************************/
/**
 * Adverse-event dashboard API client.
 */
/**************************************************************/

import { buildUrl, getFetchOptions } from '../shared/api-config.js';
import { createApiError } from '../shared/api-error.js';
import { comparatorToApiValue } from './formatters.js';
import {
    normalizeForest,
    normalizeInterchange,
    normalizeProduct,
    normalizeQuadrant,
    normalizeReverseLookup,
    normalizeTriage
} from './normalizers.js';

const API_ROOT = '/api/AdverseEvent';

/**************************************************************/
/**
 * Fetches dashboard-ready products.
 *
 * @param {Object} options Search and paging options.
 * @returns {Promise<Array<Object>>} Product view models.
 */
/**************************************************************/
export async function getProducts({ productSearch = '', pageNumber = 1, pageSize = 50, signal = null } = {}) {
    const params = new URLSearchParams();
    if (productSearch.trim()) {
        params.set('productSearch', productSearch.trim());
    }
    params.set('pageNumber', String(pageNumber));
    params.set('pageSize', String(pageSize));

    const data = await request(`${API_ROOT}/products?${params.toString()}`, { signal });
    return Array.isArray(data) ? data.map(normalizeProduct).filter(Boolean) : [];
}

/**************************************************************/
/**
 * Fetches the current user's favorite dashboard products.
 *
 * @returns {Promise<Array<Object>>} Favorite products.
 */
/**************************************************************/
export async function getFavoriteProducts() {
    const data = await request(`${API_ROOT}/products/favorites?pageNumber=1&pageSize=50`);
    return Array.isArray(data) ? data.map(normalizeProduct).filter(Boolean) : [];
}

/**************************************************************/
/**
 * Persists favorite state for a product.
 *
 * @param {string} documentGuid Product document GUID.
 * @param {boolean} isFavorite Desired favorite state.
 * @returns {Promise<void>} Resolves when saved.
 */
/**************************************************************/
export async function setFavoriteProduct(documentGuid, isFavorite) {
    await request(`${API_ROOT}/products/${encodeURIComponent(documentGuid)}/favorite`, {
        method: isFavorite ? 'PUT' : 'DELETE'
    });
}

/**************************************************************/
/**
 * Fetches a product triage view.
 *
 * @param {string} documentGuid Product document GUID.
 * @param {string} comparator Comparator state.
 * @param {boolean} includeFragile Fragile-row flag.
 * @param {AbortSignal} signal Abort signal.
 * @returns {Promise<Object>} Triage view model.
 */
/**************************************************************/
export async function getTriage(documentGuid, comparator, includeFragile, signal = null) {
    const data = await request(productViewPath(documentGuid, 'triage', comparator, includeFragile), { signal });
    return normalizeTriage(data);
}

/**************************************************************/
/**
 * Fetches a product forest plot.
 *
 * @param {string} documentGuid Product document GUID.
 * @param {string} comparator Comparator state.
 * @param {boolean} includeFragile Fragile-row flag.
 * @param {AbortSignal} signal Abort signal.
 * @returns {Promise<Object>} Forest plot view model.
 */
/**************************************************************/
export async function getForest(documentGuid, comparator, includeFragile, signal = null) {
    const data = await request(productViewPath(documentGuid, 'forest', comparator, includeFragile), { signal });
    return normalizeForest(data);
}

/**************************************************************/
/**
 * Fetches a product quadrant view.
 *
 * @param {string} documentGuid Product document GUID.
 * @param {string} comparator Comparator state.
 * @param {boolean} includeFragile Fragile-row flag.
 * @param {AbortSignal} signal Abort signal.
 * @returns {Promise<Object>} Quadrant view model.
 */
/**************************************************************/
export async function getQuadrant(documentGuid, comparator, includeFragile, signal = null) {
    const data = await request(productViewPath(documentGuid, 'quadrant', comparator, includeFragile), { signal });
    return normalizeQuadrant(data);
}

/**************************************************************/
/**
 * Performs an exact-term symptom reverse lookup.
 *
 * @param {string} symptom Exact adverse-event term.
 * @param {Array<string>} documentGuids Optional product scope.
 * @returns {Promise<Object>} Reverse lookup result.
 */
/**************************************************************/
export async function reverseLookup(symptom, documentGuids = []) {
    const params = new URLSearchParams();
    params.set('symptom', symptom.trim());
    documentGuids.forEach(guid => params.append('documentGuids', guid));

    const data = await request(`${API_ROOT}/reverse-lookup?${params.toString()}`);
    return normalizeReverseLookup(data);
}

/**************************************************************/
/**
 * Fetches an interchange comparison.
 *
 * @param {string} documentGuidA Product A document GUID.
 * @param {string} documentGuidB Product B document GUID.
 * @param {boolean} differencesOnly Differences-only flag.
 * @returns {Promise<Object>} Interchange comparison.
 */
/**************************************************************/
export async function getInterchange(documentGuidA, documentGuidB, differencesOnly = false) {
    const params = new URLSearchParams();
    params.set('documentGuidA', documentGuidA);
    params.set('documentGuidB', documentGuidB);
    params.set('differencesOnly', String(Boolean(differencesOnly)));

    const data = await request(`${API_ROOT}/interchange?${params.toString()}`);
    return normalizeInterchange(data);
}

/**************************************************************/
/**
 * Builds a product view endpoint path.
 */
/**************************************************************/
function productViewPath(documentGuid, view, comparator, includeFragile) {
    const params = new URLSearchParams();
    const comparatorValue = comparatorToApiValue(comparator);
    if (comparatorValue) {
        params.set('comparator', comparatorValue);
    }
    params.set('includeFragile', String(Boolean(includeFragile)));

    return `${API_ROOT}/products/${encodeURIComponent(documentGuid)}/${view}?${params.toString()}`;
}

/**************************************************************/
/**
 * Executes an API request and parses JSON or 204 responses.
 */
/**************************************************************/
async function request(endpointPath, options = {}) {
    const response = await fetch(buildUrl(endpointPath), getFetchOptions(options));

    if (response.status === 204) {
        return null;
    }

    if (!response.ok) {
        throw await createApiError(response);
    }

    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
        return await response.json();
    }

    return await response.text();
}
