# MedRecPro Skill Selectors

This document defines routing rules for skill selection. Use these rules to determine which skill(s) to load for a user query.

---

## Selection Decision Tree

```
Query Analysis
    |
    +-- Asks about database contents, inventory, or "what products do you have"?
    |       YES --> inventorySummary (FIRST-LINE for these questions)
    |
    +-- Contains drug class terminology WITHOUT specific product name?
    |       YES --> pharmacologicClassSearch
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
    +-- Asks about system capabilities, how to use features, or general help?
    |       YES --> isDirectResponse = true (with contextual help content)
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

Query: "What types of data can you help me find?"
  +-- Contains specific product name? NO
  +-- Contains condition/symptom? NO
  +-- Contains drug class? NO
  +-- Asks about system capabilities or how to use features? YES
  +-- SELECT: isDirectResponse = true (capabilities overview)

Query: "How do I import SPL files?"
  +-- Contains specific product name? NO
  +-- Asks about how to use a feature? YES ("how do I import")
  +-- SELECT: isDirectResponse = true (import instructions)
```

**NEVER use labelContent when:**
- The query does NOT mention a specific product name
- The query is asking to FIND/DISCOVER products for a condition
- The query uses phrases like "what can be used for", "what helps with", "options for"

---

## Keyword Mappings

### inventorySummary

**Primary Keywords**
- what products do you have, what drugs do you have, what medications do you have
- what is available, what is in the database, what do you have
- how many products, how many drugs, how many medications, how many labels
- database contents, database inventory, database summary
- available products, available drugs, available medications
- summarize available, summarize products, summarize inventory
- list available, total products, total drugs
- top labelers, top manufacturers, top producers, who makes the most
- top drug classes, top pharmacologic classes

**Context Keywords**
- inventory, summary, overview, statistics, totals, counts
- database, available, have, contents

**Selection Rule**: If query asks about what products/drugs/medications are in the database or available, use inventorySummary. This is the FIRST-LINE approach for inventory questions. **Do NOT use paginated product search endpoints** - they give incomplete impressions of database size.

**CRITICAL: Execute Immediately**: When this skill is selected, call the `/api/Label/inventory/summary` endpoint IMMEDIATELY and return the formatted results. **DO NOT** describe the process or ask for confirmation. **DO NOT** say "Would you like me to retrieve..." - just retrieve it and present the results.

**IMPORTANT**: This skill takes precedence over labelContent for general "what do you have" questions. Only use labelContent when a SPECIFIC product name is mentioned.

---

### pharmacologicClassSearch

**Primary Keywords**
- what medications are, what drugs are, which medications are, which drugs are
- list of, show me, find all, what are the
- drugs in class, medications in class, products in class
- drug class, medication class, therapeutic class, pharmacologic class

**Drug Class Keywords**
- beta blocker, beta-blocker, adrenergic blocker, beta adrenergic
- ACE inhibitor, angiotensin, converting enzyme
- SSRI, serotonin reuptake, antidepressant class
- statin, HMG-CoA, cholesterol lowering
- calcium channel blocker, CCB
- PPI, proton pump inhibitor
- opioid class, narcotic class, opioid agonist
- benzodiazepine, benzo, anxiolytic class
- antibiotic class, antibacterial, antimicrobial class
- antipsychotic, neuroleptic
- anticoagulant, blood thinner class
- diuretic, water pill class
- NSAID, anti-inflammatory class

**Selection Rule**: If query asks about a DRUG CLASS (e.g., "what medications are beta blockers", "list ACE inhibitors") WITHOUT a specific product name, select pharmacologicClassSearch. This is the FIRST-LINE approach for drug class queries.

**IMPORTANT**: Distinguish from indicationDiscovery:
- "What helps with high blood pressure?" --> indicationDiscovery (condition-based)
- "What are the beta blockers?" --> pharmacologicClassSearch (class-based)
- "Show me ACE inhibitors" --> pharmacologicClassSearch (class-based)

---

### indicationDiscovery

**Primary Keywords**
- what helps with, what can help, what can be used for, options for, treatment for, products for
- what can I take, I have, feeling, feeling down, alternatives to
- what treats, medicine for, drug for, what product, which medication
- generic alternative, similar to, like lipitor, like prozac, equivalent to
- generics, same ingredient, indicated for, what is indicated

