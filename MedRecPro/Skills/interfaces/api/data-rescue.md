# Data Rescue - API Interface

Maps the **Narrative Text Extraction** capability to API endpoints.

---

## Purpose

Fallback strategy when structured data endpoints return empty results. Searches label prose sections for information stored in narrative form.

---

## When to Use

- Primary structured endpoint returns empty results
- Data exists in narrative text rather than dedicated fields
- Information is embedded in Description or other prose sections

**Common Scenarios**:
- Inactive ingredients listed in Description section (not InactiveIngredient table)
- Physical characteristics in narrative text
- Storage conditions in non-standard sections

---

## Workflow Pattern

### Step 1: Identify Document

Use existing document GUID from failed primary query, or search:

```
GET /api/Label/ingredient/search?substanceNameSearch={name}&pageSize=10
```

### Step 2: Get All Section Content

```
GET /api/Label/markdown/sections/{documentGuid}
```

Omit `sectionCode` to retrieve all sections.

### Step 3: Parse Narrative Text

Search section content for target keywords:
- "inactive ingredients" in Description section (34089-3)
- Storage keywords in non-standard locations
- Physical characteristics in prose

---

## Target Section Codes

| Data Type | Primary Section | Fallback Sections |
|-----------|-----------------|-------------------|
| Inactive Ingredients | InactiveIngredient (structured) | 34089-3 (Description) |
| Storage Conditions | How Supplied/Storage | 34089-3, 42229-5 |
| Physical Characteristics | Product table | 34089-3 |

---

## Example Workflow

**Scenario**: Inactive ingredients not in structured table

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "cephalexin", "pageSize": 1 },
      "outputMapping": { "documentGuid": "documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34089-3" },
      "dependsOn": 1
    }
  ]
}
```

---

## Parsing Instructions

When extracting from narrative:

1. **Locate keyword phrase** - "inactive ingredients:", "excipients include:", "contains the following inactive"
2. **Extract following text** - Typically comma-separated list or bulleted items
3. **Parse to structured format** - Split on delimiters
4. **Attribute source** - Note extraction from section text, not structured field

---

## Response Format

When presenting rescued data:

```json
{
  "response": "Based on the Description section of the label, the inactive ingredients include: ...",
  "dataHighlights": {
    "source": "Description section (LOINC 34089-3)",
    "extractionMethod": "Narrative text parsing"
  },
  "warnings": ["This information was extracted from narrative text, not structured data fields"]
}
```

---

## Limitations

- Data extraction from narrative is less reliable than structured fields
- Formatting may vary across labels
- Always indicate source and extraction method
- Recommend verification against full label when critical
