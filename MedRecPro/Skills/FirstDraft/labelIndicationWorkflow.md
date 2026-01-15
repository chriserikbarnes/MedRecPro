# Label Product Indication Discovery Skills

This document provides instructions for identifying products based on conditions, diseases, symptoms, or therapeutic needs. Use this skill when users ask "what product can help with X" or similar questions about finding medications for specific health concerns.

---

## Table of Contents

1. [When to Use This Skill](#when-to-use-this-skill)
2. [Primary Workflow: UNII Matching via Reference Data](#primary-workflow-unii-matching-via-reference-data)
3. [API Workflow: Get Labels and Related Products](#api-workflow-get-labels-and-related-products)
4. [Synthesis Instructions](#synthesis-instructions)
5. [Common Condition Keywords](#common-condition-keywords)

---

## When to Use This Skill

Trigger this skill when users ask about:

- **Finding products for a condition**: "What helps with depression?", "I have high blood pressure", "Options for diabetes"
- **Symptom-based queries**: "I'm feeling down", "My blood pressure is high", "I have joint pain"
- **Therapeutic category searches**: "What antidepressants are available?", "Show me pain relievers"
- **Alternative product searches**: "What else works like Lipitor?", "Generic alternatives"

### Key Indicators

| Query Pattern | Example |
|--------------|---------|
| "What helps with {condition}" | "What helps with anxiety?" |
| "I have {symptom/condition}" | "I have migraines" |
| "What are my options for {condition}" | "What are my options for diabetes?" |
| "Products for {condition}" | "Products for hypertension" |
| "What can I take for {symptom}" | "What can I take for pain?" |
| "Alternatives to {product}" | "Alternatives to Prozac" |

---

## Primary Workflow: UNII Matching via Reference Data

**CRITICAL**: For condition-based queries, use the `labelProductIndication.md` reference file to match the user's condition to the best UNII(s). This file contains curated indication summaries organized by UNII.

### Step 1: Load Reference Data

Load the file: `C:\Users\chris\Documents\Repos\MedRecPro\Skills\labelProductIndication.md`

This file contains FDA indications data in pipe-delimited format:
```
ProductNames|UNII|IndicationsSummary
```

Each entry is separated by `---` and includes:
- **ProductNames**: Comma-separated list of brand/generic names
- **UNII**: Unique Ingredient Identifier (FDA standard)
- **IndicationsSummary**: Combined indication text for all products with this UNII

### Step 2: Match Condition to UNII

Search the `IndicationsSummary` field for keywords matching the user's **COMPLETE condition** (not just part of it):

**CRITICAL: Match ALL Parts of the User's Query**

When a user asks about a compound condition like "estrogen sensitive cancer" or "hormone receptor positive breast cancer":
- Match BOTH the mechanism/therapeutic class AND the specific disease/condition
- Do NOT match drugs that only address one part (e.g., don't return generic estrogen blockers if the user asked about cancer)
- The IndicationsSummary MUST contain keywords for the FULL indication

**Example - CORRECT Matching**:
- Query: "estrogen sensitive cancer"
- Search for: entries containing BOTH "estrogen" AND ("cancer" OR "carcinoma" OR "breast cancer" OR "malignancy")
- Result: Tamoxifen, Letrozole, Exemestane (indicated for breast cancer)

**Example - INCORRECT Matching**:
- Query: "estrogen sensitive cancer"
- Search for: only "estrogen" without cancer context
- Result: Estradiol supplements (WRONG - treats menopause, not cancer)

**Example Condition Mappings**:

| User Query | Search Keywords (ALL must be present) | Example Matching UNIIs |
|------------|---------------------------------------|------------------------|
| "estrogen sensitive cancer" | estrogen + (cancer OR carcinoma OR breast) | Tamoxifen, Letrozole, Anastrozole |
| "hormone receptor positive breast cancer" | breast cancer + hormone OR estrogen | Tamoxifen, Letrozole, Fulvestrant |
| "depression" | depression OR depressive OR MDD | Fluoxetine, Sertraline, etc. |
| "high blood pressure" | hypertension OR blood pressure | Lisinopril, Amlodipine, etc. |
| "diabetes" | diabetes OR glycemic OR type 2 | Metformin, Insulin, etc. |
| "anxiety" | anxiety OR anxiolytic OR panic | Alprazolam, Buspirone, etc. |
| "pain" | pain OR analgesic | Ibuprofen, Acetaminophen, etc. |
| "cholesterol" | cholesterol OR hyperlipidemia | Atorvastatin, Simvastatin, etc. |

**Selection Criteria**:
1. Match ALL keywords from the user's query in the IndicationsSummary text
2. For compound conditions (disease + qualifier), BOTH parts must be present in the indication
3. Prefer entries with more specific indication matches over general mechanism matches
4. Select 1-5 best matching UNIIs based on relevance
5. Note the ProductNames for user display

### Step 3: Extract UNII(s) for API Calls

After matching, extract the UNII code(s) to use in subsequent API calls.

---

## CRITICAL: Relevance Validation Step (Criticism Step)

**BEFORE presenting ANY product to the user, you MUST validate that it is relevant to their query.**

This validation step prevents returning irrelevant products (e.g., Tretinoin Cream for "surgical analgesic" query).

### When to Perform Validation

**ALWAYS** perform this validation after:
1. Receiving results from `/api/Label/product/latest`
2. Before synthesizing the response
3. Before creating `dataReferences` entries

### How to Validate Relevance

For each product returned by the API:

1. **Check the product's indication** against the user's query using `labelProductIndication.md`:
   - Look up the product's UNII in `labelProductIndication.md`
   - Read the `IndicationsSummary` for that UNII
   - **Does the indication match the user's query?**

2. **Ask yourself these questions**:
   - If user asked about "surgical analgesic", does this product's indication mention "surgical", "perioperative", "anesthesia", or "intraoperative"?
   - If user asked about "cancer", does this product's indication mention "cancer", "carcinoma", "malignancy", or "tumor"?
   - If user asked about "seasonal allergies", does this product's indication mention "allergic rhinitis", "seasonal", or "allergy"?

3. **EXCLUDE products that fail validation**:
   - If the product's indication does NOT contain keywords matching the user's query, DO NOT include it in the response
   - Do not create a `dataReferences` entry for irrelevant products
   - Do not mention irrelevant products at all

### Validation Examples

**Query**: "What can be used as a surgical analgesic?"

| Product Returned | UNII | Indication Summary | Relevant? |
|-----------------|------|-------------------|-----------|
| Fentanyl Citrate | UF599785JZ | "...indicated for analgesic action of short duration during the anesthetic periods, premedication, induction and maintenance, and in the immediate postoperative period..." | ✅ YES - mentions anesthetic, induction, postoperative |
| Propofol | YI7VU623SF | "...indicated for induction and maintenance of general anesthesia..." | ✅ YES - mentions induction, anesthesia |
| Tretinoin Cream | 5688UTC01R | "...indicated for topical treatment of acne vulgaris..." | ❌ NO - treats acne, not surgical use |
| Tetrabenazine | Z9O08YRN8O | "...indicated for the treatment of chorea associated with Huntington's disease..." | ❌ NO - treats Huntington's, not surgical use |

**Result**: Only include Fentanyl Citrate and Propofol in the response. Exclude Tretinoin and Tetrabenazine entirely.

### Validation Failure Response

If NO products pass validation (i.e., none of the API results are relevant to the query):

```json
{
  "response": "I searched the database but did not find any products specifically indicated for {user's condition}. The products in the database may not include medications for this therapeutic use. Would you like to try a different search term?",
  "dataHighlights": {
    "searchTerm": "{user's condition}",
    "productsSearched": 5,
    "productsRelevant": 0
  },
  "dataReferences": {},
  "suggestedFollowUps": ["What conditions are covered in the database?", "Try a different search term"],
  "warnings": ["No relevant products found for this indication"],
  "isComplete": true
}
```

---

## API Workflow: Get Labels and Related Products

Once you have identified UNII(s) from the reference data, use the following API workflow:

### CRITICAL: Use UNII-Based Queries ONLY

**DO NOT USE** these endpoints for condition-based discovery queries:
- ❌ `/api/Label/ingredient/search?substanceNameSearch={name}` - Name matching is UNRELIABLE (e.g., "fexofenadine" may match "fosfomycin")
- ❌ `/api/Label/pharmacologic-class/search` - Too broad, doesn't filter by specific indication
- ❌ Any endpoint that searches by ingredient NAME instead of UNII code

**ALWAYS USE** the UNII code from `labelProductIndication.md`:
- ✅ `/api/Label/product/latest?unii={UNII}` - Precise matching by FDA UNII code

### Step 1: Get Latest Label by UNII

For each matched UNII, call the first-line product selector:

```
GET /api/Label/product/latest?unii={UNII}&pageNumber=1&pageSize=5
```

**IMPORTANT**: Limit results to **10 products or fewer** across all UNII queries to avoid slow database performance. Use `pageSize=5` for each UNII query.

**Example**:
```
GET /api/Label/product/latest?unii=R16CO5Y76E&pageSize=5
```

**Response Fields**:
- `ProductName`: Proprietary product name
- `ActiveIngredient`: Active ingredient name
- `UNII`: Unique Ingredient Identifier
- `DocumentGUID`: For retrieving the complete label and related products

### Step 2: Get Label Content for Summary (REQUIRED)

**ALWAYS** retrieve label content for each product to provide accurate, attributable summaries.

**PREFERRED ENDPOINT (Markdown with sectionCode filter):**

```
GET /api/Label/markdown/sections/{DocumentGUID}?sectionCode={loincCode}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `DocumentGUID` | GUID | Yes | The document identifier from Step 1 |
| `sectionCode` | string | No | LOINC code to filter sections (e.g., "34067-9" for Indications) |

**Token Optimization:** Use `sectionCode` parameter to fetch only the needed section. This reduces payload from ~88KB (all sections) to ~1-2KB per section.

**Common LOINC Codes for Indication Workflows:**
- `34067-9` = Indications and Usage (what the drug treats)
- `34089-3` = Description (drug class, mechanism)
- `34068-7` = Dosage and Administration
- `43685-7` = Warnings and Precautions
- `34084-4` = Adverse Reactions

**Response Fields:**
- `sectionCode`: LOINC code for the section
- `sectionTitle`: Human-readable section title
- `fullSectionText`: **Pre-formatted markdown content ready for AI consumption**
- `documentTitle`: For attribution

**IMPORTANT - Data Sources for Summaries**:
1. **Primary**: Use `/api/Label/markdown/sections/{DocumentGUID}?sectionCode={loincCode}` for attributable content
2. **Secondary**: Use text from `labelProductIndication.md` reference file (contains curated indication summaries)
3. **Never**: Do NOT generate descriptions from training data

**Why Markdown Endpoint is Preferred**:
- Returns pre-formatted markdown ready for AI summarization
- `fullSectionText` contains aggregated, formatted content
- Server-side sectionCode filtering reduces token usage significantly
- Content is directly attributable to the API response

### Step 3: Get Related Products Using DocumentGUID

For each DocumentGUID returned in Step 1, find related products:

```
GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}
```

**Filter Options**:
- **Same Active Ingredient** (for generics):
  ```
  GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}&relationshipType=SameActiveIngredient
  ```

- **Same Application Number** (products under same approval):
  ```
  GET /api/Label/product/related?sourceDocumentGuid={DocumentGUID}&relationshipType=SameApplicationNumber
  ```

**Response Fields**:
- `RelatedProductName`: Name of the related product
- `RelatedDocumentGUID`: Document identifier for the related product's label
- `RelationshipType`: How the products are related
- `SharedValue`: The shared attribute (ingredient or application number)

### Step 4: Build Label Links

For each product found, construct the label link using the **generate** endpoint:
```
/api/Label/generate/{DocumentGUID}/true
```

**IMPORTANT**: Use `/api/Label/generate/{DocumentGUID}/true` (NOT `/api/Label/single/`) for viewable label links. The `/true` suffix enables minified XML for faster rendering.

**CRITICAL - Use RELATIVE URLs Only:**
- ✅ CORRECT: `/api/Label/generate/908025cf-e126-4241-b794-47d2fd90078e/true`
- ❌ WRONG: `http://localhost:5001/api/Label/generate/908025cf.../true`
- ❌ WRONG: `http://localhost:5093/api/Label/generate/908025cf.../true`
- ❌ WRONG: `https://medrecpro.com/api/Label/generate/908025cf.../true`

**NEVER include `http://`, `https://`, `localhost`, or any domain in label URLs.** The frontend will add the correct base URL automatically.

---

## Output Format

When processing a condition-based query, respond with:

```json
{
  "success": true,
  "referenceDataUsed": "labelProductIndication.md",
  "matchedCondition": "{user's condition}",
  "matchedUNIIs": [
    {
      "unii": "{UNII}",
      "productNames": "{comma-separated product names}",
      "indicationMatch": "{relevant excerpt from IndicationsSummary}"
    }
  ],
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": {
        "unii": "{matched UNII}",
        "pageNumber": 1,
        "pageSize": 5
      },
      "description": "Get latest label for matched UNII (limit 5 for performance)",
      "outputMapping": {
        "documentGuid": "$[0].ProductLatestLabel.DocumentGUID",
        "productName": "$[0].ProductLatestLabel.ProductName"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1,
      "description": "Get Indications section as markdown (token optimized)"
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/product/related",
      "queryParameters": {
        "sourceDocumentGuid": "{{documentGuid}}"
      },
      "dependsOn": 1,
      "description": "Find related products (generics, alternatives)"
    }
  ],
  "explanation": "Matched '{condition}' to UNII(s) using reference data. Full label content retrieved for summaries."
}
```

---

## Example Workflows

### Depression Treatment Search

**User:** "I'm feeling down, what helps with depression?"

**Step 1: Search labelProductIndication.md**

Find entries where IndicationsSummary contains "depression":
```
Citalopram Hydrobromide|QUC7NX6WMB|
# Citalopram Summary
Citalopram tablets are indicated for the treatment of depression...

Fluoxetine Hydrochloride|01K63SUP8D|
# Fluoxetine Summary
PROZAC is indicated for the treatment of major depressive disorder...

Sertraline Hydrochloride|QUC7NX6WMB|
# Sertraline Summary
Sertraline tablets are indicated for major depressive disorder (MDD)...
```

**Step 2: API Calls**

```json
{
  "success": true,
  "referenceDataUsed": "labelProductIndication.md",
  "matchedCondition": "depression",
  "matchedUNIIs": [
    {
      "unii": "QUC7NX6WMB",
      "productNames": "Citalopram Hydrobromide",
      "indicationMatch": "indicated for the treatment of depression"
    },
    {
      "unii": "01K63SUP8D",
      "productNames": "Fluoxetine Hydrochloride, PROZAC",
      "indicationMatch": "indicated for the treatment of major depressive disorder"
    }
  ],
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "QUC7NX6WMB", "pageNumber": 1, "pageSize": 5 },
      "description": "Get latest label for Citalopram (limit 5 for performance)",
      "outputMapping": { "documentGuid1": "$[0].ProductLatestLabel.DocumentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "01K63SUP8D", "pageNumber": 1, "pageSize": 5 },
      "description": "Get latest label for Fluoxetine (limit 5 for performance)",
      "outputMapping": { "documentGuid2": "$[0].ProductLatestLabel.DocumentGUID" }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid1}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1,
      "description": "Get Citalopram Indications section as markdown (token optimized)"
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid2}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 2,
      "description": "Get Fluoxetine Indications section as markdown (token optimized)"
    },
    {
      "step": 5,
      "method": "GET",
      "path": "/api/Label/product/related",
      "queryParameters": { "sourceDocumentGuid": "{{documentGuid1}}" },
      "dependsOn": 1,
      "description": "Find Citalopram alternatives"
    },
    {
      "step": 6,
      "method": "GET",
      "path": "/api/Label/product/related",
      "queryParameters": { "sourceDocumentGuid": "{{documentGuid2}}" },
      "dependsOn": 2,
      "description": "Find Fluoxetine alternatives"
    }
  ],
  "explanation": "Found antidepressants matching 'depression' in reference data. Full label content retrieved for summaries."
}
```

---

### Seasonal Allergies Treatment Search

**User:** "What can be used for seasonal allergies"

**Step 1: Search labelProductIndication.md**

Find entries where IndicationsSummary contains "allergic rhinitis" or "seasonal allergy" or "antihistamine":
```
Cetirizine Hydrochloride|YO7261ME24|
# Cetirizine Summary
Cetirizine is indicated for relief of symptoms associated with seasonal allergic rhinitis...

Fexofenadine Hydrochloride|E6582LOH6V|
# Fexofenadine Summary
Fexofenadine is indicated for the treatment of symptoms associated with seasonal allergic rhinitis...

Diphenhydramine Hydrochloride|TC2D6JAD40|
# Diphenhydramine Summary
Diphenhydramine is indicated for allergic conditions...
```

**Step 2: API Calls - Use UNII codes ONLY (NOT substance names)**

```json
{
  "success": true,
  "referenceDataUsed": "labelProductIndication.md",
  "matchedCondition": "seasonal allergies",
  "matchedUNIIs": [
    {
      "unii": "YO7261ME24",
      "productNames": "Cetirizine Hydrochloride",
      "indicationMatch": "seasonal allergic rhinitis"
    },
    {
      "unii": "E6582LOH6V",
      "productNames": "Fexofenadine Hydrochloride",
      "indicationMatch": "seasonal allergic rhinitis"
    }
  ],
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "YO7261ME24", "pageNumber": 1, "pageSize": 5 },
      "description": "Get latest label for Cetirizine BY UNII (not by name)",
      "outputMapping": { "documentGuid1": "$[0].ProductLatestLabel.DocumentGUID" }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": { "unii": "E6582LOH6V", "pageNumber": 1, "pageSize": 5 },
      "description": "Get latest label for Fexofenadine BY UNII (not by name)",
      "outputMapping": { "documentGuid2": "$[0].ProductLatestLabel.DocumentGUID" }
    },
    {
      "step": 3,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid1}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 1,
      "description": "Get Cetirizine Indications section as markdown (token optimized)"
    },
    {
      "step": 4,
      "method": "GET",
      "path": "/api/Label/markdown/sections/{{documentGuid2}}",
      "queryParameters": { "sectionCode": "34067-9" },
      "dependsOn": 2,
      "description": "Get Fexofenadine Indications section as markdown (token optimized)"
    }
  ],
  "explanation": "Found antihistamines matching 'seasonal allergies' using UNII codes from reference data. Token-optimized calls fetch only Indications section."
}
```

**CRITICAL**: Notice we use `unii=YO7261ME24` NOT `substanceNameSearch=cetirizine`. This prevents incorrect matches.

---

### Hypertension Treatment Search

**User:** "I have high blood pressure, what are my options?"

**Step 1: Search labelProductIndication.md**

Find entries where IndicationsSummary contains "hypertension" or "blood pressure":
```
Lisinopril|7Q3P4BS2FD|
# Lisinopril Summary
Lisinopril is indicated for the treatment of hypertension...

Amlodipine Besylate|1J444QC288|
# Amlodipine Summary
Amlodipine is indicated for the treatment of hypertension...
```

**Step 2: API Workflow**

Use the matched UNIIs (7Q3P4BS2FD, 1J444QC288) with GetProductLatestLabels, then get section content, then GetRelatedProducts.

---

### Alternative Product Search

**User:** "What alternatives are there to Lipitor?"

For product-specific queries (not condition-based), use the first-line selector directly:

```json
{
  "success": true,
  "endpoints": [
    {
      "step": 1,
      "method": "GET",
      "path": "/api/Label/product/latest",
      "queryParameters": {
        "productNameSearch": "Lipitor",
        "pageNumber": 1,
        "pageSize": 1
      },
      "description": "Find Lipitor to get its document GUID",
      "outputMapping": {
        "documentGuid": "$[0].ProductLatestLabel.DocumentGUID",
        "productName": "$[0].ProductLatestLabel.ProductName",
        "activeIngredient": "$[0].ProductLatestLabel.ActiveIngredient"
      }
    },
    {
      "step": 2,
      "method": "GET",
      "path": "/api/Label/product/related",
      "queryParameters": {
        "sourceDocumentGuid": "{{documentGuid}}",
        "relationshipType": "SameActiveIngredient"
      },
      "dependsOn": 1,
      "description": "Find products with the same active ingredient (generics)"
    }
  ],
  "explanation": "Finding generic alternatives to Lipitor based on same active ingredient."
}
```

---

## Synthesis Instructions

**CRITICAL - DATA SOURCE RESTRICTIONS:**
- **ONLY use data from the API responses** - do NOT supplement with training data
- Product names MUST come from `productLatestLabel.productName` in the API response
- Active ingredients MUST come from `productLatestLabel.activeIngredient` in the API response
- **Product summaries/descriptions MUST come from `/api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}`**
- **DO NOT generate drug descriptions, mechanisms of action, or clinical details from training data**
- If the API didn't return data for a product, do NOT describe that product

When presenting results to the user:

### For Condition-Based Results

```markdown
## Products Found for {Condition}

Based on the API results, I found **{count}** products in your database:

### {ProductName from API}
- **Active Ingredient**: {activeIngredient from API response}
- **Indication**: {fullSectionText from markdown/sections API call - sectionCode=34067-9}

### {ProductName2 from API}
- **Active Ingredient**: {activeIngredient from API response}
- **Indication**: {fullSectionText from markdown/sections API call}

---

**Important**: Click the label links below to view full prescribing information. Consult a healthcare provider before starting any medication.
```

**WHAT TO INCLUDE:**
- Product names exactly as returned by `/api/Label/product/latest`
- Active ingredients exactly as returned by the API
- **Indication summaries from `labelProductIndication.md`** reference file (PRIMARY source)
- UNII codes from the API response
- Related products from `/api/Label/product/related` response

**DATA SOURCE PRIORITY FOR PRODUCT DESCRIPTIONS:**
1. **Primary**: Use `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9` for attributable, token-optimized content
2. **Supplemental**: Use text from `labelProductIndication.md` reference file (contains curated indication summaries)
3. **Never**: Do NOT generate descriptions from training data

**WHAT NOT TO INCLUDE:**
- Drug class descriptions from training data (e.g., "bronchodilator", "LAMA")
- Mechanism of action unless from API
- Dosing recommendations from training data
- Comparative statements between products from training data

### REQUIRED: dataReferences with Label Links

**Your JSON response MUST include a `dataReferences` object** with clickable links to view each product's full FDA label.

For each product returned by the API calls, add an entry to `dataReferences`:

```json
{
  "response": "...",
  "dataReferences": {
    "View Full Label (Aspirin and Extended-Release Dipyridamole)": "/api/Label/generate/bf81f508-2def-4916-a2f1-0f73f473be3f/true",
    "View Full Label (Warfarin Sodium)": "/api/Label/generate/402c9141-7413-29cb-e063-6294a90abfef/true",
    "View Full Label (Amlodipine and Atorvastatin)": "/api/Label/generate/96c4b322-32ff-4344-ae71-02aa14479784/true"
  },
  "suggestedFollowUps": [...],
  "isComplete": true
}
```

**CRITICAL**:
- Use the `documentGUID` values from the `/api/Label/product/latest` API responses
- Use the **ACTUAL** `productName` values from the API responses for the display text
- **DO NOT use generic placeholders** like "Prescription Drug" or "OTC Drug" - use the real product name
- **CORRECT**: `"View Full Label (Amantadine)": "/api/Label/generate/a9507ffa.../true"`
- **WRONG**: `"View Full Label (Prescription Drug)": "/api/Label/generate/a9507ffa.../true"`
- The link format is: `/api/Label/generate/{DocumentGUID}/true`
- Include ALL products found, not just the first one

### Key Synthesis Points

1. **List ONLY products returned by the API** - use exact `productName` values from API response
2. **Limit to 10 products or fewer** - use `pageSize=5` per UNII to avoid slow database performance
3. **Include active ingredients** exactly as returned by `activeIngredient` field in API
4. **Product summaries from markdown sections endpoint (REQUIRED)**:
   - Use `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9` for Indications (token optimized)
   - Use `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34089-3` for Description/drug class
   - Use `fullSectionText` field which contains pre-formatted markdown content
   - The `labelProductIndication.md` reference file is for UNII matching only, not for final summaries
5. **ALWAYS include dataReferences** with label links using `/api/Label/generate/{DocumentGUID}/true`
6. **Show related products** ONLY from the `/api/Label/product/related` API response
7. **Add medical disclaimer** at the end
8. **NEVER add drug descriptions from training data** - all descriptions must come from:
   - `/api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}` API response (PRIMARY - token optimized)
   - `labelProductIndication.md` reference file (for UNII matching only)

---

## Common Condition Keywords

Map common user terms to search keywords for the reference data:

| User Term | Search Keywords (ALL keywords must appear in indication) |
|-----------|----------------------------------------------------------|
| estrogen sensitive cancer | (estrogen OR hormone receptor) + (cancer OR carcinoma OR breast) |
| hormone positive breast cancer | breast cancer + (hormone OR estrogen OR ER-positive) |
| breast cancer | breast cancer, breast carcinoma, mammary |
| prostate cancer | prostate cancer, prostate carcinoma |
| feeling down, sad | depression, depressive, MDD |
| high blood pressure | hypertension, antihypertensive |
| diabetes, blood sugar | diabetes, glycemic, type 2 diabetes |
| heart problems | cardiovascular, cardiac, heart failure |
| anxiety, nervous | anxiety, anxiolytic, panic disorder |
| pain, aches | pain, analgesic, arthritis |
| allergies, sneezing, seasonal allergies | allergic rhinitis, allergic, rhinitis, antihistamine, seasonal allergy |
| acid reflux, heartburn | GERD, gastroesophageal, acid |
| high cholesterol | cholesterol, hyperlipidemia, statin |
| arthritis, joint pain | arthritis, osteoarthritis, rheumatoid |
| asthma, breathing | asthma, bronchospasm, respiratory |
| infection | bacterial infection, antibiotic |
| insomnia, can't sleep | insomnia, sleep disorder, sedative |
| seizures, epilepsy | seizure, epilepsy, anticonvulsant |
| migraines, headaches | migraine, headache |
| surgical analgesic, surgery pain | surgical, analgesic, perioperative, intraoperative pain |
| anesthesia, sedation | anesthetic, anesthesia, sedation, induction |
| muscle relaxant (surgical) | neuromuscular, muscle relaxant, paralytic |
| local anesthetic | local anesthetic, lidocaine, numbing |

**NOTE for Cancer Queries**: When the user mentions cancer, the indication summary MUST include cancer-related keywords (cancer, carcinoma, malignancy, tumor, neoplasm). Do NOT return drugs that only block hormones for non-cancer uses (e.g., estrogen supplements for menopause).

**NOTE for Surgical/Anesthetic Queries**: When the user asks about surgical analgesics, anesthetics, or perioperative medications, the indication summary MUST include surgical/anesthetic-related keywords (surgical, perioperative, anesthesia, sedation, induction, intraoperative). Do NOT return drugs for general pain relief unless they are specifically indicated for surgical/perioperative use.

---

## Integration with Other Skills

This skill works together with:

- **label**: For detailed section content (side effects, dosing, warnings)
- **rescueWorkflow**: When reference data matching returns no results

### Escalation to Label Skill

After identifying products, users may want detailed information:
- "Tell me more about {ProductName}" -> Use label skill
- "What are the side effects of {ProductName}?" -> Use label skill with section codes
- "How do I take {ProductName}?" -> Use label skill for dosage section

---

## Priority Order for Product Identification

1. **For condition/symptom queries**:
   - Search `labelProductIndication.md` to match condition to UNII(s)
   - Use UNII with `/api/Label/product/latest` to get DocumentGUID
   - Use DocumentGUID with `/api/Label/product/related` to find alternatives

2. **For product-specific queries**:
   - Use `/api/Label/product/latest` with productNameSearch or activeIngredientSearch
   - Use DocumentGUID with `/api/Label/product/related` to find alternatives

3. **For UNII-specific queries**:
   - Use `/api/Label/product/latest` with unii parameter directly

---

## CRITICAL: Content Adequacy Detection and Multi-Product Fallback

### Problem: Truncated or Incomplete Section Content

Some FDA labels have incomplete section content. For example:

```json
{
  "fullSectionText": "## 1 INDICATIONS AND USAGE\r\n\r\nDrug X is indicated for:",
  "contentBlockCount": 1
}
```

This is **truncated** - the text ends with "indicated for:" but lists no actual indications.

### Detecting Inadequate Content During Synthesis

Evaluate returned section content for these **truncation indicators**:

| Indicator | Pattern | Example |
|-----------|---------|---------|
| Trailing colon | Text ends with `:` followed by nothing | "indicated for:", "patients with:", "including:" |
| Very short content | `fullSectionText` < 200 characters | Single sentence sections |
| Low block count | `contentBlockCount` = 1 for detailed sections | Indications, Contraindications should have more |
| Header only | Section contains only a heading, no body | "## 1 INDICATIONS AND USAGE" alone |

### Multi-Product Fallback Workflow for Indications

**ALWAYS use multi-product workflow** to search ALL products with the same active ingredient. Do NOT rely on a single product.

**CRITICAL - Array Extraction Syntax**: Use `[]` suffix to extract ALL values from the array, not just the first one.

```json
{
  "success": true,
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
      "description": "Search for ALL products containing {ingredientName} (use pageSize=50)",
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
      "dependsOn": 1,
      "description": "Get Indications from ALL found products (batch expansion)"
    }
  ],
  "explanation": "Fetching Indications from ALL products to ensure complete information."
}
```

### CRITICAL: Array Extraction vs Single Value

| Syntax | Behavior | Use Case |
|--------|----------|----------|
| `"documentGuid": "documentGUID"` | Extracts ONLY the first value | ❌ WRONG for multi-product |
| `"documentGuids": "documentGUID[]"` | Extracts ALL values as array | ✅ CORRECT for multi-product |

**The `[]` suffix is REQUIRED** for multi-product workflows. Without it, only the first product is queried and you'll miss products with complete data.

### Synthesis Instructions for Multi-Product Indication Results

When synthesizing results from multiple product labels:

1. **Identify the most complete response** - Look for highest `contentBlockCount` and longest `fullSectionText`
2. **Skip products with 404 errors** - Some products may not have the requested section
3. **Detect truncated content** - If text ends with ":" or has < 200 chars, mark as incomplete
4. **Cross-reference for consistency** - Compare indication text across products
5. **Use the most detailed source** as the primary response
6. **Cite all sources** - List all product names that contributed
7. **Flag truncated sources** - Note which labels had incomplete data

---

## Critical Reminders

1. **Use labelProductIndication.md reference file** to match conditions to UNIIs - this file provides UNII codes for API calls
2. **Use `/api/Label/ingredient/advanced` with pageSize=50** to search for ALL products with the ingredient
3. **ALWAYS use array extraction syntax `[]`** in outputMapping to get ALL documentGUIDs, not just the first one
4. **GetRelatedProducts with DocumentGUID** finds alternatives and generics
5. **ALWAYS include `dataReferences`** in your JSON response with label links for each product
6. **Use the correct label link format**: `/api/Label/generate/{DocumentGUID}/true` (NOT `/api/Label/single/`)
7. **Extract ProductName and DocumentGUID** from the API responses to build label links
8. **Add medical disclaimer** reminding users to consult healthcare providers
9. **NEVER USE TRAINING DATA** - All product information (names, ingredients, descriptions) must come from:
   - `/api/Label/markdown/sections/{documentGuid}?sectionCode={loincCode}` API response (PRIMARY - token optimized)
   - `labelProductIndication.md` reference file (for UNII matching and supplemental summaries)
   - The API response fields (`productName`, `activeIngredient`, `unii`, `documentGUID`)
   - NOT from your training knowledge about medications
10. **Detect truncated content** - If section content ends with ":", is < 200 chars, or has contentBlockCount = 1, it's incomplete
11. **Aggregate from multiple sources** - Select the most complete content from all returned products
