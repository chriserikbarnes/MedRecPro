/**************************************************************/
/**
 * Batch Synthesizer Module
 *
 * @fileoverview Handles progressive synthesis by processing results in batches
 * and updating the UI as each batch completes.
 *
 * @description
 * The batch synthesizer module provides:
 * - Batching of results by product group
 * - Progressive synthesis with UI updates after each batch
 * - Accumulated context passing between batches
 * - Batch combination for efficiency with small groups
 *
 * This module enables the progressive display experience where users
 * see content appearing incrementally rather than waiting for everything.
 *
 * @example
 * import { BatchSynthesizer } from './batch-synthesizer.js';
 *
 * // Synthesize in batches with callbacks
 * await BatchSynthesizer.synthesizeInBatches(groups, context, {
 *     onBatchStart: (batch, index, total) => updateUI(batch),
 *     onBatchComplete: (response, batch) => appendContent(response),
 *     onComplete: (fullResponse) => finalize(fullResponse)
 * });
 *
 * @module chat/batch-synthesizer
 * @see ProgressiveConfig - Configuration for batch sizing
 * @see ResultGrouper - Provides grouped results
 * @see ApiService - Performs synthesis API calls
 */
/**************************************************************/

import { ProgressiveConfig } from './progressive-config.js';
import { ResultGrouper } from './result-grouper.js';
import { ApiService } from './api-service.js';
import { ChatState } from './state.js';

