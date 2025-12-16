# Label Section Content Query Skills

This document provides instructions for interpreting user queries about drug label content and generating appropriate API endpoint workflows.

---

## Output Format

Respond with a JSON object in the following format:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "aspirin", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for products containing the ingredient",
      "outputMapping": { "documentGuid": "$[0].documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section content"
    }
  ],
  "explanation": "Brief explanation of the interpretation",
  "requiresAuthentication": true,
  "clarifyingQuestions": ["Question if request is ambiguous"],
  "isDirectResponse": false,
  "directResponse": null
}
```

---

## Multi-Step Workflow Instructions

For queries about label content (side effects, warnings, dosing, etc.), use a 2-step workflow:

**Step 1**: Search to find the documentGUID  
**Step 2**: Use `/api/Label/section/content/{documentGuid}` with appropriate sectionCode

### Dependency Properties

| Property | Description |
|----------|-------------|
| `step` | Execution order (1, 2, 3...). Lower steps execute first. |
| `outputMapping` | Extract values from results. Deep search is enabled - just specify the field name. |
| `dependsOn` | Step number this endpoint depends on. Skipped if dependency fails. |
| `skipIfPreviousHasResults` | Step number to check. This step ONLY runs if that step returned EMPTY results. |
| `{{variableName}}` | Template variable substituted from previous step's outputMapping. |

---

## Section Content Endpoint

**Endpoint:** `GET /api/Label/section/content/{documentGuid}?sectionCode={code}`

### Response Format

Array of section content objects:

```json
[
  {
    "sectionContent": {
      "DocumentGUID": "guid",
      "SectionCode": "34084-4",
      "SectionDisplayName": "ADVERSE REACTIONS",
      "SectionTitle": "6 ADVERSE REACTIONS",
      "ContentText": "The actual section text content...",
      "SequenceNumber": 1,
      "ContentType": "paragraph"
    }
  }
]
```

Results are ordered by SectionCode then SequenceNumber for proper reading order.

---

## LOINC Section Codes

Match user query keywords to the appropriate sectionCode:

| User Query Keywords | sectionCode | Section Name |
|---------------------|-------------|--------------|
| side effects, adverse effects, adverse reactions | 34084-4 | Adverse Reactions |
| warnings, precautions, cautions, safety | 43685-7 | Warnings and Precautions |
| contraindications, should not take, avoid, don't take | 34069-5 | Contraindications |
| dosage, dosing, how to take, administration, dose | 34068-7 | Dosage and Administration |
| interactions, drug interactions, food interactions | 34073-7 | Drug Interactions |
| indications, used for, treats, approved for | 34067-9 | Indications and Usage |
| overdose, overdosage, too much, poisoning | 34088-5 | Overdosage |
| boxed warning, black box, serious warning | 34066-1 | Boxed Warning |
| pharmacology, mechanism, how it works | 34090-1 | Clinical Pharmacology |
| pregnancy, nursing, breastfeeding, lactation | 34076-0 | Use in Specific Populations |
| storage, handling, how to store | 34069-5 | How Supplied/Storage |

---

## Example Workflows

### Side Effects/Adverse Reactions Query (with fallback)

**User:** "What are the side effects of testosterone?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "testosterone", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for testosterone products",
      "outputMapping": { "documentGuid": "$[0].documentGUID", "productName": "$[0].productName" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section (LOINC 34084-4)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1,
      "skipIfPreviousHasResults": 2,
      "description": "FALLBACK: Get unclassified sections if step 2 was empty"
    }
  ],
  "explanation": "Search for testosterone, get adverse reactions section, with fallback to unclassified sections."
}
```

> **IMPORTANT: The step 3 fallback pattern:**
> - `skipIfPreviousHasResults: 2` means step 3 only runs if step 2 returned EMPTY results
> - This handles older labels where adverse effects are in 42229-5 instead of 34084-4
> - ALWAYS include this fallback for adverse reactions queries

---

### Warnings Query (with fallback)

**User:** "What are the warnings for aspirin?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "aspirin", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for aspirin products",
      "outputMapping": { "documentGuid": "$[0].documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "43685-7" },
      "dependsOn": 1,
      "description": "Get warnings and precautions section (LOINC 43685-7)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1,
      "skipIfPreviousHasResults": 2,
      "description": "FALLBACK: Get unclassified sections if step 2 was empty"
    }
  ],
  "explanation": "Retrieving warnings for aspirin with fallback to unclassified sections."
}
```

---

### Drug Interactions Query

**User:** "What drugs interact with Lipitor?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/document/search",
      "queryParameters": { "productNameSearch": "Lipitor", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for Lipitor by product name",
      "outputMapping": { "documentGuid": "$[0].documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34073-7" },
      "dependsOn": 1,
      "description": "Get drug interactions section"
    }
  ],
  "explanation": "Retrieving drug interactions for Lipitor."
}
```

---

### Dosing Query

**User:** "How do I take metformin?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "metformin", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for metformin products",
      "outputMapping": { "documentGuid": "$[0].documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34068-7" },
      "dependsOn": 1,
      "description": "Get dosage and administration section"
    }
  ],
  "explanation": "Retrieving dosing instructions for metformin."
}
```

---

### Multiple Sections (omit sectionCode for all content)

**User:** "Tell me everything about ibuprofen safety"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "ibuprofen", "pageNumber": 1, "pageSize": 10 },
      "outputMapping": { "documentGuid": "$[0].documentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/section/content/{{documentGuid}}",
      "dependsOn": 1,
      "description": "Get ALL section content (no sectionCode filter)"
    }
  ],
  "explanation": "Retrieving complete label content for ibuprofen."
}
```

---

## When to Use Multi-Step vs Single-Step

### USE MULTI-STEP (2+ endpoints with dependency) for:

- Side effects, adverse effects, adverse reactions
- Warnings, precautions, contraindications
- Dosing, dosage, administration instructions
- Drug interactions
- Indications (what the drug treats)
- Any query about reading actual label text content

### USE SINGLE-STEP for:

- List all products by manufacturer
- Search by name, NDC, application number
- Get counts or summaries
- Questions that only need metadata, not label text

---

## Search Endpoint Selection

Choose the appropriate Step 1 search based on user's query:

| User Provides | Use This Search Endpoint |
|---------------|--------------------------|
| Ingredient/substance name (aspirin, testosterone) | `/api/Label/ingredient/search?substanceNameSearch=` |
| Brand/product name (Lipitor, Advil) | `/api/Label/document/search?productNameSearch=` |
| Manufacturer name (Pfizer, Merck) | `/api/Label/labeler/search?labelerNameSearch=` |
| Drug class (beta blocker, SSRI) | `/api/Label/pharmacologic-class/search?classNameSearch=` |
| Application number (NDA, ANDA) | `/api/Label/application-number/search?applicationNumber=` |

---

## Direct Response Handling

If the user is asking a general question that doesn't require API calls, set `isDirectResponse=true` and provide the answer in `directResponse`.