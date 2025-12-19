/**************************************************************/
/**
 * MedRecPro Chat Settings Renderer Module
 *
 * @fileoverview Renders settings and log data from the Settings API endpoints.
 * Provides specialized formatting for log statistics, log entries, categories, and users.
 *
 * @description
 * The settings renderer module provides:
 * - Detection of settings/logs endpoint responses
 * - Log statistics card rendering with visual indicators
 * - Log entries table rendering with level-based styling
 * - Log categories and users list rendering
 * - Pagination information display
 * - Level-based color coding (Error, Warning, Information, etc.)
 *
 * @example
 * import { SettingsRenderer } from './settings-renderer.js';
 *
 * // Check if results are from settings endpoints
 * if (SettingsRenderer.isSettingsResponse(results)) {
 *     const html = SettingsRenderer.renderSettingsData(results);
 * }
 *
 * @module chat/settings-renderer
 * @see MessageRenderer - Parent rendering module
 * @see EndpointExecutor - Provides executed endpoint results
 */
/**************************************************************/

import { ChatUtils } from './utils.js';

export const SettingsRenderer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Log level color mappings for visual distinction.
     *
     * @type {Object}
     * @description
     * Background colors for log level badges.
     */
    /**************************************************************/
    const LOG_LEVEL_COLORS = {
        'Trace': { bg: '#6b7280', text: '#fff' },
        'Debug': { bg: '#9ca3af', text: '#fff' },
        'Information': { bg: '#3b82f6', text: '#fff' },
        'Warning': { bg: '#f59e0b', text: '#000' },
        'Error': { bg: '#ef4444', text: '#fff' },
        'Critical': { bg: '#991b1b', text: '#fff' }
    };

    /**************************************************************/
    /**
     * Common table styles for consistent rendering.
     */
    /**************************************************************/
    const TABLE_STYLES = {
        table: 'width:100%;border-collapse:collapse;margin:12px 0;font-size:14px;',
        th: 'text-align:left;padding:10px 12px;background:rgba(59,130,246,0.15);border-bottom:2px solid rgba(59,130,246,0.3);font-weight:600;color:#93c5fd;',
        thRight: 'text-align:right;padding:10px 12px;background:rgba(59,130,246,0.15);border-bottom:2px solid rgba(59,130,246,0.3);font-weight:600;color:#93c5fd;',
        td: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);vertical-align:top;',
        tdRight: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);text-align:right;vertical-align:top;',
        tdMono: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);font-family:monospace;font-size:13px;vertical-align:top;'
    };

    /**************************************************************/
    /**
     * Determines if API results are from settings/logs endpoints.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {boolean} True if any result is from a settings endpoint
     */
    /**************************************************************/
    function isSettingsResponse(results) {
        if (!results || !Array.isArray(results)) return false;

        return results.some(r => {
            const path = r.specification?.path?.toLowerCase() || '';
            return path.includes('/api/settings/logs') ||
                   path.includes('/api/settings/clearmanagedcache');
        });
    }

    /**************************************************************/
    /**
     * Determines the type of settings response.
     *
     * @param {Object} result - Single endpoint execution result
     * @returns {string} Response type: 'statistics', 'categories', 'users', 'entries', or 'unknown'
     */
    /**************************************************************/
    function getSettingsResponseType(result) {
        const path = result.specification?.path?.toLowerCase() || '';

        if (path.includes('/logs/statistics')) return 'statistics';
        if (path.includes('/logs/categories')) return 'categories';
        if (path.includes('/logs/users')) return 'users';
        if (path.includes('/logs/by-date') ||
            path.includes('/logs/by-category') ||
            path.includes('/logs/by-user') ||
            path.match(/\/logs\??/)) return 'entries';
        if (path.includes('/clearmanagedcache')) return 'cache';

        return 'unknown';
    }

    /**************************************************************/
    /**
     * Renders settings/logs data as formatted HTML.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {string} Formatted HTML string for display
     */
    /**************************************************************/
    function renderSettingsData(results) {
        if (!results || !Array.isArray(results)) return '';

        console.log('[SettingsRenderer] Processing results:', results.length, 'items');

        const sections = [];
        let hasAuthError = false;
        let hasPermissionError = false;

        for (const result of results) {
            console.log('[SettingsRenderer] Result:', result.specification?.path, 'status:', result.statusCode);

            // Check for authentication/authorization errors
            if (result.statusCode === 401) {
                console.log('[SettingsRenderer] Detected 401 auth error');
                hasAuthError = true;
                continue;
            }
            if (result.statusCode === 403) {
                console.log('[SettingsRenderer] Detected 403 permission error');
                hasPermissionError = true;
                continue;
            }
            if (!result.result || result.statusCode >= 400) {
                console.log('[SettingsRenderer] Skipping result - no data or error status');
                continue;
            }

            const type = getSettingsResponseType(result);
            let rendered = '';

            switch (type) {
                case 'statistics':
                    rendered = renderLogStatistics(result.result);
                    break;
                case 'categories':
                    rendered = renderLogCategories(result.result);
                    break;
                case 'users':
                    rendered = renderLogUsers(result.result);
                    break;
                case 'entries':
                    rendered = renderLogEntries(result.result);
                    break;
                case 'cache':
                    rendered = renderCacheResult(result.result);
                    break;
                default:
                    rendered = '```json\n' + JSON.stringify(result.result, null, 2) + '\n```';
            }

            if (rendered) {
                sections.push(rendered);
            }
        }

        // If no successful results, check for auth errors and provide helpful message
        if (sections.length === 0) {
            console.log('[SettingsRenderer] No successful results. hasAuthError:', hasAuthError, 'hasPermissionError:', hasPermissionError);
            if (hasAuthError) {
                return renderAuthError();
            }
            if (hasPermissionError) {
                return renderPermissionError();
            }
            console.log('[SettingsRenderer] Returning empty - no auth errors detected but no successful data');
        }

        return sections.join('\n\n---\n\n');
    }

    /**************************************************************/
    /**
     * Renders authentication required error message.
     *
     * @returns {string} Formatted HTML error message
     */
    /**************************************************************/
    function renderAuthError() {
        return `## Authentication Required

<div style="padding:16px;background:rgba(251,191,36,0.15);border-left:4px solid #fbbf24;border-radius:4px;margin:12px 0;">
<strong style="color:#fbbf24;">🔐 Please sign in to access log data</strong>
<p style="margin:8px 0 0 0;color:rgba(255,255,255,0.8);">
The application logs are restricted to authenticated users. Please sign in to view log statistics, entries, and system diagnostics.
</p>
</div>

**To access logs:**
- Sign in using the login button in the top navigation
- Ensure your account has the appropriate permissions
- Try your request again after signing in`;
    }

    /**************************************************************/
    /**
     * Renders permission denied error message.
     *
     * @returns {string} Formatted HTML error message
     */
    /**************************************************************/
    function renderPermissionError() {
        return `## Access Denied

<div style="padding:16px;background:rgba(239,68,68,0.15);border-left:4px solid #ef4444;border-radius:4px;margin:12px 0;">
<strong style="color:#ef4444;">🚫 Administrator access required</strong>
<p style="margin:8px 0 0 0;color:rgba(255,255,255,0.8);">
Viewing application logs requires administrator privileges. Your current account does not have permission to access this data.
</p>
</div>

**If you need access:**
- Contact your system administrator to request elevated permissions
- Verify you are signed in with the correct account
- Administrator role is required for log viewing and cache management`;
    }

    /**************************************************************/
    /**
     * Renders log statistics as formatted HTML tables.
     *
     * @param {Object} stats - Log statistics object from API
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderLogStatistics(stats) {
        if (!stats) return '';

        const lines = [];
        lines.push('## Log Statistics\n');

        // Overview section
        lines.push('### Overview\n');
        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Metric</th><th style="${TABLE_STYLES.thRight}">Value</th></tr></thead>`);
        lines.push('<tbody>');
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Total Entries</td><td style="${TABLE_STYLES.tdRight}"><strong>${formatNumber(stats.totalEntries)}</strong></td></tr>`);
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Categories</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(stats.categoryCount)}</td></tr>`);
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Unique Users</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(stats.uniqueUserCount)}</td></tr>`);
        lines.push('</tbody></table>\n');

        // Time range
        if (stats.oldestEntry || stats.newestEntry) {
            lines.push('### Time Range\n');
            lines.push(`<table style="${TABLE_STYLES.table}">`);
            lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Boundary</th><th style="${TABLE_STYLES.thRight}">Timestamp</th></tr></thead>`);
            lines.push('<tbody>');
            if (stats.oldestEntry) {
                lines.push(`<tr><td style="${TABLE_STYLES.td}">Oldest</td><td style="${TABLE_STYLES.tdRight}">${formatTimestamp(stats.oldestEntry)}</td></tr>`);
            }
            if (stats.newestEntry) {
                lines.push(`<tr><td style="${TABLE_STYLES.td}">Newest</td><td style="${TABLE_STYLES.tdRight}">${formatTimestamp(stats.newestEntry)}</td></tr>`);
            }
            lines.push('</tbody></table>\n');
        }

        // Entries by level
        if (stats.entriesByLevel && Object.keys(stats.entriesByLevel).length > 0) {
            const levelOrder = ['Critical', 'Error', 'Warning', 'Information', 'Debug', 'Trace'];
            const sortedLevels = Object.entries(stats.entriesByLevel)
                .sort((a, b) => levelOrder.indexOf(a[0]) - levelOrder.indexOf(b[0]));

            lines.push('### Entries by Level\n');
            lines.push(`<table style="${TABLE_STYLES.table}">`);
            lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Level</th><th style="${TABLE_STYLES.thRight}">Count</th></tr></thead>`);
            lines.push('<tbody>');

            for (const [level, count] of sortedLevels) {
                const levelBadge = renderLevelBadge(level);
                lines.push(`<tr><td style="${TABLE_STYLES.td}">${levelBadge}</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(count)}</td></tr>`);
            }

            lines.push('</tbody></table>\n');
        }

        // Configuration
        lines.push('### Configuration\n');
        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Setting</th><th style="${TABLE_STYLES.thRight}">Value</th></tr></thead>`);
        lines.push('<tbody>');
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Retention</td><td style="${TABLE_STYLES.tdRight}">${stats.retentionMinutes || 60} minutes</td></tr>`);
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Max per Category</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(stats.maxEntriesPerCategory)}</td></tr>`);
        lines.push(`<tr><td style="${TABLE_STYLES.td}">Max Total</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(stats.maxTotalEntries)}</td></tr>`);
        lines.push('</tbody></table>');

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders a styled level badge.
     *
     * @param {string} level - Log level name
     * @returns {string} HTML for styled badge
     */
    /**************************************************************/
    function renderLevelBadge(level) {
        const colors = LOG_LEVEL_COLORS[level] || { bg: '#6b7280', text: '#fff' };
        const indicator = getLevelIndicator(level);
        const style = `display:inline-block;padding:3px 8px;border-radius:4px;font-size:12px;font-weight:500;background:${colors.bg};color:${colors.text};`;
        return `<span style="${style}">${indicator} ${level}</span>`;
    }

    /**************************************************************/
    /**
     * Renders log categories as a formatted HTML table.
     *
     * @param {Array} categories - Array of category summary objects
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderLogCategories(categories) {
        if (!categories || !Array.isArray(categories) || categories.length === 0) {
            return '## Log Categories\n\nNo categories found.';
        }

        const lines = [];
        lines.push('## Log Categories\n');
        lines.push(`Found **${categories.length}** categories:\n`);

        const sorted = [...categories].sort((a, b) =>
            (b.entryCount || 0) - (a.entryCount || 0)
        );

        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Category</th><th style="${TABLE_STYLES.thRight}">Entries</th><th style="${TABLE_STYLES.thRight}">Newest Entry</th></tr></thead>`);
        lines.push('<tbody>');

        for (const cat of sorted) {
            const categoryName = cat.category || cat.name || 'Unknown';
            const shortName = escapeHtml(shortenCategoryName(categoryName));
            const count = formatNumber(cat.entryCount || 0);
            const newest = cat.newestEntry ? formatTimestamp(cat.newestEntry) : '-';
            lines.push(`<tr><td style="${TABLE_STYLES.tdMono}">${shortName}</td><td style="${TABLE_STYLES.tdRight}">${count}</td><td style="${TABLE_STYLES.tdRight}">${newest}</td></tr>`);
        }

        lines.push('</tbody></table>');
        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders log users as a formatted HTML table.
     *
     * @param {Array} users - Array of user summary objects
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderLogUsers(users) {
        if (!users || !Array.isArray(users) || users.length === 0) {
            return '## Log Users\n\nNo user activity found.';
        }

        const lines = [];
        lines.push('## Log Users\n');
        lines.push(`Found **${users.length}** users with log activity:\n`);

        const sorted = [...users].sort((a, b) =>
            (b.entryCount || 0) - (a.entryCount || 0)
        );

        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">User</th><th style="${TABLE_STYLES.thRight}">Entries</th><th style="${TABLE_STYLES.thRight}">Last Activity</th></tr></thead>`);
        lines.push('<tbody>');

        for (const user of sorted) {
            const userName = user.userName || user.userId || 'Unknown';
            const displayName = escapeHtml(truncateText(userName, 30));
            const count = formatNumber(user.entryCount || 0);
            const newest = user.newestEntry ? formatTimestamp(user.newestEntry) : '-';
            lines.push(`<tr><td style="${TABLE_STYLES.td}">${displayName}</td><td style="${TABLE_STYLES.tdRight}">${count}</td><td style="${TABLE_STYLES.tdRight}">${newest}</td></tr>`);
        }

        lines.push('</tbody></table>');
        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders log entries as a formatted HTML table.
     *
     * @param {Object} data - Log entries response object
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderLogEntries(data) {
        if (!data) return '';

        const entries = data.entries || data.Entries || [];
        const totalCount = data.totalCount || data.TotalCount || entries.length;
        const pageNumber = data.pageNumber || data.PageNumber || 1;
        const pageSize = data.pageSize || data.PageSize || 100;
        const totalPages = data.totalPages || data.TotalPages || 1;
        const filter = data.filter || data.Filter;

        const lines = [];
        lines.push('## Log Entries\n');

        // Show filter info if present
        if (filter) {
            const filterParts = [];
            if (filter.category || filter.Category) {
                filterParts.push(`Category: \`${filter.category || filter.Category}\``);
            }
            if (filter.userId || filter.UserId) {
                filterParts.push(`User: \`${truncateText(filter.userId || filter.UserId, 20)}\``);
            }
            if (filter.startDate || filter.StartDate) {
                filterParts.push(`From: ${formatTimestamp(filter.startDate || filter.StartDate)}`);
            }
            if (filter.endDate || filter.EndDate) {
                filterParts.push(`To: ${formatTimestamp(filter.endDate || filter.EndDate)}`);
            }
            if (filterParts.length > 0) {
                lines.push(`**Filter:** ${filterParts.join(' | ')}\n`);
            }
        }

        // Pagination info
        lines.push(`Showing page **${pageNumber}** of **${totalPages}** (${formatNumber(totalCount)} total entries)\n`);

        if (entries.length === 0) {
            lines.push('\n*No log entries match the current filter.*');
            return lines.join('\n');
        }

        // Entries table
        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Level</th><th style="${TABLE_STYLES.th}">Time</th><th style="${TABLE_STYLES.th}">Category</th><th style="${TABLE_STYLES.th}">Message</th></tr></thead>`);
        lines.push('<tbody>');

        for (const entry of entries) {
            const level = entry.level || entry.Level || 'Information';
            const levelBadge = renderLevelBadge(level);
            const timestamp = formatTimestamp(entry.timestamp || entry.Timestamp);
            const category = escapeHtml(shortenCategoryName(entry.category || entry.Category || ''));
            let message = entry.message || entry.Message || '';
            message = escapeHtml(truncateText(message, 60));

            lines.push(`<tr><td style="${TABLE_STYLES.td}">${levelBadge}</td><td style="${TABLE_STYLES.td};white-space:nowrap;">${timestamp}</td><td style="${TABLE_STYLES.tdMono}">${category}</td><td style="${TABLE_STYLES.td}">${message}</td></tr>`);

            // Show exception details if present
            const exceptionMsg = entry.exceptionMessage || entry.ExceptionMessage;
            const exceptionType = entry.exceptionType || entry.ExceptionType;
            if (exceptionMsg || exceptionType) {
                const exceptionInfo = exceptionType
                    ? `<strong>${escapeHtml(exceptionType)}</strong>: ${escapeHtml(truncateText(exceptionMsg || '', 50))}`
                    : escapeHtml(truncateText(exceptionMsg || '', 60));
                lines.push(`<tr><td colspan="4" style="${TABLE_STYLES.td};padding-left:24px;color:#f87171;font-size:13px;"><em>Exception: ${exceptionInfo}</em></td></tr>`);
            }
        }

        lines.push('</tbody></table>');

        // Pagination hint
        if (totalPages > 1 && pageNumber < totalPages) {
            lines.push(`\n*Use \`pageNumber=${pageNumber + 1}\` to see more entries.*`);
        }

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders cache clear result.
     *
     * @param {Object} result - Cache clear result object
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderCacheResult(result) {
        if (!result) return '';

        const success = result.success || result.Success;
        const message = result.message || result.Message || '';

        if (success) {
            return `## Cache Management\n\n<div style="padding:12px;background:rgba(34,197,94,0.15);border-left:4px solid #22c55e;border-radius:4px;"><strong>Cache cleared successfully.</strong></div>\n\n${message}`;
        } else {
            const error = result.error || result.Error || 'Unknown error';
            return `## Cache Management\n\n<div style="padding:12px;background:rgba(239,68,68,0.15);border-left:4px solid #ef4444;border-radius:4px;"><strong>Cache clear failed.</strong><br/>Error: ${escapeHtml(error)}</div>`;
        }
    }

    /**************************************************************/
    /**
     * Gets a visual indicator emoji for a log level.
     *
     * @param {string} level - Log level name
     * @returns {string} Emoji indicator for the level
     */
    /**************************************************************/
    function getLevelIndicator(level) {
        switch (level) {
            case 'Critical': return '🔴';
            case 'Error': return '❌';
            case 'Warning': return '⚠️';
            case 'Information': return 'ℹ️';
            case 'Debug': return '🔍';
            case 'Trace': return '⚪';
            default: return '•';
        }
    }

    /**************************************************************/
    /**
     * Formats a number with thousand separators.
     *
     * @param {number} num - Number to format
     * @returns {string} Formatted number string
     */
    /**************************************************************/
    function formatNumber(num) {
        if (num === null || num === undefined) return '0';
        return num.toLocaleString();
    }

    /**************************************************************/
    /**
     * Formats an ISO timestamp to a readable format.
     *
     * @param {string} timestamp - ISO timestamp string
     * @returns {string} Formatted date/time string
     */
    /**************************************************************/
    function formatTimestamp(timestamp) {
        if (!timestamp) return '-';

        try {
            const date = new Date(timestamp);
            return date.toLocaleString('en-US', {
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit',
                hour12: false
            });
        } catch (e) {
            return timestamp;
        }
    }

    /**************************************************************/
    /**
     * Shortens a category name for display.
     *
     * @param {string} category - Full category name
     * @returns {string} Shortened name
     */
    /**************************************************************/
    function shortenCategoryName(category) {
        if (!category) return '';
        const parts = category.split('.');
        return parts[parts.length - 1] || category;
    }

    /**************************************************************/
    /**
     * Truncates text to a maximum length with ellipsis.
     *
     * @param {string} text - Text to truncate
     * @param {number} maxLength - Maximum length before truncation
     * @returns {string} Truncated text with ellipsis if needed
     */
    /**************************************************************/
    function truncateText(text, maxLength) {
        if (!text) return '';
        if (text.length <= maxLength) return text;
        return text.substring(0, maxLength - 3) + '...';
    }

    /**************************************************************/
    /**
     * Escapes HTML special characters.
     *
     * @param {string} text - Text to escape
     * @returns {string} Escaped text safe for HTML
     */
    /**************************************************************/
    function escapeHtml(text) {
        if (!text) return '';
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    /**************************************************************/
    /**
     * Generates follow-up suggestions for log queries.
     *
     * @param {Array} results - Endpoint execution results
     * @returns {Array<string>} Array of suggested follow-up queries
     */
    /**************************************************************/
    function getLogFollowUpSuggestions(results) {
        const suggestions = [];

        for (const result of results) {
            const type = getSettingsResponseType(result);

            switch (type) {
                case 'statistics':
                    suggestions.push('Show me recent errors');
                    suggestions.push('What log categories are there?');
                    suggestions.push('Show logs from the last hour');
                    break;
                case 'entries':
                    const data = result.result;
                    if (data?.totalPages > 1 && data?.pageNumber < data?.totalPages) {
                        suggestions.push(`Show page ${(data.pageNumber || 1) + 1} of logs`);
                    }
                    suggestions.push('Filter logs by Error level');
                    suggestions.push('Show log categories');
                    break;
                case 'categories':
                    if (result.result && Array.isArray(result.result) && result.result.length > 0) {
                        const topCategory = result.result[0]?.category;
                        if (topCategory) {
                            const shortName = shortenCategoryName(topCategory);
                            suggestions.push(`Show logs from ${shortName}`);
                        }
                    }
                    break;
                case 'users':
                    if (result.result && Array.isArray(result.result) && result.result.length > 0) {
                        suggestions.push('Show logs for a specific user');
                    }
                    break;
            }
        }

        return [...new Set(suggestions)].slice(0, 3);
    }

    /**************************************************************/
    /**
     * Public API for the settings rendering module.
     */
    /**************************************************************/
    return {
        isSettingsResponse: isSettingsResponse,
        getSettingsResponseType: getSettingsResponseType,
        renderSettingsData: renderSettingsData,
        renderLogStatistics: renderLogStatistics,
        renderLogCategories: renderLogCategories,
        renderLogUsers: renderLogUsers,
        renderLogEntries: renderLogEntries,
        getLogFollowUpSuggestions: getLogFollowUpSuggestions,
        getLevelIndicator: getLevelIndicator
    };
})();
