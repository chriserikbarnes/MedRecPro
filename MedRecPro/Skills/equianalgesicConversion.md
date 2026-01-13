# Equianalgesic Conversion Skills

This document provides instructions for handling opioid dose conversion and equianalgesic dosing queries. Use this skill when users ask about converting between opioid medications or need equianalgesic dosing information.

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
| Fentanyl Transdermal System | UF599785JZ | Indications (34067-9), Dosage (34068-7) |
| Hydromorphone ER | L960UP2KRW | Dosage and Administration (34068-7) |
| Morphine Sulfate ER | X3P646A2J0 | Dosage and Administration (34068-7) |
| Oxycodone ER | C1ENJ2TE6C | Dosage and Administration (34068-7) |
| Oxymorphone ER | 5Y2EI94NBC | Dosage and Administration (34068-7) |
| Methadone | 229809935B | Dosage and Administration (34068-7) |

### Key Equianalgesic Information Location

From the fentanyl transdermal label (and similar ER opioid labels), equianalgesic threshold definitions are:

> "Patients considered opioid-tolerant are those who are taking, for one week or longer, at least 60 mg morphine per day, 25 mcg transdermal fentanyl per hour, 30 mg oral oxycodone per day, 8 mg oral hydromorphone per day, 25 mg oral oxymorphone per day, 60 mg oral hydrocodone per day, or an equianalgesic dose of another opioid."

**This text is from FDA labeling and can be cited.**

---

## Common LOINC Codes for Dosing Information

| LOINC Code | Section | Contains |
|------------|---------|----------|
| 34068-7 | DOSAGE AND ADMINISTRATION | Conversion tables, titration schedules, equianalgesic charts |
| 34067-9 | INDICATIONS AND USAGE | Opioid-tolerant definitions with equianalgesic thresholds |
| 34090-1 | CLINICAL PHARMACOLOGY | Pharmacokinetic comparisons |
| 42229-5 | SPL UNCLASSIFIED SECTION | May contain supplemental equianalgesic tables |
| 43685-7 | WARNINGS AND PRECAUTIONS | Cross-tolerance warnings, dose reduction recommendations |

---

## API Workflow: Retrieve Equianalgesic Data

### Step 1: Get Product Labels by UNII

For the source opioid (e.g., morphine):

```
GET /api/Label/product/latest?unii={sourceUNII}&pageSize=3
```

For the target opioid (e.g., hydromorphone):

```
GET /api/Label/product/latest?unii={targetUNII}&pageSize=3
```

### Step 2: Retrieve Dosage Sections (REQUIRED)

**CRITICAL**: Always retrieve the Dosage and Administration section which contains conversion tables:

```
GET /api/Label/markdown/sections/{DocumentGUID}?sectionCode=34068-7
```

Also retrieve Indications for opioid-tolerant definitions:

```
GET /api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9
```

### Step 3: Extract Conversion Data from Label Content

The `fullSectionText` response will contain the official FDA conversion information. Parse this for:

- Equianalgesic dose tables
- Conversion factors
- Opioid-tolerant patient definitions
- Cross-tolerance warnings

---

## Synthesis Instructions

### When Label Contains Conversion Data

If the retrieved label sections contain equianalgesic information:

```markdown
## Equianalgesic Dosing Information

Based on the FDA-approved labeling for {ProductName}:

{Quote relevant section from fullSectionText}

**Source**: {ProductName} Label, {SectionTitle} (LOINC {sectionCode})

---

### Data Sources:
- `/api/Label/markdown/sections/{guid}?sectionCode=34068-7`
- `/api/Label/markdown/sections/{guid}?sectionCode=34067-9`

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
- Indications and Usage (LOINC 34067-9)

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
    "sectionsSearched": ["34068-7", "34067-9"]
  },
  "dataReferences": {
    "View Full Label ({ProductName1})": "/api/Label/generate/{DocumentGUID1}/true",
    "View Full Label ({ProductName2})": "/api/Label/generate/{DocumentGUID2}/true"
  },
  "dataSources": {
    "Dosage Section - {ProductName1}": "/api/Label/markdown/sections/{guid}?sectionCode=34068-7",
    "Indications Section - {ProductName1}": "/api/Label/markdown/sections/{guid}?sectionCode=34067-9"
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

If equianalgesic data is not found in Dosage section:

1. Try Indications section (34067-9) for opioid-tolerant definitions
2. Try Clinical Pharmacology section (34090-1) for PK comparisons
3. Try SPL Unclassified section (42229-5) for supplemental tables
4. If still not found, provide label links and recommend consulting full prescribing information

---

## Critical Reminders

1. **NEVER generate conversion ratios from training data** - all data must come from API responses
2. **ALWAYS cite the specific FDA label section** that contains the conversion information
3. **ALWAYS include clinical disclaimers** about individualized dosing and cross-tolerance
4. **ALWAYS provide label links** so users can verify the full prescribing information
5. **Fentanyl transdermal labels** are particularly useful as they contain opioid-tolerant threshold definitions with multiple opioid equivalents
6. **Methadone conversions** are complex and not linear - labels contain special warnings about this
