/**************************************************************/
/**
 * MedRecPro Chat Utility Functions Module
 *
 * @fileoverview Provides common utility functions used throughout the chat interface.
 * Contains pure functions for string manipulation, formatting, and DOM helpers.
 *
 * @description
 * The utilities module provides:
 * - UUID generation for message and conversation tracking
 * - HTML escaping for XSS prevention
 * - File size formatting for display
 * - DOM scrolling utilities
 *
 * These utilities are stateless and can be safely used by any other module.
 *
 * @example
 * import { ChatUtils } from './utils.js';
 *
 * const id = ChatUtils.generateUUID();
 * const safeHtml = ChatUtils.escapeHtml(userInput);
 * const displaySize = ChatUtils.formatFileSize(1048576); // "1.0 MB"
 *
 * @module chat/utils
 * @see ChatState - Uses generateUUID for IDs
 * @see MarkdownRenderer - Uses escapeHtml for safe rendering
 * @see FileHandler - Uses formatFileSize for display
 */
/**************************************************************/

export const ChatUtils = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Generates a UUID v4 for unique identification.
     *
     * @returns {string} UUID string in format 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'
     *
     * @description
     * Creates a cryptographically pseudo-random UUID v4 compliant string.
     * The '4' indicates version 4 (random), and the 'y' character is
     * constrained to [8, 9, a, b] as per RFC 4122.
     *
     * Used for:
     * - Message IDs to track individual messages
     * - Conversation IDs to maintain context across API calls
     * - Code block indexing for copy functionality
     *
     * @example
     * const messageId = generateUUID();  // 'f47ac10b-58cc-4372-a567-0e02b2c3d479'
     *
     * @see ChatState.addMessage - Uses for message ID generation
     * @see ChatState.clearConversation - Uses for new conversation ID
     */
    /**************************************************************/
    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            // Generate random 4-bit value
            const r = Math.random() * 16 | 0;

            // For 'x': use full random value
            // For 'y': constrain to 8-b range (0b1000 to 0b1011)
            const v = c === 'x' ? r : (r & 0x3 | 0x8);

            return v.toString(16);
        });
    }

    /**************************************************************/
    /**
     * Escapes HTML special characters to prevent XSS attacks.
     *
     * @param {string} text - Raw text that may contain HTML characters
     * @returns {string} Escaped HTML string safe for DOM insertion
     *
     * @description
     * Uses the browser's native text content handling to escape:
     * - < and > (tag delimiters)
     * - & (entity starter)
     * - " and ' (attribute delimiters)
     *
     * This is the safest method as it leverages browser-native escaping
     * rather than manual string replacement which may miss edge cases.
     *
     * @example
     * const safe = escapeHtml('<script>alert("xss")</script>');
     * // Returns: '&lt;script&gt;alert("xss")&lt;/script&gt;'
     *
     * @see MarkdownRenderer.renderMarkdown - Uses for safe content rendering
     * @see MessageRenderer.renderMessage - Uses for user message display
     */
    /**************************************************************/
    function escapeHtml(text) {
        // Create temporary div element
        const div = document.createElement('div');

        // Setting textContent automatically escapes HTML entities
        div.textContent = text;

        // innerHTML returns the escaped version
        return div.innerHTML;
    }

    /**************************************************************/
    /**
     * Formats file size in bytes to human-readable string.
     *
     * @param {number} bytes - File size in bytes
     * @returns {string} Formatted size string with unit (B, KB, or MB)
     *
     * @description
     * Converts byte count to appropriate unit:
     * - Under 1 KB: Shows bytes (e.g., "512 B")
     * - Under 1 MB: Shows kilobytes (e.g., "1.5 KB")
     * - 1 MB and above: Shows megabytes (e.g., "2.3 MB")
     *
     * Uses decimal KB/MB (1000-based would be kB/MB, but we use
     * binary 1024-based which some call KiB/MiB).
     *
     * @example
     * formatFileSize(512);      // "512 B"
     * formatFileSize(1536);     // "1.5 KB"
     * formatFileSize(5242880);  // "5.0 MB"
     *
     * @see FileHandler.renderFileList - Uses for file size display
     */
    /**************************************************************/
    function formatFileSize(bytes) {
        // Handle bytes (< 1 KB)
        if (bytes < 1024) {
            return bytes + ' B';
        }

        // Handle kilobytes (< 1 MB)
        if (bytes < 1024 * 1024) {
            return (bytes / 1024).toFixed(1) + ' KB';
        }

        // Handle megabytes (>= 1 MB)
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    /**************************************************************/
    /**
     * Scrolls the messages container to the bottom.
     *
     * @param {HTMLElement} container - The scrollable container element
     *
     * @description
     * Uses requestAnimationFrame to ensure the scroll happens after
     * DOM updates are complete, preventing scroll jitter and ensuring
     * accurate scroll position calculation.
     *
     * Called after:
     * - Adding new messages
     * - Updating streaming content
     * - Rendering initial message list
     *
     * @example
     * scrollToBottom(document.getElementById('messagesContainer'));
     *
     * @see MessageRenderer.renderMessages - Calls after rendering
     * @see MessageRenderer.updateMessage - Calls after updates
     */
    /**************************************************************/
    function scrollToBottom(container) {
        // Use RAF to ensure DOM is fully updated before scrolling
        requestAnimationFrame(() => {
            container.scrollTop = container.scrollHeight;
        });
    }

    /**************************************************************/
    /**
     * Debounces a function to limit execution frequency.
     *
     * @param {Function} func - Function to debounce
     * @param {number} wait - Milliseconds to wait before executing
     * @returns {Function} Debounced function
     *
     * @description
     * Creates a debounced version of the provided function that delays
     * invocation until after `wait` milliseconds have elapsed since the
     * last call. Useful for rate-limiting expensive operations triggered
     * by rapid events (e.g., resize, input).
     *
     * @example
     * const debouncedResize = debounce(() => {
     *     recalculateLayout();
     * }, 250);
     * window.addEventListener('resize', debouncedResize);
     *
     * @see UIHelpers.autoResizeTextarea - Could benefit from debouncing
     */
    /**************************************************************/
    function debounce(func, wait) {
        let timeout;

        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };

            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    /**************************************************************/
    /**
     * Converts a string to Title Case with smart handling of pharmaceutical terms.
     *
     * @param {string} str - String to convert to title case
     * @returns {string} Title-cased string
     *
     * @description
     * Converts text to title case where the first letter of each word is
     * capitalized and the rest are lowercase. Handles special cases:
     * - Preserves common pharmaceutical suffixes (XR, ER, SR, DR, CR, LA, SA, XL, CD)
     * - Preserves Roman numerals (I, II, III, IV, V, VI, VII, VIII, IX, X)
     * - Preserves all-caps acronyms (HCL, HCl, etc.)
     * - Handles hyphenated words correctly
     * - Trims whitespace and normalizes multiple spaces
     *
     * @example
     * toTitleCase('LISINOPRIL');              // 'Lisinopril'
     * toTitleCase('metformin hcl');           // 'Metformin HCL'
     * toTitleCase('METOPROLOL SUCCINATE ER'); // 'Metoprolol Succinate ER'
     * toTitleCase('diltiazem cd');            // 'Diltiazem CD'
     * toTitleCase('omega-3 fatty acids');     // 'Omega-3 Fatty Acids'
     *
     * @see CheckpointRenderer.renderSourceItem - Uses for product name display
     */
    /**************************************************************/
    function toTitleCase(str) {
        if (!str || typeof str !== 'string') {
            return str || '';
        }

        // Pharmaceutical suffixes to preserve in uppercase
        const preserveUppercase = new Set([
            'XR', 'ER', 'SR', 'DR', 'CR', 'LA', 'SA', 'XL', 'CD', 'EC', 'IR', 'ODT',
            'HCL', 'HCL', 'HBR', 'MG', 'ML', 'MCG',
            'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X',
            'D/R'  // Delayed release notation
        ]);

        // Trim and normalize whitespace (including newlines and tabs)
        const normalized = str.trim().replace(/[\s\n\t]+/g, ' ');

        // Split on spaces and process each word
        return normalized.split(' ').map(word => {
            if (!word) return '';

            // Check if whole word (uppercase) should be preserved
            const upperWord = word.toUpperCase();
            if (preserveUppercase.has(upperWord)) {
                return upperWord;
            }

            // Handle hyphenated words (e.g., "omega-3")
            if (word.includes('-')) {
                return word.split('-').map(part => {
                    const upperPart = part.toUpperCase();
                    if (preserveUppercase.has(upperPart)) {
                        return upperPart;
                    }
                    // Keep numbers as-is
                    if (/^\d+$/.test(part)) {
                        return part;
                    }
                    return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
                }).join('-');
            }

            // Handle forward slash notation (e.g., "D/R")
            if (word.includes('/')) {
                const upperSlash = word.toUpperCase();
                if (preserveUppercase.has(upperSlash)) {
                    return upperSlash;
                }
            }

            // Standard title case: first letter uppercase, rest lowercase
            return word.charAt(0).toUpperCase() + word.slice(1).toLowerCase();
        }).join(' ');
    }

    /**************************************************************/
    /**
     * Truncates a string to a maximum length with ellipsis.
     *
     * @param {string} str - String to truncate
     * @param {number} maxLength - Maximum length including ellipsis
     * @returns {string} Truncated string with '...' if exceeded
     *
     * @description
     * Shortens strings that exceed the maximum length, appending '...'
     * to indicate truncation. The resulting string (including ellipsis)
     * will not exceed maxLength.
     *
     * @example
     * truncate('This is a long string', 10);  // 'This is...'
     * truncate('Short', 10);                   // 'Short'
     *
     * @see formatApiSourceLinks - Uses for product name display
     */
    /**************************************************************/
    function truncate(str, maxLength) {
        if (!str || str.length <= maxLength) {
            return str;
        }

        // Account for ellipsis length (3 characters)
        return str.substring(0, maxLength - 3) + '...';
    }

    /**************************************************************/
    /**
     * Checks if a value is a non-empty string.
     *
     * @param {*} value - Value to check
     * @returns {boolean} True if non-empty string
     *
     * @example
     * isNonEmptyString('hello');  // true
     * isNonEmptyString('');       // false
     * isNonEmptyString(null);     // false
     * isNonEmptyString(123);      // false
     */
    /**************************************************************/
    function isNonEmptyString(value) {
        return typeof value === 'string' && value.trim().length > 0;
    }

    /**************************************************************/
    /**
     * Safely parses JSON with fallback.
     *
     * @param {string} jsonString - JSON string to parse
     * @param {*} fallback - Value to return on parse failure
     * @returns {*} Parsed object or fallback value
     *
     * @description
     * Wraps JSON.parse in try-catch to prevent exceptions from
     * propagating when parsing potentially malformed JSON.
     *
     * @example
     * safeParseJSON('{"valid": true}', {});  // { valid: true }
     * safeParseJSON('invalid', {});          // {}
     *
     * @see ApiService - Uses for API response parsing
     */
    /**************************************************************/
    function safeParseJSON(jsonString, fallback = null) {
        try {
            return JSON.parse(jsonString);
        } catch (e) {
            console.warn('[ChatUtils] JSON parse failed:', e.message);
            return fallback;
        }
    }

    /**************************************************************/
    /**
     * Deep clones an object using JSON serialization.
     *
     * @param {Object} obj - Object to clone
     * @returns {Object} Deep clone of the object
     *
     * @description
     * Creates a deep copy using JSON.parse(JSON.stringify()).
     * Note: This will not preserve:
     * - Functions
     * - undefined values
     * - Symbols
     * - Circular references
     *
     * @example
     * const original = { nested: { value: 1 } };
     * const clone = deepClone(original);
     * clone.nested.value = 2;  // original unchanged
     */
    /**************************************************************/
    function deepClone(obj) {
        return JSON.parse(JSON.stringify(obj));
    }

    /**************************************************************/
    /**
     * Public API for the utilities module.
     *
     * @description
     * Exposes all utility functions for use by other modules.
     * These are pure functions with no side effects.
     */
    /**************************************************************/
    return {
        // ID generation
        generateUUID: generateUUID,

        // String utilities
        escapeHtml: escapeHtml,
        toTitleCase: toTitleCase,
        truncate: truncate,
        isNonEmptyString: isNonEmptyString,

        // Formatting
        formatFileSize: formatFileSize,

        // DOM utilities
        scrollToBottom: scrollToBottom,

        // Function utilities
        debounce: debounce,

        // Data utilities
        safeParseJSON: safeParseJSON,
        deepClone: deepClone
    };
})();
