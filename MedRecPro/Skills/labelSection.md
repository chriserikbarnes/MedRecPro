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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section as pre-formatted markdown (token optimized)"
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
**Step 2**: Use `/api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}` (PREFERRED - token optimized, returns pre-formatted markdown)

### Dependency Properties

| Property | Description |
|----------|-------------|
| `step` | Execution order (1, 2, 3...). Lower steps execute first. |
| `outputMapping` | Extract values from results. Deep search is enabled - just specify the field name. |
| `dependsOn` | Step number this endpoint depends on. Skipped if dependency fails. |
| `skipIfPreviousHasResults` | Step number to check. This step ONLY runs if that step returned EMPTY results. |
| `{{variableName}}` | Template variable substituted from previous step's outputMapping. |

---

## Section Content Endpoints

### PREFERRED: Markdown Sections Endpoint (Token Optimized)

**Endpoint:** `GET /api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}`

Returns pre-formatted markdown content ready for AI consumption:

```json
[
  {
    "labelSectionMarkdown": {
      "documentGUID": "guid",
      "sectionCode": "34084-4",
      "sectionTitle": "6 ADVERSE REACTIONS",
      "fullSectionText": "## 6 ADVERSE REACTIONS\r\n\r\nThe actual section text content...",
      "contentBlockCount": 5
    }
  }
]
```

**Key Fields:**
- `fullSectionText` - Pre-aggregated markdown content ready for AI consumption
- `sectionCode` - LOINC code for attribution
- Token optimized: ~1-2KB per section vs ~88KB for all sections

### Legacy: Section Content Endpoint

**Endpoint:** `GET /api/Label/section/content/{documentGuid}?sectionCode={code}`

Returns array of individual content blocks that must be combined:

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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section (LOINC 34084-4)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "43685-7" },
      "dependsOn": 1,
      "description": "Get warnings and precautions section (LOINC 43685-7)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
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
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
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

## First-Line Product Selector

**IMPORTANT**: For product identification queries, use `/api/Label/product/latest` as the **first-line selector**. This endpoint returns the most recent label for each UNII/ProductName combination.

### When to Use First-Line Selector

Use `/api/Label/product/latest` when:
- User provides a product name (Lipitor, aspirin)
- User provides an active ingredient name (atorvastatin)
- User provides a UNII code (R16CO5Y76E)
- You need to identify a product before retrieving section content

### First-Line Selector Endpoint

```
GET /api/Label/product/latest?productNameSearch={name}&pageNumber=1&pageSize=10
GET /api/Label/product/latest?activeIngredientSearch={ingredient}&pageNumber=1&pageSize=10
GET /api/Label/product/latest?unii={uniiCode}&pageNumber=1&pageSize=10
```

**Response Fields**:
- `ProductName`: Proprietary product name
- `ActiveIngredient`: Active ingredient name
- `UNII`: Unique Ingredient Identifier
- `DocumentGUID`: Use this for section content retrieval

### Example: Product Lookup then Section Content

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "productNameSearch": "Lipitor", "pageNumber": 1, "pageSize": 1 },
      "description": "Find Lipitor using first-line product selector",
      "outputMapping": { "documentGuid": "$[0].ProductLatestLabel.DocumentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "dependsOn": 1,
      "description": "Get adverse reactions section"
    }
  ],
  "explanation": "Using first-line selector to find Lipitor, then retrieving adverse reactions."
}
```

### Finding Related Products After First-Line Selection

After identifying a product, use the DocumentGUID to find related products:

```
GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}
GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}&relationshipType=SameActiveIngredient
```

---

## Legacy Search Endpoint Selection

For backwards compatibility and specialized searches, these endpoints remain available:

| User Provides | Use This Search Endpoint |
|---------------|--------------------------|
| Ingredient/substance name (aspirin, testosterone) | `/api/Label/ingredient/search?substanceNameSearch=` |
| Brand/product name (Lipitor, Advil) | `/api/Label/document/search?productNameSearch=` |
| Manufacturer name (Pfizer, Merck) | `/api/Label/labeler/search?labelerNameSearch=` |
| Drug class (beta blocker, SSRI) | `/api/Label/pharmacologic-class/search?classNameSearch=` |
| Application number (NDA, ANDA) | `/api/Label/application-number/search?applicationNumber=` |

**Recommendation**: Prefer `/api/Label/product/latest` for product identification as it returns the most current label.

---

## Direct Response Handling

If the user is asking a general question that doesn't require API calls, set `isDirectResponse=true` and provide the answer in `directResponse`.

---

## Aggregate Queries Across ALL Documents

When users ask about data across the ENTIRE database (e.g., "all side effects", "summarize everything", "list all warnings"), use a multi-document workflow that retrieves content from ALL documents, not just one.

### Key Indicators for Aggregate Queries

- "all", "every", "everything", "entire database", "summarize all"
- "what side effects do you have" (no specific drug mentioned)
- "list all warnings in the system"
- Generic questions without a specific drug/ingredient name

### Multi-Document Workflow Pattern

**User:** "Summarize all side effects in your database"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/label/section/Document",
      "queryParameters": { "pageNumber": 1, "pageSize": 50 },
      "description": "Get all available documents in the database",
      "outputMapping": { "documentGuids": "documentGUID[]" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "34084-4" },
      "description": "Get adverse reactions sections from all documents (LOINC 34084-4)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "skipIfPreviousHasResults": 2,
      "description": "FALLBACK: Get unclassified sections if step 2 was empty"
    }
  ],
  "explanation": "Retrieving adverse reactions from ALL documents in the database."
}
```