**Condition Keywords**
- depression, anxiety, pain, pain relief, hypertension, high blood pressure, diabetes, cholesterol
- allergy, allergies, seasonal, rhinitis, asthma
- cancer, carcinoma, breast cancer, prostate cancer
- seizure, epilepsy, neuropathy, neuropathic, nerve pain, migraine
- shingles, postherpetic, neuralgia
- condition, symptom, disease, therapeutic, indication

**Surgical/Anesthesia Keywords**
- surgical, analgesic, anesthetic, anesthesia, perioperative, surgery, operative
- sedation, sedative, induction, intubation, muscle relaxant, neuromuscular
- local anesthetic, general anesthesia, regional anesthesia, epidural
- spinal anesthesia, procedural sedation, preoperative, postoperative, intraoperative

**Selection Rule**: If query mentions a condition/symptom WITHOUT a specific product name, select indicationDiscovery. This is the FIRST-LINE approach for condition-based queries.

---

### labelContent

**Primary Keywords**
- side effects, adverse effects, warnings, precautions
- contraindications, dosage, dose, dosing, how to take
- interactions, drug interactions, overdose
- boxed warning, black box, pharmacology, pharmacologic
- prescribing, pharmaceutical, therapeutic, class

**Product Keywords**
- drug, product, ingredient, medication, medicine, tablet, capsule, injection
- NDC, manufacturer, labeler, application number
- ANDA, NDA, BLA, UNII, SPL, FDA, document
- label, section, import, export

**General Information Keywords**
- tell me about, what is, information about, details about
- know about, learn about

**Selection Rule**: If query mentions a SPECIFIC product AND asks about label details, select labelContent. Also use for general "tell me about {drug}" queries.

---

### equianalgesicConversion

**Core Conversion Terms**
- equianalgesic, opioid conversion, morphine equivalent, MME
- dose conversion, switching opioids, equivalent dose, opioid switch

**Conversion Action Phrases**
- convert from, convert to, conversion from, conversion to
- convert morphine, convert hydromorphone, convert fentanyl
- convert oxycodone, convert methadone, convert buprenorphine

**Drug-to-Drug Conversion Patterns (X to Y)**
- morphine to hydromorphone, hydromorphone to morphine
- fentanyl to morphine, morphine to fentanyl
- oxycodone to morphine, morphine to oxycodone
- oxycodone to buprenorphine, buprenorphine to oxycodone
- oxycodone to hydromorphone, hydromorphone to oxycodone
- oxycodone to fentanyl, fentanyl to oxycodone
- methadone to buprenorphine, buprenorphine to methadone
- methadone to morphine, morphine to methadone
- hydromorphone to buprenorphine, buprenorphine to hydromorphone
- fentanyl to buprenorphine, buprenorphine to fentanyl

**Selection Rule**: Any query about opioid dose conversion or equivalence.

**IMPORTANT**: Do NOT match on standalone drug names (e.g., "buprenorphine", "methadone", "opioid"). These should route to labelContent for general drug information. Only match when conversion-specific context is present (e.g., "convert from X", "X to Y").

---

### userActivity

**Log Keywords**
- log, logs, error log, warning log, application log, admin log
- debug, trace, diagnostic, log statistics, log categor, log level
- show errors, show warnings, recent errors, what errors

**Activity Keywords**
- activity, user activity, activity log, what did user, user's activity
- what did, how many times

**Performance Keywords**
- endpoint performance, endpoint stats, response time
- controller performance, api performance, how fast
- performance for, performance of, statistics, monitoring

**Selection Rule**: Log viewing, user activity audit, or performance analysis. **Requires authentication.**

---

### cacheManagement

**Keywords**
- cache, clear cache, reset cache, flush cache, invalidate

**Selection Rule**: Cache operations. **Requires authentication.**

---

### sessionManagement

**Keywords**
- login, logout, authenticate, authenticated, sign in, sign out
- profile, current user, who am i, am i logged in, context
- authentication status, my account, my session
- oauth, google login, microsoft login
- conversation, interpret, synthesize, chat

---

### helpAndCapabilities

**Capabilities Keywords**
- what can you do, what are you capable of, what types of data
- what can you help, what data can you, what information can you
- what features, capabilities, abilities, what do you offer
- help me find, what can I search, what can I look up
- tell me about this system, what is this, what does this do

**How-To Keywords**
- how do I, how to, how can I, show me how
- instructions, guide, tutorial, help with, need help
- getting started, where do I start, how to use
- explain how, walk me through

