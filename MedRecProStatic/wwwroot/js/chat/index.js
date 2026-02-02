/**************************************************************/
/**
 * MedRecPro Chat Main Orchestrator
 *
 * @fileoverview Main entry point that orchestrates all chat interface modules.
 * Initializes components, wires up dependencies, and exposes the global API.
 *
 * @description
 * The main orchestrator:
 * - Imports and initializes all modules
 * - Wires up inter-module dependencies
 * - Exposes global MedRecProChat object for HTML event handlers
 * - Coordinates the message send/receive flow
 * - Handles initialization and startup
 *
 * ## Module Architecture
 *
 * ```
 * index.js (Orchestrator)
 *     |
 *     +-- config.js         - API configuration, environment detection
 *     +-- state.js          - Centralized application state
 *     +-- utils.js          - Pure utility functions
 *     +-- markdown.js       - Markdown to HTML rendering
 *     +-- message-renderer.js - Message display components
 *     +-- file-handler.js   - File upload and management
 *     +-- api-service.js    - API communication layer
 *     +-- endpoint-executor.js - Multi-step endpoint execution
 *     +-- ui-helpers.js     - UI state and event handling
 * ```
 *
 * ## Message Flow
 *
 * 1. User types message and clicks Send (or presses Enter)
 * 2. UIHelpers triggers sendMessage callback
 * 3. Orchestrator creates user/assistant message placeholders
 * 4. If files attached: FileHandler uploads and tracks progress
 * 5. ApiService interprets message to get API endpoints
 * 6. EndpointExecutor executes endpoints with dependencies
 * 7. ApiService synthesizes results into natural language
 * 8. MessageRenderer updates display with response
 *
 * @example
 * <!-- Include in HTML -->
 * <script type="module" src="/js/chat/index.js"></script>
 *
 * <!-- Event handlers reference global object -->
 * <button onclick="MedRecProChat.copyMessage('msg-id')">Copy</button>
 *
 * @module chat/index
 * @see config - API configuration
 * @see state - Application state
 * @see api-service - API communication
 * @see endpoint-executor - Multi-step execution
 */
/**************************************************************/

import { ChatConfig } from './config.js';
import { ChatState } from './state.js';
import { ChatUtils } from './utils.js';
import { MarkdownRenderer } from './markdown.js';
import { MessageRenderer } from './message-renderer.js';
import { FileHandler } from './file-handler.js';
import { ApiService } from './api-service.js';
import { EndpointExecutor } from './endpoint-executor.js';
import { UIHelpers } from './ui-helpers.js';
import { SettingsRenderer } from './settings-renderer.js';
import { EndpointStatsRenderer } from './endpoint-stats-renderer.js';

// Progressive response modules
import { ProgressiveConfig } from './progressive-config.js';
import { ResultGrouper } from './result-grouper.js';
import { CheckpointManager } from './checkpoint-manager.js';
import { CheckpointRenderer } from './checkpoint-renderer.js';
import { BatchSynthesizer } from './batch-synthesizer.js';

/**************************************************************/
/**
 * MedRecPro Chat Application
 *
 * @description
 * Main application object that orchestrates all modules and provides
 * the global API for HTML event handlers.
 */
