# Label Content - API Interface

Maps the **Label Content Retrieval** and **Label Document Search** capabilities to API endpoints.

---

## Section Content Retrieval

### Primary Endpoint (Preferred)

```
GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}
```

Returns pre-formatted markdown content.

**Response Fields**:
- `fullSectionText` - Aggregated markdown content
- `sectionCode` - LOINC code
- `sectionTitle` - Human-readable title
- `contentBlockCount` - Quality indicator

**Token Optimization**: Use `sectionCode` parameter to fetch only needed sections (~1-2KB vs ~88KB for all sections).

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
| Drug class | `/api/Label/pharmacologic-class/search?classNameSearch=` |
| Application number | `/api/Label/application-number/search?applicationNumber=` |

---

## Workflow Patterns

### Single Product Query

**Query**: "What are the side effects of Lipitor?"

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "productNameSearch": "Lipitor", "pageSize": 1 },
      "outputMapping": { "documentGuid": "DocumentGUID" }
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

## Summary Query Workflow

For "summarize usage" or "what is" queries:

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "{drug}", "pageSize": 10 },
      "outputMapping": {
        "documentGuid": "documentGUID",
        "productName": "productName",
        "labelerName": "labelerName"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/single/{{documentGuid}}",
      "dependsOn": 1
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34089-3" },
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
