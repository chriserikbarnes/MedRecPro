/**************************************************************/
/**
 * MedRecPro Chat Endpoint Execution Module
 *
 * @fileoverview Handles complex multi-step API endpoint execution with dependency resolution.
 * Supports variable extraction, template substitution, and conditional execution.
 *
 * @description
 * The endpoint executor module provides:
 * - Multi-step endpoint execution with dependency ordering
 * - Variable extraction from API responses (explicit and auto-extraction)
 * - Template variable substitution in endpoint paths/parameters
 * - Array expansion for batch operations
 * - Conditional execution (skipIfPreviousHasResults)
 * - Case-insensitive and deep property searching
 *
 * This module implements the core workflow orchestration for complex queries
 * that require multiple API calls with data flowing between steps.
 *
 * @example
 * import { EndpointExecutor } from './endpoint-executor.js';
 *
 * // Execute endpoints with dependencies
 * const results = await EndpointExecutor.executeEndpointsWithDependencies(
 *     suggestedEndpoints,
 *     assistantMessage,
 *     abortController
 * );
 *
 * @module chat/endpoint-executor
 * @see ApiService - Provides URL building and fetch utilities
 * @see ChatConfig - API configuration
 * @see MessageRenderer - Updates message progress during execution
 */
/**************************************************************/

import { ChatConfig } from './config.js';
import { ChatState } from './state.js';
import { ApiService } from './api-service.js';
import { MessageRenderer } from './message-renderer.js';

