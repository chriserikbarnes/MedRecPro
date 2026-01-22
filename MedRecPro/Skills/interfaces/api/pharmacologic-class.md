# Pharmacologic Class Search - API Interface

Intelligent search for products by therapeutic or pharmacologic class with context-aware terminology matching.

---

## CRITICAL: Use Label Endpoint for All Class Queries

**ALWAYS use `/api/Label/pharmacologic-class/search` for pharmacologic class queries.**

This endpoint handles vocabulary matching automatically when the `query` parameter is used. User queries like "beta blockers", "ACE inhibitors", or "aminoglycosides" do NOT match database class names directly.

**WRONG:** `/api/Label/pharmacologic-class/search?classNameSearch=beta blockers` (will fail - requires exact database class names)

**CORRECT:** `/api/Label/pharmacologic-class/search?query=beta blockers` (handles terminology matching)

---

## Primary Endpoint (REQUIRED)

### Intelligent Class Search

**This is the ONLY endpoint you should use for pharmacologic class queries.**

```
GET /api/Label/pharmacologic-class/search?query={userQuery}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `query` | string | Yes | User's natural language query (e.g., "beta blockers", "aminoglycosides", "ACE inhibitors") |
| `maxProductsPerClass` | int | No | Max products per matched class (default: 500) |

**What it does:**
1. Gets all pharmacologic classes from database (cached)
2. Uses AI to match user query to actual database class names
3. Searches each matched class for products
4. Returns consolidated results with label links

**Response Structure:**
```json
{
  "success": true,
  "originalQuery": "beta blockers",
  "matchedClasses": ["Beta-Adrenergic Blockers [EPC]"],
  "productsByClass": {
    "Beta-Adrenergic Blockers [EPC]": [
      {
        "productName": "METOPROLOL TARTRATE",
        "documentGuid": "abc-123-def",
        "pharmClassName": "Beta-Adrenergic Blockers [EPC]",
        "activeIngredient": "Metoprolol Tartrate",
        "labelerName": "Mylan Pharmaceuticals"
      }
    ]
  },
  "totalProductCount": 47,
  "labelLinks": {
    "View Full Label (METOPROLOL TARTRATE)": "/api/Label/original/abc-123-def/true"
  },
  "explanation": "Matched 'beta blockers' to Beta-Adrenergic Blockers [EPC]",
  "suggestedFollowUps": [
    "Tell me about the side effects of METOPROLOL TARTRATE",
    "What are the contraindications for these medications?"
  ]
}
```

---

## Required JSON Response Format

When the AI selects `pharmacologicClassSearch` skill, return this endpoint specification:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/pharmacologic-class/search",
      "queryParameters": {
        "query": "{user's drug class term}"
      },
      "description": "Search for products by pharmacologic class with intelligent terminology matching"
    }
  ],
  "explanation": "I'll search for all medications in this drug class."
}
```

**Examples:**
- User says "beta blockers" → `"query": "beta blockers"`
- User says "aminoglycosides" → `"query": "aminoglycosides"`
- User says "ACE inhibitors" → `"query": "ACE inhibitors"`
- User says "what antibiotics are available" → `"query": "antibiotics"`

---

## Browse All Classes Endpoint

Lists all available pharmacologic classes with product counts.

```
GET /api/Label/pharmacologic-class/summaries
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `useAiCache` | bool | No | Use AI service's cached summaries (default: false) |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Use case:** Browse available drug categories before searching, or provide suggestions when no match is found.

---

## Common Class Terminology Mappings

The endpoint handles these mappings automatically when using the `query` parameter:

| User Query | Database Class Name |
|------------|---------------------|
| "beta blockers" | Beta-Adrenergic Blockers [EPC] |
| "ACE inhibitors" | Angiotensin Converting Enzyme Inhibitors [EPC] |
| "calcium channel blockers" | Calcium Channel Blockers [EPC] |
| "SSRIs" | Selective Serotonin Reuptake Inhibitors [EPC] |
| "statins" | HMG-CoA Reductase Inhibitors [EPC] |
| "proton pump inhibitors" | Proton Pump Inhibitors [EPC] |
| "opioids" | Opioid Agonists [EPC] |
| "benzodiazepines" | Benzodiazepines [EPC] |
| "NSAIDs" | Non-steroidal Anti-inflammatory Drugs [EPC] |
| "anticoagulants" | Anticoagulants [EPC] |
| "diuretics" | Diuretics [EPC] |
| "aminoglycosides" | Aminoglycosides [Chemical/Ingredient] |
| "antibiotics" | Various antibiotic classes |

**Note:** You do NOT need to translate these yourself. The `/api/Label/pharmacologic-class/search?query=` endpoint handles all terminology matching.

---

## Trigger Phrases

Routes to this skill when user asks:
- "What medications are {class}?"
- "List {class} drugs"
- "Show me all {class}"
- "Find {class} products"
- "Which drugs are {class}?"
- "What {class} are available?"

Where {class} is a drug class name (not a medical condition).

**Key Distinction:**
- "What are the beta blockers?" → `pharmacologicClassSearch` → `/api/Label/pharmacologic-class/search?query=`
- "What helps with high blood pressure?" → `indicationDiscovery`

---

## Label Link Format (MANDATORY)

**Every response MUST include label links:**

```markdown
### View Full Labels:
- [View Full Label (METOPROLOL TARTRATE)](/api/Label/original/{GUID1}/true)
- [View Full Label (ATENOLOL)](/api/Label/original/{GUID2}/true)
```

The endpoint returns pre-built label links in the `labelLinks` field.

---

## Error Handling

### No Matching Classes Found

```json
{
  "success": false,
  "originalQuery": "xyz blockers",
  "matchedClasses": [],
  "error": "No pharmacologic classes matched your query.",
  "suggestedFollowUps": [
    "Try using more specific class terminology",
    "Browse available classes with 'show pharmacologic classes'"
  ]
}
```

---

## Legacy Direct Database Search

For cases requiring exact class name matching without AI terminology translation:

```
GET /api/Label/pharmacologic-class/search?classNameSearch={exactClassName}&pageNumber={n}&pageSize={n}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `classNameSearch` | string | Yes | Exact pharmacologic class name from database |
| `pageNumber` | int | No | 1-based page number |
| `pageSize` | int | No | Records per page |

