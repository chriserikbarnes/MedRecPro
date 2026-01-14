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

### Step 3: Get Label Content (Required)

```
GET /api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9
```

**Section Codes**:
- `34067-9` - Indications and Usage
- `34089-3` - Description (drug class)

**Output Fields**:
- `fullSectionText` - Pre-formatted markdown
- `contentBlockCount` - Quality indicator

### Step 4: Find Related Products (Optional)

```
GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}
```

**Filter Options**:
- `relationshipType=SameActiveIngredient` - Generic equivalents
- `relationshipType=SameApplicationNumber` - Same approval

### Step 5: Build Label Links

**Format**: `/api/Label/generate/{DocumentGUID}/true`

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

## Multi-Product Workflow

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
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1
    }
  ]
}
```

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
    "View Full Label (ProductName)": "/api/Label/generate/{DocumentGUID}/true"
  }
}
```

Use ACTUAL product names from API response. Never use placeholders.

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
      "outputMapping": { "documentGuid1": "DocumentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "E6582LOH6V", "pageSize": 5 },
      "outputMapping": { "documentGuid2": "DocumentGUID" }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid1}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid2}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 2
    }
  ]
}
```
