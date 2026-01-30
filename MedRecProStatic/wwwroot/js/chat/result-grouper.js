/**************************************************************/
/**
 * Result Grouper Module
 *
 * @fileoverview Groups endpoint execution results by product/drug for checkpoint
 * display and batch synthesis.
 *
 * @description
 * The result grouper module provides:
 * - Grouping of endpoint results by unique product identifier
 * - Product name extraction from various API response formats
 * - Data size estimation for batch optimization
 * - Result filtering for meaningful content detection
 *
 * This module enables the checkpoint system to display results organized
 * by product rather than by individual API endpoint.
 *
 * @example
 * import { ResultGrouper } from './result-grouper.js';
 *
 * // Group results by product
 * const groups = ResultGrouper.groupResultsByProduct(endpointResults);
 * // groups = { 'lisinopril-guid': { name: 'Lisinopril', results: [...] }, ... }
 *
 * @module chat/result-grouper
 * @see CheckpointManager - Uses grouped results for checkpoint decisions
 * @see BatchSynthesizer - Uses groups for batch processing
 * @see CheckpointRenderer - Displays grouped results in checkpoint UI
 */
/**************************************************************/

export const ResultGrouper = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Common field names for product identifiers across API responses.
     *
     * @description
     * Different API endpoints return product identifiers under various
     * field names. This list covers the known variations in the MedRecPro API.
     *
     * @see extractProductIdentifier - Uses these fields
     */
    /**************************************************************/
    const IDENTIFIER_FIELDS = [
        'documentGuid', 'DocumentGuid', 'document_guid',
        'productId', 'ProductId', 'product_id',
        'labelId', 'LabelId', 'label_id',
        'unii', 'UNII',
        'ndc', 'NDC',
        'applicationNumber', 'ApplicationNumber'
    ];

    /**************************************************************/
    /**
     * Common field names for product display names across API responses.
     *
     * @description
     * Different API endpoints return product names under various
     * field names. This list covers the known variations in the MedRecPro API.
     *
     * @see extractProductName - Uses these fields
     */
    /**************************************************************/
    const NAME_FIELDS = [
        'productName', 'ProductName', 'product_name',
        'brandName', 'BrandName', 'brand_name',
        'genericName', 'GenericName', 'generic_name',
        'title', 'Title',
        'displayName', 'DisplayName', 'display_name',
        'documentDisplayName', 'DocumentDisplayName',
        'name', 'Name',
        'substanceName', 'SubstanceName', 'substance_name'
    ];

    /**************************************************************/
    /**
     * Common wrapper object names that may contain product data.
     *
     * @description
     * API responses often nest product data inside wrapper objects.
     * These are the known wrapper names to search within.
     *
     * @see findFieldInObject - Uses these wrappers
     */
    /**************************************************************/
    const WRAPPER_NAMES = [
        'sectionContent', 'SectionContent',
        'document', 'Document',
        'label', 'Label',
        'product', 'Product',
        'data', 'Data',
        'result', 'Result'
    ];

    /**************************************************************/
    /**
     * Groups endpoint results by extracted product identifier.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {Object} Map of product identifiers to product groups
     *
     * @description
     * Each product group contains:
     * - id: Unique product identifier
     * - name: Display-friendly product name
     * - results: Array of endpoint results for this product
     * - totalDataSize: Estimated total data size in characters
     * - hasData: Whether any results contain meaningful data
     *
     * Results without identifiable products are grouped under 'unknown'.
     *
     * @example
     * const groups = groupResultsByProduct(endpointResults);
     * // {
     * //   'abc-123-guid': {
     * //     id: 'abc-123-guid',
     * //     name: 'Lisinopril 10mg Tablets',
     * //     results: [{...}, {...}],
     * //     totalDataSize: 15000,
     * //     hasData: true
     * //   },
     * //   ...
     * // }
     *
     * @see extractProductIdentifier - Gets product ID from result
     * @see extractProductName - Gets display name from result
     */
    /**************************************************************/
    function groupResultsByProduct(results) {
        if (!results || !Array.isArray(results) || results.length === 0) {
            return {};
        }

        const groups = {};
        let unknownCount = 0;

        results.forEach((result, index) => {
            // Skip results that failed or have no data
            if (result.statusCode >= 400 || result.error) {
                return;
            }

            // Extract product identifier
            let productId = extractProductIdentifier(result);
            let productName = extractProductName(result);

            // Generate fallback identifier if none found
            if (!productId) {
                // Try to generate from endpoint path
                productId = generateIdFromEndpoint(result);

                if (!productId) {
                    unknownCount++;
                    productId = `unknown-${unknownCount}`;
                }
            }

            // Generate fallback name if none found
            if (!productName) {
                productName = generateNameFromEndpoint(result);
            }

            // Create or update group
            if (!groups[productId]) {
                groups[productId] = {
                    id: productId,
                    name: productName || productId,
                    results: [],
                    totalDataSize: 0,
                    hasData: false,
                    endpointDescriptions: []
                };
            } else if (productName && result.extractedProductName) {
                // Update group name if we found a better (extracted) name
                // Prefer extractedProductName over generated names
                groups[productId].name = productName;
            }

            // Add result to group
            groups[productId].results.push(result);

            // Update data size estimate
            const dataSize = getResultDataSize(result);
            groups[productId].totalDataSize += dataSize;

            // Update hasData flag
            if (dataSize > 0 && hasResultData(result)) {
                groups[productId].hasData = true;
            }

            // Track endpoint descriptions for context
            if (result.specification && result.specification.description) {
                const desc = result.specification.description;
                if (!groups[productId].endpointDescriptions.includes(desc)) {
                    groups[productId].endpointDescriptions.push(desc);
                }
            }
        });

        // Filter out non-product groups (like "all" which is a meta-result)
        const invalidNames = ['all', 'unknown', 'undefined', 'null'];
        const filteredGroups = {};
        for (const [id, group] of Object.entries(groups)) {
            const lowerName = (group.name || '').toLowerCase();
            const lowerId = (id || '').toLowerCase();

            // Skip if name or id is in the invalid list
            if (invalidNames.includes(lowerName) || invalidNames.includes(lowerId)) {
                console.log(`[ResultGrouper] Filtering out non-product group: id="${id}", name="${group.name}"`);
                continue;
            }
            filteredGroups[id] = group;
        }

        // Sort groups by name for consistent display
        const sortedGroups = {};
        Object.keys(filteredGroups)
            .sort((a, b) => {
                const nameA = filteredGroups[a].name.toLowerCase();
                const nameB = filteredGroups[b].name.toLowerCase();
                return nameA.localeCompare(nameB);
            })
            .forEach(key => {
                sortedGroups[key] = filteredGroups[key];
            });

        console.log(`[ResultGrouper] Grouped ${results.length} results into ${Object.keys(sortedGroups).length} product groups (filtered from ${Object.keys(groups).length})`);

        return sortedGroups;
    }

    /**************************************************************/
    /**
     * Extracts unique product identifier from a result.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {string|null} Product identifier or null if not found
     *
     * @description
     * Searches for known identifier fields in the result data.
     * Handles both top-level and nested data structures.
     *
     * @see IDENTIFIER_FIELDS - Field names to search
     * @see findFieldInObject - Helper for nested search
     */
    /**************************************************************/
    function extractProductIdentifier(result) {
        if (!result) return null;

        // Check result.result (the actual API response data)
        const data = result.result;
        if (!data) return null;

        // Handle array responses - use first item
        const item = Array.isArray(data) ? (data[0] || {}) : data;

        // Search for identifier fields
        for (const field of IDENTIFIER_FIELDS) {
            const value = findFieldInObject(item, field);
            if (value && typeof value === 'string' && value.length > 0) {
                return value;
            }
        }

        return null;
    }

    /**************************************************************/
    /**
     * Extracts display-friendly product name from a result.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {string|null} Product name or null if not found
     *
     * @description
     * First checks for pre-extracted product name (set during endpoint execution).
     * Then searches for known name fields in the result data.
     * Cleans and truncates the name for display.
     *
     * @see NAME_FIELDS - Field names to search
     * @see cleanProductName - Cleans the extracted name
     * @see EndpointExecutor.extractProgressProductName - Sets extractedProductName
     */
    /**************************************************************/
    function extractProductName(result) {
        if (!result) return null;

        // Check for pre-extracted product name (most reliable source)
        if (result.extractedProductName && typeof result.extractedProductName === 'string') {
            return result.extractedProductName;
        }

        const data = result.result;
        if (!data) return null;

        // Handle array responses
        const item = Array.isArray(data) ? (data[0] || {}) : data;

        // Search for name fields
        for (const field of NAME_FIELDS) {
            const value = findFieldInObject(item, field);
            if (value && typeof value === 'string' && value.length > 0) {
                return cleanProductName(value);
            }
        }

        return null;
    }

    /**************************************************************/
    /**
     * Searches for a field in an object, including nested wrappers.
     *
     * @param {Object} obj - Object to search
     * @param {string} fieldName - Field name to find
     * @returns {*} Field value or undefined if not found
     *
     * @description
     * First checks top-level properties, then searches within
     * known wrapper objects for the field.
     *
     * @see WRAPPER_NAMES - Wrapper objects to search within
     */
    /**************************************************************/
    function findFieldInObject(obj, fieldName) {
        if (!obj || typeof obj !== 'object') return undefined;

        // Check direct property (exact match)
        if (obj.hasOwnProperty(fieldName)) {
            return obj[fieldName];
        }

        // Check case-insensitive match at top level
        const lowerFieldName = fieldName.toLowerCase();
        for (const key of Object.keys(obj)) {
            if (key.toLowerCase() === lowerFieldName) {
                return obj[key];
            }
        }

        // Search in known wrapper objects
        for (const wrapper of WRAPPER_NAMES) {
            if (obj[wrapper] && typeof obj[wrapper] === 'object') {
                const wrapperObj = obj[wrapper];

                // Direct match in wrapper
                if (wrapperObj.hasOwnProperty(fieldName)) {
                    return wrapperObj[fieldName];
                }

                // Case-insensitive match in wrapper
                for (const key of Object.keys(wrapperObj)) {
                    if (key.toLowerCase() === lowerFieldName) {
                        return wrapperObj[key];
                    }
                }
            }
        }

        return undefined;
    }

    /**************************************************************/
    /**
     * Generates a product identifier from the endpoint specification.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {string|null} Generated identifier or null
     *
     * @description
     * When no identifier is found in the response data, attempts
     * to extract an identifier from the endpoint path (e.g., document GUID
     * in the URL path).
     */
    /**************************************************************/
    function generateIdFromEndpoint(result) {
        if (!result || !result.specification) return null;

        const path = result.specification.path || '';

        // Look for GUID patterns in the path
        // Pattern: 8-4-4-4-12 hex characters
        const guidMatch = path.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
        if (guidMatch) {
            return guidMatch[0].toLowerCase();
        }

        // Look for other common ID patterns
        // Pattern: /api/something/{id} where id is alphanumeric
        const pathIdMatch = path.match(/\/([A-Za-z0-9_-]{10,})\/?(?:\?|$)/);
        if (pathIdMatch) {
            return pathIdMatch[1];
        }

        return null;
    }

    /**************************************************************/
    /**
     * Generates a product name from the endpoint specification.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {string|null} Generated name or null
     *
     * @description
     * When no name is found in the response data, attempts to
     * generate a meaningful name from the endpoint description
     * or path.
     */
    /**************************************************************/
    function generateNameFromEndpoint(result) {
        if (!result || !result.specification) return null;

        // Use description if available
        if (result.specification.description) {
            let desc = result.specification.description;

            // Remove variable placeholders
            desc = desc.replace(/\{\{?[^}]+\}?\}/g, '').trim();

            // Clean up trailing punctuation
            desc = desc.replace(/[\s:,;-]+$/, '').trim();

            if (desc.length > 0) {
                return cleanProductName(desc);
            }
        }

        // Extract from path as last resort
        const path = result.specification.path || '';
        const pathParts = path.split('/').filter(p => p && !p.startsWith('api'));

        if (pathParts.length > 0) {
            // Use the most descriptive path segment
            const segment = pathParts.find(p => p.length > 3 && !/^[0-9a-f-]+$/i.test(p)) || pathParts[0];
            return cleanProductName(segment.replace(/[-_]/g, ' '));
        }

        return null;
    }

    /**************************************************************/
    /**
     * Cleans up a product name for display.
     *
     * @param {string} name - Raw product name
     * @returns {string|null} Cleaned name or null if invalid
     *
     * @description
     * Truncates long names, removes FDA boilerplate, and
     * normalizes formatting for consistent display.
     */
    /**************************************************************/
    function cleanProductName(name) {
        if (!name || typeof name !== 'string') return null;

        let cleaned = name.trim();

        // Remove FDA boilerplate prefixes
        const boilerplatePrefixes = [
            'These highlights do not include all the information needed to use',
            'HIGHLIGHTS OF PRESCRIBING INFORMATION',
            'See full prescribing information for complete boxed warning'
        ];

        for (const prefix of boilerplatePrefixes) {
            if (cleaned.toLowerCase().startsWith(prefix.toLowerCase())) {
                cleaned = cleaned.substring(prefix.length).trim();
                cleaned = cleaned.replace(/^[.,:\-;\s]+/, '').trim();
                break;
            }
        }

        // Truncate long names
        if (cleaned.length > 60) {
            cleaned = cleaned.substring(0, 57) + '...';
        }

        // Capitalize first letter if all lowercase
        if (cleaned === cleaned.toLowerCase()) {
            cleaned = cleaned.charAt(0).toUpperCase() + cleaned.slice(1);
        }

        return cleaned || null;
    }

    /**************************************************************/
    /**
     * Estimates the data size of a result in characters.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {number} Estimated size in characters
     *
     * @description
     * Provides a rough estimate of data size for batch optimization.
     * Uses JSON stringify length as approximation.
     *
     * @see groupResultsByProduct - Uses for totalDataSize calculation
     */
    /**************************************************************/
    function getResultDataSize(result) {
        if (!result || !result.result) return 0;

        try {
            // Use JSON stringify length as rough estimate
            const jsonStr = JSON.stringify(result.result);
            return jsonStr.length;
        } catch (e) {
            // If stringify fails, estimate from object keys
            if (typeof result.result === 'object') {
                return Object.keys(result.result).length * 100;
            }
            return 0;
        }
    }

    /**************************************************************/
    /**
     * Checks if a result has meaningful data content.
     *
     * @param {Object} result - Endpoint execution result
     * @returns {boolean} True if result contains meaningful data
     *
     * @description
     * Determines whether a result actually contains usable data
     * versus being empty or containing only metadata.
     */
    /**************************************************************/
    function hasResultData(result) {
        if (!result) return false;

        // Check for error state
        if (result.statusCode >= 400 || result.error) return false;

        // Check for skipped state
        if (result.skipped === true) return false;

        const data = result.result;
        if (!data) return false;

        // Check arrays
        if (Array.isArray(data)) {
            return data.length > 0;
        }

        // Check objects
        if (typeof data === 'object') {
            // Filter out metadata-only objects
            const keys = Object.keys(data);
            const dataKeys = keys.filter(k =>
                !['statusCode', 'headers', 'meta', 'pagination', 'error'].includes(k)
            );
            return dataKeys.length > 0;
        }

        // Primitives (strings, numbers) - consider as having data if truthy
        return Boolean(data);
    }

    /**************************************************************/
    /**
     * Gets the count of unique products across all groups.
     *
     * @param {Object} productGroups - Result from groupResultsByProduct
     * @returns {number} Count of unique product groups
     *
     * @see CheckpointManager.shouldShowCheckpoint - Uses this count
     */
    /**************************************************************/
    function getProductCount(productGroups) {
        if (!productGroups || typeof productGroups !== 'object') return 0;
        return Object.keys(productGroups).length;
    }

    /**************************************************************/
    /**
     * Filters groups to only those with actual data.
     *
     * @param {Object} productGroups - Result from groupResultsByProduct
     * @returns {Object} Filtered groups with data
     *
     * @description
     * Removes groups that have no meaningful data content.
     * Useful for checkpoint display where empty groups add noise.
     */
    /**************************************************************/
    function filterGroupsWithData(productGroups) {
        if (!productGroups || typeof productGroups !== 'object') return {};

        const filtered = {};

        for (const [id, group] of Object.entries(productGroups)) {
            if (group.hasData) {
                filtered[id] = group;
            }
        }

        return filtered;
    }

    /**************************************************************/
    /**
     * Gets all results from selected product groups.
     *
     * @param {Object} productGroups - All product groups
     * @param {Array<string>} selectedIds - Array of selected product IDs
     * @returns {Array} Flat array of results from selected groups
     *
     * @description
     * After user selects products in checkpoint UI, this extracts
     * the actual results to be synthesized.
     *
     * @see CheckpointManager.getSelectedResults - Uses this function
     */
    /**************************************************************/
    function getResultsFromGroups(productGroups, selectedIds) {
        if (!productGroups || !selectedIds || selectedIds.length === 0) {
            return [];
        }

        const results = [];

        selectedIds.forEach(id => {
            if (productGroups[id] && productGroups[id].results) {
                results.push(...productGroups[id].results);
            }
        });

        return results;
    }

    /**************************************************************/
    /**
     * Converts groups to an array format sorted by name.
     *
     * @param {Object} productGroups - Product groups object
     * @returns {Array} Array of group objects with index property
     *
     * @description
     * Useful for rendering in UI where array iteration is needed.
     * Adds index property for checkbox identification.
     *
     * @see CheckpointRenderer.renderCheckpointPanel - Uses this format
     */
    /**************************************************************/
    function groupsToArray(productGroups) {
        if (!productGroups || typeof productGroups !== 'object') return [];

        return Object.values(productGroups).map((group, index) => ({
            ...group,
            index: index
        }));
    }

    /**************************************************************/
    /**
     * Public API for the result grouper module.
     *
     * @description
     * Exposes result grouping and extraction utilities.
     */
    /**************************************************************/
    return {
        // Main grouping function
        groupResultsByProduct: groupResultsByProduct,

        // Individual extraction functions
        extractProductIdentifier: extractProductIdentifier,
        extractProductName: extractProductName,

        // Data utilities
        getResultDataSize: getResultDataSize,
        hasResultData: hasResultData,

        // Group utilities
        getProductCount: getProductCount,
        filterGroupsWithData: filterGroupsWithData,
        getResultsFromGroups: getResultsFromGroups,
        groupsToArray: groupsToArray
    };
})();
