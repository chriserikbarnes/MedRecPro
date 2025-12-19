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
                let responseContent = interpretation.directResponse;

                // If this is an import response, add hyperlinks to view imported documents
                if (importResult && importResult.documentIds && importResult.documentIds.length > 0) {
                    responseContent += '\n\n**View Full Labels:**\n';
                    importResult.documentIds.forEach(docGuid => {
                        const shortGuid = docGuid.substring(0, 8);
                        responseContent += `- [View Imported Label (${shortGuid}...)](${ChatConfig.buildUrl(`/api/Label/generate/${docGuid}/true`)})\n`;
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
                ChatState.updateMessage(assistantMessage.id, {
                    content: interpretation.directResponse ||
                        interpretation.explanation ||
                        'I understood your request but couldn\'t process it. Please try rephrasing.',
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
     * Executes endpoints and synthesizes results into a response.
     *
     * @param {string} originalInput - User's original message
     * @param {Object} interpretation - Interpretation with suggested endpoints
     * @param {Object} assistantMessage - Assistant message to update
     *
     * @description
     * Handles the endpoint execution flow:
     * 1. Shows progress indicator
     * 2. Executes endpoints with dependencies
     * 3. Handles retries if all endpoints fail
     * 4. Synthesizes results into natural language
     * 5. Appends follow-ups and data references
     * 6. Updates final message state
     *
     * @async
     * @see EndpointExecutor.executeEndpointsWithDependencies
     * @see ApiService.synthesizeResults
     * @see ApiService.attemptRetryInterpretation
     */
    /**************************************************************/
    async function executeAndSynthesizeEndpoints(originalInput, interpretation, assistantMessage) {
        // Show progress
        ChatState.updateMessage(assistantMessage.id, {
            progress: 0,
            progressStatus: 'Executing queries...'
        });
        MessageRenderer.updateMessage(assistantMessage.id);

        // Execute endpoints with dependency support
        const results = await EndpointExecutor.executeEndpointsWithDependencies(
            interpretation.suggestedEndpoints,
            assistantMessage,
            ChatState.getAbortController()
        );

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

        // Synthesize results
        ChatState.updateMessage(assistantMessage.id, {
            progress: 0.9,
            progressStatus: isSettingsData ? 'Formatting log data...' : 'Synthesizing results...'
        });
        MessageRenderer.updateMessage(assistantMessage.id);

        // Build final response content
        let responseContent = '';

        if (isSettingsData) {
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

            // Add follow-up suggestions
            if (synthesis.suggestedFollowUps && synthesis.suggestedFollowUps.length > 0) {
                responseContent += '\n\n**Suggested follow-ups:**\n';
                synthesis.suggestedFollowUps.forEach(followUp => {
                    responseContent += `- ${followUp}\n`;
                });
            }

            // Add document reference links
            if (synthesis.dataReferences && Object.keys(synthesis.dataReferences).length > 0) {
                responseContent += '\n\n**View Full Labels:**\n';
                for (const [displayName, url] of Object.entries(synthesis.dataReferences)) {
                    responseContent += `- [${displayName}](${ChatConfig.buildUrl(url)})\n`;
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
        copyCode: MarkdownRenderer.copyCode
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