**Import/Upload Keywords**
- import SPL, upload files, how to import, add data, load data
- file upload, drag and drop, import workflow
- populate database, add labels, load labels

**Search Help Keywords**
- how to search, how do I find, how to look up
- search tips, search examples, query help

**Selection Rule**: If query asks about system capabilities, general help, how-to instructions, or feature usage WITHOUT requesting actual data retrieval, set `isDirectResponse: true` and populate `directResponse` with helpful content. **No API calls or skills are needed.**

**CRITICAL: Direct Response Content Guidance**

When this pattern matches, generate a `directResponse` following these guidelines:

#### For Capabilities Questions ("What types of data can you help me find?", "What can you do?"):

Describe the searchable data and features:
- **Product Search**: Search drug products by brand name or generic name
- **Ingredient Search**: Find products by active ingredient name or UNII code
- **Manufacturer/Labeler Search**: Browse products by manufacturer
- **Pharmacologic Class Search**: Find drugs by class (e.g., "beta blockers", "SSRIs", "statins")
- **Condition/Indication Search**: Find drugs that treat specific conditions (e.g., "What helps with depression?")
- **Label Content**: Retrieve specific sections from FDA drug labels — side effects, dosing, warnings, contraindications, drug interactions
- **Opioid Conversion**: Equianalgesic dose conversion between opioid medications
- **Inventory Browse**: View database statistics, top manufacturers, and drug class summaries

Include 2-3 example queries the user can try, such as:
- "Find beta blockers"
- "What helps with high blood pressure?"
- "What are the side effects of Lipitor?"

#### For Import Questions ("How do I import SPL files?"):

