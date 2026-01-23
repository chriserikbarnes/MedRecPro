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
 * - Fallback retry logic for 404 responses
 * - UNII resolution fallback for incorrect/hallucinated UNIIs
 * - Dynamic label content fetching for UNII fallback discoveries
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
     * Groups endpoints by their step number for ordered execution.
     *
     * @param {Array} endpoints - Array of endpoint specifications
     * @returns {Map<number, Array>} Map of step number to endpoints array
     *
     * @description
     * Organizes endpoints into execution groups. Endpoints without
     * a step number are assigned based on their dependencies.
     *
     * @example
     * const grouped = groupEndpointsByStep([
     *     { step: 1, path: '/api/search' },
     *     { step: 2, path: '/api/details' }
     * ]);
     * // Returns: Map { 1 => [...], 2 => [...] }
     *
     * @see executeEndpointsWithDependencies - Uses for step ordering
     */
    /**************************************************************/
    function groupEndpointsByStep(endpoints) {
        const endpointsByStep = new Map();

        endpoints.forEach((ep, index) => {
            // Assign step based on dependencies if not specified
            const step = ep.step || (ep.dependsOn ? Math.max(...endpoints.map(e => e.step || 1)) + 1 : 1);

            if (!endpointsByStep.has(step)) {
                endpointsByStep.set(step, []);
            }
            endpointsByStep.get(step).push({ ...ep, originalIndex: index });
        });

        return endpointsByStep;
    }

    /**************************************************************/
    /**
     * Checks if an endpoint's dependencies are satisfied.
     *
     * @param {Object} endpoint - Endpoint specification with dependsOn property
     * @param {Array} results - Results from previously executed endpoints
     * @param {number} step - Current step number for logging
     * @returns {Object} { satisfied: boolean, failedSteps: Array }
     *
     * @description
     * Verifies that all steps listed in dependsOn have completed
     * successfully (status 200-299 with data).
     *
     * @example
     * const depCheck = checkDependencies(
     *     { dependsOn: [1, 2] },
     *     previousResults,
     *     3
     * );
     * if (!depCheck.satisfied) {
     *     // Skip this endpoint
     * }
     *
     * @see executeEndpointsWithDependencies - Uses for dependency validation
     */
    /**************************************************************/
    function checkDependencies(endpoint, results, step) {
        const dependencies = Array.isArray(endpoint.dependsOn)
            ? endpoint.dependsOn
            : (endpoint.dependsOn ? [endpoint.dependsOn] : []);

        if (dependencies.length === 0) {
            return { satisfied: true, failedSteps: [] };
        }

        console.log(`[EndpointExecutor] Step ${step} has ${dependencies.length} dependency/dependencies: [${dependencies.join(', ')}]`);

        const failedSteps = [];

        for (const depStep of dependencies) {
            const depResults = results.filter(r =>
                r.specification && r.specification.step === depStep
            );

            const succeeded = depResults.some(r =>
                r.statusCode >= 200 && r.statusCode < 300 && r.result
            );

            console.log(`[EndpointExecutor]   - Dependency step ${depStep}: ${succeeded ? 'SUCCEEDED' : 'FAILED/MISSING'}`);

            if (!succeeded) {
                failedSteps.push(depStep);
            }
        }

        return {
            satisfied: failedSteps.length === 0,
            failedSteps
        };
    }

    /**************************************************************/
    /**
     * Checks if an endpoint should be skipped due to skipIfPreviousHasResults.
     *
     * @param {Object} endpoint - Endpoint specification
     * @param {Array} results - Results from previously executed endpoints
     * @param {number} step - Current step number for logging
     * @returns {Object} { shouldSkip: boolean, reason: string|null }
     *
     * @description
     * Implements the fallback pattern where a step is skipped if
     * a previous step already returned data (rescue not needed).
     *
     * @example
     * const skipCheck = checkSkipCondition(
     *     { skipIfPreviousHasResults: 1 },
     *     previousResults,
     *     2
     * );
     * if (skipCheck.shouldSkip) {
     *     // Skip this fallback endpoint
     * }
     *
     * @see executeEndpointsWithDependencies - Uses for conditional execution
     */
    /**************************************************************/
    function checkSkipCondition(endpoint, results, step) {
        if (!endpoint.skipIfPreviousHasResults) {
            return { shouldSkip: false, reason: null };
        }

        const checkStep = endpoint.skipIfPreviousHasResults;
        console.log(`[EndpointExecutor] Checking skipIfPreviousHasResults: step ${checkStep}`);

        const previousResult = results.find(r =>
            r.specification && r.specification.step === checkStep
        );

        if (previousResult && resultHasData(previousResult)) {
            console.log(`[EndpointExecutor] Skipping step ${step} - step ${checkStep} returned data (rescue not needed)`);
            return {
                shouldSkip: true,
                reason: `step ${checkStep} had results (fallback not needed)`
            };
        }

        console.log(`[EndpointExecutor] Step ${checkStep} was empty - proceeding with fallback step ${step}`);
        return { shouldSkip: false, reason: null };
    }

    /**************************************************************/
    /**
     * Processes output mappings to extract variables from API response.
     *
     * @param {Object} data - API response data
     * @param {Object} outputMapping - Map of varName -> jsonPath
     * @param {Object} extractedVariables - Object to store extracted values
     *
     * @description
     * Extracts values using the paths specified in outputMapping.
     * Falls back to deep search if path extraction fails.
     *
     * @example
     * processOutputMappings(
     *     apiResponse,
     *     { documentGuid: 'DocumentGUID[]' },
     *     extractedVars
     * );
     *
     * @see extractValueByPath - Performs the actual extraction
     * @see findPropertyDeep - Fallback extraction method
     */
    /**************************************************************/
    function processOutputMappings(data, outputMapping, extractedVariables) {
        console.log(`[EndpointExecutor] Processing outputMapping: ${JSON.stringify(outputMapping)}`);

        for (const [varName, jsonPath] of Object.entries(outputMapping)) {
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
    }

    /**************************************************************/
    /**
     * Executes a single API call and returns the result.
     *
     * @param {Object} processedEndpoint - Endpoint with substituted variables
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<Object>} { response: Response, data: Object|null, error: string|null }
     *
     * @description
     * Performs the HTTP fetch and parses JSON response.
     * Handles both success and error cases.
     *
     * @example
     * const { response, data, error } = await executeApiCall(endpoint, controller);
     * if (response.ok) {
     *     // Process data
     * }
     *
     * @see executeEndpointsWithDependencies - Calls for each endpoint
     */
    /**************************************************************/
    async function executeApiCall(processedEndpoint, abortController) {
        const apiUrl = ApiService.buildApiUrl(processedEndpoint);
        const fullApiUrl = ChatConfig.buildUrl(apiUrl);

        const response = await fetch(fullApiUrl, ChatConfig.getFetchOptions({
            method: processedEndpoint.method || 'GET',
            headers: processedEndpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
            body: processedEndpoint.method === 'POST' ? JSON.stringify(processedEndpoint.body) : undefined,
            signal: abortController.signal
        }));

        let data = null;
        let error = null;

        if (response.ok) {
            data = await response.json();
        } else {
            error = `HTTP ${response.status}`;
        }

        return { response, data, error, fullApiUrl };
    }

    /**************************************************************/
    /**
     * Executes a fallback retry when the primary request fails with a configured status.
     *
     * @param {Object} processedEndpoint - Original endpoint that failed
     * @param {Object} fallback - Fallback configuration from endpoint.fallbackOnError
     * @param {AbortController} abortController - For request cancellation
     * @param {number} step - Current step number for logging
     * @param {string} expandInfo - Expansion info string for logging (e.g., "[1/5]")
     * @returns {Promise<Object>} Result object for the results array
     *
     * @description
     * When an endpoint has fallbackOnError configured and the response matches
     * the configured httpStatus codes, this function:
     * 1. Creates a modified endpoint with specified parameters removed
     * 2. Executes the fallback request
     * 3. Returns the result with _fallbackUsed flag
     *
     * @example
     * // Endpoint config:
     * {
     *     fallbackOnError: {
     *         httpStatus: [404],
     *         action: 'retry_without_param',
     *         removeParams: ['sectionCode']
     *     }
     * }
     *
     * @see executeEndpointsWithDependencies - Calls when primary request fails
     */
    /**************************************************************/
    async function executeFallbackRetry(processedEndpoint, fallback, abortController, step, expandInfo, startTime, currentEndpoint) {
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

            return {
                specification: fallbackEndpoint,
                statusCode: fallbackResponse.status,
                result: fallbackData,
                executionTimeMs: Date.now() - startTime,
                step: step,
                hasData: fallbackHasData,
                _expandedIndex: currentEndpoint._expandedIndex,
                _expandedTotal: currentEndpoint._expandedTotal,
                _fallbackUsed: true,
                _apiUrl: fallbackFullUrl  // Include URL for synthesis data references
            };
        } else {
            console.log(`[EndpointExecutor] Step ${step}${expandInfo} FALLBACK also failed: HTTP ${fallbackResponse.status}`);
            return {
                specification: fallbackEndpoint,
                statusCode: fallbackResponse.status,
                result: null,
                error: `HTTP ${fallbackResponse.status} (fallback also failed)`,
                executionTimeMs: Date.now() - startTime,
                step: step,
                hasData: false,
                _fallbackUsed: true,
                _apiUrl: fallbackFullUrl  // Include URL even on failure for debugging
            };
        }
    }

    /**************************************************************/
    /**
     * Simple local fallback for extracting product names from descriptions.
     *
     * @param {string} description - Endpoint description containing product info
     * @returns {string|null} Extracted product name or null if not found
     *
     * @description
     * This is a simplified fallback used when the server-side AI extraction
     * API (/api/Label/extract-product) is unavailable. The server-side API
     * handles the complex extraction logic including:
     * - Brand to generic name resolution
     * - Multi-word ingredient names
     * - Drug class vs drug name disambiguation
     *
     * This local fallback only handles simple patterns:
     * - "Search for X - description" → extracts X
     * - "Get X products" → extracts X
     * - First word that looks like a drug name (4+ letters, starts with letter)
     *
     * @example
     * extractProductNameFromDescription("Search for sevelamer - phosphate binder");
     * // Returns: "sevelamer"
     *
     * @see extractProductNameFromDescriptionAsync - Primary extraction using server API
     * @see executeUniiResolutionFallback - Uses this as last resort fallback
     */
    /**************************************************************/
    function extractProductNameFromDescription(description) {
        if (!description || typeof description !== 'string') {
            return null;
        }

        // Pattern 1: "Search for X - description"
        const searchForPattern = /search\s+for\s+([a-zA-Z][a-zA-Z0-9\-]*)/i;
        const match1 = description.match(searchForPattern);
        if (match1) {
            const extracted = match1[1].trim().toLowerCase();
            console.log(`[EndpointExecutor] Local fallback extracted: '${extracted}'`);
            return extracted;
        }

        // Pattern 2: "Get X products" or "Retrieve X"
        const getPattern = /(?:get|retrieve|find|lookup)\s+([a-zA-Z][a-zA-Z0-9\-]*)/i;
        const match2 = description.match(getPattern);
        if (match2) {
            const extracted = match2[1].trim().toLowerCase();
            console.log(`[EndpointExecutor] Local fallback extracted: '${extracted}'`);
            return extracted;
        }

        // Pattern 3: First word that looks like a drug name (simple fallback)
        const skipWords = new Set([
            'search', 'get', 'find', 'retrieve', 'lookup', 'fetch', 'query',
            'for', 'the', 'a', 'an', 'to', 'of', 'in', 'on', 'with', 'from', 'by',
            'products', 'product', 'medications', 'medication', 'drugs', 'drug',
            'label', 'labels', 'information', 'details', 'data'
        ]);

        const words = description.split(/[\s\-\(\),;:]+/);
        for (const word of words) {
            const lowerWord = word.toLowerCase();
            if (word.length >= 4 && !skipWords.has(lowerWord) && /^[a-zA-Z]/.test(word)) {
                console.log(`[EndpointExecutor] Local fallback extracted (first word): '${lowerWord}'`);
                return lowerWord;
            }
        }

        return null;
    }

    /**************************************************************/
    /**
     * Checks if an endpoint is a product/latest query with UNII parameter.
     *
     * @param {Object} processedEndpoint - Endpoint to check
     * @returns {boolean} True if endpoint is a product/latest UNII query
     *
     * @description
     * Determines if the endpoint matches the pattern that can benefit
     * from UNII resolution fallback when results are empty.
     *
     * @see executeUniiResolutionFallback - Uses this for validation
     */
    /**************************************************************/
    function isProductLatestUniiQuery(processedEndpoint) {
        const path = processedEndpoint.path || '';
        const queryParams = processedEndpoint.queryParameters || {};
        return path.includes('/api/Label/product/latest') && queryParams.unii;
    }

    /**************************************************************/
    /**
     * Extracts a product name from a description using the server-side AI API.
     *
     * @param {string} description - Endpoint description containing product info
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<string|null>} Extracted product name or null if not found
     *
     * @description
     * Calls the /api/Label/extract-product endpoint which uses Claude AI to
     * intelligently extract drug/ingredient names from natural language descriptions.
     * This handles edge cases that pattern matching cannot, such as:
     * - Varied description formats
     * - Brand to generic name resolution
     * - Multi-word ingredient names
     * - Drug class vs drug name disambiguation
     *
     * Falls back to local extractProductNameFromDescription() if the API fails.
     *
     * @example
     * const productName = await extractProductNameFromDescriptionAsync(
     *     "Search for finerenone (Kerendia) - non-steroidal MRA for CKD",
     *     abortController
     * );
     * // Returns: "finerenone"
     *
     * @see extractProductNameFromDescription - Local fallback function
     * @see executeUniiResolutionFallback - Primary consumer of this function
     */
    /**************************************************************/
    async function extractProductNameFromDescriptionAsync(description, abortController) {
        if (!description || typeof description !== 'string') {
            return null;
        }

        console.log(`[EndpointExecutor] Calling AI extraction for description: '${description.substring(0, 80)}...'`);

        try {
            // Build the API endpoint
            const extractEndpoint = {
                method: 'GET',
                path: '/api/Label/extract-product',
                queryParameters: {
                    description: description
                }
            };

            const apiUrl = ApiService.buildApiUrl(extractEndpoint);
            const fullUrl = ChatConfig.buildUrl(apiUrl);
            console.log(`[EndpointExecutor] Extract product API: ${fullUrl}`);

            const response = await fetch(fullUrl, ChatConfig.getFetchOptions({
                method: 'GET',
                signal: abortController.signal
            }));

            if (!response.ok) {
                console.log(`[EndpointExecutor] Extract product API failed HTTP ${response.status}, using local fallback`);
                return extractProductNameFromDescription(description);
            }

            const result = await response.json();

            // Check if extraction was successful
            if (result.success && result.primaryProductName) {
                console.log(`[EndpointExecutor] AI extracted product: '${result.primaryProductName}' (confidence: ${result.confidence})`);

                // Log brand mapping if applied
                if (result.brandMappingApplied) {
                    console.log(`[EndpointExecutor] Brand mapping: '${result.originalBrandName}' -> '${result.primaryProductName}'`);
                }

                return result.primaryProductName;
            }

            // AI extraction didn't find a product, try local fallback
            console.log(`[EndpointExecutor] AI extraction returned no product: ${result.error || result.explanation}`);
            return extractProductNameFromDescription(description);

        } catch (error) {
            // Network error or other issue, use local fallback
            console.log(`[EndpointExecutor] Extract product API error: ${error.message}, using local fallback`);
            return extractProductNameFromDescription(description);
        }
    }

    /**************************************************************/
    /**
     * Searches for the correct UNII using ingredient advanced search.
     *
     * @param {string} productName - Product name to search for
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<string|null>} Correct UNII or null if not found
     *
     * @description
     * Calls /api/Label/ingredient/advanced to find the correct UNII
     * for a given product name. Extracts UNII from the first result.
     *
     * @see executeUniiResolutionFallback - Uses this function
     */
    /**************************************************************/
    async function searchForCorrectUnii(productName, abortController) {
        const ingredientSearchEndpoint = {
            method: 'GET',
            path: '/api/Label/ingredient/advanced',
            queryParameters: {
                substanceNameSearch: productName,
                pageNumber: '1',
                pageSize: '5'
            }
        };

        const ingredientApiUrl = ApiService.buildApiUrl(ingredientSearchEndpoint);
        const ingredientFullUrl = ChatConfig.buildUrl(ingredientApiUrl);
        console.log(`[EndpointExecutor] Ingredient search: ${ingredientFullUrl}`);

        const ingredientResponse = await fetch(ingredientFullUrl, ChatConfig.getFetchOptions({
            method: 'GET',
            signal: abortController.signal
        }));

        if (!ingredientResponse.ok) {
            console.log(`[EndpointExecutor] UNII search: ingredient search failed HTTP ${ingredientResponse.status}`);
            return null;
        }

        const ingredientData = await ingredientResponse.json();

        // Extract UNII from the ingredient search results
        // Response format: [{ ingredientView: { unii: "...", productName: "...", ... } }]
        if (Array.isArray(ingredientData) && ingredientData.length > 0) {
            const unii = findPropertyDeep(ingredientData[0], 'unii');
            if (unii) {
                console.log(`[EndpointExecutor] UNII search: Found UNII '${unii}' for '${productName}'`);
                return unii;
            }
        }

        console.log(`[EndpointExecutor] UNII search: No UNII found in ingredient search results for '${productName}'`);
        return null;
    }

    /**************************************************************/
    /**
     * Retries product/latest endpoint with a corrected UNII.
     *
     * @param {Object} originalEndpoint - Original endpoint specification
     * @param {string} correctUnii - Corrected UNII to use
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<Object|null>} Response data if successful, null otherwise
     *
     * @description
     * Creates a new endpoint with the corrected UNII and executes the request.
     * Returns an object with { response, data, fullUrl } on success.
     *
     * @see executeUniiResolutionFallback - Uses this function
     */
    /**************************************************************/
    async function retryProductLatestWithUnii(originalEndpoint, correctUnii, abortController) {
        const correctedEndpoint = {
            ...originalEndpoint,
            queryParameters: {
                ...originalEndpoint.queryParameters,
                unii: correctUnii
            }
        };

        const correctedApiUrl = ApiService.buildApiUrl(correctedEndpoint);
        const correctedFullUrl = ChatConfig.buildUrl(correctedApiUrl);
        console.log(`[EndpointExecutor] Corrected product search: ${correctedFullUrl}`);

        const correctedResponse = await fetch(correctedFullUrl, ChatConfig.getFetchOptions({
            method: 'GET',
            signal: abortController.signal
        }));

        if (correctedResponse.ok) {
            const correctedData = await correctedResponse.json();
            return {
                response: correctedResponse,
                data: correctedData,
                fullUrl: correctedFullUrl,
                endpoint: correctedEndpoint
            };
        }

        return null;
    }

    /**************************************************************/
    /**
     * Builds a successful UNII fallback result object.
     *
     * @param {Object} params - Result parameters
     * @param {Object} params.correctedEndpoint - Endpoint with corrected UNII
     * @param {number} params.statusCode - HTTP status code
     * @param {Object} params.data - Response data
     * @param {number} params.startTime - Start timestamp for timing
     * @param {number} params.step - Current step number
     * @param {Object} params.currentEndpoint - Current endpoint for metadata
     * @param {string} params.originalUnii - Original (incorrect) UNII
     * @param {string} params.correctedUnii - Corrected UNII
     * @param {string} params.fullUrl - Full API URL used
     * @returns {Object} Formatted result object
     *
     * @description
     * Creates a standardized result object for successful UNII fallback.
     * Includes metadata about the fallback for debugging and tracing.
     *
     * @see executeUniiResolutionFallback - Uses this function
     */
    /**************************************************************/
    function buildUniiFallbackResult(params) {
        return {
            specification: params.correctedEndpoint,
            statusCode: params.statusCode,
            result: params.data,
            executionTimeMs: Date.now() - params.startTime,
            step: params.step,
            hasData: true,
            _expandedIndex: params.currentEndpoint._expandedIndex,
            _expandedTotal: params.currentEndpoint._expandedTotal,
            _uniiFallbackUsed: true,
            _originalUnii: params.originalUnii,
            _correctedUnii: params.correctedUnii,
            _apiUrl: params.fullUrl
        };
    }

    /**************************************************************/
    /**
     * Executes UNII resolution fallback when product/latest returns empty.
     *
     * @param {Object} processedEndpoint - Original endpoint that returned empty
     * @param {Object} endpoint - Original endpoint specification with description
     * @param {AbortController} abortController - For request cancellation
     * @param {number} step - Current step number for logging
     * @param {string} expandInfo - Expansion info string for logging
     * @param {number} startTime - Start timestamp for timing
     * @param {Object} currentEndpoint - Current endpoint for metadata
     * @param {Object} extractedVariables - Variables to update with corrected data
     * @returns {Promise<Object|null>} Result object if fallback succeeds, null otherwise
     *
     * @description
     * When /api/Label/product/latest?unii=X returns 200 but empty results,
     * this function attempts to resolve the correct UNII:
     *
     * 1. **Validate**: Checks if endpoint is a product/latest UNII query
     * 2. **Extract**: Gets product name from endpoint description
     * 3. **Search**: Calls ingredient/advanced to find correct UNII
     * 4. **Retry**: Re-executes product/latest with corrected UNII
     * 5. **Return**: Returns result if successful, null otherwise
     *
     * This handles cases where the interpret phase used incorrect/hallucinated
     * UNIIs from reference data but the product name in the description is correct.
     *
     * @example
     * // Endpoint description: "Search for sevelamer - phosphate binder"
     * // Original UNII UF86BE3WDJ returns empty
     * // Fallback: ingredient/advanced?substanceNameSearch=sevelamer
     * // Gets correct UNII: 9YCX42I8IU
     * // Retries: product/latest?unii=9YCX42I8IU
     *
     * @see processExpandedEndpoint - Calls this when product/latest is empty
     * @see extractProductNameFromDescriptionAsync - AI-powered product extraction (primary)
     * @see extractProductNameFromDescription - Local pattern matching (fallback)
     * @see searchForCorrectUnii - Finds correct UNII
     * @see retryProductLatestWithUnii - Retries with corrected UNII
     */
    /**************************************************************/
    async function executeUniiResolutionFallback(processedEndpoint, endpoint, abortController, step, expandInfo, startTime, currentEndpoint, extractedVariables) {
        // Step 1: Validate this is a product/latest UNII query
        if (!isProductLatestUniiQuery(processedEndpoint)) {
            return null;
        }

        const queryParams = processedEndpoint.queryParameters || {};
        const originalUnii = queryParams.unii;

        // Step 2: Extract product name from description using AI API (with local fallback)
        const description = processedEndpoint.description || endpoint.description || currentEndpoint.description || '';

        // Use async AI extraction with local pattern matching as fallback
        const productName = await extractProductNameFromDescriptionAsync(description, abortController);

        if (!productName) {
            console.log(`[EndpointExecutor] UNII fallback: Could not extract product name from description: '${description}'`);
            return null;
        }

        console.log(`[EndpointExecutor] === UNII RESOLUTION FALLBACK ===`);
        console.log(`[EndpointExecutor] Original UNII '${originalUnii}' returned empty`);
        console.log(`[EndpointExecutor] Extracted product name: '${productName}'`);

        try {
            // Step 3: Search for correct UNII
            console.log(`[EndpointExecutor] Step 1: Searching ingredient/advanced for correct UNII...`);
            const correctUnii = await searchForCorrectUnii(productName, abortController);

            if (!correctUnii) {
                console.log(`[EndpointExecutor] UNII fallback: No UNII found for product '${productName}'`);
                return null;
            }

            console.log(`[EndpointExecutor] Step 2: Found correct UNII: '${correctUnii}'`);

            // Step 4: Retry product/latest with corrected UNII
            console.log(`[EndpointExecutor] Step 3: Retrying product/latest with correct UNII...`);
            const retryResult = await retryProductLatestWithUnii(processedEndpoint, correctUnii, abortController);

            if (!retryResult) {
                console.log(`[EndpointExecutor] UNII fallback: Retry request failed`);
                return null;
            }

            const hasData = resultHasData({ result: retryResult.data, statusCode: retryResult.response.status });

            if (hasData) {
                console.log(`[EndpointExecutor] === UNII RESOLUTION SUCCESS ===`);
                console.log(`[EndpointExecutor] Corrected UNII '${correctUnii}' returned ${Array.isArray(retryResult.data) ? retryResult.data.length : 1} result(s)`);

                // Update extracted variables with the corrected UNII for downstream steps
                extractedVariables._correctedUnii = correctUnii;
                extractedVariables._originalUnii = originalUnii;

                return buildUniiFallbackResult({
                    correctedEndpoint: retryResult.endpoint,
                    statusCode: retryResult.response.status,
                    data: retryResult.data,
                    startTime,
                    step,
                    currentEndpoint,
                    originalUnii,
                    correctedUnii: correctUnii,
                    fullUrl: retryResult.fullUrl
                });
            }

            console.log(`[EndpointExecutor] UNII fallback: Corrected UNII also returned empty/failed`);
            return null;

        } catch (error) {
            console.log(`[EndpointExecutor] UNII fallback exception: ${error.message}`);
            return null;
        }
    }

    /**************************************************************/
    /**
     * Determines if a fallback retry should be attempted.
     *
     * @param {Object} endpoint - Original endpoint specification
     * @param {Object} currentEndpoint - Current (possibly expanded) endpoint
     * @param {number} statusCode - HTTP status code from failed request
     * @returns {Object|null} Fallback configuration if retry should occur, null otherwise
     *
     * @description
     * Checks if the endpoint has a fallbackOnError configuration that matches
     * the current failure conditions.
     *
     * Also applies AUTOMATIC fallback for known section retrieval endpoints:
     * - /api/Label/markdown/sections/* with sectionCode parameter → retry without sectionCode
     *
     * This ensures fallback always occurs for section queries even if the AI
     * interpret phase doesn't explicitly include fallbackOnError configuration.
     *
     * @example
     * const fallback = shouldAttemptFallback(endpoint, currentEndpoint, 404);
     * if (fallback) {
     *     // Execute fallback retry
     * }
     *
     * @see executeFallbackRetry - Executes the actual retry
     */
    /**************************************************************/
    function shouldAttemptFallback(endpoint, currentEndpoint, statusCode) {
        // Check for explicit fallbackOnError configuration
        const fallback = endpoint.fallbackOnError || currentEndpoint.fallbackOnError;

        if (fallback) {
            if (!fallback.httpStatus) return null;
            if (!fallback.httpStatus.includes(statusCode)) return null;
            if (fallback.action !== 'retry_without_param') return null;
            if (!fallback.removeParams) return null;
            return fallback;
        }

        // AUTOMATIC FALLBACK for known patterns
        // Section retrieval endpoints: when specific sectionCode returns 404, retry without it
        const processedPath = currentEndpoint.path || endpoint.path || '';
        const queryParams = currentEndpoint.queryParameters || endpoint.queryParameters || {};

        if (statusCode === 404 &&
            processedPath.includes('/api/Label/markdown/sections/') &&
            queryParams.sectionCode) {
            console.log(`[EndpointExecutor] AUTO-FALLBACK: Section endpoint returned 404 with sectionCode, will retry without sectionCode`);
            return {
                httpStatus: [404],
                action: 'retry_without_param',
                removeParams: ['sectionCode'],
                description: 'Auto-fallback: section not found, fetching ALL sections'
            };
        }

        return null;
    }

    /**************************************************************/
    /**
     * Processes a single expanded endpoint execution.
     *
     * @param {Object} expandedItem - Expanded endpoint item with variables
     * @param {Object} endpoint - Original endpoint specification
     * @param {Object} extractedVariables - Variables extracted from previous steps
     * @param {number} step - Current step number
     * @param {boolean} isArrayExpansion - Whether this is part of array expansion
     * @param {number} expandIdx - Index in array expansion (0-based)
     * @param {number} totalExpanded - Total number of expanded endpoints
     * @param {Object} assistantMessage - Message object for progress updates
     * @param {AbortController} abortController - For request cancellation
     * @param {number} completedCount - Number of completed endpoints
     * @param {number} totalEndpoints - Total endpoints to execute
     * @returns {Promise<Object>} Result object for the results array
     *
     * @description
     * Handles the complete lifecycle of executing a single endpoint:
     * 1. Variable substitution
     * 2. Progress update
     * 3. API call execution
     * 4. Output mapping processing (if applicable)
     * 5. Fallback retry (if configured and needed)
     *
     * @see executeEndpointsWithDependencies - Orchestrates endpoint execution
     */
    /**************************************************************/
    async function processExpandedEndpoint(
        expandedItem,
        endpoint,
        extractedVariables,
        step,
        isArrayExpansion,
        expandIdx,
        totalExpanded,
        assistantMessage,
        abortController,
        completedCount,
        totalEndpoints
    ) {
        const currentEndpoint = expandedItem.endpoint || expandedItem;
        const currentVars = expandedItem.variables || extractedVariables;

        // Substitute variables
        const processedEndpoint = substituteEndpointVariables(currentEndpoint, currentVars);

        // Update progress
        const expandInfo = isArrayExpansion ? ` [${expandIdx + 1}/${totalExpanded}]` : '';
        assistantMessage.progress = completedCount / totalEndpoints;
        assistantMessage.progressStatus = `Step ${step}${expandInfo}: ${processedEndpoint.description || 'Executing query'}...`;
        MessageRenderer.updateMessage(assistantMessage.id);

        const startTime = Date.now();

        try {
            const { response, data, error, fullApiUrl } = await executeApiCall(processedEndpoint, abortController);
            console.log(`[EndpointExecutor] Step ${step}${expandInfo}: Executing API call: ${fullApiUrl}`);

            if (response.ok) {
                const hasData = resultHasData({ result: data, statusCode: response.status });
                console.log(`[EndpointExecutor] Step ${step}${expandInfo} succeeded: ${processedEndpoint.path} (hasData: ${hasData})`);

                // UNII Resolution Fallback: If product/latest returned 200 but empty, try to resolve via ingredient search
                if (!hasData) {
                    const uniiFallbackResult = await executeUniiResolutionFallback(
                        processedEndpoint,
                        endpoint,
                        abortController,
                        step,
                        expandInfo,
                        startTime,
                        currentEndpoint,
                        extractedVariables
                    );

                    if (uniiFallbackResult) {
                        // UNII fallback succeeded - process output mappings on the corrected result
                        if (!isArrayExpansion && endpoint.outputMapping) {
                            processOutputMappings(uniiFallbackResult.result, endpoint.outputMapping, extractedVariables);
                        } else if (!isArrayExpansion) {
                            autoExtractCommonFields(uniiFallbackResult.result, extractedVariables);
                        }
                        return uniiFallbackResult;
                    }
                    // If UNII fallback fails, continue with original empty result
                }

                // Extract output mappings (only on non-expanded or first expansion)
                if (!isArrayExpansion && endpoint.outputMapping) {
                    processOutputMappings(data, endpoint.outputMapping, extractedVariables);
                } else if (!isArrayExpansion) {
                    // Auto-extract if no explicit mapping
                    autoExtractCommonFields(data, extractedVariables);
                }

                return {
                    specification: processedEndpoint,
                    statusCode: response.status,
                    result: data,
                    executionTimeMs: Date.now() - startTime,
                    step: step,
                    hasData: hasData,
                    _expandedIndex: currentEndpoint._expandedIndex,
                    _expandedTotal: currentEndpoint._expandedTotal,
                    _apiUrl: fullApiUrl  // Include URL for synthesis data references
                };
            } else {
                console.log(`[EndpointExecutor] Step ${step}${expandInfo} failed: ${processedEndpoint.path} - HTTP ${response.status}`);

                // Check for fallback retry
                const fallback = shouldAttemptFallback(endpoint, currentEndpoint, response.status);
                if (fallback) {
                    return await executeFallbackRetry(
                        processedEndpoint,
                        fallback,
                        abortController,
                        step,
                        expandInfo,
                        startTime,
                        currentEndpoint
                    );
                }

                // No fallback - return failure result
                return {
                    specification: processedEndpoint,
                    statusCode: response.status,
                    result: null,
                    error: `HTTP ${response.status}`,
                    executionTimeMs: Date.now() - startTime,
                    step: step,
                    hasData: false
                };
            }
        } catch (endpointError) {
            console.log(`[EndpointExecutor] Step ${step}${expandInfo} exception: ${endpointError.message}`);
            return {
                specification: processedEndpoint,
                statusCode: 500,
                error: endpointError.message,
                step: step,
                hasData: false
            };
        }
    }

    /**************************************************************/
    /**
     * Identifies documentGuids from UNII fallback that weren't fetched.
     *
     * @param {Array} results - Current execution results
     * @param {Object} extractedVariables - Variables including documentGuids from fallback
     * @returns {Object} { unfetchedGuids: Array, fetchedGuids: Set }
     *
     * @description
     * When UNII fallback succeeds, it populates documentGuidsN variables.
     * However, the original interpretation may not have included steps
     * to fetch their label content. This function identifies which
     * documentGuids have data but haven't had their labels fetched.
     *
     * @see fetchUnfetchedLabelContent - Uses this to determine what to fetch
     */
    /**************************************************************/
    function identifyUnfetchedDocumentGuids(results, extractedVariables) {
        // Find all documentGuids that were actually fetched (label content retrieved)
        const fetchedGuids = new Set();
        for (const result of results) {
            if (result.hasData && result.specification && result.specification.path) {
                const path = result.specification.path;
                // Match /api/Label/markdown/sections/{guid} pattern
                const match = path.match(/\/api\/Label\/markdown\/sections\/([a-f0-9-]+)/i);
                if (match) {
                    fetchedGuids.add(match[1].toLowerCase());
                }
            }
        }

        console.log(`[EndpointExecutor] DYNAMIC FETCH CHECK: Already fetched ${fetchedGuids.size} document GUIDs`);

        // Find all documentGuidsN variables that have data but weren't fetched
        const unfetchedGuids = [];
        for (const [varName, value] of Object.entries(extractedVariables)) {
            // Match documentGuidsN pattern (e.g., documentGuids1, documentGuids2, etc.)
            if (/^documentGuids\d+$/i.test(varName)) {
                const guids = Array.isArray(value) ? value : [value];
                for (const guid of guids) {
                    if (guid && typeof guid === 'string' && !fetchedGuids.has(guid.toLowerCase())) {
                        unfetchedGuids.push({
                            guid: guid,
                            sourceVariable: varName
                        });
                        console.log(`[EndpointExecutor] DYNAMIC FETCH: Found unfetched GUID '${guid}' from '${varName}'`);
                    }
                }
            }
        }

        return { unfetchedGuids, fetchedGuids };
    }

    /**************************************************************/
    /**
     * Fetches label content for documentGuids discovered via UNII fallback.
     *
     * @param {Array} results - Current execution results
     * @param {Object} extractedVariables - Variables including documentGuids from fallback
     * @param {Object} assistantMessage - Message object for progress updates
     * @param {AbortController} abortController - For request cancellation
     * @returns {Promise<Array>} Array of additional results from dynamic fetches
     *
     * @description
     * This function addresses the case where UNII fallback successfully
     * finds products (and their documentGuids), but the original AI
     * interpretation didn't include steps to fetch those specific labels.
     *
     * For example:
     * - Original plan: search UNII X (wrong), fetch labels for documentGuids1
     * - Fallback: search ingredient name, find correct UNII Y, store in documentGuids2
     * - Problem: No step exists to fetch labels for documentGuids2
     * - Solution: This function dynamically fetches those labels
     *
     * @example
     * // extractedVariables contains:
     * // { documentGuids1: ['abc'], documentGuids2: ['def', 'ghi'] }
     * // results contains label fetches for 'abc' but not 'def' or 'ghi'
     * // This function fetches labels for 'def' and 'ghi'
     *
     * @see identifyUnfetchedDocumentGuids - Identifies what needs fetching
     * @see executeEndpointsWithDependencies - Calls this post-execution
     */
    /**************************************************************/
    async function fetchUnfetchedLabelContent(results, extractedVariables, assistantMessage, abortController) {
        const { unfetchedGuids, fetchedGuids } = identifyUnfetchedDocumentGuids(results, extractedVariables);

        if (unfetchedGuids.length === 0) {
            console.log('[EndpointExecutor] DYNAMIC FETCH: No unfetched document GUIDs found');
            return [];
        }

        console.log(`[EndpointExecutor] === DYNAMIC LABEL FETCH: ${unfetchedGuids.length} unfetched GUIDs ===`);

        const dynamicResults = [];
        const maxStep = Math.max(...results.map(r => r.step || 0), 0);

        for (let i = 0; i < unfetchedGuids.length; i++) {
            const { guid, sourceVariable } = unfetchedGuids[i];
            const dynamicStep = maxStep + 1 + i;

            // Update progress
            assistantMessage.progressStatus = `Fetching additional label data (${i + 1}/${unfetchedGuids.length})...`;
            MessageRenderer.updateMessage(assistantMessage.id);

            console.log(`[EndpointExecutor] DYNAMIC FETCH [${i + 1}/${unfetchedGuids.length}]: Fetching label for '${guid}' (from ${sourceVariable})`);

            const endpoint = {
                method: 'GET',
                path: `/api/Label/markdown/sections/${guid}`,
                description: `Dynamic fetch: Label content for ${sourceVariable}`,
                _dynamicFetch: true,
                _sourceVariable: sourceVariable
            };

            const startTime = Date.now();

            try {
                const { response, data, fullApiUrl } = await executeApiCall(endpoint, abortController);

                if (response.ok) {
                    const hasData = resultHasData({ result: data, statusCode: response.status });
                    console.log(`[EndpointExecutor] DYNAMIC FETCH [${i + 1}/${unfetchedGuids.length}] succeeded: ${endpoint.path} (hasData: ${hasData})`);

                    dynamicResults.push({
                        specification: endpoint,
                        statusCode: response.status,
                        result: data,
                        executionTimeMs: Date.now() - startTime,
                        step: dynamicStep,
                        hasData: hasData,
                        _dynamicFetch: true,
                        _sourceVariable: sourceVariable,
                        _apiUrl: fullApiUrl
                    });
                } else {
                    console.log(`[EndpointExecutor] DYNAMIC FETCH [${i + 1}/${unfetchedGuids.length}] failed: HTTP ${response.status}`);
                    dynamicResults.push({
                        specification: endpoint,
                        statusCode: response.status,
                        result: null,
                        error: `HTTP ${response.status}`,
                        executionTimeMs: Date.now() - startTime,
                        step: dynamicStep,
                        hasData: false,
                        _dynamicFetch: true,
                        _sourceVariable: sourceVariable
                    });
                }
            } catch (error) {
                console.log(`[EndpointExecutor] DYNAMIC FETCH [${i + 1}/${unfetchedGuids.length}] exception: ${error.message}`);
                dynamicResults.push({
                    specification: endpoint,
                    statusCode: 500,
                    error: error.message,
                    step: dynamicStep,
                    hasData: false,
                    _dynamicFetch: true,
                    _sourceVariable: sourceVariable
                });
            }
        }

        console.log(`[EndpointExecutor] === DYNAMIC LABEL FETCH COMPLETE: ${dynamicResults.filter(r => r.hasData).length}/${unfetchedGuids.length} successful ===`);

        return dynamicResults;
    }

    /**************************************************************/
    /**
     * Creates a skipped result for an endpoint that was not executed.
     *
     * @param {Object} endpoint - Endpoint specification
     * @param {number} step - Step number
     * @param {string} reason - Reason for skipping
     * @param {string} [skippedReason] - Optional reason type (e.g., 'previous_has_results')
     * @returns {Object} Result object marked as skipped
     *
     * @description
     * Creates a standardized result object for endpoints that were
     * skipped due to failed dependencies or skip conditions.
     *
     * @example
     * results.push(createSkippedResult(endpoint, 2, 'dependency step 1 failed'));
     *
     * @see executeEndpointsWithDependencies - Uses for skipped endpoints
     */
    /**************************************************************/
    function createSkippedResult(endpoint, step, reason, skippedReason = null) {
        const result = {
            specification: endpoint,
            statusCode: 0,
            result: null,
            error: `Skipped: ${reason}`,
            skipped: true,
            step: step
        };

        if (skippedReason) {
            result.skippedReason = skippedReason;
        }

        return result;
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
     * 8. Handles fallback retries for 404 responses
     *
     * Endpoint specification properties:
     * - step: Execution order (lower = earlier)
     * - dependsOn: Array of step numbers that must succeed first
     * - skipIfPreviousHasResults: Step number - skip if that step had data
     * - outputMapping: Map of varName -> jsonPath for extraction
     * - fallbackOnError: Configuration for retry on specific HTTP status codes
     * - path, method, queryParameters, body: Standard endpoint properties
     *
     * @example
     * const endpoints = [
     *     { step: 1, path: '/api/search', outputMapping: { docId: '$.documentGuid' } },
     *     { step: 2, dependsOn: [1], path: '/api/Label/{{docId}}' }
     * ];
     * const results = await executeEndpointsWithDependencies(endpoints, msg, controller);
     *
     * @see groupEndpointsByStep - Groups endpoints for ordered execution
     * @see checkDependencies - Validates dependency satisfaction
     * @see checkSkipCondition - Checks skip conditions
     * @see expandEndpointForArrays - Expands array variables
     * @see processExpandedEndpoint - Executes individual endpoints
     */
    /**************************************************************/
    async function executeEndpointsWithDependencies(endpoints, assistantMessage, abortController) {
        const results = [];
        const extractedVariables = {};

        // Log execution start
        console.log('[EndpointExecutor] ========================================');
        console.log('[EndpointExecutor] MULTI-STEP ENDPOINT EXECUTION STARTED');
        console.log(`[EndpointExecutor] Total endpoints: ${endpoints.length}`);
        console.log('[EndpointExecutor] ========================================');

        // Log endpoint details for debugging
        endpoints.forEach((ep, idx) => {
            console.log(`[EndpointExecutor] Endpoint ${idx + 1}: step=${ep.step}, path=${ep.path}, dependsOn=${ep.dependsOn}, skipIfPreviousHasResults=${ep.skipIfPreviousHasResults}, hasOutputMapping=${!!ep.outputMapping}`);
        });

        // Group and sort endpoints by step
        const endpointsByStep = groupEndpointsByStep(endpoints);
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
                const depCheck = checkDependencies(endpoint, results, step);
                if (!depCheck.satisfied) {
                    const failedSteps = depCheck.failedSteps.join(', ');
                    console.log(`[EndpointExecutor] Skipping step ${step} - dependency step(s) [${failedSteps}] failed or missing`);
                    results.push(createSkippedResult(endpoint, step, `dependency step(s) [${failedSteps}] failed`));
                    completedCount++;
                    continue;
                }

                if (depCheck.failedSteps.length === 0 && endpoint.dependsOn) {
                    console.log(`[EndpointExecutor] All dependencies for step ${step} satisfied`);
                }

                // Check skip condition
                const skipCheck = checkSkipCondition(endpoint, results, step);
                if (skipCheck.shouldSkip) {
                    results.push(createSkippedResult(endpoint, step, skipCheck.reason, 'previous_has_results'));
                    completedCount++;
                    continue;
                }

                console.log(`[EndpointExecutor] Current extractedVariables: ${JSON.stringify(extractedVariables)}`);

                // Expand for array variables
                const expandedEndpoints = expandEndpointForArrays(endpoint, extractedVariables);
                const isArrayExpansion = expandedEndpoints.length > 1 ||
                    (expandedEndpoints.length === 1 && expandedEndpoints[0].variables);

                if (isArrayExpansion) {
                    console.log(`[EndpointExecutor] === MULTI-DOCUMENT EXPANSION: ${expandedEndpoints.length} calls ===`);
                }

                // Execute each expanded endpoint
                for (let expandIdx = 0; expandIdx < expandedEndpoints.length; expandIdx++) {
                    const result = await processExpandedEndpoint(
                        expandedEndpoints[expandIdx],
                        endpoint,
                        extractedVariables,
                        step,
                        isArrayExpansion,
                        expandIdx,
                        expandedEndpoints.length,
                        assistantMessage,
                        abortController,
                        completedCount,
                        totalEndpoints
                    );
                    results.push(result);
                }

                completedCount++;
            }
        }

        // ================================================================
        // DYNAMIC LABEL FETCH: Check for unfetched documentGuids from UNII fallback
        // When UNII fallback discovers correct products, the documentGuids are stored
        // in extractedVariables, but the original plan may not have included steps
        // to fetch their label content. This section identifies and fetches them.
        // ================================================================
        const unfetchedResults = await fetchUnfetchedLabelContent(
            results,
            extractedVariables,
            assistantMessage,
            abortController
        );

        if (unfetchedResults.length > 0) {
            console.log(`[EndpointExecutor] Added ${unfetchedResults.length} dynamically fetched label results`);
            results.push(...unfetchedResults);
        }

        // Log execution complete
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
