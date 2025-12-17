/**************************************************************/
// MedRecPro AI Chat Interface - Vanilla JavaScript Implementation
// Provides natural language interaction with the MedRecPro API
/**************************************************************/

(function () {
    'use strict';

    /**************************************************************/
    // Configuration
    // <remarks>
    // Determines API base URL based on environment:
    // - Local development (localhost): Uses production API with CORS
    // - Production: Uses relative URLs for same-origin requests
    // </remarks>
    /**************************************************************/

    /**
     * Detects if the application is running in a local development environment.
     * @returns {boolean} True if running on localhost or local IP
     */
    function isLocalDevelopment() {

        const hostname = window.location.hostname;
        return hostname === 'localhost' ||
            hostname === '127.0.0.1' ||
            hostname.startsWith('192.168.') ||
            hostname.startsWith('10.') ||
            hostname === '::1';

    }

    /**
     * Builds the API configuration based on the current environment.
     * @returns {Object} API configuration object with baseUrl and endpoints
     * <remarks>
     * When running locally, all API calls are routed to the production server
     * at https://www.medrecpro.com. CORS must be configured on the server
     * to accept requests from localhost origins.
     * </remarks>
     */
    function buildApiConfig() {

        // Base URL differs by environment
        const baseUrl = isLocalDevelopment()
            ? 'http://localhost:5093'       // Production API for local dev (requires CORS)
            : '';                           // Relative URLs for production (same-origin)

        return {
            baseUrl: baseUrl,
            endpoints: {
                // AI Controller endpoints
                context: '/api/Ai/context',
                interpret: '/api/Ai/interpret',
                synthesize: '/api/Ai/synthesize',
                retry: '/api/Ai/retry',
                chat: '/api/Ai/chat',
                // Labels Controller endpoint for file upload
                upload: '/api/Label/import'
            },
            pollInterval: 1000,
            maxRetryAttempts: 3
        };

    }

    // Initialize API configuration
    const API_CONFIG = buildApiConfig();

    // Log environment info for debugging
    console.log('[MedRecPro Chat] Environment:', isLocalDevelopment() ? 'Local Development' : 'Production');
    console.log('[MedRecPro Chat] API Base URL:', API_CONFIG.baseUrl || '(relative)');

    /**************************************************************/
    // State Management
    /**************************************************************/
    const state = {
        messages: [],
        files: [],
        isLoading: false,
        showFileUpload: false,
        systemContext: null,
        conversationId: generateUUID(),
        abortController: null,
        currentProgressCallback: null  // For real-time import progress updates
    };

    /**************************************************************/
    // DOM Elements
    /**************************************************************/
    const elements = {
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

    /**************************************************************/
    // Utility Functions
    /**************************************************************/

    /**
     * Generates a UUID v4 for conversation tracking.
     * @returns {string} UUID string
     */
    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    /**
     * Builds a full API URL from an endpoint path.
     * @param {string} endpointPath - The endpoint path (e.g., '/api/Ai/context')
     * @returns {string} Full URL for the API call
     * <remarks>
     * Combines the base URL with the endpoint path.
     * In production (empty baseUrl), returns just the endpoint for same-origin requests.
     * In local development, prepends the production server URL.
     * </remarks>
     */
    function buildUrl(endpointPath) {

        return API_CONFIG.baseUrl + endpointPath;

    }

    /**
     * Gets fetch options with credentials included for cookie-based auth.
     * @param {Object} options - Additional fetch options to merge
     * @returns {Object} Fetch options with credentials: 'include'
     * <remarks>
     * Ensures cookies are sent with all API requests, required for
     * authentication to work in both local and production environments.
     * </remarks>
     */
    function getFetchOptions(options = {}) {
        return {
            credentials: 'include',
            ...options
        };
    }

    /**
     * Escapes HTML special characters to prevent XSS.
     * @param {string} text - Raw text to escape
     * @returns {string} Escaped HTML string
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Formats file size for display.
     * @param {number} bytes - File size in bytes
     * @returns {string} Formatted size string
     */
    function formatFileSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    /**
     * Scrolls the messages container to the bottom.
     */
    function scrollToBottom() {
        requestAnimationFrame(() => {
            elements.messagesContainer.scrollTop = elements.messagesContainer.scrollHeight;
        });
    }

    /**************************************************************/
    // Markdown Rendering
    /**************************************************************/

    /**
     * Renders markdown content to HTML with proper escaping.
     * @param {string} text - Markdown text to render
     * @returns {string} HTML string
     */
    function renderMarkdown(text) {
        if (!text) return '';

        let html = escapeHtml(text);
        const codeBlocks = [];

        // Extract and preserve code blocks
        html = html.replace(/```(\w*)\n([\s\S]*?)```/g, (match, lang, code) => {
            const index = codeBlocks.length;
            codeBlocks.push({ lang: lang || 'code', code: code.trim() });
            return `__CODE_BLOCK_${index}__`;
        });

        // Process inline elements
        // Bold
        html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        // Italic
        html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');
        // Inline code
        html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
        // Links
        html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');

        // Process block elements
        const lines = html.split('\n');
        const processed = [];
        let inList = false;
        let listItems = [];

        function flushList() {
            if (listItems.length > 0) {
                processed.push('<ul style="padding-left:15px;margin:0.25rem 0;">' + listItems.join('') + '</ul>');
                listItems = [];
                inList = false;
            }
        }

        for (const line of lines) {
            // Headers
            if (line.startsWith('### ')) {
                flushList();
                processed.push('<h3>' + line.slice(4) + '</h3>');
            } else if (line.startsWith('## ')) {
                flushList();
                processed.push('<h2>' + line.slice(3) + '</h2>');
            } else if (line.startsWith('# ')) {
                flushList();
                processed.push('<h1>' + line.slice(2) + '</h1>');
            }
            // Horizontal rule
            else if (/^[-*_]{3,}$/.test(line.trim())) {
                flushList();
                processed.push('<hr>');
            }
            // Blockquote
            else if (line.startsWith('&gt; ')) {
                flushList();
                processed.push('<blockquote>' + line.slice(5) + '</blockquote>');
            }
            // Unordered list
            else if (/^[-*]\s/.test(line)) {
                inList = true;
                listItems.push('<li>' + line.slice(2) + '</li>');
            }
            // Numbered list
            else if (/^\d+\.\s/.test(line)) {
                flushList();
                processed.push('<div style="display:flex;gap:0.5rem;margin:0.25rem 0;"><span style="color:var(--color-text-muted);font-family:var(--font-mono);font-size:0.875rem;">' +
                    line.match(/^\d+/)[0] + '.</span><span>' + line.replace(/^\d+\.\s/, '') + '</span></div>');
            }
            // Code block placeholder
            else if (line.includes('__CODE_BLOCK_')) {
                flushList();
                processed.push(line);
            }
            // Empty line
            else if (line.trim() === '') {
                flushList();
                processed.push('<div style="height:0.5rem;"></div>');
            }
            // Regular paragraph
            else {
                flushList();
                processed.push('<p>' + line + '</p>');
            }
        }
        flushList();

        html = processed.join('');

        // Restore code blocks with proper formatting
        codeBlocks.forEach((block, index) => {
            const codeBlockHtml = `
                        <div class="code-block">
                            <div class="code-header">
                                <span class="code-language">${escapeHtml(block.lang)}</span>
                                <button class="code-copy-btn" onclick="copyCode(this, '${index}')">
                                    <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                                    </svg>
                                    Copy
                                </button>
                            </div>
                            <div class="code-content">
                                <pre>${escapeHtml(block.code)}</pre>
                            </div>
                        </div>
                    `;
            html = html.replace(`__CODE_BLOCK_${index}__`, codeBlockHtml);
        });

        return html;
    }

    // Store code blocks for copy functionality
    window.codeBlockStorage = [];

    /**
     * Copies code block content to clipboard.
     * @param {HTMLElement} btn - The copy button element
     * @param {string} index - Code block index
     */
    window.copyCode = function (btn, index) {
        const codeContent = btn.closest('.code-block').querySelector('pre').textContent;
        navigator.clipboard.writeText(codeContent).then(() => {
            const originalHtml = btn.innerHTML;
            btn.classList.add('copied');
            btn.innerHTML = `
                        <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                            <polyline points="20 6 9 17 4 12"></polyline>
                        </svg>
                        Copied!
                    `;
            setTimeout(() => {
                btn.classList.remove('copied');
                btn.innerHTML = originalHtml;
            }, 2000);
        });
    };

    /**************************************************************/
    // Message Rendering
    /**************************************************************/

    /**
     * Renders a single message to HTML.
     * @param {Object} message - Message object
     * @returns {string} HTML string for the message
     */
    function renderMessage(message) {
        const isUser = message.role === 'user';
        const avatarClass = isUser ? 'avatar-user' : 'avatar-assistant';
        const avatarText = isUser ? 'U' : 'AI';
        const bubbleClass = isUser ? 'bubble-user' : 'bubble-assistant';

        let thinkingHtml = '';
        if (!isUser && message.thinking) {
            const isThinking = message.isStreaming && !message.content;
            thinkingHtml = `
                        <div class="thinking-block">
                            <div class="thinking-header" onclick="toggleThinking(this)">
                                <svg class="thinking-icon icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="9 18 15 12 9 6"></polyline>
                                </svg>
                                <span class="thinking-label">${isThinking ? 'Thinking...' : 'View thinking process'}</span>
                                ${isThinking ? '<svg class="thinking-spinner icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="2" x2="12" y2="6"></line><line x1="12" y1="18" x2="12" y2="22"></line><line x1="4.93" y1="4.93" x2="7.76" y2="7.76"></line><line x1="16.24" y1="16.24" x2="19.07" y2="19.07"></line><line x1="2" y1="12" x2="6" y2="12"></line><line x1="18" y1="12" x2="22" y2="12"></line><line x1="4.93" y1="19.07" x2="7.76" y2="16.24"></line><line x1="16.24" y1="7.76" x2="19.07" y2="4.93"></line></svg>' : ''}
                            </div>
                            <div class="thinking-content">${escapeHtml(message.thinking)}</div>
                        </div>
                    `;
        }

        let filesHtml = '';
        if (message.files && message.files.length > 0) {
            filesHtml = `
                        <div class="file-attachments">
                            ${message.files.map(f => `
                                <div class="file-badge">
                                    <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                        <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
                                    </svg>
                                    ${escapeHtml(f.name)}
                                </div>
                            `).join('')}
                        </div>
                    `;
        }

        let contentHtml = '';
        if (isUser) {
            contentHtml = `<div class="message-text">${escapeHtml(message.content)}</div>`;
        } else {
            contentHtml = `<div class="markdown-content">${renderMarkdown(message.content)}</div>`;
        }

        let progressHtml = '';
        if (message.progress !== undefined && message.progress < 1) {
            progressHtml = `
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
                                <div class="progress-status-text">${escapeHtml(message.progressStatus || 'Processing...')}</div>
                                <div class="progress-hint">Please wait</div>
                            </div>
                        </div>
                    `;
        }

        let streamingHtml = '';
        if (message.isStreaming && !progressHtml) {
            streamingHtml = `
                        <div class="streaming-indicator">
                            <div class="streaming-dot"></div>
                            <span>Generating...</span>
                        </div>
                    `;
        }

        let errorHtml = '';
        if (message.error) {
            errorHtml = `
                        <div class="error-banner">
                            <span class="error-text">${escapeHtml(message.error)}</span>
                        </div>
                    `;
        }

        let actionsHtml = '';
        if (!isUser && !message.isStreaming) {
            actionsHtml = `
                        <div class="message-actions">
                            <button class="action-btn" onclick="copyMessage('${message.id}')" title="Copy message">
                                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                    <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                                </svg>
                            </button>
                            ${message.error ? `
                                <button class="action-btn" onclick="retryMessage('${message.id}')" title="Retry">
                                    <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                        <polyline points="23 4 23 10 17 10"></polyline>
                                        <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
                                    </svg>
                                </button>
                            ` : ''}
                        </div>
                    `;
        }

        return `
                    <div class="message ${isUser ? 'message-user' : ''}" data-message-id="${message.id}">
                        <div class="message-avatar ${avatarClass}">${avatarText}</div>
                        <div class="message-content">
                            <div class="message-bubble ${bubbleClass}">
                                ${thinkingHtml}
                                ${filesHtml}
                                ${contentHtml}
                                ${progressHtml}
                                ${streamingHtml}
                                ${errorHtml}
                            </div>
                            ${actionsHtml}
                        </div>
                    </div>
                `;
    }

    /**
     * Toggles the thinking block expansion state.
     * @param {HTMLElement} header - The thinking header element
     */
    window.toggleThinking = function (header) {
        const icon = header.querySelector('.thinking-icon');
        const content = header.nextElementSibling;
        icon.classList.toggle('expanded');
        content.classList.toggle('expanded');
    };

    /**
     * Copies a message's content to clipboard.
     * @param {string} messageId - The message ID
     */
    window.copyMessage = function (messageId) {
        const message = state.messages.find(m => m.id === messageId);
        if (message) {
            let text = message.content;
            if (message.thinking) {
                text += '\n\n[Thinking]\n' + message.thinking;
            }
            navigator.clipboard.writeText(text).then(() => {
                const btn = document.querySelector(`[data-message-id="${messageId}"] .action-btn`);
                if (btn) {
                    btn.classList.add('success');
                    setTimeout(() => btn.classList.remove('success'), 2000);
                }
            });
        }
    };

    /**
     * Retries a failed message.
     * @param {string} messageId - The failed message ID
     */
    window.retryMessage = function (messageId) {
        const messageIndex = state.messages.findIndex(m => m.id === messageId);
        if (messageIndex === -1) return;

        // Find the preceding user message
        let userMessageIndex = messageIndex - 1;
        while (userMessageIndex >= 0 && state.messages[userMessageIndex].role !== 'user') {
            userMessageIndex--;
        }

        if (userMessageIndex >= 0) {
            const userMessage = state.messages[userMessageIndex];
            // Remove the failed message
            state.messages = state.messages.filter(m => m.id !== messageId);
            renderMessages();
            // Resend
            elements.messageInput.value = userMessage.content;
            sendMessage();
        }
    };

    /**
     * Renders all messages to the DOM.
     */
    function renderMessages() {
        if (state.messages.length === 0) {
            elements.emptyState.style.display = 'flex';
            elements.messagesWrapper.style.display = 'none';
        } else {
            elements.emptyState.style.display = 'none';
            elements.messagesWrapper.style.display = 'block';
            elements.messagesWrapper.innerHTML = state.messages.map(renderMessage).join('');
        }
        scrollToBottom();
    }

    /**
     * Updates a specific message in the DOM.
     * @param {string} messageId - The message ID to update
     */
    function updateMessage(messageId) {
        const message = state.messages.find(m => m.id === messageId);
        if (!message) return;

        const msgEl = document.querySelector(`[data-message-id="${messageId}"]`);
        if (msgEl) {
            // Try to update just the progress status text to avoid flickering
            const statusTextEl = msgEl.querySelector('.progress-status-text');
            if (statusTextEl && message.progressStatus && message.isStreaming) {
                statusTextEl.textContent = message.progressStatus;
                return;
            }
            // Full rebuild for content changes
            msgEl.outerHTML = renderMessage(message);
        }
        scrollToBottom();
    }

    /**************************************************************/
    // File Handling
    /**************************************************************/

    /**
     * Renders the file list in the dropzone.
     */
    function renderFileList() {
        if (state.files.length === 0) {
            elements.fileList.innerHTML = '';
            elements.attachBadge.style.display = 'none';
        } else {
            elements.fileList.innerHTML = state.files.map((file, index) => `
                        <div class="file-item">
                            <svg class="file-icon icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
                            </svg>
                            <span class="file-name">${escapeHtml(file.name)}</span>
                            <span class="file-size">(${formatFileSize(file.size)})</span>
                            <button class="file-remove" onclick="removeFile(${index})">
                                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <line x1="18" y1="6" x2="6" y2="18"></line>
                                    <line x1="6" y1="6" x2="18" y2="18"></line>
                                </svg>
                            </button>
                        </div>
                    `).join('');
            elements.attachBadge.textContent = state.files.length;
            elements.attachBadge.style.display = 'flex';
        }
    }

    /**
     * Removes a file from the upload list.
     * @param {number} index - File index to remove
     */
    window.removeFile = function (index) {
        state.files.splice(index, 1);
        renderFileList();
    };

    /**
     * Adds files to the upload list (ZIP only).
     * @param {FileList} files - Files to add
     */
    function addFiles(files) {
        const zipFiles = Array.from(files).filter(f =>
            f.type === 'application/zip' ||
            f.type === 'application/x-zip-compressed' ||
            f.name.endsWith('.zip')
        );
        if (zipFiles.length > 0) {
            state.files.push(...zipFiles);
            renderFileList();
        }
    }

    /**************************************************************/
    /**
     * Polls the import progress endpoint until the operation completes.
     * @param {string} progressUrl - The URL to poll for progress
     * @param {number} maxWaitMs - Maximum time to wait (default: 60 seconds)
     * @returns {Promise<Object>} The final import status with results
     * <remarks>
     * Uses exponential backoff starting at pollInterval (1 second).
     * Stops polling when status is "Completed", "Failed", or "Canceled".
     * </remarks>
     */
    async function pollImportProgress(progressUrl, onProgress, maxWaitMs = 120000) {
        // #region implementation

        const startTime = Date.now();
        let pollDelay = API_CONFIG.pollInterval;

        while (Date.now() - startTime < maxWaitMs) {
            try {
                const response = await fetch(buildUrl(progressUrl), getFetchOptions({
                    signal: state.abortController?.signal
                }));

                if (!response.ok) {
                    console.warn('[MedRecPro Chat] Progress poll failed:', response.status);
                    await new Promise(r => setTimeout(r, pollDelay));
                    pollDelay = Math.min(pollDelay * 1.5, 5000);
                    continue;
                }

                const status = await response.json();
                console.log('[MedRecPro Chat] Import progress:', status.percentComplete + '%', status.status);

                // Call progress callback for UI updates
                if (onProgress) {
                    onProgress(status);
                }

                // Check for terminal states

                if (status.status === 'Completed' || status.status === 'Failed' || status.status === 'Canceled') {
                    return status;
                }

                await new Promise(r => setTimeout(r, pollDelay));
            } catch (error) {
                if (error.name === 'AbortError') throw error;
                console.warn('[MedRecPro Chat] Progress poll error:', error);
                await new Promise(r => setTimeout(r, pollDelay));
            }
        }

        return { status: 'Timeout', error: 'Import operation timed out' };

        // #endregion
    }

    /**************************************************************/
    /**
     * Uploads files to the server and waits for import completion.
     * @returns {Promise<Object>} Import result containing documentIds and status
     * <remarks>
     * Uses the buildUrl helper to construct the full URL.
     * Polls the progress endpoint to wait for async import completion.
     * Returns an object with importResults instead of just fileIds.
     * </remarks>
     */
    async function uploadFiles() {
        // #region implementation

        if (state.files.length === 0) {
            return { success: false, documentIds: [], message: 'No files to upload' };
        }

        const formData = new FormData();
        state.files.forEach(file => formData.append('files', file));

        const uploadUrl = buildUrl(API_CONFIG.endpoints.upload);
        console.log('[MedRecPro Chat] Uploading files to:', uploadUrl);

        const response = await fetch(uploadUrl, getFetchOptions({
            method: 'POST',
            body: formData,
            signal: state.abortController?.signal
        }));

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`File upload failed: ${response.status} - ${errorText}`);
        }

        const result = await response.json();
        console.log('[MedRecPro Chat] Upload response:', result);

        // Handle async import response (returns OperationId and ProgressUrl)
        if (result.operationId && result.progressUrl) {
            console.log('[MedRecPro Chat] Import operation queued:', result.operationId);

            //Poll for completion with progress callback
            const finalStatus = await pollImportProgress(result.progressUrl, state.currentProgressCallback);

            if (finalStatus.status === 'Completed' && finalStatus.results) {
                // Extract document IDs from the nested response structure
                // Response format: results[].fileResults[].splGUID
                const extracted = extractImportResults(finalStatus);

                return {
                    success: true,
                    documentIds: extracted.documentIds,
                    documentNames: extracted.documentNames,
                    statistics: extracted.statistics,
                    totalFilesProcessed: extracted.totalFilesProcessed,
                    totalFilesSucceeded: extracted.totalFilesSucceeded,
                    results: finalStatus.results,
                    message: `Successfully imported ${extracted.documentIds.length} document(s)`
                };
            } else if (finalStatus.status === 'Failed') {
                return {
                    success: false,
                    documentIds: [],
                    error: finalStatus.error || 'Import failed',
                    message: `Import failed: ${finalStatus.error}`
                };
            } else if (finalStatus.status === 'Canceled') {
                return {
                    success: false,
                    documentIds: [],
                    message: 'Import was canceled'
                };
            } else {
                return {
                    success: false,
                    documentIds: [],
                    operationId: result.operationId,
                    progressUrl: result.progressUrl,
                    message: 'Import is still processing. Check progress later.'
                };
            }
        }

        // Fallback for legacy response format
        if (result.fileIds) {
            return {
                success: true,
                documentIds: result.fileIds,
                message: `Files uploaded: ${result.fileIds.length}`
            };
        }

        console.warn('[MedRecPro Chat] Unexpected upload response format:', result);
        return {
            success: false,
            documentIds: [],
            rawResponse: result,
            message: 'Unexpected response from server'
        };

        // #endregion
    }

    /**************************************************************/
    // API Communication
    /**************************************************************/

    /**
     * Fetches the system context from the API.
     * <remarks>
     * Retrieves demo mode status and other system configuration.
     * Uses buildUrl helper for proper cross-origin handling.
     * </remarks>
     */
    async function fetchSystemContext() {

        try {
            const contextUrl = buildUrl(API_CONFIG.endpoints.context);
            console.log('[MedRecPro Chat] Fetching context from:', contextUrl);

            const response = await fetch(contextUrl, getFetchOptions());
            if (response.ok) {
                state.systemContext = await response.json();

                // Show demo mode banner if applicable
                if (state.systemContext.isDemoMode) {
                    elements.contextBanner.textContent = state.systemContext.demoModeMessage ||
                        '⚠️ DEMO MODE - Database resets periodically';
                    elements.contextBanner.style.display = 'block';
                }
            }
        } catch (error) {
            console.error('[MedRecPro Chat] Failed to fetch system context:', error);
            // In local dev, this might fail if CORS isn't configured - show helpful message
            if (isLocalDevelopment()) {
                console.warn('[MedRecPro Chat] CORS may not be configured on the server. ' +
                    'Ensure the API at medrecpro.com allows localhost origins.');
            }
        }

    }

    /**
     * Sends a message to the AI API.
     * <remarks>
     * Handles the full message flow:
     * 1. Creates user and assistant message placeholders
     * 2. Uploads any attached files
     * 3. Calls interpret endpoint for intent analysis
     * 4. Processes response and updates UI
     * </remarks>
     */
    async function sendMessage() {

        const input = elements.messageInput.value.trim();
        if (!input && state.files.length === 0) return;
        if (state.isLoading) return;

        // Create user message
        const userMessage = {
            id: generateUUID(),
            role: 'user',
            content: input,
            files: state.files.map(f => ({ name: f.name, size: f.size })),
            timestamp: new Date()
        };

        // Create assistant message placeholder
        const assistantMessage = {
            id: generateUUID(),
            role: 'assistant',
            content: '',
            thinking: '',
            isStreaming: true,
            timestamp: new Date()
        };

        state.messages.push(userMessage, assistantMessage);
        elements.messageInput.value = '';
        autoResizeTextarea();
        state.isLoading = true;
        updateUI();
        renderMessages();

        state.abortController = new AbortController();

        try {
            // Upload files if any
            let importResult = null;
            if (state.files.length > 0) {
                assistantMessage.progress = 0.05;
                assistantMessage.progressStatus = 'Uploading files...';
                updateMessage(assistantMessage.id);

                // Set up progress callback for real-time updates
                state.currentProgressCallback = (status) => {
                    const percent = status.percentComplete || 0;
                    assistantMessage.progress = percent / 100;
                    assistantMessage.progressStatus = status.status || 'Processing...';
                    updateMessage(assistantMessage.id);
                };

                importResult = await uploadFiles();
                state.currentProgressCallback = null; // Clear callback
                state.files = [];
                renderFileList();
                hideFileUpload();

                // Final status update
                if (importResult.success && importResult.documentIds.length > 0) {
                    assistantMessage.progress = 1.0;
                    assistantMessage.progressStatus = `Import complete: ${importResult.documentIds.length} document(s)`;
                } else if (importResult.success) {
                    assistantMessage.progressStatus = 'Import completed (no new documents)';
                } else {
                    assistantMessage.progressStatus = importResult.message || 'Import issue';
                }
                updateMessage(assistantMessage.id);
            }

            // Build conversation history for context
            const conversationHistory = state.messages
                .filter(m => m.id !== assistantMessage.id)
                .slice(-10)
                .map(m => ({ role: m.role, content: m.content }));

            // Build user message with import context if files were uploaded
            let enhancedUserMessage = input;
            if (importResult) {
                if (importResult.success && importResult.documentIds.length > 0) {
                    // Build statistics summary
                    const stats = importResult.statistics || {};
                    const statsText = Object.entries(stats)
                        .filter(([k, v]) => v > 0)
                        .map(([k, v]) => `${k}: ${v}`)
                        .join(', ');

                    enhancedUserMessage = `[IMPORT COMPLETED SUCCESSFULLY: Imported ${importResult.documentIds.length} document(s). Document GUIDs: ${importResult.documentIds.join(', ')}. Statistics: ${statsText || 'N/A'}]\n\nUser request: ${input || 'Please acknowledge the successful import and provide information about the imported documents.'}`;
                } else if (importResult.success) {
                    enhancedUserMessage = `[IMPORT COMPLETED: Files were processed but no new documents were created. ${importResult.message}]\n\nUser request: ${input}`;
                } else {
                    enhancedUserMessage = `[IMPORT ISSUE: ${importResult.message}]\n\nUser request: ${input}`;
                }
            }

            // Call the interpret endpoint
            const interpretUrl = buildUrl(API_CONFIG.endpoints.interpret);
            console.log('[MedRecPro Chat] Calling interpret endpoint:', interpretUrl);

            const interpretResponse = await fetch(interpretUrl, getFetchOptions({
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    userMessage: enhancedUserMessage,
                    conversationId: state.conversationId,
                    conversationHistory: conversationHistory,
                    importResult: importResult
                }),
                signal: state.abortController.signal
            }));

            if (!interpretResponse.ok) {
                throw new Error(`API error: ${interpretResponse.status}`);
            }

            const interpretation = await interpretResponse.json();

            // Normalize endpoint property name (backend may return 'endpoints' or 'suggestedEndpoints')
            if (interpretation.endpoints && !interpretation.suggestedEndpoints) {
                interpretation.suggestedEndpoints = interpretation.endpoints;
            }

            // Handle the interpretation response
            if (interpretation.thinking) {
                assistantMessage.thinking = interpretation.thinking;
                updateMessage(assistantMessage.id);
            }

            // Check if this is a direct response (no API call needed)
            if (interpretation.directResponse) {
                assistantMessage.content = interpretation.directResponse;
                assistantMessage.isStreaming = false;
                updateMessage(assistantMessage.id);
            }
            // Check if we need to execute API endpoints
            else if (interpretation.suggestedEndpoints && interpretation.suggestedEndpoints.length > 0) {
                assistantMessage.progress = 0;
                assistantMessage.progressStatus = 'Executing queries...';
                updateMessage(assistantMessage.id);

                // Execute endpoints with dependency support
                   const results = await executeEndpointsWithDependencies(
                       interpretation.suggestedEndpoints,
                       assistantMessage,
                       state.abortController
                   );

                /**************************************************************/
                // Enhanced Endpoint Execution with Dependency Support
                /**************************************************************/

                /**
                 * Gets a property value from an object using case-insensitive matching.
                 * Handles PascalCase (DocumentGuid) vs camelCase (documentGuid) differences.
                 * @param {Object} obj - The object to get the property from
                 * @param {string} propName - The property name to find (case-insensitive)
                 * @returns {*} The property value or undefined
                 */
                function getCaseInsensitiveProperty(obj, propName) {
                    if (!obj || typeof obj !== 'object') return undefined;

                    // Try exact match first (fastest)
                    if (obj.hasOwnProperty(propName)) {
                        return obj[propName];
                    }

                    // Try case-insensitive match
                    const lowerPropName = propName.toLowerCase();
                    for (const key of Object.keys(obj)) {
                        if (key.toLowerCase() === lowerPropName) {
                            console.log(`[MedRecPro Chat] Case-insensitive match: '${propName}' -> '${key}'`);
                            return obj[key];
                        }
                    }

                    return undefined;
                }

                /**
                 * Recursively searches for a property by name anywhere in the object hierarchy.
                 * Returns the first match found (depth-first search).
                 * @param {Object|Array} obj - The object/array to search in
                 * @param {string} propName - The property name to find (case-insensitive)
                 * @param {number} maxDepth - Maximum depth to search (default 10)
                 * @param {number} currentDepth - Current recursion depth
                 * @param {string} currentPath - Current path for logging
                 * @returns {*} The property value or undefined
                 */
                function findPropertyDeep(obj, propName, maxDepth = 10, currentDepth = 0, currentPath = '$') {
                    if (currentDepth > maxDepth) {
                        return undefined;
                    }

                    if (!obj || typeof obj !== 'object') {
                        return undefined;
                    }

                    // If it's an array, search each element
                    if (Array.isArray(obj)) {
                        for (let i = 0; i < obj.length; i++) {
                            const result = findPropertyDeep(obj[i], propName, maxDepth, currentDepth + 1, `${currentPath}[${i}]`);
                            if (result !== undefined) {
                                return result;
                            }
                        }
                        return undefined;
                    }

                    // Check if this object has the property (case-insensitive)
                    const lowerPropName = propName.toLowerCase();
                    for (const key of Object.keys(obj)) {
                        if (key.toLowerCase() === lowerPropName) {
                            console.log(`[MedRecPro Chat] Deep search: found '${propName}' at path '${currentPath}.${key}'`);
                            return obj[key];
                        }
                    }

                    // Recursively search nested objects
                    for (const key of Object.keys(obj)) {
                        const value = obj[key];
                        if (value && typeof value === 'object') {
                            const result = findPropertyDeep(value, propName, maxDepth, currentDepth + 1, `${currentPath}.${key}`);
                            if (result !== undefined) {
                                return result;
                            }
                        }
                    }

                    return undefined;
                }

                /**
                 * Extracts a value from an object using a path expression or deep search.
                 * 
                 * Supports:
                 * - Explicit path: "$[0].productsByIngredient.documentGUID"
                 * - Simple property with deep search: "$[0].documentGuid" (will search nested objects)
                 * - Property name only: "documentGuid" (deep searches entire object)
                 * 
                 * @param {Object|Array} data - The data to extract from
                 * @param {string} path - Path expression or property name
                 * @returns {*} The extracted value or undefined
                 */
                /**
                 * Extracts a value (or array of values) from data using a path.
                 * Supports array extraction with [] suffix: "documentGUID[]" extracts ALL values.
                 * @param {Object|Array} data - The data to extract from
                 * @param {string} path - JSONPath-like path, with optional [] suffix for array extraction
                 * @returns {*} The extracted value, array of values, or undefined
                 */
                function extractValueByPath(data, path) {
                    if (!data || !path) {
                        console.log('[MedRecPro Chat] extractValueByPath: data or path is null/undefined');
                        return undefined;
                    }

                    // Check for array extraction syntax (path ending with [])
                    const isArrayExtraction = path.endsWith('[]');
                    const cleanedPath = isArrayExtraction ? path.slice(0, -2) : path;

                    console.log(`[MedRecPro Chat] === EXTRACTING VALUE ===`);
                    console.log(`[MedRecPro Chat] Path: '${path}'${isArrayExtraction ? ' (ARRAY MODE - extracting ALL values)' : ''}`);
                    console.log(`[MedRecPro Chat] Data type: ${Array.isArray(data) ? 'array' : typeof data}`);

                    // If array extraction mode and data is an array, extract the field from EACH element
                    if (isArrayExtraction && Array.isArray(data)) {
                        const values = [];
                        for (let i = 0; i < data.length; i++) {
                            const value = extractSingleValue(data[i], cleanedPath);
                            if (value !== undefined && value !== null) {
                                values.push(value);
                            }
                        }
                        console.log(`[MedRecPro Chat] === ARRAY EXTRACTION RESULT: ${values.length} values ===`);
                        console.log(`[MedRecPro Chat] Values: [${values.slice(0, 5).join(', ')}${values.length > 5 ? '...' : ''}]`);
                        return values.length > 0 ? values : undefined;
                    }

                    // Standard single value extraction
                    return extractSingleValue(data, cleanedPath);
                }

                /**
                 * Extracts a single value from data using a path (internal helper).
                 * @param {Object|Array} data - The data to extract from
                 * @param {string} path - JSONPath-like path
                 * @returns {*} The extracted value or undefined
                 */
                function extractSingleValue(data, path) {
                    if (!data || !path) {
                        return undefined;
                    }

                    console.log(`[MedRecPro Chat] Data type: ${Array.isArray(data) ? 'array' : typeof data}`);

                    // Log structure for debugging
                    if (Array.isArray(data)) {
                        console.log(`[MedRecPro Chat] Array length: ${data.length}`);
                        if (data.length > 0) {
                            const firstKeys = Object.keys(data[0]);
                            console.log(`[MedRecPro Chat] First element keys: [${firstKeys.join(', ')}]`);
                            // Show nested structure if there's a wrapper object
                            for (const key of firstKeys) {
                                if (typeof data[0][key] === 'object' && data[0][key] !== null) {
                                    const nestedKeys = Object.keys(data[0][key]);
                                    console.log(`[MedRecPro Chat] Nested '${key}' keys: [${nestedKeys.slice(0, 5).join(', ')}${nestedKeys.length > 5 ? '...' : ''}]`);
                                }
                            }
                        }
                    }

                    // Remove leading $ if present
                    let cleanPath = path.startsWith('$') ? path.substring(1) : path;

                    // Split by . and []
                    const parts = cleanPath.split(/\.|\[|\]/).filter(p => p !== '');
                    console.log(`[MedRecPro Chat] Path parts: [${parts.join(', ')}]`);

                    let current = data;
                    let lastPropertyName = null;

                    for (let i = 0; i < parts.length; i++) {
                        const part = parts[i];

                        if (current === null || current === undefined) {
                            console.log(`[MedRecPro Chat] ❌ Path traversal stopped: current is null/undefined at part '${part}'`);
                            return undefined;
                        }

                        // Handle array index
                        if (/^\d+$/.test(part)) {
                            const index = parseInt(part, 10);
                            if (Array.isArray(current) && index < current.length) {
                                current = current[index];
                                console.log(`[MedRecPro Chat] ✓ Accessed array index [${index}]`);
                            } else {
                                console.log(`[MedRecPro Chat] ❌ Array index [${index}] out of bounds or current is not an array`);
                                return undefined;
                            }
                        } else {
                            lastPropertyName = part;

                            // First try direct case-insensitive property access
                            let value = getCaseInsensitiveProperty(current, part);

                            // If not found directly, try deep search within current object
                            if (value === undefined) {
                                console.log(`[MedRecPro Chat] Property '${part}' not found at current level, trying deep search...`);
                                value = findPropertyDeep(current, part, 5);
                            }

                            if (value === undefined) {
                                console.log(`[MedRecPro Chat] ❌ Property '${part}' not found anywhere in current object`);
                                return undefined;
                            }

                            current = value;
                            const displayValue = typeof current === 'string' ? `"${current}"` :
                                typeof current === 'object' ? '{object}' : String(current);
                            console.log(`[MedRecPro Chat] ✓ Found property '${part}' = ${displayValue}`);
                        }
                    }

                    console.log(`[MedRecPro Chat] === EXTRACTION RESULT: ${current} ===`);
                    return current;
                }

                /**
                 * Substitutes template variables in a string.
                 * @param {string} template - String with {{variableName}} placeholders
                 * @param {Object} variables - Key-value pairs for substitution
                 * @returns {string} String with substituted values
                 */
                function substituteVariables(template, variables) {
                    if (!template || typeof template !== 'string') return template;

                    // Check if there are any template variables to substitute
                    const hasTemplates = /\{\{(\w+)\}\}/.test(template);
                    if (!hasTemplates) return template;

                    console.log(`[MedRecPro Chat] === VARIABLE SUBSTITUTION ===`);
                    console.log(`[MedRecPro Chat] Template: '${template}'`);
                    console.log(`[MedRecPro Chat] Available variables: ${JSON.stringify(variables)}`);

                    const result = template.replace(/\{\{(\w+)\}\}/g, (match, varName) => {
                        // Try exact match first
                        let value = variables[varName];

                        // Try case-insensitive match if not found
                        if (value === undefined) {
                            const lowerVarName = varName.toLowerCase();
                            for (const key of Object.keys(variables)) {
                                if (key.toLowerCase() === lowerVarName) {
                                    value = variables[key];
                                    console.log(`[MedRecPro Chat] Case-insensitive variable match: '${varName}' -> '${key}'`);
                                    break;
                                }
                            }
                        }

                        if (value !== undefined) {
                            console.log(`[MedRecPro Chat] ✓ Substituted {{${varName}}} -> '${value}'`);
                            return value;
                        } else {
                            console.log(`[MedRecPro Chat] ❌ WARNING: Variable '${varName}' not found! Keeping placeholder.`);
                            return match; // Keep original if not found
                        }
                    });

                    if (result !== template) {
                        console.log(`[MedRecPro Chat] Final result: '${result}'`);
                    }
                    return result;
                }

                /**
                 * Checks if an endpoint path contains a template variable that maps to an array.
                 * @param {string} path - The endpoint path with {{variable}} placeholders
                 * @param {Object} variables - Extracted variables from previous steps
                 * @returns {Object|null} { varName, values } if array found, null otherwise
                 */
                function findArrayVariable(path, variables) {
                    const match = path.match(/\{\{(\w+)\}\}/);
                    if (match) {
                        const varName = match[1];
                        const value = variables[varName] || variables[varName.toLowerCase()];
                        if (Array.isArray(value) && value.length > 1) {
                            console.log(`[MedRecPro Chat] Found array variable '${varName}' with ${value.length} values`);
                            return { varName, values: value };
                        }
                    }
                    return null;
                }

                /**
                 * Expands an endpoint into multiple endpoints if it contains array variables.
                 * @param {Object} endpoint - Endpoint specification
                 * @param {Object} variables - Extracted variables from previous steps
                 * @returns {Array<Object>} Array of expanded endpoints (1 if no arrays, N if array)
                 */
                function expandEndpointForArrays(endpoint, variables) {
                    const arrayVar = findArrayVariable(endpoint.path, variables);

                    if (!arrayVar) {
                        return [endpoint]; // No array expansion needed
                    }

                    console.log(`[MedRecPro Chat] === EXPANDING ENDPOINT FOR ARRAY ===`);
                    console.log(`[MedRecPro Chat] Variable '${arrayVar.varName}' has ${arrayVar.values.length} values`);
                    console.log(`[MedRecPro Chat] Will generate ${arrayVar.values.length} API calls`);

                    // Create one endpoint per array value
                    return arrayVar.values.map((value, index) => {
                        const expandedVars = { ...variables };
                        expandedVars[arrayVar.varName] = value;

                        const expandedEndpoint = {
                            ...endpoint,
                            _expandedIndex: index + 1,
                            _expandedTotal: arrayVar.values.length,
                            _expandedValue: value
                        };

                        return { endpoint: expandedEndpoint, variables: expandedVars };
                    });
                }

                /**
                 * Substitutes variables in an endpoint specification.
                 * @param {Object} endpoint - Endpoint specification
                 * @param {Object} variables - Extracted variables from previous steps
                 * @returns {Object} New endpoint with substituted values
                 */
                function substituteEndpointVariables(endpoint, variables) {
                    console.log(`[MedRecPro Chat] Substituting variables for endpoint: ${endpoint.path}`);

                    const substituted = { ...endpoint };

                    // Substitute in path
                    if (substituted.path) {
                        substituted.path = substituteVariables(substituted.path, variables);
                    }

                    // Substitute in pathParameters
                    if (substituted.pathParameters) {
                        substituted.pathParameters = {};
                        for (const [key, value] of Object.entries(endpoint.pathParameters)) {
                            substituted.pathParameters[key] = substituteVariables(String(value), variables);
                        }
                    }

                    // Substitute in queryParameters
                    if (substituted.queryParameters) {
                        substituted.queryParameters = {};
                        for (const [key, value] of Object.entries(endpoint.queryParameters)) {
                            if (typeof value === 'string') {
                                substituted.queryParameters[key] = substituteVariables(value, variables);
                            } else {
                                substituted.queryParameters[key] = value;
                            }
                        }
                    }

                    return substituted;
                }

                /**
                 * Performs auto-extraction of common fields from API response data.
                 * Uses DEEP SEARCH to find properties regardless of nesting level.
                 * @param {Object|Array} data - The API response data
                 * @param {Object} extractedVariables - Object to store extracted values
                 */
                function autoExtractCommonFields(data, extractedVariables) {
                    console.log('[MedRecPro Chat] === AUTO-EXTRACTION (deep search) ===');

                    // Common fields to auto-extract - will search entire object hierarchy
                    const fieldsToExtract = [
                        'documentGuid', 'documentGUID',
                        'productName',
                        'encryptedId', 'encryptedID',
                        'encryptedDocumentID',
                        'encryptedProductID',
                        'setGuid', 'setGUID',
                        'labelerName'
                    ];

                    // Track which normalized names we've already extracted
                    const extractedNormalized = new Set();

                    for (const fieldName of fieldsToExtract) {
                        // Normalize to camelCase for consistency
                        const normalizedKey = fieldName
                            .replace(/GUID$/i, 'Guid')
                            .replace(/ID$/i, 'Id');
                        const lowerNormalized = normalizedKey.toLowerCase();

                        // Skip if we already have this field
                        if (extractedNormalized.has(lowerNormalized)) continue;

                        // Use deep search to find the property anywhere in the hierarchy
                        const value = findPropertyDeep(data, fieldName);

                        if (value !== undefined && value !== null) {
                            // Store with normalized key name
                            extractedVariables[normalizedKey] = value;
                            extractedNormalized.add(lowerNormalized);
                            console.log(`[MedRecPro Chat] ✓ Auto-extracted ${normalizedKey}: '${value}'`);
                        }
                    }

                    console.log(`[MedRecPro Chat] Variables after auto-extraction: ${JSON.stringify(extractedVariables)}`);
                }

                /**************************************************************/
                // Add support for "skipIfPreviousHasResults" property that allows
                // fallback/rescue steps to only execute when a previous step
                // returned empty results.
                //
                // This enables patterns like:
                // - Step 1: Search for documentGUID
                // - Step 2: Get adverse reactions (34084-4)
                // - Step 3: Fallback to unclassified sections (42229-5) if Step 2 empty
                //
                // Update the executeEndpointsWithDependencies function to handle this.
                /**************************************************************/

                /**
                 * Checks if a result has meaningful data (not empty).
                 * @param {Object} result - The API result object
                 * @returns {boolean} True if result has data, false if empty
                 */
                function resultHasData(result) {
                    if (!result || result.statusCode < 200 || result.statusCode >= 300) {
                        return false;
                    }

                    const data = result.result;

                    if (data === null || data === undefined) {
                        return false;
                    }

                    // Check for empty array
                    if (Array.isArray(data)) {
                        return data.length > 0;
                    }

                    // Check for empty object
                    if (typeof data === 'object') {
                        return Object.keys(data).length > 0;
                    }

                    // Primitive values are considered "has data"
                    return true;
                }

                /**
                 * Execute endpoints with dependency support AND conditional execution.
                 * 
                 * New property: skipIfPreviousHasResults
                 * - If set to a step number, this step will be SKIPPED if that step returned data
                 * - Use for fallback/rescue patterns where step 3 only runs if step 2 was empty
                 * 
                 * @param {Array} endpoints - Array of endpoint specifications
                 * @param {Object} assistantMessage - Message object for progress updates
                 * @param {AbortController} abortController - For cancellation
                 * @returns {Promise<Array>} Array of execution results
                 */
                async function executeEndpointsWithDependencies(endpoints, assistantMessage, abortController) {
                    const results = [];
                    const extractedVariables = {};

                    console.log('[MedRecPro Chat] ========================================');
                    console.log('[MedRecPro Chat] MULTI-STEP ENDPOINT EXECUTION STARTED');
                    console.log(`[MedRecPro Chat] Total endpoints: ${endpoints.length}`);
                    console.log('[MedRecPro Chat] ========================================');

                    // Log all endpoints for debugging
                    endpoints.forEach((ep, idx) => {
                        console.log(`[MedRecPro Chat] Endpoint ${idx + 1}: step=${ep.step}, path=${ep.path}, dependsOn=${ep.dependsOn}, skipIfPreviousHasResults=${ep.skipIfPreviousHasResults}, hasOutputMapping=${!!ep.outputMapping}`);
                    });

                    // Group endpoints by step
                    const endpointsByStep = new Map();
                    endpoints.forEach((ep, index) => {
                        const step = ep.step || (ep.dependsOn ? Math.max(...endpoints.map(e => e.step || 1)) + 1 : 1);
                        if (!endpointsByStep.has(step)) {
                            endpointsByStep.set(step, []);
                        }
                        endpointsByStep.get(step).push({ ...ep, originalIndex: index });
                    });

                    // Sort steps
                    const sortedSteps = Array.from(endpointsByStep.keys()).sort((a, b) => a - b);
                    console.log(`[MedRecPro Chat] Execution order: steps [${sortedSteps.join(', ')}]`);

                    const totalEndpoints = endpoints.length;
                    let completedCount = 0;

                    for (const step of sortedSteps) {
                        const stepEndpoints = endpointsByStep.get(step);
                        console.log(`[MedRecPro Chat] ======== EXECUTING STEP ${step} (${stepEndpoints.length} endpoint(s)) ========`);

                        for (const endpoint of stepEndpoints) {

                            // Check dependencies (must succeed)
                            const dependencies = Array.isArray(endpoint.dependsOn)
                                ? endpoint.dependsOn
                                : (endpoint.dependsOn ? [endpoint.dependsOn] : []);

                            if (dependencies.length > 0) {
                                console.log(`[MedRecPro Chat] Step ${step} has ${dependencies.length} dependency/dependencies: [${dependencies.join(', ')}]`);

                                const dependencyStatus = dependencies.map(depStep => {
                                    const depResults = results.filter(r =>
                                        r.specification && r.specification.step === depStep
                                    );

                                    const succeeded = depResults.some(r =>
                                        r.statusCode >= 200 && r.statusCode < 300 && r.result
                                    );

                                    console.log(`[MedRecPro Chat]   - Dependency step ${depStep}: ${succeeded ? '✓ SUCCEEDED' : '❌ FAILED/MISSING'}`);

                                    return { step: depStep, succeeded };
                                });

                                const failedDependencies = dependencyStatus.filter(d => !d.succeeded);

                                if (failedDependencies.length > 0) {
                                    const failedSteps = failedDependencies.map(d => d.step).join(', ');
                                    console.log(`[MedRecPro Chat] ❌ Skipping step ${step} - dependency step(s) [${failedSteps}] failed or missing`);

                                    results.push({
                                        specification: endpoint,
                                        statusCode: 0,
                                        result: null,
                                        error: `Skipped: dependency step(s) [${failedSteps}] failed`,
                                        skipped: true,
                                        step: step
                                    });
                                    completedCount++;
                                    continue;
                                }

                                console.log(`[MedRecPro Chat] ✓ All dependencies for step ${step} satisfied`);
                            }

                            // NEW: Check skipIfPreviousHasResults condition
                            if (endpoint.skipIfPreviousHasResults) {
                                const checkStep = endpoint.skipIfPreviousHasResults;
                                console.log(`[MedRecPro Chat] Checking skipIfPreviousHasResults: step ${checkStep}`);

                                const previousResult = results.find(r =>
                                    r.specification && r.specification.step === checkStep
                                );

                                if (previousResult && resultHasData(previousResult)) {
                                    console.log(`[MedRecPro Chat] ⏭️ Skipping step ${step} - step ${checkStep} returned data (rescue not needed)`);

                                    results.push({
                                        specification: endpoint,
                                        statusCode: 0,
                                        result: null,
                                        error: `Skipped: step ${checkStep} had results (fallback not needed)`,
                                        skipped: true,
                                        skippedReason: 'previous_has_results',
                                        step: step
                                    });
                                    completedCount++;
                                    continue;
                                } else {
                                    console.log(`[MedRecPro Chat] ✓ Step ${checkStep} was empty - proceeding with fallback step ${step}`);
                                }
                            }

                            console.log(`[MedRecPro Chat] Current extractedVariables: ${JSON.stringify(extractedVariables)}`);

                            // Check if endpoint path contains an array variable - if so, expand into multiple calls
                            const expandedEndpoints = expandEndpointForArrays(endpoint, extractedVariables);
                            const isArrayExpansion = expandedEndpoints.length > 1 ||
                                (expandedEndpoints.length === 1 && expandedEndpoints[0].variables);

                            if (isArrayExpansion) {
                                console.log(`[MedRecPro Chat] === MULTI-DOCUMENT EXPANSION: ${expandedEndpoints.length} calls ===`);
                            }

                            // Execute each expanded endpoint (or just the original if no expansion)
                            for (let expandIdx = 0; expandIdx < expandedEndpoints.length; expandIdx++) {
                                const expandedItem = expandedEndpoints[expandIdx];
                                const currentEndpoint = expandedItem.endpoint || expandedItem;
                                const currentVars = expandedItem.variables || extractedVariables;

                                // Substitute any variables from previous steps
                                const processedEndpoint = substituteEndpointVariables(currentEndpoint, currentVars);

                                // Update progress with expansion info
                                const expandInfo = isArrayExpansion ? ` [${expandIdx + 1}/${expandedEndpoints.length}]` : '';
                                assistantMessage.progress = completedCount / totalEndpoints;
                                assistantMessage.progressStatus = `Step ${step}${expandInfo}: ${processedEndpoint.description || 'Executing query'}...`;
                                updateMessage(assistantMessage.id);

                                try {
                                    const startTime = Date.now();
                                    const apiUrl = buildApiUrl(processedEndpoint);
                                    const fullApiUrl = buildUrl(apiUrl);
                                    console.log(`[MedRecPro Chat] Step ${step}${expandInfo}: Executing API call: ${fullApiUrl}`);

                                    const apiResponse = await fetch(fullApiUrl, getFetchOptions({
                                        method: processedEndpoint.method || 'GET',
                                        headers: processedEndpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
                                        body: processedEndpoint.method === 'POST' ? JSON.stringify(processedEndpoint.body) : undefined,
                                        signal: abortController.signal
                                    }));

                                    if (apiResponse.ok) {
                                        const data = await apiResponse.json();
                                        const hasData = resultHasData({ result: data, statusCode: apiResponse.status });
                                        console.log(`[MedRecPro Chat] Step ${step}${expandInfo} ✓ succeeded: ${processedEndpoint.path} (hasData: ${hasData})`);

                                        // Extract output mappings for use in subsequent steps (only on first expansion or non-expansion)
                                        if (!isArrayExpansion && endpoint.outputMapping) {
                                            console.log(`[MedRecPro Chat] Processing outputMapping: ${JSON.stringify(endpoint.outputMapping)}`);
                                            for (const [varName, jsonPath] of Object.entries(endpoint.outputMapping)) {
                                                let extractedValue = extractValueByPath(data, jsonPath);

                                                // If path extraction failed, try deep search using the variable name
                                                if (extractedValue === undefined) {
                                                    console.log(`[MedRecPro Chat] Path extraction failed, trying deep search for '${varName}'...`);
                                                    extractedValue = findPropertyDeep(data, varName);
                                                }

                                                if (extractedValue !== undefined) {
                                                    extractedVariables[varName] = extractedValue;
                                                    console.log(`[MedRecPro Chat] ✓ Stored variable '${varName}' = '${extractedValue}'`);
                                                } else {
                                                    console.log(`[MedRecPro Chat] ❌ Failed to extract '${varName}' - not found by path or deep search`);
                                                }
                                            }
                                        } else if (!isArrayExpansion) {
                                            // Auto-extract common fields if no explicit mapping
                                            autoExtractCommonFields(data, extractedVariables);
                                        }

                                        results.push({
                                            specification: processedEndpoint,
                                            statusCode: apiResponse.status,
                                            result: data,
                                            executionTimeMs: Date.now() - startTime,
                                            step: step,
                                            hasData: hasData,
                                            _expandedIndex: currentEndpoint._expandedIndex,
                                            _expandedTotal: currentEndpoint._expandedTotal
                                        });
                                    } else {
                                        console.log(`[MedRecPro Chat] Step ${step}${expandInfo} ❌ failed: ${processedEndpoint.path} - HTTP ${apiResponse.status}`);
                                        results.push({
                                            specification: processedEndpoint,
                                            statusCode: apiResponse.status,
                                            result: null,
                                            error: `HTTP ${apiResponse.status}`,
                                            executionTimeMs: Date.now() - startTime,
                                            step: step,
                                            hasData: false
                                        });
                                    }
                                } catch (endpointError) {
                                    console.log(`[MedRecPro Chat] Step ${step}${expandInfo} ❌ exception: ${endpointError.message}`);
                                    results.push({
                                        specification: processedEndpoint,
                                        statusCode: 500,
                                        error: endpointError.message,
                                        step: step,
                                        hasData: false
                                    });
                                }
                            } // End of expanded endpoints loop

                            completedCount++;
                        }
                    }

                    console.log('[MedRecPro Chat] ========================================');
                    console.log('[MedRecPro Chat] MULTI-STEP EXECUTION COMPLETE');
                    console.log(`[MedRecPro Chat] Total results: ${results.length}`);
                    console.log(`[MedRecPro Chat] Final extractedVariables: ${JSON.stringify(extractedVariables)}`);
                    console.log('[MedRecPro Chat] ========================================');

                    return results;
                }

                // Check for failed endpoints and retry if needed
                const failedResults = results.filter(r => r.statusCode >= 400 || r.error);
                const successfulResults = results.filter(r => r.statusCode >= 200 && r.statusCode < 300 && !r.error);

                // If all endpoints failed, attempt retry
                if (failedResults.length > 0 && successfulResults.length === 0) {
                    console.log('[MedRecPro Chat] All endpoints failed, attempting retry...');

                    const retryResults = await attemptRetryInterpretation(
                        input,
                        interpretation,
                        failedResults,
                        assistantMessage,
                        1  // Start at attempt 1
                    );

                    // If retry produced results, use those instead
                    if (retryResults && retryResults.length > 0) {
                        const hasSuccessful = retryResults.some(r => r.statusCode >= 200 && r.statusCode < 300);
                        if (hasSuccessful) {
                            results.length = 0;  // Clear original results
                            results.push(...retryResults);  // Use retry results
                        }
                    }
                }

                // Call synthesize to format the results
                assistantMessage.progress = 0.9;
                assistantMessage.progressStatus = 'Synthesizing results...';
                updateMessage(assistantMessage.id);

                const synthesizeUrl = buildUrl(API_CONFIG.endpoints.synthesize);
                console.log('[MedRecPro Chat] Calling synthesize endpoint:', synthesizeUrl);

                const synthesizeResponse = await fetch(synthesizeUrl, getFetchOptions({
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        originalQuery: input,
                        interpretation: interpretation,
                        executedEndpoints: results,
                        conversationId: state.conversationId
                    }),
                    signal: state.abortController.signal
                }));
                if (synthesizeResponse.ok) {
                    const synthesis = await synthesizeResponse.json();
                    assistantMessage.content = synthesis.response || 'No results found.';

                    // Add any follow-up suggestions
                    if (synthesis.suggestedFollowUps && synthesis.suggestedFollowUps.length > 0) {
                        assistantMessage.content += '\n\n**Suggested follow-ups:**\n';
                        synthesis.suggestedFollowUps.forEach(followUp => {
                            assistantMessage.content += `- ${followUp}\n`;
                        });
                    }

                    // Add document reference links for full label viewing
                    if (synthesis.dataReferences && Object.keys(synthesis.dataReferences).length > 0) {
                        assistantMessage.content += '\n\n**View Full Labels:**\n';
                        for (const [displayName, url] of Object.entries(synthesis.dataReferences)) {
                            assistantMessage.content += `- [${displayName}](${buildUrl(url)})\n`;
                        }
                    }

                    // Add API data source links for transparency
                    const sourceLinks = formatApiSourceLinks(results);
                    if (sourceLinks) {
                        assistantMessage.content += sourceLinks;
                    }
                } else {
                    assistantMessage.content = 'Unable to synthesize results. Please try again.';
                }

                // Clear progress state
                assistantMessage.progress = undefined;
                assistantMessage.progressStatus = undefined;
                assistantMessage.isStreaming = false;
                updateMessage(assistantMessage.id);
            }
            // Fallback if no recognizable response
            else {
                assistantMessage.content = interpretation.directResponse ||
                    interpretation.explanation ||
                    'I understood your request but couldn\'t process it. Please try rephrasing.';
                assistantMessage.isStreaming = false;
                updateMessage(assistantMessage.id);
            }

        } catch (error) {
            if (error.name === 'AbortError') {
                assistantMessage.error = 'Request cancelled';
            } else {
                assistantMessage.error = error.message || 'An error occurred';
                // Provide more helpful error message for CORS issues
                if (isLocalDevelopment() && error.message.includes('Failed to fetch')) {
                    assistantMessage.error = 'CORS error: The API server may not be configured to accept requests from localhost. ' +
                        'Check browser console for details.';
                }
            }
            assistantMessage.isStreaming = false;
            updateMessage(assistantMessage.id);
        } finally {
            state.isLoading = false;
            updateUI();
        }

    }

    /**
     * Builds a full API URL from an endpoint specification.
     * @param {Object} endpoint - Endpoint specification object
     * @returns {string} URL path with query parameters
     * <remarks>
     * This handles the endpoint object from interpretation results,
     * constructing the path with any query parameters.
     * The result should be passed to buildUrl() for full URL construction.
     * </remarks>
     */
    function buildApiUrl(endpoint) {

        let url = endpoint.path;

        // Handle query parameters
        if (endpoint.queryParameters) {
            const params = new URLSearchParams();
            for (const [key, value] of Object.entries(endpoint.queryParameters)) {
                if (value !== null && value !== undefined) {
                    params.append(key, value);
                }
            }
            const queryString = params.toString();
            if (queryString) {
                url += (url.includes('?') ? '&' : '?') + queryString;
            }
        }

        return url;

    }

    /**
     * Formats API endpoint results as markdown links for data source transparency.
     * @param {Array} results - Array of endpoint execution results
     * @returns {string} Markdown formatted string with API links, or empty string if no successful results
     * <remarks>
     * Only includes successful endpoints (2xx status codes) that returned data.
     * Creates clickable links to the underlying API endpoints so users can
     * explore the raw data directly.
     * Separates primary data sources from fallback sources.
     * </remarks>
     */
    function formatApiSourceLinks(results) {

        if (!results || results.length === 0) {
            return '';
        }

        // Separate results into categories:
        // 1. Primary sources: successful with data, not a fallback
        // 2. Fallback sources: successful with data, was a fallback that was used
        // 3. Skipped fallbacks: endpoints that were skipped because primary had data
        const primarySources = [];
        const fallbackSourcesUsed = [];
        const skippedFallbacks = [];

        results.forEach(r => {
            if (!r.specification) return;

            const isFallback = r.specification.skipIfPreviousHasResults !== undefined;
            const wasSkipped = r.skipped === true;
            const hasData = r.hasData === true && r.statusCode >= 200 && r.statusCode < 300;

            if (wasSkipped && r.skippedReason === 'previous_has_results') {
                // This was a fallback that wasn't needed
                skippedFallbacks.push(r);
            } else if (hasData && r.result) {
                if (isFallback) {
                    // This fallback was actually used (primary returned empty)
                    fallbackSourcesUsed.push(r);
                } else {
                    // Primary source with data
                    primarySources.push(r);
                }
            }
        });

        // Build display name with product info where available
        function buildSourceDescription(r) {
            let description = r.specification.description || r.specification.path;
            let productName = null;

            // Try to extract product name from result data
            if (r.result) {
                productName = extractProductNameFromResult(r.result);
            }

            // Clean up template variables
            description = description.replace(/\{\{?documentGuid\}?\}/gi, '').trim();

            // Remove trailing colon or dash left from template variable removal
            description = description.replace(/[\s:-]+$/, '').trim();

            // Append product name if found
            if (productName) {
                description = `${description} - ${productName}`;
            }

            return description;
        }

        // Extract product name from API result data
        function extractProductNameFromResult(data) {
            if (!data) return null;

            // If data is an array, check the first item
            const item = Array.isArray(data) ? (data[0] || {}) : data;

            // Try common field names for product/drug names
            const fieldNames = [
                'productName', 'ProductName', 'product_name',
                'title', 'Title',
                'documentDisplayName', 'displayName',
                'name', 'Name'
            ];

            for (const field of fieldNames) {
                // Check directly on item
                if (item[field] && typeof item[field] === 'string') {
                    return cleanProductName(item[field]);
                }

                // Check nested in common wrapper objects
                const wrappers = ['sectionContent', 'document', 'label'];
                for (const wrapper of wrappers) {
                    if (item[wrapper] && item[wrapper][field]) {
                        return cleanProductName(item[wrapper][field]);
                    }
                }
            }

            return null;
        }

        // Clean up product name for display
        function cleanProductName(name) {
            if (!name) return null;

            // Truncate very long names
            if (name.length > 50) {
                name = name.substring(0, 47) + '...';
            }

            // Remove common FDA boilerplate prefixes
            const boilerplatePrefixes = [
                'These highlights do not include all the information needed to use',
                'HIGHLIGHTS OF PRESCRIBING INFORMATION'
            ];

            for (const prefix of boilerplatePrefixes) {
                if (name.toLowerCase().startsWith(prefix.toLowerCase())) {
                    name = name.substring(prefix.length).trim();
                    // Remove leading punctuation
                    name = name.replace(/^[.,:\-;\s]+/, '');
                    break;
                }
            }

            return name || null;
        }

        // Deduplicate by base description
        function deduplicateResults(sourceArray) {
            const seenDescriptions = new Set();
            const unique = [];

            sourceArray.forEach(r => {
                const description = buildSourceDescription(r);
                const baseDescription = description.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, '{guid}');

                if (!seenDescriptions.has(baseDescription)) {
                    seenDescriptions.add(baseDescription);
                    unique.push({ result: r, description: description, baseDescription: baseDescription });
                }
            });

            return unique;
        }

        const uniquePrimary = deduplicateResults(primarySources);
        const uniqueFallbacks = deduplicateResults(fallbackSourcesUsed);

        if (uniquePrimary.length === 0 && uniqueFallbacks.length === 0) {
            return '';
        }

        // Build the markdown section
        let linksMarkdown = '\n\n---\n';

        // Primary data sources
        if (uniquePrimary.length > 0) {
            linksMarkdown += '**Data sources:**\n';
            uniquePrimary.forEach(item => {
                const endpoint = item.result.specification;
                const apiUrl = buildApiUrl(endpoint);
                const fullUrl = buildUrl(apiUrl);
                linksMarkdown += `- [${item.description}](${fullUrl})\n`;
            });
        }

        // Fallback sources that were actually used
        if (uniqueFallbacks.length > 0) {
            if (uniquePrimary.length > 0) {
                linksMarkdown += '\n';
            }
            linksMarkdown += '**Fallback sources used:**\n';
            uniqueFallbacks.forEach(item => {
                const endpoint = item.result.specification;
                const apiUrl = buildApiUrl(endpoint);
                const fullUrl = buildUrl(apiUrl);
                linksMarkdown += `- [${item.description}](${fullUrl})\n`;
            });
        }

        return linksMarkdown;

    }

    /**
     * Cancels the current request.
     */
    function cancelRequest() {
        if (state.abortController) {
            state.abortController.abort();
        }
        state.isLoading = false;
        updateUI();
    }

    /**
     * Clears the conversation.
     */
    function clearConversation() {
        state.messages = [];
        state.files = [];
        state.conversationId = generateUUID();
        renderMessages();
        renderFileList();
        hideFileUpload();
    }

    /**************************************************************/
    // UI Helpers
    /**************************************************************/

    /**
     * Updates UI state based on loading status.
     */
    function updateUI() {
        elements.messageInput.disabled = state.isLoading;
        elements.sendBtn.style.display = state.isLoading ? 'none' : 'inline-flex';
        elements.cancelBtn.style.display = state.isLoading ? 'inline-flex' : 'none';
        elements.sendBtn.disabled = !elements.messageInput.value.trim() && state.files.length === 0;
    }

    /**
     * Toggles the file upload dropzone visibility.
     */
    function toggleFileUpload() {
        state.showFileUpload = !state.showFileUpload;
        elements.fileDropzone.style.display = state.showFileUpload ? 'block' : 'none';
        elements.attachBtn.classList.toggle('active', state.showFileUpload || state.files.length > 0);
    }

    /**
     * Hides the file upload dropzone.
     */
    function hideFileUpload() {
        state.showFileUpload = false;
        elements.fileDropzone.style.display = 'none';
        elements.attachBtn.classList.toggle('active', state.files.length > 0);
    }

    /**
     * Auto-resizes the textarea based on content.
     * Grows without scrollbars until max-height is reached.
     * When max-height is reached, enables thin scrollbar.
     */
    function autoResizeTextarea() {
        const textarea = elements.messageInput;

        // Reset height to auto to get accurate scrollHeight
        textarea.style.height = 'auto';

        // Get the computed max-height from CSS (50vh by default)
        const computedStyle = window.getComputedStyle(textarea);
        const maxHeight = parseInt(computedStyle.maxHeight, 10) || (window.innerHeight * 0.5);

        // Calculate the new height based on content
        const newHeight = textarea.scrollHeight;

        // If content exceeds max-height, cap it and enable scrolling
        if (newHeight >= maxHeight) {
            textarea.style.height = maxHeight + 'px';
            textarea.classList.add('has-scroll');
        } else {
            // Content fits within bounds - grow to fit without scrollbar
            textarea.style.height = newHeight + 'px';
            textarea.classList.remove('has-scroll');
        }
    }

    /**************************************************************/
    // Event Listeners
    /**************************************************************/

    // Send button
    elements.sendBtn.addEventListener('click', sendMessage);

    // Cancel button
    elements.cancelBtn.addEventListener('click', cancelRequest);

    // Clear button
    elements.clearBtn.addEventListener('click', clearConversation);

    // Attach button
    elements.attachBtn.addEventListener('click', toggleFileUpload);

    // Message input
    elements.messageInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    elements.messageInput.addEventListener('input', () => {
        autoResizeTextarea();
        updateUI();
    });

    // File drop area
    elements.dropArea.addEventListener('click', () => {
        elements.fileInput.click();
    });

    elements.dropArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        elements.dropArea.classList.add('dragging');
    });

    elements.dropArea.addEventListener('dragleave', (e) => {
        e.preventDefault();
        elements.dropArea.classList.remove('dragging');
    });

    elements.dropArea.addEventListener('drop', (e) => {
        e.preventDefault();
        elements.dropArea.classList.remove('dragging');
        addFiles(e.dataTransfer.files);
    });

    elements.fileInput.addEventListener('change', (e) => {
        addFiles(e.target.files);
        e.target.value = ''; // Reset for re-selection
    });

    // Suggestion cards
    document.querySelectorAll('.suggestion-card').forEach(card => {
        card.addEventListener('click', () => {
            elements.messageInput.value = card.dataset.suggestion;
            autoResizeTextarea();
            updateUI();
            elements.messageInput.focus();
        });
    });

    /**************************************************************/
    // Import Result Extraction
    // <remarks>
    // Extracts document information from the nested import response structure.
    // The backend returns: results[].fileResults[].splGUID (not results[].documentGuid)
    // </remarks>
    /**************************************************************/

    /**
     * Extracts document information from the completed import response.
     * @param {Object} finalStatus - The completed import status with results
     * @returns {Object} Extracted document IDs, names, and statistics
     * <remarks>
     * Navigates the nested structure: results[].fileResults[] to find splGUID.
     * Aggregates statistics across all imported files.
     * </remarks>
     */
    function extractImportResults(finalStatus) {
        // #region implementation

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
                        if (fileResult.documentsCreated) statistics.documentsCreated += fileResult.documentsCreated;
                        if (fileResult.organizationsCreated) statistics.organizationsCreated += fileResult.organizationsCreated;
                        if (fileResult.productsCreated) statistics.productsCreated += fileResult.productsCreated;
                        if (fileResult.sectionsCreated) statistics.sectionsCreated += fileResult.sectionsCreated;
                        if (fileResult.ingredientsCreated) statistics.ingredientsCreated += fileResult.ingredientsCreated;
                        if (fileResult.productElementsCreated) statistics.productElementsCreated += fileResult.productElementsCreated;
                    }
                }
            }
        }

        console.log('[MedRecPro Chat] Extracted import results:', {
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

        // #endregion
    }

    /**************************************************************/
    // Retry Interpretation Logic
    // <remarks>
    // Implements recursive retry when API endpoints fail.
    // Calls the server-side retry endpoint to get alternative endpoints.
    // </remarks>
    /**************************************************************/

    /**
     * Attempts to retry interpretation when endpoints fail.
     * @param {string} originalQuery - The user's original query
     * @param {Object} originalInterpretation - The original interpretation that failed
     * @param {Array} failedResults - Array of failed endpoint results
     * @param {Object} assistantMessage - The assistant message object to update
     * @param {number} attemptNumber - Current attempt number (1-3)
     * @returns {Array} Array of results from retry, or empty array if retry failed
     */
    async function attemptRetryInterpretation(originalQuery, originalInterpretation, failedResults, assistantMessage, attemptNumber) {

        // #region implementation

        const maxAttempts = API_CONFIG.maxRetryAttempts || 3;

        if (attemptNumber > maxAttempts) {
            console.log(`[MedRecPro Chat] Max retry attempts (${maxAttempts}) reached`);
            return [];
        }

        console.log(`[MedRecPro Chat] Retry attempt ${attemptNumber} of ${maxAttempts}`);

        // Update progress indicator
        assistantMessage.progressStatus = `Retrying with alternative approach (attempt ${attemptNumber}/${maxAttempts})...`;
        updateMessage(assistantMessage.id);

        try {
            // Call the retry endpoint
            const retryUrl = buildUrl(API_CONFIG.endpoints.retry);
            console.log('[MedRecPro Chat] Calling retry endpoint:', retryUrl);

            const retryResponse = await fetch(retryUrl, getFetchOptions({
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    userMessage: originalQuery,
                    conversationId: state.conversationId,
                    systemContext: state.systemContext
                }),
                signal: state.abortController.signal
            }));

            // Build request with failed results info
            const retryRequest = {
                originalRequest: {
                    userMessage: originalQuery,
                    conversationId: state.conversationId,
                    systemContext: state.systemContext
                },
                failedResults: failedResults.map(r => ({
                    specification: r.specification || { method: 'GET', path: r.endpoint },
                    statusCode: r.statusCode || 500,
                    error: r.error || 'Unknown error'
                })),
                attemptNumber: attemptNumber
            };

            const retryInterpretResponse = await fetch(retryUrl, getFetchOptions({
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(retryRequest),
                signal: state.abortController.signal
            }));

            if (!retryInterpretResponse.ok) {
                console.log('[MedRecPro Chat] Retry interpretation request failed:', retryInterpretResponse.status);
                return [];
            }

            const retryInterpretation = await retryInterpretResponse.json();

            // If direct response, we're done
            if (retryInterpretation.isDirectResponse) {
                console.log('[MedRecPro Chat] Retry returned direct response');
                assistantMessage.content = retryInterpretation.directResponse ||
                    retryInterpretation.explanation ||
                    'Unable to find alternative data sources.';
                return [];
            }

            // If no new endpoints, we're done
            if (!retryInterpretation.suggestedEndpoints &&
                !retryInterpretation.endpoints ||
                (retryInterpretation.endpoints || retryInterpretation.suggestedEndpoints || []).length === 0) {
                console.log('[MedRecPro Chat] Retry returned no new endpoints');
                return [];
            }

            const newEndpoints = retryInterpretation.endpoints || retryInterpretation.suggestedEndpoints;
            console.log(`[MedRecPro Chat] Retry suggested ${newEndpoints.length} new endpoint(s)`);

            // Execute the new endpoints
            const newResults = [];
            for (const endpoint of newEndpoints) {
                assistantMessage.progressStatus = `Trying: ${endpoint.description || endpoint.path}...`;
                updateMessage(assistantMessage.id);

                try {
                    const startTime = Date.now();
                    const apiUrl = buildApiUrl(endpoint);
                    const fullApiUrl = buildUrl(apiUrl);
                    console.log('[MedRecPro Chat] Executing retry API call:', fullApiUrl);

                    const apiResponse = await fetch(fullApiUrl, getFetchOptions({
                        method: endpoint.method || 'GET',
                        headers: endpoint.method === 'POST' ? { 'Content-Type': 'application/json' } : {},
                        body: endpoint.method === 'POST' ? JSON.stringify(endpoint.body) : undefined,
                        signal: state.abortController.signal
                    }));

                    if (apiResponse.ok) {
                        const data = await apiResponse.json();
                        newResults.push({
                            specification: endpoint,
                            statusCode: apiResponse.status,
                            result: data,
                            executionTimeMs: Date.now() - startTime
                        });
                        console.log('[MedRecPro Chat] Retry endpoint succeeded:', endpoint.path);
                    } else {
                        newResults.push({
                            specification: endpoint,
                            statusCode: apiResponse.status,
                            result: null,
                            error: `HTTP ${apiResponse.status}`,
                            executionTimeMs: Date.now() - startTime
                        });
                        console.log('[MedRecPro Chat] Retry endpoint failed:', endpoint.path, apiResponse.status);
                    }
                } catch (endpointError) {
                    newResults.push({
                        specification: endpoint,
                        statusCode: 500,
                        error: endpointError.message
                    });
                }
            }

            // Check if any new endpoints succeeded
            const successfulNewResults = newResults.filter(r => r.statusCode >= 200 && r.statusCode < 300);

            if (successfulNewResults.length > 0) {
                console.log(`[MedRecPro Chat] Retry succeeded with ${successfulNewResults.length} result(s)`);
                return newResults;
            }

            // All new endpoints also failed - recursive retry
            console.log('[MedRecPro Chat] Retry endpoints also failed, attempting next retry...');
            const allFailedResults = [...failedResults, ...newResults.filter(r => r.statusCode >= 400 || r.error)];

            return await attemptRetryInterpretation(
                originalQuery,
                retryInterpretation,
                allFailedResults,
                assistantMessage,
                attemptNumber + 1
            );

        } catch (error) {
            console.error('[MedRecPro Chat] Error in retry interpretation:', error);
            return [];
        }

        // #endregion
    }

    /**************************************************************/
    // Initialization
    /**************************************************************/

    // Focus input on load
    elements.messageInput.focus();

    // Fetch system context
    fetchSystemContext();

    // Initial UI state
    updateUI();
})();