# Response Format Standards

Defines output requirements for all skill workflows.

---

## JSON Response Structure

```json
{
  "response": "Markdown-formatted response text",
  "dataHighlights": {
    "key": "value"
  },
  "dataReferences": {
    "View Full Label (ProductName)": "/api/Label/generate/{GUID}/true"
  },
  "suggestedFollowUps": [
    "Follow-up question 1",
    "Follow-up question 2"
  ],
  "warnings": [],
  "isComplete": true
}
```

---

## Label Link Requirements

**Every product mentioned must have a label link.**

### Format

```
/api/Label/generate/{DocumentGUID}/true
```

### Rules

1. Use RELATIVE URLs only - never include protocol or domain
2. Use ACTUAL product name from API response
3. Never use placeholders ("Prescription Drug", "OTC Drug")
4. Get DocumentGUID from `/api/Label/product/latest` or section endpoints

### Validation Checklist

Before responding:
- Does every label link contain an actual product name?
- Are there zero placeholder names?
- Did each ProductName come from the API response?
- If ProductName unavailable, is the link omitted?

---

## Data Source Rules

### Allowed Sources

1. **Primary**: API response data
2. **Supplemental**: Reference files (labelProductIndication.md)
3. **Metadata**: Structured fields from search results

### Prohibited Sources

- Training data for drug descriptions
- Generated mechanisms of action
- Comparative statements not in labels
- Clinical details not from API

### Missing Data Handling

If data unavailable:
```
"**FDA Approval:** Marketing start date: 2006-10-25 (original approval date not available in label data)"
```

---

## Array Processing

When API returns multiple items:

1. Process the entire array
2. Extract identifiers from each item
3. Create label link for each relevant product
4. Group by dosage form when helpful

---

## URL Standards

| Correct | Incorrect |
|---------|-----------|
| `/api/Label/generate/{GUID}/true` | `http://localhost:5001/api/...` |
| `/api/Label/product/latest?unii=...` | `https://medrecpro.com/api/...` |

Never include: `http://`, `https://`, `localhost`, domain names

---

## Workflow Response Format

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/...",
      "queryParameters": {},
      "description": "Step description",
      "outputMapping": {}
    }
  ],
  "explanation": "Brief explanation"
}
```

---

## Selector Response Format

```json
{
  "success": true,
  "selectedSkills": ["skill1", "skill2"],
  "explanation": "Selection rationale",
  "isDirectResponse": false
}
```

### Direct Response

```json
{
  "success": true,
  "selectedSkills": [],
  "isDirectResponse": true,
  "directResponse": "Answer text",
  "explanation": "Query answered directly"
}
```