export const EndpointExecutor = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Gets a property value from an object using case-insensitive matching.
     *
     * @param {Object} obj - The object to search
     * @param {string} propName - Property name to find (case-insensitive)
     * @returns {*} Property value or undefined if not found
     *
     * @description
     * Handles PascalCase vs camelCase differences between frontend/backend.
     * Example: matches 'DocumentGuid' when searching for 'documentGuid'.
     *
     * @example
     * const obj = { DocumentGUID: 'abc-123' };
     * getCaseInsensitiveProperty(obj, 'documentGuid'); // 'abc-123'
     *
     * @see findPropertyDeep - Uses this for leaf property matching
     */
    /**************************************************************/
    function getCaseInsensitiveProperty(obj, propName) {
        if (!obj || typeof obj !== 'object') return undefined;

        // Try exact match first (fastest path)
        if (obj.hasOwnProperty(propName)) {
            return obj[propName];
        }

        // Try case-insensitive match
        const lowerPropName = propName.toLowerCase();
        for (const key of Object.keys(obj)) {
            if (key.toLowerCase() === lowerPropName) {
                console.log(`[EndpointExecutor] Case-insensitive match: '${propName}' -> '${key}'`);
                return obj[key];
            }
        }

        return undefined;
    }

    /**************************************************************/
    /**
     * Recursively searches for a property anywhere in the object hierarchy.
     *
     * @param {Object|Array} obj - Object/array to search
     * @param {string} propName - Property name to find (case-insensitive)
     * @param {number} [maxDepth=10] - Maximum recursion depth
     * @param {number} [currentDepth=0] - Current recursion depth
     * @param {string} [currentPath='$'] - Current path for logging
     * @returns {*} First matching property value (depth-first) or undefined
     *
     * @description
     * Performs depth-first search through nested objects and arrays
     * to find a property regardless of nesting level. Useful when
     * the exact response structure varies or is deeply nested.
     *
     * @example
     * const data = {
     *     wrapper: {
     *         nested: {
     *             documentGuid: 'abc-123'
     *         }
     *     }
     * };
     * findPropertyDeep(data, 'documentGuid'); // 'abc-123'
     *
     * @see autoExtractCommonFields - Uses for automatic field extraction
     */
    /**************************************************************/
    function findPropertyDeep(obj, propName, maxDepth = 10, currentDepth = 0, currentPath = '$') {
        // Depth limit check
        if (currentDepth > maxDepth) {
            return undefined;
        }

        // Null/primitive check
        if (!obj || typeof obj !== 'object') {
            return undefined;
        }

        // Array: search each element
        if (Array.isArray(obj)) {
            for (let i = 0; i < obj.length; i++) {
                const result = findPropertyDeep(
                    obj[i], propName, maxDepth, currentDepth + 1, `${currentPath}[${i}]`
                );
                if (result !== undefined) {
                    return result;
                }
            }
            return undefined;
        }

        // Object: check direct properties first
        const lowerPropName = propName.toLowerCase();
        for (const key of Object.keys(obj)) {
            if (key.toLowerCase() === lowerPropName) {
                console.log(`[EndpointExecutor] Deep search: found '${propName}' at path '${currentPath}.${key}'`);
                return obj[key];
            }
        }

        // Recursively search nested objects
        for (const key of Object.keys(obj)) {
            const value = obj[key];
            if (value && typeof value === 'object') {
                const result = findPropertyDeep(
                    value, propName, maxDepth, currentDepth + 1, `${currentPath}.${key}`
                );
                if (result !== undefined) {
                    return result;
                }
            }
        }

        return undefined;
    }

    /**************************************************************/
    /**
     * Extracts a single value from data using a path expression.
     *
     * @param {Object|Array} data - Data to extract from
     * @param {string} path - JSONPath-like expression
     * @returns {*} Extracted value or undefined
     *
     * @description
     * Supports path expressions like:
     * - "$[0].productName" (array index + property)
     * - "$.results.documentGuid" (nested properties)
     * - "documentGuid" (direct property or deep search)
     *
     * If direct path navigation fails, falls back to deep search.
     *
     * @example
     * const data = [{ product: { name: 'Aspirin' } }];
     * extractSingleValue(data, '$[0].product.name'); // 'Aspirin'
     *
     * @see extractValueByPath - Public interface supporting array extraction
     */
    /**************************************************************/
    function extractSingleValue(data, path) {
        if (!data || !path) {
            return undefined;
        }

        console.log(`[EndpointExecutor] Data type: ${Array.isArray(data) ? 'array' : typeof data}`);

        // Log structure for debugging
        if (Array.isArray(data)) {
            console.log(`[EndpointExecutor] Array length: ${data.length}`);
            if (data.length > 0) {
                const firstKeys = Object.keys(data[0]);
                console.log(`[EndpointExecutor] First element keys: [${firstKeys.join(', ')}]`);

                // Show nested structure if wrapper object present
                for (const key of firstKeys) {
                    if (typeof data[0][key] === 'object' && data[0][key] !== null) {
                        const nestedKeys = Object.keys(data[0][key]);
                        console.log(`[EndpointExecutor] Nested '${key}' keys: [${nestedKeys.slice(0, 5).join(', ')}${nestedKeys.length > 5 ? '...' : ''}]`);
                    }
                }
            }
        }

        // Remove leading $ if present
        let cleanPath = path.startsWith('$') ? path.substring(1) : path;

        // Split by . and [] separators
        const parts = cleanPath.split(/\.|\[|\]/).filter(p => p !== '');
        console.log(`[EndpointExecutor] Path parts: [${parts.join(', ')}]`);

        let current = data;
        let lastPropertyName = null;

        for (let i = 0; i < parts.length; i++) {
            const part = parts[i];

            if (current === null || current === undefined) {
                console.log(`[EndpointExecutor] Path traversal stopped: current is null/undefined at part '${part}'`);
                return undefined;
            }

            // Handle array index
            if (/^\d+$/.test(part)) {
                const index = parseInt(part, 10);
                if (Array.isArray(current) && index < current.length) {
                    current = current[index];
                    console.log(`[EndpointExecutor] Accessed array index [${index}]`);
                } else {
                    console.log(`[EndpointExecutor] Array index [${index}] out of bounds or not an array`);
                    return undefined;
                }
            } else {
                lastPropertyName = part;

                // Try direct property access (case-insensitive)
                let value = getCaseInsensitiveProperty(current, part);

                // Fall back to deep search if not found directly
                if (value === undefined) {
                    console.log(`[EndpointExecutor] Property '${part}' not found at current level, trying deep search...`);
                    value = findPropertyDeep(current, part, 5);
                }

                if (value === undefined) {
                    console.log(`[EndpointExecutor] Property '${part}' not found anywhere in current object`);
                    return undefined;
                }

                current = value;
                const displayValue = typeof current === 'string' ? `"${current}"` :
                    typeof current === 'object' ? '{object}' : String(current);
                console.log(`[EndpointExecutor] Found property '${part}' = ${displayValue}`);
            }
        }

        console.log(`[EndpointExecutor] === EXTRACTION RESULT: ${current} ===`);
        return current;
    }

    /**************************************************************/
    /**
     * Finds all arrays nested within an object structure.
     *
     * @param {Object|Array} obj - Object to search
     * @param {number} [maxDepth=5] - Maximum recursion depth
     * @param {number} [currentDepth=0] - Current recursion depth
     * @returns {Array<Array>} All found arrays (flattened for extraction)
     *
     * @description
     * Recursively searches through nested objects to find arrays.
     * Used when the data structure contains nested arrays (like productsByClass).
     *
     * @example
     * const data = { productsByClass: { "GLP-1 [EPC]": [{...}, {...}] } };
     * findNestedArrays(data); // Returns [{...}, {...}]
     *
     * @see extractValueByPath - Uses this for nested array extraction
     */
    /**************************************************************/
    function findNestedArrays(obj, maxDepth = 5, currentDepth = 0) {
        if (currentDepth > maxDepth || !obj || typeof obj !== 'object') {
            return [];
        }

        // If obj itself is an array, return it
        if (Array.isArray(obj)) {
            return obj;
        }

        // Search through object properties
        let foundArrays = [];
        for (const key of Object.keys(obj)) {
            const value = obj[key];
            if (Array.isArray(value) && value.length > 0) {
                // Found an array - add its contents
                console.log(`[EndpointExecutor] Found nested array at key '${key}' with ${value.length} elements`);
                foundArrays = foundArrays.concat(value);
            } else if (value && typeof value === 'object' && !Array.isArray(value)) {
                // Recurse into nested objects
                const nestedArrays = findNestedArrays(value, maxDepth, currentDepth + 1);
                if (nestedArrays.length > 0) {
                    foundArrays = foundArrays.concat(nestedArrays);
                }
            }
        }

        return foundArrays;
    }

    /**************************************************************/
    /**
     * Extracts a value (or array of values) from data using a path.
     *
     * @param {Object|Array} data - Data to extract from
     * @param {string} path - Path expression, optionally ending with [] for array extraction
     * @returns {*} Extracted value, array of unique values, or undefined
     *
     * @description
     * Supports array extraction with [] suffix:
     * - "documentGUID[]" extracts ALL matching values from array elements
     * - Automatically deduplicates extracted values (important for combination
     *   products where the same documentGUID appears multiple times in results)
     * - Handles nested arrays (e.g., productsByClass structure from pharmacologic
     *   class search where arrays are nested under dynamic keys)
     *
     * This enables batch operations where a subsequent endpoint needs
     * to be called once per extracted value.
     *
     * @example
     * // Simple top-level array
     * const data = [
     *     { documentGuid: 'abc' },
     *     { documentGuid: 'def' },
     *     { documentGuid: 'abc' }  // duplicate - will be removed
     * ];
     * extractValueByPath(data, 'documentGuid[]'); // ['abc', 'def'] (deduplicated)
     *
     * @example
     * // Nested array structure (pharmacologic class search)
     * const data = {
     *     productsByClass: {
     *         "GLP-1 Receptor Agonist [EPC]": [
     *             { documentGuid: 'abc', productName: 'Byetta' },
     *             { documentGuid: 'def', productName: 'Trulicity' }
     *         ]
     *     }
     * };
     * extractValueByPath(data, 'documentGuid[]'); // ['abc', 'def']
     *
     * @see expandEndpointForArrays - Uses array values for batch calls
     * @see findNestedArrays - Finds arrays in nested structures
     */
    /**************************************************************/
    function extractValueByPath(data, path) {
        if (!data || !path) {
            console.log('[EndpointExecutor] extractValueByPath: data or path is null/undefined');
            return undefined;
        }

        // Check for array extraction syntax (path ending with [])
        const isArrayExtraction = path.endsWith('[]');
        const cleanedPath = isArrayExtraction ? path.slice(0, -2) : path;

        console.log(`[EndpointExecutor] === EXTRACTING VALUE ===`);
        console.log(`[EndpointExecutor] Path: '${path}'${isArrayExtraction ? ' (ARRAY MODE - extracting ALL values)' : ''}`);
        console.log(`[EndpointExecutor] Data type: ${Array.isArray(data) ? 'array' : typeof data}`);

        // Array extraction mode: extract field from EACH array element
        // Automatically deduplicates values (important for combination products
        // where the same documentGUID appears multiple times)
        if (isArrayExtraction) {
            let arrayData = data;

            // If data is not a top-level array, search for nested arrays
            // This handles structures like { productsByClass: { "ClassName": [...] } }
            if (!Array.isArray(data)) {
                console.log(`[EndpointExecutor] Data is not a top-level array, searching for nested arrays...`);
                arrayData = findNestedArrays(data);
                if (arrayData.length === 0) {
                    console.log(`[EndpointExecutor] No nested arrays found`);
                    return undefined;
                }
                console.log(`[EndpointExecutor] Found ${arrayData.length} total elements in nested arrays`);
            }

            const seen = new Set();
            const values = [];
            for (let i = 0; i < arrayData.length; i++) {
                const value = extractSingleValue(arrayData[i], cleanedPath);
                if (value !== undefined && value !== null) {
                    // Deduplicate: only add if not already seen
                    const key = typeof value === 'object' ? JSON.stringify(value) : String(value);
                    if (!seen.has(key)) {
                        seen.add(key);
                        values.push(value);
                    }
                }
            }
            const duplicatesRemoved = arrayData.length - values.length;
            console.log(`[EndpointExecutor] === ARRAY EXTRACTION RESULT: ${values.length} unique values (${duplicatesRemoved} duplicates removed) ===`);
            console.log(`[EndpointExecutor] Values: [${values.slice(0, 5).join(', ')}${values.length > 5 ? '...' : ''}]`);
            return values.length > 0 ? values : undefined;
        }

        // Standard single value extraction
        return extractSingleValue(data, cleanedPath);
    }

    /**************************************************************/
    /**
     * Substitutes template variables in a string.
     *
     * @param {string} template - String with {{variableName}} placeholders
     * @param {Object} variables - Key-value pairs for substitution
     * @returns {string} String with substituted values
     *
     * @description
     * Replaces {{varName}} placeholders with values from the variables object.
     * Uses case-insensitive matching for variable names.
     * Unmatched placeholders are preserved (logged as warnings).
     *
     * @example
     * substituteVariables(
     *     '/api/Label/{{documentGuid}}/sections',
     *     { documentGuid: 'abc-123' }
     * );
     * // Returns: '/api/Label/abc-123/sections'
     *
     * @see substituteEndpointVariables - Applies to full endpoint spec
     */
    /**************************************************************/
    function substituteVariables(template, variables) {
        if (!template || typeof template !== 'string') return template;

        // Check for template variables
        const hasTemplates = /\{\{(\w+)\}\}/.test(template);
        if (!hasTemplates) return template;

        console.log(`[EndpointExecutor] === VARIABLE SUBSTITUTION ===`);
        console.log(`[EndpointExecutor] Template: '${template}'`);
        console.log(`[EndpointExecutor] Available variables: ${JSON.stringify(variables)}`);

        const result = template.replace(/\{\{(\w+)\}\}/g, (match, varName) => {
            // Try exact match first
            let value = variables[varName];

            // Try case-insensitive match
            if (value === undefined) {
                const lowerVarName = varName.toLowerCase();
                for (const key of Object.keys(variables)) {
                    if (key.toLowerCase() === lowerVarName) {
                        value = variables[key];
                        console.log(`[EndpointExecutor] Case-insensitive variable match: '${varName}' -> '${key}'`);
                        break;
                    }
                }
            }

            if (value !== undefined) {
                console.log(`[EndpointExecutor] Substituted {{${varName}}} -> '${value}'`);
                return value;
            } else {
                console.log(`[EndpointExecutor] WARNING: Variable '${varName}' not found! Keeping placeholder.`);
                return match;  // Keep original if not found
            }
        });

        if (result !== template) {
            console.log(`[EndpointExecutor] Final result: '${result}'`);
        }
        return result;
    }

    /**************************************************************/
    /**
     * Checks if an endpoint path contains an array variable.
     *
     * @param {string} path - Endpoint path with {{variable}} placeholders
     * @param {Object} variables - Extracted variables from previous steps
     * @returns {Object|null} { varName, values } if array found, null otherwise
     *
     * @description
     * Identifies when a template variable maps to an array, which means
     * the endpoint needs to be expanded into multiple calls.
     *
     * @example
     * findArrayVariable('/api/Label/{{documentGuid}}', { documentGuid: ['a', 'b'] });
     * // Returns: { varName: 'documentGuid', values: ['a', 'b'] }
     *
     * @see expandEndpointForArrays - Uses this to detect expansion needs
     */
    /**************************************************************/
    function findArrayVariable(path, variables) {
        const match = path.match(/\{\{(\w+)\}\}/);
        if (match) {
            const varName = match[1];
            const value = variables[varName] || variables[varName.toLowerCase()];
            if (Array.isArray(value) && value.length > 1) {
                console.log(`[EndpointExecutor] Found array variable '${varName}' with ${value.length} values`);
                return { varName, values: value };
            }
        }
        return null;
    }

    /**************************************************************/
    /**
     * Expands an endpoint into multiple endpoints for array variables.
     *
     * @param {Object} endpoint - Endpoint specification
     * @param {Object} variables - Extracted variables from previous steps
     * @returns {Array<Object>} Array of expanded endpoints (1 if no arrays, N if array)
     *
     * @description
     * When a template variable maps to an array, creates one endpoint
     * per array value. Each endpoint gets metadata about its position
     * (_expandedIndex, _expandedTotal, _expandedValue).
     *
     * @example
     * const endpoints = expandEndpointForArrays(
     *     { path: '/api/Label/{{documentGuid}}' },
     *     { documentGuid: ['abc', 'def'] }
     * );
     * // Returns 2 endpoints, one for each documentGuid
     *
     * @see executeEndpointsWithDependencies - Uses for batch operations
     */
    /**************************************************************/
    function expandEndpointForArrays(endpoint, variables) {
        const arrayVar = findArrayVariable(endpoint.path, variables);

        if (!arrayVar) {
            return [endpoint];  // No array expansion needed
        }

        console.log(`[EndpointExecutor] === EXPANDING ENDPOINT FOR ARRAY ===`);
        console.log(`[EndpointExecutor] Variable '${arrayVar.varName}' has ${arrayVar.values.length} values`);
        console.log(`[EndpointExecutor] Will generate ${arrayVar.values.length} API calls`);

        // Create one endpoint per array value
        return arrayVar.values.map((value, index) => {
            const expandedVars = { ...variables };
            expandedVars[arrayVar.varName] = value;

            const expandedEndpoint = {
                ...endpoint,
                _expandedIndex: index + 1,
                _expandedTotal: arrayVar.values.length,
                _expandedValue: value
            };

            return { endpoint: expandedEndpoint, variables: expandedVars };
        });
    }

    /**************************************************************/
    /**
     * Substitutes variables in an endpoint specification.
     *
     * @param {Object} endpoint - Endpoint specification
     * @param {Object} variables - Variables for substitution
     * @returns {Object} New endpoint with substituted values
     *
     * @description
     * Applies variable substitution to:
     * - endpoint.path
     * - endpoint.pathParameters
     * - endpoint.queryParameters
     *
     * @example
     * substituteEndpointVariables(
     *     { path: '/api/Label/{{documentGuid}}', queryParameters: { id: '{{id}}' } },
     *     { documentGuid: 'abc', id: '123' }
     * );
     *
     * @see substituteVariables - String-level substitution
     */
    /**************************************************************/
    function substituteEndpointVariables(endpoint, variables) {
        console.log(`[EndpointExecutor] Substituting variables for endpoint: ${endpoint.path}`);

        const substituted = { ...endpoint };

        // Substitute in path
        if (substituted.path) {
            substituted.path = substituteVariables(substituted.path, variables);
        }

        // Substitute in pathParameters
        if (substituted.pathParameters) {
            substituted.pathParameters = {};
            for (const [key, value] of Object.entries(endpoint.pathParameters)) {
                substituted.pathParameters[key] = substituteVariables(String(value), variables);
            }
        }

        // Substitute in queryParameters
        if (substituted.queryParameters) {
            substituted.queryParameters = {};
            for (const [key, value] of Object.entries(endpoint.queryParameters)) {
                if (typeof value === 'string') {
                    substituted.queryParameters[key] = substituteVariables(value, variables);
                } else {
                    substituted.queryParameters[key] = value;
                }
            }
        }

        return substituted;
    }

    /**************************************************************/
    /**
     * Auto-extracts common fields from API response data.
     *
     * @param {Object|Array} data - API response data
     * @param {Object} extractedVariables - Object to store extracted values
     *
     * @description
     * Uses deep search to find commonly needed fields regardless of nesting:
     * - documentGuid/documentGUID
     * - productName
     * - encryptedId/encryptedID
     * - encryptedDocumentID
     * - encryptedProductID
     * - setGuid/setGUID
     * - labelerName
     *
     * Normalizes field names to camelCase for consistent access.
     *
     * @example
     * const vars = {};
     * autoExtractCommonFields(apiResponse, vars);
     * console.log(vars.documentGuid); // Extracted value
     *
     * @see executeEndpointsWithDependencies - Calls when no explicit outputMapping
     */
    /**************************************************************/
    function autoExtractCommonFields(data, extractedVariables) {
        console.log('[EndpointExecutor] === AUTO-EXTRACTION (deep search) ===');

        // Common fields to auto-extract
        const fieldsToExtract = [
            'documentGuid', 'documentGUID',
            'productName',
            'encryptedId', 'encryptedID',
            'encryptedDocumentID',
            'encryptedProductID',
            'setGuid', 'setGUID',
            'labelerName'
        ];

        // Track extracted normalized names to avoid duplicates
        const extractedNormalized = new Set();

        for (const fieldName of fieldsToExtract) {
            // Normalize to camelCase
            const normalizedKey = fieldName
                .replace(/GUID$/i, 'Guid')
                .replace(/ID$/i, 'Id');
            const lowerNormalized = normalizedKey.toLowerCase();

            // Skip if already extracted
            if (extractedNormalized.has(lowerNormalized)) continue;

            // Deep search for property
            const value = findPropertyDeep(data, fieldName);

            if (value !== undefined && value !== null) {
                extractedVariables[normalizedKey] = value;
                extractedNormalized.add(lowerNormalized);
                console.log(`[EndpointExecutor] Auto-extracted ${normalizedKey}: '${value}'`);
            }
        }

        console.log(`[EndpointExecutor] Variables after auto-extraction: ${JSON.stringify(extractedVariables)}`);
    }

    /**************************************************************/
    /**
     * Checks if a result has meaningful data (not empty).
     *
     * @param {Object} result - API result object
     * @returns {boolean} True if result has data, false if empty
     *
     * @description
     * Used for:
     * - Determining if dependencies are satisfied
     * - Checking skipIfPreviousHasResults conditions
     *
     * @example
     * if (resultHasData(previousResult)) {
     *     // Skip fallback endpoint
     * }
     *
     * @see executeEndpointsWithDependencies - Uses for conditional logic
     */
    /**************************************************************/
    function resultHasData(result) {
        if (!result || result.statusCode < 200 || result.statusCode >= 300) {
            return false;
        }

        const data = result.result;

        if (data === null || data === undefined) {
            return false;
        }

        // Check for empty array
        if (Array.isArray(data)) {
            return data.length > 0;
        }

        // Check for empty object
        if (typeof data === 'object') {
            return Object.keys(data).length > 0;
        }

        // Primitive values count as "has data"
        return true;
    }

    /**************************************************************/
    /**
     * Executes endpoints with dependency support and conditional execution.
     *
     * @param {Array} endpoints - Array of endpoint specifications
     * @param {Object} assistantMessage - Message object for progress updates
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<Array>} Array of execution results
     *
     * @description
     * Core orchestration function that:
     *
     * 1. Groups endpoints by step number
     * 2. Executes steps in order
     * 3. Checks dependency satisfaction before each step
     * 4. Supports skipIfPreviousHasResults for fallback patterns
     * 5. Expands endpoints with array variables into multiple calls
     * 6. Extracts variables using outputMapping or auto-extraction
     * 7. Substitutes variables in subsequent endpoint templates
     *
     * Endpoint specification properties:
     * - step: Execution order (lower = earlier)
     * - dependsOn: Array of step numbers that must succeed first
     * - skipIfPreviousHasResults: Step number - skip if that step had data
     * - outputMapping: Map of varName -> jsonPath for extraction
     * - path, method, queryParameters, body: Standard endpoint properties
     *
     * @example
     * const endpoints = [
     *     { step: 1, path: '/api/search', outputMapping: { docId: '$.documentGuid' } },
     *     { step: 2, dependsOn: [1], path: '/api/Label/{{docId}}' }
     * ];
     * const results = await executeEndpointsWithDependencies(endpoints, msg, controller);
     *
     * @see substituteEndpointVariables - Variable substitution
     * @see expandEndpointForArrays - Array expansion
     * @see autoExtractCommonFields - Automatic variable extraction
     */
    /**************************************************************/
    async function executeEndpointsWithDependencies(endpoints, assistantMessage, abortController) {
        const results = [];
        const extractedVariables = {};

        console.log('[EndpointExecutor] ========================================');
        console.log('[EndpointExecutor] MULTI-STEP ENDPOINT EXECUTION STARTED');
        console.log(`[EndpointExecutor] Total endpoints: ${endpoints.length}`);
        console.log('[EndpointExecutor] ========================================');

        // Log endpoint details for debugging
        endpoints.forEach((ep, idx) => {
            console.log(`[EndpointExecutor] Endpoint ${idx + 1}: step=${ep.step}, path=${ep.path}, dependsOn=${ep.dependsOn}, skipIfPreviousHasResults=${ep.skipIfPreviousHasResults}, hasOutputMapping=${!!ep.outputMapping}`);
        });

        // Group endpoints by step
        const endpointsByStep = new Map();
        endpoints.forEach((ep, index) => {
            const step = ep.step || (ep.dependsOn ? Math.max(...endpoints.map(e => e.step || 1)) + 1 : 1);
            if (!endpointsByStep.has(step)) {
                endpointsByStep.set(step, []);
            }
            endpointsByStep.get(step).push({ ...ep, originalIndex: index });
        });

        // Sort steps for ordered execution
        const sortedSteps = Array.from(endpointsByStep.keys()).sort((a, b) => a - b);
        console.log(`[EndpointExecutor] Execution order: steps [${sortedSteps.join(', ')}]`);

        const totalEndpoints = endpoints.length;
        let completedCount = 0;

        // Execute each step in order
        for (const step of sortedSteps) {
            const stepEndpoints = endpointsByStep.get(step);
            console.log(`[EndpointExecutor] ======== EXECUTING STEP ${step} (${stepEndpoints.length} endpoint(s)) ========`);

            for (const endpoint of stepEndpoints) {
                // Check dependencies
                const dependencies = Array.isArray(endpoint.dependsOn)
                    ? endpoint.dependsOn
                    : (endpoint.dependsOn ? [endpoint.dependsOn] : []);

                if (dependencies.length > 0) {
                    console.log(`[EndpointExecutor] Step ${step} has ${dependencies.length} dependency/dependencies: [${dependencies.join(', ')}]`);

                    const dependencyStatus = dependencies.map(depStep => {
                        const depResults = results.filter(r =>
                            r.specification && r.specification.step === depStep
                        );

                        const succeeded = depResults.some(r =>
                            r.statusCode >= 200 && r.statusCode < 300 && r.result
                        );

                        console.log(`[EndpointExecutor]   - Dependency step ${depStep}: ${succeeded ? 'SUCCEEDED' : 'FAILED/MISSING'}`);
                        return { step: depStep, succeeded };
                    });

                    const failedDependencies = dependencyStatus.filter(d => !d.succeeded);

                    if (failedDependencies.length > 0) {
                        const failedSteps = failedDependencies.map(d => d.step).join(', ');
                        console.log(`[EndpointExecutor] Skipping step ${step} - dependency step(s) [${failedSteps}] failed or missing`);

                        results.push({
                            specification: endpoint,
                            statusCode: 0,
                            result: null,
                            error: `Skipped: dependency step(s) [${failedSteps}] failed`,
                            skipped: true,
                            step: step
                        });
                        completedCount++;
                        continue;
                    }

                    console.log(`[EndpointExecutor] All dependencies for step ${step} satisfied`);
                }

                // Check skipIfPreviousHasResults condition
                if (endpoint.skipIfPreviousHasResults) {
                    const checkStep = endpoint.skipIfPreviousHasResults;
                    console.log(`[EndpointExecutor] Checking skipIfPreviousHasResults: step ${checkStep}`);

                    const previousResult = results.find(r =>
                        r.specification && r.specification.step === checkStep
                    );

                    if (previousResult && resultHasData(previousResult)) {
                        console.log(`[EndpointExecutor] Skipping step ${step} - step ${checkStep} returned data (rescue not needed)`);

                        results.push({
                            specification: endpoint,
                            statusCode: 0,
                            result: null,
                            error: `Skipped: step ${checkStep} had results (fallback not needed)`,
                            skipped: true,
                            skippedReason: 'previous_has_results',
                            step: step
                        });
                        completedCount++;
                        continue;
                    } else {
                        console.log(`[EndpointExecutor] Step ${checkStep} was empty - proceeding with fallback step ${step}`);
                    }
                }

                console.log(`[EndpointExecutor] Current extractedVariables: ${JSON.stringify(extractedVariables)}`);

                // Check for array expansion
                const expandedEndpoints = expandEndpointForArrays(endpoint, extractedVariables);
                const isArrayExpansion = expandedEndpoints.length > 1 ||
                    (expandedEndpoints.length === 1 && expandedEndpoints[0].variables);

                if (isArrayExpansion) {
                    console.log(`[EndpointExecutor] === MULTI-DOCUMENT EXPANSION: ${expandedEndpoints.length} calls ===`);
                }

                // Execute each expanded endpoint
                for (let expandIdx = 0; expandIdx < expandedEndpoints.length; expandIdx++) {
                    const expandedItem = expandedEndpoints[expandIdx];
                    const currentEndpoint = expandedItem.endpoint || expandedItem;
                    const currentVars = expandedItem.variables || extractedVariables;

                    // Substitute variables
                    const processedEndpoint = substituteEndpointVariables(currentEndpoint, currentVars);

                    // Update progress
                    const expandInfo = isArrayExpansion ? ` [${expandIdx + 1}/${expandedEndpoints.length}]` : '';
                    assistantMessage.progress = completedCount / totalEndpoints;
                    assistantMessage.progressStatus = `Step ${step}${expandInfo}: ${processedEndpoint.description || 'Executing query'}...`;
                    MessageRenderer.updateMessage(assistantMessage.id);

                    try {
                        const startTime = Date.now();
                        const apiUrl = ApiService.buildApiUrl(processedEndpoint);
                        const fullApiUrl = ChatConfig.buildUrl(apiUrl);
                        console.log(`[EndpointExecutor] Step ${step}${expandInfo}: Executing API call: ${fullApiUrl}`);

                        const apiResponse = await fetch(fullApiUrl, ChatConfig.getFetchOptions({
                            method: processedEndpoint.method || 'GET',
                            headers: processedEndpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
                            body: processedEndpoint.method === 'POST' ? JSON.stringify(processedEndpoint.body) : undefined,
                            signal: abortController.signal
                        }));

                        if (apiResponse.ok) {
                            const data = await apiResponse.json();
                            const hasData = resultHasData({ result: data, statusCode: apiResponse.status });
                            console.log(`[EndpointExecutor] Step ${step}${expandInfo} succeeded: ${processedEndpoint.path} (hasData: ${hasData})`);

                            // Extract output mappings (only on non-expanded or first expansion)
                            if (!isArrayExpansion && endpoint.outputMapping) {
                                console.log(`[EndpointExecutor] Processing outputMapping: ${JSON.stringify(endpoint.outputMapping)}`);
                                for (const [varName, jsonPath] of Object.entries(endpoint.outputMapping)) {
                                    let extractedValue = extractValueByPath(data, jsonPath);

                                    // Fall back to deep search
                                    if (extractedValue === undefined) {
                                        console.log(`[EndpointExecutor] Path extraction failed, trying deep search for '${varName}'...`);
                                        extractedValue = findPropertyDeep(data, varName);
                                    }

                                    if (extractedValue !== undefined) {
                                        extractedVariables[varName] = extractedValue;
                                        console.log(`[EndpointExecutor] Stored variable '${varName}' = '${extractedValue}'`);
                                    } else {
                                        console.log(`[EndpointExecutor] Failed to extract '${varName}' - not found by path or deep search`);
                                    }
                                }
                            } else if (!isArrayExpansion) {
                                // Auto-extract if no explicit mapping
                                autoExtractCommonFields(data, extractedVariables);
                            }

                            results.push({
                                specification: processedEndpoint,
                                statusCode: apiResponse.status,
                                result: data,
                                executionTimeMs: Date.now() - startTime,
                                step: step,
                                hasData: hasData,
                                _expandedIndex: currentEndpoint._expandedIndex,
                                _expandedTotal: currentEndpoint._expandedTotal
                            });
                        } else {
                            console.log(`[EndpointExecutor] Step ${step}${expandInfo} failed: ${processedEndpoint.path} - HTTP ${apiResponse.status}`);

                            // Check for fallbackOnError configuration
                            const fallback = endpoint.fallbackOnError || currentEndpoint.fallbackOnError;
                            const shouldRetry = fallback &&
                                fallback.httpStatus &&
                                fallback.httpStatus.includes(apiResponse.status) &&
                                fallback.action === 'retry_without_param' &&
                                fallback.removeParams;

                            if (shouldRetry) {
                                console.log(`[EndpointExecutor] === FALLBACK RETRY: Removing params [${fallback.removeParams.join(', ')}] ===`);

                                // Create a new endpoint without the specified params
                                const fallbackEndpoint = { ...processedEndpoint };
                                if (fallbackEndpoint.queryParameters) {
                                    fallbackEndpoint.queryParameters = { ...fallbackEndpoint.queryParameters };
                                    for (const param of fallback.removeParams) {
                                        delete fallbackEndpoint.queryParameters[param];
                                    }
                                }

                                // Build and execute the fallback URL
                                const fallbackApiUrl = ApiService.buildApiUrl(fallbackEndpoint);
                                const fallbackFullUrl = ChatConfig.buildUrl(fallbackApiUrl);
                                console.log(`[EndpointExecutor] Step ${step}${expandInfo} FALLBACK: ${fallbackFullUrl}`);

                                const fallbackResponse = await fetch(fallbackFullUrl, ChatConfig.getFetchOptions({
                                    method: fallbackEndpoint.method || 'GET',
                                    headers: fallbackEndpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
                                    body: fallbackEndpoint.method === 'POST' ? JSON.stringify(fallbackEndpoint.body) : undefined,
                                    signal: abortController.signal
                                }));

                                if (fallbackResponse.ok) {
                                    const fallbackData = await fallbackResponse.json();
                                    const fallbackHasData = resultHasData({ result: fallbackData, statusCode: fallbackResponse.status });
                                    console.log(`[EndpointExecutor] Step ${step}${expandInfo} FALLBACK succeeded (hasData: ${fallbackHasData})`);

                                    results.push({
                                        specification: fallbackEndpoint,
                                        statusCode: fallbackResponse.status,
                                        result: fallbackData,
                                        executionTimeMs: Date.now() - startTime,
                                        step: step,
                                        hasData: fallbackHasData,
                                        _expandedIndex: currentEndpoint._expandedIndex,
                                        _expandedTotal: currentEndpoint._expandedTotal,
                                        _fallbackUsed: true
                                    });
                                } else {
                                    console.log(`[EndpointExecutor] Step ${step}${expandInfo} FALLBACK also failed: HTTP ${fallbackResponse.status}`);
                                    results.push({
                                        specification: fallbackEndpoint,
                                        statusCode: fallbackResponse.status,
                                        result: null,
                                        error: `HTTP ${fallbackResponse.status} (fallback also failed)`,
                                        executionTimeMs: Date.now() - startTime,
                                        step: step,
                                        hasData: false,
                                        _fallbackUsed: true
                                    });
                                }
                            } else {
                                // No fallback configured or conditions not met
                                results.push({
                                    specification: processedEndpoint,
                                    statusCode: apiResponse.status,
                                    result: null,
                                    error: `HTTP ${apiResponse.status}`,
                                    executionTimeMs: Date.now() - startTime,
                                    step: step,
                                    hasData: false
                                });
                            }
                        }
                    } catch (endpointError) {
                        console.log(`[EndpointExecutor] Step ${step}${expandInfo} exception: ${endpointError.message}`);
                        results.push({
                            specification: processedEndpoint,
                            statusCode: 500,
                            error: endpointError.message,
                            step: step,
                            hasData: false
                        });
                    }
                }

                completedCount++;
            }
        }

        console.log('[EndpointExecutor] ========================================');
        console.log('[EndpointExecutor] MULTI-STEP EXECUTION COMPLETE');
        console.log(`[EndpointExecutor] Total results: ${results.length}`);
        console.log(`[EndpointExecutor] Final extractedVariables: ${JSON.stringify(extractedVariables)}`);
        console.log('[EndpointExecutor] ========================================');

        return results;
    }

    /**************************************************************/
    /**
     * Public API for the endpoint execution module.
     *
     * @description
     * Exposes the main execution function and utility functions.
     */
    /**************************************************************/
    return {
        // Main execution
        executeEndpointsWithDependencies: executeEndpointsWithDependencies,

        // Utility functions (exposed for testing/advanced use)
        extractValueByPath: extractValueByPath,
        findPropertyDeep: findPropertyDeep,
        substituteVariables: substituteVariables,
        resultHasData: resultHasData
    };
})();
