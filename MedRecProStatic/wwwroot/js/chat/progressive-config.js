/**************************************************************/
/**
 * Progressive Response Configuration Module
 *
 * @fileoverview Centralized settings for progressive response features including
 * checkpoints, batch synthesis, and progress display.
 *
 * @description
 * This module provides configuration for the progressive response system:
 * - Checkpoint thresholds - when to show user checkpoints
 * - Batch synthesis settings - how to group and process results
 * - Progress display settings - how to show execution progress
 * - Feature flags - enable/disable progressive features
 *
 * @example
 * import { ProgressiveConfig } from './progressive-config.js';
 *
 * if (ProgressiveConfig.enableCheckpoints && productCount >= ProgressiveConfig.checkpointThreshold) {
 *     showCheckpoint();
 * }
 *
 * @module chat/progressive-config
 * @see CheckpointManager - Uses configuration for checkpoint decisions
 * @see BatchSynthesizer - Uses configuration for batch sizing
 * @see ResultGrouper - Uses configuration for grouping logic
 */
/**************************************************************/

export const ProgressiveConfig = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Core configuration settings for progressive response features.
     *
     * @description
     * All settings are designed to be easily adjustable for tuning
     * the user experience. Feature flags allow quick rollback if needed.
     *
     * @property {number} checkpointThreshold - Show checkpoint if this many products found
     * @property {boolean} enableCheckpoints - Master switch for checkpoint feature
     * @property {boolean} enableBatchSynthesis - Master switch for batch synthesis
     * @property {boolean} enableDetailedProgress - Show product names during execution
     * @property {number} synthesisBatchSize - Number of products per synthesis batch
     * @property {number} minBatchSize - Minimum products to form a batch
     * @property {number} maxProgressItems - Max items to show in progress list
     * @property {boolean} showEstimatedTime - Show time estimate (experimental)
     */
    /**************************************************************/
    const config = {
        // region: checkpoint_settings
        /**
         * Number of unique products that triggers a checkpoint.
         * When endpoint execution finds this many or more distinct products,
         * the system pauses and shows a selection UI.
         */
        checkpointThreshold: 5,

        /**
         * Master switch for checkpoint feature.
         * Set to false to disable checkpoints entirely and revert to original behavior.
         */
        enableCheckpoints: true,
        // endregion: checkpoint_settings

        // region: batch_synthesis_settings
        /**
         * Master switch for batch synthesis.
         * When enabled, results are synthesized in batches with progressive display.
         * When disabled, all results are synthesized in a single request.
         */
        enableBatchSynthesis: true,

        /**
         * Number of products to include in each synthesis batch.
         * Smaller batches provide faster initial feedback but more API calls.
         * Larger batches are more efficient but delay initial response.
         */
        synthesisBatchSize: 3,

        /**
         * Minimum number of products to form a standalone batch.
         * If remaining products are fewer than this, combine with previous batch.
         */
        minBatchSize: 2,
        // endregion: batch_synthesis_settings

        // region: progress_display_settings
        /**
         * Master switch for detailed progress display.
         * When enabled, shows actual product names during execution.
         * When disabled, shows generic "Executing queries..." message.
         */
        enableDetailedProgress: true,

        /**
         * Maximum number of completed items to display in progress list.
         * Older items are hidden to prevent UI clutter.
         */
        maxProgressItems: 5,

        /**
         * Whether to show estimated time remaining.
         * Experimental feature - estimates based on average execution time.
         */
        showEstimatedTime: false,
        // endregion: progress_display_settings

        // region: animation_settings
        /**
         * Duration in ms for progress item fade-in animation.
         */
        progressAnimationDuration: 200,

        /**
         * Duration in ms for checkpoint panel slide animation.
         */
        checkpointAnimationDuration: 300,
        // endregion: animation_settings
    };

    /**************************************************************/
    /**
     * Determines if the checkpoint feature should be active.
     *
     * @returns {boolean} True if checkpoints are enabled and threshold is valid
     *
     * @description
     * Combines the enable flag with threshold validation.
     * Returns false if threshold is set to an unreasonable value.
     *
     * @example
     * if (ProgressiveConfig.isCheckpointEnabled()) {
     *     // Initialize checkpoint system
     * }
     *
     * @see enableCheckpoints - Master switch
     * @see checkpointThreshold - Threshold value
     */
    /**************************************************************/
    function isCheckpointEnabled() {
        return config.enableCheckpoints && config.checkpointThreshold > 0;
    }

    /**************************************************************/
    /**
     * Determines if batch synthesis should be used.
     *
     * @returns {boolean} True if batch synthesis is enabled and configured properly
     *
     * @description
     * Validates that batch synthesis settings are sensible before enabling.
     *
     * @example
     * if (ProgressiveConfig.isBatchSynthesisEnabled()) {
     *     // Use batch synthesizer
     * } else {
     *     // Use single synthesis call
     * }
     *
     * @see enableBatchSynthesis - Master switch
     * @see synthesisBatchSize - Batch size setting
     */
    /**************************************************************/
    function isBatchSynthesisEnabled() {
        return config.enableBatchSynthesis && config.synthesisBatchSize > 0;
    }

    /**************************************************************/
    /**
     * Determines if detailed progress display should be used.
     *
     * @returns {boolean} True if detailed progress is enabled
     *
     * @example
     * if (ProgressiveConfig.isDetailedProgressEnabled()) {
     *     showProductName(result);
     * } else {
     *     showGenericProgress();
     * }
     *
     * @see enableDetailedProgress - Master switch
     */
    /**************************************************************/
    function isDetailedProgressEnabled() {
        return config.enableDetailedProgress;
    }

    /**************************************************************/
    /**
     * Gets the checkpoint threshold value.
     *
     * @returns {number} Number of products that triggers checkpoint
     *
     * @see CheckpointManager.shouldShowCheckpoint - Uses this threshold
     */
    /**************************************************************/
    function getCheckpointThreshold() {
        return config.checkpointThreshold;
    }

    /**************************************************************/
    /**
     * Gets the batch size for synthesis operations.
     *
     * @returns {number} Number of products per synthesis batch
     *
     * @see BatchSynthesizer.synthesizeInBatches - Uses this value
     */
    /**************************************************************/
    function getSynthesisBatchSize() {
        return config.synthesisBatchSize;
    }

    /**************************************************************/
    /**
     * Gets the minimum batch size for synthesis.
     *
     * @returns {number} Minimum products to form a batch
     *
     * @see BatchSynthesizer.combineBatches - Uses this value
     */
    /**************************************************************/
    function getMinBatchSize() {
        return config.minBatchSize;
    }

    /**************************************************************/
    /**
     * Gets the maximum number of progress items to display.
     *
     * @returns {number} Max items in progress list
     *
     * @see CheckpointRenderer.renderProgressDetails - Uses this value
     */
    /**************************************************************/
    function getMaxProgressItems() {
        return config.maxProgressItems;
    }

    /**************************************************************/
    /**
     * Checks if estimated time display is enabled.
     *
     * @returns {boolean} True if time estimates should be shown
     */
    /**************************************************************/
    function shouldShowEstimatedTime() {
        return config.showEstimatedTime;
    }

    /**************************************************************/
    /**
     * Gets animation duration for progress items.
     *
     * @returns {number} Duration in milliseconds
     */
    /**************************************************************/
    function getProgressAnimationDuration() {
        return config.progressAnimationDuration;
    }

    /**************************************************************/
    /**
     * Gets animation duration for checkpoint panel.
     *
     * @returns {number} Duration in milliseconds
     */
    /**************************************************************/
    function getCheckpointAnimationDuration() {
        return config.checkpointAnimationDuration;
    }

    /**************************************************************/
    /**
     * Updates a configuration value at runtime.
     *
     * @param {string} key - Configuration key to update
     * @param {*} value - New value for the configuration
     * @returns {boolean} True if update was successful
     *
     * @description
     * Allows runtime configuration changes for A/B testing or
     * user preferences. Validates key exists before updating.
     *
     * @example
     * // Disable checkpoints at runtime
     * ProgressiveConfig.setConfig('enableCheckpoints', false);
     *
     * // Adjust threshold
     * ProgressiveConfig.setConfig('checkpointThreshold', 10);
     */
    /**************************************************************/
    function setConfig(key, value) {
        if (config.hasOwnProperty(key)) {
            config[key] = value;
            console.log(`[ProgressiveConfig] Updated ${key} = ${value}`);
            return true;
        }
        console.warn(`[ProgressiveConfig] Unknown config key: ${key}`);
        return false;
    }

    /**************************************************************/
    /**
     * Gets all configuration values (for debugging).
     *
     * @returns {Object} Copy of all configuration values
     */
    /**************************************************************/
    function getAllConfig() {
        return { ...config };
    }

    /**************************************************************/
    /**
     * Public API for the progressive configuration module.
     *
     * @description
     * Exposes configuration values and feature detection methods.
     */
    /**************************************************************/
    return {
        // Feature detection
        isCheckpointEnabled: isCheckpointEnabled,
        isBatchSynthesisEnabled: isBatchSynthesisEnabled,
        isDetailedProgressEnabled: isDetailedProgressEnabled,

        // Configuration getters
        getCheckpointThreshold: getCheckpointThreshold,
        getSynthesisBatchSize: getSynthesisBatchSize,
        getMinBatchSize: getMinBatchSize,
        getMaxProgressItems: getMaxProgressItems,
        shouldShowEstimatedTime: shouldShowEstimatedTime,
        getProgressAnimationDuration: getProgressAnimationDuration,
        getCheckpointAnimationDuration: getCheckpointAnimationDuration,

        // Runtime configuration
        setConfig: setConfig,
        getAllConfig: getAllConfig,

        // Direct access to raw config values (for backwards compatibility)
        get checkpointThreshold() { return config.checkpointThreshold; },
        get enableCheckpoints() { return config.enableCheckpoints; },
        get enableBatchSynthesis() { return config.enableBatchSynthesis; },
        get enableDetailedProgress() { return config.enableDetailedProgress; },
        get synthesisBatchSize() { return config.synthesisBatchSize; },
        get minBatchSize() { return config.minBatchSize; },
        get maxProgressItems() { return config.maxProgressItems; },
        get showEstimatedTime() { return config.showEstimatedTime; }
    };
})();
