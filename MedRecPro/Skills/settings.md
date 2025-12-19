# MedRecPro Settings API Skills Document

This document describes the administrative settings and logging endpoints available in MedRecPro.

## Table of Contents

1. [Overview](#overview)
2. [Cache Management](#cache-management)
3. [Log Viewing (Admin Only)](#log-viewing-admin-only)
4. [Query Decision Tree](#query-decision-tree)

---

## Overview

The Settings API provides administrative functionality for cache management and log viewing. Most endpoints require Admin role.

**Base API Path**: `/api/settings`

### Authentication Requirements

**IMPORTANT**: Log viewing endpoints require authentication AND Admin role.

When a user requests log data:
1. **If `isAuthenticated` is `false`** in the system context: Return a direct response explaining they must sign in first
2. **If authenticated but not Admin**: The endpoints will return 403 Forbidden

**Direct Response for Unauthenticated Users**:
When the user is NOT authenticated and requests logs, respond with:
```
isDirectResponse: true
directResponse: "Viewing application logs requires authentication. Please sign in using the login button in the top navigation, then try your request again. Log viewing is restricted to users with administrator privileges."
```

Do NOT suggest log endpoints if `isAuthenticated` is `false`.

---

## Cache Management

### Managed Cache Reset

Clears managed cache entries when critical data changes require immediate consistency.

```
POST /api/settings/clearmanagedcache
```

Clears managed performance cache key-chain entries.

**Use after**: Updates like assignment ownership changes, organization changes, or other global edits.

---

## Log Viewing (Admin Only)

View and filter in-memory application logs. All endpoints require Admin role.

### Get Log Statistics
```
GET /api/settings/logs/statistics
```

Get overview of in-memory log storage including entry counts, retention settings, and level distribution.

**Response Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `totalEntries` | int | Total log entries in memory |
| `categoryCount` | int | Number of unique log categories |
| `oldestEntry` | datetime | Timestamp of oldest entry |
| `newestEntry` | datetime | Timestamp of newest entry |
| `entriesByLevel` | object | Counts by log level (Information, Warning, Error, etc.) |
| `uniqueUserCount` | int | Number of unique users with log entries |
| `retentionMinutes` | int | Configured retention period |
| `maxEntriesPerCategory` | int | Max entries per category |
| `maxTotalEntries` | int | Max total entries |

**Trigger Phrases**: "log statistics", "how many logs", "log summary", "logging status"

### Get Log Categories
```
GET /api/settings/logs/categories
```

Get list of all log categories with entry counts. Use to discover available categories for filtering.

**Response**: Array of category summaries with category name, entry count, and time range.

**Trigger Phrases**: "log categories", "what categories", "list log sources"

### Get Log Users
```
GET /api/settings/logs/users
```

Get list of users who have generated log entries with counts.

**Response**: Array of user summaries with userId, userName, entry count, and time range.

**Trigger Phrases**: "who has logs", "user log activity", "log users"

### Get All Logs
```
GET /api/settings/logs?pageNumber={n}&pageSize={n}&minLevel={level}
```

Get all log entries with optional pagination and level filtering.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pageNumber` | int | No | 1-based page number (default: 1) |
| `pageSize` | int | No | Entries per page (default: 100, max: 1000) |
| `minLevel` | string | No | Minimum log level: Trace, Debug, Information, Warning, Error, Critical |

**Trigger Phrases**: "show logs", "get logs", "view logs", "recent logs", "application logs"

### Get Logs By Date
```
GET /api/settings/logs/by-date?startDate={dt}&endDate={dt}&pageNumber={n}&pageSize={n}
```

Get log entries filtered by UTC date range.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `startDate` | datetime | Yes | Start of date range (UTC) |
| `endDate` | datetime | Yes | End of date range (UTC) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Entries per page |

**Trigger Phrases**: "logs between dates", "logs from yesterday", "logs in the last hour", "filter logs by date"

### Get Logs By Category
```
GET /api/settings/logs/by-category?category={name}&pageNumber={n}&pageSize={n}
```

Get log entries filtered by category (case-insensitive partial match).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | string | Yes | Category name to filter (e.g., "Controller", "ClaudeApiService") |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Entries per page |

**Example**: Filter by "Controller" matches "MedRecPro.Controllers.LabelsController"

**Trigger Phrases**: "logs from controller", "filter logs by category", "show ClaudeApiService logs", "logs by source"

### Get Logs By User
```
GET /api/settings/logs/by-user?userId={id}&pageNumber={n}&pageSize={n}
```

Get log entries filtered by user ID.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | string | Yes | User ID to filter by |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Entries per page |

**Note**: Use `GET /api/settings/logs/users` to discover users with log entries.

**Trigger Phrases**: "logs for user", "user's logs", "filter logs by user", "what did user do"

### Log Entry Fields

Each log entry contains:

| Field | Type | Description |
|-------|------|-------------|
| `message` | string | Log message content |
| `level` | string | Log level (Trace, Debug, Information, Warning, Error, Critical) |
| `timestamp` | datetime | UTC timestamp when logged |
| `category` | string | Logger category (typically class name) |
| `userId` | string | Authenticated user ID (if available) |
| `userName` | string | User display name (if available) |
| `exceptionMessage` | string | Exception message (if applicable) |
| `exceptionType` | string | Exception type name (if applicable) |

### Notes

- Logs are stored in-memory with configurable retention (default: 60 minutes)
- User context is automatically captured for authenticated requests
- Configure retention in `appsettings.json` under `LoggingSettings`:
  - `RetentionMinutes`: How long to keep logs
  - `MaxEntriesPerCategory`: Max entries per category
  - `MaxTotalEntries`: Max total entries
  - `CaptureUserContext`: Whether to capture user info

---

## Query Decision Tree

### CRITICAL: Check Authentication First

**Before suggesting ANY settings/log endpoints, check the system context:**

```
IF isAuthenticated == false THEN:
  Return direct response: "Viewing application logs requires you to sign in first.
  Please use the Sign In button in the top navigation bar, then try your request again.
  Note: Log viewing is restricted to users with administrator privileges."

  DO NOT suggest any /api/settings/logs endpoints.
END IF
```

### Admin asks: 'Clear cache / I changed core reference data and need consistency'

1. `POST /api/settings/clearmanagedcache` (admin-only in most deployments)

### Admin asks: 'Show application logs / errors / warnings'

**PREREQUISITE**: User must be authenticated (check `isAuthenticated` in system context)

1. **Get log statistics**: `GET /api/settings/logs/statistics`
2. **Get all logs**: `GET /api/settings/logs?pageNumber=1&pageSize=100`
3. **Filter by level** (errors only): `GET /api/settings/logs?minLevel=Error`
4. **Filter by date**: `GET /api/settings/logs/by-date?startDate={dt}&endDate={dt}`
5. **Filter by category**: `GET /api/settings/logs/by-category?category=ClaudeApiService`
6. **Filter by user**: `GET /api/settings/logs/by-user?userId={userId}`
7. **Discover categories**: `GET /api/settings/logs/categories`
8. **Discover users**: `GET /api/settings/logs/users`

---

## Common Intent Patterns

| User Intent | Endpoint | Key Parameter |
|-------------|----------|---------------|
| View log overview | `/api/settings/logs/statistics` | - |
| Get recent logs | `/api/settings/logs` | `pageNumber`, `pageSize` |
| Filter by severity | `/api/settings/logs` | `minLevel` |
| Filter by time range | `/api/settings/logs/by-date` | `startDate`, `endDate` |
| Filter by source | `/api/settings/logs/by-category` | `category` |
| Filter by user | `/api/settings/logs/by-user` | `userId` |
| List categories | `/api/settings/logs/categories` | - |
| List users with logs | `/api/settings/logs/users` | - |
| Clear cache | `/api/settings/clearmanagedcache` | - |

---

## Recommended Workflows

### Starting with Log Analysis

When a user asks to "see logs" or "check application logs" without specific filters:

1. **Start with statistics**: Call `/api/settings/logs/statistics` first to understand the current log state
2. **This provides**: Total count, level distribution, time range, and category count
3. **Then offer**: Follow-up options based on what they see (filter by errors, specific category, etc.)

### Troubleshooting Errors

When user reports an issue or wants to see errors:

1. Call `/api/settings/logs?minLevel=Error&pageSize=50`
2. If many errors exist, suggest filtering by category or date
3. Show exception details in the response if present

### User Activity Tracking

When admin wants to see what a specific user did:

1. First call `/api/settings/logs/users` to get the encrypted userId
2. Then call `/api/settings/logs/by-user?userId={encryptedUserId}`

### Category-Based Filtering

When user wants logs from a specific component:

1. If unsure of category names, call `/api/settings/logs/categories` first
2. Then filter: `/api/settings/logs/by-category?category={categoryName}`
3. Partial matches work: "Controller" matches all controller categories

---

## Response Formatting Notes

The chat interface includes a specialized settings renderer that formats log data:

- **Statistics**: Displayed as formatted tables with level distribution
- **Log entries**: Table format with level indicators (emoji), timestamp, category, message
- **Level colors**:
  - 🔴 Critical
  - ❌ Error
  - ⚠️ Warning
  - ℹ️ Information
  - 🔍 Debug
  - ⚪ Trace
- **Pagination**: Shows page X of Y with hints to get more results
- **Filters**: Active filters are displayed at the top of results
