# Logging conventions

MedRecPro logging uses structured templates. Keep the message template stable and pass values as separate arguments; do not use string interpolation or concatenation in `ILogger` calls.

Use these property names whenever the value is available:

- `TraceId` for the request/activity correlation identifier.
- `UserId` for an authenticated user identifier (only when the destination is authorized and encrypted as needed).
- `DocumentGuid` for a label document identifier.
- `OperationId` for queued or background work.
- `RequestMethod` and `RequestPath` for the HTTP operation.
- `CacheKey`, `ResultCount`, `PageNumber`, and `PageSize` for cache and pagination diagnostics.

Never log request bodies, credentials, connection strings, decrypted identifiers, access tokens, raw AI response bodies, or exception text intended for a client response. The in-memory administrative log retains a redacted exception type and generic summary; the global exception handler owns the detailed server-side record.
