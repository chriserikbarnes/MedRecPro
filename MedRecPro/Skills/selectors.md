# MedRecPro Skill Selectors

This document defines routing rules for skill selection. Use these rules to determine which skill(s) to load for a user query.

---

## Selection Decision Tree

```
Query Analysis
    |
    +-- Contains medical condition/symptom WITHOUT specific product name?
    |       YES --> indicationDiscovery
    |
    +-- Contains specific product name AND asks about details (side effects, dosing, warnings)?
    |       YES --> labelContent
    |
    +-- Contains opioid conversion keywords?
    |       YES --> equianalgesicConversion
    |
    +-- Contains log/activity/performance keywords?
    |       YES --> userActivity (requires auth)
    |
    +-- Contains cache/settings keywords?
    |       YES --> cacheManagement (requires auth)
    |
    +-- Contains auth/login keywords?
    |       YES --> sessionManagement
    |
    +-- Primary query returned empty results?
            YES --> Add dataRescue as supplementary skill
```

---

## Decision Tree Examples

**Ask yourself: Does the query contain a SPECIFIC product name?**

```
Query: "What can be used as a surgical analgesic?"
  +-- Contains specific product name? NO
  +-- Contains condition/symptom/therapeutic need? YES ("surgical analgesic")
  +-- SELECT: indicationDiscovery

Query: "What are the side effects of Lipitor?"
  +-- Contains specific product name? YES ("Lipitor")
  +-- Asking about label DETAILS of that product? YES ("side effects")
  +-- SELECT: labelContent

Query: "What helps with pain?"
  +-- Contains specific product name? NO
  +-- Contains condition/symptom? YES ("pain")
  +-- SELECT: indicationDiscovery

Query: "Show me the dosage for metformin"
  +-- Contains specific product name? YES ("metformin")
  +-- Asking about label DETAILS of that product? YES ("dosage")
  +-- SELECT: labelContent
```

**NEVER use labelContent when:**
- The query does NOT mention a specific product name
- The query is asking to FIND/DISCOVER products for a condition
- The query uses phrases like "what can be used for", "what helps with", "options for"

---

## Keyword Mappings

### indicationDiscovery

**Primary Keywords**
- what helps with, what can be used for, options for, treatment for, products for
- what can I take, I have, feeling, alternatives to

**Condition Keywords**
- depression, anxiety, pain, hypertension, diabetes, cholesterol
- allergy, allergies, seasonal, rhinitis, asthma
- cancer, carcinoma, breast cancer, prostate cancer
- seizure, epilepsy, neuropathy, migraine

**Surgical/Anesthesia Keywords**
- surgical, analgesic, anesthetic, anesthesia, perioperative, surgery, operative
- sedation, sedative, induction, intubation, muscle relaxant, neuromuscular
- local anesthetic, general anesthesia, regional anesthesia, epidural
- spinal anesthesia, procedural sedation, preoperative, postoperative, intraoperative

**Selection Rule**: If query mentions a condition/symptom WITHOUT a specific product name, select indicationDiscovery.

---

### labelContent

**Primary Keywords**
- side effects, adverse effects, warnings, precautions
- contraindications, dosage, dosing, how to take
- interactions, drug interactions, overdose
- boxed warning, black box, pharmacology

**Product Keywords**
- drug, product, medication, tablet, capsule, injection
- NDC, manufacturer, labeler, application number
- ANDA, NDA, BLA, UNII, SPL

**Selection Rule**: If query mentions a SPECIFIC product AND asks about label details, select labelContent.

---

### equianalgesicConversion

**Keywords**
- equianalgesic, opioid conversion, morphine equivalent, MME
- convert morphine, convert hydromorphone, convert fentanyl
- convert oxycodone, convert methadone, convert buprenorphine
- switching opioids, equivalent dose, opioid switch

**Selection Rule**: Any query about opioid dose conversion or equivalence.

---

### userActivity

**Keywords**
- log, logs, application log, error, warning, debug
- activity, user activity, what did user
- endpoint, performance, response time, controller
- statistics, how fast, monitoring, diagnostic

**Selection Rule**: Log viewing, user activity audit, or performance analysis. **Requires authentication.**

---

### cacheManagement

**Keywords**
- cache, clear cache, reset cache, flush cache, invalidate

**Selection Rule**: Cache operations. **Requires authentication.**

---

### sessionManagement

**Keywords**
- login, logout, authenticate, sign in, sign out
- profile, current user, who am I, oauth

---

### dataRescue

**Keywords**
- not found, empty results, where else, alternative
- rescue, fallback, text search, description section

