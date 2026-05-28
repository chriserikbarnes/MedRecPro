/**************************************************************/
/**
 * Shared MedRecPro API URL and fetch configuration helpers.
 *
 * @fileoverview Keeps browser clients aligned on local-development API routing
 * and cookie-bearing fetch options without duplicating environment checks.
 */
/**************************************************************/

/**************************************************************/
/**
 * Detects whether the static site is running from a local development host.
 *
 * @returns {boolean} True when the current hostname is local or private-network.
 */
/**************************************************************/
export function isLocalDevelopment() {
    const hostname = window.location.hostname;

    return hostname === 'localhost'
        || hostname === '127.0.0.1'
        || hostname.startsWith('192.168.')
        || hostname.startsWith('10.')
        || hostname === '::1';
}

/**************************************************************/
/**
 * Gets the API base URL for the current browser environment.
 *
 * @returns {string} Local API origin in development; otherwise an empty string.
 */
/**************************************************************/
export function getApiBaseUrl() {
    return isLocalDevelopment() ? 'http://localhost:5093' : '';
}

/**************************************************************/
/**
 * Builds a full API URL from an endpoint path.
 *
 * @param {string} endpointPath Endpoint path beginning with "/api/".
 * @returns {string} Fully qualified local URL or same-origin relative URL.
 */
/**************************************************************/
export function buildUrl(endpointPath) {
    return `${getApiBaseUrl()}${endpointPath}`;
}

/**************************************************************/
/**
 * Gets fetch options with credentials included for cookie authentication.
 *
 * @param {RequestInit} options Additional fetch options.
 * @returns {RequestInit} Fetch options with credentials included.
 */
/**************************************************************/
export function getFetchOptions(options = {}) {
    return {
        credentials: 'include',
        ...options
    };
}
