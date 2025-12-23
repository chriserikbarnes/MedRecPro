# Rescue Workflow Skills

This document provides rescue strategies when standard label queries fail to find requested information in expected locations. Use these workflows when the primary search returns empty or incomplete results, but the data may exist elsewhere in the document.

---

## Table of Contents

1. [When to Use Rescue Workflows](#when-to-use-rescue-workflows)
2. [Rescue Workflow: Inactive Ingredients](#rescue-workflow-inactive-ingredients)
3. [Rescue Workflow: Generic Text Search](#rescue-workflow-generic-text-search)
4. [Text Analysis Patterns](#text-analysis-patterns)

---

## When to Use Rescue Workflows

Trigger a rescue workflow when:

1. **Primary endpoint returns empty** - The expected section/table has no data for the requested information
2. **Data not in expected location** - Information like inactive ingredients may be embedded in narrative text (e.g., Description section) rather than structured fields
3. **User specifically requests information** - The user has explicitly asked for data that should exist but wasn't found via standard queries

### Decision Process

```
User Request → Primary Query → Results?
                                 ├─ Yes → Return results
                                 └─ No → Rescue Workflow
                                           ├─ Search by ingredient/product name
                                           ├─ Get all sections for document
                                           ├─ Scan section text for target keywords
                                           └─ Extract data from narrative text
```

---

## Rescue Workflow: Inactive Ingredients

| Property | Value |
|----------|-------|
| **Intent** | Find inactive ingredients when not available in structured InactiveIngredient table |
| **Triggers** | "inactive ingredients for {product}", "excipients in {product}", "what are the inactive ingredients" |
| **Primary Failure** | `/api/label/section/InactiveIngredient` returns empty or no results for the product |

### Rescue Strategy

When inactive ingredients are not found in the dedicated table, they are often listed in the **DESCRIPTION SECTION** (LOINC code: `34089-3`) as part of the narrative text describing the drug formulation.

### Step-by-Step Rescue Process

#### Step 1: Find the Product Document

Use the active ingredient or product name to locate the document:

```
GET /api/Label/ingredient/search?substanceNameSearch={activeIngredient}&pageNumber=1&pageSize=10
```

**Output needed**: `documentGUID` from the response

**Example Response Fields**:
- `documentGUID`: "15a26986-58b8-444b-9759-2529bd41cd25"
- `productName`: "CEPHALEXIN"
- `labelerName`: "Bryant Ranch Prepack"

#### Step 2: Get All Sections for the Document

Retrieve all section content to search for the target information:

```
GET /api/Label/section/content/{documentGUID}
```

**Example**:
```
GET /api/Label/section/content/15a26986-58b8-444b-9759-2529bd41cd25
```

#### Step 3: Scan Section Text for Target Keyword

Look through the `contentText` field of each section for the keyword **"inactive"**. The section containing this text typically has:

- `sectionCode`: "34089-3" (DESCRIPTION SECTION)
- `sectionDisplayName`: "DESCRIPTION SECTION"
- `sectionTitle`: "11 DESCRIPTION"

#### Step 4: Retrieve the Specific Section

Once the section code is identified, get just that section:

```
GET /api/Label/section/content/{documentGUID}?sectionCode=34089-3
```

**Example**:
```
GET /api/Label/section/content/15a26986-58b8-444b-9759-2529bd41cd25?sectionCode=34089-3
```

#### Step 5: Extract Inactive Ingredients from Text

The `contentText` will contain a sentence like:

> "Each capsule contains cephalexin monohydrate equivalent to 250 mg or 500 mg of cephalexin. The capsules also contain the following **inactive** ingredients: microcrystalline cellulose, croscarmellose sodium, D&C Yellow No. 10, FD&C Blue No. 1, FD&C Yellow No. 6, gelatin, magnesium stearate, titanium dioxide, and sodium lauryl sulfate."

**Extraction Pattern**: Find the phrase "inactive ingredients:" and extract the comma-separated list that follows until the next period.

### Endpoint Specification (for interpret response)

```json
{
  "suggestedEndpoints": [
    {
      "step": 1,
      "path": "/api/Label/ingredient/search",
      "method": "GET",
      "queryParameters": {
        "substanceNameSearch": "{activeIngredient}",
        "pageNumber": 1,
        "pageSize": 10
      },
      "description": "Find products containing {activeIngredient} to get document GUID",
      "outputMapping": {
        "documentGuid": "$[0].documentGUID",
        "productName": "$[0].productName",
        "labelerName": "$[0].labelerName"
      }
    },
    {
      "step": 2,
      "path": "/api/Label/section/content/{{documentGuid}}",
      "method": "GET",
      "dependsOn": 1,
      "description": "Get all sections to scan for target keyword"
    },
    {
      "step": 3,
      "path": "/api/Label/section/content/{{documentGuid}}",
      "method": "GET",
      "queryParameters": {
        "sectionCode": "34089-3"
      },
      "dependsOn": 1,
      "description": "Get DESCRIPTION section (LOINC 34089-3) which typically contains inactive ingredient list"
    }
  ]
}
```

### Synthesis Instructions

When synthesizing results from this rescue workflow:

1. **From Step 1**: Confirm product was found, note the product name and labeler
2. **From Step 2/3**: Locate text containing "inactive ingredients:" or "inactive:"
3. **Parse the sentence**: Extract the list following "inactive ingredients:" up to the next period
4. **Format as list**: Present each inactive ingredient as a bullet point

### Example Response Format

```
## Inactive Ingredients for {productName}

**Source**: {labelerName} - {productName} Label
**Section**: Description (LOINC 34089-3)

The following inactive ingredients are listed in the product description:

- Microcrystalline cellulose
- Croscarmellose sodium
- D&C Yellow No. 10
- FD&C Blue No. 1
- FD&C Yellow No. 6
- Gelatin
- Magnesium stearate
- Titanium dioxide
- Sodium lauryl sulfate

---

**Note**: These inactive ingredients were extracted from the narrative text in the Description section, as they were not available in a dedicated inactive ingredients field for this label.
```

### When This Rescue Fails

If the Description section doesn't contain inactive ingredient information:

```
I searched for inactive ingredients for {productName} using multiple approaches:

1. ❌ Checked the InactiveIngredient structured data - not available
2. ❌ Searched the Description section (34089-3) - no inactive ingredient list found

The inactive ingredient information for this product may be found in:
- The complete product packaging
- Other sections of the full prescribing information
- The manufacturer's complete product labeling

Would you like me to search other sections of this label for mentions of "inactive" or "excipient"?
```

---

## Rescue Workflow: Generic Text Search

| Property | Value |
|----------|-------|
| **Intent** | Find any information mentioned in label text when not in structured fields |
| **Triggers** | Any query where structured data returns empty but user expects the information to exist |

### Rescue Strategy

When structured data queries fail, search through all section content for the target keywords.

### Step-by-Step Process

#### Step 1: Get Document GUID

Use any known identifier (product name, ingredient, NDC) to find the document:

```
GET /api/Label/document/search?productNameSearch={productName}&pageNumber=1&pageSize=1
```

or

```
GET /api/Label/ingredient/search?substanceNameSearch={ingredient}&pageNumber=1&pageSize=10
```

#### Step 2: Retrieve All Section Content

```
GET /api/Label/section/content/{documentGuid}
```

#### Step 3: Text Scan

Scan all `contentText` values for the target keyword(s). Note which section(s) contain matches.

#### Step 4: Targeted Section Retrieval

For each matching section, retrieve full content:

```
GET /api/Label/section/content/{documentGuid}?sectionCode={matchingSectionCode}
```

### Common Search Targets and Likely Locations

| Target Information | Likely Section | LOINC Code |
|-------------------|----------------|------------|
| Inactive ingredients | Description | 34089-3 |
| Storage conditions | How Supplied/Storage | 34069-5 or 44425-7 |
| Physical description | Description | 34089-3 |
| Mechanism of action | Clinical Pharmacology | 34090-1 |
| Pregnancy information | Use in Specific Populations | 43684-0 |
| Pediatric dosing | Pediatric Use | 34081-0 |

---

## Text Analysis Patterns

When extracting structured data from narrative text, use these patterns:

### Pattern: List After Keyword

**Structure**: `{keyword}: item1, item2, item3, and item4.`

**Extraction**:
1. Find the keyword (e.g., "inactive ingredients")
2. Capture text after the colon
3. Split by commas
4. Handle "and" before last item
5. Trim each item

**Example Input**:
> "The capsules also contain the following inactive ingredients: microcrystalline cellulose, croscarmellose sodium, and titanium dioxide."

**Extracted**:
- microcrystalline cellulose
- croscarmellose sodium
- titanium dioxide

### Pattern: Parenthetical Information

**Structure**: `{item} ({additional info})`

**Example**: "titanium dioxide (colorant)" → Item: titanium dioxide, Purpose: colorant

### Pattern: Strength Information

**Structure**: `{ingredient} {amount} {unit}`

**Example**: "cephalexin monohydrate equivalent to 250 mg" → Ingredient: cephalexin monohydrate, Amount: 250, Unit: mg

---

## Integration with Main Skills

This rescue workflow supplements the main `label.md` skills. The workflow sequence is:

1. **Primary Query** (from label.md): Try structured data endpoints first
2. **Check Results**: If empty or incomplete
3. **Rescue Query** (from this document): Use text search approach
4. **Text Analysis**: Extract structured data from narrative content
5. **Synthesis**: Present findings with source attribution

### Example Combined Workflow

```
User: "What are the inactive ingredients in Cephalexin?"

→ Step 1 (label.md): GET /api/label/section/InactiveIngredient?pageNumber=1&pageSize=50
← Result: Empty or no Cephalexin records

→ Step 2 (rescueWorkflow.md): GET /api/Label/ingredient/search?substanceNameSearch=Cephalexin
← Result: documentGUID found

→ Step 3 (rescueWorkflow.md): GET /api/Label/section/content/{documentGUID}?sectionCode=34089-3
← Result: Description text containing inactive ingredients list

→ Step 4 (text analysis): Extract list from "inactive ingredients:" phrase

→ Synthesis: Present extracted list with source attribution
```

---

## Critical Reminders

1. **Always try structured endpoints first** - Rescue workflows are fallbacks, not primary approaches
2. **Attribute sources clearly** - When data comes from narrative text, note the section it was extracted from
3. **Handle multiple products** - Ingredient search may return multiple products; present options or use first match
4. **Section code 34089-3** - The Description section is the most common location for embedded inactive ingredient lists
5. **Text extraction is best-effort** - Narrative format varies; present raw text if structured extraction fails
