# MedRecPro API Skills Document

This document describes the available API endpoints for querying and managing SPL (Structured Product Labeling) pharmaceutical data in MedRecPro.

## Table of Contents

1. [System Overview](#system-overview)
2. [Quick Reference: Key Endpoints](#quick-reference-key-endpoints)
3. [Navigation Views (Search & Discovery)](#navigation-views-search--discovery)
4. [Label CRUD Operations](#label-crud-operations)
5. [Import/Export Operations](#importexport-operations)
6. [AI Comparison Analysis](#ai-comparison-analysis)
7. [Data Discovery Workflow](#data-discovery-workflow-important)
8. [Query Decision Tree](#query-decision-tree-expanded-scenarios)
9. [Table-to-Data Mapping Reference](#table-to-data-mapping-reference)
10. [Fallback Strategy](#fallback-strategy-for-404-errors)
11. [Multi-Step Workflows](#multi-step-workflows-dependent-endpoint-execution)
12. [Complete Request Examples](#complete-request-examples)
13. [Critical Reminders](#critical-reminders-for-data-queries)
14. [Important Notes](#important-notes)

---

## System Overview

**MedRecPro** is an SPL (Structured Product Labeling) pharmaceutical labeling management system based on the FDA SPL Implementation Guide.

### Key Characteristics

- **Base API Path**: `/api`
- **Authentication**: Google OAuth, Microsoft OAuth, Cookie-based
- **Data Security**: All primary/foreign keys are encrypted using secure cipher
- **Data Source**: SPL XML files from FDA DailyMed

---

## Quick Reference: Key Endpoints

### CRITICAL: Attribution Policy

**ALL content in summaries and comparisons MUST be sourced from API responses.** Never supplement with training data. Every fact must be attributable to a specific API endpoint.

### Primary Endpoints by Task

| Task | Endpoint | Notes |
|------|----------|-------|
| **Summarize/Compare Drug Labels** | `GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}` | **PREFERRED** - Use sectionCode filter for token optimization |
| Find Products by Name | `GET /api/Label/document/search?productNameSearch={name}` | Returns `documentGuid` |
| Find Products by Ingredient | `GET /api/Label/ingredient/advanced?substanceNameSearch={name}` | Returns `documentGUID` |
| Get Complete Document | `GET /api/Label/single/{documentGuid}` | Full document metadata |
| Generate SPL XML | `GET /api/Label/generate/{documentGuid}/true` | View full label link |

### Markdown Sections Endpoint (For AI Summarization)

```
GET /api/Label/markdown/sections/{documentGuid}
GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | The document identifier from search results |
| `sectionCode` | string | No | LOINC code to filter sections (e.g., "34067-9" for Indications) |

**Token Optimization:** Use `sectionCode` parameter when comparing multiple drugs. Reduces payload from ~88KB (all sections) to ~1-2KB per section.

**Response:** Array of sections with pre-formatted `fullSectionText` ready for AI consumption.

**Key Fields:**
- `sectionCode` - LOINC code (e.g., "34089-3" for DESCRIPTION)
- `sectionTitle` - Human-readable title
- `fullSectionText` - **Pre-aggregated markdown content**
- `documentTitle` - For attribution

### Common LOINC Codes

| Code | Section | Use For |
|------|---------|---------|
| 34089-3 | DESCRIPTION | Drug class, mechanism |
| 34067-9 | INDICATIONS AND USAGE | What it treats |
| 34084-4 | ADVERSE REACTIONS | Side effects comparison |
| 34070-3 | CONTRAINDICATIONS | When not to use |
| 43685-7 | WARNINGS AND PRECAUTIONS | Safety info |
| 34068-7 | DOSAGE AND ADMINISTRATION | Dosing info |
| 34073-7 | DRUG INTERACTIONS | Interaction warnings |

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

#### All Ingredient Summaries
```
GET /api/Label/ingredient/summaries?ingredient={text}&minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get all ingredient summaries ranked by frequency.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `ingredient` | string | No | Filter by ingredient name (partial match on SubstanceName). Use to vary results for paginated queries. |
| `minProductCount` | int | No | Minimum product count filter |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

#### Active Ingredient Summaries
```
GET /api/Label/ingredient/active/summaries?ingredient={text}&minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get active ingredient summaries with product, document, and labeler counts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `ingredient` | string | No | Filter by ingredient name (partial match on SubstanceName). Use to vary results for paginated queries. |
| `minProductCount` | int | No | Minimum product count filter |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

#### Inactive Ingredient Summaries
```
GET /api/Label/ingredient/inactive/summaries?ingredient={text}&minProductCount={n}&pageNumber={n}&pageSize={n}
```

Get inactive ingredient (excipient) summaries with product, document, and labeler counts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `ingredient` | string | No | Filter by ingredient name (partial match on SubstanceName). Use to vary results for paginated queries. |
| `minProductCount` | int | No | Minimum product count filter |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

#### Ingredient Search
```
GET /api/Label/ingredient/advanced?unii={code}&substanceNameSearch={name}&applicationNumber={appNum}&applicationType={type}&productNameSearch={product}&activeOnly={bool}&pageNumber={n}&pageSize={n}
```

Enhanced ingredient search with application number filtering, document linkage, and product name matching.

**IMPORTANT:** This is the ONLY ingredient search endpoint that returns application numbers.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `unii` | string | No* | FDA UNII code for exact match |
| `substanceNameSearch` | string | No* | Substance name (partial/phonetic match) |
| `applicationNumber` | string | No* | Application number (e.g., NDA020702, 020702) |
| `applicationType` | string | No* | NDA, ANDA, BLA filter |
| `productNameSearch` | string | No* | Product name (partial/phonetic match) |
| `activeOnly` | bool | No | true=active, false=inactive, null=all |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

*At least one search parameter is required.

**Response includes:**
- `DocumentGUID` for linking to: `/api/label/generate/{documentGuid}` and `/api/label/single/{documentGuid}`
- `ApplicationNumber` and `ApplicationType` for regulatory context
- `ClassCode` to distinguish active vs inactive ingredients

**Example**: Find products containing aspirin, acetaminophen

**Trigger Phrases**: "find by application number", "search NDA ingredients", "products with ANDA", "inactive ingredients for product", "what application number", "show application number for", "what is the NDA for", "what is the ANDA for","find by ingredient", "products containing aspirin", "drugs with acetaminophen"

#### Find Products by Application Number with Same Ingredient
```
GET /api/Label/ingredient/by-application?applicationNumber={appNum}&pageNumber={n}&pageSize={n}
```

Find all products that contain the same active ingredient as a specified application number.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `applicationNumber` | string | Yes | Application number (e.g., NDA020702, 020702) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Use Case:** Given an NDA number, find all generic (ANDA) products with the same active ingredient.

**Trigger Phrases**: "find generic equivalents", "products with same ingredient as NDA", "related drugs by application number"

#### Get Related Ingredients
```
GET /api/Label/ingredient/related?unii={code}&substanceNameSearch={name}&isActive={bool}
```

Find all products containing a specified ingredient and their related active/inactive ingredients.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `unii` | string | No* | FDA UNII code to search |
| `substanceNameSearch` | string | No* | Substance name to search |
| `isActive` | bool | No | true if searching active ingredient (default), false for inactive |

*At least one of `unii` or `substanceNameSearch` is required.

**Response includes:**
- `searchedIngredients`: Ingredients matching the search criteria
- `relatedActiveIngredients`: All active ingredients in found products
- `relatedInactiveIngredients`: All inactive ingredients (excipients) in found products
- `relatedProducts`: Unique products containing the searched ingredient
- Summary counts: `totalActiveCount`, `totalInactiveCount`, `totalProductCount`

**Use Case:** Given an active ingredient, find all products containing it and their inactive ingredients.

**Trigger Phrases**: "related ingredients", "excipients in products with", "inactive ingredients for drug containing"

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

#### Get Section Content as Markdown (PREFERRED)
```
GET /api/Label/markdown/sections/{documentGuid}
```

**This is the PREFERRED endpoint for all summarization and comparison workflows.** It returns pre-formatted markdown text with all section content aggregated and ready for AI consumption.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | Document GUID to retrieve content for |

**Response Fields:**
- `documentGUID`: The document identifier
- `setGUID`: The document set identifier
- `documentTitle`: Full document title with approval info
- `sectionCode`: LOINC code for the section
- `sectionTitle`: Human-readable section title
- `sectionKey`: Unique key combining document, code, and title
- `fullSectionText`: **Pre-formatted markdown content ready for summarization**
- `contentBlockCount`: Number of content blocks aggregated

**Example Response:**
```json
{
  "labelSectionMarkdown": {
    "documentGUID": "774105bc-8258-481c-8883-f25f25166aea",
    "sectionCode": "34089-3",
    "sectionTitle": "11 DESCRIPTION",
    "fullSectionText": "## 11 DESCRIPTION\r\n\r\nFEMARA (letrozole) is a nonsteroidal aromatase inhibitor...",
    "contentBlockCount": 13
  }
}
```

**Use Cases:**
- Summarizing drug labels for users
- Comparing sections across multiple products
- Generating comparison tables with attributable data
- Any AI-powered content synthesis

**Trigger Phrases**: "summarize section", "compare drugs", "drug comparison", "section content for", "text from section"

#### Get Section Content (Raw/Legacy)
```
GET /api/Label/section/content/{documentGuid}?sectionGuid={guid?}&sectionCode={code?}&pageNumber={n}&pageSize={n}
```

Returns individual content blocks (use markdown endpoint above for aggregated content).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentGuid` | GUID | Yes | Document GUID to retrieve content for |
| `sectionGuid` | GUID | No | Filter to specific section |
| `sectionCode` | string | No | Filter by LOINC code (e.g., "34084-4") |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

##### Common LOINC Section Codes

| Code | Section Name | User Request Examples |
|------|--------------|----------------------|
| 34066-1 | Boxed Warning | boxed warning, black box warning |
| 34067-9 | Indications and Usage | indications, uses, what is it for |
| 34068-7 | Dosage and Administration | dosing, dosage, how to take, administration |
| 34070-3 | Contraindications | contraindications, when not to use |
| 34071-1 | Warnings | warnings |
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

Use this decision tree to select the correct endpoint.

### Data Sources Best Practices

**IMPORTANT:** When responding to user queries, always include relevant API links in a "Data Sources" section so users can explore further:

- For ingredient queries: Include link to `/api/Label/ingredient/advanced` (shows application numbers, document links)
- For application number queries: Include link to `/api/Label/ingredient/by-application` (find related products)
- For inactive ingredient queries: Include link to `/api/Label/ingredient/related` (shows all ingredients in related products)
- Always use the **advanced search** endpoint when application numbers, document GUIDs, or regulatory context is relevant

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

### User asks: 'Find products that contain ingredient X' or 'What are the ingredients for [drug]?'

**Standard workflow - use BOTH endpoints for comprehensive results:**

1. **Search**: `GET /api/Label/ingredient/advanced?substanceNameSearch={name}` (or `unii={code}`)
   - Returns ApplicationNumber, ApplicationType, DocumentGUID for linking to labels
   - Include this link in Data Sources for user reference
2. **For related ingredients**: `GET /api/Label/ingredient/related?substanceNameSearch={name}&isActive=true`
   - Returns both active and inactive ingredients for products containing the ingredient
3. If views fail: query `ActiveIngredient` / `InactiveIngredient` tables via Label CRUD and filter client-side

**Data Sources should include:**
- Link to advanced search: `/api/Label/ingredient/advanced?substanceNameSearch={name}` (shows application numbers)
- Link to related ingredients: `/api/Label/ingredient/related?substanceNameSearch={name}&isActive=true`

### User asks: 'What is the application number for [ingredient/UNII/product]?'

1. **USE THIS**: `GET /api/Label/ingredient/advanced?unii={code}` (returns ApplicationNumber and ApplicationType)
2. **OR**: `GET /api/Label/ingredient/advanced?substanceNameSearch={name}` (if UNII not known)
3. **OR**: `GET /api/Label/ingredient/advanced?productNameSearch={product}` (search by product name)

The response includes `ApplicationNumber` and `ApplicationType` fields directly in the results.


### User asks: 'Find products by application number' or 'Find generic equivalents'

1. `GET /api/Label/ingredient/by-application?applicationNumber={appNum}` (find all products with same active ingredient)
2. `GET /api/Label/ingredient/advanced?applicationNumber={appNum}` (filter by application number)
3. `GET /api/Label/application-number/search?applicationNumber={appNum}` (direct application number search)

### User asks: 'What inactive ingredients (excipients) are in products containing X?' or 'What are the inactive ingredients for ANDA [number]?'

1. **For ingredient-based search**: `GET /api/Label/ingredient/related?substanceNameSearch={name}&isActive=true`
   - Response includes `relatedInactiveIngredients` with all excipients found in products containing the ingredient
2. **For application number-based search**: `GET /api/Label/ingredient/advanced?applicationNumber={appNum}&activeOnly=false`
   - Returns inactive ingredients for products with that application number
3. **ALSO**: `GET /api/Label/ingredient/advanced?substanceNameSearch={name}&activeOnly=false`
   - Search inactive ingredients by name directly

**Data Sources should include:**
- Link to advanced search for inactive ingredients: `/api/Label/ingredient/advanced?applicationNumber={appNum}&activeOnly=false`
- Link to related ingredients: `/api/Label/ingredient/related?substanceNameSearch={name}&isActive=true`

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
2. **Get section content for summarization (PREFERRED)**:
   - `GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}` - use sectionCode filter for token optimization
   - Example: `GET /api/Label/markdown/sections/{documentGuid}?sectionCode=34084-4` for adverse reactions
3. **LOINC codes for common requests**:
   - Warnings: `43685-7` (Warnings and Precautions) or `34071-1` (Warnings)
   - Adverse reactions/side effects: `34084-4`
   - Dosing: `34068-7` (Dosage and Administration)
   - Drug interactions: `34073-7`
   - Boxed warning: `34066-1`
   - Description/Mechanism: `34089-3`

### User asks: 'Compare drug X vs drug Y' or 'Compare these drugs'

1. **Find documents for each product**:
   - `GET /api/Label/document/search?productNameSearch={product1}` → get `documentGuid1`
   - `GET /api/Label/document/search?productNameSearch={product2}` → get `documentGuid2`
2. **Get relevant sections for each product (use sectionCode filter for token optimization)**:
   - `GET /api/Label/markdown/sections/{documentGuid1}?sectionCode={loincCode}`
   - `GET /api/Label/markdown/sections/{documentGuid2}?sectionCode={loincCode}`
   - Example for adverse reactions: `sectionCode=34084-4`
   - Example for indications: `sectionCode=34067-9`
3. **Build comparison using `fullSectionText`** from matching `sectionCode` values
4. **CRITICAL**: Only include information found in the API responses - never supplement with training data

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
| Section Markdown (PREFERRED) | /api/Label/markdown/sections/{guid}?sectionCode={loincCode} | documentGuid, sectionCode, fullSectionText |
| Section Text Content (Legacy) | vw_SectionContent (via endpoint) | documentGuid, sectionCode, contentText |
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

If section content endpoints return empty:

1. **PREFERRED**: Try markdown endpoint: `GET /api/Label/markdown/sections/{documentGuid}`
   - Returns all sections as pre-formatted markdown with `fullSectionText`
2. Verify the documentGuid is valid: `GET /api/label/single/{documentGuid}`
3. Check available sections: `GET /api/Label/section/summaries?pageNumber=1&pageSize=50`
4. Try the full document endpoint and extract sections: `GET /api/label/single/{documentGuid}`
5. Fallback to Section table: `GET /api/label/section/Section?pageNumber=1&pageSize=200` and filter by documentId

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
      "path": "/api/Label/ingredient/advanced",
      "method": "GET",
      "queryParameters": { "substanceNameSearch": "{ingredient}" },
      "description": "Search for products containing the ingredient",
      "outputMapping": {
        "documentGuid": "$[0].documentGUID",
        "productName": "$[0].productName"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "{loincCode}" },
      "dependsOn": 1,
      "description": "Get specific section as pre-formatted markdown (use sectionCode for token optimization)"
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

### Endpoint Specification (Using Markdown Endpoint with sectionCode Filter)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/ingredient/advanced",
      "method": "GET",
      "queryParameters": {
        "substanceNameSearch": "{ingredient}",
        "pageNumber": 1,
        "pageSize": 10
      },
      "description": "Search for products containing {ingredient}",
      "outputMapping": {
        "documentGuid": "$[0].documentGUID",
        "productName": "$[0].productName",
        "labelerName": "$[0].labelerName"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "43685-7" },
      "dependsOn": 1,
      "description": "Get warnings/precautions section (token optimized)"
    },
    {
      "step": 3,
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section (token optimized)"
    }
  ]
}
```

### Synthesis Instructions

When synthesizing results from this workflow:

1. **From Step 1**: Note how many products were found, list product names and manufacturers
2. **From Steps 2-3**: Use the filtered sections directly:
   - Step 2: `43685-7` for WARNINGS AND PRECAUTIONS
   - Step 3: `34084-4` for ADVERSE REACTIONS
3. **Use `fullSectionText`**: Content is already formatted as markdown
4. **Format**: Present directly with attribution

### Example Response Format

```
Found {n} products containing {ingredient}. Here are the warnings and precautions
for {productName} by {labelerName}:

{fullSectionText from sectionCode 43685-7}

## Adverse Reactions

{fullSectionText from sectionCode 34084-4}

---
**Data sources:**
- Warnings and Precautions - {documentTitle}
- Adverse Reactions - {documentTitle}

**Other products containing {ingredient}:**
- {product 2} by {labeler 2}
- {product 3} by {labeler 3}

Would you like to see warnings for a different product?
```

---

## Workflow: Comprehensive Drug Summary/Usage

| Property | Value |
|----------|-------|
| **Intent** | Provide a complete summary of a drug with all available data from the label |
| **Triggers** | "summarize usage for {drug}", "what is {drug}", "tell me about {drug}", "summarize {drug}", "overview of {drug}", "information about {drug}" |

### CRITICAL: Data Sourcing Policy

**ALL information MUST come from executed API endpoints. Never supplement with training data.**

- If a fact isn't in the API response, state "not available in label data"
- Marketing start date is NOT the same as original FDA approval date
- Clinical study results must come from section 34092-7, not general knowledge

### Endpoint Specification (Using Markdown Endpoint)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/ingredient/advanced",
      "method": "GET",
      "queryParameters": {
        "substanceNameSearch": "{ingredient}",
        "pageNumber": 1,
        "pageSize": 50
      },
      "description": "Search for products containing {ingredient}",
      "outputMapping": {
        "documentGuid": "$[0].documentGUID",
        "productName": "$[0].productName",
        "labelerName": "$[0].labelerName",
        "applicationNumber": "$[0].applicationNumber",
        "marketingCategory": "$[0].marketingCategory"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/single/{{documentGuid}}",
      "method": "GET",
      "dependsOn": 1,
      "description": "Get complete document with all metadata (dosage form, route, marketing dates)"
    },
    {
      "step": 3,
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "method": "GET",
      "dependsOn": 1,
      "description": "Get all sections as pre-formatted markdown for summarization"
    }
  ]
}
```

**Note:** Step 3 returns all sections in a single call (~88KB). For comprehensive summaries this is appropriate. For targeted queries, use `?sectionCode={loincCode}` to reduce payload to ~1-2KB:
- `34067-9` for Indications and Usage
- `34089-3` for Description (drug class info)
- `34084-4` for Adverse Reactions

### Data Source Mapping

| Response Field | Source | Path |
|----------------|--------|------|
| Indication | Step 3 | `fullSectionText` where `sectionCode` = "34067-9" |
| Active Ingredient | Step 2 | activeIngredients[].substanceName, strength |
| Dosage Form | Step 2 | products[].dosageForm |
| Route | Step 2 | routes[].routeName |
| Marketing Date | Step 2 | marketingCategories[].startDate |
| Application Number | Step 1 | applicationNumber |
| Manufacturer | Step 1 | labelerName |
| Drug Class | Step 3 | `fullSectionText` where `sectionCode` = "34089-3" |

### Synthesis Format

```
## {ProductName} Usage Summary

**Primary Indication:**
{Exact text from Indications section ContentText}

**Product Details:**
- **Active Ingredient:** {from Step 2 activeIngredients}
- **Dosage Form:** {from Step 2 products.dosageForm}
- **Route:** {from Step 2 routes.routeName}
- **Manufacturer:** {from Step 1 labelerName}

**Regulatory Information:**
- **Application Number:** {from Step 1 applicationNumber}
- **Marketing Category:** {from Step 1 marketingCategory}
- **Marketing Start Date:** {from Step 2 marketingCategories.startDate}

**Key Information:**
{Brief summary from Indications ContentText}

**Important Note:** The prescribing information contains additional details about dosing, administration, contraindications, and safety information that should be reviewed before use.
```

### What NOT to Include (Training Data Leakage)

- ❌ "Originally approved in 1996" (unless from marketingCategories.startDate)
- ❌ "Clinical studies support its effectiveness" (unless from section 34092-7)
- ❌ "Long history of clinical use" (inference, not from data)
- ❌ Drug comparisons not in the label
- ❌ Mechanism details not in Description/Clinical Pharmacology sections

---

## Workflow: Section Content Summarization

| Property | Value |
|----------|-------|
| **Intent** | Get section text content for AI summarization |
| **Triggers** | "summarize {section} for {product}", "get text from {section}", "section content", "summarize warnings" |

### Endpoint Specification (Using Markdown Endpoint)

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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "{requestedLoincCode}" },
      "dependsOn": 1,
      "description": "Get specific section as pre-formatted markdown (use sectionCode for token optimization)"
    }
  ]
}
```

**Token Optimization:** Use `sectionCode` parameter to fetch only the requested section. This reduces payload from ~88KB (all sections) to ~1-2KB per section.

### Synthesis Instructions

When synthesizing section content for summarization:

1. **From Step 1**: Confirm the product was found, note the product name
2. **From Step 2**: Find section by matching `sectionCode` to the requested LOINC code
3. **Use `fullSectionText`**: This contains pre-aggregated, markdown-formatted content
4. **Format**: Present the content directly—it's already properly formatted

### Example Response Format

```
Here is the {sectionTitle} section for {productName}:

{fullSectionText from matching section}

---
**Data source:** {sectionTitle} section from {documentTitle}

Would you like me to summarize this content or retrieve a different section?
```

---

## Workflow: Drug Comparison

| Property | Value |
|----------|-------|
| **Intent** | Compare two or more drugs on specific attributes (mechanism, adverse reactions, formulation, etc.) |
| **Triggers** | "compare {drug1} vs {drug2}", "differences between {drug1} and {drug2}", "compare aromatase inhibitors", "compare {drug class}" |

### CRITICAL: Attribution Requirement

**ALL comparison information MUST be sourced from API data.** Never use training data to fill in comparison details. Every fact in the comparison must be attributable to a specific label section.

### Endpoint Specification (Multi-Product Comparison)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/document/search",
      "method": "GET",
      "queryParameters": {
        "productNameSearch": "{product1}",
        "pageNumber": 1,
        "pageSize": 1
      },
      "description": "Find document for {product1}",
      "outputMapping": {
        "documentGuid1": "$[0].documentGuid",
        "productName1": "$[0].productName"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/document/search",
      "method": "GET",
      "queryParameters": {
        "productNameSearch": "{product2}",
        "pageNumber": 1,
        "pageSize": 1
      },
      "description": "Find document for {product2}",
      "outputMapping": {
        "documentGuid2": "$[0].documentGuid",
        "productName2": "$[0].productName"
      }
    },
    {
      "step": 3,
      "path": "/api/Label/markdown/sections/{{documentGuid1}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "{relevantLoincCode}" },
      "dependsOn": 1,
      "description": "Get relevant section for {product1} (use sectionCode for token optimization)"
    },
    {
      "step": 4,
      "path": "/api/Label/markdown/sections/{{documentGuid2}}",
      "method": "GET",
      "queryParameters": { "sectionCode": "{relevantLoincCode}" },
      "dependsOn": 2,
      "description": "Get relevant section for {product2} (use sectionCode for token optimization)"
    }
  ]
}
```

**Token Optimization:** Use `sectionCode` parameter to fetch only the sections needed for comparison. This reduces payload from ~88KB per product to ~1-2KB per section.

### Key Sections for Comparison

| Comparison Aspect | LOINC Code | Section Title |
|-------------------|------------|---------------|
| Drug Class/Mechanism | 34089-3 | DESCRIPTION |
| Indications | 34067-9 | INDICATIONS AND USAGE |
| Adverse Reactions | 34084-4 | ADVERSE REACTIONS |
| Contraindications | 34070-3 | CONTRAINDICATIONS |
| Warnings | 43685-7 | WARNINGS AND PRECAUTIONS |
| Drug Interactions | 34073-7 | DRUG INTERACTIONS |
| Dosing | 34068-7 | DOSAGE AND ADMINISTRATION |
| How Supplied | 34069-5 | HOW SUPPLIED/STORAGE AND HANDLING |

### Synthesis Instructions

When synthesizing a drug comparison:

1. **Match sections by `sectionCode`** from both products
2. **Extract relevant text from `fullSectionText`** for each comparison aspect
3. **Create structured comparison tables** using ONLY data from the API responses
4. **Include Data Sources section** with links to the specific sections used

### Example Response Format

```markdown
## {Product1} vs {Product2} Comparison

I found {n} products in the database for this comparison.

### Drug Class & Mechanism

| Product | Drug Class | Description |
|---------|------------|-------------|
| {Product1} | {from 34089-3 fullSectionText} | {mechanism excerpt} |
| {Product2} | {from 34089-3 fullSectionText} | {mechanism excerpt} |

### Adverse Reactions Comparison

#### {Product1}
{Extract from 34084-4 fullSectionText}

#### {Product2}
{Extract from 34084-4 fullSectionText}

### Key Differences

| Aspect | {Product1} | {Product2} |
|--------|------------|------------|
| Mechanism | {from label} | {from label} |
| Formulation | {from label} | {from label} |

---

**View Full Labels:**
• [View Full Label ({Product1})](/api/Label/generate/{DocumentGUID1}/true)
• [View Full Label ({Product2})](/api/Label/generate/{DocumentGUID2}/true)

**Data sources:**
- {Product1} Description (mechanism) - HUMAN PRESCRIPTION DRUG LABEL
- {Product1} Adverse Reactions - HUMAN PRESCRIPTION DRUG LABEL
- {Product2} Description (mechanism) - HUMAN PRESCRIPTION DRUG LABEL
- {Product2} Adverse Reactions - HUMAN PRESCRIPTION DRUG LABEL
```

### What NOT to Include (Training Data Leakage)

- ❌ Adverse reaction frequencies not in the label
- ❌ Mechanism details beyond what's in DESCRIPTION section
- ❌ Comparative efficacy claims not in the label
- ❌ General drug class knowledge not from the specific label
- ❌ Historical approval information unless in documentTitle

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
- Search by UNII code if known: `GET /api/Label/ingredient/advanced?unii={code}`
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

## UNIVERSAL REQUIREMENT: Label Links for Every Product

**Every response that mentions a pharmaceutical product MUST include a clickable link to view the full FDA label.**

**Required Format:**
```
View Full Labels:
• [View Full Label (ProductName)](/api/Label/generate/{DocumentGUID}/true)
```

**Rules:**
1. **Every product = one link**: If you mention 3 products, include 3 label links
2. **Use the ACTUAL product name**: Get `ProductName` from the API response
3. **Use the correct DocumentGUID**: From the API response (e.g., `/api/Label/document/search`, `/api/Label/ingredient/advanced`)
4. **Link format**: `/api/Label/generate/{DocumentGUID}/true`
5. **NEVER use placeholders**: "Prescription Drug", "OTC Drug", "Document #" are FORBIDDEN

---

## Critical Reminders for Data Queries

### Attribution is Mandatory

1. **NEVER use training data for drug information** - ALL facts must come from API responses
2. **Use markdown sections endpoint for summaries/comparisons** - `GET /api/Label/markdown/sections/{documentGuid}`
3. **Include Data Sources section** - Every response should cite the specific endpoints used

### Technical Requirements

4. **ALWAYS try /api/label/section/{table} if views fail** - This is the reliable fallback
5. **Use sectionMenu to discover tables** - `GET /api/label/sectionMenu`
6. **Table names are case-sensitive** - Use exactly: Document, Product, ActiveIngredient, etc.
7. **Pagination is required for large datasets** - Always include pageNumber and pageSize
8. **IDs are encrypted** - Use the encryptedId values returned, not raw database IDs
9. **Check context first** - `GET /api/ai/context` tells you document/product counts
10. **For multi-step AI**: interpret returns endpoint specs; synthesize converts executed results into answers
11. **When global data changes**: use managed cache clear to reduce stale reads (`POST /api/settings/clearmanagedcache`)
12. **ALWAYS include label links**: Every product mentioned MUST have a "View Full Label" link with the actual ProductName

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
| Find products by ingredient | `/api/Label/ingredient/advanced` | `substanceNameSearch` |
| Find products by manufacturer | `/api/Label/labeler/search` | `labelerNameSearch` |
| Search by NDC | `/api/Label/ndc/search` | `productCode` |
| Search by application number | `/api/Label/application-number/search` | `applicationNumber` |
| Search by drug class | `/api/Label/pharmacologic-class/search` | `classNameSearch` |
| Find specific sections | `/api/Label/section/search` | `sectionCode` (LOINC) |
| Get section content for summarization | `/api/Label/markdown/sections/{documentGuid}` | Filter by `sectionCode` |
| Import data | `/api/label/import` | (file upload) |