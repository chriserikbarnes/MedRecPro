/**************************************************************/
/**
 * Checkpoint Renderer Module
 *
 * @fileoverview UI components for checkpoint panels and progress displays.
 *
 * @description
 * The checkpoint renderer module provides:
 * - Checkpoint panel HTML generation with product checkboxes
 * - Progress details rendering during endpoint execution
 * - Estimated time display (when enabled)
 * - Interactive UI elements for user selection
 *
 * This module generates the HTML for the progressive response features
 * that are displayed within assistant messages.
 *
 * @example
 * import { CheckpointRenderer } from './checkpoint-renderer.js';
 *
 * // Render checkpoint panel
 * const html = CheckpointRenderer.renderCheckpointPanel(productGroups, messageId);
 * container.innerHTML = html;
 *
 * @module chat/checkpoint-renderer
 * @see CheckpointManager - Manages checkpoint state
 * @see ProgressiveConfig - Configuration for rendering
 * @see MessageRenderer - Integrates checkpoint UI into messages
 */
/**************************************************************/

import { ProgressiveConfig } from './progressive-config.js';
import { ResultGrouper } from './result-grouper.js';
import { ChatUtils } from './utils.js';

export const CheckpointRenderer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Renders the main checkpoint panel HTML.
     *
     * @param {Object} productGroups - Grouped results from ResultGrouper
     * @param {string} messageId - Associated assistant message ID
     * @param {Object} [options] - Rendering options
     * @param {Array<string>} [options.selectedIds] - Pre-selected product IDs (all by default)
     * @returns {string} HTML string for the checkpoint panel
     *
     * @description
     * Generates a complete checkpoint panel with:
     * - Header with product count
     * - Scrollable list of product checkboxes
     * - Select All / None buttons
     * - Synthesize and Cancel action buttons
     * - Helpful hint text
     *
     * @example
     * const html = renderCheckpointPanel(groups, 'msg-123');
     * document.querySelector('.message-bubble').innerHTML += html;
     *
     * @see renderSourceItem - Renders individual product items
     * @see CheckpointManager - Manages selection state
     */
    /**************************************************************/
    function renderCheckpointPanel(productGroups, messageId, options = {}) {
        const groupsArray = ResultGrouper.groupsToArray(productGroups);
        const totalCount = groupsArray.length;

        // Default to all selected if not specified
        const selectedIds = options.selectedIds || Object.keys(productGroups);
        const selectedCount = selectedIds.length;

        // Build source items HTML
        const sourceItemsHtml = groupsArray.map((group, index) => {
            const isSelected = selectedIds.includes(group.id);
            return renderSourceItem(group, index, isSelected, messageId);
        }).join('');

        // Build the complete panel
        return `
            <div class="checkpoint-panel" data-message-id="${messageId}">
                <div class="checkpoint-header">
                    <div class="checkpoint-title">
                        <svg class="checkpoint-icon icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                            <polyline points="22 4 12 14.01 9 11.01"></polyline>
                        </svg>
                        <span>Found ${totalCount} data sources</span>
                    </div>
                    <div class="checkpoint-selection-info">
                        <span class="selected-count">${selectedCount}</span> of ${totalCount} selected
                    </div>
                </div>

                <div class="checkpoint-sources">
                    ${sourceItemsHtml}
                </div>

                <div class="checkpoint-selection-controls">
                    <button class="checkpoint-select-btn" onclick="MedRecProChat.checkpointSelectAll('${messageId}')" title="Select all sources">
                        Select All
                    </button>
                    <button class="checkpoint-select-btn" onclick="MedRecProChat.checkpointSelectNone('${messageId}')" title="Deselect all sources">
                        Select None
                    </button>
                </div>

                <div class="checkpoint-actions">
                    <button class="btn btn-primary checkpoint-confirm-btn" onclick="MedRecProChat.checkpointConfirm('${messageId}')" ${selectedCount === 0 ? 'disabled' : ''}>
                        <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <polyline points="20 6 9 17 4 12"></polyline>
                        </svg>
                        Synthesize Selected (${selectedCount})
                    </button>
                    <button class="btn btn-ghost checkpoint-cancel-btn" onclick="MedRecProChat.checkpointCancel('${messageId}')">
                        Cancel
                    </button>
                </div>

                <div class="checkpoint-hint">
                    Uncheck sources you don't need to reduce processing time
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders an individual source checkbox item.
     *
     * @param {Object} group - Product group from ResultGrouper
     * @param {number} index - Index in the groups array
     * @param {boolean} isSelected - Whether this item is currently selected
     * @param {string} messageId - Parent message ID
     * @returns {string} HTML string for the source item
     *
     * @description
     * Renders a single product with:
     * - Checkbox for selection
     * - Product name
     * - Result count badge
     * - Endpoint descriptions (if available)
     *
     * @see renderCheckpointPanel - Uses this for each product
     */
    /**************************************************************/
    function renderSourceItem(group, index, isSelected, messageId) {
        // Apply title case to product name for consistent display
        const titleCaseName = ChatUtils.toTitleCase(group.name);
        const escapedName = ChatUtils.escapeHtml(titleCaseName);
        const resultCount = group.results ? group.results.length : 0;
        const checkedAttr = isSelected ? 'checked' : '';

        // Build description summarizing the data types available
        let descriptionHtml = '';
        if (group.endpointDescriptions && group.endpointDescriptions.length > 0) {
            // Extract key data types from descriptions
            const dataTypes = extractDataTypes(group.endpointDescriptions);
            if (dataTypes.length > 0) {
                const dataTypeStr = dataTypes.join(', ');
                descriptionHtml = `<div class="source-description">${ChatUtils.escapeHtml(dataTypeStr)}</div>`;
            }
        }

        return `
            <label class="source-checkbox" data-product-id="${group.id}">
                <input type="checkbox"
                       ${checkedAttr}
                       onchange="MedRecProChat.checkpointToggle('${messageId}', '${group.id}', this.checked)"
                />
                <span class="source-checkbox-custom"></span>
                <div class="source-info">
                    <span class="source-name">${escapedName}</span>
                    ${descriptionHtml}
                </div>
            </label>
        `;
    }

    /**************************************************************/
    /**
     * Extracts data type keywords from endpoint descriptions.
     *
     * @param {Array<string>} descriptions - Array of endpoint descriptions
     * @returns {Array<string>} Array of extracted data type keywords
     *
     * @description
     * Parses endpoint descriptions to identify what types of data are being
     * fetched (e.g., "Dosing", "Interactions", "Contraindications").
     */
    /**************************************************************/
    function extractDataTypes(descriptions) {
        const typeKeywords = [
            'Dosing', 'Dosage', 'Administration',
            'Interactions', 'Drug Interactions',
            'Contraindications', 'Warnings', 'Precautions',
            'Side Effects', 'Adverse Reactions',
            'Indications', 'Usage',
            'Pharmacology', 'Mechanism',
            'Pregnancy', 'Nursing', 'Lactation',
            'Overdose', 'Overdosage',
            'Label', 'Prescribing Information'
        ];

        const foundTypes = new Set();

        descriptions.forEach(desc => {
            if (!desc) return;
            const lowerDesc = desc.toLowerCase();

            typeKeywords.forEach(keyword => {
                if (lowerDesc.includes(keyword.toLowerCase())) {
                    foundTypes.add(keyword);
                }
            });
        });

        // Return up to 3 types for display
        return Array.from(foundTypes).slice(0, 3);
    }

    /**************************************************************/
    /**
     * Updates the checkpoint panel UI after selection changes.
     *
     * @param {string} messageId - Message ID containing the checkpoint
     * @param {number} selectedCount - New selected count
     * @param {number} totalCount - Total product count
     *
     * @description
     * Updates the panel's visual state:
     * - Selected count display
     * - Confirm button text and disabled state
     *
     * @example
     * // After user toggles a checkbox
     * CheckpointRenderer.updateCheckpointSelection(msgId, 5, 10);
     */
    /**************************************************************/
    function updateCheckpointSelection(messageId, selectedCount, totalCount) {
        const panel = document.querySelector(`.checkpoint-panel[data-message-id="${messageId}"]`);
        if (!panel) return;

        // Update count display
        const countSpan = panel.querySelector('.selected-count');
        if (countSpan) {
            countSpan.textContent = selectedCount;
        }

        // Update confirm button
        const confirmBtn = panel.querySelector('.checkpoint-confirm-btn');
        if (confirmBtn) {
            confirmBtn.disabled = selectedCount === 0;
            confirmBtn.innerHTML = `
                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
                Synthesize Selected (${selectedCount})
            `;
        }
    }

    /**************************************************************/
    /**
     * Updates all checkboxes in the panel to match selection state.
     *
     * @param {string} messageId - Message ID containing the checkpoint
     * @param {Array<string>} selectedIds - Array of selected product IDs
     *
     * @description
     * Used after "Select All" or "Select None" to sync checkbox states.
     */
    /**************************************************************/
    function updateCheckboxStates(messageId, selectedIds) {
        const panel = document.querySelector(`.checkpoint-panel[data-message-id="${messageId}"]`);
        if (!panel) return;

        const checkboxes = panel.querySelectorAll('.source-checkbox');
        checkboxes.forEach(label => {
            const productId = label.dataset.productId;
            const input = label.querySelector('input[type="checkbox"]');
            if (input && productId) {
                input.checked = selectedIds.includes(productId);
            }
        });
    }

    /**************************************************************/
    /**
     * Renders the progress details list during endpoint execution.
     *
     * @param {Array} completedItems - Array of completed item info objects
     * @param {Object} [options] - Rendering options
     * @param {number} [options.current] - Current item index (1-based)
     * @param {number} [options.total] - Total items to process
     * @param {string} [options.currentName] - Name of currently processing item
     * @returns {string} HTML string for progress details
     *
     * @description
     * Displays a list of completed items with checkmarks and the
     * currently processing item with a spinner. Shows newest items
     * at the top, limited by maxProgressItems config.
     *
     * @example
     * const html = renderProgressDetails(
     *     [{ name: 'Lisinopril', success: true }],
     *     { current: 2, total: 10, currentName: 'Metformin' }
     * );
     *
     * @see ProgressiveConfig.getMaxProgressItems - Max items to show
     */
    /**************************************************************/
    function renderProgressDetails(completedItems, options = {}) {
        const { current, total, currentName } = options;
        const maxItems = ProgressiveConfig.getMaxProgressItems();

        // Build completed items list (newest first, limited)
        const recentItems = completedItems.slice(-maxItems).reverse();

        let itemsHtml = '';

        // Add currently processing item if provided
        if (currentName) {
            itemsHtml += `
                <div class="progress-item progress-item-current">
                    <svg class="progress-item-spinner icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <line x1="12" y1="2" x2="12" y2="6"></line>
                        <line x1="12" y1="18" x2="12" y2="22"></line>
                        <line x1="4.93" y1="4.93" x2="7.76" y2="7.76"></line>
                        <line x1="16.24" y1="16.24" x2="19.07" y2="19.07"></line>
                        <line x1="2" y1="12" x2="6" y2="12"></line>
                        <line x1="18" y1="12" x2="22" y2="12"></line>
                        <line x1="4.93" y1="19.07" x2="7.76" y2="16.24"></line>
                        <line x1="16.24" y1="7.76" x2="19.07" y2="4.93"></line>
                    </svg>
                    <span class="progress-item-name">${ChatUtils.escapeHtml(currentName)}</span>
                </div>
            `;
        }

        // Add completed items
        recentItems.forEach(item => {
            const statusClass = item.success ? 'progress-item-success' : 'progress-item-failed';
            const icon = item.success
                ? '<polyline points="20 6 9 17 4 12"></polyline>'
                : '<line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line>';

            itemsHtml += `
                <div class="progress-item ${statusClass}">
                    <svg class="progress-item-icon icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        ${icon}
                    </svg>
                    <span class="progress-item-name">${ChatUtils.escapeHtml(item.name)}</span>
                </div>
            `;
        });

        // Show hidden count if there are more
        const hiddenCount = completedItems.length - recentItems.length;
        if (hiddenCount > 0) {
            itemsHtml += `
                <div class="progress-item progress-item-more">
                    <span>+ ${hiddenCount} more completed</span>
                </div>
            `;
        }

        // Build progress header
        let headerText = 'Fetching data...';
        if (current !== undefined && total !== undefined) {
            headerText = `Fetching data (${current}/${total})`;
        }

        return `
            <div class="progress-details">
                <div class="progress-details-header">
                    ${headerText}
                </div>
                <div class="progress-details-list">
                    ${itemsHtml}
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders estimated time remaining.
     *
     * @param {number} completed - Number of completed items
     * @param {number} total - Total items to process
     * @param {number} avgTime - Average time per item in milliseconds
     * @returns {string} HTML string for time estimate, or empty if disabled
     *
     * @description
     * Calculates and displays estimated remaining time based on
     * average execution time per item. Only shown if enabled in config.
     *
     * @see ProgressiveConfig.shouldShowEstimatedTime - Feature flag
     */
    /**************************************************************/
    function renderEstimatedTime(completed, total, avgTime) {
        if (!ProgressiveConfig.shouldShowEstimatedTime()) {
            return '';
        }

        if (completed === 0 || avgTime <= 0) {
            return '';
        }

        const remaining = total - completed;
        const estimatedMs = remaining * avgTime;

        // Format time
        let timeText;
        if (estimatedMs < 1000) {
            timeText = 'less than a second';
        } else if (estimatedMs < 60000) {
            const seconds = Math.round(estimatedMs / 1000);
            timeText = `${seconds} second${seconds !== 1 ? 's' : ''}`;
        } else {
            const minutes = Math.round(estimatedMs / 60000);
            timeText = `${minutes} minute${minutes !== 1 ? 's' : ''}`;
        }

        return `
            <div class="progress-estimate">
                Estimated: ${timeText} remaining
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders a batch synthesis progress indicator.
     *
     * @param {number} currentBatch - Current batch number (1-based)
     * @param {number} totalBatches - Total number of batches
     * @param {Array<string>} batchProductNames - Names of products in current batch
     * @returns {string} HTML string for batch progress
     *
     * @description
     * Shows progress during batch synthesis with product names.
     *
     * @see BatchSynthesizer - Uses this during batch processing
     */
    /**************************************************************/
    function renderBatchProgress(currentBatch, totalBatches, batchProductNames) {
        const productList = batchProductNames.map(name =>
            `<span class="batch-product">${ChatUtils.escapeHtml(name)}</span>`
        ).join(', ');

        return `
            <div class="batch-progress">
                <div class="batch-progress-header">
                    Synthesizing batch ${currentBatch} of ${totalBatches}
                </div>
                <div class="batch-progress-products">
                    ${productList}
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders a synthesis complete indicator.
     *
     * @param {number} productCount - Number of products synthesized
     * @returns {string} HTML string for completion indicator
     *
     * @description
     * Shown briefly after batch synthesis completes before
     * final response is displayed.
     */
    /**************************************************************/
    function renderSynthesisComplete(productCount) {
        return `
            <div class="synthesis-complete">
                <svg class="synthesis-complete-icon icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                </svg>
                <span>Synthesized ${productCount} product${productCount !== 1 ? 's' : ''}</span>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders a cancellation message.
     *
     * @param {string} [reason] - Optional reason for cancellation
     * @returns {string} HTML string for cancellation message
     */
    /**************************************************************/
    function renderCancellationMessage(reason) {
        const reasonText = reason ? ` (${ChatUtils.escapeHtml(reason)})` : '';

        return `
            <div class="checkpoint-cancelled">
                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="15" y1="9" x2="9" y2="15"></line>
                    <line x1="9" y1="9" x2="15" y2="15"></line>
                </svg>
                <span>Query cancelled${reasonText}</span>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Creates a simple progress status string with product name.
     *
     * @param {string} productName - Name of product being fetched
     * @param {number} current - Current index (1-based)
     * @param {number} total - Total count
     * @returns {string} Progress status text
     *
     * @description
     * Generates text like "Fetching: Lisinopril (3/12)..."
     * Used for updating message.progressStatus.
     *
     * @example
     * const status = createProgressStatus('Lisinopril', 3, 12);
     * // Returns: "Fetching: Lisinopril (3/12)..."
     */
    /**************************************************************/
    function createProgressStatus(productName, current, total) {
        const truncatedName = productName.length > 30
            ? productName.substring(0, 27) + '...'
            : productName;

        return `Fetching: ${truncatedName} (${current}/${total})...`;
    }

    /**************************************************************/
    /**
     * Public API for the checkpoint renderer module.
     *
     * @description
     * Exposes rendering functions for checkpoint and progress UI.
     */
    /**************************************************************/
    return {
        // Checkpoint panel rendering
        renderCheckpointPanel: renderCheckpointPanel,
        renderSourceItem: renderSourceItem,
        updateCheckpointSelection: updateCheckpointSelection,
        updateCheckboxStates: updateCheckboxStates,

        // Progress rendering
        renderProgressDetails: renderProgressDetails,
        renderEstimatedTime: renderEstimatedTime,
        createProgressStatus: createProgressStatus,

        // Batch synthesis rendering
        renderBatchProgress: renderBatchProgress,
        renderSynthesisComplete: renderSynthesisComplete,

        // Status rendering
        renderCancellationMessage: renderCancellationMessage
    };
})();
