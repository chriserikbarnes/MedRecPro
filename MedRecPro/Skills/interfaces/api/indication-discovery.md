# Indication Discovery - API Interface

Maps the **Condition-Based Product Search** and **Alternative Product Discovery** capabilities to API endpoints.

---

## Reference Data

**File**: `C:\Users\chris\Documents\Repos\MedRecPro\Skills\labelProductIndication.md`

**Format**: Pipe-delimited entries separated by `---`
```
ProductNames|UNII|IndicationsSummary
```

**Usage**: Search `IndicationsSummary` for condition keywords to extract UNII codes for API calls.

---

## Primary Workflow

### Step 1: Match Condition to UNII

Search reference data for condition keywords. Match ALL parts of compound conditions.

| User Query | Required Keywords |
|------------|-------------------|
| "estrogen sensitive cancer" | estrogen + (cancer OR carcinoma OR breast) |
| "seasonal allergies" | allergic rhinitis OR seasonal OR antihistamine |
| "surgical analgesic" | surgical + analgesic OR perioperative |

### Step 2: Get Latest Labels by UNII

```
GET /api/Label/product/latest?unii={UNII}&pageNumber=1&pageSize=5
```

**Output Fields**:
- `ProductName` - Display name
- `ActiveIngredient` - Ingredient list
- `UNII` - Ingredient identifier
- `DocumentGUID` - For section content retrieval

### Step 3: Get Label Content (REQUIRED)

**CRITICAL**: This step is REQUIRED, not optional.

**Recommended Approach - Get ALL Sections:**
```
GET /api/Label/markdown/sections/{DocumentGUID}
```

**Alternative - Specific Section (may 404):**
```
GET /api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9
```

**Why omit sectionCode?**
- Not all labels have all section codes - specific queries may return 404
- Omitting `sectionCode` returns ALL available sections
- More reliable for synthesis - no missing data

**Section Codes (if you need specific sections)**:
- `34067-9` - Indications and Usage
- `34089-3` - Description (drug class)

**Output Fields**:
- `fullSectionText` - Pre-formatted markdown
- `contentBlockCount` - Quality indicator

### Full Label Content Requirement

**Step 3 is REQUIRED for product summarization:**
- Always call `/api/Label/markdown/sections/{DocumentGUID}`
- **RECOMMENDED**: Omit the `sectionCode` parameter to get ALL sections reliably
- Returns ALL sections including Indications, Description, Dosage, Warnings, etc.
- Use this data for product summaries, NOT training data
- The reference file (`labelProductIndication.md`) is for UNII matching only, not final summaries

**CRITICAL - NO TRAINING DATA**:
- Response content MUST come ONLY from API results
- DO NOT generate drug descriptions, mechanisms, or classifications from training data
- List ONLY products returned by the API with their exact `productName` and `activeIngredient` values
- If the API returns 5 products, list exactly those 5 products - no more, no less

### Step 4: Find Related Products (Optional)

```
GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}
```

**Filter Options**:
- `relationshipType=SameActiveIngredient` - Generic equivalents
- `relationshipType=SameApplicationNumber` - Same approval

### Step 5: Build Label Links

**Format**: `/api/Label/original/{DocumentGUID}/true`

Use RELATIVE URLs only. Never include protocol or domain.

---

## Output Mapping

```json
{
  "outputMapping": {
    "documentGuid": "DocumentGUID",
    "productName": "ProductName",
    "activeIngredient": "ActiveIngredient"
  }
}
```

For multi-product queries, use array extraction:
```json
{
  "outputMapping": {
    "documentGuids": "documentGUID[]",
    "productNames": "productName[]"
  }
}
```

---

## Relevance Validation

Before presenting results, validate each product against the original query:

1. Look up product's UNII in reference data
2. Check if `IndicationsSummary` matches query keywords
3. Exclude products that fail validation
4. Do not create `dataReferences` entries for irrelevant products

---

## Truncation Detection

