# MedRecPro AI Skills Document

This document describes the available API endpoints and capabilities that the AI agent can interpret and suggest when processing user requests in the MedRecPro pharmaceutical labeling management system.

## Table of Contents

1. [System Overview](#system-overview)
2. [Navigation Views](#navigation-views)
3. [Label CRUD Operations](#label-crud-operations)
4. [Import/Export Operations](#importexport-operations)
5. [AI Comparison Analysis](#ai-comparison-analysis)
6. [Authentication](#authentication)
7. [User Management](#user-management)
8. [Contextual Responses](#contextual-responses)
9. [Interpretation Guidelines](#interpretation-guidelines)

---

## System Overview

**MedRecPro** is an SPL (Structured Product Labeling) pharmaceutical labeling management system based on the FDA SPL Implementation Guide.

### Key Characteristics

- **Base API Path**: `/api`
- **Authentication**: Google OAuth, Microsoft OAuth, Cookie-based
- **Data Security**: All primary/foreign keys are encrypted using secure cipher
- **Data Source**: SPL XML files from FDA DailyMed

---

## Navigation Views

Search and discovery endpoints for navigating pharmaceutical data.

### Application Number Navigation

Search products by FDA application number (NDA, ANDA, BLA).

#### Search by Application Number
```
GET /api/views/application-number/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `applicationNumber` | string | Yes | Application number (e.g., NDA014526, ANDA125669) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "find by application number", "search NDA", "search ANDA", "products under NDA"

#### Application Number Summaries
```
GET /api/views/application-number/summaries
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `marketingCategory` | string | No | Filter by NDA, ANDA, BLA |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### Pharmacologic Class Navigation

Search by therapeutic/pharmacologic class.

#### Search by Pharmacologic Class
```
GET /api/views/pharmacologic-class/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `classNameSearch` | string | Yes | Pharmacologic class name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "find by drug class", "beta blockers", "ACE inhibitors", "drugs in class"

---

### Ingredient Navigation

Search products by active ingredient (UNII or substance name).

#### Search by Ingredient
```
GET /api/views/ingredient/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `unii` | string | No* | FDA Unique Ingredient Identifier |
| `substanceNameSearch` | string | No* | Substance/ingredient name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

*At least one of `unii` or `substanceNameSearch` is required.

**Trigger Phrases**: "find by ingredient", "products containing aspirin", "drugs with acetaminophen"

#### Ingredient Summaries
```
GET /api/views/ingredient/summaries
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `minProductCount` | int | No | Minimum product count filter |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### NDC (National Drug Code) Navigation

Search by product or package NDC code.

#### Search by Product NDC
```
GET /api/views/ndc/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productCode` | string | Yes | NDC product code (e.g., 12345-678-90) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "search by NDC", "find NDC", "national drug code"

#### Search by Package NDC
```
GET /api/views/ndc/package/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `packageCode` | string | Yes | NDC package code |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### Labeler (Manufacturer) Navigation

Search products by marketing organization/labeler name.

#### Search by Labeler
```
GET /api/views/labeler/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `labelerNameSearch` | string | Yes | Labeler/manufacturer name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "products by manufacturer", "Pfizer products", "manufactured by"

#### Labeler Summaries
```
GET /api/views/labeler/summaries
```

Returns labeler statistics ranked by product count.

---

### Document Navigation

Navigate SPL documents and version history.

#### Search Documents
```
GET /api/views/document/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productNameSearch` | string | Yes | Product name to search |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "find document", "LIPITOR document", "prescribing information for"

#### Document Version History
```
GET /api/views/document/version-history/{setGuidOrDocumentGuid}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `setGuidOrDocumentGuid` | GUID | Yes | SetGUID or DocumentGUID |

---

### Section Navigation

Search labeling sections by LOINC code.

#### Search by Section Code
```
GET /api/views/section/search
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sectionCode` | string | Yes | LOINC section code |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

##### Common LOINC Section Codes

| Code | Section Name |
|------|--------------|
| 34066-1 | Boxed Warning |
| 34067-9 | Indications and Usage |
| 34068-7 | Dosage and Administration |
| 34069-5 | Contraindications |
| 34070-3 | Warnings |
| 34071-1 | Warnings and Precautions |
| 34073-7 | Drug Interactions |
| 34084-4 | Adverse Reactions |
| 34088-5 | Overdosage |
| 34090-1 | Clinical Pharmacology |

**Trigger Phrases**: "find boxed warnings", "show contraindications", "drug interactions"

---

## Label CRUD Operations

Dynamic CRUD operations for label data sections.

### Read Operations (No Authentication Required)

#### Get Section Menu
```
GET /api/labels/sectionMenu
```
Lists all available label sections/entity types.

#### Get Section Documentation
```
GET /api/labels/{menuSelection}/documentation
```
Returns schema documentation for a specific section.

#### Get Section Records
```
GET /api/labels/section/{menuSelection}?pageNumber={n}&pageSize={n}
```
Returns paginated records for a section.

#### Get Record by ID
```
GET /api/labels/{menuSelection}/{encryptedId}
```
Returns a specific record by encrypted ID.

#### Get Complete Document
```
GET /api/labels/single/{documentGuid}
```
Returns complete document with all related sections.

#### Get All Complete Documents
```
GET /api/labels/complete/{pageNumber}/{pageSize}
```
Returns paginated list of complete documents.

### Write Operations (Authentication Required)

#### Create Record
```
POST /api/labels/{menuSelection}
Body: JSON object matching section schema
```

#### Update Record
```
PUT /api/labels/{menuSelection}/{encryptedId}
Body: JSON object with updated fields
```

#### Delete Record
```
DELETE /api/labels/{menuSelection}/{encryptedId}
```

### Available Sections (menuSelection values)

| Section | Description |
|---------|-------------|
| Document | Main SPL document metadata |
| Organization | Companies and regulatory bodies |
| Product | Drug products |
| ActiveMoiety | Active moiety substances |
| ActiveIngredient | Active ingredients with strengths |
| InactiveIngredient | Inactive/excipient ingredients |
| Section | Document sections (by LOINC code) |
| Subsection | Nested content within sections |
| PackagingLevel | Package hierarchy levels |
| PackageItem | Individual package items |
| ProductIdentifier | NDC and other product codes |
| PackageIdentifier | Package-level NDC codes |
| Characteristic | Physical characteristics |
| MarketingCategory | NDA, ANDA, BLA classifications |
| Route | Routes of administration |
| EquivalentSubstance | Equivalent substance mappings |
| PharmacologicClass | Therapeutic classifications |
| DrugInteraction | Drug interaction data |
| ContraindicatedDrug | Contraindicated combinations |
| ItemContains | Container contents relationships |
| ContainedItem | Items within packages |
| Address | Organization addresses |
| BusinessOperation | Manufacturing operations |

---

## Import/Export Operations

### Import SPL ZIP Files (Authentication Required)
```
POST /api/labels/import
Content-Type: multipart/form-data
Body: files (ZIP files containing SPL XML)
```

Returns `operationId` for progress tracking.

**Data Source**: ZIP files available from [DailyMed](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm)

**Trigger Phrases**: "import SPL", "upload labels", "load ZIP files"

### Get Import Progress
```
GET /api/labels/import/progress/{operationId}
```

### Generate SPL XML
```
GET /api/labels/generate/{documentGuid}/{minify}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | Document identifier |
| `minify` | bool | No | Compact XML output (default: false) |

**Trigger Phrases**: "export XML", "generate SPL", "download label"

---

## AI Comparison Analysis

Compare original SPL XML with database representation.

### Queue Comparison Analysis
```
POST /api/labels/comparison/analysis/{documentGuid}
```

Starts AI-powered analysis comparing source XML with DTO representation.

### Get Comparison Results
```
GET /api/labels/comparison/analysis/{documentGuid}
```

Returns cached comparison results.

### Get Comparison Progress
```
GET /api/labels/comparison/progress/{operationId}
```

---

## Authentication

### OAuth Login
```
GET /api/auth/login/{provider}
```

| Parameter | Values |
|-----------|--------|
| `provider` | Google, Microsoft |

### Get Current User
```
GET /api/auth/user
```
Requires authentication.

### Logout
```
POST /api/auth/logout
```
Requires authentication.

---

## User Management

### Get My Profile
```
GET /api/users/me
```
Requires authentication.

### Get User Activity
```
GET /api/users/user/{encryptedUserId}/activity
```
Requires authentication.

---

## Contextual Responses

### Empty Database

When `isDatabaseEmpty == true`:

> The database is currently empty. To get started with MedRecPro, you'll need to import SPL data. Download ZIP files from [DailyMed](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm) and use the import endpoint.

Suggested action: `POST /api/labels/import`

### Demo Mode

When `isDemoMode == true`:

> In demo mode, the database is periodically reset. User accounts and activity logs are preserved, but label data may be removed.

### Authentication Required

When an operation requires authentication and user is not authenticated:

> This operation requires authentication. Please log in using Google or Microsoft OAuth.

Suggested action: `GET /api/auth/login/Google`

---

## Interpretation Guidelines

### Priority Order

1. Check if database is empty - suggest import if so
2. Determine if request requires authentication
3. Match intent to most specific endpoint
4. Use search endpoints for discovery queries
5. Use CRUD endpoints for specific record operations
6. Provide clarifying questions for ambiguous requests

### Common Intent Patterns

| User Intent | Endpoint | Key Parameter |
|-------------|----------|---------------|
| Find products by ingredient | `/api/views/ingredient/search` | `substanceNameSearch` |
| Find products by manufacturer | `/api/views/labeler/search` | `labelerNameSearch` |
| Search by NDC | `/api/views/ndc/search` | `productCode` |
| Search by application number | `/api/views/application-number/search` | `applicationNumber` |
| Search by drug class | `/api/views/pharmacologic-class/search` | `classNameSearch` |
| Find specific sections | `/api/views/section/search` | `sectionCode` (LOINC) |
| Import data | `/api/labels/import` | (file upload) |

---

## Notes

1. **Encrypted IDs**: All IDs returned by the API are encrypted. Use these encrypted values in subsequent requests.

2. **Pagination**: `pageNumber` is 1-based. Default `pageSize` is typically 10.

3. **Authentication**: Write operations (POST, PUT, DELETE) require authentication. Read operations are generally public.

4. **Demo Mode**: Data may be periodically reset. User data is preserved.

5. **Data Source**: SPL ZIP files can be downloaded from [DailyMed](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm).
