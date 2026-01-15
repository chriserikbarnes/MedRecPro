# Equianalgesic Conversion - API Interface

Maps the **Equianalgesic Dose Calculation** capability to API endpoints.

---

## Required Section Codes

| Code | Section | Content |
|------|---------|---------|
| **34068-7** | **Dosage and Administration** | Main dosing header, titration schedules |
| **42229-5** | **SPL Unclassified** | Conversion tables (e.g., "Table 1: Conversion Factors") |

### Do Not Use These Section Codes

| Code | Section | Why NOT to Use |
|------|---------|----------------|
| 34090-1 | Clinical Pharmacology | Contains pharmacokinetics, NOT conversion tables |
| 43685-7 | Warnings and Precautions | Contains warnings, NOT conversion tables |
| 34067-9 | Indications and Usage | Contains indications, NOT conversion tables |

---

## 6-Step Workflow

For opioid conversion queries, fetch data from BOTH source and target opioid labels, from BOTH section codes.

### Step 1: Search Source Opioid Products

```
GET /api/Label/ingredient/advanced?substanceNameSearch={sourceOpioid}&pageSize=5
```

**Output Mapping**:
```json
{ "sourceDocumentGuids": "documentGUID[]" }
```

### Step 2: Search Target Opioid Products

```
GET /api/Label/ingredient/advanced?substanceNameSearch={targetOpioid}&pageSize=5
```

**Output Mapping**:
```json
{ "targetDocumentGuids": "documentGUID[]" }
```

### Step 3: Get Dosage from Source

```
GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=34068-7
```

Batch expansion fetches from ALL source opioid products.

### Step 4: Get Unclassified from Source

```
GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=42229-5
```

**CRITICAL**: Detailed conversion tables (e.g., "Table 1: Conversion Factors") are typically in this section.

### Step 5: Get Dosage from Target

```
GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=34068-7
```

Batch expansion fetches from ALL target opioid products.

### Step 6: Get Unclassified from Target

```
GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=42229-5
```

---

## Why Both Sections Are Required

FDA labels structure dosing information hierarchically. The main Dosage section (34068-7) contains header text, but **detailed conversion tables are often in subsections classified as "Unclassified" (42229-5)**:

| What You Need | Where It Is | LOINC |
|---------------|-------------|-------|
| Main dosing header | Dosage and Administration | 34068-7 |
| **Detailed conversion tables** | **SPL Unclassified (subsections)** | **42229-5** |
| **"Table 1: Conversion Factors"** | **SPL Unclassified** | **42229-5** |
| **Calculation examples** | **SPL Unclassified** | **42229-5** |
| "Switching from" instructions | Either section | 34068-7 or 42229-5 |

**Example**: Methadone's "Table 1: Conversion Factors to Methadone Hydrochloride Tablets" is in section 42229-5, NOT in 34068-7.

---

## Complete JSON Workflow Example

