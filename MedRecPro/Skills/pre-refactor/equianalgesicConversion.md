# Equianalgesic Conversion Skills

This document provides instructions for handling opioid dose conversion and equianalgesic dosing queries. Use this skill when users ask about converting between opioid medications or need equianalgesic dosing information.

---

## ⚠️ CRITICAL WORKFLOW REQUIREMENTS - READ FIRST

**For ANY opioid conversion query, you MUST:**

1. **Search for BOTH opioids** - Use `/api/Label/ingredient/advanced` for source AND target opioids
2. **Extract ALL documentGUIDs** - Use `documentGUID[]` array extraction syntax (the `[]` suffix is REQUIRED)
3. **Fetch ONLY THESE TWO sections from ALL products for BOTH opioids:**
   - **34068-7** (Dosage and Administration) - Main section with conversion tables
   - **42229-5** (SPL Unclassified) - Subsections contain detailed conversion tables like "Table 1"
4. **Fetch from BOTH source AND target opioid products** - Conversion tables are often in BOTH labels

**ONLY USE sectionCode=34068-7 and sectionCode=42229-5. NO OTHER SECTION CODES.**

**Example: "Convert methadone to buprenorphine"**
- Step 1: Search methadone products → extract `methadoneDocumentGuids: "documentGUID[]"`
- Step 2: Search buprenorphine products → extract `buprenorphineDocumentGuids: "documentGUID[]"`
- Step 3: Fetch **sectionCode=34068-7** from ALL methadone products
- Step 4: Fetch **sectionCode=42229-5** from ALL methadone products ← **"Table 1: Conversion Factors" is HERE!**
- Step 5: Fetch **sectionCode=34068-7** from ALL buprenorphine products
- Step 6: Fetch **sectionCode=42229-5** from ALL buprenorphine products

**❌ WRONG - DO NOT USE THESE SECTION CODES:**
- ❌ sectionCode=34090-1 (Clinical Pharmacology) - NO conversion tables here
- ❌ sectionCode=43685-7 (Warnings and Precautions) - NO conversion tables here
- ❌ sectionCode=34067-9 (Indications and Usage) - NO conversion tables here

**✅ CORRECT - ONLY USE THESE SECTION CODES:**
- ✅ sectionCode=34068-7 (Dosage and Administration)
- ✅ sectionCode=42229-5 (SPL Unclassified - contains "Table 1: Conversion Factors")

---

## Table of Contents

