# Retry Fallback - API Interface

Provides alternative endpoints when primary API calls fail. Use this interface for re-interpreting failed requests.

---

## Fallback Mapping

When primary endpoints fail, use these alternatives:

| Failed Endpoint | Try Instead | Notes |
|-----------------|-------------|-------|
| `/api/Label/section/InactiveIngredient` | `/api/Label/markdown/sections/{guid}?sectionCode=34089-3` | Description section often contains inactive ingredients |
| `/api/Label/section/{sectionType}` | `/api/Label/markdown/sections/{guid}` | Fetch all sections, then filter |
| `/api/Label/product/latest` (no results) | `/api/Label/ingredient/search` | Search by ingredient name instead |
| `/api/Label/ingredient/search` (no results) | `/api/Label/document/search` | Broader document search |
| `/api/Label/ingredient/*` | `/api/label/section/ActiveIngredient` or `/api/label/section/InactiveIngredient` | Direct table access |
| `/api/Label/document/*` | `/api/label/section/Document` | Direct table access |
| `/api/Label/labeler/*` | `/api/label/section/Organization` | Direct table access |
| `/api/Label/pharmacologic-class/*` | `/api/label/section/PharmacologicClass` | Direct table access |
| Any search endpoint | Same section with no filter + pagination | Get all records, filter client-side |

---

## Critical Fallback Rules

1. **Structured to narrative fallback**: If structured field returns empty, search narrative text
2. **Section fallback**: If section endpoint returns 404, try SPL Unclassified (42229-5)
3. **Name fallback**: If brand name fails, try active ingredient name
4. **Preserve identifiers**: Always preserve DocumentGUID from successful earlier steps
5. **Pagination required**: Include `pageNumber=1` and `pageSize=50` for section queries

---

## Common Alternatives Table

| Failed Endpoint | Alternative Endpoint |
|-----------------|---------------------|
| `/api/Label/ingredient/*` | `/api/label/section/ActiveIngredient` |
| `/api/Label/ingredient/*` | `/api/label/section/InactiveIngredient` |
| `/api/Label/document/*` | `/api/label/section/Document` |
| `/api/Label/labeler/*` | `/api/label/section/Organization` |
| `/api/Label/pharmacologic-class/*` | `/api/label/section/PharmacologicClass` |

**Note**: Table names are case-sensitive: `Document`, `Product`, `ActiveIngredient`, `InactiveIngredient`, `Organization`

---

## Section Code Fallbacks

If a specific LOINC section code returns 404:

| Primary Section | Fallback Section | LOINC Code |
|-----------------|-----------------|------------|
| Inactive Ingredients (structured) | Description | 34089-3 |
| Specific section endpoint | All sections | (no sectionCode) |
| Dosage and Administration | SPL Unclassified | 42229-5 |
| Any section | SPL Unclassified | 42229-5 |

---

## Output Format

Respond with JSON containing DIFFERENT endpoints than the failed ones:

```json
{
  "success": true,
  "endpoints": [
    {
      "method": "GET",
      "path": "/api/label/section/{table}",
      "queryParameters": {
        "pageNumber": "1",
        "pageSize": "50"
      },
      "description": "Alternative approach using direct table access"
    }
  ],
  "explanation": "Trying alternative endpoint because the view was not available",
  "isDirectResponse": false
}
```

---

## No Alternatives Response

If NO alternatives exist, provide a direct response:

```json
{
  "success": true,
  "endpoints": [],
  "isDirectResponse": true,
  "directResponse": "The requested data could not be found using any available endpoint. The specific section or data may not exist in the imported SPL documents.",
  "explanation": "All fallback options exhausted"
}
```

---

## Workflow Integration

This interface supplements the `dataRescue` skill. Common integration pattern:

1. Primary skill (e.g., `labelContent`) makes initial request
2. Request returns 404 or empty results
3. Load `dataRescue` skill with this retry-fallback interface
4. Execute fallback endpoints
5. If fallback succeeds, synthesize results
6. If fallback fails, provide direct response explaining data unavailability

---

## Related Documents

- [Data Rescue](./data-rescue.md) - Rescue workflow skill
- [Label Content](./label-content.md) - Primary label endpoints
- [Synthesis Rules](../synthesis-rules.md) - Response formatting