**Query**: "Convert methadone to buprenorphine"

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": {
        "substanceNameSearch": "methadone",
        "pageNumber": 1,
        "pageSize": 5
      },
      "description": "Search for ALL SOURCE opioid (methadone) products",
      "outputMapping": {
        "sourceDocumentGuids": "documentGUID[]",
        "sourceProductNames": "productName[]"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": {
        "substanceNameSearch": "buprenorphine",
        "pageNumber": 1,
        "pageSize": 5
      },
      "description": "Search for ALL TARGET opioid (buprenorphine) products",
      "outputMapping": {
        "targetDocumentGuids": "documentGUID[]",
        "targetProductNames": "productName[]"
      }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{sourceDocumentGuids}}",
      "queryParameters": { "sectionCode": "34068-7" },
      "dependsOn": 1,
      "description": "Get Dosage and Administration (main header) from ALL SOURCE opioid products"
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{sourceDocumentGuids}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1,
      "description": "Get SPL Unclassified (subsections with detailed conversion tables) from ALL SOURCE opioid products - CRITICAL: 'Table 1: Conversion Factors' is HERE!"
    },
    {
      "step": 5,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{targetDocumentGuids}}",
      "queryParameters": { "sectionCode": "34068-7" },
      "dependsOn": 2,
      "description": "Get Dosage and Administration (main header) from ALL TARGET opioid products"
    },
    {
      "step": 6,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{targetDocumentGuids}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 2,
      "description": "Get SPL Unclassified (subsections with detailed conversion tables) from ALL TARGET opioid products"
    }
  ],
  "explanation": "Fetching BOTH 34068-7 (main Dosage section) AND 42229-5 (subsections with detailed tables) from ALL products for BOTH opioids. Detailed conversion tables like 'Table 1: Conversion Factors to Methadone' are in section 42229-5."
}
```

---

## Array Extraction Requirement

The `[]` suffix is **mandatory** for multi-product workflows:

| Syntax | Behavior | Result |
|--------|----------|--------|
| `"documentGuid": "documentGUID"` | Extracts ONLY the first value | Only 1 product queried - **WRONG** |
| `"documentGuids": "documentGUID[]"` | Extracts ALL values as array | All products queried - **CORRECT** |

**Common Mistake**: Using `"documentGuid": "documentGUID"` (without `[]`) causes the workflow to query only the first product, missing products with complete data.

---

## Where Conversion Data Is Found

| Scenario | Where Conversion Data Is Found | Section |
|----------|-------------------------------|---------|
| Any - Methadone | Methadone label's "Table 1: Conversion Factors" | **42229-5** |
| Morphine - Buprenorphine | Buprenorphine transdermal label's Dosage subsections | 34068-7 or 42229-5 |
| Morphine - Fentanyl | Fentanyl transdermal label's conversion charts | 34068-7 or 42229-5 |
| Morphine - Hydromorphone | Hydromorphone ER label's Dosage section | 34068-7 or 42229-5 |

---

## Common Opioid Mappings

| Common Name | Substance Search Term | UNII |
|-------------|----------------------|------|
| Morphine | morphine | X3P646A2J0 |
| Hydromorphone | hydromorphone | L960UP2KRW |
| Fentanyl | fentanyl | UF599785JZ |
| Fentanyl Citrate | fentanyl citrate | MUN5LYG46H |
| Oxycodone | oxycodone | C1ENJ2TE6C |
| Oxymorphone | oxymorphone | 5Y2EI94NBC |
| Methadone | methadone | 229809935B |
| Buprenorphine | buprenorphine | 40D3SCR4GZ |
| Hydrocodone | hydrocodone | 6YKS4Y3WQ7 |
| Codeine | codeine | Q830PW7520 |

---

## Wrong vs Correct Workflow Examples

### WRONG - Do NOT Do This

```
# WRONG - Single product, wrong section
GET /api/Label/markdown/sections/{singleGUID}?sectionCode=34090-1

# WRONG - Only fetching Indications from one opioid
GET /api/Label/markdown/sections/{singleGUID}?sectionCode=34067-9

# WRONG - Only fetching from one opioid (must fetch BOTH)
GET /api/Label/ingredient/advanced?substanceNameSearch=buprenorphine
```

### CORRECT - Always Do This

```
# CORRECT - Multi-product workflow for BOTH opioids, BOTH sections

Step 1: GET /api/Label/ingredient/advanced?substanceNameSearch={sourceOpioid}&pageSize=5
        outputMapping: { "sourceDocumentGuids": "documentGUID[]" }

Step 2: GET /api/Label/ingredient/advanced?substanceNameSearch={targetOpioid}&pageSize=5
        outputMapping: { "targetDocumentGuids": "documentGUID[]" }

Step 3: GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=34068-7

Step 4: GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=42229-5

Step 5: GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=34068-7

Step 6: GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=42229-5
```

---

## Dependency Properties

| Property | Description |
|----------|-------------|
| `step` | Execution order (1, 2, 3...) |
| `outputMapping` | Extract values from results |
| `dependsOn` | Step number this depends on |
| `{{variableName}}` | Template variable from previous step |

---

## Response Fields

### From `/api/Label/markdown/sections`

| Field | Description |
|-------|-------------|
| `fullSectionText` | Pre-aggregated markdown content |
| `sectionCode` | LOINC code |
| `sectionTitle` | Human-readable title |
| `contentBlockCount` | Quality indicator |
| `documentTitle` | For attribution |

---

## Label Link Format

```
/api/Label/generate/{DocumentGUID}/true
```

Use **RELATIVE URLs only**. The `/true` suffix enables minified XML.

---

## Data Source Restrictions

- Conversion ratios and formulas MUST come from FDA label content
- Detailed tables (e.g., methadone's "Table 1") are in section 42229-5
- If label lacks conversion data, state explicitly and provide label links
- **NEVER** generate conversion tables from training data