1. [When to Use This Skill](#when-to-use-this-skill)
2. [Critical Attribution Requirement](#critical-attribution-requirement)
3. [Primary Workflow: Label-Based Conversion Data](#primary-workflow-label-based-conversion-data)
4. [Common LOINC Codes for Dosing Information](#common-loinc-codes-for-dosing-information)
5. [API Workflow: Retrieve Equianalgesic Data](#api-workflow-retrieve-equianalgesic-data)
6. [Synthesis Instructions](#synthesis-instructions)
7. [Response Format Requirements](#response-format-requirements)

---

## When to Use This Skill

Trigger this skill when users ask about:

- **Opioid dose conversions**: "Convert morphine to hydromorphone", "What is the equivalent dose?"
- **Equianalgesic dosing**: "Equianalgesic dose", "equivalent dose", "opioid conversion"
- **Switching between opioids**: "Switch from fentanyl to morphine", "changing opioids"
- **Opioid tolerance definitions**: "What is considered opioid tolerant?"
- **Morphine milligram equivalents (MME)**: "MME calculation", "morphine equivalent"

### Key Indicators

| Query Pattern | Example |
|--------------|---------|
| "Convert {opioid1} to {opioid2}" | "Convert 30mg morphine to hydromorphone" |
| "Equivalent dose of {opioid}" | "What is the equivalent dose of fentanyl?" |
| "Equianalgesic {opioid}" | "Equianalgesic morphine dose" |
| "Opioid conversion" | "How do I convert between opioids?" |
| "Switch from {opioid1} to {opioid2}" | "Switching from oxycodone to morphine" |
| "What dose of {opioid2} equals {dose} {opioid1}" | "What dose of hydromorphone equals 60mg morphine?" |

---

## Critical Attribution Requirement

**ALL equianalgesic and dose conversion information MUST come from FDA product labels retrieved via API calls.**

### Data Source Priority

1. **PRIMARY (REQUIRED)**: Use `/api/Label/markdown/sections/{DocumentGUID}?sectionCode={loincCode}` to retrieve official FDA label content
2. **SECONDARY**: Use `labelProductIndication.md` reference file for UNII matching only
3. **NEVER**: Generate conversion ratios, formulas, or dosing from training data

### Why This Matters

The user's original query about morphine-to-hydromorphone conversion returned a response with conversion formulas and tables that were generated from AI training data rather than from official FDA product labels. **This is problematic because**:

1. Training data may be outdated or incorrect
2. No source attribution to official labeling
3. Clinical decisions require authoritative sources
4. Regulatory and liability concerns

**ALWAYS** retrieve and cite the actual FDA label content that contains equianalgesic information.

---

## Primary Workflow: Label-Based Conversion Data

### Step 1: Identify Relevant Products

Equianalgesic dosing information is typically found in labels for:

- **Extended-release opioids** (fentanyl transdermal, hydromorphone ER, oxycodone ER, morphine ER)
- **Opioid-tolerant product labels** (contain definitions of opioid-tolerant patients with equianalgesic doses)
- **Dosage and Administration sections** (contain conversion tables)

### Step 2: Reference Data Products with Equianalgesic Information

The following products are known to contain equianalgesic dosing information in their FDA labels:

| Product | UNII | Label Sections with Conversion Data |
|---------|------|-------------------------------------|
| Fentanyl Transdermal System | UF599785JZ | Dosage (34068-7), Unclassified (42229-5) |
| Hydromorphone ER | L960UP2KRW | Dosage (34068-7), Unclassified (42229-5) |
| Morphine Sulfate ER | X3P646A2J0 | Dosage (34068-7), Unclassified (42229-5) |
| Oxycodone ER | C1ENJ2TE6C | Dosage (34068-7), Unclassified (42229-5) |
| Oxymorphone ER | 5Y2EI94NBC | Dosage (34068-7), Unclassified (42229-5) |
| Methadone | 229809935B | Dosage (34068-7), **Unclassified (42229-5) - Contains "Table 1: Conversion Factors"** |
| Buprenorphine Transdermal | 40D3SCR4GZ | Dosage (34068-7), Unclassified (42229-5) |

### Key Equianalgesic Information Location

From the fentanyl transdermal label (and similar ER opioid labels), equianalgesic threshold definitions are:

> "Patients considered opioid-tolerant are those who are taking, for one week or longer, at least 60 mg morphine per day, 25 mcg transdermal fentanyl per hour, 30 mg oral oxycodone per day, 8 mg oral hydromorphone per day, 25 mg oral oxymorphone per day, 60 mg oral hydrocodone per day, or an equianalgesic dose of another opioid."

**This text is from FDA labeling and can be cited.**

---

## Common LOINC Codes for Dosing Information

### ⭐ REQUIRED SECTIONS - ONLY USE THESE TWO

| LOINC Code | Section | Contains | Priority |
|------------|---------|----------|----------|
| **34068-7** | **DOSAGE AND ADMINISTRATION** | **Conversion tables, titration schedules, equianalgesic charts** | **PRIMARY - MUST FETCH** |
| **42229-5** | **SPL UNCLASSIFIED SECTION** | **Subsections under Dosage - detailed conversion tables (e.g., "Table 1: Conversion Factors")** | **PRIMARY - MUST FETCH** |

### ❌ DO NOT USE THESE SECTIONS - They do NOT contain conversion tables

| LOINC Code | Section | Why NOT to Use |
|------------|---------|----------------|
| 34090-1 | CLINICAL PHARMACOLOGY | ❌ Contains pharmacokinetics, NOT conversion tables |
| 43685-7 | WARNINGS AND PRECAUTIONS | ❌ Contains warnings, NOT conversion tables |
| 34067-9 | INDICATIONS AND USAGE | ❌ Contains indications, NOT conversion tables |

**CRITICAL**: Only fetch 34068-7 and 42229-5. Do NOT fetch 34090-1, 43685-7, or 34067-9 for conversion queries.

---

## API Workflow: Retrieve Equianalgesic Data

### ⚠️ MANDATORY: Use Multi-Product Workflow

**DO NOT use single-product queries.** Always use the multi-product workflow described below.

### REQUIRED SECTIONS - ALWAYS FETCH BOTH

For ANY equianalgesic conversion query, you MUST fetch **BOTH sections** from ALL products for **BOTH opioids**:

| Section | LOINC | Why Required |
|---------|-------|--------------|
| **Dosage and Administration** | **34068-7** | **PRIMARY - Main section header with conversion tables** |
| **SPL Unclassified** | **42229-5** | **PRIMARY - Subsections under Dosage with detailed conversion tables, calculation examples** |

### ❌ WRONG - Do NOT Do This

```
# WRONG - Single product, wrong section
GET /api/Label/markdown/sections/{singleGUID}?sectionCode=34090-1  # Clinical Pharmacology

# WRONG - Only fetching Indications from one opioid
GET /api/Label/markdown/sections/{singleGUID}?sectionCode=34067-9  # Indications only

# WRONG - Only fetching from one opioid (must fetch BOTH)
GET /api/Label/ingredient/advanced?substanceNameSearch=buprenorphine  # Missing methadone!
```

### ✅ CORRECT - Always Do This (6-Step Workflow)

```
# CORRECT - Multi-product workflow for BOTH opioids, BOTH sections

Step 1: GET /api/Label/ingredient/advanced?substanceNameSearch={sourceOpioid}&pageSize=50
        outputMapping: { "sourceDocumentGuids": "documentGUID[]" }

Step 2: GET /api/Label/ingredient/advanced?substanceNameSearch={targetOpioid}&pageSize=50
        outputMapping: { "targetDocumentGuids": "documentGUID[]" }

Step 3: GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=34068-7
        (Batch expansion - Dosage and Administration from ALL SOURCE opioid products)

Step 4: GET /api/Label/markdown/sections/{{sourceDocumentGuids}}?sectionCode=42229-5
        (Batch expansion - Unclassified subsections from ALL SOURCE opioid products)
        ⭐ DETAILED CONVERSION TABLES OFTEN HERE (e.g., methadone "Table 1: Conversion Factors")

Step 5: GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=34068-7
        (Batch expansion - Dosage and Administration from ALL TARGET opioid products)

Step 6: GET /api/Label/markdown/sections/{{targetDocumentGuids}}?sectionCode=42229-5
        (Batch expansion - Unclassified subsections from ALL TARGET opioid products)
```

### Why Both 34068-7 AND 42229-5 are REQUIRED

FDA labels structure dosing information hierarchically. The main Dosage section (34068-7) contains header text, but **detailed conversion tables are often in subsections classified as "Unclassified" (42229-5)**:

| What You Need | Where It Is | LOINC |
|---------------|-------------|-------|
| Main dosing header | Dosage and Administration | 34068-7 |
| **Detailed conversion tables** | **SPL Unclassified (subsections)** | **42229-5** |
| **"Table 1: Conversion Factors"** | **SPL Unclassified** | **42229-5** |
| **Calculation examples** | **SPL Unclassified** | **42229-5** |
| "Switching from" instructions | Either section | 34068-7 or 42229-5 |
| Pharmacokinetics (NOT conversions) | Clinical Pharmacology | 34090-1 |

**Example**: Methadone's "Table 1: Conversion Factors to Methadone Hydrochloride Tablets" is in section 42229-5, NOT in 34068-7.

---

## Synthesis Instructions

### IMPORTANT: Handling 404 Errors in Multi-Product Queries

When fetching from multiple products, **some products WILL return 404** because not all FDA labels contain every section type. This is **NORMAL and EXPECTED**.

**During synthesis:**
1. **Ignore 404 errors** - Focus on the successful results that DID return data
2. **Don't list individual 404s** - Just note "X of Y products had the requested section"
3. **Prioritize products with data** - Use the products that returned section content
4. **Check BOTH opioids** - Conversion tables may be in only ONE of the opioid labels

**Example synthesis note:**
```
I found conversion data in 12 buprenorphine product labels. The methadone labels did not contain
section 42229-5 (SPL Unclassified), but the buprenorphine labels contain relevant conversion
information.
```

### Conversion Tables Location Priority

Equianalgesic conversion tables are typically found in the **TARGET** opioid's label (the drug being converted TO):
- "Convert methadone to buprenorphine" → Look in **buprenorphine** labels
- "Convert morphine to fentanyl" → Look in **fentanyl** labels

If the source opioid's labels return 404 for section 42229-5, focus synthesis on the target opioid's data.

### When Label Contains Conversion Data

If the retrieved label sections contain equianalgesic information:

```markdown
## Equianalgesic Dosing Information

Based on the FDA-approved labeling for {ProductName}:

{Quote relevant section from fullSectionText}

**Source**: {ProductName} Label, {SectionTitle} (LOINC {sectionCode})

---

### Data Sources:
- `/api/Label/markdown/sections/{guid}?sectionCode=34068-7` (Dosage and Administration)
- `/api/Label/markdown/sections/{guid}?sectionCode=42229-5` (SPL Unclassified - contains detailed conversion tables)

### View Full Labels:
- [View Full Label ({ProductName1})](/api/Label/generate/{DocumentGUID1}/true)
- [View Full Label ({ProductName2})](/api/Label/generate/{DocumentGUID2}/true)

**Important Clinical Note**: Equianalgesic dose tables are approximations. Individual patient response varies. Consider reducing the calculated equianalgesic dose by 25-50% when switching between opioids to account for incomplete cross-tolerance. Always consult current prescribing information and clinical guidelines.
```

### When Label Does NOT Contain Conversion Data

If the retrieved sections do not contain equianalgesic tables:

```markdown
## Equianalgesic Information Search

I searched the FDA labels for {ProductName1} and {ProductName2} but did not find specific equianalgesic conversion tables in the retrieved sections.

### What Was Searched:
- Dosage and Administration (LOINC 34068-7)
- SPL Unclassified (LOINC 42229-5) - contains detailed conversion tables

### Alternative Approach:
For authoritative equianalgesic dosing information, please consult:
1. The complete prescribing information at the links below
2. Clinical guidelines from AMDG, CDC, or institution-specific protocols

### View Full Labels:
- [View Full Label ({ProductName1})](/api/Label/generate/{DocumentGUID1}/true)
- [View Full Label ({ProductName2})](/api/Label/generate/{DocumentGUID2}/true)

**Note**: This response is based solely on the API results. I cannot provide conversion ratios from training data as they may not reflect current FDA labeling.
```

---

## Response Format Requirements

### JSON Response Structure

```json
{
  "response": "...",
  "dataHighlights": {
    "sourceOpioid": "{name}",
    "targetOpioid": "{name}",
    "conversionDataFound": true|false,
    "sectionsSearched": ["34068-7", "42229-5"]
  },
  "dataReferences": {
    "View Full Label ({ProductName1})": "/api/Label/generate/{DocumentGUID1}/true",
    "View Full Label ({ProductName2})": "/api/Label/generate/{DocumentGUID2}/true"
  },
  "dataSources": {
    "Dosage Section - {ProductName1}": "/api/Label/markdown/sections/{guid}?sectionCode=34068-7",
    "Unclassified Section - {ProductName1}": "/api/Label/markdown/sections/{guid}?sectionCode=42229-5"
  },
  "suggestedFollowUps": [
    "Show warnings for {ProductName}",
    "What are the adverse reactions for {ProductName}?",
    "Find generic alternatives"
  ],
  "warnings": ["Consult healthcare provider for clinical decisions"],
  "isComplete": true
}
```

### Critical Requirements

1. **ALWAYS include `dataSources`** showing which API endpoints were used
2. **ALWAYS include `dataReferences`** with clickable label links
3. **ALWAYS include a clinical disclaimer** about cross-tolerance and individualized dosing
4. **NEVER include conversion ratios or formulas from training data**
5. **If no conversion data found in labels**, clearly state this and provide label links for full prescribing information

---

## Common Opioid UNIIs

| Opioid | UNII | Notes |
|--------|------|-------|
| Morphine Sulfate | X3P646A2J0 | Reference standard for equianalgesic dosing |
| Hydromorphone HCl | L960UP2KRW | Extended-release labels contain conversion tables |
| Fentanyl | UF599785JZ | Transdermal system labels contain MCG/HR conversions |
| Fentanyl Citrate | MUN5LYG46H | Injectable/buccal formulations |
| Oxycodone HCl | C1ENJ2TE6C | Common conversion source |
| Oxymorphone HCl | 5Y2EI94NBC | Higher potency opioid |
| Methadone HCl | 229809935B | Complex pharmacokinetics, special conversion considerations |
| Hydrocodone | 6YKS4Y3WQ7 | Combination product labeling |

---

## Integration with Other Skills

This skill works together with:

- **label**: For detailed section content after initial discovery
- **labelIndicationWorkflow**: For finding opioid products by indication
- **rescueWorkflow**: When primary sections don't contain conversion data

### Escalation Path

If equianalgesic data is not found in the primary sections (34068-7 and 42229-5):

1. **ALREADY SEARCHED**: Dosage and Administration (34068-7) + SPL Unclassified (42229-5) - these are the PRIMARY sections
2. Check if different products for same opioid have more complete content
3. Try other source/target opioid labels - conversion tables may be in either label
4. If still not found, provide label links and recommend consulting full prescribing information

**Note**: Do NOT escalate to 34067-9 (Indications) or 34090-1 (Clinical Pharmacology) for conversion tables - they don't contain them.

---

## CRITICAL: Content Adequacy Detection for Dosing Sections

### Problem: Truncated Dosing Information

Dosage and Administration sections (LOINC 34068-7) are critical for equianalgesic conversions. Some labels have truncated content.

### Truncation Detection

Before using dosing section content, check for these indicators:

| Indicator | Pattern | Example |
|-----------|---------|---------|
| **Trailing colon** | Text ends with `:` | "The recommended dose is:", "Dosing should be:" |
| **Very short content** | `fullSectionText` < 300 characters | Dosing sections should be detailed |
| **Low block count** | `contentBlockCount` < 3 for dosing sections | Conversion tables need multiple blocks |
| **Missing conversion table** | No numeric values or dose ranges | "See full prescribing information" |

### Multi-Product Workflow for Complete Dosing Data

For equianalgesic queries, **ALWAYS use multi-product workflow** to search BOTH the source AND target opioids:

**CRITICAL - Array Extraction Syntax**: Use `[]` suffix to extract ALL values from the array, not just the first one.

**CRITICAL - Dual-Opioid Search + Both Sections**: When converting between two opioids (e.g., methadone to buprenorphine), you MUST:
1. Search for BOTH substances
2. Fetch BOTH 34068-7 (Dosage and Administration) AND 42229-5 (SPL Unclassified) from ALL products for BOTH opioids
3. Conversion tables are often in 42229-5 subsections (e.g., methadone's "Table 1: Conversion Factors")

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
        "pageSize": 50
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
        "pageSize": 50
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

### Why Search BOTH Opioids AND Both Sections?

| Scenario | Where Conversion Data Is Found | Section |
|----------|-------------------------------|---------|
| Any → Methadone | Methadone label's "Table 1: Conversion Factors" | **42229-5** (subsection) |
| Morphine → Buprenorphine | Buprenorphine transdermal label's Dosage subsections | 34068-7 or 42229-5 |
| Morphine → Fentanyl | Fentanyl transdermal label's conversion charts | 34068-7 or 42229-5 |
| Morphine → Hydromorphone | Hydromorphone ER label's Dosage section | 34068-7 or 42229-5 |

**The detailed conversion tables (e.g., "Table 1") are often in SPL Unclassified (42229-5) subsections, NOT in the main Dosage header (34068-7).**

### CRITICAL: Array Extraction vs Single Value

| Syntax | Behavior | Use Case |
|--------|----------|----------|
| `"documentGuid": "documentGUID"` | Extracts ONLY the first value | ❌ WRONG for multi-product |
| `"documentGuids": "documentGUID[]"` | Extracts ALL values as array | ✅ CORRECT for multi-product |

**The `[]` suffix is REQUIRED** for multi-product workflows. Without it, only the first product is queried.

### Aggregating Conversion Data from Multiple Sources

When multiple labels are returned from BOTH source and target opioid searches:

1. **Review ALL sections from BOTH 34068-7 AND 42229-5** - Detailed conversion tables are often in 42229-5 subsections
2. **Prioritize 42229-5 (SPL Unclassified) for detailed tables** - "Table 1: Conversion Factors" is typically HERE
3. **Check BOTH source AND target opioid labels** - Methadone's conversion table is in methadone's label, not in buprenorphine's
4. **Prioritize ER/transdermal labels** - These typically have the most detailed conversion data
   - Example: Buprenorphine transdermal system (BUTRANS) contains morphine-to-buprenorphine conversion tables
   - Example: Fentanyl transdermal system contains morphine-to-fentanyl conversion charts
5. **Look for specific conversion tables** - Search for phrases like "conversion", "equianalgesic", "switching from", "prior opioid", "Table 1"
6. **Cross-reference threshold definitions** - "Opioid-tolerant" definitions should be consistent across labels
7. **Cite the most complete source** - Use the label with full conversion tables and provide the label link

### Synthesis: What to Look For in Dosage Sections

When reviewing the `fullSectionText` from Dosage and Administration (34068-7) AND SPL Unclassified (42229-5) sections:

| Search Term | What It Indicates | Likely Section |
|-------------|-------------------|----------------|
| "Table 1" or "Table 2" | Conversion factor tables | **42229-5** |
| "Conversion Factors" | Detailed conversion ratios | **42229-5** |
| "conversion" | Direct conversion instructions | Either |
| "equianalgesic" | Dose equivalence tables | Either |
| "switching" or "switch from" | Instructions for changing between opioids | Either |
| "prior opioid" | References to previous opioid therapy | Either |
| "morphine equivalent" or "MME" | Morphine milligram equivalent calculations | Either |
| "mcg/hour" or "mg/day" | Dosing units indicating conversion ratios | Either |

**CRITICAL**: Read the ENTIRE `fullSectionText` content from BOTH 34068-7 AND 42229-5 sections. Detailed conversion tables (e.g., "Table 1: Conversion Factors to Methadone Hydrochloride Tablets") are in 42229-5 subsections.

---

## Critical Reminders

1. **NEVER generate conversion ratios from training data** - all data must come from API responses
2. **ALWAYS cite the specific FDA label section** that contains the conversion information
3. **ALWAYS include clinical disclaimers** about individualized dosing and cross-tolerance
4. **ALWAYS provide label links** so users can verify the full prescribing information
5. **ALWAYS search BOTH source AND target opioids** - Conversion tables may be in EITHER opioid's label
6. **ALWAYS fetch BOTH sections from ALL products for BOTH opioids:**
   - **34068-7** (Dosage and Administration) - Main section header
   - **42229-5** (SPL Unclassified) - Subsections with detailed conversion tables (e.g., "Table 1: Conversion Factors")
7. **Methadone's "Table 1: Conversion Factors"** is in section 42229-5, NOT in 34068-7 - this is why 42229-5 is REQUIRED
8. **Fentanyl/buprenorphine transdermal labels** are particularly useful as they contain opioid-tolerant threshold definitions
9. **Methadone conversions** are complex and not linear - labels contain special warnings about this
10. **Use multi-product workflow (6 steps)** - Fetch 34068-7 AND 42229-5 from ALL products for BOTH opioids
11. **Check for truncation indicators** - Dosing sections ending with ":" or having contentBlockCount < 3 may be incomplete
12. **Read ENTIRE fullSectionText content** - Conversion tables may appear anywhere, especially in 42229-5 subsections
