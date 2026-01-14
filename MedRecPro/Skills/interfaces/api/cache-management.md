# Cache Management - API Interface

Maps the **Cache Management** capability to API endpoints.

**Authorization Required**: All endpoints require Admin role.

---

## Cache Operations

### Clear Managed Cache

```
POST /api/Settings/cache/clear
```

Invalidates all cached data. Use after data updates or configuration changes.

### Get Cache Status

```
GET /api/Settings/cache/status
```

Returns current cache statistics and entry counts.

---

## Example Workflow

### Clear Cache

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "POST",
      "path": "/api/Settings/cache/clear"
    }
  ]
}
```

---

## Pre-Execution Check

Before executing any endpoint in this interface:

```
IF isAuthenticated == false:
    ABORT workflow
    RESPOND: "Please sign in with an administrator account to manage cache settings."
```

---

## When to Use

- After bulk data imports
- After configuration changes
- When stale data is suspected
- During maintenance operations
