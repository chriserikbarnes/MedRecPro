/**************************************************************/
// MedRecPro AI Chat Interface - Vanilla JavaScript Implementation
// Provides natural language interaction with the MedRecPro API
/**************************************************************/

(function () {
    'use strict';

    /**************************************************************/
    // Configuration
    /**************************************************************/
    const API_CONFIG = {
        baseUrl: 'https://www.medrecpro.com/api/',
        endpoints: {
            context: '/context',
            interpret: '/interpret',
            synthesize: '/synthesize',
            chat: '/chat',
            upload: '/api/Labels/import' // File upload endpoint
        },
        pollInterval: 1000
    };

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
        abortController: null
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
     * @@returns {string} UUID string
     */
    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    /**
     * Escapes HTML special characters to prevent XSS.
     * @@param {string} text - Raw text to escape
     * @@returns {string} Escaped HTML string
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Formats file size for display.
     * @@param {number} bytes - File size in bytes
     * @@returns {string} Formatted size string
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
     * @@param {string} text - Markdown text to render
     * @@returns {string} HTML string
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
                processed.push('<ul>' + listItems.join('') + '</ul>');
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
     * @@param {HTMLElement} btn - The copy button element
     * @@param {string} index - Code block index
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
     * @@param {Object} message - Message object
     * @@returns {string} HTML string for the message
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
            const circumference = 2 * Math.PI * 16;
            const offset = circumference - (message.progress * circumference);
            progressHtml = `
                        <div class="progress-indicator">
                            <div class="progress-ring">
                                <svg width="44" height="44">
                                    <circle class="progress-ring-bg" cx="22" cy="22" r="16"></circle>
                                    <circle class="progress-ring-fill" cx="22" cy="22" r="16"
                                        stroke-dasharray="${message.progress * circumference} ${circumference}"></circle>
                                </svg>
                                <span class="progress-percent">${Math.round(message.progress * 100)}%</span>
                            </div>
                            <div class="progress-info">
                                <div class="progress-status">${escapeHtml(message.progressStatus || 'Processing...')}</div>
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
     * @@param {HTMLElement} header - The thinking header element
     */
    window.toggleThinking = function (header) {
        const icon = header.querySelector('.thinking-icon');
        const content = header.nextElementSibling;
        icon.classList.toggle('expanded');
        content.classList.toggle('expanded');
    };

    /**
     * Copies a message's content to clipboard.
     * @@param {string} messageId - The message ID
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
     * @@param {string} messageId - The failed message ID
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
     * @@param {string} messageId - The message ID to update
     */
    function updateMessage(messageId) {
        const message = state.messages.find(m => m.id === messageId);
        if (!message) return;

        const msgEl = document.querySelector(`[data-message-id="${messageId}"]`);
        if (msgEl) {
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
     * @@param {number} index - File index to remove
     */
    window.removeFile = function (index) {
        state.files.splice(index, 1);
        renderFileList();
    };

    /**
     * Adds files to the upload list (ZIP only).
     * @@param {FileList} files - Files to add
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

    /**
     * Uploads files to the server.
     * @@returns {Promise<string[]>} Array of file IDs
     */
    async function uploadFiles() {
        if (state.files.length === 0) return [];

        const formData = new FormData();
        state.files.forEach(file => formData.append('files', file));

        const response = await fetch(API_CONFIG.endpoints.upload, {
            method: 'POST',
            body: formData,
            signal: state.abortController?.signal
        });

        if (!response.ok) {
            throw new Error('File upload failed');
        }

        const result = await response.json();
        return result.fileIds || [];
    }

    /**************************************************************/
    // API Communication
    /**************************************************************/

    /**
     * Fetches the system context from the API.
     */
    async function fetchSystemContext() {
        try {
            const response = await fetch(API_CONFIG.baseUrl + API_CONFIG.endpoints.context);
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
            console.error('Failed to fetch system context:', error);
        }
    }

    /**
     * Sends a message to the AI API.
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
            let fileIds = [];
            if (state.files.length > 0) {
                fileIds = await uploadFiles();
                state.files = [];
                renderFileList();
                hideFileUpload();
            }

            // Build conversation history for context
            const conversationHistory = state.messages
                .filter(m => m.id !== assistantMessage.id)
                .slice(-10) // Last 10 messages for context
                .map(m => ({ role: m.role, content: m.content }));

            // Call the interpret endpoint
            const interpretResponse = await fetch(API_CONFIG.baseUrl + API_CONFIG.endpoints.interpret, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    userMessage: input,
                    conversationId: state.conversationId,
                    conversationHistory: conversationHistory,
                    fileIds: fileIds
                }),
                signal: state.abortController.signal
            });

            if (!interpretResponse.ok) {
                throw new Error(`API error: ${interpretResponse.status}`);
            }

            const interpretation = await interpretResponse.json();

            // Update thinking content
            if (interpretation.explanation) {
                assistantMessage.thinking = interpretation.explanation;
                updateMessage(assistantMessage.id);
            }

            // Handle direct response (no API calls needed)
            if (interpretation.isDirectResponse && interpretation.directResponse) {
                assistantMessage.content = interpretation.directResponse;
                assistantMessage.isStreaming = false;
                updateMessage(assistantMessage.id);
            }
            // Handle clarifying questions
            else if (interpretation.clarifyingQuestions && interpretation.clarifyingQuestions.length > 0) {
                assistantMessage.content = interpretation.clarifyingQuestions.join('\n\n');
                assistantMessage.isStreaming = false;
                updateMessage(assistantMessage.id);
            }
            // Handle endpoint execution
            else if (interpretation.endpoints && interpretation.endpoints.length > 0) {
                // Execute the suggested endpoints
                const executedEndpoints = [];

                for (const endpoint of interpretation.endpoints) {
                    try {
                        const url = buildApiUrl(endpoint);
                        const options = {
                            method: endpoint.method || 'GET',
                            headers: { 'Content-Type': 'application/json' },
                            signal: state.abortController.signal
                        };

                        if (endpoint.method === 'POST' && endpoint.body) {
                            options.body = JSON.stringify(endpoint.body);
                        }

                        const startTime = Date.now();
                        const apiResponse = await fetch(url, options);
                        const executionTime = Date.now() - startTime;

                        let result = null;
                        if (apiResponse.ok) {
                            result = await apiResponse.json();
                        }

                        executedEndpoints.push({
                            specification: endpoint,
                            statusCode: apiResponse.status,
                            result: result,
                            executionTimeMs: executionTime
                        });
                    } catch (endpointError) {
                        executedEndpoints.push({
                            specification: endpoint,
                            statusCode: 500,
                            error: endpointError.message,
                            executionTimeMs: 0
                        });
                    }
                }

                // Synthesize the results
                const synthesisResponse = await fetch(API_CONFIG.baseUrl + API_CONFIG.endpoints.synthesize, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        originalQuery: input,
                        conversationId: state.conversationId,
                        executedEndpoints: executedEndpoints
                    }),
                    signal: state.abortController.signal
                });

                if (synthesisResponse.ok) {
                    const synthesis = await synthesisResponse.json();
                    assistantMessage.content = synthesis.response || 'No response generated.';

                    // Add suggested follow-ups if available
                    if (synthesis.suggestedFollowUps && synthesis.suggestedFollowUps.length > 0) {
                        assistantMessage.content += '\n\n**Suggested follow-ups:**\n';
                        synthesis.suggestedFollowUps.forEach(followUp => {
                            assistantMessage.content += `- ${followUp}\n`;
                        });
                    }
                } else {
                    assistantMessage.content = 'Unable to synthesize results. Please try again.';
                }

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
     * @@param {Object} endpoint - Endpoint specification
     * @@returns {string} Full URL
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
    // Initialization
    /**************************************************************/

    // Focus input on load
    elements.messageInput.focus();

    // Fetch system context
    fetchSystemContext();

    // Initial UI state
    updateUI();
})();