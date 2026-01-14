# Session Management - API Interface

Maps the **Session Management** capability to API endpoints.

---

## Authentication Endpoints

### Get Current User

```
GET /api/Auth/current
```

Returns current authentication status and user profile.

### OAuth Sign-In

```
GET /api/Auth/login/{provider}
```

Initiates OAuth flow. Supported providers: `google`, `microsoft`.

### Sign Out

```
POST /api/Auth/logout
```

Terminates current session.

---

## Example Workflows

### Check Authentication Status

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Auth/current"
    }
  ]
}
```

### Sign In

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Auth/login/google"
    }
  ]
}
```

---

## Response Fields

### /api/Auth/current

```json
{
  "isAuthenticated": true,
  "userId": "user-id",
  "userName": "User Name",
  "email": "user@example.com",
  "roles": ["Admin", "User"]
}
```

---

## Context Availability

The `isAuthenticated` status is available in the system context and should be checked before:

- Selecting admin-only skills (userActivity, cacheManagement)
- Executing protected endpoints
- Displaying user-specific information
