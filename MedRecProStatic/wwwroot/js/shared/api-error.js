/**************************************************************/
/**
 * Shared API error helpers for MedRecPro browser clients.
 *
 * @fileoverview Normalizes HTTP status, response text, and retry hints into a
 * small error object that renderers can display consistently.
 */
/**************************************************************/

/**************************************************************/
/**
 * Error type representing a non-successful API response.
 */
/**************************************************************/
export class ApiError extends Error {
    /**************************************************************/
    /**
     * Initializes a new API error.
     *
     * @param {number} status HTTP status code.
     * @param {string} message Human-readable message.
     * @param {string} body Raw response body when available.
     */
    /**************************************************************/
    constructor(status, message, body = '') {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.body = body;
    }

    /**************************************************************/
    /**
     * Gets whether the error is an authentication or permission failure.
     *
     * @returns {boolean} True for 401 or 403 responses.
     */
    /**************************************************************/
    get isAuthFailure() {
        return this.status === 401 || this.status === 403;
    }

    /**************************************************************/
    /**
     * Gets whether the error indicates the feature is disabled.
     *
     * @returns {boolean} True for service-unavailable responses.
     */
    /**************************************************************/
    get isFeatureDisabled() {
        return this.status === 503;
    }
}

/**************************************************************/
/**
 * Creates an ApiError from a fetch response.
 *
 * @param {Response} response Fetch response object.
 * @returns {Promise<ApiError>} Parsed API error.
 */
/**************************************************************/
export async function createApiError(response) {
    let body = '';

    try {
        body = await response.text();
    } catch (error) {
        body = '';
    }

    const message = body && !body.trim().startsWith('<')
        ? body.trim()
        : defaultMessageForStatus(response.status);

    return new ApiError(response.status, message, body);
}

/**************************************************************/
/**
 * Maps common HTTP statuses to dashboard-friendly fallback text.
 *
 * @param {number} status HTTP status code.
 * @returns {string} Default status message.
 */
/**************************************************************/
export function defaultMessageForStatus(status) {
    switch (status) {
        case 400:
            return 'The request could not be validated. Check the selected filters and try again.';
        case 401:
        case 403:
            return 'Sign in with API access to use this action.';
        case 404:
            return 'The selected product is not available in the AE dashboard.';
        case 503:
            return 'The AE dashboard feature is currently disabled.';
        case 500:
            return 'The API hit an unexpected error. You can retry this request.';
        default:
            return `API request failed with HTTP ${status}.`;
    }
}
