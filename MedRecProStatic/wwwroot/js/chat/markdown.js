/**************************************************************/
/**
 * MedRecPro Chat Markdown Rendering Module
 *
 * @fileoverview Converts markdown text to HTML for safe display in chat messages.
 * Provides custom markdown parsing with code block handling and copy functionality.
 *
 * @description
 * The markdown module handles:
 * - Code block extraction and syntax highlighting labels
 * - Inline formatting (bold, italic, inline code, links)
 * - Block elements (headers, lists, blockquotes, horizontal rules)
 * - Code copy-to-clipboard functionality
 * - XSS prevention through proper escaping
 *
 * @example
 * import { MarkdownRenderer } from './markdown.js';
 *
 * const html = MarkdownRenderer.render('**Bold** and `code`');
 * // Returns: '<strong>Bold</strong> and <code>code</code>'
 *
 * @module chat/markdown
 * @see ChatUtils.escapeHtml - Used for XSS prevention
 * @see MessageRenderer - Consumes rendered markdown for message display
 */
/**************************************************************/

import { ChatUtils } from './utils.js';

export const MarkdownRenderer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Storage for code blocks to enable copy functionality.
     *
     * @description
     * When code blocks are rendered, their content is stored here
     * indexed by their position in the rendered output. This allows
     * the copy button to retrieve the original code content.
     *
     * @type {Array<{lang: string, code: string}>}
     * @see copyCode - Uses this storage to copy code
     */
    /**************************************************************/
    const codeBlockStorage = [];

    // Expose storage globally for copy button onclick handlers
    window.codeBlockStorage = codeBlockStorage;

    /**************************************************************/
    /**
     * Renders markdown content to HTML with proper escaping.
     *
     * @param {string} text - Markdown text to render
     * @returns {string} HTML string safe for innerHTML assignment
     *
     * @description
     * Processing order (important for correct rendering):
     * 1. Escape HTML to prevent XSS
     * 2. Extract and preserve code blocks (prevents processing inside code)
     * 3. Process inline elements (bold, italic, code, links)
     * 4. Process block elements (headers, lists, blockquotes)
     * 5. Restore code blocks with formatting
     *
     * Supported markdown syntax:
     * - **bold** -> <strong>bold</strong>
     * - *italic* -> <em>italic</em>
     * - `code` -> <code>code</code>
     * - [text](url) -> <a href="url">text</a>
     * - # Header -> <h1>Header</h1> (supports h1-h3)
     * - - item -> <li>item</li> (unordered list)
     * - 1. item -> numbered list item
     * - > quote -> <blockquote>quote</blockquote>
     * - --- -> <hr>
     * - ```lang\ncode\n``` -> code block with copy button
     *
     * @example
     * // Basic formatting
     * render('**Bold** and *italic*');
     * // Returns: '<strong>Bold</strong> and <em>italic</em>'
     *
     * // Code blocks
     * render('```js\nconst x = 1;\n```');
     * // Returns styled code block with copy button
     *
     * @see ChatUtils.escapeHtml - First step in rendering
     * @see MessageRenderer.renderMessage - Primary consumer
     */
    /**************************************************************/
    function render(text) {
        // Handle null/undefined input
        if (!text) return '';

        // Step 1: Escape HTML to prevent XSS attacks
        let html = ChatUtils.escapeHtml(text);

        // Temporary storage for code blocks during processing
        const codeBlocks = [];

        // Step 2: Extract and preserve code blocks
        // Regex matches: ```language\ncode\n```
        // The language specifier is optional
        html = html.replace(/```(\w*)\n([\s\S]*?)```/g, (match, lang, code) => {
            const index = codeBlocks.length;
            codeBlocks.push({
                lang: lang || 'code',  // Default to 'code' if no language
                code: code.trim()       // Remove leading/trailing whitespace
            });
            // Replace with placeholder that won't be affected by other processing
            return `__CODE_BLOCK_${index}__`;
        });

        // Step 3: Process inline elements
        // Order matters: process bold before italic to handle **text** correctly

        // Bold: **text** -> <strong>text</strong>
        html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

        // Italic: *text* -> <em>text</em>
        html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');

        // Inline code: `code` -> <code>code</code>
        html = html.replace(/`([^`]+)`/g, '<code>$1</code>');

        // Links: [text](url) -> <a href="url" target="_blank">text</a>
        html = html.replace(
            /\[([^\]]+)\]\(([^)]+)\)/g,
            '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>'
        );

        // Step 4: Process block elements (line by line)
        const lines = html.split('\n');
        const processed = [];
        let inList = false;
        let listItems = [];

        /**************************************************************/
        /**
         * Flushes accumulated list items to the processed output.
         *
         * @description
         * Called when exiting a list context (encountering non-list content).
         * Wraps accumulated <li> elements in a <ul> container.
         */
        /**************************************************************/
        function flushList() {
            if (listItems.length > 0) {
                processed.push(
                    '<ul style="padding-left:15px;margin:0.25rem 0;">' +
                    listItems.join('') +
                    '</ul>'
                );
                listItems = [];
                inList = false;
            }
        }

        // Process each line
        for (const line of lines) {
            // Headers: ### -> h3, ## -> h2, # -> h1
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
            // Horizontal rule: --- or *** or ___
            else if (/^[-*_]{3,}$/.test(line.trim())) {
                flushList();
                processed.push('<hr>');
            }
            // Blockquote: > text (escaped as &gt; after HTML escaping)
            else if (line.startsWith('&gt; ')) {
                flushList();
                processed.push('<blockquote>' + line.slice(5) + '</blockquote>');
            }
            // Unordered list: - item or * item
            else if (/^[-*]\s/.test(line)) {
                inList = true;
                listItems.push('<li>' + line.slice(2) + '</li>');
            }
            // Numbered list: 1. item
            else if (/^\d+\.\s/.test(line)) {
                flushList();
                // Extract the number for display
                const num = line.match(/^\d+/)[0];
                const content = line.replace(/^\d+\.\s/, '');
                processed.push(
                    '<div style="display:flex;gap:0.5rem;margin:0.25rem 0;">' +
                    '<span style="color:var(--color-text-muted);font-family:var(--font-mono);font-size:0.875rem;">' +
                    num + '.</span><span>' + content + '</span></div>'
                );
            }
            // Code block placeholder: pass through unchanged
            else if (line.includes('__CODE_BLOCK_')) {
                flushList();
                processed.push(line);
            }
            // Empty line: add spacing
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

        // Flush any remaining list items
        flushList();

        // Join processed lines
        html = processed.join('');

        // Step 5: Restore code blocks with full formatting
        codeBlocks.forEach((block, index) => {
            // Store for copy functionality
            codeBlockStorage.push(block);

            const codeBlockHtml = `
                <div class="code-block">
                    <div class="code-header">
                        <span class="code-language">${ChatUtils.escapeHtml(block.lang)}</span>
                        <button class="code-copy-btn" onclick="MedRecProChat.copyCode(this, '${index}')">
                            <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                            </svg>
                            Copy
                        </button>
                    </div>
                    <div class="code-content">
                        <pre>${ChatUtils.escapeHtml(block.code)}</pre>
                    </div>
                </div>
            `;

            html = html.replace(`__CODE_BLOCK_${index}__`, codeBlockHtml);
        });

        return html;
    }

    /**************************************************************/
    /**
     * Copies code block content to clipboard.
     *
     * @param {HTMLElement} btn - The copy button element that was clicked
     * @param {string} index - Code block index (string from onclick handler)
     *
     * @description
     * Extracts code from the nearest <pre> element within the code block
     * and copies it to the clipboard. Updates button UI to show success state.
     *
     * The button shows "Copied!" with a checkmark for 2 seconds before
     * reverting to the original "Copy" state.
     *
     * @example
     * // Called from onclick handler in code block HTML
     * <button onclick="MedRecProChat.copyCode(this, '0')">Copy</button>
     *
     * @see render - Generates code blocks with copy buttons
     */
    /**************************************************************/
    function copyCode(btn, index) {
        // Find the code content within this block
        const codeContent = btn.closest('.code-block').querySelector('pre').textContent;

        // Copy to clipboard using modern async API
        navigator.clipboard.writeText(codeContent).then(() => {
            // Save original button state
            const originalHtml = btn.innerHTML;

            // Show success state
            btn.classList.add('copied');
            btn.innerHTML = `
                <svg class="icon-sm" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
                Copied!
            `;

            // Revert after 2 seconds
            setTimeout(() => {
                btn.classList.remove('copied');
                btn.innerHTML = originalHtml;
            }, 2000);
        }).catch(err => {
            console.error('[MarkdownRenderer] Clipboard write failed:', err);
        });
    }

    /**************************************************************/
    /**
     * Clears the code block storage.
     *
     * @description
     * Should be called when starting a new conversation or clearing
     * messages to prevent memory buildup from accumulated code blocks.
     *
     * @see ChatState.clearConversation - Should call this when clearing
     */
    /**************************************************************/
    function clearCodeBlockStorage() {
        codeBlockStorage.length = 0;
    }

    /**************************************************************/
    /**
     * Public API for the markdown rendering module.
     *
     * @description
     * Exposes rendering function and code copy handler for external use.
     */
    /**************************************************************/
    return {
        // Main rendering function
        render: render,

        // Code block utilities
        copyCode: copyCode,
        clearCodeBlockStorage: clearCodeBlockStorage
    };
})();
