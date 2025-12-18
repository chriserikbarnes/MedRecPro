/**************************************************************/
/**
 * MedRecPro Chat UI Helpers Module
 *
 * @fileoverview Provides UI utility functions for the chat interface.
 * Handles button states, textarea auto-resize, and user interaction events.
 *
 * @description
 * The UI helpers module provides:
 * - Button state management (send/cancel visibility)
 * - Textarea auto-resize functionality
 * - Request cancellation
 * - Conversation clearing
 * - Event listener setup
 * - Suggestion card handling
 *
 * @example
 * import { UIHelpers } from './ui-helpers.js';
 *
 * // Initialize with DOM elements
 * UIHelpers.initElements({ messageInput, sendBtn, cancelBtn, clearBtn });
 *
 * // Setup event listeners
 * UIHelpers.setupEventListeners();
 *
 * @module chat/ui-helpers
 * @see ChatState - Source of loading state
 * @see MessageRenderer - Updates messages on state changes
 * @see FileHandler - File-related UI interactions
 */
/**************************************************************/

import { ChatState } from './state.js';
import { MessageRenderer } from './message-renderer.js';
import { FileHandler } from './file-handler.js';
import { MarkdownRenderer } from './markdown.js';

export const UIHelpers = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Cached DOM elements for UI operations.
     *
     * @type {Object}
     * @property {HTMLElement} messageInput - Text input field
     * @property {HTMLElement} sendBtn - Send button
     * @property {HTMLElement} cancelBtn - Cancel button
     * @property {HTMLElement} clearBtn - Clear conversation button
     * @property {HTMLElement} attachBtn - File attachment button
     * @property {HTMLElement} contextBanner - Demo mode banner
     *
     * @see initElements - Initializes these references
     */
    /**************************************************************/
    let elements = {
        messageInput: null,
        sendBtn: null,
        cancelBtn: null,
        clearBtn: null,
        attachBtn: null,
        contextBanner: null
    };

    /**************************************************************/
    /**
     * Callback function for sending messages.
     *
     * @type {Function|null}
     * @description Set by the main orchestrator to handle message sending.
     */
    /**************************************************************/
    let onSendMessage = null;

    /**************************************************************/
    /**
     * Initializes DOM element references for UI operations.
     *
     * @param {Object} domElements - Object containing DOM element references
     *
     * @example
     * UIHelpers.initElements({
     *     messageInput: document.getElementById('messageInput'),
     *     sendBtn: document.getElementById('sendBtn'),
     *     cancelBtn: document.getElementById('cancelBtn'),
     *     clearBtn: document.getElementById('clearBtn'),
     *     attachBtn: document.getElementById('attachBtn'),
     *     contextBanner: document.getElementById('contextBanner')
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
     * Sets the callback for sending messages.
     *
     * @param {Function} callback - Function to call when user sends a message
     *
     * @example
     * UIHelpers.setOnSendMessage(() => {
     *     const text = UIHelpers.getInputValue();
     *     // Process and send message
     * });
     */
    /**************************************************************/
    function setOnSendMessage(callback) {
        onSendMessage = callback;
    }

    /**************************************************************/
    /**
     * Updates UI state based on loading status.
     *
     * @description
     * Manages the visibility and disabled states of UI elements:
     * - Disables input when loading
     * - Shows send button when not loading
     * - Shows cancel button when loading
     * - Disables send when no content and no files
     *
     * @example
     * // After state changes
     * UIHelpers.updateUI();
     *
     * @see ChatState.isLoading - Source of loading state
     */
    /**************************************************************/
    function updateUI() {
        const isLoading = ChatState.isLoading();

        // Disable input during loading
        elements.messageInput.disabled = isLoading;

        // Toggle send/cancel button visibility
        elements.sendBtn.style.display = isLoading ? 'none' : 'inline-flex';
        elements.cancelBtn.style.display = isLoading ? 'inline-flex' : 'none';

        // Disable send if no content and no files
        const hasContent = elements.messageInput.value.trim().length > 0;
        const hasFiles = ChatState.getFileCount() > 0;
        elements.sendBtn.disabled = !hasContent && !hasFiles;
    }

    /**************************************************************/
    /**
     * Auto-resizes the textarea based on content.
     *
     * @description
     * Grows the textarea without scrollbars until max-height is reached.
     * When max-height (50vh by default from CSS) is reached, enables
     * thin scrollbar by adding 'has-scroll' class.
     *
     * @example
     * // Called on input event
     * elements.messageInput.addEventListener('input', autoResizeTextarea);
     *
     * @see setupEventListeners - Attaches this to input events
     */
    /**************************************************************/
    function autoResizeTextarea() {
        const textarea = elements.messageInput;

        // Reset height to get accurate scrollHeight
        textarea.style.height = 'auto';

        // Get max-height from CSS (50vh default)
        const computedStyle = window.getComputedStyle(textarea);
        const maxHeight = parseInt(computedStyle.maxHeight, 10) || (window.innerHeight * 0.5);

        // Calculate new height
        const newHeight = textarea.scrollHeight;

        // Cap at max-height and enable scrolling
        if (newHeight >= maxHeight) {
            textarea.style.height = maxHeight + 'px';
            textarea.classList.add('has-scroll');
        } else {
            // Grow to fit without scrollbar
            textarea.style.height = newHeight + 'px';
            textarea.classList.remove('has-scroll');
        }
    }

    /**************************************************************/
    /**
     * Cancels the current request.
     *
     * @description
     * Aborts any in-flight request by calling abort() on the AbortController.
     * Updates loading state and UI accordingly.
     *
     * @example
     * // Called from cancel button click
     * UIHelpers.cancelRequest();
     *
     * @see ChatState.getAbortController - Gets the controller to abort
     */
    /**************************************************************/
    function cancelRequest() {
        const controller = ChatState.getAbortController();
        if (controller) {
            controller.abort();
        }
        ChatState.setLoading(false);
        updateUI();
    }

    /**************************************************************/
    /**
     * Clears the conversation and resets state.
     *
     * @description
     * Performs full conversation reset:
     * - Clears all messages from state
     * - Clears pending files
     * - Generates new conversation ID
     * - Clears markdown code block storage
     * - Re-renders empty message list
     * - Re-renders empty file list
     * - Hides file upload dropzone
     *
     * @example
     * // Called from clear button click
     * UIHelpers.clearConversation();
     *
     * @see ChatState.clearConversation - Clears state
     * @see MessageRenderer.renderMessages - Re-renders messages
     */
    /**************************************************************/
    function clearConversation() {
        // Clear state
        ChatState.clearConversation();

        // Clear markdown code block storage
        MarkdownRenderer.clearCodeBlockStorage();

        // Re-render UI
        MessageRenderer.renderMessages();
        FileHandler.renderFileList();
        FileHandler.hideFileUpload();
    }

    /**************************************************************/
    /**
     * Handles send button click or Enter key press.
     *
     * @description
     * Validates input and triggers the send callback if set.
     *
     * @see setOnSendMessage - Sets the callback
     */
    /**************************************************************/
    function handleSend() {
        if (onSendMessage) {
            onSendMessage();
        }
    }

    /**************************************************************/
    /**
     * Gets the current input value.
     *
     * @returns {string} Trimmed input text
     */
    /**************************************************************/
    function getInputValue() {
        return elements.messageInput.value.trim();
    }

    /**************************************************************/
    /**
     * Clears the input field.
     */
    /**************************************************************/
    function clearInput() {
        elements.messageInput.value = '';
        autoResizeTextarea();
    }

    /**************************************************************/
    /**
     * Sets the input field value.
     *
     * @param {string} value - Text to set in input
     *
     * @example
     * // From suggestion card click
     * UIHelpers.setInputValue('What are the side effects of aspirin?');
     */
    /**************************************************************/
    function setInputValue(value) {
        elements.messageInput.value = value;
        autoResizeTextarea();
        updateUI();
    }

    /**************************************************************/
    /**
     * Focuses the input field.
     */
    /**************************************************************/
    function focusInput() {
        elements.messageInput.focus();
    }

    /**************************************************************/
    /**
     * Shows the demo mode banner with a message.
     *
     * @param {string} message - Banner text to display
     *
     * @example
     * UIHelpers.showDemoBanner('DEMO MODE - Database resets periodically');
     *
     * @see ApiService.fetchSystemContext - Triggers banner display
     */
    /**************************************************************/
    function showDemoBanner(message) {
        elements.contextBanner.textContent = message;
        elements.contextBanner.style.display = 'block';
    }

    /**************************************************************/
    /**
     * Sets up all event listeners for the chat interface.
     *
     * @description
     * Configures event handlers for:
     * - Send button click
     * - Cancel button click
     * - Clear button click
     * - Attach button click
     * - Enter key to send (without Shift)
     * - Input changes for auto-resize and UI update
     * - Suggestion card clicks
     *
     * @example
     * // Called during initialization
     * UIHelpers.setupEventListeners();
     *
     * @see MedRecProChat.init - Calls during startup
     */
    /**************************************************************/
    function setupEventListeners() {
        // Send button
        elements.sendBtn.addEventListener('click', handleSend);

        // Cancel button
        elements.cancelBtn.addEventListener('click', cancelRequest);

        // Clear button
        elements.clearBtn.addEventListener('click', clearConversation);

        // Attach button
        elements.attachBtn.addEventListener('click', () => {
            FileHandler.toggleFileUpload();
        });

        // Message input - Enter to send (Shift+Enter for newline)
        elements.messageInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                handleSend();
            }
        });

        // Message input - auto-resize and UI update
        elements.messageInput.addEventListener('input', () => {
            autoResizeTextarea();
            updateUI();
        });

        // Suggestion cards
        document.querySelectorAll('.suggestion-card').forEach(card => {
            card.addEventListener('click', () => {
                setInputValue(card.dataset.suggestion);
                focusInput();
            });
        });
    }

    /**************************************************************/
    /**
     * Public API for the UI helpers module.
     *
     * @description
     * Exposes UI management functions and event setup.
     */
    /**************************************************************/
    return {
        // Initialization
        initElements: initElements,
        setupEventListeners: setupEventListeners,
        setOnSendMessage: setOnSendMessage,

        // UI updates
        updateUI: updateUI,
        autoResizeTextarea: autoResizeTextarea,
        showDemoBanner: showDemoBanner,

        // Request management
        cancelRequest: cancelRequest,
        clearConversation: clearConversation,

        // Input management
        getInputValue: getInputValue,
        clearInput: clearInput,
        setInputValue: setInputValue,
        focusInput: focusInput
    };
})();
