# MedRecPro Settings API Skills Document

This document describes the administrative settings and cache management endpoints available in MedRecPro.

## Table of Contents

1. [Overview](#overview)
2. [Cache Management](#cache-management)
3. [Query Decision Tree](#query-decision-tree)

---

## Overview

The Settings API provides administrative functionality for cache management and system configuration. Most endpoints require Admin role.

**Base API Path**: `/api/settings`

### Authentication Requirements

**IMPORTANT**: Cache management endpoints require authentication AND Admin role.

When a user requests cache operations:
1. **If `isAuthenticated` is `false`** in the system context: Return a direct response explaining they must sign in first
2. **If authenticated but not Admin**: The endpoints will return 403 Forbidden

**Direct Response for Unauthenticated Users**:
When the user is NOT authenticated and requests cache operations, respond with:
```
isDirectResponse: true
directResponse: "Cache management requires authentication. Please sign in using the login button in the top navigation, then try your request again. Cache operations are restricted to users with administrator privileges."
```

Do NOT suggest cache endpoints if `isAuthenticated` is `false`.

---

## Cache Management

### Managed Cache Reset

Clears managed cache entries when critical data changes require immediate consistency.

```
POST /api/settings/clearmanagedcache
```

Clears managed performance cache key-chain entries.

**Use after**: Updates like assignment ownership changes, organization changes, or other global edits.

**Trigger Phrases**: "clear cache", "reset cache", "flush cache", "invalidate cache"

### When to Clear Cache

Clearing the managed cache is recommended after:
- Bulk data imports
- Administrative configuration changes
- When experiencing stale data issues
- After direct database modifications

---

## Query Decision Tree

### CRITICAL: Check Authentication First

**Before suggesting ANY settings endpoints, check the system context:**

```
IF isAuthenticated == false THEN:
  Return direct response: "Cache management requires you to sign in first.
  Please use the Sign In button in the top navigation bar, then try your request again.
  Note: Cache operations are restricted to users with administrator privileges."

  DO NOT suggest any /api/settings endpoints.
END IF
```

### Admin asks: 'Clear cache / I changed core reference data and need consistency'

1. `POST /api/settings/clearmanagedcache` (admin-only in most deployments)

---

## Common Intent Patterns

| User Intent | Endpoint | Key Parameter |
|-------------|----------|---------------|
| Clear cache | `/api/settings/clearmanagedcache` | - |
| Reset cache | `/api/settings/clearmanagedcache` | - |
| Invalidate cache | `/api/settings/clearmanagedcache` | - |

---

## Note on Application Logs

**Application log viewing has been moved to the User Activity & Monitoring skill.**

For log-related queries (viewing logs, filtering logs, log statistics), use the `userActivity` skill which provides comprehensive monitoring capabilities including:
- Application log viewing and filtering
- User activity tracking
- Endpoint performance statistics

This consolidation makes it easier to access all monitoring and observability features in one place.
