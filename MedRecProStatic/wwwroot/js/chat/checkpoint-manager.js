/**************************************************************/
/**
 * Checkpoint Manager Module
 *
 * @fileoverview Manages checkpoint state and decision logic for when to show
 * user checkpoints during large query processing.
 *
 * @description
 * The checkpoint manager module provides:
 * - Decision logic for when to show user checkpoints
 * - Checkpoint state creation and management
 * - Selection filtering for user-chosen products
 * - Checkpoint lifecycle management (create, get, clear)
 *
 * This module acts as the controller for the checkpoint feature,
 * coordinating between the result grouper and checkpoint renderer.
 *
 * @example
 * import { CheckpointManager } from './checkpoint-manager.js';
 *
 * // Check if checkpoint should be shown
 * if (CheckpointManager.shouldShowCheckpoint(productGroups)) {
 *     CheckpointManager.createCheckpoint(checkpointData);
 *     showCheckpointUI();
 * }
 *
 * @module chat/checkpoint-manager
 * @see ProgressiveConfig - Configuration for checkpoint thresholds
 * @see ResultGrouper - Provides grouped results for checkpoints
 * @see CheckpointRenderer - Renders checkpoint UI
 */
/**************************************************************/

import { ProgressiveConfig } from './progressive-config.js';
import { ResultGrouper } from './result-grouper.js';

