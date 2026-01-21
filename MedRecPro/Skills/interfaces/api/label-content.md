# Label Content - API Interface

Maps the **Label Content Retrieval** and **Label Document Search** capabilities to API endpoints.

---

## CRITICAL: Label Links Are MANDATORY

**Every response that retrieves product data MUST include label links.**

### Required Response Elements

```markdown
### View Full Labels:
- [View Full Label ({ProductName})](/api/Label/generate/{DocumentGUID}/true)
```

```json
"dataReferences": {
  "View Full Label ({ProductName})": "/api/Label/generate/{DocumentGUID}/true"
}
```

**If you retrieved product data, you MUST provide label links. No exceptions.**

---

## Section Content Retrieval

### Primary Endpoint: Get ALL Sections (Recommended)

```
GET /api/Label/markdown/sections/{documentGuid}
```

**Omit the `sectionCode` parameter to retrieve ALL available sections.** This is the **recommended approach** because:
- Not all labels have all section codes - specific section queries may return 404
- Returns all available content in a single API call
- Avoids the need to iterate through multiple LOINC codes

**Response**: Returns an array of all available sections with their content.

### Alternative: Get Specific Section

```
GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}
```

**CAUTION**: This may return 404 if the label doesn't have that section. Use the full-section approach above for reliability.

### Response Fields

- `fullSectionText` - Aggregated markdown content
- `sectionCode` - LOINC code
- `sectionTitle` - Human-readable title
- `contentBlockCount` - Quality indicator

**Token Optimization**: For narrow queries where you know the section exists, use `sectionCode` (~1-2KB vs ~88KB). For general queries, omit it to get all sections reliably.

### Legacy Endpoint

```
GET /api/Label/section/content/{documentGuid}?sectionCode={code}
```

Returns individual content blocks requiring combination.

---

## LOINC Section Codes

| Query Keywords | sectionCode | Section Name |
|----------------|-------------|--------------|
| side effects, adverse | 34084-4 | Adverse Reactions |
| warnings, precautions | 43685-7 | Warnings and Precautions |
| contraindications, avoid | 34069-5 | Contraindications |
| dosage, how to take | 34068-7 | Dosage and Administration |
| interactions | 34073-7 | Drug Interactions |
| indications, used for | 34067-9 | Indications and Usage |
| overdose | 34088-5 | Overdosage |
| boxed warning, black box | 34066-1 | Boxed Warning |
| pharmacology, mechanism | 34090-1 | Clinical Pharmacology |
| pregnancy, nursing | 34076-0 | Use in Specific Populations |

---

## Document Search Endpoints

### First-Line Selector (Preferred)

```
GET /api/Label/product/latest?productNameSearch={name}&pageNumber=1&pageSize=10
GET /api/Label/product/latest?activeIngredientSearch={ingredient}&pageNumber=1&pageSize=10
GET /api/Label/product/latest?unii={uniiCode}&pageNumber=1&pageSize=10
```

Returns most recent label for each product.

### Legacy Search Endpoints

| Search Type | Endpoint |
|-------------|----------|
| Ingredient name | `/api/Label/ingredient/search?substanceNameSearch=` |
| Product name | `/api/Label/document/search?productNameSearch=` |
| Manufacturer | `/api/Label/labeler/search?labelerNameSearch=` |
| Drug class | **Use `/api/Label/pharmacologic-class/search?query=` instead** (handles terminology matching) |
| Application number | `/api/Label/application-number/search?applicationNumber=` |

---

## Workflow Patterns

### Single Product Query (Recommended: Get All Sections)

**Query**: "What should I know about buprenorphine?"

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "activeIngredientSearch": "buprenorphine", "pageSize": 10 },
      "outputMapping": {
        "documentGuids": "DocumentGUID[]",
        "productNames": "ProductName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "description": "Get ALL sections (no sectionCode = returns everything)",
      "dependsOn": 1
    }
  ]
}
```

**Key Points**:
- Use `DocumentGUID[]` (with `[]`) to extract ALL document GUIDs as an array
- Omit `sectionCode` to get ALL available sections - avoids 404 errors
- Step 2 will expand to N API calls, one per document

### Single Product Query (Specific Section)

**Query**: "What are the side effects of Lipitor?"

Use this pattern ONLY when you need a specific section and expect it to exist.

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "productNameSearch": "Lipitor", "pageSize": 1 },
      "outputMapping": {
        "documentGuid": "DocumentGUID",
        "productName": "ProductName"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1
    }
  ]
}
```

### With Fallback

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "aspirin", "pageSize": 10 },
      "outputMapping": { "documentGuid": "documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1,
      "skipIfPreviousHasResults": 2
    }
  ]
}
```

---

## Dependency Properties

| Property | Description |
|----------|-------------|
| `step` | Execution order (1, 2, 3...) |
| `outputMapping` | Extract values from results |
| `dependsOn` | Step number this depends on |
| `skipIfPreviousHasResults` | Only run if specified step was empty |
| `{{variableName}}` | Template variable from previous step |

---

## Multi-Document Queries

For aggregate queries ("all side effects", "summarize everything"):

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/label/section/Document",
      "queryParameters": { "pageNumber": 1, "pageSize": 50 },
      "outputMapping": { "documentGuids": "documentGUID[]" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "34084-4" }
    }
  ]
}
```

Use `[]` suffix for array extraction.

---

## Summary Query Workflow (Recommended)

For "summarize usage", "what is", or general product queries - **use full section retrieval**:

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "activeIngredientSearch": "{drug}", "pageSize": 10 },
      "description": "Search for products by ingredient",
      "outputMapping": {
        "documentGuids": "DocumentGUID[]",
        "productNames": "ProductName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "description": "Get ALL sections - no sectionCode parameter means all sections returned",
      "dependsOn": 1
    }
  ]
}
```

**Why this is better**:
- Omitting `sectionCode` returns ALL available sections
- Avoids 404 errors from non-existent section codes
- Single Step 2 expands to N calls (one per document) automatically
- Synthesis can select relevant sections from the complete data

### Alternative: Specific Sections (Use with Caution)

Only use specific section codes when you're certain the label contains that section:

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "{drug}", "pageSize": 10 },
      "outputMapping": {
        "documentGuids": "documentGUID[]",
        "productNames": "productName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "description": "Indications - may 404 if not present",
      "dependsOn": 1
    }
  ]
}
```

---

## Label Link Format

```
/api/Label/generate/{DocumentGUID}/true
```

Use RELATIVE URLs only. The `/true` suffix enables minified XML.

---

## Content Quality Assessment

| Level | Indicators | Action |
|-------|------------|--------|
| High | > 500 chars, blockCount >= 3 | Present confidently |
| Medium | 200-500 chars, blockCount = 2 | Present with attribution |
| Low | < 200 chars, blockCount = 1 | Aggregate from multiple sources |
| Unusable | Header only, ends with colon | Note data unavailable |
