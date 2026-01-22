# User Activity - API Interface

Maps the **User Activity Monitoring** and **Endpoint Performance Analysis** capabilities to API endpoints.

**Authorization Required**: All endpoints require Admin role.

---

## Application Log Viewing (Admin Only)

View and filter in-memory application logs.

### Get Log Statistics

```
GET /api/Settings/logs/statistics
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
GET /api/Settings/logs/categories
```

Get list of all log categories with entry counts. Use to discover available categories for filtering.

**Response**: Array of category summaries with category name, entry count, and time range.

**Trigger Phrases**: "log categories", "what categories", "list log sources"

### Get Log Users

```
GET /api/Settings/logs/users
```

Get list of users who have generated log entries with counts.

**Response**: Array of user summaries with userId, userName, entry count, and time range.

**Trigger Phrases**: "who has logs", "user log activity", "log users"

### Get All Logs

```
GET /api/Settings/logs?pageNumber={n}&pageSize={n}&minLevel={level}
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
GET /api/Settings/logs/by-date?startDate={dt}&endDate={dt}&pageNumber={n}&pageSize={n}
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
GET /api/Settings/logs/by-category?category={name}&pageNumber={n}&pageSize={n}
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
GET /api/Settings/logs/by-user?userId={id}&pageNumber={n}&pageSize={n}
```

Get log entries filtered by user ID.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | string | Yes | User ID to filter by |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Entries per page |

**Note**: Use `GET /api/Settings/logs/users` to discover users with log entries.

**Trigger Phrases**: "logs for user", "user's logs", "filter logs by user"

### Log Entry Response Fields

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

### Log Level Reference

- **Critical**: System-critical failures
- **Error**: Errors that affect functionality
- **Warning**: Potential issues or deprecations
- **Information**: Normal operational events
- **Debug**: Detailed diagnostic information
- **Trace**: Very detailed tracing information

---

## User Lookup Endpoints

**CRITICAL**: Before calling ANY user activity endpoints, you MUST first obtain the user's encrypted ID through one of the lookup methods below.

### Get User by ID (Validation)

```
GET /api/Users/{encryptedUserId}
```

Retrieves a specific user by their encrypted ID. Use to validate an encryptedUserId before calling activity endpoints.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `encryptedUserId` | string | Yes (path) | The encrypted identifier of the user |

### Get Users List

```
GET /api/Users?includeDeleted={bool}&skip={n}&take={n}
```

Retrieves a paginated list of users to find the encrypted user ID.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `includeDeleted` | bool | No | Whether to include soft-deleted users. Defaults to false. |
| `skip` | int | No | Number of records to skip for pagination. Defaults to 0. |
| `take` | int | No | Number of records to take for pagination. Defaults to 100, max 1000. |

**Response Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `encryptedUserId` | string | **Use this value** for activity endpoints |
| `displayName` | string | User's display name |
| `primaryEmail` | string | User's email address |
| `canonicalUsername` | string | Normalized username |
| `userRole` | string | User role (Admin, User, etc.) |
| `lastLoginAt` | datetime | Last login timestamp |
| `lastActivityAt` | datetime | Last activity timestamp |

### Get User by Email

```
GET /api/Users/byemail?email={email}
```

Retrieves a single user by their email address. **This is the preferred method when the user's email is known.**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `email` | string | Yes | The email address of the user to retrieve |

**Response**: Returns the full user object including `encryptedUserId` which you MUST capture for subsequent activity requests.

**Trigger Phrases**: "find user", "lookup user", "user by email", "get user info"

---

## User Activity Endpoints (Admin Only)

View activity logs for specific users.

### Get User Activity

```
GET /api/Users/user/{encryptedUserId}/activity?pageNumber={n}&pageSize={n}
```

Retrieves activity log entries for a specific user with optional paging.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `encryptedUserId` | string | Yes (path) | The encrypted identifier of the user |
| `pageNumber` | int | No | Page number to retrieve. Defaults to 1. |
| `pageSize` | int | No | Entries per page. Defaults to 50, max 100. |

