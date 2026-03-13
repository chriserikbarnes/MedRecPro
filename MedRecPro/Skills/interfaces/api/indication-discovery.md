# Indication Discovery - API Interface

Intelligent search for products indicated for specific medical conditions, diseases, or symptoms.

---

## CRITICAL: Use Indication Search Endpoint for All Condition Queries

**ALWAYS use `/api/Label/indication/search` for condition-based product queries.**

This endpoint handles terminology matching, AI-powered indication matching, and validation automatically. User queries like "high blood pressure", "what helps with depression", or "surgical analgesic" are processed through a 3-stage server-side pipeline.

**WRONG:** Manually searching reference data, chaining multiple API calls, or building UNII lookups client-side

**CORRECT:** `/api/Label/indication/search?query=high blood pressure` (handles everything server-side)

---

## Primary Endpoint (REQUIRED)

### Condition-Based Product Search

**This is the ONLY endpoint you should use for indication/condition queries.**

```
GET /api/Label/indication/search?query={userQuery}&maxProductsPerIndication=25
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | User's natural language condition query (e.g., "high blood pressure", "depression", "surgical analgesic") |
| `maxProductsPerIndication` | int | No | Max products per matched indication (default: 25) |

**What it does:**
1. Keyword pre-filter against curated indication reference data (with synonym expansion)
2. AI semantic matching to identify products genuinely indicated for the condition
3. AI validation against actual FDA label "Indications & Usage" text to eliminate false positives
4. Returns consolidated results with label links

**Response Structure:**
```json
{
  "originalQuery": "high blood pressure",
  "matchedIndications": [
    {
      "unii": "ABC123XYZ",
      "productNames": "LISINOPRIL",
      "relevanceReason": "Indicated for treatment of hypertension",
      "confidence": "high"
    }
  ],
  "productsByIndication": {
    "ABC123XYZ": [
      {
        "productName": "LISINOPRIL",
        "documentGuid": "abc-123-def",
        "unii": "ABC123XYZ",
        "activeIngredient": "Lisinopril",
        "labelerName": "Mylan Pharmaceuticals",
        "indicationSummary": "Treatment of hypertension...",
        "validationReason": "FDA label confirms hypertension indication",
        "validationConfidence": "high"
      }
    ]
  },
  "totalProductCount": 47,
  "labelLinks": {
    "View Full Label (LISINOPRIL)": "/api/Label/original/abc-123-def/true"
  },
  "explanation": "Matched 'high blood pressure' to hypertension-indicated products",
  "suggestedFollowUps": [
    "Tell me about the side effects of LISINOPRIL",
    "What are the contraindications for these medications?"
  ]
}
```

---

## Required JSON Response Format

When the AI selects `indicationDiscovery` skill, return this endpoint specification:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/indication/search",
      "queryParameters": {
        "query": "{user's condition/symptom term}"
      },
      "description": "Search for products indicated for this medical condition"
    }
  ],
  "explanation": "I'll search for all medications indicated for this condition."
}
```

**Examples:**
- User says "what helps with depression" → `"query": "depression"`
- User says "high blood pressure medications" → `"query": "high blood pressure"`
- User says "surgical analgesic" → `"query": "surgical analgesic"`
- User says "what treats seasonal allergies" → `"query": "seasonal allergies"`

---

## Trigger Phrases

Routes to this skill when user asks:
- "What helps with {condition}?"
- "What can be used for {condition}?"
- "Options for {condition}"
- "Treatment for {condition}"
- "Products for {condition}"
- "What treats {condition}?"

Where {condition} is a medical condition, symptom, or therapeutic need (not a specific product name).

**Key Distinction:**
- "What helps with high blood pressure?" → `indicationDiscovery` → `/api/Label/indication/search?query=`
- "What are the beta blockers?" → `pharmacologicClassSearch` → `/api/Label/pharmacologic-class/search?query=`
- "What are the side effects of Lipitor?" → `labelContent` → product-specific label retrieval

---

## Label Link Format (MANDATORY)

**Every response MUST include label links:**

```markdown
### View Full Labels:
- [View Full Label (LISINOPRIL)](/api/Label/original/{GUID1}/true)
- [View Full Label (AMLODIPINE)](/api/Label/original/{GUID2}/true)
```

The endpoint returns pre-built label links in the `labelLinks` field.

---

## Error Handling

### No Matching Products Found

```json
{
  "originalQuery": "xyz condition",
  "matchedIndications": [],
  "productsByIndication": {},
  "totalProductCount": 0,
  "error": "No products found matching the condition query.",
  "suggestedFollowUps": [
    "Try using more specific medical terminology",
    "Search by drug class instead: 'What are the beta blockers?'"
  ]
}
```

---

## Related Documents

- [Label Content](./label-content.md) — Section retrieval after indication search
- [Pharmacologic Class](./pharmacologic-class.md) — Drug class search (alternative discovery path)
- [Data Rescue](./data-rescue.md) — Fallback strategies when primary search returns empty
