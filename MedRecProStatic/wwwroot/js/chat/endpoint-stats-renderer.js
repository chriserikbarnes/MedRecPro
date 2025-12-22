/**************************************************************/
/**
 * MedRecPro Chat Endpoint Statistics Renderer Module
 *
 * @fileoverview Renders endpoint performance statistics and user activity data
 * from the Users API endpoints.
 *
 * @description
 * The endpoint stats renderer module provides:
 * - Detection of endpoint-stats and user activity responses
 * - Performance statistics table rendering with status indicators
 * - User activity table rendering with activity type styling
 * - Controller performance summary rendering
 * - Performance insights and recommendations generation
 *
 * @example
 * import { EndpointStatsRenderer } from './endpoint-stats-renderer.js';
 *
 * // Check if results are from endpoint stats endpoints
 * if (EndpointStatsRenderer.isEndpointStatsResponse(results)) {
 *     const html = EndpointStatsRenderer.renderEndpointStatsData(results);
 * }
 *
 * @module chat/endpoint-stats-renderer
 * @see MessageRenderer - Parent rendering module
 * @see EndpointExecutor - Provides executed endpoint results
 */
/**************************************************************/

import { ChatUtils } from './utils.js';

export const EndpointStatsRenderer = (function () {
    'use strict';

    /**************************************************************/
    /**
     * Status indicator mappings for performance thresholds.
     *
     * @type {Object}
     * @description
     * Color mappings based on execution time performance.
     */
    /**************************************************************/
    const PERFORMANCE_THRESHOLDS = {
        fast: { maxMs: 50, indicator: '\u2705', label: 'Active', color: '#22c55e' },
        normal: { maxMs: 200, indicator: '\u2705', label: 'Active', color: '#3b82f6' },
        slow: { maxMs: 1000, indicator: '\u26a0\ufe0f', label: 'Slow', color: '#f59e0b' },
        verySlow: { maxMs: Infinity, indicator: '\u274c', label: 'Very Slow', color: '#ef4444' }
    };

    /**************************************************************/
    /**
     * Activity type color mappings for visual distinction.
     *
     * @type {Object}
     */
    /**************************************************************/
    const ACTIVITY_TYPE_COLORS = {
        'Read': { bg: '#3b82f6', text: '#fff', indicator: '\ud83d\udcd6' },
        'Create': { bg: '#22c55e', text: '#fff', indicator: '\u2795' },
        'Update': { bg: '#f59e0b', text: '#000', indicator: '\u270f\ufe0f' },
        'Delete': { bg: '#ef4444', text: '#fff', indicator: '\ud83d\uddd1\ufe0f' },
        'Login': { bg: '#8b5cf6', text: '#fff', indicator: '\ud83d\udd10' }
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
        thCenter: 'text-align:center;padding:10px 12px;background:rgba(59,130,246,0.15);border-bottom:2px solid rgba(59,130,246,0.3);font-weight:600;color:#93c5fd;',
        td: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);vertical-align:top;',
        tdRight: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);text-align:right;vertical-align:top;',
        tdCenter: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);text-align:center;vertical-align:top;',
        tdMono: 'padding:8px 12px;border-bottom:1px solid rgba(255,255,255,0.1);font-family:monospace;font-size:13px;vertical-align:top;'
    };

    /**************************************************************/
    /**
     * Determines if API results are from endpoint-stats or user activity endpoints.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {boolean} True if any result is from an endpoint stats/activity endpoint
     */
    /**************************************************************/
    function isEndpointStatsResponse(results) {
        if (!results || !Array.isArray(results)) return false;

        return results.some(r => {
            const path = r.specification?.path?.toLowerCase() || '';
            return path.includes('/api/users/endpoint-stats') ||
                   path.includes('/api/users/user/') && path.includes('/activity');
        });
    }

    /**************************************************************/
    /**
     * Determines the type of endpoint stats response.
     *
     * @param {Object} result - Single endpoint execution result
     * @returns {string} Response type: 'endpoint-stats', 'user-activity', 'user-activity-daterange', or 'unknown'
     */
    /**************************************************************/
    function getResponseType(result) {
        const path = result.specification?.path?.toLowerCase() || '';

        if (path.includes('/endpoint-stats')) return 'endpoint-stats';
        if (path.includes('/activity/daterange')) return 'user-activity-daterange';
        if (path.includes('/activity')) return 'user-activity';

        return 'unknown';
    }

    /**************************************************************/
    /**
     * Renders endpoint stats/activity data as formatted HTML.
     *
     * @param {Array} results - Array of endpoint execution results
     * @returns {string} Formatted HTML string for display
     */
    /**************************************************************/
    function renderEndpointStatsData(results) {
        if (!results || !Array.isArray(results)) return '';

        console.log('[EndpointStatsRenderer] Processing results:', results.length, 'items');

        // Separate endpoint stats from user activity results
        const endpointStatsResults = [];
        const userActivityResults = [];
        let hasAuthError = false;
        let hasPermissionError = false;

        for (const result of results) {
            console.log('[EndpointStatsRenderer] Result:', result.specification?.path, 'status:', result.statusCode);

            // Check for authentication/authorization errors
            if (result.statusCode === 401) {
                hasAuthError = true;
                continue;
            }
            if (result.statusCode === 403) {
                hasPermissionError = true;
                continue;
            }
            if (!result.result || result.statusCode >= 400) {
                continue;
            }

            const type = getResponseType(result);
            if (type === 'endpoint-stats') {
                endpointStatsResults.push(result);
            } else if (type === 'user-activity' || type === 'user-activity-daterange') {
                userActivityResults.push(result);
            }
        }

        const sections = [];

        // Render endpoint stats as a performance summary table
        if (endpointStatsResults.length > 0) {
            sections.push(renderPerformanceSummary(endpointStatsResults));
        }

        // Render user activity results
        for (const result of userActivityResults) {
            sections.push(renderUserActivity(result.result, result.specification));
        }

        // If no successful results, check for auth errors
        if (sections.length === 0) {
            if (hasAuthError) {
                return renderAuthError();
            }
            if (hasPermissionError) {
                return renderPermissionError();
            }
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
<strong style="color:#fbbf24;">\ud83d\udd10 Please sign in to access this data</strong>
<p style="margin:8px 0 0 0;color:rgba(255,255,255,0.8);">
User activity and endpoint statistics are restricted to authenticated administrators. Please sign in to view this data.
</p>
</div>

**To access:**
- Sign in using the login button in the top navigation
- Ensure your account has administrator privileges
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
<strong style="color:#ef4444;">\ud83d\udeab Administrator access required</strong>
<p style="margin:8px 0 0 0;color:rgba(255,255,255,0.8);">
Viewing user activity and endpoint statistics requires administrator privileges. Your current account does not have permission.
</p>
</div>

**If you need access:**
- Contact your system administrator
- Verify you are signed in with the correct account
- Administrator role is required for these features`;
    }

    /**************************************************************/
    /**
     * Renders a performance summary table from multiple endpoint-stats results.
     *
     * @param {Array} results - Array of endpoint-stats results
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderPerformanceSummary(results) {
        if (!results || results.length === 0) return '';

        const lines = [];

        // Determine controller name from first result
        const firstResult = results[0]?.result;
        const controllerName = firstResult?.controllerName || 'Unknown';

        lines.push(`## Performance Summary for ${escapeHtml(controllerName)} Controller\n`);

        // Build table header
        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push('<thead><tr>');
        lines.push(`<th style="${TABLE_STYLES.th}">Endpoint</th>`);
        lines.push(`<th style="${TABLE_STYLES.thRight}">Total Calls</th>`);
        lines.push(`<th style="${TABLE_STYLES.thRight}">Avg Time (ms)</th>`);
        lines.push(`<th style="${TABLE_STYLES.thRight}">Min Time (ms)</th>`);
        lines.push(`<th style="${TABLE_STYLES.thRight}">Max Time (ms)</th>`);
        lines.push(`<th style="${TABLE_STYLES.thCenter}">Status</th>`);
        lines.push('</tr></thead>');
        lines.push('<tbody>');

        // Collect stats for insights
        const allStats = [];

        // Sort results by total activities descending, then by name
        const sortedResults = [...results].sort((a, b) => {
            const aTotal = a.result?.totalActivities || 0;
            const bTotal = b.result?.totalActivities || 0;
            if (bTotal !== aTotal) return bTotal - aTotal;
            const aName = a.result?.actionName || '';
            const bName = b.result?.actionName || '';
            return aName.localeCompare(bName);
        });

        for (const result of sortedResults) {
            const stats = result.result;
            if (!stats) continue;

            allStats.push(stats);

            const actionName = stats.actionName || 'Unknown';
            const totalCalls = stats.totalActivities || 0;
            const avgTime = stats.averageExecutionTimeMs;
            const minTime = stats.minExecutionTimeMs;
            const maxTime = stats.maxExecutionTimeMs;

            // Determine status based on usage and performance
            let statusHtml;
            if (totalCalls === 0) {
                statusHtml = renderStatusBadge('no-usage');
            } else {
                statusHtml = renderStatusBadge(getPerformanceStatus(avgTime));
            }

            // Format values
            const avgDisplay = avgTime !== null && avgTime !== undefined ? formatDecimal(avgTime, 2) : '-';
            const minDisplay = minTime !== null && minTime !== undefined ? formatNumber(minTime) : '-';
            const maxDisplay = maxTime !== null && maxTime !== undefined ? formatNumber(maxTime) : '-';

            lines.push('<tr>');
            lines.push(`<td style="${TABLE_STYLES.td}"><strong>${escapeHtml(actionName)}</strong></td>`);
            lines.push(`<td style="${TABLE_STYLES.tdRight}">${formatNumber(totalCalls)}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdRight}">${avgDisplay}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdRight}">${minDisplay}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdRight}">${maxDisplay}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdCenter}">${statusHtml}</td>`);
            lines.push('</tr>');
        }

        lines.push('</tbody></table>\n');

        // Add performance insights
        const insights = generatePerformanceInsights(allStats);
        if (insights) {
            lines.push(insights);
        }

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Gets performance status based on average execution time.
     *
     * @param {number} avgMs - Average execution time in milliseconds
     * @returns {string} Status key: 'fast', 'normal', 'slow', or 'verySlow'
     */
    /**************************************************************/
    function getPerformanceStatus(avgMs) {
        if (avgMs === null || avgMs === undefined) return 'normal';
        if (avgMs <= PERFORMANCE_THRESHOLDS.fast.maxMs) return 'fast';
        if (avgMs <= PERFORMANCE_THRESHOLDS.normal.maxMs) return 'normal';
        if (avgMs <= PERFORMANCE_THRESHOLDS.slow.maxMs) return 'slow';
        return 'verySlow';
    }

    /**************************************************************/
    /**
     * Renders a status badge for performance or usage.
     *
     * @param {string} status - Status key
     * @returns {string} HTML for styled badge
     */
    /**************************************************************/
    function renderStatusBadge(status) {
        if (status === 'no-usage') {
            const style = `display:inline-block;padding:3px 8px;border-radius:4px;font-size:12px;font-weight:500;background:#6b7280;color:#fff;`;
            return `<span style="${style}">\ud83d\udd0d No Usage</span>`;
        }

        const threshold = PERFORMANCE_THRESHOLDS[status] || PERFORMANCE_THRESHOLDS.normal;
        const style = `display:inline-block;padding:3px 8px;border-radius:4px;font-size:12px;font-weight:500;background:${threshold.color};color:#fff;`;
        return `<span style="${style}">${threshold.indicator} ${threshold.label}</span>`;
    }

    /**************************************************************/
    /**
     * Generates performance insights from stats data.
     *
     * @param {Array} allStats - Array of endpoint statistics
     * @returns {string} Formatted insights HTML
     */
    /**************************************************************/
    function generatePerformanceInsights(allStats) {
        if (!allStats || allStats.length === 0) return '';

        const lines = [];
        lines.push('### Performance Insights\n');

        // Filter stats with actual usage
        const usedEndpoints = allStats.filter(s => s.totalActivities > 0);
        const unusedEndpoints = allStats.filter(s => s.totalActivities === 0);

        if (usedEndpoints.length === 0) {
            lines.push('*No endpoint activity recorded in the analyzed period.*\n');
            return lines.join('\n');
        }

        // Most used endpoint
        const mostUsed = usedEndpoints.reduce((max, s) =>
            (s.totalActivities || 0) > (max.totalActivities || 0) ? s : max
        );
        if (mostUsed.totalActivities > 0) {
            const perfNote = mostUsed.averageExecutionTimeMs <= 50 ? ' - excellent performance' : '';
            lines.push(`- **Most Used**: ${mostUsed.actionName} (${formatNumber(mostUsed.totalActivities)} calls)${perfNote}`);
        }

        // Fastest endpoints (with usage)
        const withTimes = usedEndpoints.filter(s => s.minExecutionTimeMs !== null && s.minExecutionTimeMs !== undefined);
        if (withTimes.length > 0) {
            const fastest = withTimes.reduce((min, s) =>
                (s.minExecutionTimeMs || Infinity) < (min.minExecutionTimeMs || Infinity) ? s : min
            );
            const fastestMin = fastest.minExecutionTimeMs || 0;
            const avgNote = fastest.averageExecutionTimeMs ? ` and ${fastest.actionName} (${formatDecimal(fastest.averageExecutionTimeMs, 1)}ms minimum)` : '';
            lines.push(`- **Fastest**: ${fastest.actionName} (${formatNumber(fastestMin)}ms minimum)`);
        }

        // Slowest endpoint (by max time)
        const withMaxTimes = usedEndpoints.filter(s => s.maxExecutionTimeMs !== null && s.maxExecutionTimeMs !== undefined);
        if (withMaxTimes.length > 0) {
            const slowest = withMaxTimes.reduce((max, s) =>
                (s.maxExecutionTimeMs || 0) > (max.maxExecutionTimeMs || 0) ? s : max
            );
            if (slowest.maxExecutionTimeMs > 500) {
                lines.push(`- **Slowest**: ${slowest.actionName} (${formatNumber(slowest.maxExecutionTimeMs)}ms maximum) - may need optimization`);
            }
        }

        // Check for consistent performers (small range between min and max)
        const consistentEndpoints = usedEndpoints.filter(s => {
            if (!s.minExecutionTimeMs || !s.maxExecutionTimeMs) return false;
            const range = s.maxExecutionTimeMs - s.minExecutionTimeMs;
            return range < 100 && s.totalActivities >= 2;
        });
        if (consistentEndpoints.length > 0) {
            const example = consistentEndpoints[0];
            const range = (example.maxExecutionTimeMs || 0) - (example.minExecutionTimeMs || 0);
            lines.push(`- **Consistent**: ${example.actionName} shows stable performance (${example.minExecutionTimeMs}-${example.maxExecutionTimeMs}ms range)`);
        }

        // Recommendations section
        const recommendations = [];

        // Slow endpoints that need attention
        const slowEndpoints = usedEndpoints.filter(s =>
            s.averageExecutionTimeMs && s.averageExecutionTimeMs > 500
        );
        if (slowEndpoints.length > 0) {
            recommendations.push(`Monitor ${slowEndpoints.map(s => s.actionName).join(', ')} for potential optimization opportunities`);
        }

        // Unused endpoints
        if (unusedEndpoints.length > 0) {
            recommendations.push(`Consider investigating unused endpoints for potential deprecation or promotion`);
        }

        if (recommendations.length > 0) {
            lines.push('\n### Recommendations\n');
            recommendations.forEach(rec => {
                lines.push(`- ${rec}`);
            });
        }

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders user activity data as formatted HTML table.
     *
     * @param {Array} activities - Array of activity log entries
     * @param {Object} specification - Original request specification
     * @returns {string} Formatted HTML string
     */
    /**************************************************************/
    function renderUserActivity(activities, specification) {
        if (!activities || !Array.isArray(activities)) {
            return '## User Activity\n\n*No activity data available.*';
        }

        const lines = [];

        // Try to get user info from first activity
        const firstActivity = activities[0];
        const displayName = firstActivity?.displayName || 'Unknown User';
        const email = firstActivity?.email || '';

        lines.push(`## Activity Summary for ${escapeHtml(displayName)}`);
        if (email) {
            lines.push(`**Email:** ${escapeHtml(email)}\n`);
        }

        // Group activities by type for summary
        const activityCounts = {};
        for (const activity of activities) {
            const type = activity.activityType || 'Unknown';
            activityCounts[type] = (activityCounts[type] || 0) + 1;
        }

        // Activity type summary table
        if (Object.keys(activityCounts).length > 0) {
            lines.push('### Activity Summary\n');
            lines.push(`<table style="${TABLE_STYLES.table}">`);
            lines.push(`<thead><tr><th style="${TABLE_STYLES.th}">Activity Type</th><th style="${TABLE_STYLES.thRight}">Count</th></tr></thead>`);
            lines.push('<tbody>');

            const sortedTypes = Object.entries(activityCounts)
                .sort((a, b) => b[1] - a[1]);

            for (const [type, count] of sortedTypes) {
                const typeBadge = renderActivityTypeBadge(type);
                lines.push(`<tr><td style="${TABLE_STYLES.td}">${typeBadge}</td><td style="${TABLE_STYLES.tdRight}">${formatNumber(count)}</td></tr>`);
            }

            lines.push('</tbody></table>\n');
        }

        // Recent activity table
        lines.push('### Recent Activity\n');
        lines.push(`<table style="${TABLE_STYLES.table}">`);
        lines.push('<thead><tr>');
        lines.push(`<th style="${TABLE_STYLES.th}">Timestamp</th>`);
        lines.push(`<th style="${TABLE_STYLES.th}">Type</th>`);
        lines.push(`<th style="${TABLE_STYLES.th}">Description</th>`);
        lines.push(`<th style="${TABLE_STYLES.thCenter}">Status</th>`);
        lines.push(`<th style="${TABLE_STYLES.thRight}">Time (ms)</th>`);
        lines.push('</tr></thead>');
        lines.push('<tbody>');

        // Show up to 50 entries
        const displayActivities = activities.slice(0, 50);

        for (const activity of displayActivities) {
            const timestamp = formatTimestamp(activity.activityTimestamp);
            const typeBadge = renderActivityTypeBadge(activity.activityType);
            const description = escapeHtml(truncateText(activity.description || '', 40));
            const statusBadge = renderResultBadge(activity.result);
            const execTime = activity.executionTimeMs !== null && activity.executionTimeMs !== undefined
                ? formatNumber(activity.executionTimeMs)
                : '-';

            lines.push('<tr>');
            lines.push(`<td style="${TABLE_STYLES.td};white-space:nowrap;">${timestamp}</td>`);
            lines.push(`<td style="${TABLE_STYLES.td}">${typeBadge}</td>`);
            lines.push(`<td style="${TABLE_STYLES.td}">${description}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdCenter}">${statusBadge}</td>`);
            lines.push(`<td style="${TABLE_STYLES.tdRight}">${execTime}</td>`);
            lines.push('</tr>');
        }

        lines.push('</tbody></table>');

        // Pagination hint
        if (activities.length > 50) {
            lines.push(`\n*Showing first 50 of ${formatNumber(activities.length)} activities. Use pagination to see more.*`);
        }

        // Follow-up suggestions
        lines.push('\n**Suggested follow-ups:**');
        lines.push('- Filter by date range');
        lines.push('- Show endpoint performance for a specific controller');
        lines.push('- View activity for another user');

        return lines.join('\n');
    }

    /**************************************************************/
    /**
     * Renders an activity type badge.
     *
     * @param {string} type - Activity type (Read, Create, Update, Delete, Login)
     * @returns {string} HTML for styled badge
     */
    /**************************************************************/
    function renderActivityTypeBadge(type) {
        const colors = ACTIVITY_TYPE_COLORS[type] || { bg: '#6b7280', text: '#fff', indicator: '\u2022' };
        const style = `display:inline-block;padding:3px 8px;border-radius:4px;font-size:12px;font-weight:500;background:${colors.bg};color:${colors.text};`;
        return `<span style="${style}">${colors.indicator} ${escapeHtml(type)}</span>`;
    }

    /**************************************************************/
    /**
     * Renders a result status badge.
     *
     * @param {string} result - Result status (Success, Failure)
     * @returns {string} HTML for styled badge
     */
    /**************************************************************/
    function renderResultBadge(result) {
        if (result === 'Success') {
            const style = `display:inline-block;padding:2px 6px;border-radius:4px;font-size:11px;font-weight:500;background:#22c55e;color:#fff;`;
            return `<span style="${style}">\u2713 Success</span>`;
        } else if (result === 'Failure') {
            const style = `display:inline-block;padding:2px 6px;border-radius:4px;font-size:11px;font-weight:500;background:#ef4444;color:#fff;`;
            return `<span style="${style}">\u2717 Failure</span>`;
        }
        return result || '-';
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
     * Formats a decimal number to specified places.
     *
     * @param {number} num - Number to format
     * @param {number} places - Decimal places
     * @returns {string} Formatted number string
     */
    /**************************************************************/
    function formatDecimal(num, places) {
        if (num === null || num === undefined) return '-';
        return num.toFixed(places);
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
                year: 'numeric',
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
     * Generates follow-up suggestions for endpoint stats queries.
     *
     * @param {Array} results - Endpoint execution results
     * @returns {Array<string>} Array of suggested follow-up queries
     */
    /**************************************************************/
    function getFollowUpSuggestions(results) {
        const suggestions = [];

        for (const result of results) {
            const type = getResponseType(result);

            switch (type) {
                case 'endpoint-stats':
                    suggestions.push('Show me details about the slowest endpoint');
                    suggestions.push('Which endpoints are most frequently used?');
                    suggestions.push("What's the performance trend over time?");
                    break;
                case 'user-activity':
                case 'user-activity-daterange':
                    suggestions.push('Filter activity by date range');
                    suggestions.push('Show activity for another user');
                    suggestions.push('Get endpoint performance statistics');
                    break;
            }
        }

        return [...new Set(suggestions)].slice(0, 3);
    }

    /**************************************************************/
    /**
     * Public API for the endpoint stats rendering module.
     */
    /**************************************************************/
    return {
        isEndpointStatsResponse: isEndpointStatsResponse,
        getResponseType: getResponseType,
        renderEndpointStatsData: renderEndpointStatsData,
        renderPerformanceSummary: renderPerformanceSummary,
        renderUserActivity: renderUserActivity,
        getFollowUpSuggestions: getFollowUpSuggestions,
        renderActivityTypeBadge: renderActivityTypeBadge,
        renderStatusBadge: renderStatusBadge
    };
})();
