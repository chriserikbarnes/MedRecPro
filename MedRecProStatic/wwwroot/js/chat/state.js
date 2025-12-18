/**************************************************************/
/**
 * MedRecPro Chat State Management Module
 *
 * @fileoverview Centralizes all application state for the chat interface.
 * Provides a single source of truth for UI state, conversation data, and file management.
 *
 * @description
 * The state management module maintains:
 * - Conversation messages (user and assistant)
 * - File attachments pending upload
 * - Loading/streaming indicators
 * - System context from the API
 * - Conversation tracking IDs
 * - Request cancellation controllers
 *
 * @example
 * // Import and access state
 * import { ChatState } from './state.js';
 *
 * ChatState.addMessage({ role: 'user', content: 'Hello' });
 * const messages = ChatState.getMessages();
 *
 * @module chat/state
 * @see ChatConfig - Configuration module
 * @see MessageRenderer - Consumes message state for display
 * @see FileHandler - Manages file state
 */
/**************************************************************/

export const ChatState = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Generates a UUID v4 for unique identification.
     *
     * @returns {string} UUID string in format 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'
     *
     * @description
     * Local UUID generator to avoid circular dependency with utils.js.
     * Creates a cryptographically pseudo-random UUID v4 compliant string.
     */
    /**************************************************************/
    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    /**************************************************************/
    /**
     * Core application state object.
     *
     * @description
     * Contains all mutable state for the chat interface. State is private
     * and accessed only through getter/setter methods to maintain encapsulation.
     *
     * @property {Array} messages - Array of message objects (user and assistant)
     * @property {Array} files - Array of File objects pending upload
     * @property {boolean} isLoading - True when an API request is in progress
     * @property {boolean} showFileUpload - True when file dropzone is visible
     * @property {Object|null} systemContext - System configuration from /api/Ai/context
     * @property {string} conversationId - UUID for tracking conversation continuity
     * @property {AbortController|null} abortController - Controller for cancelling requests
     * @property {Function|null} currentProgressCallback - Callback for import progress updates
     *
     * @see generateUUID - Creates conversation IDs
     */
    /**************************************************************/
    const state = {
        messages: [],
        files: [],
        isLoading: false,
        showFileUpload: false,
        systemContext: null,
        conversationId: generateUUID(),
        abortController: null,
        currentProgressCallback: null
    };

    /**************************************************************/
    /**
     * Gets the current messages array.
     *
     * @returns {Array} Copy of the messages array to prevent direct mutation
     *
     * @description
     * Returns a shallow copy of the messages array. For message modifications,
     * use addMessage(), updateMessage(), or removeMessage() methods.
     *
     * @example
     * const messages = ChatState.getMessages();
     * messages.forEach(msg => console.log(msg.content));
     *
     * @see addMessage - Adds a new message
     * @see updateMessage - Updates an existing message
     */
    /**************************************************************/
    function getMessages() {
        return [...state.messages];
    }

    /**************************************************************/
    /**
     * Gets a specific message by ID.
     *
     * @param {string} messageId - The unique message identifier
     * @returns {Object|undefined} The message object or undefined if not found
     *
     * @example
     * const msg = ChatState.getMessageById('abc-123');
     * if (msg) {
     *     console.log(msg.content);
     * }
     *
     * @see updateMessage - Updates a found message
     */
    /**************************************************************/
    function getMessageById(messageId) {
        return state.messages.find(m => m.id === messageId);
    }

    /**************************************************************/
    /**
     * Adds a new message to the conversation.
     *
     * @param {Object} message - Message object to add
     * @param {string} message.id - Unique identifier (auto-generated if not provided)
     * @param {string} message.role - 'user' or 'assistant'
     * @param {string} message.content - Message text content
     * @param {Date} [message.timestamp] - Creation timestamp (auto-set if not provided)
     * @returns {Object} The added message with auto-generated fields
     *
     * @example
     * const msg = ChatState.addMessage({
     *     role: 'user',
     *     content: 'What are the side effects of aspirin?'
     * });
     * console.log(msg.id);  // Auto-generated UUID
     *
     * @see ChatUtils.generateUUID - Generates message IDs
     */
    /**************************************************************/
    function addMessage(message) {
        const newMessage = {
            id: message.id || generateUUID(),
            timestamp: message.timestamp || new Date(),
            ...message
        };
        state.messages.push(newMessage);
        return newMessage;
    }

    /**************************************************************/
    /**
     * Updates an existing message by ID.
     *
     * @param {string} messageId - The message ID to update
     * @param {Object} updates - Partial message object with fields to update
     * @returns {Object|null} The updated message or null if not found
     *
     * @description
     * Merges the updates into the existing message. Commonly used to:
     * - Update streaming content as it arrives
     * - Set error states
     * - Update progress indicators
     * - Mark streaming as complete
     *
     * @example
     * // Update streaming content
     * ChatState.updateMessage(msgId, {
     *     content: currentContent + newChunk,
     *     isStreaming: true
     * });
     *
     * // Mark as complete
     * ChatState.updateMessage(msgId, {
     *     isStreaming: false,
     *     progress: undefined
     * });
     *
     * @see getMessageById - Find messages before updating
     */
    /**************************************************************/
    function updateMessage(messageId, updates) {
        const index = state.messages.findIndex(m => m.id === messageId);
        if (index !== -1) {
            state.messages[index] = { ...state.messages[index], ...updates };
            return state.messages[index];
        }
        return null;
    }

    /**************************************************************/
    /**
     * Removes a message by ID.
     *
     * @param {string} messageId - The message ID to remove
     * @returns {boolean} True if message was found and removed
     *
     * @example
     * if (ChatState.removeMessage(failedMsgId)) {
     *     console.log('Failed message removed');
     * }
     *
     * @see retryMessage - May remove failed messages before retrying
     */
    /**************************************************************/
    function removeMessage(messageId) {
        const initialLength = state.messages.length;
        state.messages = state.messages.filter(m => m.id !== messageId);
        return state.messages.length < initialLength;
    }

    /**************************************************************/
    /**
     * Clears all messages and resets conversation state.
     *
     * @description
     * Resets the conversation to initial state:
     * - Clears all messages
     * - Clears pending files
     * - Generates new conversation ID
     *
     * @example
     * ChatState.clearConversation();
     *
     * @see ChatUtils.generateUUID - Generates new conversation ID
     */
    /**************************************************************/
    function clearConversation() {
        state.messages = [];
        state.files = [];
        state.conversationId = generateUUID();
    }

    /**************************************************************/
    /**
     * Gets the current files array.
     *
     * @returns {Array} Copy of the files array
     *
     * @see addFiles - Adds files to the array
     * @see removeFile - Removes a file by index
     * @see clearFiles - Clears all files
     */
    /**************************************************************/
    function getFiles() {
        return [...state.files];
    }

    /**************************************************************/
    /**
     * Adds files to the pending upload list.
     *
     * @param {Array<File>} files - Array of File objects to add
     *
     * @example
     * ChatState.addFiles([file1, file2]);
     *
     * @see FileHandler.addFiles - Higher-level file handling with validation
     */
    /**************************************************************/
    function addFiles(files) {
        state.files.push(...files);
    }

    /**************************************************************/
    /**
     * Removes a file by index.
     *
     * @param {number} index - Array index of file to remove
     * @returns {File|undefined} The removed file or undefined if index invalid
     *
     * @example
     * const removed = ChatState.removeFile(0);
     *
     * @see FileHandler.removeFile - Triggers UI update after removal
     */
    /**************************************************************/
    function removeFile(index) {
        if (index >= 0 && index < state.files.length) {
            return state.files.splice(index, 1)[0];
        }
        return undefined;
    }

    /**************************************************************/
    /**
     * Clears all pending files.
     *
     * @example
     * ChatState.clearFiles();  // After successful upload
     */
    /**************************************************************/
    function clearFiles() {
        state.files = [];
    }

    /**************************************************************/
    /**
     * Gets the loading state.
     *
     * @returns {boolean} True if a request is in progress
     *
     * @see setLoading - Sets the loading state
     * @see UIHelpers.updateUI - Uses loading state for button visibility
     */
    /**************************************************************/
    function isLoading() {
        return state.isLoading;
    }

    /**************************************************************/
    /**
     * Sets the loading state.
     *
     * @param {boolean} loading - New loading state
     *
     * @example
     * ChatState.setLoading(true);  // Start request
     * // ... perform request ...
     * ChatState.setLoading(false); // Request complete
     *
     * @see ApiService.sendMessage - Sets loading during API calls
     */
    /**************************************************************/
    function setLoading(loading) {
        state.isLoading = loading;
    }

    /**************************************************************/
    /**
     * Gets the file upload visibility state.
     *
     * @returns {boolean} True if dropzone is visible
     *
     * @see setShowFileUpload - Sets the visibility
     * @see UIHelpers.toggleFileUpload - Toggles visibility
     */
    /**************************************************************/
    function isShowFileUpload() {
        return state.showFileUpload;
    }

    /**************************************************************/
    /**
     * Sets the file upload visibility state.
     *
     * @param {boolean} show - New visibility state
     */
    /**************************************************************/
    function setShowFileUpload(show) {
        state.showFileUpload = show;
    }

    /**************************************************************/
    /**
     * Gets the system context.
     *
     * @returns {Object|null} System context from /api/Ai/context or null
     *
     * @description
     * System context contains configuration such as:
     * - isDemoMode: Whether running in demo mode
     * - demoModeMessage: Banner text for demo mode
     *
     * @see setSystemContext - Sets the context after fetching
     * @see ApiService.fetchSystemContext - Fetches context from API
     */
    /**************************************************************/
    function getSystemContext() {
        return state.systemContext;
    }

    /**************************************************************/
    /**
     * Sets the system context.
     *
     * @param {Object} context - System context object from API
     *
     * @see ApiService.fetchSystemContext - Calls this after successful fetch
     */
    /**************************************************************/
    function setSystemContext(context) {
        state.systemContext = context;
    }

    /**************************************************************/
    /**
     * Gets the current conversation ID.
     *
     * @returns {string} UUID identifying this conversation
     *
     * @description
     * The conversation ID is used to maintain context across multiple
     * API calls within a single conversation session.
     *
     * @see clearConversation - Generates new ID when clearing
     */
    /**************************************************************/
    function getConversationId() {
        return state.conversationId;
    }

    /**************************************************************/
    /**
     * Gets the abort controller for request cancellation.
     *
     * @returns {AbortController|null} Current abort controller or null
     *
     * @see setAbortController - Sets the controller
     * @see UIHelpers.cancelRequest - Uses this to cancel requests
     */
    /**************************************************************/
    function getAbortController() {
        return state.abortController;
    }

    /**************************************************************/
    /**
     * Sets the abort controller for request cancellation.
     *
     * @param {AbortController|null} controller - New abort controller or null
     *
     * @example
     * const controller = new AbortController();
     * ChatState.setAbortController(controller);
     * fetch(url, { signal: controller.signal });
     */
    /**************************************************************/
    function setAbortController(controller) {
        state.abortController = controller;
    }

    /**************************************************************/
    /**
     * Gets the current progress callback.
     *
     * @returns {Function|null} Progress callback or null
     *
     * @see setProgressCallback - Sets the callback
     * @see FileHandler.uploadFiles - Uses callback for progress updates
     */
    /**************************************************************/
    function getProgressCallback() {
        return state.currentProgressCallback;
    }

    /**************************************************************/
    /**
     * Sets the progress callback for async operations.
     *
     * @param {Function|null} callback - Callback function or null
     *
     * @example
     * ChatState.setProgressCallback((status) => {
     *     console.log(`Progress: ${status.percentComplete}%`);
     * });
     */
    /**************************************************************/
    function setProgressCallback(callback) {
        state.currentProgressCallback = callback;
    }

    /**************************************************************/
    /**
     * Gets the number of pending files.
     *
     * @returns {number} Count of files pending upload
     *
     * @see UIHelpers.updateUI - Uses for badge display
     */
    /**************************************************************/
    function getFileCount() {
        return state.files.length;
    }

    /**************************************************************/
    /**
     * Public API for the state management module.
     *
     * @description
     * Exposes state access and mutation methods. Direct state object access
     * is intentionally not exposed to maintain encapsulation.
     */
    /**************************************************************/
    return {
        // Message management
        getMessages: getMessages,
        getMessageById: getMessageById,
        addMessage: addMessage,
        updateMessage: updateMessage,
        removeMessage: removeMessage,
        clearConversation: clearConversation,

        // File management
        getFiles: getFiles,
        addFiles: addFiles,
        removeFile: removeFile,
        clearFiles: clearFiles,
        getFileCount: getFileCount,

        // Loading state
        isLoading: isLoading,
        setLoading: setLoading,

        // File upload visibility
        isShowFileUpload: isShowFileUpload,
        setShowFileUpload: setShowFileUpload,

        // System context
        getSystemContext: getSystemContext,
        setSystemContext: setSystemContext,

        // Conversation tracking
        getConversationId: getConversationId,

        // Request cancellation
        getAbortController: getAbortController,
        setAbortController: setAbortController,

        // Progress tracking
        getProgressCallback: getProgressCallback,
        setProgressCallback: setProgressCallback
    };
})();
