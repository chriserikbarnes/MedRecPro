# MedRecPro General API Skills Document

This document describes the general API endpoints for AI agent workflows, authentication, and user management in MedRecPro.

## Table of Contents

1. [AI Agent Workflow](#ai-agent-workflow-interpret--execute--synthesize)
2. [Authentication](#authentication)
3. [User Management](#user-management)
4. [Settings & Caching](#settings--caching)

---

## AI Agent Workflow (Interpret → Execute → Synthesize)

MedRecPro supports an agentic workflow where the server returns *endpoint specifications* for the client to execute, then the server synthesizes results into a final answer.

### Start / Manage Conversation Context
```
POST /api/ai/conversations
```

Create a new conversation session (optional).

**Notes**:
- Conversations expire after 1 hour of inactivity
- You can skip this; calling interpret without a conversationId can auto-create one

### Interpret a Natural Language Query into Endpoint Calls
```
POST /api/ai/interpret
Body: AiAgentRequest (originalQuery, optional conversationId/history/system context)
Response: AiAgentInterpretation (endpoints, reasoning hints, direct responses when applicable)
```

Return endpoint specifications to execute.

### Synthesize Executed Results Back into a Human Answer
```
POST /api/ai/synthesize
Body: AiSynthesisRequest (originalQuery, conversationId, executedEndpoints[])
Response: synthesized answer + highlights + suggested follow-ups
```

Provide executed endpoint results and get final narrative response.

### Convenience: One-shot Query
```
GET /api/ai/chat?message={text}
```

Convenience endpoint (interpret + immediate execution for simple queries).

Use when you don't need multi-step client execution. For richer conversation/history, prefer `POST /api/ai/interpret`.

### Context / Readiness Checks

#### Get System Context
```
GET /api/ai/context
```

Returns system context (e.g., documentCount) used to guide workflows.

#### Get Skills Document
```
GET /api/ai/skills
```

Returns the current skills document (this content) for AI/tooling clients.

---

## Authentication

### OAuth Login
```
GET /api/auth/login/{provider}
```

Initiate OAuth login.

| Parameter | Values |
|-----------|--------|
| `provider` | Google, Microsoft |

### Get Current User
```
GET /api/auth/user
```

Get current user info. Requires authentication.

### Logout
```
POST /api/auth/logout
```

Log out. Requires authentication.

---

## User Management

### Get My Profile
```
GET /api/users/me
```

Get current user profile. Requires authentication.

### Get User by ID
```
GET /api/users/{encryptedUserId}
```

Get user by ID.

### Update User Profile
```
PUT /api/users/{encryptedUserId}/profile
```

Update a user's own profile.

### Get User Activity (Admin)
```
GET /api/users/user/{encryptedUserId}/activity
```

Get user activity log (paged, newest first). Requires authentication.

### Get User Activity by Date Range
```
GET /api/users/user/{encryptedUserId}/activity/daterange?startDate={dt}&endDate={dt}
```

Activity within date range.

### Get Endpoint Statistics (Admin)
```
GET /api/users/endpoint-stats?startDate={dt?}&endDate={dt?}
```

Endpoint usage statistics.

---

### Local Authentication (Legacy/Alternative to OAuth)

#### Sign Up
```
POST /api/users/signup
```

Create a new user account.

#### Authenticate
```
POST /api/users/authenticate
```

Authenticate user (email/password) and return user details.

#### Rotate Password
```
POST /api/users/rotate-password
```

Rotate password (current + new password).

#### Admin Update
```
PUT /api/users/admin-update
```

(Admin) Bulk update user properties.

---

## Settings & Caching

### Managed Cache Reset

Clears managed cache entries when critical data changes require immediate consistency.

```
POST /api/settings/clearmanagedcache
```

Clears managed performance cache key-chain entries.

**Use after**: Updates like assignment ownership changes, organization changes, or other global edits.

---

## Query Decision Tree

### User asks: 'How do I do multi-step AI-assisted querying?'

1. `POST /api/ai/interpret` with a natural language query
2. Execute returned endpoint specs on client
3. `POST /api/ai/synthesize` with executed endpoint results
4. Optional: `POST /api/ai/conversations` to explicitly start a session first

### User asks: 'Clear cache / I changed core reference data and need consistency'

1. `POST /api/settings/clearmanagedcache` (admin-only in most deployments)

### Admin asks: 'Show endpoint usage / audit activity'

1. `GET /api/users/endpoint-stats?startDate={dt?}&endDate={dt?}`
2. `GET /api/users/user/{encryptedUserId}/activity?pageNumber=1&pageSize=50`
3. Date filtering: `/activity/daterange?startDate={dt}&endDate={dt}`

---

## Common Intent Patterns

| User Intent | Endpoint | Key Parameter |
|-------------|----------|---------------|
| Start AI conversation | `/api/ai/conversations` | (POST) |
| Interpret query | `/api/ai/interpret` | (POST with query) |
| Synthesize results | `/api/ai/synthesize` | (POST with results) |
| One-shot chat | `/api/ai/chat` | `message` |
| OAuth login | `/api/auth/login/{provider}` | `provider` |
| Get current user | `/api/auth/user` | (none) |
| Logout | `/api/auth/logout` | (POST) |
| Get my profile | `/api/users/me` | (none) |
| Clear cache | `/api/settings/clearmanagedcache` | (POST) |

---

## Important Notes

1. **Encrypted IDs**: All IDs returned by the API are encrypted. Use these encrypted values in subsequent requests.

2. **Authentication**: Write operations (POST, PUT, DELETE) require authentication. Read operations are generally public.

3. **Demo Mode**: Database may be periodically reset. User data is preserved.

---

## Contextual Responses

### Authentication Required

When an operation requires authentication and user is not authenticated:

> This operation requires authentication. Please log in using Google or Microsoft OAuth.

Suggested action: `GET /api/auth/login/Google`
