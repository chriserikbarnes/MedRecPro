# Synthesis Rules

Defines rules for synthesizing API results into helpful, conversational responses.

---

## Placeholder Detection and Prevention

Before finalizing ANY response that contains label links, scan for FORBIDDEN placeholder patterns:

| Forbidden Pattern | Example |
|-------------------|---------|
| Generic drug type | "View Full Label (Prescription Drug)" |
| Generic OTC type | "View Full Label (OTC Drug)" |
| Generic document | "View Full Label (Document)" |
| Generic drug term | "View Full Label (Drug)" |
| Generic medication | "View Full Label (Medication)" |

### Self-Check Checklist

Before sending response:

```
[ ] Does every label link contain an ACTUAL product name (e.g., "Fentanyl Citrate", "Propofol")?
[ ] Are there ZERO instances of "Prescription Drug", "OTC Drug", or other generic terms?
[ ] Did every ProductName come from the API response, not from training data?
[ ] If ProductName unavailable, is the link omitted entirely?
```

### Recovery Steps

If you find yourself about to write a placeholder:

1. **STOP** - You are missing the ProductName from the API response
2. Go back and check the `/api/Label/product/latest` response for `productLatestLabel.productName`
3. If you cannot find a ProductName in the API response, **DO NOT** create a label link
4. Never create a label link without the actual product name from the API

---

## Content Adequacy Detection

Some FDA labels have incomplete or truncated section content. Before synthesizing, **evaluate quality**.

### Truncation Indicators

| Indicator | Pattern | Example |
|-----------|---------|---------|
| Trailing colon | Text ends with `:` followed by nothing | "patients with:", "including:", "such as:" |
| Very short content | `fullSectionText` < 200 characters | Single sentence sections |
| Low block count | `contentBlockCount` = 1 for detailed sections | Contraindications, Warnings should have more |
| Header only | Section contains only a heading, no body | "## 5 WARNINGS AND PRECAUTIONS" alone |
| Incomplete list | Numbered/bulleted list with only 1 item | Expected to have multiple items |

### Truncation Detection Regex

Check section content against this pattern to detect truncation:

```regex
(patients with|including|such as|characterized by|conditions|following|contraindicated in|indicated for|used for):[\s\r\n]*$
```

If section text matches this pattern (ends with colon), it is **TRUNCATED**.

### Quality Assessment Matrix

| Quality Level | Indicators | Synthesis Action |
|---------------|------------|------------------|
| **High** | > 500 chars, contentBlockCount >= 3, complete sentences | Present confidently with single source |
| **Medium** | 200-500 chars, contentBlockCount = 2, no truncation patterns | Present with source attribution |
| **Low** | < 200 chars, contentBlockCount = 1, truncation patterns | **Must aggregate from multiple sources** |
| **Unusable** | Header only, ends with colon, < 50 chars | **Exclude from response**, note data unavailable |

### Example: Detecting Truncated Content

**TRUNCATED (DO NOT USE AS-IS):**
```json
{
  "fullSectionText": "## 4 CONTRAINDICATIONS\r\n\r\nGLUMETZA is contraindicated in patients with:",
  "contentBlockCount": 1
}
```
- Ends with "patients with:" (trailing colon pattern)
- contentBlockCount = 1 (too few)
- < 100 characters of actual content

**COMPLETE (USE THIS):**
```json
{
  "fullSectionText": "## 4 CONTRAINDICATIONS\r\n\r\nMetformin is contraindicated in patients with:\r\n\r\n- Severe renal impairment (eGFR below 30 mL/min/1.73 m2)\r\n- Hypersensitivity to metformin hydrochloride\r\n- Acute or chronic metabolic acidosis, including diabetic ketoacidosis",
  "contentBlockCount": 4
}
```
- Contains actual list items
- contentBlockCount = 4
- > 200 characters of content

---

## Data Sourcing Policy

**ALL information in synthesized responses MUST come from executed API endpoints.**

### Prohibited Actions

- Supplement responses with general knowledge or training data
- Include FDA approval dates not present in the marketing start date fields
- Add clinical study results not found in label sections
- Make comparisons to other drugs not mentioned in the data
- Include pharmacology details not in the retrieved sections
- State facts about the drug that aren't in the API response

### Required Actions

- Clearly indicate when requested data is not available: "Not available in label data"
- Reference the source of each fact (which endpoint/field provided it)
- Quote or paraphrase directly from ContentText fields
- Use only the metadata fields returned by the API

### Examples

**CORRECT:**
```
**FDA Approval:** Marketing start date: 2006-10-25 (per label metadata)
```

**INCORRECT:**
```
**FDA Approval:** Originally approved in 1996 (This date came from training data, not the API!)
```

---

