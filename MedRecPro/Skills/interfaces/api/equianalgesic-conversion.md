# Equianalgesic Conversion - API Interface

Maps the **Equianalgesic Dose Calculation** capability to API endpoints.

---

## Required Section Codes

| Code | Section | Content |
|------|---------|---------|
| 34068-7 | Dosage and Administration | Main dosing header |
| 42229-5 | SPL Unclassified | Conversion tables (e.g., "Table 1: Conversion Factors") |

**Do Not Use**:
- 34090-1 (Clinical Pharmacology) - Pharmacokinetics, not conversions
- 43685-7 (Warnings) - Warnings, not conversions
- 34067-9 (Indications) - Indications, not conversions

---

## Workflow (6 Steps)

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

### Step 4: Get Unclassified from Source

```
GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=42229-5
```

Contains detailed conversion tables.

### Step 5: Get Dosage from Target

```
GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=34068-7
```

### Step 6: Get Unclassified from Target

```
GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=42229-5
```

---

## Example Workflow

**Query**: "Convert methadone to buprenorphine"

```json
{
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": { "substanceNameSearch": "methadone", "pageSize": 5 },
      "outputMapping": { "sourceDocumentGuids": "documentGUID[]" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/ingredient/advanced",
      "queryParameters": { "substanceNameSearch": "buprenorphine", "pageSize": 5 },
      "outputMapping": { "targetDocumentGuids": "documentGUID[]" }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{sourceDocumentGuids}}",
      "queryParameters": { "sectionCode": "34068-7" },
      "dependsOn": 1
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{sourceDocumentGuids}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 1
    },
    {
      "step": 5,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{targetDocumentGuids}}",
      "queryParameters": { "sectionCode": "34068-7" },
      "dependsOn": 2
    },
    {
      "step": 6,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{targetDocumentGuids}}",
      "queryParameters": { "sectionCode": "42229-5" },
      "dependsOn": 2
    }
  ]
}
```

---

## Array Extraction Requirement

The `[]` suffix is mandatory for multi-product workflows:

| Syntax | Behavior |
|--------|----------|
| `"documentGuid": "documentGUID"` | First value only |
| `"documentGuids": "documentGUID[]"` | All values (correct) |

---

## Data Source Restrictions

- Conversion ratios and formulas must come from FDA label content
- Detailed tables (e.g., methadone's "Table 1") are in section 42229-5
- If label lacks conversion data, state explicitly and provide label links
- Never generate conversion tables from training data

---

## Synthesis Instructions

When presenting conversion information:

1. **Cite FDA label source** - Include product name and document reference
2. **Present tables as found** - Do not modify conversion factors
3. **Note limitations** - If data is incomplete, state what is missing
4. **Provide label links** - For verification of source material
5. **Include clinical context** - Warnings about patient-specific factors from label

---

## Common Opioid Mappings

| Common Name | Substance Search Term |
|-------------|----------------------|
| Morphine | morphine |
| Hydromorphone | hydromorphone |
| Fentanyl | fentanyl |
| Oxycodone | oxycodone |
| Methadone | methadone |
| Buprenorphine | buprenorphine |
| Hydrocodone | hydrocodone |
| Codeine | codeine |