export const BatchSynthesizer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Main batch synthesis orchestrator.
     *
     * @param {Object} productGroups - Grouped results from ResultGrouper
     * @param {Object} context - Synthesis context
     * @param {string} context.originalQuery - User's original query
     * @param {Object} context.interpretation - Original interpretation result
     * @param {Object} callbacks - Progress and completion callbacks
     * @param {Function} [callbacks.onBatchStart] - Called when batch starts: (batchGroups, batchIndex, totalBatches)
     * @param {Function} [callbacks.onBatchComplete] - Called when batch completes: (batchResponse, batchGroups, batchIndex)
     * @param {Function} [callbacks.onComplete] - Called when all batches complete: (combinedResponse)
     * @param {Function} [callbacks.onError] - Called on error: (error, batchIndex)
     * @returns {Promise<Object>} Combined synthesis result
     *
     * @description
     * Orchestrates the batch synthesis process:
     * 1. Divides product groups into batches
     * 2. For each batch, calls synthesis API with accumulated context
     * 3. Updates UI after each batch via callbacks
     * 4. Combines all responses into final result
     *
     * @example
     * const result = await synthesizeInBatches(groups, {
     *     originalQuery: 'ACE inhibitor dosing',
     *     interpretation: {...}
     * }, {
     *     onBatchComplete: (response) => appendToMessage(response)
     * });
     *
     * @see ProgressiveConfig.getSynthesisBatchSize - Batch size
     * @see combineBatches - Batch grouping logic
     */
    /**************************************************************/
    async function synthesizeInBatches(productGroups, context, callbacks = {}) {
        const { originalQuery, interpretation } = context;
        const { onBatchStart, onBatchComplete, onComplete, onError } = callbacks;

        // Check if batch synthesis is enabled
        if (!ProgressiveConfig.isBatchSynthesisEnabled()) {
            console.log('[BatchSynthesizer] Batch synthesis disabled, using single call');
            return await synthesizeSingleCall(productGroups, context, callbacks);
        }

        // Create batches from groups
        const batches = createBatches(productGroups);
        const totalBatches = batches.length;

        console.log(`[BatchSynthesizer] Processing ${totalBatches} batch(es)`);

        // If only one batch, do single synthesis
        if (totalBatches === 1) {
            console.log('[BatchSynthesizer] Single batch, using standard synthesis');
            return await synthesizeSingleCall(productGroups, context, callbacks);
        }

        // Process batches sequentially with accumulated context
        let accumulatedContent = '';
        let allResponses = [];
        let allFollowUps = [];
        let allDataReferences = {};

        for (let i = 0; i < batches.length; i++) {
            const batch = batches[i];
            const batchGroups = batch.groups;
            const batchIndex = i + 1;

            console.log(`[BatchSynthesizer] Processing batch ${batchIndex}/${totalBatches}: ${batch.productNames.join(', ')}`);

            // Notify batch start (await in case callback is async for UI repaint)
            if (onBatchStart) {
                await onBatchStart(batchGroups, batchIndex, totalBatches);
            }

            try {
                // Get results for this batch
                const batchResults = ResultGrouper.getResultsFromGroups(
                    productGroups,
                    Object.keys(batchGroups)
                );

                // Create batch-specific context
                const batchContext = createBatchContext(
                    originalQuery,
                    interpretation,
                    accumulatedContent,
                    batchIndex,
                    totalBatches,
                    batch.productNames
                );

                // Call synthesis API
                const batchResponse = await ApiService.synthesizeResults(
                    batchContext.enhancedQuery,
                    interpretation,
                    batchResults
                );

                // Extract and accumulate response
                const responseContent = batchResponse.response || '';
                accumulatedContent += (accumulatedContent ? '\n\n' : '') + responseContent;
                allResponses.push(responseContent);

                // Collect follow-ups (deduplicated later)
                if (batchResponse.suggestedFollowUps) {
                    allFollowUps.push(...batchResponse.suggestedFollowUps);
                }

                // Collect data references
                if (batchResponse.dataReferences) {
                    Object.assign(allDataReferences, batchResponse.dataReferences);
                }

                // Notify batch complete (await in case callback is async for UI repaint)
                if (onBatchComplete) {
                    await onBatchComplete(batchResponse, batchGroups, batchIndex);
                }

            } catch (error) {
                console.error(`[BatchSynthesizer] Batch ${batchIndex} failed:`, error);

                if (onError) {
                    onError(error, batchIndex);
                }

                // Continue with next batch unless fatal
                if (error.name === 'AbortError') {
                    throw error;
                }
            }
        }

        // Build combined result
        const combinedResult = {
            response: accumulatedContent,
            suggestedFollowUps: deduplicateFollowUps(allFollowUps),
            dataReferences: allDataReferences,
            batchCount: totalBatches,
            partialResponses: allResponses
        };

        // Notify completion
        if (onComplete) {
            onComplete(combinedResult);
        }

        console.log(`[BatchSynthesizer] Completed ${totalBatches} batches`);

        return combinedResult;
    }

    /**************************************************************/
    /**
     * Fallback to single synthesis call when batching isn't needed.
     *
     * @param {Object} productGroups - Grouped results
     * @param {Object} context - Synthesis context
     * @param {Object} callbacks - Callbacks object
     * @returns {Promise<Object>} Synthesis result
     *
     * @description
     * Used when batch synthesis is disabled or there's only one batch.
     * Still invokes callbacks for consistent behavior.
     */
    /**************************************************************/
    async function synthesizeSingleCall(productGroups, context, callbacks = {}) {
        const { originalQuery, interpretation } = context;
        const { onBatchStart, onBatchComplete, onComplete, onError } = callbacks;

        // Get all results
        const allResults = ResultGrouper.getResultsFromGroups(
            productGroups,
            Object.keys(productGroups)
        );

        // Notify start
        if (onBatchStart) {
            onBatchStart(productGroups, 1, 1);
        }

        try {
            // Single synthesis call
            const response = await ApiService.synthesizeResults(
                originalQuery,
                interpretation,
                allResults
            );

            // Notify batch complete
            if (onBatchComplete) {
                onBatchComplete(response, productGroups, 1);
            }

            // Notify overall complete
            if (onComplete) {
                onComplete(response);
            }

            return response;

        } catch (error) {
            if (onError) {
                onError(error, 1);
            }
            throw error;
        }
    }

    /**************************************************************/
    /**
     * Creates batches from product groups.
     *
     * @param {Object} productGroups - Grouped results
     * @returns {Array} Array of batch objects
     *
     * @description
     * Divides groups into batches based on configuration:
     * - Uses synthesisBatchSize for target batch size
     * - Combines small trailing groups with previous batch
     * - Each batch includes group data and product names
     *
     * @see ProgressiveConfig.getSynthesisBatchSize - Target size
     * @see ProgressiveConfig.getMinBatchSize - Minimum for combination
     */
    /**************************************************************/
    function createBatches(productGroups) {
        const batchSize = ProgressiveConfig.getSynthesisBatchSize();
        const minBatchSize = ProgressiveConfig.getMinBatchSize();

        const groupIds = Object.keys(productGroups);
        const batches = [];

        for (let i = 0; i < groupIds.length; i += batchSize) {
            const batchIds = groupIds.slice(i, i + batchSize);

            // Build batch groups object
            const batchGroups = {};
            const productNames = [];

            batchIds.forEach(id => {
                batchGroups[id] = productGroups[id];
                productNames.push(productGroups[id].name);
            });

            batches.push({
                groups: batchGroups,
                productIds: batchIds,
                productNames: productNames
            });
        }

        // Combine small trailing batch if necessary
        if (batches.length > 1) {
            const lastBatch = batches[batches.length - 1];
            if (lastBatch.productIds.length < minBatchSize) {
                const previousBatch = batches[batches.length - 2];

                // Merge into previous batch
                lastBatch.productIds.forEach(id => {
                    previousBatch.groups[id] = productGroups[id];
                    previousBatch.productIds.push(id);
                    previousBatch.productNames.push(productGroups[id].name);
                });

                // Remove the small batch
                batches.pop();

                console.log(`[BatchSynthesizer] Combined small batch, now ${batches.length} batches`);
            }
        }

        return batches;
    }

    /**************************************************************/
    /**
     * Creates context for a batch synthesis request.
     *
     * @param {string} originalQuery - Original user query
     * @param {Object} interpretation - Original interpretation
     * @param {string} accumulatedContent - Content from previous batches
     * @param {number} batchIndex - Current batch number (1-based)
     * @param {number} totalBatches - Total number of batches
     * @param {Array<string>} productNames - Names of products in this batch
     * @returns {Object} Batch context with enhanced query
     *
     * @description
     * Creates a modified query that includes:
     * - Reference to accumulated content from previous batches
     * - Indication of which batch this is
     * - Instructions to avoid repeating previous content
     *
     * @example
     * const context = createBatchContext(
     *     'ACE inhibitor dosing',
     *     interpretation,
     *     'Lisinopril: 10mg...',
     *     2, 3,
     *     ['Enalapril', 'Ramipril']
     * );
     */
    /**************************************************************/
    function createBatchContext(originalQuery, interpretation, accumulatedContent, batchIndex, totalBatches, productNames) {
        let enhancedQuery = originalQuery;

        // Add batch context if not the first batch
        if (batchIndex > 1 && accumulatedContent) {
            const productList = productNames.join(', ');

            enhancedQuery = `[BATCH ${batchIndex}/${totalBatches}: Continue response for: ${productList}]

Previous content has covered other products. Now focus specifically on: ${productList}

Do not repeat information already provided. Maintain consistent formatting.

Original query: ${originalQuery}`;
        }

        return {
            originalQuery: originalQuery,
            enhancedQuery: enhancedQuery,
            interpretation: interpretation,
            batchIndex: batchIndex,
            totalBatches: totalBatches,
            productNames: productNames,
            hasPreviousContent: batchIndex > 1
        };
    }

    /**************************************************************/
    /**
     * Deduplicates follow-up suggestions.
     *
     * @param {Array<string>} followUps - Array of follow-up suggestions
     * @returns {Array<string>} Deduplicated array
     *
     * @description
     * Removes duplicate suggestions that may arise from
     * multiple batches suggesting the same follow-ups.
     */
    /**************************************************************/
    function deduplicateFollowUps(followUps) {
        if (!followUps || followUps.length === 0) return [];

        const seen = new Set();
        const unique = [];

        followUps.forEach(followUp => {
            // Normalize for comparison
            const normalized = followUp.toLowerCase().trim();

            if (!seen.has(normalized)) {
                seen.add(normalized);
                unique.push(followUp);
            }
        });

        return unique;
    }

    /**************************************************************/
    /**
     * Estimates time for batch synthesis.
     *
     * @param {number} productCount - Number of products
     * @param {number} avgSynthesisTime - Average synthesis time per batch (ms)
     * @returns {number} Estimated total time in milliseconds
     *
     * @description
     * Provides rough estimate for progress display.
     * Based on batch size and average API response time.
     */
    /**************************************************************/
    function estimateSynthesisTime(productCount, avgSynthesisTime = 3000) {
        const batchSize = ProgressiveConfig.getSynthesisBatchSize();
        const batchCount = Math.ceil(productCount / batchSize);

        return batchCount * avgSynthesisTime;
    }

    /**************************************************************/
    /**
     * Determines if batch synthesis should be used.
     *
     * @param {Object} productGroups - Grouped results
     * @returns {boolean} True if batching would be beneficial
     *
     * @description
     * Evaluates whether batch synthesis provides benefit:
     * - Feature must be enabled
     * - Must have more products than batch size
     * - Products must have actual data
     */
    /**************************************************************/
    function shouldUseBatchSynthesis(productGroups) {
        if (!ProgressiveConfig.isBatchSynthesisEnabled()) {
            return false;
        }

        const productCount = ResultGrouper.getProductCount(productGroups);
        const batchSize = ProgressiveConfig.getSynthesisBatchSize();

        // Only batch if we have more than one batch worth
        return productCount > batchSize;
    }

    /**************************************************************/
    /**
     * Gets batch information for progress display.
     *
     * @param {Object} productGroups - Grouped results
     * @returns {Object} Batch information object
     *
     * @description
     * Provides info for displaying batch progress:
     * - Total products
     * - Number of batches
     * - Products per batch
     */
    /**************************************************************/
    function getBatchInfo(productGroups) {
        const productCount = ResultGrouper.getProductCount(productGroups);
        const batchSize = ProgressiveConfig.getSynthesisBatchSize();
        const batchCount = Math.ceil(productCount / batchSize);

        return {
            productCount: productCount,
            batchSize: batchSize,
            batchCount: batchCount,
            willBatch: batchCount > 1
        };
    }

    /**************************************************************/
    /**
     * Public API for the batch synthesizer module.
     *
     * @description
     * Exposes batch synthesis orchestration and utilities.
     */
    /**************************************************************/
    return {
        // Main synthesis function
        synthesizeInBatches: synthesizeInBatches,

        // Decision helpers
        shouldUseBatchSynthesis: shouldUseBatchSynthesis,
        getBatchInfo: getBatchInfo,

        // Context creation (for custom implementations)
        createBatchContext: createBatchContext,

        // Utilities
        estimateSynthesisTime: estimateSynthesisTime,
        createBatches: createBatches
    };
})();
