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

---

## Import Status Handling

### Import Issue Response Format

When an import operation fails, times out, or is still processing:

```json
{
  "response": "Import Status Issue Detected\n\nThere's currently an import operation that is either still processing or has failed. Before importing new files, we need to:\n\n1. **Check the current import status** - Use the progress endpoint to see what's happening\n2. **Wait for completion** - If it's still processing, let it finish\n3. **Resolve any errors** - If it failed, we may need to clear the operation\n\n**Next Steps:**\n- Check the import progress using the operation ID from your previous import\n- Once resolved, you can import new SPL ZIP files",
  "dataHighlights": {
    "operationId": "{operationId}",
    "progressEndpoint": "/api/Label/import/progress/{operationId}"
  },
  "suggestedFollowUps": ["Check import progress", "What files can I import?"],
  "warnings": ["Import operation in progress or failed"],
  "isComplete": true
}
```

### Import Success Response Format

When an import completes successfully:

```json
{
  "response": "Import Completed Successfully!\n\nYour SPL files have been imported:\n\n**Import Summary:**\n- Documents Imported: 2\n- Sections Created: 73\n- Ingredients Created: 5\n\n**Imported Document GUIDs:**\n- 348acd7e-41b4-4686-ac49-00c3d85ae5c6\n- fcac2d10-44da-4beb-a588-3612698c4495",
  "dataHighlights": {
    "documentsImported": 2,
    "sectionsCreated": 73,
    "ingredientsCreated": 5,
    "operationId": "b8896a58-35fb-47be-a03d-7e50aeced8d1"
  },
  "dataReferences": {
    "View Label - 348acd7e-41b4-4686-ac49-00c3d85ae5c6": "/api/Label/generate/348acd7e-41b4-4686-ac49-00c3d85ae5c6/true",
    "View Label - fcac2d10-44da-4beb-a588-3612698c4495": "/api/Label/generate/fcac2d10-44da-4beb-a588-3612698c4495/true",
    "Check Import Progress": "/api/Label/import/progress/b8896a58-35fb-47be-a03d-7e50aeced8d1"
  },
  "suggestedFollowUps": ["What products were imported?", "Show me the adverse reactions"],
  "warnings": [],
  "isComplete": true
}
```

### dataReferences Requirements for Import

1. **View Label Links**: For each imported document GUID:
   - Key: `"View Label - {documentGuid}"`
   - Value: `"/api/Label/generate/{documentGuid}/true"`

2. **Import Progress Link**: When operationId is available:
   - Key: `"Check Import Progress"`
   - Value: `"/api/Label/import/progress/{operationId}"`

3. **Links open in new tab**: The frontend renders these as markdown links

### Failed/Incomplete Import Response

```json
{
  "response": "Import Status Issue Detected\n\nThere's currently an import operation that is either still processing or has failed.\n\n**Next Steps:**\n1. Check the current import status using the link below\n2. Wait for completion if still processing\n3. Resolve any errors if the operation failed",
  "dataHighlights": {
    "operationId": "b8896a58-35fb-47be-a03d-7e50aeced8d1",
    "status": "Unknown"
  },
  "dataReferences": {
    "Check Import Progress": "/api/Label/import/progress/b8896a58-35fb-47be-a03d-7e50aeced8d1"
  },
  "suggestedFollowUps": ["Retry the import", "What files can I import?"],
  "warnings": ["Import operation status unknown"],
  "isComplete": true
}
```

---

## Related Documents

- [Synthesis Rules](./synthesis-rules.md) - Content quality and aggregation rules
- [Indication Discovery](./api/indication-discovery.md) - Product search workflows
- [Label Content](./api/label-content.md) - Section retrieval workflows
