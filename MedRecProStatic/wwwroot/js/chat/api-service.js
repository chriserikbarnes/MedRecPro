/**************************************************************/
/**
 * MedRecPro Chat API Service Module
 *
 * @fileoverview Handles all API communication for the chat interface.
 * Provides methods for interpreting messages, synthesizing results, and retrying failed requests.
 *
 * @description
 * The API service module provides:
 * - System context fetching (demo mode, settings)
 * - Message interpretation (NLP to API endpoint mapping)
 * - Result synthesis (API responses to natural language)
 * - Retry logic for failed API calls
 * - URL building for API endpoints
 *
 * @example
 * import { ApiService } from './api-service.js';
 *
 * // Fetch system context
 * await ApiService.fetchSystemContext();
 *
 * // Interpret a user message
 * const interpretation = await ApiService.interpretMessage('What is aspirin?');
 *
 * @module chat/api-service
 * @see ChatConfig - API configuration and URL building
 * @see EndpointExecutor - Executes interpreted endpoints
 * @see ChatState - Stores conversation context
 */
/**************************************************************/

import { ChatConfig } from './config.js';
import { ChatState } from './state.js';

export const ApiService = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Fetches the system context from the API.
     *
     * @returns {Promise<Object|null>} System context object or null on failure
     *
     * @description
     * Retrieves system configuration including:
     * - isDemoMode: Whether running in demo mode
     * - demoModeMessage: Banner text for demo mode display
     *
     * Updates state with context and returns it for caller use.
     * Logs helpful CORS warnings for local development.
     *
     * @example
     * const context = await ApiService.fetchSystemContext();
     * if (context?.isDemoMode) {
     *     showDemoBanner(context.demoModeMessage);
     * }
     *
     * @see ChatConfig.API_CONFIG.endpoints.context - Context endpoint path
     * @see ChatState.setSystemContext - Stores context in state
     */
    /**************************************************************/
    async function fetchSystemContext() {
        try {
            const contextUrl = ChatConfig.buildUrl(ChatConfig.API_CONFIG.endpoints.context);
            console.log('[ApiService] Fetching context from:', contextUrl);

            const response = await fetch(contextUrl, ChatConfig.getFetchOptions());

            if (response.ok) {
                const context = await response.json();
                ChatState.setSystemContext(context);
                return context;
            }

            return null;

        } catch (error) {
            console.error('[ApiService] Failed to fetch system context:', error);

            // Provide helpful CORS guidance for local development
            if (ChatConfig.isLocalDevelopment()) {
                console.warn(
                    '[ApiService] CORS may not be configured on the server. ' +
                    'Ensure the API at medrecpro.com allows localhost origins.'
                );
            }

            return null;
        }
    }

    /**************************************************************/
    /**
     * Interprets a user message using the AI interpretation endpoint.
     *
     * @param {string} userMessage - The user's message to interpret
     * @param {Array} [conversationHistory=[]] - Previous messages for context
     * @param {Object} [importResult=null] - Result from file import (if any)
     * @returns {Promise<Object>} Interpretation result containing:
     *   - thinking: AI's reasoning process
     *   - directResponse: Response if no API call needed
     *   - suggestedEndpoints: Array of API endpoints to call
     *
     * @description
     * Sends the user message to the interpret endpoint for NLP processing.
     * The AI analyzes the message and either:
     * 1. Returns a direct response (no API call needed)
     * 2. Suggests API endpoints to call for data retrieval
     *
     * @example
     * const result = await ApiService.interpretMessage(
     *     'What are the side effects of aspirin?',
     *     conversationHistory
     * );
     *
     * if (result.directResponse) {
     *     // Display direct response
     * } else if (result.suggestedEndpoints) {
     *     // Execute suggested endpoints
     * }
     *
     * @see ChatConfig.API_CONFIG.endpoints.interpret - Interpret endpoint path
     * @see EndpointExecutor.executeEndpointsWithDependencies - Executes endpoints
     */
    /**************************************************************/
    async function interpretMessage(userMessage, conversationHistory = [], importResult = null) {
        const interpretUrl = ChatConfig.buildUrl(ChatConfig.API_CONFIG.endpoints.interpret);
        console.log('[ApiService] Calling interpret endpoint:', interpretUrl);

        const response = await fetch(interpretUrl, ChatConfig.getFetchOptions({
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                userMessage: userMessage,
                conversationId: ChatState.getConversationId(),
                conversationHistory: conversationHistory,
                importResult: importResult
            }),
            signal: ChatState.getAbortController()?.signal
        }));

        if (!response.ok) {
            throw new Error(`API error: ${response.status}`);
        }

        const interpretation = await response.json();

        // Normalize endpoint property name (backend may return 'endpoints' or 'suggestedEndpoints')
        if (interpretation.endpoints && !interpretation.suggestedEndpoints) {
            interpretation.suggestedEndpoints = interpretation.endpoints;
        }

        return interpretation;
    }

    /**************************************************************/
    /**
     * Synthesizes API results into a natural language response.
     *
     * @param {string} originalQuery - The user's original question
     * @param {Object} interpretation - The interpretation result
     * @param {Array} executedEndpoints - Results from executed API calls
     * @returns {Promise<Object>} Synthesis result containing:
     *   - response: Natural language response text
     *   - suggestedFollowUps: Array of suggested follow-up questions
     *   - dataReferences: Map of display names to URLs for viewing full labels
     *
     * @description
     * Takes raw API data and transforms it into a human-readable response.
     * The synthesis considers:
     * - The original user intent
     * - All API results (successful and failed)
     * - Conversation context
     *
     * @example
     * const synthesis = await ApiService.synthesizeResults(
     *     'What is aspirin used for?',
     *     interpretation,
     *     apiResults
     * );
     * displayResponse(synthesis.response);
     *
     * @see ChatConfig.API_CONFIG.endpoints.synthesize - Synthesize endpoint path
     */
    /**************************************************************/
    async function synthesizeResults(originalQuery, interpretation, executedEndpoints) {
        const synthesizeUrl = ChatConfig.buildUrl(ChatConfig.API_CONFIG.endpoints.synthesize);
        console.log('[ApiService] Calling synthesize endpoint:', synthesizeUrl);

        const response = await fetch(synthesizeUrl, ChatConfig.getFetchOptions({
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                originalQuery: originalQuery,
                interpretation: interpretation,
                executedEndpoints: executedEndpoints,
                conversationId: ChatState.getConversationId()
            }),
            signal: ChatState.getAbortController()?.signal
        }));

        if (response.ok) {
            return await response.json();
        }

        // Return fallback on failure
        return {
            response: 'Unable to synthesize results. Please try again.',
            suggestedFollowUps: [],
            dataReferences: {}
        };
    }

    /**************************************************************/
    /**
     * Attempts to retry interpretation when endpoints fail.
     *
     * @param {string} originalQuery - The user's original query
     * @param {Object} originalInterpretation - The original interpretation that failed
     * @param {Array} failedResults - Array of failed endpoint results
     * @param {Function} onProgress - Callback for progress updates
     * @param {number} [attemptNumber=1] - Current attempt number (1-3)
     * @returns {Promise<Array>} Array of results from retry, or empty array if retry failed
     *
     * @description
     * Implements recursive retry logic for failed API calls:
     * 1. Sends failed results to retry endpoint
     * 2. Gets alternative endpoints or direct response
     * 3. Executes new endpoints
     * 4. Recursively retries if still failing (up to max attempts)
     *
     * @example
     * const retryResults = await ApiService.attemptRetryInterpretation(
     *     'What is aspirin?',
     *     originalInterpretation,
     *     failedEndpoints,
     *     (status) => updateProgress(status),
     *     1
     * );
     *
     * @see ChatConfig.API_CONFIG.maxRetryAttempts - Max retry count
     * @see ChatConfig.API_CONFIG.endpoints.retry - Retry endpoint path
     */
    /**************************************************************/
    async function attemptRetryInterpretation(originalQuery, originalInterpretation, failedResults, onProgress, attemptNumber = 1) {
        const maxAttempts = ChatConfig.API_CONFIG.maxRetryAttempts || 3;

        // Check retry limit
        if (attemptNumber > maxAttempts) {
            console.log(`[ApiService] Max retry attempts (${maxAttempts}) reached`);
            return [];
        }

        console.log(`[ApiService] Retry attempt ${attemptNumber} of ${maxAttempts}`);

        // Update progress
        if (onProgress) {
            onProgress(`Retrying with alternative approach (attempt ${attemptNumber}/${maxAttempts})...`);
        }

        try {
            const retryUrl = ChatConfig.buildUrl(ChatConfig.API_CONFIG.endpoints.retry);
            console.log('[ApiService] Calling retry endpoint:', retryUrl);

            // Build retry request with failed results info
            const retryRequest = {
                originalRequest: {
                    userMessage: originalQuery,
                    conversationId: ChatState.getConversationId(),
                    systemContext: ChatState.getSystemContext()
                },
                failedResults: failedResults.map(r => ({
                    specification: r.specification || { method: 'GET', path: r.endpoint },
                    statusCode: r.statusCode || 500,
                    error: r.error || 'Unknown error'
                })),
                attemptNumber: attemptNumber
            };

            const retryResponse = await fetch(retryUrl, ChatConfig.getFetchOptions({
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(retryRequest),
                signal: ChatState.getAbortController()?.signal
            }));

            if (!retryResponse.ok) {
                console.log('[ApiService] Retry interpretation request failed:', retryResponse.status);
                return [];
            }

            const retryInterpretation = await retryResponse.json();

            // Handle direct response (no further API calls needed)
            if (retryInterpretation.isDirectResponse) {
                console.log('[ApiService] Retry returned direct response');
                return [{
                    isDirectResponse: true,
                    content: retryInterpretation.directResponse || retryInterpretation.explanation
                }];
            }

            // Handle no new endpoints
            const newEndpoints = retryInterpretation.endpoints || retryInterpretation.suggestedEndpoints || [];
            if (newEndpoints.length === 0) {
                console.log('[ApiService] Retry returned no new endpoints');
                return [];
            }

            console.log(`[ApiService] Retry suggested ${newEndpoints.length} new endpoint(s)`);

            // Execute new endpoints
            const newResults = [];
            for (const endpoint of newEndpoints) {
                if (onProgress) {
                    onProgress(`Trying: ${endpoint.description || endpoint.path}...`);
                }

                try {
                    const result = await executeEndpoint(endpoint);
                    newResults.push(result);
                    console.log('[ApiService] Retry endpoint', endpoint.path,
                        result.statusCode >= 200 && result.statusCode < 300 ? 'succeeded' : 'failed');
                } catch (error) {
                    newResults.push({
                        specification: endpoint,
                        statusCode: 500,
                        error: error.message
                    });
                }
            }

            // Check for successful results
            const successfulResults = newResults.filter(
                r => r.statusCode >= 200 && r.statusCode < 300
            );

            if (successfulResults.length > 0) {
                console.log(`[ApiService] Retry succeeded with ${successfulResults.length} result(s)`);
                return newResults;
            }

            // Recursive retry with combined failures
            console.log('[ApiService] Retry endpoints also failed, attempting next retry...');
            const allFailedResults = [
                ...failedResults,
                ...newResults.filter(r => r.statusCode >= 400 || r.error)
            ];

            return await attemptRetryInterpretation(
                originalQuery,
                retryInterpretation,
                allFailedResults,
                onProgress,
                attemptNumber + 1
            );

        } catch (error) {
            console.error('[ApiService] Error in retry interpretation:', error);
            return [];
        }
    }

    /**************************************************************/
    /**
     * Executes a single API endpoint.
     *
     * @param {Object} endpoint - Endpoint specification
     * @param {string} endpoint.path - API path
     * @param {string} [endpoint.method='GET'] - HTTP method
     * @param {Object} [endpoint.queryParameters] - Query string parameters
     * @param {Object} [endpoint.body] - Request body for POST requests
     * @returns {Promise<Object>} Result object with statusCode, result/error
     *
     * @description
     * Low-level endpoint execution used by retry logic.
     * For complex multi-step execution with dependencies,
     * use EndpointExecutor.executeEndpointsWithDependencies.
     *
     * @example
     * const result = await executeEndpoint({
     *     path: '/api/Label/search',
     *     method: 'GET',
     *     queryParameters: { term: 'aspirin' }
     * });
     *
     * @see buildApiUrl - Constructs full URL from endpoint spec
     * @see EndpointExecutor - Higher-level execution with dependencies
     */
    /**************************************************************/
    async function executeEndpoint(endpoint) {
        const startTime = Date.now();
        const apiUrl = buildApiUrl(endpoint);
        const fullUrl = ChatConfig.buildUrl(apiUrl);

        const response = await fetch(fullUrl, ChatConfig.getFetchOptions({
            method: endpoint.method || 'GET',
            headers: endpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
            body: endpoint.method === 'POST' ? JSON.stringify(endpoint.body) : undefined,
            signal: ChatState.getAbortController()?.signal
        }));

        if (response.ok) {
            const data = await response.json();
            return {
                specification: endpoint,
                statusCode: response.status,
                result: data,
                executionTimeMs: Date.now() - startTime
            };
        } else {
            return {
                specification: endpoint,
                statusCode: response.status,
                result: null,
                error: `HTTP ${response.status}`,
                executionTimeMs: Date.now() - startTime
            };
        }
    }

    /**************************************************************/
    /**
     * Builds a full API URL from an endpoint specification.
     *
     * @param {Object} endpoint - Endpoint specification object
     * @param {string} endpoint.path - Base path (e.g., '/api/Label/search')
     * @param {Object} [endpoint.queryParameters] - Key-value query parameters
     * @returns {string} URL path with query parameters
     *
     * @description
     * Constructs the URL path portion including query string.
     * The result should be passed to ChatConfig.buildUrl() for full URL.
     *
     * Handles:
     * - Base path preservation
     * - Query parameter encoding
     * - Null/undefined parameter filtering
     *
     * @example
     * const path = buildApiUrl({
     *     path: '/api/Label/search',
     *     queryParameters: { term: 'aspirin', limit: 10 }
     * });
     * // Returns: '/api/Label/search?term=aspirin&limit=10'
     *
     * @see ChatConfig.buildUrl - Adds base URL to path
     */
    /**************************************************************/
    function buildApiUrl(endpoint) {
        let url = endpoint.path;

        // Add query parameters if present
        if (endpoint.queryParameters) {
            const params = new URLSearchParams();

            for (const [key, value] of Object.entries(endpoint.queryParameters)) {
                // Skip null/undefined values
                if (value !== null && value !== undefined) {
                    params.append(key, value);
                }
            }

            const queryString = params.toString();
            if (queryString) {
                // Handle existing query string in path
                url += (url.includes('?') ? '&' : '?') + queryString;
            }
        }

        return url;
    }

    /**************************************************************/
    /**
     * Formats API endpoint results as markdown links for transparency.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {string} Markdown formatted string with API links
     *
     * @description
     * Creates clickable links to the underlying API endpoints so users
     * can explore the raw data directly. Separates:
     * - Primary data sources (successful endpoints with data)
     * - Fallback sources (rescue endpoints that were used)
     *
     * @example
     * const sourceLinks = formatApiSourceLinks(results);
     * if (sourceLinks) {
     *     response += sourceLinks;
     * }
     *
     * @see synthesizeResults - Appends source links to response
     */
    /**************************************************************/
    function formatApiSourceLinks(results) {
        if (!results || results.length === 0) {
            return '';
        }

        // Categorize results
        const primarySources = [];
        const fallbackSourcesUsed = [];

        results.forEach(r => {
            if (!r.specification) return;

            const isFallback = r.specification.skipIfPreviousHasResults !== undefined;
            const wasSkipped = r.skipped === true;
            const hasData = r.hasData === true && r.statusCode >= 200 && r.statusCode < 300;

            if (wasSkipped && r.skippedReason === 'previous_has_results') {
                // Skipped fallback - don't show
                return;
            } else if (hasData && r.result) {
                if (isFallback) {
                    fallbackSourcesUsed.push(r);
                } else {
                    primarySources.push(r);
                }
            }
        });

        // Filter to sources with actual content
        const filterWithContent = (sources) => sources.filter(r => {
            if (!r.result) return false;
            if (Array.isArray(r.result)) return r.result.length > 0;
            if (typeof r.result === 'object') return Object.keys(r.result).length > 0;
            return r.result !== null && r.result !== undefined && r.result !== '';
        });

        const sourcesWithContent = filterWithContent(primarySources);
        const fallbacksWithContent = filterWithContent(fallbackSourcesUsed);

        if (sourcesWithContent.length === 0 && fallbacksWithContent.length === 0) {
            return '';
        }

        // Build markdown
        let markdown = '\n\n---\n';

        // Helper to build description
        const buildDescription = (r) => {
            let description = r.specification.description || r.specification.path;
            description = description.replace(/\{\{?documentGuid\}?\}/gi, '').trim();
            description = description.replace(/[\s:-]+$/, '').trim();

            // Try to extract product name
            const productName = extractProductName(r.result);
            if (productName) {
                description = `${description} - ${productName}`;
            }

            return description;
        };

        // Primary sources
        if (sourcesWithContent.length > 0) {
            markdown += '**Data sources:**\n';
            sourcesWithContent.forEach(r => {
                const description = buildDescription(r);
                const fullUrl = ChatConfig.buildUrl(buildApiUrl(r.specification));
                markdown += `- [${description}](${fullUrl})\n`;
            });
        }

        // Fallback sources
        if (fallbacksWithContent.length > 0) {
            if (sourcesWithContent.length > 0) markdown += '\n';
            markdown += '**Fallback sources used:**\n';
            fallbacksWithContent.forEach(r => {
                const description = buildDescription(r);
                const fullUrl = ChatConfig.buildUrl(buildApiUrl(r.specification));
                markdown += `- [${description}](${fullUrl})\n`;
            });
        }

        return markdown;
    }

    /**************************************************************/
    /**
     * Extracts product name from API result data.
     *
     * @param {Object|Array} data - API response data
     * @returns {string|null} Product name or null if not found
     *
     * @description
     * Searches common field names for product/drug names in the response.
     * Truncates long names and removes FDA boilerplate prefixes.
     *
     * @see formatApiSourceLinks - Uses for source descriptions
     */
    /**************************************************************/
    function extractProductName(data) {
        if (!data) return null;

        const item = Array.isArray(data) ? (data[0] || {}) : data;

        const fieldNames = [
            'productName', 'ProductName', 'product_name',
            'title', 'Title',
            'documentDisplayName', 'displayName',
            'name', 'Name'
        ];

        for (const field of fieldNames) {
            if (item[field] && typeof item[field] === 'string') {
                return cleanProductName(item[field]);
            }

            // Check nested wrappers
            const wrappers = ['sectionContent', 'document', 'label'];
            for (const wrapper of wrappers) {
                if (item[wrapper] && item[wrapper][field]) {
                    return cleanProductName(item[wrapper][field]);
                }
            }
        }

        return null;
    }

    /**************************************************************/
    /**
     * Cleans up product name for display.
     *
     * @param {string} name - Raw product name
     * @returns {string|null} Cleaned name or null
     *
     * @description
     * Truncates long names and removes common FDA boilerplate.
     */
    /**************************************************************/
    function cleanProductName(name) {
        if (!name) return null;

        // Truncate long names
        if (name.length > 50) {
            name = name.substring(0, 47) + '...';
        }

        // Remove FDA boilerplate prefixes
        const prefixes = [
            'These highlights do not include all the information needed to use',
            'HIGHLIGHTS OF PRESCRIBING INFORMATION'
        ];

        for (const prefix of prefixes) {
            if (name.toLowerCase().startsWith(prefix.toLowerCase())) {
                name = name.substring(prefix.length).trim();
                name = name.replace(/^[.,:\-;\s]+/, '');
                break;
            }
        }

        return name || null;
    }

    /**************************************************************/
    /**
     * Public API for the API service module.
     *
     * @description
     * Exposes API communication methods.
     */
    /**************************************************************/
    return {
        // Context and interpretation
        fetchSystemContext: fetchSystemContext,
        interpretMessage: interpretMessage,
        synthesizeResults: synthesizeResults,

        // Retry logic
        attemptRetryInterpretation: attemptRetryInterpretation,

        // URL building
        buildApiUrl: buildApiUrl,

        // Source formatting
        formatApiSourceLinks: formatApiSourceLinks
    };
})();
