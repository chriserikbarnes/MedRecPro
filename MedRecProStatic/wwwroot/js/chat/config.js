/**************************************************************/
/**
 * MedRecPro Chat Configuration Module
 *
 * @fileoverview Manages API configuration, environment detection, and endpoint definitions.
 * This module centralizes all configuration settings for the chat interface, enabling
 * seamless switching between local development and production environments.
 *
 * @description
 * The configuration module provides:
 * - Environment detection (local vs production)
 * - API base URL resolution
 * - Endpoint path definitions
 * - Polling and retry configuration
 *
 * @example
 * // Import and use configuration
 * import { ChatConfig } from './config.js';
 *
 * const url = ChatConfig.buildUrl('/api/Ai/interpret');
 * const isLocal = ChatConfig.isLocalDevelopment();
 *
 * @module chat/config
 * @see ChatState - State management module that uses this configuration
 * @see ApiService - API communication module that consumes endpoint definitions
 */
/**************************************************************/

export const ChatConfig = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Detects if the application is running in a local development environment.
     *
     * @returns {boolean} True if running on localhost, local IP, or IPv6 loopback
     *
     * @description
     * Identifies local development by checking the hostname against common local patterns:
     * - localhost: Standard local development hostname
     * - 127.0.0.1: IPv4 loopback address
     * - 192.168.x.x: Private network addresses (local network testing)
     * - 10.x.x.x: Private network addresses (corporate networks)
     * - ::1: IPv6 loopback address
     *
     * @example
     * if (isLocalDevelopment()) {
     *     console.log('Running in development mode');
     * }
     *
     * @see buildApiConfig - Uses this function to determine API base URL
     */
    /**************************************************************/
    function isLocalDevelopment() {
        const hostname = window.location.hostname;

        // Check for common local development hostnames and IP patterns
        return hostname === 'localhost' ||
            hostname === '127.0.0.1' ||
            hostname.startsWith('192.168.') ||
            hostname.startsWith('10.') ||
            hostname === '::1';
    }

    /**************************************************************/
    /**
     * Builds the API configuration object based on the current environment.
     *
     * @returns {Object} API configuration object containing:
     *   - baseUrl {string}: The base URL for API requests
     *   - endpoints {Object}: Map of endpoint names to paths
     *   - pollInterval {number}: Milliseconds between polling requests
     *   - maxRetryAttempts {number}: Maximum number of retry attempts for failed requests
     *
     * @description
     * Creates environment-aware API configuration:
     * - Local development: Routes to localhost:5093 (requires CORS configuration)
     * - Production: Uses relative URLs for same-origin requests
     *
     * The endpoints object maps logical names to API paths, centralizing
     * all endpoint definitions for easy maintenance.
     *
     * @example
     * const config = buildApiConfig();
     * console.log(config.baseUrl);        // '' or 'http://localhost:5093'
     * console.log(config.endpoints.chat); // '/api/Ai/chat'
     *
     * @see isLocalDevelopment - Used to determine environment
     * @see API_CONFIG - The singleton configuration instance
     */
    /**************************************************************/
    function buildApiConfig() {
        // Determine base URL based on environment
        // Local development uses explicit localhost URL for cross-origin requests
        // Production uses relative URLs since API is same-origin
        const baseUrl = isLocalDevelopment()
            ? 'http://localhost:5093'  // Local API server (requires CORS)
            : '';                       // Relative URLs for production (same-origin)

        return {
            baseUrl: baseUrl,

            /**************************************************************/
            /**
             * API endpoint path definitions.
             *
             * @property {string} context - Retrieves system context (demo mode, settings)
             * @property {string} interpret - Interprets user messages and suggests API calls
             * @property {string} synthesize - Synthesizes API results into natural language
             * @property {string} retry - Handles retry logic for failed API calls
             * @property {string} chat - Direct chat endpoint (alternative flow)
             * @property {string} upload - File upload endpoint for label imports
             *
             * @see ApiService.fetchSystemContext - Uses context endpoint
             * @see ApiService.interpretMessage - Uses interpret endpoint
             * @see ApiService.synthesizeResults - Uses synthesize endpoint
             */
            /**************************************************************/
            endpoints: {
                // AI Controller endpoints - Handle natural language processing
                context: '/api/Ai/context',
                interpret: '/api/Ai/interpret',
                synthesize: '/api/Ai/synthesize',
                retry: '/api/Ai/retry',
                chat: '/api/Ai/chat',

                // Labels Controller endpoint - File upload for drug label imports
                upload: '/api/Label/import'
            },

            // Polling configuration for async operations (e.g., file imports)
            pollInterval: 1000,  // 1 second between progress checks

            // Retry configuration for failed API calls
            maxRetryAttempts: 3  // Maximum retry attempts before giving up
        };
    }

    // Initialize configuration singleton
    const API_CONFIG = buildApiConfig();

    // Log environment information for debugging
    console.log('[MedRecPro Chat] Environment:', isLocalDevelopment() ? 'Local Development' : 'Production');
    console.log('[MedRecPro Chat] API Base URL:', API_CONFIG.baseUrl || '(relative)');

    /**************************************************************/
    /**
     * Builds a full API URL from an endpoint path.
     *
     * @param {string} endpointPath - The endpoint path (e.g., '/api/Ai/context')
     * @returns {string} Full URL for the API call
     *
     * @description
     * Combines the configured base URL with the endpoint path.
     * - In production (empty baseUrl): Returns just the endpoint for same-origin requests
     * - In local development: Prepends the localhost server URL
     *
     * @example
     * // Production
     * buildUrl('/api/Ai/interpret');  // Returns '/api/Ai/interpret'
     *
     * // Local development
     * buildUrl('/api/Ai/interpret');  // Returns 'http://localhost:5093/api/Ai/interpret'
     *
     * @see API_CONFIG.baseUrl - The base URL used for URL construction
     */
    /**************************************************************/
    function buildUrl(endpointPath) {
        return API_CONFIG.baseUrl + endpointPath;
    }

    /**************************************************************/
    /**
     * Gets fetch options with credentials included for cookie-based authentication.
     *
     * @param {Object} [options={}] - Additional fetch options to merge
     * @returns {Object} Fetch options with credentials: 'include'
     *
     * @description
     * Ensures cookies are sent with all API requests, required for authentication
     * to work in both local and production environments. The 'include' credentials
     * mode sends cookies even for cross-origin requests.
     *
     * @example
     * // Basic usage
     * fetch(url, getFetchOptions());
     *
     * // With additional options
     * fetch(url, getFetchOptions({
     *     method: 'POST',
     *     headers: { 'Content-Type': 'application/json' },
     *     body: JSON.stringify(data)
     * }));
     *
     * @see buildUrl - Often used together with this function
     */
    /**************************************************************/
    function getFetchOptions(options = {}) {
        return {
            credentials: 'include',  // Include cookies for authentication
            ...options               // Merge additional options
        };
    }

    /**************************************************************/
    /**
     * Public API for the configuration module.
     *
     * @description
     * Exposes configuration values and helper functions for use by other modules.
     * The API_CONFIG object is frozen to prevent accidental modification.
     */
    /**************************************************************/
    return {
        // Configuration values (read-only)
        API_CONFIG: Object.freeze(API_CONFIG),

        // Environment detection
        isLocalDevelopment: isLocalDevelopment,

        // URL building utilities
        buildUrl: buildUrl,
        getFetchOptions: getFetchOptions
    };
})();