### CRITICAL: Array Extraction Syntax

When extracting multiple values from an array response, use `[]` suffix:

| Syntax | Result | Use Case |
|--------|--------|----------|
| `documentGUID` | First matching value only | Single document queries |
| `documentGUID[]` | Array of ALL matching values | Multi-document aggregate queries |

**Example outputMapping:**
```json
{
  "outputMapping": {
    "documentGuids": "documentGUID[]"
  }
}
```

This extracts ALL documentGUID values from the response array, enabling subsequent steps to query each document.

### Template Variable Expansion

When `{{documentGuids}}` contains multiple values, the client should:
1. Make separate API calls for EACH document GUID
2. Aggregate all results for synthesis

### Example: All Warnings in Database

**User:** "Show me all warnings across the database"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/label/section/Document",
      "queryParameters": { "pageNumber": 1, "pageSize": 50 },
      "description": "Get all documents",
      "outputMapping": { "documentGuids": "documentGUID[]" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "43685-7" },
      "description": "Get warnings and precautions from all documents"
    }
  ],
  "explanation": "Retrieving warnings from all documents in the database."
}
```

### Synthesis Instructions for Aggregate Queries

When synthesizing multi-document results:

1. **Count**: Report total number of documents processed
2. **Group**: Organize side effects by product/document
3. **Highlight**: Note common patterns across multiple products
4. **List**: Provide document references for each source

**Example synthesis format:**
```
I found side effects data from {n} drug labels in the database:

**{Product 1 Name}** ({Label Type}):
- Side effect 1
- Side effect 2

**{Product 2 Name}** ({Label Type}):
- Side effect 1
- Side effect 2

**Common side effects across multiple products:**
- {shared side effect}
```

---

## Comprehensive Usage/Summary Queries

When users ask for a **summary**, **overview**, or **usage** of a drug, the workflow must retrieve BOTH metadata AND section content to provide complete, sourced information.

### Key Indicators for Summary Queries

- "summarize", "summary", "overview", "usage", "tell me about", "what is"
- "information about", "details about", "about [drug name]"
- Generic requests for drug information without specifying a particular section

### CRITICAL: Data Sourcing Requirements

**ALL information in the response MUST come from executed API endpoints. The synthesis should:**

1. **Use ONLY data returned from API calls** - Never supplement with general knowledge
2. **Cite sources explicitly** - Reference which endpoint/field provided each fact
3. **Indicate missing data** - If requested information isn't in the API response, state "not available in label"
4. **Prefer label content over metadata** - Label text is the authoritative source

### Comprehensive Summary Workflow Pattern

**User:** "Summarize usage for mirtazapine" or "What is mirtazapine?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/search",
      "queryParameters": { "substanceNameSearch": "mirtazapine", "pageNumber": 1, "pageSize": 10 },
      "description": "Search for mirtazapine products to get document reference and product metadata",
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
      "method": "GET",
      "path": "/api/Label/single/{{documentGuid}}",
      "dependsOn": 1,
      "description": "Get complete document with all metadata (dosage form, route, characteristics, marketing dates)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1,
      "description": "Get Indications and Usage section (LOINC 34067-9) - PRIMARY source for usage"
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34089-3" },
      "dependsOn": 1,
      "description": "Get Description section (LOINC 34089-3) - Contains drug class, mechanism"
    },
    {
      "step": 5,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1,
      "skipIfPreviousHasResults": 3,
      "description": "FALLBACK: Get unclassified sections if Indications section was empty"
    }
  ],
  "explanation": "Comprehensive query: Retrieve product metadata AND label sections for complete summary."
}
```

### Data Source Mapping for Summary Response