**Response Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `encryptedId` | string | Encrypted activity log entry ID |
| `encryptedUserId` | string | Encrypted user ID |
| `email` | string | User's email |
| `displayName` | string | User's display name |
| `activityType` | string | Type: Read, Create, Update, Delete, Login |
| `activityTimestamp` | datetime | When the activity occurred |
| `description` | string | Activity description (e.g., "GET Settings/GetLogs") |
| `ipAddress` | string | Client IP address |
| `userAgent` | string | Browser/client user agent |
| `requestPath` | string | API path called |
| `httpMethod` | string | HTTP method (GET, POST, PUT, DELETE) |
| `controllerName` | string | Controller that handled the request |
| `actionName` | string | Action method name |
| `responseStatusCode` | int | HTTP response status code |
| `executionTimeMs` | int | Execution time in milliseconds |
| `result` | string | Result status (Success, Failure) |

**Trigger Phrases**: "user activity", "what did [user] do", "activity for [user]", "user's actions"

### Get User Activity by Date Range

```
GET /api/Users/user/{encryptedUserId}/activity/daterange?startDate={dt}&endDate={dt}&pageNumber={n}&pageSize={n}
```

Retrieves activity log entries filtered by date range.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `encryptedUserId` | string | Yes (path) | The encrypted identifier of the user |
| `startDate` | datetime | Yes | Start of date range (inclusive) |
| `endDate` | datetime | Yes | End of date range (inclusive) |
| `pageNumber` | int | No | Page number to retrieve. Defaults to 1. |
| `pageSize` | int | No | Entries per page. Defaults to 50, max 100. |

**Constraints**:
- Date range cannot exceed 365 days
- Start date must be before or equal to end date

**Trigger Phrases**: "activity between dates", "activity from [date] to [date]", "user actions in [time period]"

---

## Endpoint Performance Statistics (Admin Only)

### Get Endpoint Statistics

```
GET /api/Users/endpoint-stats?controllerName={name}&actionName={name}&limit={n}
```

Retrieves endpoint statistics for a specific controller and action, including average execution time.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `controllerName` | string | Yes | The controller name (e.g., "Settings", "Label", "Users") |
| `actionName` | string | No | The action method name. If omitted, stats for all actions in controller. |
| `limit` | int | No | Max activity entries to analyze. Defaults to 100, max 1000. |

**IMPORTANT**: Use the **method name** (e.g., "GetLogs", "SearchByIngredient"), NOT the URL path.

**Response Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `controllerName` | string | Controller name |
| `actionName` | string | Action method name |
| `totalActivities` | int | Number of activities analyzed |
| `averageExecutionTimeMs` | double | Average execution time in milliseconds |
| `minExecutionTimeMs` | int | Minimum execution time |
| `maxExecutionTimeMs` | int | Maximum execution time |
| `activitiesWithExecutionTime` | int | Count with recorded execution time |
| `analyzedLimit` | int | Limit used for analysis |
| `dateRangeAnalyzed` | object | Contains `from` and `to` timestamps |

**Trigger Phrases**: "endpoint performance", "how fast is [endpoint]", "response time for", "controller stats", "API performance"

---

## Available Controllers and Actions

Use these exact names when querying endpoint statistics:

### Settings Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `GetDemoModeStatus` | /api/Settings/demomode | Get demo mode status |
| `GetApplicationInfo` | /api/Settings/info | Get application info |
| `GetFeatures` | /api/Settings/features | Get feature flags |
| `GetDatabaseLimits` | /api/Settings/database-limits | Get database limits |
| `GetDatabaseMetrics` | /api/Settings/metrics/database-cost | Get database costs |
| `ClearManagedCache` | /api/Settings/clearmanagedcache | Clear cache |
| `GetLogStatistics` | /api/Settings/logs/statistics | Get log statistics |
| `GetLogCategories` | /api/Settings/logs/categories | Get log categories |
| `GetLogUsers` | /api/Settings/logs/users | Get log users |
| `GetLogs` | /api/Settings/logs | Get all logs |
| `GetLogsByDate` | /api/Settings/logs/by-date | Get logs by date |
| `GetLogsByCategory` | /api/Settings/logs/by-category | Get logs by category |
| `GetLogsByUser` | /api/Settings/logs/by-user | Get logs by user |

### Users Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `GetAllUsers` | /api/Users | Get user list |
| `GetUser` | /api/Users/{id} | Get user by ID |
| `GetUserByEmail` | /api/Users/byemail | Get user by email |
| `GetMe` | /api/Users/me | Get current user |
| `GetUserActivity` | /api/Users/user/{id}/activity | Get user activity |
| `GetUserActivityByDateRange` | /api/Users/user/{id}/activity/daterange | Get activity by date |
| `GetEndpointStats` | /api/Users/endpoint-stats | Get endpoint stats |

