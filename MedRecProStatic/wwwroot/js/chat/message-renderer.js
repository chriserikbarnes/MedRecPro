/**************************************************************/
/**
 * MedRecPro Chat Message Rendering Module
 *
 * @fileoverview Renders chat messages to HTML for display in the chat interface.
 * Handles both user and assistant messages with various content types.
 *
 * @description
 * The message renderer module provides:
 * - Message HTML generation (user and assistant variants)
 * - Thinking block rendering with expand/collapse
 * - Progress indicator rendering for async operations
 * - Error state rendering with retry options
 * - File attachment display
 * - Message action buttons (copy, retry)
 * - Full message list rendering and individual message updates
 *
 * @example
 * import { MessageRenderer } from './message-renderer.js';
 *
 * // Render single message
 * const html = MessageRenderer.renderMessage(message);
 *
 * // Render all messages
 * MessageRenderer.renderMessages(messagesWrapper, emptyState);
 *
 * @module chat/message-renderer
 * @see MarkdownRenderer - Used for assistant message content
 * @see ChatState - Source of message data
 * @see ChatUtils - Utility functions for escaping and scrolling
 */
/**************************************************************/

import { ChatUtils } from './utils.js';
import { ChatState } from './state.js';
import { MarkdownRenderer } from './markdown.js';
import { ProgressiveConfig } from './progressive-config.js';

