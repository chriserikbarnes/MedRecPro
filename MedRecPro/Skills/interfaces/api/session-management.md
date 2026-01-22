# Session Management - API Interface

Maps the **Session Management** capability to API endpoints.

---

## Authentication Endpoints

### Get Current User

```
GET /api/Auth/user
```

Returns current authentication status and user profile. Requires user to be authenticated.

**Response Fields** (when authenticated):
| Field | Type | Description |
|-------|------|-------------|
| `id` | string | User's unique identifier |
| `name` | string | User's display name |
| `claims` | array | Array of user claims (type/value pairs) |

**Response** (when not authenticated):
Returns 401 Unauthorized.

**Trigger Phrases**: "am I logged in", "authentication status", "who am I", "current user", "my account"

### OAuth Sign-In

```
GET /api/Auth/login/{provider}
```

Initiates OAuth authentication flow. Redirects user to external provider.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `provider` | string | Yes (path) | OAuth provider: `google` or `microsoft` |

**Trigger Phrases**: "sign in", "log in", "authenticate"

### Sign Out

```
POST /api/Auth/logout
```

Terminates current session and clears authentication cookies.

**Trigger Phrases**: "sign out", "log out", "end session"

---

## Example Workflows

### Check Authentication Status

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Auth/user",
      "description": "Get current authenticated user information"
    }
  ],
  "explanation": "Checking authentication status by querying the current user endpoint."
}
```

### Sign In with Google

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Auth/login/google",
      "description": "Initiate Google OAuth sign-in flow"
    }
  ],
  "explanation": "Initiating Google OAuth sign-in flow."
}
```

### Sign In with Microsoft

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Auth/login/microsoft",
      "description": "Initiate Microsoft OAuth sign-in flow"
    }
  ],
  "explanation": "Initiating Microsoft OAuth sign-in flow."
}
```

### Sign Out

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "POST",
      "path": "/api/Auth/logout",
      "description": "Sign out current user"
    }
  ],
  "explanation": "Signing out the current user."
}
```

---

## Context Availability

The `isAuthenticated` status is available in the system context and should be checked before:

- Selecting admin-only skills (userActivity, cacheManagement)
- Executing protected endpoints
- Displaying user-specific information

When `isAuthenticated` is `false`, respond with guidance to sign in rather than suggesting protected endpoints.