### Label Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `SearchByApplicationNumber` | /api/Label/application-number/search | Search by app number |
| `SearchByPharmacologicClass` | /api/Label/pharmacologic-class/search | Search by drug class |
| `SearchByIngredient` | /api/Label/ingredient/search | Search by ingredient |
| `SearchByNDC` | /api/Label/ndc/search | Search by NDC |
| `SearchByLabeler` | /api/Label/labeler/search | Search by labeler |
| `GetSectionContent` | /api/Label/section/content/{guid} | Section content |
| `SearchProductSummary` | /api/Label/product/search | Search products |
| `GetRelatedProducts` | /api/Label/product/related | Related products |
| `GetSingleCompleteLabel` | /api/Label/single/{guid} | Single document |

### Ai Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `Interpret` | /api/Ai/interpret | Interpret query |
| `Synthesize` | /api/Ai/synthesize | Synthesize results |
| `Chat` | /api/Ai/chat | One-shot chat |

---

## Common Intent Patterns

| User Intent | Endpoint(s) | Key Parameters |
|-------------|-------------|----------------|
| View application logs | `/api/Settings/logs` | `pageNumber`, `pageSize`, `minLevel` |
| Filter logs by error level | `/api/Settings/logs` | `minLevel=Error` |
| Filter logs by category | `/api/Settings/logs/by-category` | `category` |
| Filter logs by date | `/api/Settings/logs/by-date` | `startDate`, `endDate` |
| Get log statistics | `/api/Settings/logs/statistics` | none |
| Find user by email | `/api/Users/byemail` | `email` |
| List all users | `/api/Users` | `skip`, `take` |
| View user activity | `/api/Users/user/{id}/activity` | `encryptedUserId`, `pageNumber`, `pageSize` |
| Filter activity by date | `/api/Users/user/{id}/activity/daterange` | `startDate`, `endDate` |
| Endpoint performance | `/api/Users/endpoint-stats` | `controllerName`, `actionName`, `limit` |

---

## Example Workflows

### View Application Logs (Default)

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Settings/logs",
      "queryParameters": { "pageNumber": 1, "pageSize": 100 },
      "description": "Fetch application logs with default settings"
    }
  ],
  "explanation": "Retrieving application logs."
}
```

### Error Log Review

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Settings/logs",
      "queryParameters": { "minLevel": "Error", "pageNumber": 1, "pageSize": 50 },
      "description": "Fetch error-level logs"
    }
  ],
  "explanation": "Retrieving error-level logs."
}
```

### Logs by Category

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Settings/logs/by-category",
      "queryParameters": { "category": "ClaudeApiService", "pageNumber": 1, "pageSize": 100 },
      "description": "Fetch logs filtered by category"
    }
  ],
  "explanation": "Retrieving logs filtered by category."
}
```

### User Activity Audit (Multi-Step)

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "path": "/api/Users/byemail",
      "method": "GET",
      "queryParameters": { "email": "{userEmail}" },
      "description": "Find user by email to get encrypted ID",
      "outputMapping": { "encryptedUserId": "encryptedUserId", "displayName": "displayName" }
    },
    {
      "step": 2,
      "path": "/api/Users/user/{{encryptedUserId}}/activity",
      "method": "GET",
      "queryParameters": { "pageNumber": 1, "pageSize": 50 },
      "dependsOn": 1,
      "description": "Get activity logs for {{displayName}}"
    }
  ],
  "explanation": "Looking up user by email and retrieving their activity logs."
}
```

### Controller Performance Analysis

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Users/endpoint-stats",
      "queryParameters": { "controllerName": "Settings", "actionName": "GetLogs", "limit": 100 },
      "description": "Get performance stats for Settings/GetLogs"
    }
  ],
  "explanation": "Retrieving endpoint performance statistics."
}
```

### Log Statistics Overview

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Settings/logs/statistics",
      "description": "Get log storage statistics and level distribution"
    }
  ],
  "explanation": "Retrieving log statistics overview."
}
```

---

## Pre-Execution Check

Before executing any endpoint in this interface:

```
IF isAuthenticated == false:
    ABORT workflow
    SET isDirectResponse = true
    SET directResponse = "Viewing application logs, user activity, and endpoint statistics requires authentication. Please sign in using the login button in the top navigation, then try your request again. These features are restricted to users with administrator privileges."
```

Do NOT suggest monitoring endpoints if `isAuthenticated` is `false`.
