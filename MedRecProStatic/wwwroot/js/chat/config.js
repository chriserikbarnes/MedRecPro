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

import { buildUrl, getApiBaseUrl, getFetchOptions, isLocalDevelopment } from '../shared/api-config.js';

export const ChatConfig = (function () {
    'use strict';

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
        return {
            baseUrl: getApiBaseUrl(),

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
