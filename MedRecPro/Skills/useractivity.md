# MedRecPro User Activity & Monitoring API Skills Document

This document describes the user activity monitoring, application log viewing, and endpoint performance statistics endpoints available in MedRecPro.

## Table of Contents

1. [Overview](#overview)
2. [Application Log Viewing (Admin Only)](#application-log-viewing-admin-only)
3. [User Lookup (Required First Step)](#user-lookup-required-first-step)
4. [User Activity Endpoints (Admin Only)](#user-activity-endpoints-admin-only)
5. [Endpoint Performance Statistics (Admin Only)](#endpoint-performance-statistics-admin-only)
6. [Available Controllers and Actions](#available-controllers-and-actions)
7. [Query Decision Tree](#query-decision-tree)
8. [Workflows](#workflows)

---

## Overview

The User Activity & Monitoring API provides administrative functionality for:
- **Application Log Viewing**: In-memory application logs with filtering by level, category, date, and user
- **User Activity Tracking**: Activity logs for specific users showing their actions in the system
- **Endpoint Performance Statistics**: Response time analysis for API endpoints

All endpoints require Admin role.

**Base API Paths**:
- Logs: `/api/settings/logs`
- User Activity: `/api/Users`

### Authentication Requirements

**IMPORTANT**: All monitoring endpoints require authentication AND Admin role.

When a user requests monitoring data:
1. **If `isAuthenticated` is `false`** in the system context: Return a direct response explaining they must sign in first
2. **If authenticated but not Admin**: The endpoints will return 403 Forbidden

**Direct Response for Unauthenticated Users**:
When the user is NOT authenticated and requests monitoring data, respond with:
```
isDirectResponse: true
directResponse: "Viewing application logs, user activity, and endpoint statistics requires authentication. Please sign in using the login button in the top navigation, then try your request again. These features are restricted to users with administrator privileges."
```

Do NOT suggest monitoring endpoints if `isAuthenticated` is `false`.

---

## Application Log Viewing (Admin Only)

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

**Trigger Phrases**: "logs for user", "user's logs", "filter logs by user"

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

### Log Level Reference

- **Critical**: System-critical failures
- **Error**: Errors that affect functionality
- **Warning**: Potential issues or deprecations
- **Information**: Normal operational events
- **Debug**: Detailed diagnostic information
- **Trace**: Very detailed tracing information

### Notes

- Logs are stored in-memory with configurable retention (default: 60 minutes)
- User context is automatically captured for authenticated requests
- Configure retention in `appsettings.json` under `LoggingSettings`

---

## User Lookup (Required First Step)

**CRITICAL**: Before calling ANY user activity endpoints, you MUST first obtain the user's encrypted ID through one of the lookup methods below. The `encryptedUserId` is required for all activity endpoints.

### IMPORTANT: Encrypted User ID Persistence

**For pagination and follow-up requests**: When the user asks for "next page", "page 2", or continues a previous activity query:
1. You MUST use the **same encryptedUserId** from the previous request
2. Look in the conversation history for the previously used encryptedUserId
3. If the encryptedUserId is not in context, you MUST re-fetch it using the user lookup endpoints below
4. **NEVER guess or construct an encryptedUserId** - always retrieve it from the API

### Get User by ID (Validation)
```
GET /api/Users/{encryptedUserId}
```

Retrieves a specific user by their encrypted ID. **Use this to validate an encryptedUserId before calling activity endpoints.**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `encryptedUserId` | string | Yes (path) | The encrypted identifier of the user |

**Example**: `GET /api/Users/F-2Q_FrS1xViLg4t4zo4PbnrJmc7H48DAxWC0o1Q3j1hPK39jrmI1XvmaLMuT7HEXg`

**Use Case**: Validate that an encryptedUserId is still valid before making activity requests.

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

**Example**: `GET /api/Users?skip=0&take=50`

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

**Example**: `GET /api/Users/byemail?email=chris.erik.barnes@gmail.com`

**Response**: Returns the full user object including `encryptedUserId` which you MUST capture for subsequent activity requests.

**Trigger Phrases**: "find user", "lookup user", "user by email", "get user info"

---

## User Activity Endpoints (Admin Only)

View activity logs for specific users. All endpoints require Admin role.

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

**Example**: `GET /api/Users/user/F-2Q_FrS1xViLg4t4zo4Pbnr.../activity?pageNumber=1&pageSize=50`

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

**Example**: `GET /api/Users/user/{encryptedUserId}/activity/daterange?startDate=2025-12-01&endDate=2025-12-22`

**Trigger Phrases**: "activity between dates", "activity from [date] to [date]", "user actions in [time period]"

---

## Endpoint Performance Statistics (Admin Only)

View performance statistics for API endpoints.

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

**Example**: `GET /api/Users/endpoint-stats?controllerName=Settings&actionName=GetLogs&limit=100`

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
| `GetDemoModeStatus` | /api/settings/demomode | Get demo mode status |
| `GetApplicationInfo` | /api/settings/info | Get application info |
| `GetFeatures` | /api/settings/features | Get feature flags |
| `GetDatabaseLimits` | /api/settings/database-limits | Get database limits |
| `GetDatabaseMetrics` | /api/settings/metrics/database-cost | Get database costs |
| `ClearManagedCache` | /api/settings/clearmanagedcache | Clear cache |
| `GetLogStatistics` | /api/settings/logs/statistics | Get log statistics |
| `GetLogCategories` | /api/settings/logs/categories | Get log categories |
| `GetLogUsers` | /api/settings/logs/users | Get log users |
| `GetLogs` | /api/settings/logs | Get all logs |
| `GetLogsByDate` | /api/settings/logs/by-date | Get logs by date |
| `GetLogsByCategory` | /api/settings/logs/by-category | Get logs by category |
| `GetLogsByUser` | /api/settings/logs/by-user | Get logs by user |

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
| `SignUpUser` | /api/Users/signup | Sign up new user |
| `AuthenticateUser` | /api/Users/authenticate | Authenticate user |
| `UpdateUserProfile` | /api/Users/{id}/profile | Update profile |
| `DeleteUser` | /api/Users/{id} | Delete user |
| `AdminUpdateUser` | /api/Users/admin-update | Admin update user |
| `RotatePassword` | /api/Users/rotate-password | Rotate password |

### Label Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `SearchByApplicationNumber` | /api/Label/application-number/search | Search by app number |
| `GetApplicationNumberSummaries` | /api/Label/application-number/summaries | App number summaries |
| `SearchByPharmacologicClass` | /api/Label/pharmacologic-class/search | Search by drug class |
| `GetPharmacologicClassHierarchy` | /api/Label/pharmacologic-class/hierarchy | Class hierarchy |
| `GetPharmacologicClassSummaries` | /api/Label/pharmacologic-class/summaries | Class summaries |
| `SearchByIngredient` | /api/Label/ingredient/search | Search by ingredient |
| `GetIngredientSummaries` | /api/Label/ingredient/summaries | Ingredient summaries |
| `GetIngredientActiveSummaries` | /api/Label/ingredient/active/summaries | Active ingredients |
| `GetIngredientInactiveSummaries` | /api/Label/ingredient/inactive/summaries | Inactive ingredients |
| `SearchByNDC` | /api/Label/ndc/search | Search by NDC |
| `SearchByPackageNDC` | /api/Label/ndc/package/search | Search package NDC |
| `SearchByLabeler` | /api/Label/labeler/search | Search by labeler |
| `GetLabelerSummaries` | /api/Label/labeler/summaries | Labeler summaries |
| `GetDocumentNavigation` | /api/Label/document/navigation | Document navigation |
| `GetDocumentVersionHistory` | /api/Label/document/version-history/{guid} | Version history |
| `SearchBySectionCode` | /api/Label/section/search | Search sections |
| `GetSectionTypeSummaries` | /api/Label/section/summaries | Section summaries |
| `GetSectionContent` | /api/Label/section/content/{guid} | Section content |
| `GetDrugInteractions` | /api/Label/drug-safety/interactions | Drug interactions |
| `GetDEAScheduleProducts` | /api/Label/drug-safety/dea-schedule | DEA schedule |
| `SearchProductSummary` | /api/Label/product/search | Search products |
| `GetRelatedProducts` | /api/Label/product/related | Related products |
| `GetAPIEndpointGuide` | /api/Label/guide | API guide |
| `GetLabelSectionMenu` | /api/Label/sectionMenu | Section menu |
| `GetSectionDocumentation` | /api/Label/{menu}/documentation | Section docs |
| `GetSection` | /api/Label/section/{menu} | Section records |
| `GetSingleCompleteLabel` | /api/Label/single/{guid} | Single document |
| `GetCompleteLabels` | /api/Label/complete/{page}/{size} | All documents |
| `GetDocumentComparisonAnalysis` | /api/Label/comparison/analysis/{guid} | Comparison analysis |
| `GetByIdAsync` | /api/Label/{menu}/{id} | Get record by ID |
| `CreateAsync` | /api/Label/{menu} | Create record |
| `UploadSplZips` | /api/Label/import | Import SPL |
| `GetImportProgress` | /api/Label/import/progress/{id} | Import progress |
| `GetComparisonProgress` | /api/Label/comparison/progress/{id} | Comparison progress |
| `QueueDocumentComparisonAnalysis` | /api/Label/comparison/analysis/{guid} (POST) | Queue comparison |
| `GenerateXmlDocument` | /api/Label/generate/{guid}/{minify} | Generate XML |
| `UpdateAsync` | /api/Label/{menu}/{id} | Update record |
| `DeleteAsync` | /api/Label/{menu}/{id} | Delete record |

### Ai Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `GetContext` | /api/Ai/context | Get AI context |
| `Interpret` | /api/Ai/interpret | Interpret query |
| `Synthesize` | /api/Ai/synthesize | Synthesize results |
| `GetSkills` | /api/Ai/skills | Get skills document |
| `Chat` | /api/Ai/chat | One-shot chat |
| `CreateConversation` | /api/Ai/conversations | Create conversation |
| `GetConversation` | /api/Ai/conversations/{id} | Get conversation |
| `GetConversationHistory` | /api/Ai/conversations/{id}/history | Get history |
| `DeleteConversation` | /api/Ai/conversations/{id} | Delete conversation |
| `GetConversationStats` | /api/Ai/conversations/stats | Conversation stats |
| `RetryInterpretation` | /api/Ai/retry | Retry interpretation |

### Auth Controller
| Action Name | URL Path | Description |
|-------------|----------|-------------|
| `LoginExternalProvider` | /api/auth/login/{provider} | External login |
| `ExternalLoginCallback` | /api/Auth/external-logincallback | Login callback |
| `LoginFailure` | /api/Auth/loginfailure | Login failure |
| `Lockout` | /api/Auth/lockout | Account lockout |
| `GetUser` | /api/Auth/user | Get auth user |
| `Logout` | /api/Auth/logout | Logout |
| `HandleLoginRedirect` | /api/Auth/login | Login redirect |
| `HandleAccessDenied` | /api/Auth/accessdenied | Access denied |

---

## Query Decision Tree

### CRITICAL: Check Authentication First

**Before suggesting ANY user activity endpoints, check the system context:**

```
IF isAuthenticated == false THEN:
  Return direct response: "Viewing user activity and endpoint statistics requires you to sign in first.
  Please use the Sign In button in the top navigation bar, then try your request again.
  Note: These features are restricted to users with administrator privileges."

  DO NOT suggest any /api/Users/user/ or /api/Users/endpoint-stats endpoints.
END IF
```

### User asks: 'What is the activity for [user name/email]?'

**PREREQUISITE**: User must be authenticated (check `isAuthenticated` in system context)

**Workflow (Multi-Step)**:
1. **Find the user first**:
   - If email provided: `GET /api/Users/byemail?email={email}`
   - Otherwise: `GET /api/Users?skip=0&take=100` and search results for matching name
2. **Capture the encryptedUserId** from the response
3. **Get activity**: `GET /api/Users/user/{encryptedUserId}/activity?pageNumber=1&pageSize=50`

### User asks: 'What is the performance for [controller]?'

**PREREQUISITE**: User must be authenticated (check `isAuthenticated` in system context)

**Workflow (Iterative)**:
1. Get all actions for the controller from the [Available Controllers and Actions](#available-controllers-and-actions) table
2. **Iteratively call** `GET /api/Users/endpoint-stats?controllerName={controller}&actionName={action}` for each action
3. Summarize results in a performance table

### User asks: 'How is the Settings controller performing?'

1. Call endpoint-stats for each Settings action:
   - `GET /api/Users/endpoint-stats?controllerName=Settings&actionName=GetLogStatistics`
   - `GET /api/Users/endpoint-stats?controllerName=Settings&actionName=GetLogCategories`
   - `GET /api/Users/endpoint-stats?controllerName=Settings&actionName=GetLogs`
   - (continue for all Settings actions)
2. Summarize in a table

---

## Workflows

### Workflow: User Activity Analysis

| Property | Value |
|----------|-------|
| **Intent** | View activity logs for a specific user |
| **Triggers** | "activity for [user]", "what did [user] do", "user activity", "[user]'s actions" |

#### Endpoint Specification (Multi-Step)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Users/byemail",
      "method": "GET",
      "queryParameters": {
        "email": "{userEmail}"
      },
      "description": "Find user by email to get encrypted ID",
      "outputMapping": {
        "encryptedUserId": "encryptedUserId",
        "displayName": "displayName"
      }
    },
    {
      "step": 2,
      "path": "/api/Users/user/{{encryptedUserId}}/activity",
      "method": "GET",
      "queryParameters": {
        "pageNumber": 1,
        "pageSize": 50
      },
      "dependsOn": 1,
      "description": "Get activity logs for {{displayName}}"
    }
  ]
}
```

#### Synthesis Instructions

When synthesizing user activity results:

1. **From Step 1**: Confirm user found, note display name and email
2. **From Step 2**: Group activities by type (Read, Create, Update, Delete, Login)
3. **Format**: Present as activity summary table

#### Example Response Format

```
## Activity Summary for {displayName} ({email})

| Activity Type | Count |
|--------------|-------|
| Read | 45 |
| Create | 12 |
| Login | 5 |

### Recent Activity

| Timestamp | Type | Description | Status | Time (ms) |
|-----------|------|-------------|--------|-----------|
| 2025-12-22 17:44:22 | Read | GET Settings/GetLogStatistics | Success | 7 |
| 2025-12-22 17:44:09 | Read | GET Settings/GetLogCategories | Success | 3 |
| ... | ... | ... | ... | ... |

Would you like to filter by date range or see more details?
```

---

### Workflow: Controller Performance Analysis

| Property | Value |
|----------|-------|
| **Intent** | View performance statistics for a controller |
| **Triggers** | "performance for [controller]", "how fast is [controller]", "[controller] stats" |

#### Endpoint Specification (Iterative)

For the Settings controller:
```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Users/endpoint-stats",
      "method": "GET",
      "queryParameters": {
        "controllerName": "Settings",
        "actionName": "GetLogStatistics",
        "limit": 100
      },
      "description": "Get stats for GetLogStatistics"
    },
    {
      "step": 2,
      "path": "/api/Users/endpoint-stats",
      "method": "GET",
      "queryParameters": {
        "controllerName": "Settings",
        "actionName": "GetLogCategories",
        "limit": 100
      },
      "description": "Get stats for GetLogCategories"
    }
  ]
}
```

#### Synthesis Instructions

When synthesizing endpoint statistics:

1. **Collect all responses**: Aggregate stats from each endpoint-stats call
2. **Calculate totals**: Sum activities, compute overall averages
3. **Format**: Present as performance summary table sorted by average execution time

#### Example Response Format

```
## Performance Summary for Settings Controller

| Action | Calls | Avg (ms) | Min (ms) | Max (ms) |
|--------|-------|----------|----------|----------|
| GetLogs | 15 | 2.5 | 1 | 8 |
| GetLogStatistics | 12 | 8.2 | 0 | 22 |
| GetLogCategories | 8 | 3.1 | 2 | 5 |
| GetLogUsers | 5 | 11.4 | 6 | 18 |

**Summary**:
- Total API Calls Analyzed: 40
- Overall Average Response Time: 5.8ms
- Fastest Endpoint: GetLogs (2.5ms avg)
- Slowest Endpoint: GetLogUsers (11.4ms avg)

Would you like to see details for a specific endpoint?
```

---

## Common Intent Patterns

| User Intent | Endpoint(s) | Key Parameters |
|-------------|-------------|----------------|
| Find user by email | `/api/Users/byemail` | `email` |
| List all users | `/api/Users` | `skip`, `take` |
| View user activity | `/api/Users/user/{id}/activity` | `encryptedUserId`, `pageNumber`, `pageSize` |
| Filter activity by date | `/api/Users/user/{id}/activity/daterange` | `startDate`, `endDate` |
| Endpoint performance | `/api/Users/endpoint-stats` | `controllerName`, `actionName`, `limit` |
| Controller performance | `/api/Users/endpoint-stats` (multiple calls) | `controllerName` |

---

## Response Formatting Notes

The chat interface should format activity and statistics data:

- **Activity entries**: Table format with timestamp, type, description, status, execution time
- **Performance stats**: Table format with action name, call count, avg/min/max times
- **Activity types**:
  - Read - Data retrieval operations
  - Create - POST operations creating new data
  - Update - PUT operations modifying data
  - Delete - DELETE operations removing data
  - Login - Authentication events
- **Status indicators**: Success/Failure with appropriate formatting
