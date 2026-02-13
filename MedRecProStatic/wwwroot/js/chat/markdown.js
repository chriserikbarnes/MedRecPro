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
 * - Markdown tables with styled HTML output
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
import { ChatConfig } from './config.js';

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
     * Processes markdown tables and converts them to styled HTML tables.
     *
     * @param {string} text - Text containing potential markdown tables
     * @returns {string} Text with markdown tables converted to HTML
     *
     * @description
     * Parses markdown table syntax:
     * | Header 1 | Header 2 |
     * |----------|----------|
     * | Cell 1   | Cell 2   |
     *
     * Converts to styled HTML tables with:
     * - Proper thead/tbody structure
     * - Styled headers and cells
     * - Responsive overflow handling
     * - Dark theme compatible colors
     *
     * @example
     * processMarkdownTables('| Name | Value |\n|------|-------|\n| A | 1 |');
     * // Returns styled HTML table
     */
    /**************************************************************/
    function processMarkdownTables(text) {
        const lines = text.split('\n');
        const result = [];
        let tableLines = [];
        let inTable = false;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();

            // Check if line looks like a table row (starts and ends with |, or has | separators)
            const isTableRow = /^\|.*\|$/.test(line) || /^[^|]+\|[^|]+/.test(line);
            // Check if line is a separator row (contains only |, -, :, and spaces)
            const isSeparatorRow = /^\|?[\s\-:|]+\|?$/.test(line) && line.includes('-');

            if (isTableRow || (inTable && isSeparatorRow)) {
                inTable = true;
                tableLines.push(line);
            } else {
                // End of table or not a table
                if (inTable && tableLines.length >= 2) {
                    // We have a complete table, convert it
                    const tableHtml = convertTableToHtml(tableLines);
                    result.push(tableHtml);
                }
                tableLines = [];
                inTable = false;
                result.push(lines[i]); // Keep original line (not trimmed)
            }
        }

        // Handle table at end of content
        if (inTable && tableLines.length >= 2) {
            const tableHtml = convertTableToHtml(tableLines);
            result.push(tableHtml);
        }

        return result.join('\n');
    }

    /**************************************************************/
    /**
     * Converts parsed table lines to a styled HTML table.
     *
     * @param {string[]} tableLines - Array of table row strings
     * @returns {string} Styled HTML table string
     *
     * @description
     * Takes raw markdown table lines and produces a complete HTML table
     * with inline styles for consistent rendering across themes.
     */
    /**************************************************************/
    function convertTableToHtml(tableLines) {
        if (tableLines.length < 2) return tableLines.join('\n');

        // Parse cells from a table row
        const parseCells = (row) => {
            // Remove leading/trailing pipes and split by |
            let cells = row.replace(/^\||\|$/g, '').split('|');
            return cells.map(cell => cell.trim());
        };

        // Find the separator row (contains dashes)
        let separatorIndex = -1;
        for (let i = 0; i < tableLines.length; i++) {
            if (/^[\s|:-]+$/.test(tableLines[i].replace(/-/g, '')) && tableLines[i].includes('-')) {
                separatorIndex = i;
                break;
            }
        }

        // If no separator found, treat first row as header
        if (separatorIndex === -1) {
            separatorIndex = 1;
        }

        // Parse alignment from separator row
        const alignments = [];
        if (separatorIndex > 0 && separatorIndex < tableLines.length) {
            const sepCells = parseCells(tableLines[separatorIndex]);
            sepCells.forEach(cell => {
                const trimmed = cell.trim();
                if (trimmed.startsWith(':') && trimmed.endsWith(':')) {
                    alignments.push('center');
                } else if (trimmed.endsWith(':')) {
                    alignments.push('right');
                } else {
                    alignments.push('left');
                }
            });
        }

        // Build HTML table
        const tableStyle = 'width:100%;border-collapse:collapse;margin:0.75rem 0;font-size:0.9rem;';
        const thStyle = 'padding:0.5rem 0.75rem;text-align:left;border-bottom:2px solid var(--color-border, #444);background:var(--color-bg-secondary, #2a2a2a);color:var(--color-text, #fff);font-weight:600;';
        const tdStyle = 'padding:0.5rem 0.75rem;border-bottom:1px solid var(--color-border, #333);color:var(--color-text-secondary, #ccc);';

        let html = `<div style="overflow-x:auto;margin:0.5rem 0;"><table style="${tableStyle}">`;

        // Detect column count from header for fixed-width columns
        const colCountCells = parseCells(tableLines[0]);
        const columnCount = colCountCells.length;

        // For 2-column tables, set fixed proportional widths for consistent alignment
        if (columnCount === 2) {
            html += '<colgroup><col style="width:70%"><col style="width:30%"></colgroup>';
        }

        // Header rows (everything before separator)
        if (separatorIndex > 0) {
            html += '<thead><tr>';
            const headerCells = parseCells(tableLines[0]);
            headerCells.forEach((cell, idx) => {
                const align = alignments[idx] || 'left';
                html += `<th style="${thStyle}text-align:${align};">${cell}</th>`;
            });
            html += '</tr></thead>';
        }

        // Body rows (everything after separator)
        html += '<tbody>';
        for (let i = separatorIndex + 1; i < tableLines.length; i++) {
            const cells = parseCells(tableLines[i]);
            // Skip empty rows
            if (cells.length === 0 || (cells.length === 1 && cells[0] === '')) continue;

            html += '<tr>';
            cells.forEach((cell, idx) => {
                const align = alignments[idx] || 'left';
                // Add hover effect via alternating row colors
                const rowBg = (i - separatorIndex) % 2 === 0 ? 'background:var(--color-bg-tertiary, #252525);' : '';
                html += `<td style="${tdStyle}text-align:${align};${rowBg}">${cell}</td>`;
            });
            html += '</tr>';
        }
        html += '</tbody></table></div>';

        return html;
    }

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
     * - # Header -> <h1>Header</h1> (supports h1-h6)
     * - - item -> <li>item</li> (unordered list)
     * - 1. item -> numbered list item
     * - > quote -> <blockquote>quote</blockquote>
     * - --- -> <hr>
     * - ```lang\ncode\n``` -> code block with copy button
     * - | Col | Col | -> styled HTML table with headers
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

        // Check if content contains trusted HTML blocks from internal renderers
        // (settings-renderer.js outputs HTML tables with inline styles)
        // Server-side handles XSS protection, so we can safely render trusted HTML
        const hasTrustedHtml = /<table\s+style=/i.test(text) || /<div\s+style=/i.test(text);

        // Step 0: Extract raw HTML blocks (tables, divs with style) before escaping
        // These are trusted internal content from our renderers (settings-renderer.js)
        const htmlBlocks = [];
        let html = text;

        // Preserve table blocks (including nested content) - use greedy match for nested tables
        html = html.replace(/<table\s+style="[^"]*">[\s\S]*?<\/table>/gi, (match) => {
            const index = htmlBlocks.length;
            htmlBlocks.push(match);
            return `\n__HTML_BLOCK_${index}__\n`;
        });

        // Preserve div blocks with style attribute (trusted internal content)
        html = html.replace(/<div\s+style="[^"]*">[\s\S]*?<\/div>/gi, (match) => {
            const index = htmlBlocks.length;
            htmlBlocks.push(match);
            return `\n__HTML_BLOCK_${index}__\n`;
        });

        // Step 1: Escape HTML to prevent XSS attacks (remaining content)
        // Skip escaping if we detected trusted HTML - server handles XSS
        if (!hasTrustedHtml) {
            html = ChatUtils.escapeHtml(html);
        }

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
        // For relative API URLs (starting with /api/), prepend the base URL
        // This ensures links work correctly in local development (different ports)
        html = html.replace(
            /\[([^\]]+)\]\(([^)]+)\)/g,
            (match, text, url) => {
                // If URL starts with /api/, prepend the configured base URL
                const finalUrl = url.startsWith('/api/') ? ChatConfig.buildUrl(url) : url;
                return `<a href="${finalUrl}" target="_blank" rel="noopener noreferrer">${text}</a>`;
            }
        );

        // Step 3.5: Process markdown tables before block elements
        // Markdown table format:
        // | Header 1 | Header 2 |
        // |----------|----------|
        // | Cell 1   | Cell 2   |
        html = processMarkdownTables(html);

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
            // Headers: ###### -> h6, ##### -> h5, #### -> h4, ### -> h3, ## -> h2, # -> h1
            // Check longer patterns first to avoid partial matches
            // Also handle edge case of # symbols without text (render as hr or skip)
            if (/^#{1,6}\s*$/.test(line)) {
                // Bare # symbols without text - treat as horizontal rule
                flushList();
                processed.push('<hr>');
            } else if (line.startsWith('###### ')) {
                flushList();
                processed.push('<h6>' + line.slice(7) + '</h6>');
            } else if (line.startsWith('##### ')) {
                flushList();
                processed.push('<h5>' + line.slice(6) + '</h5>');
            } else if (line.startsWith('#### ')) {
                flushList();
                processed.push('<h4>' + line.slice(5) + '</h4>');
            } else if (line.startsWith('### ')) {
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
            // HTML block placeholder: pass through unchanged
            else if (line.includes('__HTML_BLOCK_')) {
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

        // Step 6: Restore raw HTML blocks (trusted internal content)
        htmlBlocks.forEach((block, index) => {
            html = html.replace(`__HTML_BLOCK_${index}__`, block);
        });

        // Step 7: Prune redundant horizontal rules
        // Removes consecutive <hr> tags with only empty spacing between them
        html = pruneConsecutiveHorizontalRules(html);

        return html;
    }

    /**************************************************************/
    /**
     * Removes consecutive horizontal rules with no meaningful content between them.
     *
     * @param {string} html - The HTML string to process
     * @returns {string} HTML with consecutive horizontal rules collapsed to a single rule
     *
     * @description
     * During batch synthesis, content can produce patterns like:
     * - `<hr><hr>` (consecutive rules)
     * - `<hr><div style="height:0.5rem;"></div><hr>` (rules with only spacing)
     * - `<hr><p></p><hr>` (rules with empty paragraphs)
     *
     * This helper collapses these to a single `<hr>` for cleaner output.
     *
     * @example
     * pruneConsecutiveHorizontalRules('<hr><hr>');
     * // Returns: '<hr>'
     *
     * @example
     * pruneConsecutiveHorizontalRules('<hr><div style="height:0.5rem;"></div><hr>');
     * // Returns: '<hr>'
     */
    /**************************************************************/
    function pruneConsecutiveHorizontalRules(html) {
        // Pattern matches <hr> followed by optional empty content, then another <hr>
        // Empty content includes: whitespace, empty divs, empty paragraphs, empty spacing divs
        const emptyContentPattern = /(<hr>)(\s*(?:<div[^>]*>\s*<\/div>\s*|<p>\s*<\/p>\s*)*)+(<hr>)/gi;

        // Keep replacing until no more matches (handles multiple consecutive rules)
        let previous;
        do {
            previous = html;
            html = html.replace(emptyContentPattern, '$1');
        } while (html !== previous);

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
        clearCodeBlockStorage: clearCodeBlockStorage,

        // HTML cleanup utilities
        pruneConsecutiveHorizontalRules: pruneConsecutiveHorizontalRules
    };
})();