export const MessageRenderer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Cached DOM elements for message rendering.
     *
     * @description
     * Stores references to container elements to avoid repeated DOM queries.
     * Must be initialized by calling initElements().
     *
     * @type {Object}
     * @property {HTMLElement} messagesWrapper - Container for rendered messages
     * @property {HTMLElement} messagesContainer - Scrollable container for scroll behavior
     * @property {HTMLElement} emptyState - Empty state element shown when no messages
     *
     * @see initElements - Initializes these references
     */
    /**************************************************************/
    let elements = {
        messagesWrapper: null,
        messagesContainer: null,
        emptyState: null
    };

    /**************************************************************/
    /**
     * Initializes DOM element references for rendering.
     *
     * @param {Object} domElements - Object containing DOM element references
     * @param {HTMLElement} domElements.messagesWrapper - Container for messages
     * @param {HTMLElement} domElements.messagesContainer - Scrollable container
     * @param {HTMLElement} domElements.emptyState - Empty state placeholder
     *
     * @description
     * Must be called during application initialization before rendering.
     * Stores references to avoid repeated getElementById calls.
     *
     * @example
     * MessageRenderer.initElements({
     *     messagesWrapper: document.getElementById('messagesWrapper'),
     *     messagesContainer: document.getElementById('messagesContainer'),
     *     emptyState: document.getElementById('emptyState')
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
     * Renders a single message to HTML.
     *
     * @param {Object} message - Message object to render
     * @param {string} message.id - Unique message identifier
     * @param {string} message.role - 'user' or 'assistant'
     * @param {string} message.content - Message text content
     * @param {string} [message.thinking] - Assistant's thinking process (assistant only)
     * @param {Array} [message.files] - Attached files (user messages)
     * @param {number} [message.progress] - Progress value 0-1 for async operations
     * @param {string} [message.progressStatus] - Status text for progress indicator
     * @param {boolean} [message.isStreaming] - True if message is still being generated
     * @param {string} [message.error] - Error message if request failed
     * @returns {string} HTML string for the message
     *
     * @description
     * Generates complete message HTML including:
     * - Avatar (user 'U' or assistant 'AI')
     * - Thinking block (collapsible, assistant only)
     * - File attachments (if present)
     * - Message content (escaped for user, markdown for assistant)
     * - Progress indicator (during async operations)
     * - Streaming indicator (during generation)
     * - Error banner (if failed)
     * - Action buttons (copy, retry)
     *
     * @example
     * const userMsgHtml = renderMessage({
     *     id: 'abc-123',
     *     role: 'user',
     *     content: 'What is aspirin?'
     * });
     *
     * const assistantMsgHtml = renderMessage({
     *     id: 'def-456',
     *     role: 'assistant',
     *     content: 'Aspirin is...',
     *     thinking: 'User wants drug information...'
     * });
     *
     * @see renderMessages - Renders all messages using this function
     * @see MarkdownRenderer.render - Used for assistant content
     */
    /**************************************************************/
    function renderMessage(message) {
        // Determine message variant styling
        const isUser = message.role === 'user';
        const avatarClass = isUser ? 'avatar-user' : 'avatar-assistant';
        const avatarText = isUser ? 'U' : '<img src="/favicon.svg" alt="AI" class="avatar-img" />';
        const bubbleClass = isUser ? 'bubble-user' : 'bubble-assistant';

        // Build thinking block HTML (assistant messages only)
        let thinkingHtml = '';
        if (!isUser && message.thinking) {
            // Show animated "Thinking..." during generation
            const isThinking = message.isStreaming && !message.content;
            thinkingHtml = `
                <div class="thinking-block">
                    <div class="thinking-header" onclick="MedRecProChat.toggleThinking(this)">
                        <svg class="thinking-icon icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <polyline points="9 18 15 12 9 6"></polyline>
                        </svg>
                        <span class="thinking-label">${isThinking ? 'Thinking...' : 'View thinking process'}</span>
                        ${isThinking ? renderThinkingSpinner() : ''}
                    </div>
                    <div class="thinking-content">${ChatUtils.escapeHtml(message.thinking)}</div>
                </div>
            `;
        }

        // Build file attachments HTML
        let filesHtml = '';
        if (message.files && message.files.length > 0) {
            filesHtml = `
                <div class="file-attachments">
                    ${message.files.map(f => `
                        <div class="file-badge">
                            <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
                            </svg>
                            ${ChatUtils.escapeHtml(f.name)}
                        </div>
                    `).join('')}
                </div>
            `;
        }

        // Build message content HTML
        // User messages are escaped; assistant messages use markdown rendering
        let contentHtml = '';
        if (isUser) {
            contentHtml = `<div class="message-text">${ChatUtils.escapeHtml(message.content)}</div>`;
        } else {
            contentHtml = `<div class="markdown-content">${MarkdownRenderer.render(message.content)}</div>`;
        }

        // Build progress indicator HTML (for async operations like file import)
        let progressHtml = '';
        if (message.progress !== undefined && message.progress < 1) {
            // Check if we should show detailed progress with product names
            if (ProgressiveConfig.isDetailedProgressEnabled() && message.progressItems && message.progressItems.length > 0) {
                progressHtml = renderDetailedProgress(message);
            } else {
                progressHtml = renderProgressIndicator(message);
            }
        }

        // Build checkpoint UI HTML (for progressive response checkpoints)
        let checkpointHtml = '';
        if (message.checkpointUI) {
            checkpointHtml = message.checkpointUI;
        }

        // Build streaming indicator HTML (during response generation)
        // Uses animated spinner SVG for better visibility
        let streamingHtml = '';
        if (message.isStreaming && !progressHtml) {
            streamingHtml = `
                <div class="streaming-indicator streaming-indicator-visible">
                    <svg class="streaming-spinner icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <line x1="12" y1="2" x2="12" y2="6"></line>
                        <line x1="12" y1="18" x2="12" y2="22"></line>
                        <line x1="4.93" y1="4.93" x2="7.76" y2="7.76"></line>
                        <line x1="16.24" y1="16.24" x2="19.07" y2="19.07"></line>
                        <line x1="2" y1="12" x2="6" y2="12"></line>
                        <line x1="18" y1="12" x2="22" y2="12"></line>
                        <line x1="4.93" y1="19.07" x2="7.76" y2="16.24"></line>
                        <line x1="16.24" y1="7.76" x2="19.07" y2="4.93"></line>
                    </svg>
                    <span>Generating...</span>
                </div>
            `;
        }

        // Build error banner HTML (for failed requests)
        let errorHtml = '';
        if (message.error) {
            errorHtml = `
                <div class="error-banner">
                    <span class="error-text">${ChatUtils.escapeHtml(message.error)}</span>
                </div>
            `;
        }

        // Build action buttons HTML (copy, retry - assistant messages only)
        let actionsHtml = '';
        if (!isUser && !message.isStreaming) {
            actionsHtml = `
                <div class="message-actions">
                    <button class="action-btn" onclick="MedRecProChat.copyMessage('${message.id}')" title="Copy message">
                        <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                        </svg>
                    </button>
                    ${message.error ? renderRetryButton(message.id) : ''}
                </div>
            `;
        }

        // Assemble complete message HTML
        return `
            <div class="message ${isUser ? 'message-user' : ''}" data-message-id="${message.id}">
                <div class="message-avatar ${avatarClass}">${avatarText}</div>
                <div class="message-content">
                    <div class="message-bubble ${bubbleClass}">
                        ${thinkingHtml}
                        ${filesHtml}
                        ${contentHtml}
                        ${progressHtml}
                        ${checkpointHtml}
                        ${streamingHtml}
                        ${errorHtml}
                    </div>
                    ${actionsHtml}
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders the thinking spinner SVG.
     *
     * @returns {string} SVG HTML for animated spinner
     *
     * @description
     * Creates a loading spinner shown in the thinking block header
     * while the assistant is actively processing.
     *
     * @see renderMessage - Uses in thinking block
     */
    /**************************************************************/
    function renderThinkingSpinner() {
        return `
            <svg class="thinking-spinner icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <line x1="12" y1="2" x2="12" y2="6"></line>
                <line x1="12" y1="18" x2="12" y2="22"></line>
                <line x1="4.93" y1="4.93" x2="7.76" y2="7.76"></line>
                <line x1="16.24" y1="16.24" x2="19.07" y2="19.07"></line>
                <line x1="2" y1="12" x2="6" y2="12"></line>
                <line x1="18" y1="12" x2="22" y2="12"></line>
                <line x1="4.93" y1="19.07" x2="7.76" y2="16.24"></line>
                <line x1="16.24" y1="7.76" x2="19.07" y2="4.93"></line>
            </svg>
        `;
    }

    /**************************************************************/
    /**
     * Renders a progress indicator for async operations.
     *
     * @param {Object} message - Message with progress state
     * @param {number} message.progress - Progress value 0-1
     * @param {string} message.progressStatus - Status description text
     * @returns {string} HTML for animated progress indicator
     *
     * @description
     * Shows a circular progress ring with status text during
     * long-running operations like file imports.
     *
     * @see renderMessage - Uses for progress display
     */
    /**************************************************************/
    function renderProgressIndicator(message) {
        return `
            <div class="progress-indicator" style="min-width: 320px;">
                <div class="progress-ring-animated">
                    <svg width="44" height="44" viewBox="0 0 44 44">
                        <defs>
                            <linearGradient id="progressGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                                <stop offset="0%" style="stop-color:#3b82f6;stop-opacity:1" />
                                <stop offset="50%" style="stop-color:#8b5cf6;stop-opacity:1" />
                                <stop offset="100%" style="stop-color:#3b82f6;stop-opacity:0.3" />
                            </linearGradient>
                        </defs>
                        <circle class="progress-ring-track" cx="22" cy="22" r="16"
                            fill="none" stroke="rgba(255,255,255,0.1)" stroke-width="3"></circle>
                        <circle class="progress-ring-spinner" cx="22" cy="22" r="16"
                            fill="none" stroke="url(#progressGradient)" stroke-width="3"
                            stroke-linecap="round" stroke-dasharray="60 40"></circle>
                    </svg>
                </div>
                <div class="progress-info" style="min-width: 240px;">
                    <div class="progress-status-text">${ChatUtils.escapeHtml(message.progressStatus || 'Processing...')}</div>
                    <div class="progress-hint">Please wait</div>
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders a detailed progress indicator with product names.
     *
     * @param {Object} message - Message with progress state
     * @param {number} message.progress - Progress value 0-1
     * @param {string} message.progressStatus - Status description text
     * @param {Array} message.progressItems - Array of completed progress items
     * @param {string} [message.currentProductName] - Currently processing product name
     * @returns {string} HTML for detailed progress indicator
     *
     * @description
     * Shows progress with a list of completed items and the current item being processed.
     * Each item shows a checkmark or X icon based on success status.
     *
     * @see renderMessage - Uses for detailed progress display
     * @see ProgressiveConfig.getMaxProgressItems - Controls max items shown
     */
    /**************************************************************/
    function renderDetailedProgress(message) {
        const progressItems = message.progressItems || [];
        const maxItems = ProgressiveConfig.getMaxProgressItems();
        const recentItems = progressItems.slice(-maxItems).reverse();
        const hiddenCount = progressItems.length - recentItems.length;

        let itemsHtml = '';

        // Current item being processed
        if (message.currentProductName) {
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
                    <span class="progress-item-name">${ChatUtils.escapeHtml(message.currentProductName)}</span>
                </div>
            `;
        }

        // Completed items
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

        // Hidden count
        if (hiddenCount > 0) {
            itemsHtml += `
                <div class="progress-item progress-item-more">
                    <span>+ ${hiddenCount} more completed</span>
                </div>
            `;
        }

        return `
            <div class="progress-indicator detailed-progress" style="min-width: 320px;">
                <div class="progress-ring-animated">
                    <svg width="44" height="44" viewBox="0 0 44 44">
                        <defs>
                            <linearGradient id="progressGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                                <stop offset="0%" style="stop-color:#3b82f6;stop-opacity:1" />
                                <stop offset="50%" style="stop-color:#8b5cf6;stop-opacity:1" />
                                <stop offset="100%" style="stop-color:#3b82f6;stop-opacity:0.3" />
                            </linearGradient>
                        </defs>
                        <circle class="progress-ring-track" cx="22" cy="22" r="16"
                            fill="none" stroke="rgba(255,255,255,0.1)" stroke-width="3"></circle>
                        <circle class="progress-ring-spinner" cx="22" cy="22" r="16"
                            fill="none" stroke="url(#progressGradient)" stroke-width="3"
                            stroke-linecap="round" stroke-dasharray="60 40"></circle>
                    </svg>
                </div>
                <div class="progress-info detailed" style="min-width: 280px;">
                    <div class="progress-status-text">${ChatUtils.escapeHtml(message.progressStatus || 'Fetching data...')}</div>
                    <div class="progress-details-list">
                        ${itemsHtml}
                    </div>
                </div>
            </div>
        `;
    }

    /**************************************************************/
    /**
     * Renders a retry button for failed messages.
     *
     * @param {string} messageId - ID of the failed message
     * @returns {string} HTML for retry button
     *
     * @see renderMessage - Uses for error state
     * @see MedRecProChat.retryMessage - Handler for retry clicks
     */
    /**************************************************************/
    function renderRetryButton(messageId) {
        return `
            <button class="action-btn" onclick="MedRecProChat.retryMessage('${messageId}')" title="Retry">
                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="23 4 23 10 17 10"></polyline>
                    <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
                </svg>
            </button>
        `;
    }

    /**************************************************************/
    /**
     * Renders all messages to the DOM.
     *
     * @description
     * Replaces the entire messages container content with fresh renders
     * of all messages from state. Toggles between empty state and
     * messages wrapper based on message count.
     *
     * @example
     * // After state changes
     * MessageRenderer.renderMessages();
     *
     * @see renderMessage - Renders individual messages
     * @see ChatState.getMessages - Source of message data
     */
    /**************************************************************/
    function renderMessages() {
        const messages = ChatState.getMessages();

        if (messages.length === 0) {
            // Show empty state, hide messages
            elements.emptyState.style.display = 'flex';
            elements.messagesWrapper.style.display = 'none';
        } else {
            // Show messages, hide empty state
            elements.emptyState.style.display = 'none';
            elements.messagesWrapper.style.display = 'block';
            elements.messagesWrapper.innerHTML = messages.map(renderMessage).join('');
        }

        // Scroll to bottom to show latest content
        ChatUtils.scrollToBottom(elements.messagesContainer);
    }

    /**************************************************************/
    /**
     * Updates a specific message in the DOM.
     *
     * @param {string} messageId - The message ID to update
     *
     * @description
     * Optimized update that:
     * 1. First tries to update just the progress status text (minimal DOM change)
     * 2. Falls back to full message re-render if needed
     *
     * This optimization reduces flickering during rapid progress updates.
     *
     * @example
     * // After updating message state
     * ChatState.updateMessage(id, { progress: 0.5 });
     * MessageRenderer.updateMessage(id);
     *
     * @see ChatState.updateMessage - Updates message data
     */
    /**************************************************************/
    function updateMessage(messageId) {
        console.log('[MessageRenderer.updateMessage] Called with ID:', messageId);

        const message = ChatState.getMessageById(messageId);
        if (!message) {
            console.log('[MessageRenderer.updateMessage] Message not found in state');
            return;
        }

        console.log('[MessageRenderer.updateMessage] Message state:', {
            isStreaming: message.isStreaming,
            hasContent: !!message.content,
            contentLength: message.content?.length,
            progressStatus: message.progressStatus
        });

        const msgEl = document.querySelector(`[data-message-id="${messageId}"]`);
        console.log('[MessageRenderer.updateMessage] DOM element found:', !!msgEl);

        if (msgEl) {
            // Optimization: try to update just progress status to avoid flickering
            const statusTextEl = msgEl.querySelector('.progress-status-text');
            console.log('[MessageRenderer.updateMessage] Status text element found:', !!statusTextEl);

            if (statusTextEl && message.progressStatus && message.isStreaming) {
                console.log('[MessageRenderer.updateMessage] Using optimization path (status text only)');
                statusTextEl.textContent = message.progressStatus;
                return;
            }

            console.log('[MessageRenderer.updateMessage] Doing full rebuild');
            // Full rebuild for content changes
            msgEl.outerHTML = renderMessage(message);
            console.log('[MessageRenderer.updateMessage] Rebuild complete');
        }

        // Ensure latest content is visible
        ChatUtils.scrollToBottom(elements.messagesContainer);
    }

    /**************************************************************/
    /**
     * Toggles the thinking block expansion state.
     *
     * @param {HTMLElement} header - The thinking header element that was clicked
     *
     * @description
     * Expands or collapses the thinking content block by toggling
     * CSS classes on the icon and content elements.
     *
     * @example
     * // Called from onclick handler in thinking block
     * <div class="thinking-header" onclick="MedRecProChat.toggleThinking(this)">
     *
     * @see renderMessage - Creates thinking blocks with this handler
     */
    /**************************************************************/
    function toggleThinking(header) {
        const icon = header.querySelector('.thinking-icon');
        const content = header.nextElementSibling;

        // Toggle expanded state
        icon.classList.toggle('expanded');
        content.classList.toggle('expanded');
    }

    /**************************************************************/
    /**
     * Copies a message's content to clipboard.
     *
     * @param {string} messageId - The message ID to copy
     *
     * @description
     * Copies the message content (and thinking if present) to clipboard.
     * Shows visual feedback on the copy button.
     *
     * @example
     * // Called from action button onclick
     * MedRecProChat.copyMessage('abc-123');
     *
     * @see renderMessage - Creates copy buttons with this handler
     */
    /**************************************************************/
    function copyMessage(messageId) {
        const message = ChatState.getMessageById(messageId);
        if (!message) return;

        // Build text to copy (include thinking if present)
        let text = message.content;
        if (message.thinking) {
            text += '\n\n[Thinking]\n' + message.thinking;
        }

        // Copy to clipboard
        navigator.clipboard.writeText(text).then(() => {
            // Show success state on button
            const btn = document.querySelector(`[data-message-id="${messageId}"] .action-btn`);
            if (btn) {
                btn.classList.add('success');
                setTimeout(() => btn.classList.remove('success'), 2000);
            }
        }).catch(err => {
            console.error('[MessageRenderer] Clipboard write failed:', err);
        });
    }

    /**************************************************************/
    /**
     * Public API for the message rendering module.
     *
     * @description
     * Exposes rendering functions and interaction handlers.
     */
    /**************************************************************/
    return {
        // Initialization
        initElements: initElements,

        // Rendering
        renderMessage: renderMessage,
        renderMessages: renderMessages,
        updateMessage: updateMessage,
        renderDetailedProgress: renderDetailedProgress,

        // Interaction handlers
        toggleThinking: toggleThinking,
        copyMessage: copyMessage
    };
})();