**Note:** This requires exact database class names like "Beta-Adrenergic Blockers [EPC]" and is typically only used for internal operations or when you already know the exact class name.

---

## Multi-Step Workflows: Class Search + Section Retrieval

When a user asks about a specific label section (warnings, side effects, etc.) for a drug class, use a multi-step workflow that:
1. Searches for all products in the class
2. Iterates over EACH product to retrieve the requested section
3. Synthesizes a generalized summary from ALL product labels

### CRITICAL: Array Extraction for Multi-Product Workflows

The pharmacologic class search returns products in a **nested structure**:

```json
{
  "productsByClass": {
    "ClassName [EPC]": [
      { "documentGuid": "abc", "productName": "Product1" },
      { "documentGuid": "def", "productName": "Product2" }
    ]
  }
}
```

**Use the `[]` suffix** in `outputMapping` to extract ALL values from this nested structure:

```json
"outputMapping": {
  "documentGuids": "documentGuid[]",
  "productNames": "productName[]"
}
```

The `[]` suffix tells the executor to:
1. Find all arrays in the nested response (regardless of nesting level)
2. Extract the specified field from EACH array element
3. Deduplicate the results

### Class Warnings/Precautions Workflow

**Query:** "What are the warnings for GLP-1 products?" or "Tell me about beta blocker side effects"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/pharmacologic-class/search",
      "queryParameters": {
        "query": "{drug class term}"
      },
      "outputMapping": {
        "documentGuids": "documentGuid[]",
        "productNames": "productName[]"
      },
      "description": "Search for all products in this drug class"
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuids}}",
      "queryParameters": {
        "sectionCode": "43685-7"
      },
      "dependsOn": 1,
      "description": "Get Warnings and Precautions for EACH product",
      "fallbackOnError": {
        "httpStatus": [404],
        "action": "retry_without_param",
        "removeParams": ["sectionCode"],
        "description": "If section not found, get ALL sections"
      }
    }
  ],
  "explanation": "I'll search for all products in this class and retrieve warnings from each label."
}
```

### Section Codes for Class-Level Queries

| Query Type | sectionCode | Section Name |
|------------|-------------|--------------|
| warnings, precautions | 43685-7 | Warnings and Precautions |
| side effects, adverse | 34084-4 | Adverse Reactions |
| interactions | 34073-7 | Drug Interactions |
| contraindications | 34069-5 | Contraindications |
| black box, boxed warning | 34066-1 | Boxed Warning |
| dosing, administration | 34068-7 | Dosage and Administration |
| indications, used for | 34067-9 | Indications and Usage |

### Fallback Pattern (CRITICAL)

If the specific section returns 404, **ALWAYS fall back to retrieving ALL sections**:

```json
"fallbackOnError": {
  "httpStatus": [404],
  "action": "retry_without_param",
  "removeParams": ["sectionCode"],
  "description": "If section not found, get ALL sections and extract relevant content"
}
```

**Why this matters:**
- Not all labels have all LOINC section codes
- Some labels may have the information under different sections
- By getting ALL sections, the synthesis phase can find and extract relevant content

### Synthesis Guidelines for Class-Level Queries

When synthesizing results from multiple product labels:

1. **Identify common warnings** across ALL products in the class
2. **Note product-specific differences** where they exist
3. **Include label links** for EVERY product in the dataReferences
4. **Group warnings by severity/category** (Critical, Additional, etc.)
5. **List all products in the class** with their active ingredients

**Example synthesis structure:**

```markdown
## Warnings for {Class Name} Products

Based on FDA-approved labeling for {N} products in this class...

### Critical Warnings
1. **Warning Topic** - Description common to ALL products in class
   - Product-specific note if any differ

### Additional Warnings
...

### Products in Database
| Product | Active Ingredient |
|---------|-------------------|
| Product1 | ingredient1 |
| Product2 | ingredient2 |

### View Full Labels:
- [View Full Label (Product1)](/api/Label/original/{guid1}/true)
- [View Full Label (Product2)](/api/Label/original/{guid2}/true)

### Data Sources:
- [Search for {class} products](/api/Label/pharmacologic-class/search?query={class})
- [Get Warnings sections for {class} products](/api/Label/markdown/sections/{guid}?sectionCode=43685-7)
```

### Key Points for Multi-Product Synthesis

1. **Never generalize from a single product** - iterate over ALL products in the class
2. **Preserve product attribution** - note which products have which warnings
3. **Use database data only** - never use training data for medical information
4. **Always include label links** - mandatory for every product retrieved

---

## Related Documents

- [Label Content](./label-content.md) - Section retrieval after class search
- [Indication Discovery](./indication-discovery.md) - Alternative product discovery (condition-based)
- [Retry Fallback](./retry-fallback.md) - Fallback strategies