| Information | Primary Source | Fallback Source |
|-------------|----------------|-----------------|
| Indication (what it treats) | Section 34067-9 ContentText | Step 1 search results |
| Active Ingredient & Strength | Step 2 complete document → ActiveIngredient | Step 1 search results |
| Dosage Form | Step 2 complete document → Product.dosageForm | N/A |
| Route of Administration | Step 2 complete document → Route | N/A |
| FDA Approval Status | Step 2 complete document → MarketingCategory | Step 1 marketingCategory |
| Approval/Marketing Date | Step 2 complete document → MarketingCategory.startDate | N/A |
| Manufacturer | Step 1 labelerName | Step 2 Organization |
| Drug Class/Mechanism | Section 34089-3 ContentText | Section 34090-1 (Clinical Pharmacology) |
| Application Number | Step 1 applicationNumber | Step 2 Document |

### Synthesis Instructions for Summary Queries

When synthesizing a comprehensive drug summary:

1. **Start with the indication** (from Section 34067-9 ContentText)
2. **Include product details** from complete document:
   - Active ingredient and strength
   - Dosage form (e.g., "Oral tablets")
   - Route of administration
3. **Include regulatory information**:
   - Application number (NDA/ANDA/BLA) from metadata
   - Marketing category
   - Marketing start date (if available) - this IS the approval/availability date
4. **Include manufacturer** from labelerName
5. **Key label information** from section content text

### Example Synthesis for Summary Query

```json
{
  "response": "## Mirtazapine Usage Summary\n\n**Primary Indication:**\nMirtazapine tablets are indicated for the treatment of major depressive disorder (MDD) in adults.\n\n**Product Details:**\n- **Active Ingredient:** Mirtazapine 30 mg\n- **Dosage Form:** Oral tablets\n- **Route:** Oral\n- **Manufacturer:** Bryant Ranch Prepack\n\n**Regulatory Information:**\n- **Application Number:** ANDA078818\n- **Marketing Category:** ANDA (Abbreviated New Drug Application)\n- **Marketing Start Date:** 2006-10-25\n\n**Key Information:**\n[Summary from Indications section ContentText]\n\n**Important Note:** The prescribing information contains additional details about dosing, administration, contraindications, and safety information that should be reviewed before use.",
  "dataHighlights": {
    "productName": "MIRTAZAPINE",
    "activeIngredient": "Mirtazapine 30 mg",
    "indication": "Major Depressive Disorder (MDD)",
    "manufacturer": "Bryant Ranch Prepack"
  },
  "dataReferences": {
    "View Full Label (MIRTAZAPINE)": "/api/Label/generate/{{documentGuid}}/true",
    "Search for mirtazapine products": "/api/Label/ingredient/search?substanceNameSearch=mirtazapine",
    "Get indications and usage section (LOINC 34067-9)": "/api/Label/markdown/sections/{{documentGuid}}?sectionCode=34067-9"
  },
  "suggestedFollowUps": [
    "What are the side effects of mirtazapine?",
    "How should mirtazapine be dosed?",
    "What are the warnings for mirtazapine?",
    "Are there drug interactions with mirtazapine?"
  ],
  "warnings": [],
  "isComplete": true
}
```

### CRITICAL: Avoiding Training Data Augmentation

**DO NOT include in responses:**
- FDA original approval dates that aren't in the marketing start date
- Clinical trial results not present in label sections
- Comparisons to other drugs not mentioned in the label
- Pharmacology details not in the Clinical Pharmacology section
- Any "general knowledge" about the drug

**If data is not available:**
```
"**FDA Approval:** Marketing start date: 2006-10-25 (original approval date not available in label data)"
```

---

## Additional LOINC Codes for Comprehensive Queries

| sectionCode | Section Name | When to Use |
|-------------|--------------|-------------|
| 34089-3 | Description | Drug class, chemical structure, mechanism overview |
| 34090-1 | Clinical Pharmacology | Mechanism of action, pharmacokinetics |
| 34091-9 | Non-Clinical Toxicology | Animal studies, carcinogenicity |
| 42228-7 | Pregnancy | Pregnancy category, teratogenicity |
| 34074-5 | Drug/Laboratory Test Interactions | Lab test interference |
| 34092-7 | Clinical Studies | Efficacy data, study results |
| 51945-4 | Package Label Principal Display Panel | Package labeling |
| 55106-9 | Effective Time | Document effective date |

---

## CRITICAL: Content Adequacy Detection and Multi-Product Fallback

### Problem: Truncated or Incomplete Section Content

Some FDA labels have incomplete section content. For example, a CONTRAINDICATIONS section might return:

