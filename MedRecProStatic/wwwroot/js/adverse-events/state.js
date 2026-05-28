/**************************************************************/
/**
 * Shared mutable state for the adverse-event dashboard.
 */
/**************************************************************/

/**************************************************************/
/**
 * Dashboard state singleton.
 */
/**************************************************************/
export const dashboardState = {
    products: [],
    productsByGuid: new Map(),
    selectedDocumentGuid: null,
    activeView: 'triage',
    comparator: 'all',
    includeFragile: true,
    productSearch: '',
    favorites: new Set(),
    recents: [],
    viewCache: new Map(),
    reverseLookup: {
        symptom: '',
        scopeDocumentGuids: [],
        suggestions: new Set(),
        result: null
    },
    interchange: {
        documentGuidA: null,
        documentGuidB: null,
        differencesOnly: false,
        result: null
    },
    loading: {},
    errors: {},
    favoriteAccessDenied: false,
    featureDisabled: false,
    expandedSignals: new Set(),
    activeRequests: {}
};

/**************************************************************/
/**
 * Builds a stable cache key for product data.
 *
 * @param {string} search Search text.
 * @param {number} pageNumber Page number.
 * @param {number} pageSize Page size.
 * @returns {string} Cache key.
 */
/**************************************************************/
export function productsCacheKey(search, pageNumber, pageSize) {
    return `products:${search || ''}:${pageNumber}:${pageSize}`;
}

/**************************************************************/
/**
 * Builds a cache key for a product-level view.
 *
 * @param {string} view View name.
 * @param {string} documentGuid Product document GUID.
 * @param {string} comparator Comparator state.
 * @param {boolean} includeFragile Fragile-row flag.
 * @returns {string} Cache key.
 */
/**************************************************************/
export function productViewCacheKey(view, documentGuid, comparator, includeFragile) {
    return `${view}:${documentGuid}:${comparator}:${includeFragile}`;
}

/**************************************************************/
/**
 * Builds a reverse-lookup cache key.
 *
 * @param {string} symptom Adverse-event term.
 * @param {Array<string>} scopeDocumentGuids Product scope.
 * @returns {string} Cache key.
 */
/**************************************************************/
export function reverseCacheKey(symptom, scopeDocumentGuids = []) {
    return `reverse:${symptom || ''}:${scopeDocumentGuids.slice().sort().join(',')}`;
}

/**************************************************************/
/**
 * Builds an interchange cache key.
 *
 * @param {string} documentGuidA Product A GUID.
 * @param {string} documentGuidB Product B GUID.
 * @param {boolean} differencesOnly Differences-only flag.
 * @returns {string} Cache key.
 */
/**************************************************************/
export function interchangeCacheKey(documentGuidA, documentGuidB, differencesOnly) {
    return `interchange:${documentGuidA}:${documentGuidB}:${differencesOnly}`;
}