/**************************************************************/
const MedRecProChat = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Cached DOM element references.
     *
     * @type {Object}
     * @description Populated during init() from getElementById calls.
     */
    /**************************************************************/
    let elements = {};

    /**************************************************************/
    /**
     * Initializes all DOM element references.
     *
     * @description
     * Queries the DOM for all required elements and stores references.
     * Must be called after DOM is ready.
     *
     * @see init - Calls this during startup
     */
    /**************************************************************/
    function initDOMElements() {
        elements = {
            contextBanner: document.getElementById('contextBanner'),
            emptyState: document.getElementById('emptyState'),
            messagesWrapper: document.getElementById('messagesWrapper'),
            messagesContainer: document.getElementById('messagesContainer'),
            messageInput: document.getElementById('messageInput'),
            sendBtn: document.getElementById('sendBtn'),
            cancelBtn: document.getElementById('cancelBtn'),
            clearBtn: document.getElementById('clearBtn'),
            attachBtn: document.getElementById('attachBtn'),
            attachBadge: document.getElementById('attachBadge'),
            fileDropzone: document.getElementById('fileDropzone'),
            fileList: document.getElementById('fileList'),
            dropArea: document.getElementById('dropArea'),
            fileInput: document.getElementById('fileInput')
        };
    }

    /**************************************************************/
    /**
     * Initializes all modules with their required DOM elements.
     *
     * @description
     * Passes element references to each module that needs them.
     * Must be called after initDOMElements().
     *
     * @see init - Calls this during startup
     */
    /**************************************************************/
    function initModules() {
        // Initialize MessageRenderer with display containers
        MessageRenderer.initElements({
            messagesWrapper: elements.messagesWrapper,
            messagesContainer: elements.messagesContainer,
            emptyState: elements.emptyState
        });

        // Initialize FileHandler with file UI elements
        FileHandler.initElements({
            fileList: elements.fileList,
            dropArea: elements.dropArea,
            fileInput: elements.fileInput,
            attachBadge: elements.attachBadge,
            fileDropzone: elements.fileDropzone,
            attachBtn: elements.attachBtn
        });

        // Initialize UIHelpers with control elements
        UIHelpers.initElements({
            messageInput: elements.messageInput,
            sendBtn: elements.sendBtn,
            cancelBtn: elements.cancelBtn,
            clearBtn: elements.clearBtn,
            attachBtn: elements.attachBtn,
            contextBanner: elements.contextBanner
        });

        // Wire up send message callback
        UIHelpers.setOnSendMessage(sendMessage);

        // Setup event listeners
        UIHelpers.setupEventListeners();
        FileHandler.setupDragAndDrop();
    }

    /**************************************************************/
    /**
     * Sends a message through the complete processing pipeline.
     *
     * @description
     * Complete message flow:
     * 1. Validates input (must have text or files)
     * 2. Creates user message with content and file references
     * 3. Creates assistant placeholder message
     * 4. Updates state and renders messages
     * 5. If files: uploads with progress tracking
     * 6. Builds conversation history for context
     * 7. Interprets message to get API endpoints
     * 8. Executes endpoints with dependency resolution
     * 9. Handles retry logic for failed endpoints
     * 10. Synthesizes results into natural language
     * 11. Appends follow-ups and data references
     * 12. Updates final message state
     *
     * @async
     * @returns {Promise<void>}
     *
     * @see ApiService.interpretMessage - Step 7
     * @see EndpointExecutor.executeEndpointsWithDependencies - Step 8
     * @see ApiService.synthesizeResults - Step 10
     */
    /**************************************************************/
    async function sendMessage() {
        const input = UIHelpers.getInputValue();
        const files = ChatState.getFiles();

        // Validate: must have text or files
        if (!input && files.length === 0) return;
        if (ChatState.isLoading()) return;

        // Create user message
        const userMessage = {
            id: ChatUtils.generateUUID(),
            role: 'user',
            content: input,
            files: files.map(f => ({ name: f.name, size: f.size })),
            timestamp: new Date()
        };

        // Create assistant placeholder
        const assistantMessage = {
            id: ChatUtils.generateUUID(),
            role: 'assistant',
            content: '',
            thinking: '',
            isStreaming: true,
            timestamp: new Date()
        };

        // Update state
        ChatState.addMessage(userMessage);
        ChatState.addMessage(assistantMessage);

        // Clear input and update UI
        UIHelpers.clearInput();
        ChatState.setLoading(true);
        UIHelpers.updateUI();
        MessageRenderer.renderMessages();

        // Create abort controller for cancellation
        ChatState.setAbortController(new AbortController());

        try {
            // Handle file upload if files attached
            let importResult = null;
            if (files.length > 0) {
                // Show upload progress
                ChatState.updateMessage(assistantMessage.id, {
                    progress: 0.05,
                    progressStatus: 'Uploading files...'
                });
                MessageRenderer.updateMessage(assistantMessage.id);

                // Set up progress callback
                ChatState.setProgressCallback((status) => {
                    const percent = status.percentComplete || 0;
                    ChatState.updateMessage(assistantMessage.id, {
                        progress: percent / 100,
                        progressStatus: status.status || 'Processing...'
                    });
                    MessageRenderer.updateMessage(assistantMessage.id);
                });

                // Upload files
                importResult = await FileHandler.uploadFiles();

                // Clear progress callback and files
                ChatState.setProgressCallback(null);
                ChatState.clearFiles();
                FileHandler.renderFileList();
                FileHandler.hideFileUpload();

                // Update status based on result
                if (importResult.success && importResult.documentIds.length > 0) {
                    ChatState.updateMessage(assistantMessage.id, {
                        progress: 1.0,
                        progressStatus: `Import complete: ${importResult.documentIds.length} document(s)`
                    });
                } else if (importResult.success) {
                    ChatState.updateMessage(assistantMessage.id, {
                        progressStatus: 'Import completed (no new documents)'
                    });
                } else {
                    ChatState.updateMessage(assistantMessage.id, {
                        progressStatus: importResult.message || 'Import issue'
                    });
                }
                MessageRenderer.updateMessage(assistantMessage.id);
            }

            // Build conversation history for context
            const allMessages = ChatState.getMessages();
            const conversationHistory = allMessages
                .filter(m => m.id !== assistantMessage.id)
                .slice(-10)
                .map(m => ({ role: m.role, content: m.content }));

            // Enhance user message with import context if applicable
            let enhancedUserMessage = input;
            if (importResult) {
                enhancedUserMessage = buildEnhancedMessage(input, importResult);
            }

            // Interpret the message
            const interpretation = await ApiService.interpretMessage(
                enhancedUserMessage,
                conversationHistory,
                importResult
            );

            // Update thinking if provided
            if (interpretation.thinking) {
                ChatState.updateMessage(assistantMessage.id, {
                    thinking: interpretation.thinking
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            }

            // Handle direct response (no API calls needed)
            if (interpretation.directResponse) {
                const extracted = ApiService.extractDirectResponseContent(interpretation.directResponse);
                let responseContent = extracted.content;

                // If extraction didn't include dataReferences and we have import results,
                // add hyperlinks to view imported documents from client-side import result
                if (!extracted.hasDataReferences && importResult && importResult.documentIds && importResult.documentIds.length > 0) {
                    responseContent += '\n\n**View Full Labels:**\n';
                    importResult.documentIds.forEach(docGuid => {
                        const shortGuid = docGuid.substring(0, 8);
                        responseContent += `- [View Imported Label (${shortGuid}...)](${ChatConfig.buildUrl(`/api/Label/original/${docGuid}/true`)})\n`;
                    });

                    // Add import progress link if operationId is available
                    if (importResult.operationId) {
                        responseContent += `- [Check Import Progress](${ChatConfig.buildUrl(`/api/Label/import/progress/${importResult.operationId}`)})\n`;
                    }
                }

                ChatState.updateMessage(assistantMessage.id, {
                    content: responseContent,
                    isStreaming: false,
                    progress: undefined,
                    progressStatus: undefined
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            }
            // Handle API endpoint execution
            else if (interpretation.suggestedEndpoints && interpretation.suggestedEndpoints.length > 0) {
                await executeAndSynthesizeEndpoints(
                    input,
                    interpretation,
                    assistantMessage
                );
            }
            // Fallback for unrecognized response
            else {
                // Extract content from directResponse (handles both string and object formats)
                let fallbackContent;
                if (interpretation.directResponse) {
                    const extracted = ApiService.extractDirectResponseContent(interpretation.directResponse);
                    fallbackContent = extracted.content;
                } else {
                    fallbackContent = interpretation.explanation || 'I understood your request but couldn\'t process it. Please try rephrasing.';
                }

                ChatState.updateMessage(assistantMessage.id, {
                    content: fallbackContent,
                    isStreaming: false,
                    progress: undefined,
                    progressStatus: undefined
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            }

        } catch (error) {
            // Handle errors
            if (error.name === 'AbortError') {
                ChatState.updateMessage(assistantMessage.id, {
                    error: 'Request cancelled',
                    isStreaming: false
                });
            } else {
                let errorMessage = error.message || 'An error occurred';

                // Provide helpful CORS guidance for local dev
                if (ChatConfig.isLocalDevelopment() && error.message.includes('Failed to fetch')) {
                    errorMessage = 'CORS error: The API server may not be configured to accept requests from localhost. Check browser console for details.';
                }

                ChatState.updateMessage(assistantMessage.id, {
                    error: errorMessage,
                    isStreaming: false
                });
            }
            MessageRenderer.updateMessage(assistantMessage.id);

        } finally {
            // Reset loading state
            ChatState.setLoading(false);
            UIHelpers.updateUI();
        }
    }

    /**************************************************************/
    /**
     * Builds an enhanced user message with import context.
     *
     * @param {string} originalInput - Original user input
     * @param {Object} importResult - Import result object
     * @returns {string} Enhanced message with import context
     *
     * @description
     * Prepends import information to the user message so the AI
     * can acknowledge and reference the imported documents.
     *
     * @see sendMessage - Uses for import context
     */
    /**************************************************************/
    function buildEnhancedMessage(originalInput, importResult) {
        if (importResult.success && importResult.documentIds.length > 0) {
            // Build statistics summary
            const stats = importResult.statistics || {};
            const statsText = Object.entries(stats)
                .filter(([k, v]) => v > 0)
                .map(([k, v]) => `${k}: ${v}`)
                .join(', ');

            const defaultRequest = 'Please acknowledge the successful import and provide information about the imported documents.';

            // Include operationId if available for progress tracking
            const operationIdPart = importResult.operationId ? ` operationId: ${importResult.operationId}.` : '';

            return `[IMPORT COMPLETED SUCCESSFULLY: Imported ${importResult.documentIds.length} document(s). Document GUIDs: ${importResult.documentIds.join(', ')}.${operationIdPart} Statistics: ${statsText || 'N/A'}]\n\nUser request: ${originalInput || defaultRequest}`;
        } else if (importResult.success) {
            return `[IMPORT COMPLETED: Files were processed but no new documents were created. ${importResult.message}]\n\nUser request: ${originalInput}`;
        } else {
            // Include operationId for failed/in-progress imports so user can check progress
            const operationIdPart = importResult.operationId ? ` operationId: ${importResult.operationId}.` : '';
            return `[IMPORT ISSUE:${operationIdPart} ${importResult.message}]\n\nUser request: ${originalInput}`;
        }
    }

    /**************************************************************/
    /**
     * Sanitizes API URLs in AI-generated response content.
     *
     * @param {string} content - The response content to sanitize
     * @returns {string} Content with absolute localhost URLs converted to relative URLs
     *
     * @description
     * Fixes URLs that Claude may incorrectly generate with absolute localhost paths.
     * Despite prompt instructions to use relative URLs, Claude sometimes generates
     * URLs like `http://localhost:5001/api/...` or `http://localhost:5093/api/...`.
     * This function converts them to relative URLs starting with `/api/...` so the
     * frontend can add the correct base URL via ChatConfig.buildUrl().
     *
     * @example
     * sanitizeApiUrls('View [label](http://localhost:5001/api/Label/original/abc/true)')
     * // Returns: 'View [label](/api/Label/original/abc/true)'
     *
     * @see executeAndSynthesizeEndpoints - Uses this to clean synthesis responses
     */
    /**************************************************************/
    function sanitizeApiUrls(content) {
        if (!content) return content;

        // Replace absolute localhost URLs with relative URLs
        // Handles http://localhost:PORT/api/... patterns
        // Common ports: 5001 (old), 5093 (current local), and any other localhost port
        return content.replace(
            /https?:\/\/localhost(?::\d+)?(?=\/api\/)/gi,
            ''
        );
    }

    /**************************************************************/
    /**
     * Strips "View Full Labels:" sections from content and extracts the links.
     *
     * @param {string} content - Content potentially containing label link sections
     * @returns {Object} Object with stripped content and extracted links
     * @returns {string} returns.content - Content with label sections removed
     * @returns {Array<Object>} returns.links - Array of extracted link objects
     *
     * @description
     * When batch synthesis is used, each batch response may contain its own
     * "View Full Labels:" section with links. This function extracts those
     * links so they can be aggregated and displayed once at the end of
     * the complete response, preventing scattered/disconnected label links.
     *
     * @example
     * const { content, links } = stripAndExtractLabelLinks(batchResponse);
     * // content: "Dosing info...\n\nMore info..."
     * // links: [{ name: 'Lisinopril', url: '/api/Label/...' }]
     *
     * @see synthesizeAfterCheckpoint - Uses this for batch processing
     */
    /**************************************************************/
    function stripAndExtractLabelLinks(content) {
        if (!content) return { content: '', links: [] };

        const links = [];

        // Pattern to match "View Full Labels:" or "View Full Label:" sections
        // Captures the entire section including all bullet points until next section or end
        const viewLabelsPattern = /\n*(?:\*\*)?View Full Labels?:?(?:\*\*)?\n((?:[-*•]\s*\[.+?\]\(.+?\)\n?)+)/gi;

        // Extract links from matched sections
        let strippedContent = content.replace(viewLabelsPattern, (match, linkSection) => {
            // Parse individual links from the section
            const linkPattern = /[-*•]\s*\[(?:View Full Label\s*\(?)?([^\]\)]+?)(?:\))?\]\(([^)]+)\)/gi;
            let linkMatch;

            while ((linkMatch = linkPattern.exec(linkSection)) !== null) {
                const name = linkMatch[1].trim();
                const url = linkMatch[2].trim();

                // Clean up name - remove "View Full Label" prefix if present
                const cleanName = name.replace(/^View Full Label\s*\(?/i, '').replace(/\)?\s*$/, '');

                links.push({
                    name: cleanName || 'Unknown Product',
                    url: url
                });
            }

            return ''; // Remove the section from content
        });

        // Clean up any resulting double newlines
        strippedContent = strippedContent.replace(/\n{3,}/g, '\n\n').trim();

        return { content: strippedContent, links };
    }

    /**************************************************************/
    /**
     * Executes endpoints and synthesizes results into a response.
     *
     * @param {string} originalInput - User's original message
     * @param {Object} interpretation - Interpretation with suggested endpoints
     * @param {Object} assistantMessage - Assistant message to update
     *
     * @description
     * Handles the endpoint execution flow:
     * 1. Shows progress indicator with product names
     * 2. Executes endpoints with dependencies and progress callbacks
     * 3. Checks for checkpoint threshold (5+ products)
     * 4. Shows checkpoint UI for user selection if threshold met
     * 5. Handles retries if all endpoints fail
     * 6. Synthesizes results into natural language (batch or single)
     * 7. Appends follow-ups and data references
     * 8. Updates final message state
     *
     * @async
     * @see EndpointExecutor.executeEndpointsWithDependencies
     * @see ApiService.synthesizeResults
     * @see ApiService.attemptRetryInterpretation
     * @see CheckpointManager - Manages checkpoint decisions
     * @see BatchSynthesizer - Handles batch synthesis
     */
    /**************************************************************/
    async function executeAndSynthesizeEndpoints(originalInput, interpretation, assistantMessage) {
        // Clear any previous progress items
        ChatState.clearProgressItems();

        // Show progress
        ChatState.updateMessage(assistantMessage.id, {
            progress: 0,
            progressStatus: 'Discovering available products...',
            progressItems: []
        });
        MessageRenderer.updateMessage(assistantMessage.id);

        // Create progress callback for detailed progress display during discovery phase
        // Uses isDiscovery: true to show "Discovery Phase" instead of product names
        const discoveryProgressCallback = ProgressiveConfig.isDetailedProgressEnabled() ? (progressEvent) => {
            if (progressEvent.type === 'endpoint_start') {
                // Update current item being processed - use discovery-specific status
                ChatState.updateMessage(assistantMessage.id, {
                    currentProductName: progressEvent.productName,
                    progressStatus: CheckpointRenderer.createProgressStatus(
                        progressEvent.productName || 'Processing',
                        progressEvent.current,
                        progressEvent.total,
                        { isDiscovery: true }
                    )
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            } else if (progressEvent.type === 'endpoint_complete') {
                // During discovery, don't show individual product completions
                // Just update that discovery is progressing
                ChatState.updateMessage(assistantMessage.id, {
                    currentProductName: null
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            }
        } : null;

        // Create progress callback for fetching phase (after discovery)
        // Shows actual product names during data fetching
        const fetchingProgressCallback = ProgressiveConfig.isDetailedProgressEnabled() ? (progressEvent) => {
            if (progressEvent.type === 'endpoint_start') {
                // Update current item being processed
                ChatState.updateMessage(assistantMessage.id, {
                    currentProductName: progressEvent.productName,
                    progressStatus: CheckpointRenderer.createProgressStatus(
                        progressEvent.productName || 'Processing',
                        progressEvent.current,
                        progressEvent.total
                    )
                });
                MessageRenderer.updateMessage(assistantMessage.id);
            } else if (progressEvent.type === 'endpoint_complete') {
                // Add completed item to progress list
                if (progressEvent.productName) {
                    ChatState.addProgressItem({
                        name: progressEvent.productName,
                        success: progressEvent.success
                    });

                    ChatState.updateMessage(assistantMessage.id, {
                        progressItems: ChatState.getProgressItems(),
                        currentProductName: null
                    });
                    MessageRenderer.updateMessage(assistantMessage.id);
                }
            }
        } : null;

        // ================================================================
        // CHECKPOINT-AWARE EXECUTION FLOW
        // Phase 1: Execute discovery (step 1) to identify available products
        // Phase 2: If many products found, show checkpoint for user selection
        // Phase 3: Execute remaining steps for selected products only
        // ================================================================

        // Phase 1: Discovery - execute only step 1 to find available products
        const discovery = await EndpointExecutor.executeDiscoveryPhase(
            interpretation.suggestedEndpoints,
            assistantMessage,
            ChatState.getAbortController(),
            discoveryProgressCallback
        );

        // Check how many products were discovered
        const discoveredProductCount = Object.keys(discovery.documentGuidToProductName).length;

        // Support both 'documentGuid' (singular) and 'documentGuids' (plural) variable names
        // The skill configuration may use either form
        const documentGuidVar = discovery.extractedVariables.documentGuid ||
            discovery.extractedVariables.documentGuids;
        const hasDocumentGuids = documentGuidVar &&
            (Array.isArray(documentGuidVar)
                ? documentGuidVar.length > 0
                : true);

        // Also check if we have product GUIDs in the mapping (even if not in extractedVariables)
        // This handles single-step discovery endpoints that find products via GUID->Name mapping
        const hasDiscoveredProducts = discoveredProductCount > 0;

        console.log(`[MedRecProChat] Discovery found ${discoveredProductCount} products, hasMoreSteps=${discovery.hasMoreSteps}, hasDocumentGuids=${hasDocumentGuids}, hasDiscoveredProducts=${hasDiscoveredProducts}`);

        // Show checkpoint if:
        // 1. We have more steps to execute AND document GUIDs extracted, OR
        // 2. We discovered many products (even without explicit step 2) that need label fetching
        const shouldShowCheckpoint = discoveredProductCount >= ProgressiveConfig.getCheckpointThreshold() &&
            (discovery.hasMoreSteps && hasDocumentGuids || hasDiscoveredProducts);

        if (shouldShowCheckpoint) {

            console.log('[MedRecProChat] Checkpoint threshold met, showing selection UI BEFORE data fetch');

            // Build product groups from discovered GUIDs (not from fetched data)
            const productGroups = buildProductGroupsFromDiscovery(discovery);

            // Create checkpoint state
            CheckpointManager.createCheckpoint({
                messageId: assistantMessage.id,
                productGroups: productGroups,
                originalQuery: originalInput,
                interpretation: interpretation,
                discoveryResults: discovery.results,
                extractedVariables: discovery.extractedVariables,
                documentGuidToProductName: discovery.documentGuidToProductName
            });

            // Store checkpoint state
            ChatState.setCheckpointState({
                messageId: assistantMessage.id,
                productGroups: productGroups,
                selectedIds: Object.keys(productGroups),
                status: 'pending',
                discoveryData: {
                    extractedVariables: discovery.extractedVariables,
                    documentGuidToProductName: discovery.documentGuidToProductName
                }
            });

            // Render checkpoint UI
            const checkpointHtml = CheckpointRenderer.renderCheckpointPanel(
                productGroups,
                assistantMessage.id
            );

            // Update message with checkpoint UI
            ChatState.updateMessage(assistantMessage.id, {
                progress: undefined,
                progressStatus: undefined,
                progressItems: undefined,
                currentProductName: undefined,
                checkpointUI: checkpointHtml,
                isStreaming: false,
                content: '' // Clear any previous content
            });
            MessageRenderer.updateMessage(assistantMessage.id);

            // Exit early - remaining execution happens when user confirms checkpoint
            return;
        }

        // No checkpoint needed - execute all remaining steps
        let results = [...discovery.results];

        if (discovery.hasMoreSteps) {
            ChatState.updateMessage(assistantMessage.id, {
                progressStatus: 'Fetching product data...'
            });
            MessageRenderer.updateMessage(assistantMessage.id);

            const remainingResults = await EndpointExecutor.executeRemainingSteps(
                interpretation.suggestedEndpoints,
                assistantMessage,
                ChatState.getAbortController(),
                {
                    extractedVariables: discovery.extractedVariables,
                    documentGuidToProductName: discovery.documentGuidToProductName,
                    progressCallback: fetchingProgressCallback
                }
            );
            results.push(...remainingResults);
        } else if (discoveredProductCount > 0 && discoveredProductCount < ProgressiveConfig.getCheckpointThreshold()) {
            // Single-step plan with few products - dynamically fetch labels
            // This handles cases like "tell me about lisinopril" where the AI returns
            // a single-step discovery but we still need to fetch the label content
            const guidsToFetch = Object.keys(discovery.documentGuidToProductName);

            if (guidsToFetch.length > 0) {
                console.log(`[MedRecProChat] Single-step plan with ${guidsToFetch.length} product(s) - fetching labels dynamically`);

                ChatState.updateMessage(assistantMessage.id, {
                    progressStatus: `Fetching label data for ${guidsToFetch.length} product(s)...`
                });
                MessageRenderer.updateMessage(assistantMessage.id);

                const labelResults = await fetchLabelsForGuids(
                    guidsToFetch,
                    discovery.documentGuidToProductName,
                    assistantMessage,
                    ChatState.getAbortController(),
                    fetchingProgressCallback
                );
                results.push(...labelResults);
            }
        }

        // Group results by product (no checkpoint needed)
        const productGroups = ResultGrouper.groupResultsByProduct(results);

        // Check for failures
        const failedResults = results.filter(r => r.statusCode >= 400 || r.error);
        const successfulResults = results.filter(r => r.statusCode >= 200 && r.statusCode < 300 && !r.error);

        // Attempt retry if all endpoints failed
        let finalResults = results;
        if (failedResults.length > 0 && successfulResults.length === 0) {
            console.log('[MedRecProChat] All endpoints failed, attempting retry...');

            const retryResults = await ApiService.attemptRetryInterpretation(
                originalInput,
                interpretation,
                failedResults,
                (status) => {
                    ChatState.updateMessage(assistantMessage.id, {
                        progressStatus: status
                    });
                    MessageRenderer.updateMessage(assistantMessage.id);
                },
                1
            );

            if (retryResults && retryResults.length > 0) {
                const hasSuccessful = retryResults.some(r =>
                    r.statusCode >= 200 && r.statusCode < 300
                );
                if (hasSuccessful) {
                    finalResults = retryResults;
                }
            }
        }

        // Check if this is a settings/logs response that needs special rendering
        const isSettingsData = SettingsRenderer.isSettingsResponse(finalResults);
        // Check if this is an endpoint stats or user activity response
        const isEndpointStatsData = EndpointStatsRenderer.isEndpointStatsResponse(finalResults);

        // Synthesize results
        ChatState.updateMessage(assistantMessage.id, {
            progress: 0.9,
            progressStatus: isSettingsData ? 'Formatting log data...' :
                           isEndpointStatsData ? 'Formatting performance data...' :
                           'Synthesizing results...'
        });
        MessageRenderer.updateMessage(assistantMessage.id);

        // Build final response content
        let responseContent = '';

        if (isEndpointStatsData) {
            // Use specialized endpoint stats renderer for performance/activity data
            responseContent = EndpointStatsRenderer.renderEndpointStatsData(finalResults);

            // If renderer returned empty, fall back to standard synthesis
            if (!responseContent || responseContent.trim() === '') {
                const synthesis = await ApiService.synthesizeResults(
                    originalInput,
                    interpretation,
                    finalResults
                );
                responseContent = synthesis.response || 'Unable to retrieve performance data. Please try again.';
            } else {
                // Add follow-up suggestions from endpoint stats renderer
                const statsFollowUps = EndpointStatsRenderer.getFollowUpSuggestions(finalResults);
                if (statsFollowUps && statsFollowUps.length > 0) {
                    responseContent += '\n\n**Suggested follow-ups:**\n';
                    statsFollowUps.forEach(followUp => {
                        responseContent += `- ${followUp}\n`;
                    });
                }
            }
        } else if (isSettingsData) {
            // Use specialized settings renderer for log data
            responseContent = SettingsRenderer.renderSettingsData(finalResults);

            // If settings renderer returned empty (e.g., all endpoints failed with non-auth errors),
            // fall back to standard synthesis for a helpful message
            if (!responseContent || responseContent.trim() === '') {
                const synthesis = await ApiService.synthesizeResults(
                    originalInput,
                    interpretation,
                    finalResults
                );
                responseContent = synthesis.response || 'Unable to retrieve log data. Please try again.';
            } else {
                // Add follow-up suggestions from settings renderer
                const settingsFollowUps = SettingsRenderer.getLogFollowUpSuggestions(finalResults);
                if (settingsFollowUps && settingsFollowUps.length > 0) {
                    responseContent += '\n\n**Suggested follow-ups:**\n';
                    settingsFollowUps.forEach(followUp => {
                        responseContent += `- ${followUp}\n`;
                    });
                }
            }
        } else {
            // Use standard synthesis for non-settings data
            const synthesis = await ApiService.synthesizeResults(
                originalInput,
                interpretation,
                finalResults
            );

            responseContent = synthesis.response || 'No results found.';

            // Sanitize AI response to fix any absolute localhost URLs to relative URLs
            // Claude may generate localhost:5001 or localhost:5093 URLs despite instructions
            responseContent = sanitizeApiUrls(responseContent);

            // Add follow-up suggestions
            if (synthesis.suggestedFollowUps && synthesis.suggestedFollowUps.length > 0) {
                responseContent += '\n\n**Suggested follow-ups:**\n';
                synthesis.suggestedFollowUps.forEach(followUp => {
                    responseContent += `- ${followUp}\n`;
                });
            }

            // Add document reference links (only if response doesn't already contain "View Full Labels")
            // This prevents duplicate sections when Claude includes labels in its response
            // Check for various formats: bold (**View Full Labels:**), plain text, with/without colon
            const hasViewFullLabels = /(?:\*\*)?View Full Labels?:?(?:\*\*)?/i.test(responseContent);
            if (!hasViewFullLabels && synthesis.dataReferences && Object.keys(synthesis.dataReferences).length > 0) {
                responseContent += '\n\n**View Full Labels:**\n';
                for (const [displayName, url] of Object.entries(synthesis.dataReferences)) {
                    const titleCaseName = ChatUtils.toTitleCase(displayName);
                    responseContent += `- [View Full Label (${titleCaseName})](${ChatConfig.buildUrl(url)})\n`;
                }
            }
        }

        // Add API data source links (applies to both settings and regular responses)
        const sourceLinks = ApiService.formatApiSourceLinks(finalResults);
        if (sourceLinks) {
            responseContent += sourceLinks;
        }

        // Update final message state
        ChatState.updateMessage(assistantMessage.id, {
            content: responseContent,
            progress: undefined,
            progressStatus: undefined,
            isStreaming: false
        });
        MessageRenderer.updateMessage(assistantMessage.id);
    }

    /**************************************************************/
    /**
     * Builds product groups from discovery phase data for checkpoint display.
     *
     * @param {Object} discovery - Discovery phase result
     * @param {Object} discovery.documentGuidToProductName - GUID to name mapping
     * @param {Object} discovery.extractedVariables - Extracted variables including documentGuid array
     * @returns {Object} Product groups suitable for checkpoint display
     *
     * @description
     * Creates minimal product groups (just id/name) from the discovery data
     * before any label data has been fetched. This allows the checkpoint
     * to be shown BEFORE the expensive data fetching step.
     */
    /**************************************************************/
    function buildProductGroupsFromDiscovery(discovery) {
        const groups = {};
        const mapping = discovery.documentGuidToProductName || {};

        // Support both 'documentGuid' (singular) and 'documentGuids' (plural) variable names
        let guids = discovery.extractedVariables?.documentGuid ||
            discovery.extractedVariables?.documentGuids || [];

        // Convert single GUID to array
        let guidArray = Array.isArray(guids) ? guids : (guids ? [guids] : []);

        // If extractedVariables doesn't have the full GUID list, but documentGuidToProductName does,
        // use the mapping keys instead. This handles single-step discovery endpoints.
        if (guidArray.length <= 1 && Object.keys(mapping).length > guidArray.length) {
            console.log('[MedRecProChat] Using documentGuidToProductName keys for product groups');
            guidArray = Object.keys(mapping);
        }

        // Build groups from the GUID -> name mapping
        guidArray.forEach((guid, index) => {
            if (!guid) return;

            // Skip "all" and other meta-values
            if (guid.toLowerCase() === 'all') return;

            const name = mapping[guid] || `Product ${index + 1}`;

            groups[guid] = {
                id: guid,
                name: name,
                results: [], // No results yet - data not fetched
                totalDataSize: 0,
                hasData: false,
                endpointDescriptions: ['Dosage and Administration'] // Placeholder
            };
        });

        // Sort by name
        const sortedGroups = {};
        Object.keys(groups)
            .sort((a, b) => {
                const nameA = (groups[a].name || '').toLowerCase();
                const nameB = (groups[b].name || '').toLowerCase();
                return nameA.localeCompare(nameB);
            })
            .forEach(key => {
                sortedGroups[key] = groups[key];
            });

        console.log(`[MedRecProChat] Built ${Object.keys(sortedGroups).length} product groups from discovery`);

        return sortedGroups;
    }

    /**************************************************************/
    /**
     * Dynamically fetches label content for a list of document GUIDs.
     *
     * @param {Array<string>} guids - Array of document GUIDs to fetch
     * @param {Object} guidToNameMap - Mapping of GUIDs to product names
     * @param {Object} assistantMessage - Message object for progress updates
     * @param {AbortController} abortController - For request cancellation
     * @param {Function} progressCallback - Optional callback for progress updates
     * @returns {Promise<Array>} Array of fetch results
     *
     * @description
     * Used when the checkpoint is shown for single-step discovery endpoints
     * (where there's no step 2 in the plan to fetch labels). This function
     * dynamically generates and executes label fetch API calls for each
     * selected product GUID.
     *
     * @example
     * const results = await fetchLabelsForGuids(
     *     ['guid1', 'guid2'],
     *     { 'guid1': 'Lisinopril', 'guid2': 'Enalapril' },
     *     assistantMessage,
     *     abortController
     * );
     *
     * @see checkpointConfirm - Uses this for single-step plan label fetching
     */
    /**************************************************************/
    async function fetchLabelsForGuids(guids, guidToNameMap, assistantMessage, abortController, progressCallback) {
        const results = [];
        const totalGuids = guids.length;

        console.log(`[MedRecProChat] === DYNAMIC LABEL FETCH: ${totalGuids} GUIDs ===`);

        for (let i = 0; i < totalGuids; i++) {
            const guid = guids[i];
            const productName = guidToNameMap[guid] || `Product ${i + 1}`;

            // Update progress
            if (progressCallback) {
                progressCallback({
                    type: 'endpoint_start',
                    productName: productName,
                    current: i + 1,
                    total: totalGuids
                });
            }

            ChatState.updateMessage(assistantMessage.id, {
                progressStatus: `Fetching: ${productName} (${i + 1}/${totalGuids})...`
            });
            MessageRenderer.updateMessage(assistantMessage.id);

            const startTime = Date.now();
            let fetchResult = null;

            // Try fetching with specific section first, then fallback to full label
            const fetchStrategies = [
                { sectionCode: '34068-7', description: 'Dosage and Administration' },
                { sectionCode: null, description: 'Full label (fallback)' }
            ];

            for (const strategy of fetchStrategies) {
                const endpoint = {
                    method: 'GET',
                    path: `/api/Label/markdown/sections/${guid}`,
                    queryParams: strategy.sectionCode ? { sectionCode: strategy.sectionCode } : {},
                    description: `Label: ${productName} - ${strategy.description}`
                };

                const fullApiUrl = ChatConfig.buildUrl(endpoint.path) +
                    (strategy.sectionCode ? '?' + new URLSearchParams(endpoint.queryParams).toString() : '');

                try {
                    const response = await fetch(fullApiUrl, {
                        method: endpoint.method,
                        headers: { 'Accept': 'application/json' },
                        signal: abortController?.signal
                    });

                    if (response.ok) {
                        const data = await response.json();
                        const hasData = data && (
                            (typeof data === 'object' && Object.keys(data).length > 0) ||
                            (Array.isArray(data) && data.length > 0) ||
                            (typeof data === 'string' && data.length > 0)
                        );

                        if (hasData) {
                            console.log(`[MedRecProChat] Label fetch [${i + 1}/${totalGuids}] succeeded (${strategy.description}): ${productName} (${guid.substring(0, 8)}...)`);

                            fetchResult = {
                                specification: endpoint,
                                statusCode: response.status,
                                result: data,
                                executionTimeMs: Date.now() - startTime,
                                step: 2,
                                hasData: true,
                                extractedProductName: productName,
                                _apiUrl: fullApiUrl
                            };
                            break; // Success - stop trying other strategies
                        }
                    }

                    // If 404 or no data, try next strategy
                    if (response.status === 404 || !response.ok) {
                        console.log(`[MedRecProChat] Label fetch [${i + 1}/${totalGuids}] ${strategy.description} returned ${response.status}, trying fallback...`);
                        continue;
                    }

                } catch (error) {
                    if (error.name === 'AbortError') {
                        console.log('[MedRecProChat] Label fetch aborted');
                        throw error;
                    }
                    console.log(`[MedRecProChat] Label fetch [${i + 1}/${totalGuids}] ${strategy.description} exception: ${error.message}`);
                    // Try next strategy
                    continue;
                }
            }

            // If all strategies failed, record failure
            if (!fetchResult) {
                console.log(`[MedRecProChat] Label fetch [${i + 1}/${totalGuids}] all strategies failed: ${productName} (${guid.substring(0, 8)}...)`);
                fetchResult = {
                    specification: { method: 'GET', path: `/api/Label/markdown/sections/${guid}` },
                    statusCode: 404,
                    result: null,
                    executionTimeMs: Date.now() - startTime,
                    step: 2,
                    hasData: false,
                    extractedProductName: productName,
                    error: 'Label not found (tried section and full label)'
                };
            }

            results.push(fetchResult);

            // Notify progress callback
            if (progressCallback) {
                progressCallback({
                    type: 'endpoint_complete',
                    productName: productName,
                    current: i + 1,
                    total: totalGuids,
                    success: fetchResult.hasData
                });
            }
        }

        console.log(`[MedRecProChat] === DYNAMIC LABEL FETCH COMPLETE: ${results.filter(r => r.hasData).length}/${totalGuids} successful ===`);

        return results;
    }

    /**************************************************************/
    /**
     * Handles checkpoint confirmation - fetches data and synthesizes selected products.
     *
     * @param {string} messageId - ID of the assistant message with checkpoint
     *
     * @description
     * Called when user clicks "Synthesize Selected" on checkpoint panel.
     * Now executes the remaining steps (data fetching) for selected products only,
     * then continues with synthesis.
     *
     * @example
     * // Called from checkpoint confirm button
     * MedRecProChat.checkpointConfirm('msg-123');
     *
     * @see CheckpointManager.confirmCheckpoint - Gets selected results
     * @see synthesizeAfterCheckpoint - Continues with synthesis
     */
    /**************************************************************/
    async function checkpointConfirm(messageId) {
        console.log(`[MedRecProChat] Checkpoint confirmed for message ${messageId}`);

        // Get checkpoint confirmation data
        const confirmation = CheckpointManager.confirmCheckpoint();
        if (!confirmation) {
            console.warn('[MedRecProChat] No pending checkpoint to confirm');
            return;
        }

        // Get the checkpoint state (contains discovery data)
        const checkpointState = ChatState.getCheckpointState();

        // Clear checkpoint state
        ChatState.clearCheckpointState();

        // Get the assistant message
        const assistantMessage = ChatState.getMessageById(messageId);
        if (!assistantMessage) {
            console.warn('[MedRecProChat] Could not find assistant message');
            return;
        }

        // Clear checkpoint UI and show data fetching progress
        ChatState.updateMessage(messageId, {
            checkpointUI: undefined,
            progress: 0.1,
            progressStatus: `Fetching data for ${confirmation.selectedProducts} selected product(s)...`,
            isStreaming: true
        });
        MessageRenderer.updateMessage(messageId);

        try {
            // Get the selected product GUIDs
            const selectedGuids = confirmation.selectedIds;

            // Create progress callback
            const progressCallback = ProgressiveConfig.isDetailedProgressEnabled() ? (progressEvent) => {
                if (progressEvent.type === 'endpoint_complete' && progressEvent.productName) {
                    ChatState.updateMessage(messageId, {
                        progressStatus: `Fetching: ${progressEvent.productName} (${progressEvent.current}/${progressEvent.total})...`
                    });
                    MessageRenderer.updateMessage(messageId);
                }
            } : null;

            // Execute remaining steps with only selected products
            let remainingResults = await EndpointExecutor.executeRemainingSteps(
                confirmation.interpretation.suggestedEndpoints,
                assistantMessage,
                ChatState.getAbortController(),
                {
                    extractedVariables: checkpointState?.discoveryData?.extractedVariables || {},
                    documentGuidToProductName: checkpointState?.discoveryData?.documentGuidToProductName || {},
                    selectedGuids: selectedGuids,
                    progressCallback: progressCallback
                }
            );

            // If no remaining steps returned results, we need to dynamically fetch labels
            // This handles single-step discovery endpoints where step 2 doesn't exist
            if (remainingResults.length === 0 && selectedGuids.length > 0) {
                console.log(`[MedRecProChat] No remaining steps - dynamically fetching labels for ${selectedGuids.length} products`);

                ChatState.updateMessage(messageId, {
                    progressStatus: `Fetching label data for ${selectedGuids.length} product(s)...`
                });
                MessageRenderer.updateMessage(messageId);

                // Dynamically fetch labels for selected GUIDs
                remainingResults = await fetchLabelsForGuids(
                    selectedGuids,
                    checkpointState?.discoveryData?.documentGuidToProductName || {},
                    assistantMessage,
                    ChatState.getAbortController(),
                    progressCallback
                );
            }

            // Update progress for synthesis
            ChatState.updateMessage(messageId, {
                progress: 0.8,
                progressStatus: `Synthesizing ${confirmation.selectedProducts} product(s)...`
            });
            MessageRenderer.updateMessage(messageId);

            // Continue with synthesis using the fetched results
            await synthesizeAfterCheckpoint(
                confirmation.originalQuery,
                confirmation.interpretation,
                remainingResults,
                assistantMessage
            );
        } catch (error) {
            console.error('[MedRecProChat] Checkpoint execution failed:', error);
            ChatState.updateMessage(messageId, {
                error: error.message || 'Data fetch failed',
                isStreaming: false,
                progress: undefined,
                progressStatus: undefined
            });
            MessageRenderer.updateMessage(messageId);
        }
    }

    /**************************************************************/
    /**
     * Handles checkpoint cancellation.
     *
     * @param {string} messageId - ID of the assistant message with checkpoint
     *
     * @description
     * Called when user clicks "Cancel" on checkpoint panel.
     * Clears checkpoint state and shows cancellation message.
     *
     * @example
     * // Called from checkpoint cancel button
     * MedRecProChat.checkpointCancel('msg-123');
     */
    /**************************************************************/
    function checkpointCancel(messageId) {
        console.log(`[MedRecProChat] Checkpoint cancelled for message ${messageId}`);

        // Cancel the checkpoint
        CheckpointManager.cancelCheckpoint();

        // Clear checkpoint state
        ChatState.clearCheckpointState();

        // Update message to show cancellation
        ChatState.updateMessage(messageId, {
            checkpointUI: undefined,
            content: CheckpointRenderer.renderCancellationMessage('Query cancelled by user'),
            isStreaming: false,
            progress: undefined,
            progressStatus: undefined
        });
        MessageRenderer.updateMessage(messageId);
    }

    /**************************************************************/
    /**
     * Toggles a product selection in the checkpoint.
     *
     * @param {string} messageId - ID of the assistant message with checkpoint
     * @param {string} productId - ID of the product to toggle
     * @param {boolean} isSelected - New selection state
     *
     * @description
     * Called when user checks/unchecks a product checkbox in checkpoint panel.
     *
     * @example
     * // Called from checkbox onchange
     * MedRecProChat.checkpointToggle('msg-123', 'product-abc', true);
     */
    /**************************************************************/
    function checkpointToggle(messageId, productId, isSelected) {
        // Update selection in manager
        if (isSelected) {
            const checkpoint = CheckpointManager.getPendingCheckpoint();
            if (checkpoint && !checkpoint.selectedProductIds.includes(productId)) {
                checkpoint.selectedProductIds.push(productId);
            }
        } else {
            CheckpointManager.toggleProduct(productId);
        }

        // Update UI
        const selectedCount = CheckpointManager.getSelectedCount();
        const totalCount = CheckpointManager.getTotalCount();
        CheckpointRenderer.updateCheckpointSelection(messageId, selectedCount, totalCount);
    }

    /**************************************************************/
    /**
     * Selects all products in the checkpoint.
     *
     * @param {string} messageId - ID of the assistant message with checkpoint
     *
     * @example
     * MedRecProChat.checkpointSelectAll('msg-123');
     */
    /**************************************************************/
    function checkpointSelectAll(messageId) {
        const selectedIds = CheckpointManager.selectAll();
        CheckpointRenderer.updateCheckboxStates(messageId, selectedIds);
        CheckpointRenderer.updateCheckpointSelection(
            messageId,
            selectedIds.length,
            CheckpointManager.getTotalCount()
        );
    }

    /**************************************************************/
    /**
     * Deselects all products in the checkpoint.
     *
     * @param {string} messageId - ID of the assistant message with checkpoint
     *
     * @example
     * MedRecProChat.checkpointSelectNone('msg-123');
     */
    /**************************************************************/
    function checkpointSelectNone(messageId) {
        CheckpointManager.selectNone();
        CheckpointRenderer.updateCheckboxStates(messageId, []);
        CheckpointRenderer.updateCheckpointSelection(
            messageId,
            0,
            CheckpointManager.getTotalCount()
        );
    }

    /**************************************************************/
    /**
     * Synthesizes results after checkpoint confirmation.
     *
     * @param {string} originalQuery - User's original query
     * @param {Object} interpretation - Original interpretation
     * @param {Array} selectedResults - Results for selected products
     * @param {Object} assistantMessage - Assistant message to update
     *
     * @description
     * Continues the synthesis flow after user confirms checkpoint selection.
     * Uses batch synthesis if enabled, otherwise single synthesis.
     *
     * @async
     * @see BatchSynthesizer.synthesizeInBatches - For batch synthesis
     * @see ApiService.synthesizeResults - For single synthesis
     */
    /**************************************************************/
    async function synthesizeAfterCheckpoint(originalQuery, interpretation, selectedResults, assistantMessage) {
        // Check if batch synthesis should be used
        const productGroups = ResultGrouper.groupResultsByProduct(selectedResults);
        const useBatch = BatchSynthesizer.shouldUseBatchSynthesis(productGroups);

        let responseContent = '';

        if (useBatch) {
            // Track accumulated content for progressive display (without label links)
            let progressiveContent = '';
            let allLabelLinks = new Map(); // Use Map to deduplicate by URL

            // Use batch synthesis with progressive display
            const batchResult = await BatchSynthesizer.synthesizeInBatches(
                productGroups,
                { originalQuery, interpretation },
                {
                    onBatchStart: (batchGroups, batchIndex, totalBatches) => {
                        const productNames = Object.values(batchGroups).map(g => g.name);
                        ChatState.updateMessage(assistantMessage.id, {
                            progressStatus: `Synthesizing batch ${batchIndex}/${totalBatches}: ${productNames.join(', ')}...`
                        });
                        MessageRenderer.updateMessage(assistantMessage.id);
                    },
                    onBatchComplete: async (batchResponse, batchGroups, batchIndex) => {
                        // Extract and strip label links from batch response
                        const { content: strippedContent, links } = stripAndExtractLabelLinks(batchResponse.response || '');

                        // Collect label links for aggregation at the end
                        links.forEach(link => {
                            allLabelLinks.set(link.url, link);
                        });

                        // Collect data references from response object too
                        if (batchResponse.dataReferences) {
                            for (const [displayName, url] of Object.entries(batchResponse.dataReferences)) {
                                const fullUrl = ChatConfig.buildUrl(url);
                                allLabelLinks.set(fullUrl, { name: displayName, url: fullUrl });
                            }
                        }

                        // Progressively append stripped content
                        progressiveContent += (progressiveContent ? '\n\n' : '') + strippedContent;

                        // Update message with progressive content (hide progress status to show content)
                        ChatState.updateMessage(assistantMessage.id, {
                            content: sanitizeApiUrls(progressiveContent),
                            progress: undefined,
                            progressStatus: undefined,
                            isStreaming: true
                        });
                        MessageRenderer.updateMessage(assistantMessage.id);

                        // Yield to allow browser to repaint before next batch
                        await new Promise(resolve => requestAnimationFrame(resolve));
                    }
                }
            );

            // Build final response content from progressive content (already stripped)
            responseContent = progressiveContent;

            // Add follow-ups from batch result
            if (batchResult.suggestedFollowUps && batchResult.suggestedFollowUps.length > 0) {
                responseContent += '\n\n**Suggested follow-ups:**\n';
                batchResult.suggestedFollowUps.forEach(followUp => {
                    responseContent += `- ${followUp}\n`;
                });
            }

            // Add aggregated label links at the end (before data sources)
            if (allLabelLinks.size > 0) {
                responseContent += '\n\n**View Full Labels:**\n';
                // Sort by name for consistent display, apply title case for consistency
                const sortedLinks = Array.from(allLabelLinks.values())
                    .sort((a, b) => a.name.localeCompare(b.name));
                sortedLinks.forEach(link => {
                    const titleCaseName = ChatUtils.toTitleCase(link.name);
                    responseContent += `- [View Full Label (${titleCaseName})](${link.url})\n`;
                });
            }
        } else {
            // Use single synthesis
            const synthesis = await ApiService.synthesizeResults(
                originalQuery,
                interpretation,
                selectedResults
            );

            responseContent = synthesis.response || 'No results found.';
            responseContent = sanitizeApiUrls(responseContent);

            // Add follow-ups
            if (synthesis.suggestedFollowUps && synthesis.suggestedFollowUps.length > 0) {
                responseContent += '\n\n**Suggested follow-ups:**\n';
                synthesis.suggestedFollowUps.forEach(followUp => {
                    responseContent += `- ${followUp}\n`;
                });
            }

            // Add data references
            const hasViewFullLabels = /(?:\*\*)?View Full Labels?:?(?:\*\*)?/i.test(responseContent);
            if (!hasViewFullLabels && synthesis.dataReferences && Object.keys(synthesis.dataReferences).length > 0) {
                responseContent += '\n\n**View Full Labels:**\n';
                for (const [displayName, url] of Object.entries(synthesis.dataReferences)) {
                    const titleCaseName = ChatUtils.toTitleCase(displayName);
                    responseContent += `- [View Full Label (${titleCaseName})](${ChatConfig.buildUrl(url)})\n`;
                }
            }
        }

        // Add API source links
        const sourceLinks = ApiService.formatApiSourceLinks(selectedResults);
        if (sourceLinks) {
            responseContent += sourceLinks;
        }

        // Update final message state
        ChatState.updateMessage(assistantMessage.id, {
            content: responseContent,
            progress: undefined,
            progressStatus: undefined,
            isStreaming: false
        });
        MessageRenderer.updateMessage(assistantMessage.id);

        // Reset loading state
        ChatState.setLoading(false);
        UIHelpers.updateUI();
    }

    /**************************************************************/
    /**
     * Retries a failed message.
     *
     * @param {string} messageId - ID of the failed message
     *
     * @description
     * Finds the original user message, removes the failed assistant message,
     * and re-sends the original input.
     *
     * @example
     * // Called from retry button onclick
     * MedRecProChat.retryMessage('msg-123');
     *
     * @see MessageRenderer - Creates retry buttons for failed messages
     */
    /**************************************************************/
    function retryMessage(messageId) {
        const messages = ChatState.getMessages();
        const messageIndex = messages.findIndex(m => m.id === messageId);
        if (messageIndex === -1) return;

        // Find preceding user message
        let userMessageIndex = messageIndex - 1;
        while (userMessageIndex >= 0 && messages[userMessageIndex].role !== 'user') {
            userMessageIndex--;
        }

        if (userMessageIndex >= 0) {
            const userMessage = messages[userMessageIndex];

            // Remove the failed message
            ChatState.removeMessage(messageId);
            MessageRenderer.renderMessages();

            // Resend
            UIHelpers.setInputValue(userMessage.content);
            sendMessage();
        }
    }

    /**************************************************************/
    /**
     * Initializes the chat application.
     *
     * @description
     * Startup sequence:
     * 1. Initialize DOM element references
     * 2. Initialize all modules with elements
     * 3. Focus input field
     * 4. Fetch system context (async)
     * 5. Update initial UI state
     *
     * @example
     * // Called on DOMContentLoaded
     * document.addEventListener('DOMContentLoaded', () => {
     *     MedRecProChat.init();
     * });
     *
     * @see initDOMElements - Step 1
     * @see initModules - Step 2
     * @see ApiService.fetchSystemContext - Step 4
     */
    /**************************************************************/
    async function init() {
        // Initialize DOM and modules
        initDOMElements();
        initModules();

        // Focus input
        UIHelpers.focusInput();

        // Fetch system context (shows demo banner if applicable)
        const context = await ApiService.fetchSystemContext();
        if (context?.isDemoMode) {
            UIHelpers.showDemoBanner(
                context.demoModeMessage || 'DEMO MODE - Database resets periodically'
            );
        }

        // Initial UI state
        UIHelpers.updateUI();

        console.log('[MedRecProChat] Initialization complete');
    }

    /**************************************************************/
    /**
     * Public API exposed on window.MedRecProChat.
     *
     * @description
     * These functions are called from HTML onclick handlers.
     * The init() function is called automatically when the module loads.
     */
    /**************************************************************/
    return {
        // Initialization
        init: init,

        // Message actions (called from HTML)
        copyMessage: MessageRenderer.copyMessage,
        toggleThinking: MessageRenderer.toggleThinking,
        retryMessage: retryMessage,

        // File actions (called from HTML)
        removeFile: FileHandler.removeFile,

        // Code actions (called from HTML)
        copyCode: MarkdownRenderer.copyCode,

        // Checkpoint actions (called from HTML)
        checkpointConfirm: checkpointConfirm,
        checkpointCancel: checkpointCancel,
        checkpointToggle: checkpointToggle,
        checkpointSelectAll: checkpointSelectAll,
        checkpointSelectNone: checkpointSelectNone
    };
})();

// Expose globally for HTML event handlers
window.MedRecProChat = MedRecProChat;

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => MedRecProChat.init());
} else {
    MedRecProChat.init();
}

// Export for ES module consumers
export { MedRecProChat };