Content is truncated if:
- Text ends with `:` followed by nothing
- `fullSectionText` < 200 characters
- `contentBlockCount` = 1

**Fallback**: Use multi-product workflow to aggregate from multiple labels.

---

## Multi-Product Workflow (Recommended)

**Use full-section retrieval to avoid 404 errors:**

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": {
        "substanceNameSearch": "{ingredientName}",
        "pageNumber": 1,
        "pageSize": 20
      },
      "outputMapping": {
        "documentGuids": "documentGUID[]",
        "productNames": "productName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "description": "Get ALL sections - no sectionCode means all available sections returned",
      "dependsOn": 1
    }
  ]
}
```

**Key Points**:
- Omitting `sectionCode` returns ALL available sections
- Avoids 404 errors from non-existent section codes
- Synthesis phase selects relevant content from complete data

---

## Condition Keyword Mappings

| User Term | Search Keywords |
|-----------|-----------------|
| feeling down, sad | depression, depressive, MDD |
| high blood pressure | hypertension, antihypertensive |
| diabetes, blood sugar | diabetes, glycemic, type 2 |
| anxiety, nervous | anxiety, anxiolytic, panic |
| allergies, sneezing | allergic rhinitis, antihistamine |
| pain, aches | pain, analgesic, arthritis |
| surgical analgesic | surgical, analgesic, perioperative |
| cancer | cancer, carcinoma, malignancy |

---

## Response Requirements

### dataReferences (Required)

```json
{
  "dataReferences": {
    "View Full Label (ProductName)": "/api/Label/original/{DocumentGUID}/true"
  }
}
```

Use ACTUAL product names from API response. Never use placeholders.

### CRITICAL: Label Link Construction

When UNII/ingredient data is available from Step 2:
- **ALWAYS** use the DocumentGUID returned by `/api/Label/product/latest` to build label links
- **DO NOT** use alternative methods (regex extraction, path parsing, or other endpoints)
- The correct label link format is: `/api/Label/original/{DocumentGUID}/true`
- Include the ProductName from the API response in the link display text

### REQUIRED: View Full Labels Section

Your response MUST include a "View Full Labels:" section with clickable links.

For each product returned by `/api/Label/product/latest`:
1. Extract the `productName` and `documentGUID` from the response
2. Create a markdown link using the **ACTUAL product name** from the API response
3. **DO NOT use generic placeholders** like "Prescription Drug" or "OTC Drug"

**Example (CORRECT)**:
```
View Full Labels:
- [View Full Label (Atorvastatin Calcium)](/api/Label/original/48173596-6909-f52b-e063-6294a90a8f22/true)
- [View Full Label (Rosuvastatin)](/api/Label/original/abc12345-1234-5678-9abc-def012345678/true)
```

**Example (WRONG)**:
```
- [View Full Label (Prescription Drug)](/api/Label/original/48173596.../true)
```

### CRITICAL: Relative URLs Only

- Use RELATIVE URLs: `/api/Label/original/{DocumentGUID}/true`
- NEVER include `http://`, `https://`, `localhost`, or any domain
- The frontend will add the correct base URL

### Data Sources

1. **Primary**: `/api/Label/markdown/sections/{DocumentGUID}` API response
2. **Supplemental**: `labelProductIndication.md` reference file
3. **Never**: Training data

---

## Example Workflow

**Query**: "What helps with seasonal allergies?"

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "YO7261ME24", "pageSize": 5 },
      "outputMapping": {
        "documentGuids1": "DocumentGUID[]",
        "productNames1": "ProductName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "E6582LOH6V", "pageSize": 5 },
      "outputMapping": {
        "documentGuids2": "DocumentGUID[]",
        "productNames2": "ProductName[]"
      }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids1}}",
      "description": "Get ALL sections for first UNII products",
      "dependsOn": 1
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids2}}",
      "description": "Get ALL sections for second UNII products",
      "dependsOn": 2
    }
  ]
}
```
