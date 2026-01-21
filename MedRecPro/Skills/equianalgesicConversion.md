# Equianalgesic Conversion Skill

Retrieves FDA-labeled equianalgesic conversion information for opioid medications. Accesses official dosing guidance and conversion tables from FDA-approved labeling for opioid rotation decisions.

---

## CRITICAL: Label Links Are REQUIRED

**Every equianalgesic conversion response MUST include product label links.**

### Non-Negotiable Requirements

1. **Extract `documentGUID` and `productName`** from every API response
2. **Include label links** in the markdown response under "### View Full Labels:"
3. **Populate `dataReferences`** in the JSON output with all product links

### Enforcement

Before responding, verify you have included:
```markdown
### View Full Labels:
- [View Full Label ({SourceOpioidProductName})](/api/Label/original/{SourceDocumentGUID}/true)
- [View Full Label ({TargetOpioidProductName})](/api/Label/original/{TargetDocumentGUID}/true)
```

**If you have `documentGUID` values from the API, you MUST provide label links. No exceptions.**

---

## When to Use This Skill

Trigger this skill when users ask about:

- **Opioid dose conversions**: "Convert morphine to hydromorphone", "What is the equivalent dose?"
- **Equianalgesic dosing**: "Equianalgesic dose", "equivalent dose", "opioid conversion"
- **Switching between opioids**: "Switch from fentanyl to morphine", "changing opioids"
- **Opioid tolerance definitions**: "What is considered opioid tolerant?"
- **Morphine milligram equivalents (MME)**: "MME calculation", "morphine equivalent"

### Query Pattern Examples

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

1. **PRIMARY (REQUIRED)**: Use API endpoints to retrieve official FDA label content
2. **SECONDARY**: Use `labelProductIndication.md` reference file for UNII matching only
3. **NEVER**: Generate conversion ratios, formulas, or dosing from training data

### Why This Matters

- Training data may be outdated or incorrect
- No source attribution to official labeling
- Clinical decisions require authoritative sources
- Regulatory and liability concerns

**ALWAYS** retrieve and cite the actual FDA label content that contains equianalgesic information.

---

## Reference Data: Products with Equianalgesic Information

The following products are known to contain equianalgesic dosing information in their FDA labels:

| Product | UNII | Label Sections with Conversion Data |
|---------|------|-------------------------------------|
| Fentanyl Transdermal System | UF599785JZ | Dosage (34068-7), Unclassified (42229-5) |
| Hydromorphone ER | L960UP2KRW | Dosage (34068-7), Unclassified (42229-5) |
| Morphine Sulfate ER | X3P646A2J0 | Dosage (34068-7), Unclassified (42229-5) |
| Oxycodone ER | C1ENJ2TE6C | Dosage (34068-7), Unclassified (42229-5) |
| Oxymorphone ER | 5Y2EI94NBC | Dosage (34068-7), Unclassified (42229-5) |
| Methadone | 229809935B | Dosage (34068-7), Unclassified (42229-5) - Contains "Table 1: Conversion Factors" |
| Buprenorphine Transdermal | 40D3SCR4GZ | Dosage (34068-7), Unclassified (42229-5) |

### Key Equianalgesic Information Location

From the fentanyl transdermal label (and similar ER opioid labels), equianalgesic threshold definitions are:

> "Patients considered opioid-tolerant are those who are taking, for one week or longer, at least 60 mg morphine per day, 25 mcg transdermal fentanyl per hour, 30 mg oral oxycodone per day, 8 mg oral hydromorphone per day, 25 mg oral oxymorphone per day, 60 mg oral hydrocodone per day, or an equianalgesic dose of another opioid."

**This text is from FDA labeling and can be cited.**

---

## Synthesis Instructions

### Handling 404 Errors in Multi-Product Queries

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
- "Convert methadone to buprenorphine" - Look in **buprenorphine** labels
- "Convert morphine to fentanyl" - Look in **fentanyl** labels

If the source opioid's labels return 404 for section 42229-5, focus synthesis on the target opioid's data.

### When Label Contains Conversion Data

```markdown
## Equianalgesic Dosing Information

Based on the FDA-approved labeling for {ProductName}:

{Quote relevant section from fullSectionText}

**Source**: {ProductName} Label, {SectionTitle} (LOINC {sectionCode})

---

### View Full Labels:
- [View Full Label ({ProductName1})](/api/Label/original/{DocumentGUID1}/true)
- [View Full Label ({ProductName2})](/api/Label/original/{DocumentGUID2}/true)

**Important Clinical Note**: Equianalgesic dose tables are approximations. Individual patient response varies. Consider reducing the calculated equianalgesic dose by 25-50% when switching between opioids to account for incomplete cross-tolerance. Always consult current prescribing information and clinical guidelines.
```

### When Label Does NOT Contain Conversion Data

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
- [View Full Label ({ProductName1})](/api/Label/original/{DocumentGUID1}/true)
- [View Full Label ({ProductName2})](/api/Label/original/{DocumentGUID2}/true)

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
    "View Full Label ({ProductName1})": "/api/Label/original/{DocumentGUID1}/true",
    "View Full Label ({ProductName2})": "/api/Label/original/{DocumentGUID2}/true"
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

1. **ALWAYS include `dataReferences`** with clickable label links
2. **ALWAYS include a clinical disclaimer** about cross-tolerance and individualized dosing
3. **NEVER include conversion ratios or formulas from training data**
4. **If no conversion data found in labels**, clearly state this and provide label links for full prescribing information

---

## Content Adequacy Detection

### Truncation Detection

Before using dosing section content, check for these indicators:

| Indicator | Pattern | Example |
|-----------|---------|---------|
| **Trailing colon** | Text ends with `:` | "The recommended dose is:", "Dosing should be:" |
| **Very short content** | `fullSectionText` < 300 characters | Dosing sections should be detailed |
| **Low block count** | `contentBlockCount` < 3 for dosing sections | Conversion tables need multiple blocks |
| **Missing conversion table** | No numeric values or dose ranges | "See full prescribing information" |

### What to Look For in Dosage Sections

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

---

## Integration with Other Skills

This skill works together with:

- **labelContent**: For detailed section content after initial discovery
- **indicationDiscovery**: For finding opioid products by indication
- **dataRescue**: When primary sections don't contain conversion data

### Escalation Path

If equianalgesic data is not found in the primary sections (34068-7 and 42229-5):

1. Check if different products for same opioid have more complete content
2. Try other source/target opioid labels - conversion tables may be in either label
3. If still not found, provide label links and recommend consulting full prescribing information

**Note**: Do NOT escalate to 34067-9 (Indications) or 34090-1 (Clinical Pharmacology) for conversion tables - they don't contain them.

---

## Critical Reminders

1. **NEVER generate conversion ratios from training data** - all data must come from API responses
2. **ALWAYS cite the specific FDA label section** that contains the conversion information
3. **ALWAYS include clinical disclaimers** about individualized dosing and cross-tolerance
4. **ALWAYS provide label links** so users can verify the full prescribing information
5. **Methadone conversions** are complex and not linear - labels contain special warnings about this
6. **Fentanyl/buprenorphine transdermal labels** are particularly useful as they contain opioid-tolerant threshold definitions

---

## API Interface Reference

For endpoint specifications, workflow details, and JSON examples, see:
[Equianalgesic Conversion API Interface](./interfaces/api/equianalgesic-conversion.md)