**Selection Rule**: Add as supplementary skill when primary skill returns empty results.

---

## Priority Rules

1. **Condition queries always use indicationDiscovery** - Never use labelContent for "what helps with X" queries
2. **Specific product + detail = labelContent** - Only after product is identified
3. **Opioid conversion takes precedence** - Over general indication queries for opioid medications
4. **Admin skills require authentication** - Check auth state before selecting userActivity or cacheManagement
5. **dataRescue supplements, doesn't replace** - Always used alongside another skill

---

## Skill Combinations

| Query Pattern | Skills |
|--------------|--------|
| "What helps with depression?" | indicationDiscovery |
| "What are the side effects of Lipitor?" | labelContent |
| "Convert morphine to hydromorphone" | equianalgesicConversion |
| "What helps with depression and what are the side effects?" | indicationDiscovery + labelContent |
| "Show me application logs" | userActivity |
| "Inactive ingredients not found" (retry) | labelContent + dataRescue |

---

## Multi-Product Workflow Triggers

Use multi-product workflow (fetch from multiple labels) when:

| Query Type | Reason | Skills |
|------------|--------|--------|
| "What are the contraindications for {ingredient}?" | Safety sections may be truncated | labelContent |
| "What are ALL the warnings for {drug}?" | User explicitly wants comprehensive data | labelContent |
| "How many {ingredient} products are available?" | User wants product count + comparison | indicationDiscovery |
| Generic ingredient queries (not brand-specific) | Different labels may have different content completeness | All section-based skills |
| Opioid conversion queries | Dosing tables may be truncated in some labels | equianalgesicConversion |
| "Comprehensive information about {drug}" | Multiple sections needed, some may be truncated | labelContent |

### Multi-Product Workflow Pattern

1. Search for multiple products with same active ingredient (`pageSize=50`)
2. Extract all `documentGUID[]` values (array extraction - **the `[]` suffix is REQUIRED**)
3. Fetch sections from ALL products (batch expansion creates N API calls)
4. During synthesis, select most complete content and aggregate unique information

### Array Extraction Syntax

| outputMapping Syntax | Behavior | Outcome |
|---------------------|----------|---------|
| `"documentGuid": "documentGUID"` | Extracts only FIRST value | Only 1 product queried - **WRONG** |
| `"documentGuids": "documentGUID[]"` | Extracts ALL values | All products queried - **CORRECT** |

**Common Mistake**: Using `"documentGuid": "documentGUID"` (without `[]`) causes the workflow to query only the first product, missing products with complete data.

---

## Authentication Check

Before selecting admin skills:

```
IF query matches userActivity OR cacheManagement keywords:
    IF isAuthenticated == false:
        RESPOND: "Please sign in to access this feature"
        SELECT: sessionManagement
    ELSE:
        SELECT: requested skill
```

---

## Response Format

```json
{
  "success": true,
  "selectedSkills": ["skillName1", "skillName2"],
  "explanation": "Brief explanation of selection",
  "isDirectResponse": false
}
```

### Direct Response Cases

For general help questions that don't require API calls:

```json
{
  "success": true,
  "selectedSkills": [],
  "isDirectResponse": true,
  "directResponse": "Answer text...",
  "explanation": "Query answered directly"
}
```

---

## Interface References

After skill selection, load the corresponding interface document:

| Skill | Interface Document |
|-------|-------------------|
| indicationDiscovery | [interfaces/api/indication-discovery.md](./interfaces/api/indication-discovery.md) |
| labelContent | [interfaces/api/label-content.md](./interfaces/api/label-content.md) |
| equianalgesicConversion | [interfaces/api/equianalgesic-conversion.md](./interfaces/api/equianalgesic-conversion.md) |
| userActivity | [interfaces/api/user-activity.md](./interfaces/api/user-activity.md) |
| cacheManagement | [interfaces/api/cache-management.md](./interfaces/api/cache-management.md) |
| sessionManagement | [interfaces/api/session-management.md](./interfaces/api/session-management.md) |
| dataRescue | [interfaces/api/data-rescue.md](./interfaces/api/data-rescue.md) |
| pharmacologicClass | [interfaces/api/pharmacologic-class.md](./interfaces/api/pharmacologic-class.md) |
| retryFallback | [interfaces/api/retry-fallback.md](./interfaces/api/retry-fallback.md) |

---

## Synthesis Rules Reference

After API calls complete, apply synthesis rules from [interfaces/synthesis-rules.md](./interfaces/synthesis-rules.md).
