# User Activity - API Interface

Maps the **User Activity Monitoring** and **Endpoint Performance Analysis** capabilities to API endpoints.

**Authorization Required**: All endpoints require Admin role.

---

## Log Viewing

### Get Application Logs

```
GET /api/Logs?level={level}&category={category}&startDate={date}&endDate={date}&pageNumber=1&pageSize=50
```

**Parameters**:
- `level` - Filter by severity (Error, Warning, Info, Debug, Trace)
- `category` - Filter by log category
- `startDate`, `endDate` - Date range filter
- `userId` - Filter by specific user

### Get Log Statistics

```
GET /api/Logs/statistics
```

Returns entry counts, level distribution, category summaries.

---

## User Activity

### Get User Actions

```
GET /api/Logs/user/{userId}?startDate={date}&endDate={date}
```

Returns actions performed by a specific user.

---

## Endpoint Performance

### Get Controller Statistics

```
GET /api/Performance/controllers
```

Returns response time statistics grouped by controller.

### Get Endpoint Details

```
GET /api/Performance/endpoints?controller={controllerName}
```

Returns detailed metrics for specific endpoints.

---

## Example Workflows

### Error Log Review

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Logs",
      "queryParameters": { "level": "Error", "pageSize": 50 }
    }
  ]
}
```

### User Activity Audit

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Logs/user/{userId}",
      "queryParameters": { "startDate": "{date}", "endDate": "{date}" }
    }
  ]
}
```

### Performance Analysis

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Performance/controllers"
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Performance/endpoints",
      "queryParameters": { "controller": "Label" }
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
    RESPOND: "Please sign in with an administrator account to access logs and performance data."
```