Provide these instructions:
1. Click the **attach/paperclip button** (📎) at the bottom of the chat input area
2. Select one or more **SPL ZIP files** (available from [DailyMed](https://dailymed.nlm.nih.gov))
3. Optionally type a message, then **send** to start the import
4. The import processes automatically — data is available immediately after completion
5. You can import multiple ZIP files at once

#### For Search Help Questions ("How do I search?", "Show me how to search for drugs by ingredient"):

Explain that the user can search using natural language:
- Just type what you're looking for in the chat
- Examples: "Find drugs containing aspirin", "What are beta blockers?", "Show me pain medications"
- Can search by: product name, ingredient, manufacturer, drug class, or medical condition
- Can ask for specific label sections: "What are the side effects of Lipitor?"

**IMPORTANT**: Responses must be specific and actionable. Do NOT return generic "I can help you with many things" responses. Always include concrete examples or step-by-step instructions.

---

### dataRescue

**Keywords**
- not found, empty results, where else, alternative
- rescue, fallback, text search, description section
- inactive ingredient, excipient, not available
- couldn't find, not in, extract from text

**Selection Rule**: Add as supplementary skill when primary skill returns empty results.

---

## Priority Rules

1. **Help/capabilities questions use direct response** - "What can you do?", "How do I import?" answered immediately without API calls
2. **Drug class queries use pharmacologicClassSearch** - "What medications are beta blockers" routes to class search
3. **Condition queries use indicationDiscovery** - "What helps with high blood pressure" routes to indication search
4. **Specific product + detail = labelContent** - Only after product is identified
5. **Opioid conversion takes precedence** - Over general indication queries for opioid medications
6. **Admin skills require authentication** - Check auth state before selecting userActivity or cacheManagement
7. **dataRescue supplements, doesn't replace** - Always used alongside another skill

**Key Distinction - Class vs Condition:**
- "beta blockers" = drug CLASS --> pharmacologicClassSearch
- "high blood pressure" = medical CONDITION --> indicationDiscovery

---

## Skill Combinations

| Query Pattern | Skills |
|--------------|--------|
| "What products do you have?" | inventorySummary |
| "How many drugs are in the database?" | inventorySummary |
| "Summarize the available labels" | inventorySummary |
| "What medications are beta blockers?" | pharmacologicClassSearch |
| "List ACE inhibitors" | pharmacologicClassSearch |
| "What helps with depression?" | indicationDiscovery |
| "What are the side effects of Lipitor?" | labelContent |
| "Convert morphine to hydromorphone" | equianalgesicConversion |
| "What helps with depression and what are the side effects?" | indicationDiscovery + labelContent |
| "Show me beta blockers and their warnings" | pharmacologicClassSearch + labelContent |
| "Show me application logs" | userActivity |
| "Inactive ingredients not found" (retry) | labelContent + dataRescue |
| "What types of data can you help me find?" | Direct response (capabilities overview) |
| "How do I import SPL files?" | Direct response (upload instructions) |
| "How do I search for drugs?" | Direct response (search guidance) |
| "What can you do?" | Direct response (system overview) |

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

For help, capabilities, and how-to questions that don't require API calls, return a direct response instead of selecting skills. This avoids unnecessary API calls and provides immediate, helpful answers.

**When to use direct responses:**
- System capabilities questions ("What can you do?", "What types of data can you help me find?")
- How-to/feature usage questions ("How do I import SPL files?", "How do I search?")
- General help or getting-started questions
- Questions about the system itself (NOT about drug data)

**When NOT to use direct responses:**
- Questions that request actual data ("What products do you have?" → inventorySummary)
- Questions mentioning specific drugs, conditions, or classes → route to appropriate skill
- Questions that can be answered with database data

**Response format:**

```json
{
  "success": true,
  "selectedSkills": [],
  "isDirectResponse": true,
  "directResponse": "Specific, actionable answer with examples...",
  "explanation": "Query answered directly"
}
```

**Content quality requirements:**
- Be specific and actionable — never return generic "I can help you with many things" responses
- Include concrete examples or step-by-step instructions
- Reference UI elements when relevant (e.g., the attach/paperclip button for imports)
- End with 2-3 suggested queries the user can try next

**Examples:**

```
Query: "What types of data can you help me find?"
→ isDirectResponse: true
→ directResponse: Describe searchable data types (products, ingredients, manufacturers, drug classes, conditions) and features (label sections, opioid conversion). Include example queries.

Query: "How do I import SPL files?"
→ isDirectResponse: true
→ directResponse: Step-by-step instructions using the attach button, supported formats (SPL ZIP from DailyMed), and what happens after import.

Query: "How do I search for drugs by ingredient?"
→ isDirectResponse: true
→ directResponse: Explain natural language search with examples like "Find drugs containing aspirin" or "Search for atorvastatin".
```

See the [helpAndCapabilities](#helpandcapabilities) keyword section for detailed content guidance.

---

## Interface References

After skill selection, load the corresponding interface document:

| Skill | Interface Document |
|-------|-------------------|
| inventorySummary | [interfaces/api/label-content.md](./interfaces/api/label-content.md) (Inventory Summary section) |
| pharmacologicClassSearch | [interfaces/api/pharmacologic-class.md](./interfaces/api/pharmacologic-class.md) |
| indicationDiscovery | [interfaces/api/indication-discovery.md](./interfaces/api/indication-discovery.md) |
| labelContent | [interfaces/api/label-content.md](./interfaces/api/label-content.md) |
| equianalgesicConversion | [interfaces/api/equianalgesic-conversion.md](./interfaces/api/equianalgesic-conversion.md) |
| userActivity | [interfaces/api/user-activity.md](./interfaces/api/user-activity.md) |
| cacheManagement | [interfaces/api/cache-management.md](./interfaces/api/cache-management.md) |
| sessionManagement | [interfaces/api/session-management.md](./interfaces/api/session-management.md) |
| dataRescue | [interfaces/api/data-rescue.md](./interfaces/api/data-rescue.md) |
| retryFallback | [interfaces/api/retry-fallback.md](./interfaces/api/retry-fallback.md) |

---

## Synthesis Rules Reference

After API calls complete, apply synthesis rules from [interfaces/synthesis-rules.md](./interfaces/synthesis-rules.md).

### CRITICAL: Label Links Are MANDATORY - ENFORCEMENT

**Every synthesized response MUST include product label links.** This is the **#1 requirement**.

### Pre-Response Checklist (REQUIRED)

Before finalizing ANY response that retrieved product data:

1. ✓ Extract ALL `documentGUID` values from API responses
2. ✓ Extract ALL `productName` values from API responses
3. ✓ Include `### View Full Labels:` section in markdown response
4. ✓ Populate `dataReferences` with ALL product links

### Required Link Format

```markdown
### View Full Labels:
- [View Full Label ({ProductName1})](/api/Label/original/{DocumentGUID1}/true)
- [View Full Label ({ProductName2})](/api/Label/original/{DocumentGUID2}/true)
```

**If product data was retrieved, label links are required. A response without label links is INCOMPLETE.**