```json
{
  "fullSectionText": "## 4 CONTRAINDICATIONS\r\n\r\nGLUMETZA is contraindicated in patients with:",
  "contentBlockCount": 1
}
```

This is **truncated** - the text ends with "patients with:" but lists no actual contraindications.

### Detecting Inadequate Content

During synthesis, evaluate returned section content for these **truncation indicators**:

| Indicator | Pattern | Example |
|-----------|---------|---------|
| Trailing colon | Text ends with `:` followed by nothing | "patients with:", "including:", "such as:" |
| Very short content | `fullSectionText` < 200 characters | Single sentence sections |
| Low block count | `contentBlockCount` = 1 for detailed sections | Contraindications, Warnings should have more |
| Incomplete list | Numbered/bulleted list with only 1-2 items | Expected to have many items |
| Header only | Section contains only a heading, no body | "## 5 WARNINGS AND PRECAUTIONS" alone |

**Truncation Pattern Detection Regex**:
```
(patients with|including|such as|characterized by|conditions|following|contraindicated in):[\s\r\n]*$
```

### Multi-Product Fallback Workflow

When section content may be truncated or when comprehensive data is needed, use a **multi-product workflow** to fetch sections from multiple labels containing the same active ingredient.

**User:** "What are the contraindications for metformin?"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": {
        "substanceNameSearch": "metformin",
        "pageNumber": 1,
        "pageSize": 10
      },
      "description": "Search for multiple metformin products to ensure complete data",
      "outputMapping": {
        "documentGuids": "documentGUID[]",
        "productNames": "productName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": { "sectionCode": "34070-3" },
      "dependsOn": 1,
      "description": "Get contraindications from all found products (batch expansion)"
    }
  ],
  "explanation": "Fetching contraindications from multiple metformin products to ensure complete information."
}
```

### Array Expansion for Batch Section Retrieval

The `documentGuids[]` extraction syntax collects ALL `documentGUID` values from the search results. When `{{documentGuids}}` contains multiple values, the endpoint executor automatically expands to multiple API calls.

**How it works**:
1. Step 1 returns 5 products → extracts `["guid1", "guid2", "guid3", "guid4", "guid5"]`
2. Step 2 expands into 5 separate API calls, one per GUID
3. Results from all 5 calls are aggregated for synthesis

### When to Use Multi-Product Workflow

**ALWAYS use multi-product workflow when**:
- Querying critical safety sections (Contraindications, Warnings, Drug Interactions)
- User asks about a generic ingredient (not a specific brand)
- Previous single-product query returned truncated content
- User explicitly asks for comprehensive information

**Use single-product workflow when**:
- User specifies a particular brand/product name
- User asks about a specific label version
- Query is about product-specific information (NDC, manufacturer)

### Synthesis Instructions for Multi-Product Results

When synthesizing results from multiple product labels:

1. **Identify the most complete response** - Look for highest `contentBlockCount` and longest `fullSectionText`
2. **Cross-reference for consistency** - Compare content across products
3. **Aggregate unique information** - Some products may have additional contraindications
4. **Cite all sources** - List all product names that contributed to the response
5. **Flag truncated sources** - Note which labels had incomplete data

**Example Synthesis Format for Multi-Product Results**:

```markdown
## Metformin Contraindications

Based on **3 FDA product labels**, metformin is contraindicated in patients with:

1. **Severe renal impairment** (eGFR below 30 mL/min/1.73 m²)
2. **Hypersensitivity** to metformin hydrochloride or any component
3. **Acute or chronic metabolic acidosis**, including diabetic ketoacidosis

**Source Labels:**
- GLUMETZA (metformin HCl extended-release) - Partial data*
- GLUCOPHAGE (metformin HCl) - Complete data
- Metformin Hydrochloride Tablets - Complete data

*Note: Some labels had truncated content; complete information aggregated from multiple sources.

**View Full Labels:**
• [View Full Label (GLUCOPHAGE)](/api/Label/generate/{guid1}/true)
• [View Full Label (Metformin Hydrochloride Tablets)](/api/Label/generate/{guid2}/true)
```

### CRITICAL: Quality Assessment During Synthesis

Before presenting section content to the user, **always assess quality**:

| Quality Level | Indicators | Action |
|---------------|------------|--------|
| **High** | > 500 chars, contentBlockCount >= 3, complete sentences | Present confidently |
| **Medium** | 200-500 chars, contentBlockCount = 2, no truncation patterns | Present with source attribution |
| **Low** | < 200 chars, contentBlockCount = 1, truncation patterns | Aggregate from multiple sources or note limitation |
| **Unusable** | Header only, ends with colon, < 50 chars | Exclude from response, note data unavailable |

---