export const CheckpointManager = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Current pending checkpoint state.
     *
     * @description
     * Stores checkpoint data while waiting for user interaction.
     * Cleared after user makes selection or cancels.
     *
     * @type {Object|null}
     * @property {string} id - Unique checkpoint identifier
     * @property {string} messageId - Associated assistant message ID
     * @property {Object} productGroups - Grouped results by product
     * @property {string} originalQuery - Original user query
     * @property {Object} interpretation - Original interpretation result
     * @property {Array<string>} selectedProductIds - Currently selected product IDs
     * @property {number} createdAt - Timestamp when checkpoint was created
     * @property {string} status - 'pending', 'confirmed', 'cancelled'
     *
     * @see createCheckpoint - Creates new checkpoint
     * @see clearCheckpoint - Clears checkpoint
     */
    /**************************************************************/
    let pendingCheckpoint = null;

    /**************************************************************/
    /**
     * History of completed checkpoints for analytics.
     *
     * @type {Array}
     * @description
     * Stores completed checkpoint records for debugging and
     * potential analytics. Limited to last 10 checkpoints.
     */
    /**************************************************************/
    let checkpointHistory = [];
    const MAX_HISTORY_SIZE = 10;

    /**************************************************************/
    /**
     * Generates a unique checkpoint ID.
     *
     * @returns {string} Unique identifier for the checkpoint
     */
    /**************************************************************/
    function generateCheckpointId() {
        return 'chkpt-' + Date.now() + '-' + Math.random().toString(36).substring(2, 9);
    }

    /**************************************************************/
    /**
     * Determines if a checkpoint should be shown to the user.
     *
     * @param {Object} productGroups - Grouped results from ResultGrouper
     * @returns {boolean} True if checkpoint should be displayed
     *
     * @description
     * Evaluates multiple criteria to decide if checkpoint is warranted:
     * 1. Checkpoints must be enabled in configuration
     * 2. Product count must meet or exceed threshold
     * 3. Groups must contain actual data (not all empty)
     *
     * @example
     * const groups = ResultGrouper.groupResultsByProduct(results);
     * if (CheckpointManager.shouldShowCheckpoint(groups)) {
     *     // Show checkpoint UI
     * }
     *
     * @see ProgressiveConfig.isCheckpointEnabled - Feature flag
     * @see ProgressiveConfig.getCheckpointThreshold - Threshold value
     * @see ResultGrouper.getProductCount - Product counting
     */
    /**************************************************************/
    function shouldShowCheckpoint(productGroups) {
        // Check if checkpoints are enabled
        if (!ProgressiveConfig.isCheckpointEnabled()) {
            console.log('[CheckpointManager] Checkpoints disabled');
            return false;
        }

        // Check product count against threshold
        const productCount = ResultGrouper.getProductCount(productGroups);
        const threshold = ProgressiveConfig.getCheckpointThreshold();

        console.log(`[CheckpointManager] Product count: ${productCount}, threshold: ${threshold}`);

        if (productCount < threshold) {
            console.log('[CheckpointManager] Below threshold, no checkpoint needed');
            return false;
        }

        // Verify we have groups with actual data
        const groupsWithData = ResultGrouper.filterGroupsWithData(productGroups);
        const dataGroupCount = ResultGrouper.getProductCount(groupsWithData);

        if (dataGroupCount === 0) {
            console.log('[CheckpointManager] No groups with data, skipping checkpoint');
            return false;
        }

        console.log(`[CheckpointManager] Showing checkpoint for ${dataGroupCount} products with data`);
        return true;
    }

    /**************************************************************/
    /**
     * Creates and stores a new checkpoint.
     *
     * @param {Object} data - Checkpoint initialization data
     * @param {string} data.messageId - Assistant message ID
     * @param {Object} data.productGroups - Grouped results
     * @param {string} data.originalQuery - Original user query
     * @param {Object} data.interpretation - Original interpretation
     * @param {Array} [data.allResults] - All endpoint results
     * @returns {Object} The created checkpoint object
     *
     * @description
     * Creates a checkpoint state object with all products initially selected.
     * The checkpoint is stored and can be retrieved via getPendingCheckpoint().
     *
     * @example
     * const checkpoint = CheckpointManager.createCheckpoint({
     *     messageId: assistantMessage.id,
     *     productGroups: groups,
     *     originalQuery: userInput,
     *     interpretation: interpretation
     * });
     *
     * @see getPendingCheckpoint - Retrieves the created checkpoint
     * @see updateSelection - Updates selected products
     */
    /**************************************************************/
    function createCheckpoint(data) {
        const { messageId, productGroups, originalQuery, interpretation, allResults } = data;

        // Get all product IDs (all selected by default)
        const allProductIds = Object.keys(productGroups);

        // Create checkpoint object
        pendingCheckpoint = {
            id: generateCheckpointId(),
            messageId: messageId,
            productGroups: productGroups,
            originalQuery: originalQuery,
            interpretation: interpretation,
            allResults: allResults || [],
            selectedProductIds: [...allProductIds],
            allProductIds: allProductIds,
            createdAt: Date.now(),
            status: 'pending'
        };

        console.log(`[CheckpointManager] Created checkpoint ${pendingCheckpoint.id} with ${allProductIds.length} products`);

        return pendingCheckpoint;
    }

    /**************************************************************/
    /**
     * Gets the current pending checkpoint.
     *
     * @returns {Object|null} Current checkpoint or null if none pending
     *
     * @example
     * const checkpoint = CheckpointManager.getPendingCheckpoint();
     * if (checkpoint) {
     *     renderCheckpointUI(checkpoint);
     * }
     *
     * @see createCheckpoint - Creates checkpoints
     * @see clearCheckpoint - Clears checkpoints
     */
    /**************************************************************/
    function getPendingCheckpoint() {
        return pendingCheckpoint;
    }

    /**************************************************************/
    /**
     * Checks if there is a pending checkpoint.
     *
     * @returns {boolean} True if a checkpoint is pending
     */
    /**************************************************************/
    function hasPendingCheckpoint() {
        return pendingCheckpoint !== null && pendingCheckpoint.status === 'pending';
    }

    /**************************************************************/
    /**
     * Updates the selected products in the checkpoint.
     *
     * @param {Array<string>} selectedIds - Array of selected product IDs
     * @returns {boolean} True if update was successful
     *
     * @description
     * Called when user checks/unchecks products in the checkpoint UI.
     * Validates that all IDs are valid product IDs.
     *
     * @example
     * // User unchecks a product
     * const newSelection = currentSelection.filter(id => id !== uncheckedId);
     * CheckpointManager.updateSelection(newSelection);
     *
     * @see getSelectedResults - Gets results for selected products
     */
    /**************************************************************/
    function updateSelection(selectedIds) {
        if (!pendingCheckpoint) {
            console.warn('[CheckpointManager] No pending checkpoint to update');
            return false;
        }

        // Validate all IDs exist
        const validIds = selectedIds.filter(id => pendingCheckpoint.allProductIds.includes(id));

        if (validIds.length !== selectedIds.length) {
            console.warn('[CheckpointManager] Some selected IDs are invalid');
        }

        pendingCheckpoint.selectedProductIds = validIds;

        console.log(`[CheckpointManager] Selection updated: ${validIds.length}/${pendingCheckpoint.allProductIds.length} products`);

        return true;
    }

    /**************************************************************/
    /**
     * Toggles selection state for a single product.
     *
     * @param {string} productId - Product ID to toggle
     * @returns {boolean} New selection state (true = selected)
     *
     * @description
     * Convenience method for handling individual checkbox toggles.
     *
     * @example
     * // In checkbox onclick handler
     * const isNowSelected = CheckpointManager.toggleProduct(productId);
     * checkbox.checked = isNowSelected;
     */
    /**************************************************************/
    function toggleProduct(productId) {
        if (!pendingCheckpoint) return false;

        const currentIndex = pendingCheckpoint.selectedProductIds.indexOf(productId);

        if (currentIndex === -1) {
            // Add to selection
            pendingCheckpoint.selectedProductIds.push(productId);
            return true;
        } else {
            // Remove from selection
            pendingCheckpoint.selectedProductIds.splice(currentIndex, 1);
            return false;
        }
    }

    /**************************************************************/
    /**
     * Selects all products in the checkpoint.
     *
     * @returns {Array<string>} All product IDs (now selected)
     */
    /**************************************************************/
    function selectAll() {
        if (!pendingCheckpoint) return [];

        pendingCheckpoint.selectedProductIds = [...pendingCheckpoint.allProductIds];

        console.log(`[CheckpointManager] Selected all: ${pendingCheckpoint.selectedProductIds.length} products`);

        return pendingCheckpoint.selectedProductIds;
    }

    /**************************************************************/
    /**
     * Deselects all products in the checkpoint.
     *
     * @returns {Array} Empty array (no products selected)
     */
    /**************************************************************/
    function selectNone() {
        if (!pendingCheckpoint) return [];

        pendingCheckpoint.selectedProductIds = [];

        console.log('[CheckpointManager] Deselected all products');

        return pendingCheckpoint.selectedProductIds;
    }

    /**************************************************************/
    /**
     * Gets the results for currently selected products.
     *
     * @returns {Array} Array of endpoint results for selected products
     *
     * @description
     * Filters the original results to only include those from
     * the products the user has selected in the checkpoint.
     *
     * @example
     * const selectedResults = CheckpointManager.getSelectedResults();
     * await synthesize(selectedResults);
     *
     * @see ResultGrouper.getResultsFromGroups - Underlying implementation
     */
    /**************************************************************/
    function getSelectedResults() {
        if (!pendingCheckpoint) return [];

        return ResultGrouper.getResultsFromGroups(
            pendingCheckpoint.productGroups,
            pendingCheckpoint.selectedProductIds
        );
    }

    /**************************************************************/
    /**
     * Gets results for specified product IDs.
     *
     * @param {Object} productGroups - Product groups to filter
     * @param {Array<string>} selectedIds - Product IDs to include
     * @returns {Array} Array of endpoint results
     *
     * @description
     * Alternative to getSelectedResults() when you have the groups
     * and selections available directly.
     */
    /**************************************************************/
    function getSelectedResultsFromGroups(productGroups, selectedIds) {
        return ResultGrouper.getResultsFromGroups(productGroups, selectedIds);
    }

    /**************************************************************/
    /**
     * Gets the count of currently selected products.
     *
     * @returns {number} Number of selected products
     */
    /**************************************************************/
    function getSelectedCount() {
        if (!pendingCheckpoint) return 0;
        return pendingCheckpoint.selectedProductIds.length;
    }

    /**************************************************************/
    /**
     * Gets the total count of available products.
     *
     * @returns {number} Total number of products in checkpoint
     */
    /**************************************************************/
    function getTotalCount() {
        if (!pendingCheckpoint) return 0;
        return pendingCheckpoint.allProductIds.length;
    }

    /**************************************************************/
    /**
     * Checks if a product is currently selected.
     *
     * @param {string} productId - Product ID to check
     * @returns {boolean} True if product is selected
     */
    /**************************************************************/
    function isSelected(productId) {
        if (!pendingCheckpoint) return false;
        return pendingCheckpoint.selectedProductIds.includes(productId);
    }

    /**************************************************************/
    /**
     * Confirms the checkpoint with current selection.
     *
     * @returns {Object} Confirmation data with selected results
     *
     * @description
     * Called when user clicks "Synthesize Selected" button.
     * Marks checkpoint as confirmed and returns data needed for synthesis.
     *
     * @example
     * const confirmation = CheckpointManager.confirmCheckpoint();
     * await synthesize(confirmation.results, confirmation.query);
     */
    /**************************************************************/
    function confirmCheckpoint() {
        if (!pendingCheckpoint) {
            console.warn('[CheckpointManager] No pending checkpoint to confirm');
            return null;
        }

        const selectedResults = getSelectedResults();

        const confirmation = {
            checkpointId: pendingCheckpoint.id,
            messageId: pendingCheckpoint.messageId,
            originalQuery: pendingCheckpoint.originalQuery,
            interpretation: pendingCheckpoint.interpretation,
            results: selectedResults,
            selectedProductIds: [...pendingCheckpoint.selectedProductIds],
            selectedIds: [...pendingCheckpoint.selectedProductIds], // Alias for easier access
            totalProducts: pendingCheckpoint.allProductIds.length,
            selectedProducts: pendingCheckpoint.selectedProductIds.length
        };

        // Update status
        pendingCheckpoint.status = 'confirmed';
        pendingCheckpoint.confirmedAt = Date.now();

        // Add to history
        addToHistory(pendingCheckpoint);

        console.log(`[CheckpointManager] Confirmed checkpoint ${pendingCheckpoint.id}: ${confirmation.selectedProducts}/${confirmation.totalProducts} products`);

        // Clear pending (but keep in history)
        pendingCheckpoint = null;

        return confirmation;
    }

    /**************************************************************/
    /**
     * Cancels the checkpoint and clears state.
     *
     * @returns {Object|null} The cancelled checkpoint data
     *
     * @description
     * Called when user clicks "Cancel" button.
     * Marks checkpoint as cancelled and clears pending state.
     *
     * @example
     * const cancelled = CheckpointManager.cancelCheckpoint();
     * showCancellationMessage(cancelled.messageId);
     */
    /**************************************************************/
    function cancelCheckpoint() {
        if (!pendingCheckpoint) {
            console.warn('[CheckpointManager] No pending checkpoint to cancel');
            return null;
        }

        const cancelled = {
            checkpointId: pendingCheckpoint.id,
            messageId: pendingCheckpoint.messageId,
            totalProducts: pendingCheckpoint.allProductIds.length
        };

        // Update status
        pendingCheckpoint.status = 'cancelled';
        pendingCheckpoint.cancelledAt = Date.now();

        // Add to history
        addToHistory(pendingCheckpoint);

        console.log(`[CheckpointManager] Cancelled checkpoint ${pendingCheckpoint.id}`);

        // Clear pending
        pendingCheckpoint = null;

        return cancelled;
    }

    /**************************************************************/
    /**
     * Clears the pending checkpoint without recording.
     *
     * @description
     * Use this for cleanup scenarios where the checkpoint should
     * be discarded without recording in history.
     */
    /**************************************************************/
    function clearCheckpoint() {
        if (pendingCheckpoint) {
            console.log(`[CheckpointManager] Cleared checkpoint ${pendingCheckpoint.id}`);
            pendingCheckpoint = null;
        }
    }

    /**************************************************************/
    /**
     * Adds a completed checkpoint to history.
     *
     * @param {Object} checkpoint - Checkpoint to add
     * @private
     */
    /**************************************************************/
    function addToHistory(checkpoint) {
        checkpointHistory.unshift({
            id: checkpoint.id,
            status: checkpoint.status,
            totalProducts: checkpoint.allProductIds.length,
            selectedProducts: checkpoint.selectedProductIds.length,
            createdAt: checkpoint.createdAt,
            completedAt: Date.now()
        });

        // Trim history to max size
        if (checkpointHistory.length > MAX_HISTORY_SIZE) {
            checkpointHistory = checkpointHistory.slice(0, MAX_HISTORY_SIZE);
        }
    }

    /**************************************************************/
    /**
     * Gets checkpoint history for debugging/analytics.
     *
     * @returns {Array} Array of historical checkpoint records
     */
    /**************************************************************/
    function getHistory() {
        return [...checkpointHistory];
    }

    /**************************************************************/
    /**
     * Gets statistics about checkpoint usage.
     *
     * @returns {Object} Statistics object
     */
    /**************************************************************/
    function getStats() {
        const confirmed = checkpointHistory.filter(c => c.status === 'confirmed');
        const cancelled = checkpointHistory.filter(c => c.status === 'cancelled');

        const avgSelectedRatio = confirmed.length > 0
            ? confirmed.reduce((sum, c) => sum + (c.selectedProducts / c.totalProducts), 0) / confirmed.length
            : 0;

        return {
            totalCheckpoints: checkpointHistory.length,
            confirmed: confirmed.length,
            cancelled: cancelled.length,
            avgSelectedRatio: Math.round(avgSelectedRatio * 100),
            hasPending: hasPendingCheckpoint()
        };
    }

    /**************************************************************/
    /**
     * Public API for the checkpoint manager module.
     *
     * @description
     * Exposes checkpoint management functions.
     */
    /**************************************************************/
    return {
        // Decision making
        shouldShowCheckpoint: shouldShowCheckpoint,

        // Checkpoint lifecycle
        createCheckpoint: createCheckpoint,
        getPendingCheckpoint: getPendingCheckpoint,
        hasPendingCheckpoint: hasPendingCheckpoint,
        clearCheckpoint: clearCheckpoint,

        // Selection management
        updateSelection: updateSelection,
        toggleProduct: toggleProduct,
        selectAll: selectAll,
        selectNone: selectNone,
        isSelected: isSelected,

        // Selection queries
        getSelectedResults: getSelectedResults,
        getSelectedResultsFromGroups: getSelectedResultsFromGroups,
        getSelectedCount: getSelectedCount,
        getTotalCount: getTotalCount,

        // User actions
        confirmCheckpoint: confirmCheckpoint,
        cancelCheckpoint: cancelCheckpoint,

        // Debugging/analytics
        getHistory: getHistory,
        getStats: getStats
    };
})();
