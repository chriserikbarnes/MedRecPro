# MedRecPro API Skills Document

This document describes the available API endpoints for querying and managing SPL (Structured Product Labeling) pharmaceutical data in MedRecPro.

## Table of Contents

1. [System Overview](#system-overview)
2. [Navigation Views (Search & Discovery)](#navigation-views-search--discovery)
3. [Label CRUD Operations](#label-crud-operations)
4. [Import/Export Operations](#importexport-operations)
5. [AI Comparison Analysis](#ai-comparison-analysis)
6. [AI Agent Workflow](#ai-agent-workflow-interpret--execute--synthesize)
7. [Authentication](#authentication)
8. [User Management](#user-management)
9. [Settings & Caching](#settings--caching)
10. [Data Discovery Workflow](#data-discovery-workflow-important)
11. [Query Decision Tree](#query-decision-tree-expanded-scenarios)
12. [Table-to-Data Mapping Reference](#table-to-data-mapping-reference)
13. [Fallback Strategy](#fallback-strategy-for-404-errors)
14. [Multi-Step Workflows](#multi-step-workflows-dependent-endpoint-execution)
15. [Complete Request Examples](#complete-request-examples)
16. [Critical Reminders](#critical-reminders-for-data-queries)
17. [Important Notes](#important-notes)

---

## System Overview

**MedRecPro** is an SPL (Structured Product Labeling) pharmaceutical labeling management system based on the FDA SPL Implementation Guide.

### Key Characteristics

- **Base API Path**: `/api`
- **Authentication**: Google OAuth, Microsoft OAuth, Cookie-based
- **Data Security**: All primary/foreign keys are encrypted using secure cipher
- **Data Source**: SPL XML files from FDA DailyMed

---

## Navigation Views (Search & Discovery)

### Application Number Search

Search products by FDA application number (NDA, ANDA, BLA).

#### Search by Application Number
```
GET /api/Label/application-number/search?applicationNumber={value}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `applicationNumber` | string | Yes | Application number (e.g., NDA014526, ANDA125669) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: Find all products under NDA020702

**Trigger Phrases**: "find by application number", "search NDA", "search ANDA", "products under NDA"

#### Application Number Summaries
```
GET /api/Label/application-number/summaries?marketingCategory={code}&pageNumber={n}&pageSize={n}
```

Get aggregated summaries with product/document counts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `marketingCategory` | string | No | Filter by NDA, ANDA, BLA |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### Pharmacologic Class Search

Search by therapeutic/pharmacologic class.

#### Search by Pharmacologic Class
```
GET /api/Label/pharmacologic-class/search?classNameSearch={value}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `classNameSearch` | string | Yes | Pharmacologic class name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: Find all beta blockers, ACE inhibitors

**Trigger Phrases**: "find by drug class", "beta blockers", "ACE inhibitors", "drugs in class"

#### Pharmacologic Class Summaries
```
GET /api/Label/pharmacologic-class/summaries?pageNumber={n}&pageSize={n}
```

Get class summaries with product counts.

#### Pharmacologic Class Hierarchy
```
GET /api/Label/pharmacologic-class/hierarchy
```

Get the therapeutic class hierarchy tree (useful for faceted navigation).

---

### Ingredient Search

Search products by active or inactive ingredient (UNII or substance name).

#### Search by Ingredient
```
GET /api/Label/ingredient/search?unii={code}&substanceNameSearch={name}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `unii` | string | No* | FDA Unique Ingredient Identifier |
| `substanceNameSearch` | string | No* | Substance/ingredient name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

*At least one of `unii` or `substanceNameSearch` is required.

**Example**: Find products containing aspirin, acetaminophen

**Trigger Phrases**: "find by ingredient", "products containing aspirin", "drugs with acetaminophen"

#### All Ingredient Summaries
```
GET /api/Label/ingredient/summaries?minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get all ingredient summaries ranked by frequency.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `minProductCount` | int | No | Minimum product count filter |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

#### Active Ingredient Summaries
```
GET /api/Label/ingredient/active/summaries?minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get active ingredient summaries with product, document, and labeler counts.

#### Inactive Ingredient Summaries
```
GET /api/Label/ingredient/inactive/summaries?minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get inactive ingredient (excipient) summaries with product, document, and labeler counts.

---

### NDC (National Drug Code) Search

Search by product or package NDC code (supports partial matching).

#### Search by Product NDC
```
GET /api/Label/ndc/search?productCode={ndc}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productCode` | string | Yes | NDC product code (e.g., 12345-678-90) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: 12345-678-90 (returns matching products ordered by ProductCode)

**Trigger Phrases**: "search by NDC", "find NDC", "national drug code"

#### Search by Package NDC
```
GET /api/Label/ndc/package/search?packageCode={ndc}&pageNumber={n}&pageSize={n}
```

Search package configurations by NDC (packaging hierarchy, quantities, descriptions).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `packageCode` | string | Yes | NDC package code |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

---

### Labeler (Manufacturer) Search

Search products by marketing organization/labeler name.

#### Search by Labeler
```
GET /api/Label/labeler/search?labelerNameSearch={name}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `labelerNameSearch` | string | Yes | Labeler/manufacturer name |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: Find all Pfizer products

**Trigger Phrases**: "products by manufacturer", "Pfizer products", "manufactured by"

#### Labeler Summaries
```
GET /api/Label/labeler/summaries?pageNumber={n}&pageSize={n}
```

Get labeler summaries with product counts.

---

### Document Navigation

Navigate SPL documents and version history.

#### Search Documents
```
GET /api/Label/document/search?productNameSearch={name}&pageNumber={n}&pageSize={n}
```

Search documents by product name.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `productNameSearch` | string | Yes | Product name to search |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "find document", "LIPITOR document", "prescribing information for"

#### Document Version History
```
GET /api/Label/document/version-history/{setGuidOrDocumentGuid}
```

Get version history for a document set (newest first by VersionNumber).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `setGuidOrDocumentGuid` | GUID | Yes | SetGUID or DocumentGUID |

Use when you have a DocumentGUID or SetGUID and need all historical versions.

---

### Section Navigation

Search labeling sections by LOINC code.

#### Search by Section Code
```
GET /api/Label/section/search?sectionCode={loinc}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sectionCode` | string | Yes | LOINC section code |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Trigger Phrases**: "find boxed warnings", "show contraindications", "drug interactions"

#### Section Summaries
```
GET /api/Label/section/summaries?pageNumber={n}&pageSize={n}
```

Get section type frequency statistics (ordered by document count).

---

### Section Content Retrieval

Get section text content for AI summarization workflows.

#### Get Section Content
```
GET /api/Label/section/content/{documentGuid}?sectionGuid={guid?}&sectionCode={code?}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | Document GUID to retrieve content for |
| `sectionGuid` | GUID | No | Filter to specific section |
| `sectionCode` | string | No | Filter by LOINC code (e.g., "34084-4") |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Example**: Get adverse reactions text for summarization

**Trigger Phrases**: "summarize section", "get section text", "section content for", "text from section"

##### Common LOINC Section Codes

| Code | Section Name | User Request Examples |
|------|--------------|----------------------|
| 34066-1 | Boxed Warning | boxed warning, black box warning |
| 34067-9 | Indications and Usage | indications, uses, what is it for |
| 34068-7 | Dosage and Administration | dosing, dosage, how to take, administration |
| 34069-5 | Contraindications | contraindications, when not to use |
| 34070-3 | Warnings | warnings |
| 43685-7 | Warnings and Precautions | warnings and precautions |
| 34073-7 | Drug Interactions | drug interactions |
| 34084-4 | Adverse Reactions | adverse reactions, side effects |
| 34088-5 | Overdosage | overdose, overdosage |
| 34090-1 | Clinical Pharmacology | clinical pharmacology, mechanism |

---

## Label CRUD Operations

Dynamic CRUD operations for label sections. `menuSelection` = entity name.

### Discovery

#### Get Section Menu
```
GET /api/label/sectionMenu
```

List all available sections.

#### Get Section Documentation
```
GET /api/label/{menuSelection}/documentation
```

Get schema/field definitions for a section.

---

### Read Operations

#### Get Section Records
```
GET /api/label/section/{menuSelection}?pageNumber={n}&pageSize={n}
```

Get all records for section.

#### Get Record by ID
```
GET /api/label/{menuSelection}/{encryptedId}
```

Get single record by encrypted ID.

#### Get Complete Document
```
GET /api/label/single/{documentGuid}
```

Get complete document by GUID.

#### Get All Complete Documents
```
GET /api/label/complete/{pageNumber}/{pageSize}
```

Get all complete documents (paginated).

---

### Write Operations (Requires Authentication)

#### Create Record
```
POST /api/label/{menuSelection}
Body: JSON object matching section schema
```

#### Update Record
```
PUT /api/label/{menuSelection}/{encryptedId}
Body: JSON object with updated fields
```

#### Delete Record
```
DELETE /api/label/{menuSelection}/{encryptedId}
```

---

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

### SPL Import (Requires Authentication)

#### Import SPL ZIP Files
```
POST /api/label/import
Content-Type: multipart/form-data
Body: 'files' containing ZIP files
```

Returns `operationId` for progress tracking.

**Note**: ZIP files containing SPL XML can be obtained from DailyMed:
https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm

**Trigger Phrases**: "import SPL", "upload labels", "load ZIP files"

#### Get Import Progress
```
GET /api/label/import/progress/{operationId}
```

Check import progress.

---

### SPL Export

#### Generate SPL XML
```
GET /api/label/generate/{documentGuid}/{minify}
```

Generate SPL XML from document.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | Document identifier |
| `minify` | bool | No | true/false for compact output |

**Trigger Phrases**: "export XML", "generate SPL", "download label"

---

## AI Comparison Analysis

Compare original SPL XML with database representation.

### Queue Comparison Analysis
```
POST /api/label/comparison/analysis/{documentGuid}
```

Queue analysis.

### Get Comparison Results
```
GET /api/label/comparison/analysis/{documentGuid}
```

Get cached results.

### Get Comparison Progress
```
GET /api/label/comparison/progress/{operationId}
```

Check analysis progress.

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

## Data Discovery Workflow (IMPORTANT)

When views return 404 or do not provide the data needed, use the Label CRUD system:

### Step 1: Discover Available Tables
```
GET /api/label/sectionMenu
```

Returns list of all available data sections/tables.

This is the **PRIMARY discovery endpoint** - always works when database has data.

### Step 2: Get Data from Tables
```
GET /api/label/section/{menuSelection}?pageNumber=1&pageSize=50
```

Get records from any table.

`menuSelection` = exact table name from sectionMenu (e.g., "Document", "Product", "ActiveIngredient")

### Step 3: Get Table Schema (Optional)
```
GET /api/label/{menuSelection}/documentation
```

Get field definitions for a table.

---

## Query Decision Tree (Expanded Scenarios)

Use this decision tree to select the correct endpoint:

### User asks: 'Is the database empty / what do you have loaded?'

1. `GET /api/ai/context` (check documentCount/productCount if present)
2. If empty: recommend `POST /api/label/import` (SPL ZIPs from DailyMed)

### User asks: 'What products/documents do you have?'

1. **FIRST TRY**: `GET /api/label/section/Document?pageNumber=1&pageSize=50`
2. **FALLBACK**: `GET /api/label/section/Product?pageNumber=1&pageSize=50`

### User asks: 'I have an NDC—what is this product?'

1. `GET /api/Label/ndc/search?productCode={ndc}&pageNumber=1&pageSize=50`
2. If you need packaging breakdown: `GET /api/Label/ndc/package/search?packageCode={ndc}&pageNumber=1&pageSize=50`
3. If views fail: `GET /api/label/section/ProductIdentifier?pageNumber=1&pageSize=200` and filter by code

### User asks: 'Show me package sizes/configurations for this NDC'

1. `GET /api/Label/ndc/package/search?packageCode={ndc}&pageNumber=1&pageSize=50`
2. Fallback tables: `PackagingLevel`, `PackageItem`, `PackageIdentifier` via `/api/label/section/{menuSelection}`

### User asks: 'What are the document/product IDs?'

1. `GET /api/label/section/Document?pageNumber=1&pageSize=50` - returns encryptedId for each
2. Look for 'documentGuid' or 'encryptedId' fields in response

### User asks: 'Show me the version history for this label/document'

1. `GET /api/Label/document/version-history/{setGuidOrDocumentGuid}`
2. If you only have an encrypted document ID, first fetch Document table and map to DocumentGUID:
   - `GET /api/label/section/Document?pageNumber=1&pageSize=200` (filter by encryptedId)

### User asks: 'What ingredients are in the database?'

1. **FIRST TRY**: `GET /api/Label/ingredient/summaries?pageNumber=1&pageSize=50`
2. For active only: `GET /api/Label/ingredient/active/summaries?pageNumber=1&pageSize=50`
3. For inactive only: `GET /api/Label/ingredient/inactive/summaries?pageNumber=1&pageSize=50`
4. **FALLBACK**: `GET /api/label/section/ActiveIngredient?pageNumber=1&pageSize=50`
5. **ALSO**: `GET /api/label/section/InactiveIngredient?pageNumber=1&pageSize=50`

### User asks: 'Find products that contain ingredient X'

1. `GET /api/Label/ingredient/search?unii={code}` (best when UNII is known)
2. or `GET /api/Label/ingredient/search?substanceNameSearch={name}` (best when name is known)
3. If views fail: query `ActiveIngredient` / `InactiveIngredient` tables via Label CRUD and filter client-side

### User asks: 'What manufacturers/labelers?'

1. **FIRST TRY**: `GET /api/Label/labeler/summaries?pageNumber=1&pageSize=50`
2. **FALLBACK**: `GET /api/label/section/Organization?pageNumber=1&pageSize=50`

### User asks: 'What are the most common section types / what sections exist?'

1. **FIRST TRY**: `GET /api/Label/section/summaries?pageNumber=1&pageSize=50`
2. **FALLBACK**: `GET /api/label/section/Section?pageNumber=1&pageSize=100` (extract unique sectionCode/sectionTitle)

### User asks: 'Find a section by LOINC code (e.g., Boxed Warning)'

1. `GET /api/Label/section/search?sectionCode={loinc}&pageNumber=1&pageSize=50`
2. Fallback: `GET /api/label/section/Section?pageNumber=1&pageSize=200` and filter by sectionCode

### User asks about a specific product by name

1. **FIRST TRY**: `GET /api/Label/document/search?productNameSearch={name}`
2. **FALLBACK**: `GET /api/label/section/Product?pageNumber=1&pageSize=200` and filter results

### User asks: 'Summarize the warnings/adverse reactions/dosing for this product'

1. **Get DocumentGUID first** (if not known):
   - `GET /api/Label/document/search?productNameSearch={name}&pageNumber=1&pageSize=1`
2. **Get section content for summarization**:
   - `GET /api/Label/section/content/{documentGuid}?sectionCode={loincCode}`
3. **LOINC codes for common requests**:
   - Warnings: `43685-7` (Warnings and Precautions) or `34070-3` (Warnings)
   - Adverse reactions/side effects: `34084-4`
   - Dosing: `34068-7` (Dosage and Administration)
   - Drug interactions: `34073-7`
   - Boxed warning: `34066-1`

### User asks: 'How do I do multi-step AI-assisted querying?'

1. `POST /api/ai/interpret` with a natural language query
2. Execute returned endpoints on the client
3. `POST /api/ai/synthesize` with executed endpoint results
4. Optional: `POST /api/ai/conversations` to explicitly start a session first

### User asks: 'Clear cache / I changed core reference data and need consistency'

1. `POST /api/settings/clearmanagedcache` (admin-only in most deployments)

### Admin asks: 'Show endpoint usage / audit activity'

1. `GET /api/users/endpoint-stats?startDate={dt?}&endDate={dt?}`
2. `GET /api/users/user/{encryptedUserId}/activity?pageNumber=1&pageSize=50`
3. Date filtering: `/activity/daterange?startDate={dt}&endDate={dt}`

---

## Table-to-Data Mapping Reference

| Data Question | Primary Table (menuSelection) | Key Fields |
|--------------|------------------------------|------------|
| Documents/Labels | Document | documentGuid, setGuid, versionNumber |
| Products | Product | productName, ndcCode, productGuid |
| Active Ingredients | ActiveIngredient | substanceName, unii, strength |
| Inactive Ingredients | InactiveIngredient | substanceName, unii |
| Manufacturers/Labelers | Organization | organizationName, dunsNumber |
| Sections/Content | Section | sectionCode, sectionTitle, contentText |
| Section Text Content | vw_SectionContent (via endpoint) | documentGuid, sectionCode, contentText |
| NDC Codes | ProductIdentifier, PackageIdentifier | code, codeSystem |
| Packaging | PackagingLevel, PackageItem | quantity, formCode |
| Drug Classes | PharmacologicClass | className, classCode |
| Routes | Route | routeName, routeCode |
| Marketing Categories | MarketingCategory | categoryCode, categoryName |
| Drug Interactions | DrugInteraction | interactionDescription |
| Characteristics | Characteristic | characteristicName, value |
| User Activity | (system logs) | endpointPath, timestamp, userId |

---

## Fallback Strategy for 404 Errors

If a view endpoint returns 404, the view may not be implemented. Use this fallback:

### 404 on /api/Label/* endpoint:

- Switch to `GET /api/label/section/{relatedTable}?pageNumber=1&pageSize=50`
- Use Table-to-Data Mapping above to find the right table

### Empty results from any endpoint:

- Check if database has data: `GET /api/ai/context` (check documentCount)
- If documentCount=0, suggest user import SPL data

### Unknown search parameter:

- Get all records: `GET /api/label/section/{table}?pageNumber=1&pageSize=100`
- Filter/search results in synthesis response

### Section content retrieval fails

If `/api/Label/section/content/{documentGuid}` returns empty:

1. Verify the documentGuid is valid: `GET /api/label/single/{documentGuid}`
2. Check available sections: `GET /api/Label/section/summaries?pageNumber=1&pageSize=50`
3. Try the full document endpoint and extract sections: `GET /api/label/single/{documentGuid}`
4. Fallback to Section table: `GET /api/label/section/Section?pageNumber=1&pageSize=200` and filter by documentId

---

## Multi-Step Workflows (Dependent Endpoint Execution)

For complex queries requiring multiple API calls where later steps depend on earlier results, 
the interpret endpoint should return endpoints with `step`, `dependsOn`, and `outputMapping` properties.

### Endpoint Dependency Specification

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/ingredient/search",
      "method": "GET",
      "queryParameters": { "substanceNameSearch": "{ingredient}" },
      "description": "Search for products containing the ingredient",
      "outputMapping": {
        "documentGuid": "$[0].documentGuid",
        "productName": "$[0].productName"
      }
    },
    {
      "step": 2,
      "path": "/api/label/section/content/{{documentGuid}}",
      "method": "GET",
      "dependsOn": 1,
      "description": "Get the paragraph text for the section"
    }
  ]
}
```

### Dependency Properties

| Property | Type | Description |
|----------|------|-------------|
| `step` | int | Execution order (1, 2, 3...). Steps execute sequentially. |
| `dependsOn` | int | Step number this endpoint depends on. Skipped if dependency fails. |
| `outputMapping` | object | Map of variable names to JSONPath expressions for extraction. |
| `{{variableName}}` | string | Template variable substituted from previous step's outputMapping. |

### JSONPath Extraction Syntax

| Syntax | Example | Description |
|--------|---------|-------------|
| `$[0].field` | `$[0].documentGuid` | First array element's field |
| `$[n].field` | `$[2].productName` | Nth array element's field |
| `$.field` | `$.documentGuid` | Direct field from object |
| `field` | `documentGuid` | Shorthand for direct field |

---

## Workflow: Ingredient Safety Information

| Property | Value |
|----------|-------|
| **Intent** | Retrieve warnings, precautions, adverse effects, or side effects for an ingredient |
| **Triggers** | "adverse effects for {ingredient}", "side effects for {ingredient}", "precautions for {ingredient}", "warnings for {ingredient}" |

### Endpoint Specification (for interpret response)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/ingredient/search",
      "method": "GET",
      "queryParameters": {
        "substanceNameSearch": "{ingredient}",
        "pageNumber": 1,
        "pageSize": 10
      },
      "description": "Search for products containing {ingredient}",
      "outputMapping": {
        "documentGuid": "$[0].documentGuid",
        "productName": "$[0].productName",
        "labelerName": "$[0].labelerName"
      }
    },
    {
      "step": 2,
      "path": "/api/label/single/{{documentGuid}}",
      "method": "GET",
      "dependsOn": 1,
      "description": "Get complete label for {{productName}}"
    }
  ]
}
```

### Synthesis Instructions

When synthesizing results from this workflow:

1. **From Step 1**: Note how many products were found, list product names and manufacturers
2. **From Step 2**: Locate the section where `sectionDisplayName` contains:
   - "WARNINGS AND PRECAUTIONS" (LOINC: 43685-7)
   - "ADVERSE REACTIONS" (LOINC: 34084-4)
3. **Extract**: All `contentText` values from the matching section and its children
4. **Format**: Present as hierarchical content with section headers

### Example Response Format

```
Found {n} products containing {ingredient}. Here are the warnings and precautions 
for {productName} by {labelerName}:

## Warnings and Precautions

### {subsection title}
{contentText}

### {subsection title}  
{contentText}

---
**Other products containing {ingredient}:**
- {product 2} by {labeler 2}
- {product 3} by {labeler 3}

Would you like to see warnings for a different product?
```

---

## Workflow: Section Content Summarization

| Property | Value |
|----------|-------|
| **Intent** | Get section text content for AI summarization |
| **Triggers** | "summarize {section} for {product}", "get text from {section}", "section content", "summarize warnings" |

### Endpoint Specification

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/document/search",
      "method": "GET",
      "queryParameters": {
        "productNameSearch": "{productName}",
        "pageNumber": 1,
        "pageSize": 1
      },
      "description": "Find document for {productName}",
      "outputMapping": {
        "documentGuid": "$[0].documentGuid",
        "productName": "$[0].productName"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/section/content/{{documentGuid}}",
      "method": "GET",
      "queryParameters": {
        "sectionCode": "{loincCode}"
      },
      "dependsOn": 1,
      "description": "Get section text content for summarization"
    }
  ]
}
```

### Synthesis Instructions

When synthesizing section content for summarization:

1. **From Step 1**: Confirm the product was found, note the product name
2. **From Step 2**: Aggregate all `ContentText` values in `SequenceNumber` order
3. **Format**: Present as cohesive text, preserving section structure

### Example Response Format

```
Here is the {sectionName} section for {productName}:

{aggregated ContentText from all returned records, in sequence order}

---
Would you like me to summarize this content or retrieve a different section?
```

---

## Workflow: Manufacturer Product Portfolio

| Property | Value |
|----------|-------|
| **Intent** | List all products from a manufacturer with option to drill into specific product |
| **Triggers** | "products by {manufacturer}", "{manufacturer} drugs", "what does {manufacturer} make" |

### Endpoint Specification (Single Step - No Dependencies)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/labeler/search",
      "method": "GET",
      "queryParameters": {
        "labelerNameSearch": "{manufacturer}",
        "pageNumber": 1,
        "pageSize": 50
      },
      "description": "Search for products by {manufacturer}"
    }
  ]
}
```

### Follow-up Capability

After listing products, offer to show details:
- "Would you like to see the full label for any of these products?"
- "I can show warnings, dosing, or other sections for any product listed above."

---

## Error Handling for Dependent Workflows

### Step 1 Fails (No Results)
```
I searched for products containing {ingredient} but found no matches in the database.

Suggestions:
- Check the spelling of the ingredient name
- Try the generic name instead of brand name
- Search by UNII code if known: `GET /api/Label/ingredient/search?unii={code}`
```

### Step 2 Fails (Label Retrieval Error)
```
I found {n} products containing {ingredient}, but encountered an error retrieving 
the full label details.

Products found:
- {product 1} (DocumentGUID: {guid1})
- {product 2} (DocumentGUID: {guid2})

You can try asking for a specific product by name or GUID.
```

### No Matching Section Found
```
The label for {productName} was retrieved successfully, but it doesn't contain 
a "{sectionName}" section.

Available sections in this label:
- {section1}
- {section2}
- {section3}

Would you like to see one of these sections instead?
```

---

## Complete Request Examples

### Example: 'What products do you have?'
```
Endpoint: GET /api/label/section/Product?pageNumber=1&pageSize=50
Response: Array of product records with productName, ndcCode, etc.
```

### Example: 'List all document IDs'
```
Endpoint: GET /api/label/section/Document?pageNumber=1&pageSize=50
Response: Array with encryptedId and documentGuid for each document
```

### Example: 'What ingredients are available?'
```
Endpoint: GET /api/label/section/ActiveIngredient?pageNumber=1&pageSize=100
Response: Array with substanceName, unii, strength for each ingredient
```

### Example: 'Show me all tables in the system'
```
Endpoint: GET /api/label/sectionMenu
Response: ["Document", "Organization", "Product", "ActiveMoiety", ...]
```

### Example: 'Identify a product from NDC 12345-678-90'
```
Endpoint: GET /api/Label/ndc/search?productCode=12345-678-90&pageNumber=1&pageSize=10
Response: Array of matching products (EncryptedProductID, ProductName, LabelerName, etc.)
```

### Example: 'Show package configurations for NDC 12345-678-90'
```
Endpoint: GET /api/Label/ndc/package/search?packageCode=12345-678-90&pageNumber=1&pageSize=10
Response: Array of packages (PackageDescription, PackageQuantity, etc.)
```

### Example: 'Run an AI-assisted query (interpret → execute → synthesize)'
```
1) POST /api/ai/interpret  (body: { originalQuery: "Find all Pfizer products" })
2) Execute returned endpoint specs on client
3) POST /api/ai/synthesize (body: includes executedEndpoints[] results)
```

---

## Critical Reminders for Data Queries

1. **ALWAYS try /api/label/section/{table} if views fail** - This is the reliable fallback
2. **Use sectionMenu to discover tables** - `GET /api/label/sectionMenu`
3. **Table names are case-sensitive** - Use exactly: Document, Product, ActiveIngredient, etc.
4. **Pagination is required for large datasets** - Always include pageNumber and pageSize
5. **IDs are encrypted** - Use the encryptedId values returned, not raw database IDs
6. **Check context first** - `GET /api/ai/context` tells you document/product counts
7. **For multi-step AI**: interpret returns endpoint specs; synthesize converts executed results into answers
8. **When global data changes**: use managed cache clear to reduce stale reads (`POST /api/settings/clearmanagedcache`)

---

## Important Notes

1. **Encrypted IDs**: All IDs returned by the API are encrypted. Use these encrypted values in subsequent requests.

2. **Pagination**: `pageNumber` is 1-based. Default `pageSize` is typically 10.

3. **Authentication**: Write operations (POST, PUT, DELETE) require authentication. Read operations are generally public.

4. **Demo Mode**: Database may be periodically reset. User data is preserved.

5. **Empty Database**: If database is empty, suggest importing SPL ZIP files from DailyMed.

6. **Data Source**: SPL ZIP files can be downloaded from [DailyMed](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm).

---

## Contextual Responses

### Empty Database

When `isDatabaseEmpty == true`:

> The database is currently empty. To get started with MedRecPro, you'll need to import SPL data. Download ZIP files from [DailyMed](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm) and use the import endpoint.

Suggested action: `POST /api/label/import`

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
| Find products by ingredient | `/api/Label/ingredient/search` | `substanceNameSearch` |
| Find products by manufacturer | `/api/Label/labeler/search` | `labelerNameSearch` |
| Search by NDC | `/api/Label/ndc/search` | `productCode` |
| Search by application number | `/api/Label/application-number/search` | `applicationNumber` |
| Search by drug class | `/api/Label/pharmacologic-class/search` | `classNameSearch` |
| Find specific sections | `/api/Label/section/search` | `sectionCode` (LOINC) |
| Get section content for summarization | `/api/Label/section/content/{documentGuid}` | `sectionCode` (LOINC) |
| Import data | `/api/label/import` | (file upload) |