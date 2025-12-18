/**************************************************************/
/**
 * MedRecPro Chat File Handling Module
 *
 * @fileoverview Manages file uploads, including drag-and-drop, validation, and upload processing.
 * Handles ZIP file imports for drug label data with progress tracking.
 *
 * @description
 * The file handler module provides:
 * - File validation (ZIP files only)
 * - Drag-and-drop support
 * - File list rendering and management
 * - File upload with FormData
 * - Progress polling for async import operations
 * - Import result extraction
 *
 * @example
 * import { FileHandler } from './file-handler.js';
 *
 * // Initialize with DOM elements
 * FileHandler.initElements({ fileList, dropArea, fileInput, attachBadge });
 *
 * // Add files programmatically
 * FileHandler.addFiles(fileList);
 *
 * // Upload pending files
 * const result = await FileHandler.uploadFiles();
 *
 * @module chat/file-handler
 * @see ChatState - Stores file list state
 * @see ChatConfig - API configuration for upload endpoint
 * @see ChatUtils - Utility functions for formatting
 */
/**************************************************************/

import { ChatConfig } from './config.js';
import { ChatState } from './state.js';
import { ChatUtils } from './utils.js';

export const FileHandler = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Cached DOM elements for file handling.
     *
     * @type {Object}
     * @property {HTMLElement} fileList - Container for file list display
     * @property {HTMLElement} dropArea - Drag-and-drop target area
     * @property {HTMLElement} fileInput - Hidden file input element
     * @property {HTMLElement} attachBadge - Badge showing file count
     * @property {HTMLElement} fileDropzone - Dropzone container (toggle visibility)
     * @property {HTMLElement} attachBtn - Attachment button (toggle active state)
     *
     * @see initElements - Initializes these references
     */
    /**************************************************************/
    let elements = {
        fileList: null,
        dropArea: null,
        fileInput: null,
        attachBadge: null,
        fileDropzone: null,
        attachBtn: null
    };

    /**************************************************************/
    /**
     * Initializes DOM element references for file handling.
     *
     * @param {Object} domElements - Object containing DOM element references
     *
     * @example
     * FileHandler.initElements({
     *     fileList: document.getElementById('fileList'),
     *     dropArea: document.getElementById('dropArea'),
     *     fileInput: document.getElementById('fileInput'),
     *     attachBadge: document.getElementById('attachBadge'),
     *     fileDropzone: document.getElementById('fileDropzone'),
     *     attachBtn: document.getElementById('attachBtn')
     * });
     *
     * @see MedRecProChat.init - Calls this during startup
     */
    /**************************************************************/
    function initElements(domElements) {
        elements = { ...elements, ...domElements };
    }

    /**************************************************************/
    /**
     * Renders the file list in the dropzone.
     *
     * @description
     * Updates the file list display with current files from state.
     * Shows file names, sizes, and remove buttons.
     * Updates the attachment badge count.
     *
     * @example
     * // After adding files
     * FileHandler.renderFileList();
     *
     * @see ChatState.getFiles - Source of file data
     * @see ChatUtils.formatFileSize - Formats file sizes
     */
    /**************************************************************/
    function renderFileList() {
        const files = ChatState.getFiles();

        if (files.length === 0) {
            // Clear list and hide badge
            elements.fileList.innerHTML = '';
            elements.attachBadge.style.display = 'none';
        } else {
            // Render file items with remove buttons
            elements.fileList.innerHTML = files.map((file, index) => `
                <div class="file-item">
                    <svg class="file-icon icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
                    </svg>
                    <span class="file-name">${ChatUtils.escapeHtml(file.name)}</span>
                    <span class="file-size">(${ChatUtils.formatFileSize(file.size)})</span>
                    <button class="file-remove" onclick="MedRecProChat.removeFile(${index})">
                        <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <line x1="18" y1="6" x2="6" y2="18"></line>
                            <line x1="6" y1="6" x2="18" y2="18"></line>
                        </svg>
                    </button>
                </div>
            `).join('');

            // Show badge with count
            elements.attachBadge.textContent = files.length;
            elements.attachBadge.style.display = 'flex';
        }
    }

    /**************************************************************/
    /**
     * Removes a file from the upload list by index.
     *
     * @param {number} index - Array index of file to remove
     *
     * @description
     * Removes the file from state and re-renders the file list.
     *
     * @example
     * // Called from remove button onclick
     * FileHandler.removeFile(0);
     *
     * @see ChatState.removeFile - Removes from state
     */
    /**************************************************************/
    function removeFile(index) {
        ChatState.removeFile(index);
        renderFileList();
    }

    /**************************************************************/
    /**
     * Adds files to the pending upload list.
     *
     * @param {FileList|Array<File>} files - Files to add (from input or drop)
     *
     * @description
     * Filters to only accept ZIP files based on:
     * - MIME type: application/zip or application/x-zip-compressed
     * - File extension: .zip
     *
     * Non-ZIP files are silently ignored.
     *
     * @example
     * // From file input
     * FileHandler.addFiles(input.files);
     *
     * // From drag and drop
     * FileHandler.addFiles(event.dataTransfer.files);
     *
     * @see ChatState.addFiles - Stores files in state
     */
    /**************************************************************/
    function addFiles(files) {
        // Filter to ZIP files only
        const zipFiles = Array.from(files).filter(f =>
            f.type === 'application/zip' ||
            f.type === 'application/x-zip-compressed' ||
            f.name.endsWith('.zip')
        );

        if (zipFiles.length > 0) {
            ChatState.addFiles(zipFiles);
            renderFileList();
        }
    }

    /**************************************************************/
    /**
     * Toggles the file upload dropzone visibility.
     *
     * @description
     * Shows/hides the dropzone and updates the attach button's active state.
     *
     * @example
     * // Called from attach button click
     * FileHandler.toggleFileUpload();
     *
     * @see ChatState.setShowFileUpload - Updates visibility state
     */
    /**************************************************************/
    function toggleFileUpload() {
        const isShowing = !ChatState.isShowFileUpload();
        ChatState.setShowFileUpload(isShowing);

        elements.fileDropzone.style.display = isShowing ? 'block' : 'none';
        elements.attachBtn.classList.toggle('active', isShowing || ChatState.getFileCount() > 0);
    }

    /**************************************************************/
    /**
     * Hides the file upload dropzone.
     *
     * @description
     * Called after successful upload or when dismissing the dropzone.
     * Button remains active if files are still pending.
     *
     * @example
     * // After upload completes
     * FileHandler.hideFileUpload();
     */
    /**************************************************************/
    function hideFileUpload() {
        ChatState.setShowFileUpload(false);
        elements.fileDropzone.style.display = 'none';
        elements.attachBtn.classList.toggle('active', ChatState.getFileCount() > 0);
    }

    /**************************************************************/
    /**
     * Sets up drag-and-drop event handlers.
     *
     * @description
     * Configures the drop area element with:
     * - Click: Opens file picker
     * - Dragover: Shows drag state styling
     * - Dragleave: Removes drag state styling
     * - Drop: Adds dropped files
     * - Input change: Handles file picker selection
     *
     * @example
     * // Called during initialization
     * FileHandler.setupDragAndDrop();
     *
     * @see initElements - Must be called first
     */
    /**************************************************************/
    function setupDragAndDrop() {
        // Click to open file picker
        elements.dropArea.addEventListener('click', () => {
            elements.fileInput.click();
        });

        // Dragover: show visual feedback
        elements.dropArea.addEventListener('dragover', (e) => {
            e.preventDefault();
            elements.dropArea.classList.add('dragging');
        });

        // Dragleave: remove visual feedback
        elements.dropArea.addEventListener('dragleave', (e) => {
            e.preventDefault();
            elements.dropArea.classList.remove('dragging');
        });

        // Drop: add files
        elements.dropArea.addEventListener('drop', (e) => {
            e.preventDefault();
            elements.dropArea.classList.remove('dragging');
            addFiles(e.dataTransfer.files);
        });

        // File input change: add selected files
        elements.fileInput.addEventListener('change', (e) => {
            addFiles(e.target.files);
            e.target.value = '';  // Reset for re-selection of same file
        });
    }

    /**************************************************************/
    /**
     * Polls the import progress endpoint until operation completes.
     *
     * @param {string} progressUrl - The URL to poll for progress
     * @param {Function} [onProgress] - Callback for progress updates
     * @param {number} [maxWaitMs=300000] - Maximum wait time in milliseconds (default 5 minutes for Azure)
     * @returns {Promise<Object>} Final import status with results
     *
     * @description
     * Uses exponential backoff starting at pollInterval (1 second).
     * Stops polling when status is "Completed", "Failed", or "Canceled".
     * Extended timeout for Azure production environments where imports may take longer.
     *
     * The onProgress callback receives status updates for UI display:
     * - status.percentComplete: 0-100 progress value
     * - status.status: Current status string
     * - status.progressUrl: URL to manually check progress
     * - status.operationId: Operation ID for reference
     *
     * @example
     * const finalStatus = await pollImportProgress(
     *     '/api/Label/progress/abc-123',
     *     (status) => console.log(`${status.percentComplete}%`)
     * );
     *
     * @see uploadFiles - Uses for async import tracking
     */
    /**************************************************************/
    async function pollImportProgress(progressUrl, onProgress, maxWaitMs = 300000) {
        const startTime = Date.now();
        let pollDelay = ChatConfig.API_CONFIG.pollInterval;
        let consecutiveFailures = 0;
        const maxConsecutiveFailures = 10;
        let lastKnownStatus = null;

        while (Date.now() - startTime < maxWaitMs) {
            try {
                const response = await fetch(
                    ChatConfig.buildUrl(progressUrl),
                    ChatConfig.getFetchOptions({
                        signal: ChatState.getAbortController()?.signal
                    })
                );

                if (!response.ok) {
                    consecutiveFailures++;
                    console.warn('[FileHandler] Progress poll failed:', response.status, `(${consecutiveFailures}/${maxConsecutiveFailures})`);

                    // If we've had too many consecutive failures, return with status info
                    if (consecutiveFailures >= maxConsecutiveFailures) {
                        return {
                            status: 'Unknown',
                            error: 'Unable to retrieve import status after multiple attempts',
                            progressUrl: progressUrl,
                            message: `Import may still be processing. Check progress manually at: ${progressUrl}`,
                            lastKnownStatus: lastKnownStatus
                        };
                    }

                    await new Promise(r => setTimeout(r, pollDelay));
                    // Exponential backoff, capped at 8 seconds for Azure resilience
                    pollDelay = Math.min(pollDelay * 1.5, 8000);
                    continue;
                }

                // Reset failure counter on successful response
                consecutiveFailures = 0;

                const status = await response.json();
                lastKnownStatus = status;

                // Build file count display string for progress reporting
                const fileProgress = (status.totalFiles > 0 && status.currentFile > 0)
                    ? `${status.currentFile} of ${status.totalFiles} files`
                    : (status.percentComplete !== undefined ? `${status.percentComplete}%` : '');

                console.log('[FileHandler] Import progress:', fileProgress, status.status);

                // Ensure progressUrl is included in the status
                if (!status.progressUrl) {
                    status.progressUrl = progressUrl;
                }

                // Add formatted file progress string for UI display
                status.fileProgress = fileProgress;

                // Call progress callback for UI updates
                if (onProgress) {
                    onProgress(status);
                }

                // Check for terminal states
                if (status.status === 'Completed' ||
                    status.status === 'Failed' ||
                    status.status === 'Canceled') {
                    return status;
                }

                await new Promise(r => setTimeout(r, pollDelay));

            } catch (error) {
                // Re-throw abort errors to allow cancellation
                if (error.name === 'AbortError') throw error;

                consecutiveFailures++;
                console.warn('[FileHandler] Progress poll error:', error, `(${consecutiveFailures}/${maxConsecutiveFailures})`);

                if (consecutiveFailures >= maxConsecutiveFailures) {
                    return {
                        status: 'Unknown',
                        error: error.message || 'Connection error during polling',
                        progressUrl: progressUrl,
                        message: `Import may still be processing. Check progress manually at: ${progressUrl}`,
                        lastKnownStatus: lastKnownStatus
                    };
                }

                await new Promise(r => setTimeout(r, pollDelay));
            }
        }

        // Timeout reached - return informative status with progress URL
        return {
            status: 'Timeout',
            error: 'Import operation is taking longer than expected',
            progressUrl: progressUrl,
            message: `Import is still processing. You can check progress at: ${progressUrl}`,
            lastKnownStatus: lastKnownStatus
        };
    }

    /**************************************************************/
    /**
     * Extracts document information from completed import response.
     *
     * @param {Object} finalStatus - The completed import status with results
     * @returns {Object} Extracted information:
     *   - documentIds: Array of SPL GUIDs
     *   - documentNames: Array of file names
     *   - statistics: Aggregated creation counts
     *   - totalFilesProcessed: Total files attempted
     *   - totalFilesSucceeded: Total files succeeded
     *
     * @description
     * Navigates the nested import response structure:
     * results[].fileResults[] to find splGUID values.
     * Aggregates statistics across all imported files.
     *
     * @example
     * const extracted = extractImportResults(finalStatus);
     * console.log(`Imported ${extracted.documentIds.length} documents`);
     *
     * @see uploadFiles - Uses for result processing
     */
    /**************************************************************/
    function extractImportResults(finalStatus) {
        const documentIds = [];
        const documentNames = [];
        const statistics = {
            documentsCreated: 0,
            organizationsCreated: 0,
            productsCreated: 0,
            sectionsCreated: 0,
            ingredientsCreated: 0,
            productElementsCreated: 0
        };
        let totalFilesProcessed = 0;
        let totalFilesSucceeded = 0;

        // Iterate through ZIP file results
        if (finalStatus.results && Array.isArray(finalStatus.results)) {
            for (const zipResult of finalStatus.results) {
                totalFilesProcessed += zipResult.totalFilesProcessed || 0;
                totalFilesSucceeded += zipResult.totalFilesSucceeded || 0;

                // Iterate through individual file results within each ZIP
                if (zipResult.fileResults && Array.isArray(zipResult.fileResults)) {
                    for (const fileResult of zipResult.fileResults) {
                        // Extract document ID (splGUID is the correct field name)
                        if (fileResult.success && fileResult.splGUID) {
                            documentIds.push(fileResult.splGUID);
                            documentNames.push(fileResult.fileName || fileResult.splGUID);
                        }

                        // Aggregate statistics
                        if (fileResult.documentsCreated) {
                            statistics.documentsCreated += fileResult.documentsCreated;
                        }
                        if (fileResult.organizationsCreated) {
                            statistics.organizationsCreated += fileResult.organizationsCreated;
                        }
                        if (fileResult.productsCreated) {
                            statistics.productsCreated += fileResult.productsCreated;
                        }
                        if (fileResult.sectionsCreated) {
                            statistics.sectionsCreated += fileResult.sectionsCreated;
                        }
                        if (fileResult.ingredientsCreated) {
                            statistics.ingredientsCreated += fileResult.ingredientsCreated;
                        }
                        if (fileResult.productElementsCreated) {
                            statistics.productElementsCreated += fileResult.productElementsCreated;
                        }
                    }
                }
            }
        }

        console.log('[FileHandler] Extracted import results:', {
            documentIds,
            documentNames,
            statistics,
            totalFilesProcessed,
            totalFilesSucceeded
        });

        return {
            documentIds,
            documentNames,
            statistics,
            totalFilesProcessed,
            totalFilesSucceeded
        };
    }

    /**************************************************************/
    /**
     * Uploads files to the server and waits for import completion.
     *
     * @returns {Promise<Object>} Import result object:
     *   - success: boolean indicating success
     *   - documentIds: Array of imported document GUIDs
     *   - documentNames: Array of file names
     *   - statistics: Import statistics
     *   - message: Human-readable result message
     *
     * @description
     * Complete upload flow:
     * 1. Validates files are pending
     * 2. Creates FormData with files
     * 3. POSTs to upload endpoint
     * 4. Polls progress endpoint for async completion
     * 5. Extracts and returns results
     *
     * Uses the currentProgressCallback from state for UI updates.
     *
     * @example
     * const result = await FileHandler.uploadFiles();
     * if (result.success) {
     *     console.log(`Imported: ${result.documentIds.join(', ')}`);
     * }
     *
     * @see ChatConfig.API_CONFIG.endpoints.upload - Upload endpoint path
     * @see pollImportProgress - Progress polling
     * @see extractImportResults - Result extraction
     */
    /**************************************************************/
    async function uploadFiles() {
        const files = ChatState.getFiles();

        // Validate files exist
        if (files.length === 0) {
            return {
                success: false,
                documentIds: [],
                message: 'No files to upload'
            };
        }

        // Build FormData with files
        const formData = new FormData();
        files.forEach(file => formData.append('files', file));

        const uploadUrl = ChatConfig.buildUrl(ChatConfig.API_CONFIG.endpoints.upload);
        console.log('[FileHandler] Uploading files to:', uploadUrl);

        // POST files
        const response = await fetch(uploadUrl, ChatConfig.getFetchOptions({
            method: 'POST',
            body: formData,
            signal: ChatState.getAbortController()?.signal
        }));

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`File upload failed: ${response.status} - ${errorText}`);
        }

        const result = await response.json();
        console.log('[FileHandler] Upload response:', result);

        // Handle async import response (returns OperationId and ProgressUrl)
        if (result.operationId && result.progressUrl) {
            console.log('[FileHandler] Import operation queued:', result.operationId);

            // Poll for completion with progress callback from state
            const progressCallback = ChatState.getProgressCallback();
            const finalStatus = await pollImportProgress(result.progressUrl, progressCallback);

            if (finalStatus.status === 'Completed' && finalStatus.results) {
                // Extract document IDs from nested response structure
                const extracted = extractImportResults(finalStatus);

                return {
                    success: true,
                    documentIds: extracted.documentIds,
                    documentNames: extracted.documentNames,
                    statistics: extracted.statistics,
                    totalFilesProcessed: extracted.totalFilesProcessed,
                    totalFilesSucceeded: extracted.totalFilesSucceeded,
                    results: finalStatus.results,
                    operationId: result.operationId,
                    progressUrl: finalStatus.progressUrl || result.progressUrl,
                    message: `Successfully imported ${extracted.documentIds.length} document(s)`
                };
            } else if (finalStatus.status === 'Failed') {
                return {
                    success: false,
                    documentIds: [],
                    error: finalStatus.error || 'Import failed',
                    operationId: result.operationId,
                    progressUrl: finalStatus.progressUrl || result.progressUrl,
                    message: `Import failed: ${finalStatus.error}`
                };
            } else if (finalStatus.status === 'Canceled') {
                return {
                    success: false,
                    documentIds: [],
                    operationId: result.operationId,
                    progressUrl: finalStatus.progressUrl || result.progressUrl,
                    message: 'Import was canceled'
                };
            } else if (finalStatus.status === 'Timeout' || finalStatus.status === 'Unknown') {
                // Handle timeout/unknown states with helpful information
                return {
                    success: false,
                    documentIds: [],
                    operationId: result.operationId,
                    progressUrl: finalStatus.progressUrl || result.progressUrl,
                    error: finalStatus.error,
                    message: finalStatus.message || 'Import is still processing. Check progress later.',
                    lastKnownStatus: finalStatus.lastKnownStatus
                };
            } else {
                return {
                    success: false,
                    documentIds: [],
                    operationId: result.operationId,
                    progressUrl: finalStatus.progressUrl || result.progressUrl,
                    message: 'Import is still processing. Check progress later.'
                };
            }
        }

        // Fallback for legacy response format (direct fileIds)
        if (result.fileIds) {
            return {
                success: true,
                documentIds: result.fileIds,
                message: `Files uploaded: ${result.fileIds.length}`
            };
        }

        // Unexpected response format
        console.warn('[FileHandler] Unexpected upload response format:', result);
        return {
            success: false,
            documentIds: [],
            rawResponse: result,
            message: 'Unexpected response from server'
        };
    }

    /**************************************************************/
    /**
     * Public API for the file handling module.
     *
     * @description
     * Exposes file management functions and upload capabilities.
     */
    /**************************************************************/
    return {
        // Initialization
        initElements: initElements,
        setupDragAndDrop: setupDragAndDrop,

        // File list management
        renderFileList: renderFileList,
        addFiles: addFiles,
        removeFile: removeFile,

        // Dropzone visibility
        toggleFileUpload: toggleFileUpload,
        hideFileUpload: hideFileUpload,

        // Upload operations
        uploadFiles: uploadFiles,
        extractImportResults: extractImportResults
    };
})();
