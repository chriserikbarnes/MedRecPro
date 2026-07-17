/**************************************************************/
/**
 * MedRecPro API Test Manifest Module
 *
 * @fileoverview Owns the audited API-operation inventory used by the browser integration diagnostic; it has no UI or transport responsibilities.
 *
 * @module site-tests/api-manifest
 */
/**************************************************************/
window.MedRecProApiTestManifest = (function () {
    'use strict';

    var auditMetadata = {
        auditedAt: '2026-07-17T14:16:36-04:00',
        sourceRevision: '32eb21213d026d6de67d3dba8b3c3aec8a6a1642'
    };

    // Host-side inventory verifier (run from the repository root, not in a browser):
    // $swagger = Invoke-RestMethod http://localhost:5093/swagger/v1/swagger.json
    // Compare its GET/POST/PUT/DELETE method-and-path set with the literal operationKey values in api-phases.js.
    // The browser cannot perform this cross-origin comparison because Swagger is registered before CORS.
    var operations = `
DELETE /api/AdverseEvent/products/{documentGuid}/favorite
DELETE /api/Ai/conversations/{conversationId}
DELETE /api/Label/{menuSelection}/{encryptedId}
DELETE /api/Users/{encryptedUserId}
GET /api/AdverseEvent/correlation
GET /api/AdverseEvent/correlation/cell
GET /api/AdverseEvent/correlation/classes
GET /api/AdverseEvent/correlation/heatmap
GET /api/AdverseEvent/correlation/systems
GET /api/AdverseEvent/correlation/systems/cell
GET /api/AdverseEvent/correlation/systems/heatmap
GET /api/AdverseEvent/correlation/systems/map
GET /api/AdverseEvent/interchange
GET /api/AdverseEvent/products
GET /api/AdverseEvent/products/{documentGuid}/forest
GET /api/AdverseEvent/products/{documentGuid}/quadrant
GET /api/AdverseEvent/products/{documentGuid}/triage
GET /api/AdverseEvent/products/catalog
GET /api/AdverseEvent/products/count
GET /api/AdverseEvent/products/favorites
GET /api/AdverseEvent/reverse-lookup
GET /api/Ai/chat
GET /api/Ai/context
GET /api/Ai/conversations/{conversationId}
GET /api/Ai/conversations/{conversationId}/history
GET /api/Ai/conversations/stats
GET /api/Auth/accessdenied
GET /api/Auth/external-login
GET /api/Auth/external-logincallback
GET /api/Auth/lockout
GET /api/Auth/login
GET /api/Auth/login/{provider}
GET /api/Auth/loginfailure
GET /api/Auth/user
GET /api/Label/{menuSelection}/{encryptedId}
GET /api/Label/{menuSelection}/documentation
GET /api/Label/application-number/search
GET /api/Label/application-number/summaries
GET /api/Label/comparison/analysis/{documentGuid}
GET /api/Label/comparison/progress/{operationId}
GET /api/Label/complete/{pageNumber?}/{pageSize?}
GET /api/Label/document/navigation
GET /api/Label/document/version-history/{setGuidOrDocumentGuid}
GET /api/Label/drug-safety/dea-schedule
GET /api/Label/extract-product
GET /api/Label/generate/{documentGuid}/{minify}
GET /api/Label/guide
GET /api/Label/import/progress/{operationId}
GET /api/Label/indication/search
GET /api/Label/ingredient/active/summaries
GET /api/Label/ingredient/advanced
GET /api/Label/ingredient/by-application
GET /api/Label/ingredient/inactive/summaries
GET /api/Label/ingredient/related
GET /api/Label/ingredient/search
GET /api/Label/ingredient/summaries
GET /api/Label/inventory/summary
GET /api/Label/labeler/search
GET /api/Label/labeler/summaries
GET /api/Label/markdown/display/{documentGuid}
GET /api/Label/markdown/download/{documentGuid}
GET /api/Label/markdown/export/{documentGuid}
GET /api/Label/markdown/sections/{documentGuid}
GET /api/Label/ndc/package/search
GET /api/Label/ndc/search
GET /api/Label/original/{documentGuid}/{minify}
GET /api/Label/pharmacologic-class/hierarchy
GET /api/Label/pharmacologic-class/search
GET /api/Label/pharmacologic-class/summaries
GET /api/Label/product/indications
GET /api/Label/product/latest
GET /api/Label/product/latest/details
GET /api/Label/product/related
GET /api/Label/product/search
GET /api/Label/section/{menuSelection}
GET /api/Label/section/content/{documentGuid}
GET /api/Label/section/search
GET /api/Label/section/summaries
GET /api/Label/sectionMenu
GET /api/Label/single/{documentGuid}
GET /api/OrangeBook/expiring
GET /api/Settings/database-limits
GET /api/Settings/demomode
GET /api/Settings/features
GET /api/Settings/info
GET /api/Settings/logs
GET /api/Settings/logs/by-category
GET /api/Settings/logs/by-date
GET /api/Settings/logs/by-user
GET /api/Settings/logs/categories
GET /api/Settings/logs/statistics
GET /api/Settings/logs/users
GET /api/Settings/metrics/database-cost
GET /api/Settings/test/app-credential
GET /api/Settings/test/app-metrics-pipeline
GET /api/Users
GET /api/Users/{encryptedUserId}
GET /api/Users/byemail
GET /api/Users/endpoint-stats
GET /api/Users/me
GET /api/Users/user/{encryptedUserId}/activity
GET /api/Users/user/{encryptedUserId}/activity/daterange
POST /api/Ai/conversations
POST /api/Ai/interpret
POST /api/Ai/retry
POST /api/Ai/synthesize
POST /api/Auth/logout
POST /api/Auth/token-placeholder
POST /api/Label/{menuSelection}
POST /api/Label/comparison/analysis/{documentGuid}
POST /api/Label/import
POST /api/Settings/clearmanagedcache
POST /api/Users/authenticate
POST /api/Users/resolve-mcp
POST /api/Users/rotate-password
POST /api/Users/signup
PUT /api/AdverseEvent/products/{documentGuid}/favorite
PUT /api/Label/{menuSelection}/{encryptedId}
PUT /api/Users/{encryptedUserId}/profile
PUT /api/Users/admin-update
`.trim().split('\n');


    return Object.freeze({
        auditMetadata: Object.freeze(auditMetadata),
        operations: Object.freeze(operations.slice())
    });
})();
