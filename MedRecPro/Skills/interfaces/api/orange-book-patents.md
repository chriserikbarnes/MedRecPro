# Orange Book Patent Search - API Interface

Search Orange Book NDA patents by expiration date, brand name, or active ingredient to discover patent expiration timelines and generic drug availability.

---

## CRITICAL: Tool Selection

**Use this skill when the user asks about:**
- Patent expiration dates or timelines
- When a generic version of a drug will be available
- Orange Book patent data
- Upcoming generic drugs

**DO NOT use this skill when:**
- The user asks about drug label content (side effects, dosing, warnings) → use `labelContent`
- The user asks about what helps with a condition → use `indicationDiscovery`
- The user asks about a drug class → use `pharmacologicClassSearch`

---

## Primary Endpoint

### Patent Expiration Search

```
GET /api/OrangeBook/expiring
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tradeName` | string | No | Brand/trade name search. Supports partial matching (e.g., "Ozem" matches "Ozempic"). Use when user mentions brand names like "Ozempic", "Lipitor", "Humira". |
| `ingredient` | string | No | Active ingredient name search. Supports partial matching (e.g., "semaglut" matches "semaglutide"). Use when user mentions generic names like "semaglutide", "atorvastatin". |
| `expiringInMonths` | int | No | Number of months from today to search for expiring patents. Must be > 0. Example: 6 returns patents expiring in the next 6 months. Omit when using tradeName/ingredient for open-ended search (all future patents). |
| `pageNumber` | int | No | Page number, 1-based. Default: 1 |
| `pageSize` | int | No | Results per page (1-200). Default: 10 |

**Constraint:** At least ONE parameter is required: `expiringInMonths`, `tradeName`, or `ingredient`.

---

## Parameter Selection Guide

| User Says | Parameters |
|-----------|-----------|
| "When will generic Ozempic be available?" | `tradeName=Ozempic` (omit expiringInMonths for open-ended search) |
| "What patents expire soon?" | `expiringInMonths=6` |
| "Semaglutide patent expiry" | `ingredient=semaglutide` |
| "Generics available next year" | `expiringInMonths=12` |
| "Lipitor patents expiring in 6 months" | `tradeName=Lipitor&expiringInMonths=6` |

---

## Response Structure

```json
{
  "patents": [
    {
      "orangeBookPatent": {
        "Type": "Patent",
        "Appl_No": "021025",
        "Product_No": "004",
        "Trade_Name": "OZEMPIC",
        "Ingredient": "SEMAGLUTIDE",
        "Strength": "0.25MG/0.5ML; 0.5MG/0.5ML",
        "Patent_No": "8129343",
        "Patent_Expire_Date_Text": "2026-12-05"
      },
      "labelLink": "/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/false"
    }
  ],
  "markdown": "| Type | Application# | ... | Expires |\n|---|---|...",
  "totalCount": 15,
  "totalPages": 2
}
```

**Key Fields:**
- **patents**: Structured patent data with optional `labelLink` when a cross-referenced SPL label exists
- **markdown**: Pre-rendered markdown table — render this directly in the response
- **totalCount**: Total matching patents across all pages
- **totalPages**: Total pages available

---

## Required JSON Response Format

When the AI selects `orangeBookPatents` skill, return this endpoint specification:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/OrangeBook/expiring",
      "queryParameters": {
        "tradeName": "{brand name if provided}",
        "ingredient": "{ingredient if provided}",
        "expiringInMonths": "{months if provided}",
        "pageSize": "25"
      },
      "description": "Search Orange Book patents by expiration date, brand name, or ingredient"
    }
  ],
  "explanation": "I'll search the Orange Book for patent expiration data."
}
```

**Examples:**
- User says "When will generic Ozempic be available?" → `"tradeName": "Ozempic"` (omit expiringInMonths)
- User says "What patents expire in 6 months?" → `"expiringInMonths": "6"` (omit tradeName/ingredient)
- User says "Semaglutide patents" → `"ingredient": "semaglutide"` (omit expiringInMonths)

---

## Fallback Strategy

**Brand vs Generic Name Ambiguity:**

Users may not know whether they are using a brand name or generic name. If `tradeName` search returns ZERO results (empty Patents array):

1. Retry the SAME query using the `ingredient` parameter instead
2. Example: `tradeName=semaglutide` → 0 results → `ingredient=semaglutide` → results found

Conversely, if `ingredient` returns zero results, retry with `tradeName`.

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/OrangeBook/expiring",
      "queryParameters": {
        "tradeName": "{user's term}"
      },
      "description": "Search by trade name first"
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/OrangeBook/expiring",
      "queryParameters": {
        "ingredient": "{user's term}"
      },
      "dependsOn": 1,
      "condition": "step1.patents.length === 0",
      "description": "Fallback: search by ingredient if trade name returned no results"
    }
  ]
}
```

---

## Result Formatting Requirements

### Markdown Table

The response includes a pre-rendered `markdown` field containing a formatted table. **Render this directly** — do not reconstruct the table from the patents array.

### Label Links

When a trade name has an associated FDA label, it appears as a clickable markdown link in the table:
- `[OZEMPIC](/api/Label/original/{DocumentGUID}/false)`
- Not all rows have links — only those with SPL cross-references

**Preserve these links** in your output so users can navigate to the official FDA label.

### Pediatric Exclusivity

Patent rows with pediatric exclusivity are marked with ⚠️ emoji in the Expires column. These represent extended expiration dates beyond the base patent. A legend row is appended when pediatric rows exist.

### Pagination

Include `TotalCount` and `TotalPages` when presenting paginated results:
```markdown
Showing page 1 of 2 (15 total patents)
```

---

## Label Link Format (MANDATORY)

**Every response that includes patent data with label links MUST preserve those links.**

Links in the markdown table use relative URLs:
```markdown
[TRADE_NAME](/api/Label/original/{DocumentGUID}/false)
```

---

## Multi-Step Workflows: Patent Search + Label Content

When a user asks about patents AND label details for the same product:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/OrangeBook/expiring",
      "queryParameters": {
        "tradeName": "{product name}"
      },
      "description": "Search for patent expiration data"
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": {
        "productNameSearch": "{product name}",
        "pageSize": "3"
      },
      "outputMapping": {
        "documentGuids": "DocumentGUID[]"
      },
      "description": "Find label documents for the product"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "dependsOn": 2,
      "description": "Get label sections for the product"
    }
  ],
  "explanation": "I'll find patent expiration data and label details for this product."
}
```

---

## Error Handling

### No Results Found

If no patents match the search criteria, present a clear message:
- "No patents found for {tradeName/ingredient}. This product may not be in the Orange Book, or all patents may have already expired."

### Invalid Parameters

- `expiringInMonths` must be > 0 when provided
- At least one of `expiringInMonths`, `tradeName`, or `ingredient` must be provided

---

## Related Documents

- [Label Content](./label-content.md) - Retrieve label details for products found in patent search
- [Retry Fallback](./retry-fallback.md) - General fallback strategies