## Multi-Document 404 Error Handling

**EXPECT 404 errors** - Not all FDA labels contain every section type. This is **NORMAL**.

### Rules for 404 Responses

| Do NOT | Do |
|--------|-----|
| List individual 404 errors | Focus on successful results |
| Dump "Request to X returned 404" messages | Summarize failures briefly |
| Present errors as primary content | Use data from successful responses |

### Example Response Handling

**WRONG Response:**
```
Request to /api/Label/markdown/sections/abc123 returned 404.
Request to /api/Label/markdown/sections/def456 returned 404.
... (50 more 404 errors)
```

**CORRECT Response:**
```
I found conversion data in 12 buprenorphine product labels. 28 other product labels
did not contain the SPL Unclassified section (this is normal - not all labels have
all sections).

Based on the successful results:
[Present the actual content from successful API calls]
```

---

## Aggregated Multi-Product Response Format

When aggregating content from multiple sources because primary source was truncated:

### Response Structure

```json
{
  "response": "## Metformin Contraindications\n\nBased on **3 FDA product labels**, metformin is contraindicated in patients with:\n\n1. **Severe renal impairment** (eGFR below 30 mL/min/1.73 m2)\n2. **Hypersensitivity** to metformin hydrochloride\n3. **Acute or chronic metabolic acidosis**, including diabetic ketoacidosis\n\n**Source Labels:**\n- GLUCOPHAGE - Complete data\n- Metformin Hydrochloride Tablets - Complete data\n- GLUMETZA - Partial data (content truncated)*\n\n*Note: Some labels had incomplete section content; complete information aggregated from multiple sources.",
  "dataHighlights": {
    "totalProducts": 3,
    "productsWithCompleteData": 2,
    "productsWithTruncatedData": 1,
    "relevantSections": ["CONTRAINDICATIONS"],
    "aggregatedFromMultipleSources": true
  },
  "dataReferences": {
    "View Full Label (GLUCOPHAGE)": "/api/Label/generate/{guid1}/true",
    "View Full Label (Metformin Hydrochloride Tablets)": "/api/Label/generate/{guid2}/true"
  },
  "suggestedFollowUps": ["What are the warnings for metformin?", "How should metformin be dosed?"],
  "warnings": ["Some source labels had truncated content; information aggregated from multiple sources"],
  "isComplete": true
}
```

### Key Points for Aggregated Responses

1. **Transparently note aggregation** - Tell the user when content came from multiple sources
2. **Mark truncated sources** - Use asterisk (*) or "Partial data" notation
3. **Prioritize complete sources** - Use dataReferences only for labels with complete content
4. **Include quality indicators** - `productsWithCompleteData` vs `productsWithTruncatedData`
5. **Set aggregation flag** - Include `aggregatedFromMultipleSources: true` in dataHighlights

---

## Data Sources and Summary Consistency

**Every product shown in "Data sources" MUST appear in the main response summary.**

### Consistency Rules

1. If you searched for a product and the API returned data, **include it in the summary**
2. If you searched for a product and the API returned NO data, **mention that you searched but found no results**
3. Never include a data source link without addressing those results in your summary
4. The most relevant product for the user's query should be **prominently featured**, not hidden in data sources

### Example: Inconsistent Response (WRONG)

```
Summary: "I found limited topical options..."
- Only lists Product A

Data sources:
- Search for Product A (shown in summary)
- Search for hydrocortisone products  <-- NOT IN SUMMARY - WRONG!
```

### Example: Consistent Response (CORRECT)

```
Summary: "I found the following products..."
- Product A: [description from API]
- Hydrocortisone: [description from API]

Data sources:
- Search for Product A
- Search for hydrocortisone products
```

---

## Array Extraction Requirements

When API returns multiple products, you MUST use proper array extraction syntax.

### Output Mapping Syntax

| Syntax | Behavior | Result |
|--------|----------|--------|
| `"documentGuid": "documentGUID"` | Extracts only FIRST value | Queries only 1 product - **WRONG** |
| `"documentGuids": "documentGUID[]"` | Extracts ALL values as array | Queries ALL products - **CORRECT** |

**Common Mistake**: Using `"documentGuid": "documentGUID"` (without `[]`) causes the workflow to query only the first product, missing products with complete data.

### Processing Requirements

1. **Scan the ENTIRE array** - Don't just use the first result
2. **Extract productName and documentGUID from EACH item**
3. **Create a label link for EACH relevant product**
4. **Group by dosage form** when relevant (ointments, solutions, suspensions, etc.)

---

## Related Documents

- [Response Format Standards](./response-format.md) - JSON output structure
- [Indication Discovery](./api/indication-discovery.md) - Product search workflows
- [Label Content](./api/label-content.md) - Section retrieval workflows
