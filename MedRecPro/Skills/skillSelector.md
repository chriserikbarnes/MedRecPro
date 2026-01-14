# MedRecPro AI Skill Selector Manifest

This lightweight manifest enables efficient two-stage routing for AI skill selection. Use this document to determine which skill(s) to load for a user query.

---

## Available Skills

### labelIndicationWorkflow
**Product discovery by medical condition, disease, symptom, or therapeutic need.**

Use for queries about:
- Finding products for a specific condition (depression, hypertension, diabetes)
- Symptom-based product searches (pain, anxiety, insomnia)
- Treatment options and alternatives
- "What helps with X?" questions

**Keywords**: what helps with, I have, options for, treatment for, products for, what can I take, what can be used for, alternatives to, feeling down, high blood pressure, diabetes, depression, anxiety, pain, hypertension, cholesterol, condition, symptom, disease, therapeutic, indication, what product, which medication, generics, same ingredient, indicated for, what is indicated, shingles, postherpetic, neuralgia, neuropathic, neuropathy, seizure, epilepsy, nerve pain, cancer, carcinoma, breast cancer, prostate cancer, estrogen sensitive, hormone receptor, malignancy, tumor, oncology, allergy, allergies, allergic, seasonal allergies, antihistamine, rhinitis, sneezing, hay fever, asthma, respiratory, surgical, analgesic, anesthetic, anesthesia, perioperative, surgery, operative, sedation, sedative, induction, intubation, muscle relaxant, neuromuscular, local anesthetic, general anesthesia, regional anesthesia, epidural, spinal anesthesia, procedural sedation, preoperative, postoperative, intraoperative

**Process**:
1. **Load reference data**: Use `labelProductIndication.md` to match condition keywords to UNII(s) - this provides UNII codes for API calls
2. **Get latest labels**: Call `/api/Label/product/latest?unii={UNII}` - this returns **ProductName**, **SubstanceName**, and **DocumentGUID** for each matched product
3. **Get label content (PREFERRED)**: Call `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9` - use sectionCode filter for token optimization
4. **Build label links**: Use the DocumentGUID from step 2 to construct: `/api/Label/generate/{DocumentGUID}/true`
5. **Find alternatives** (optional): Use `/api/Label/product/related?sourceDocumentGuid={guid}` to find generics and related products

**CRITICAL - Use Markdown Sections Endpoint**:
- Step 3 uses `/api/Label/markdown/sections/{DocumentGUID}?sectionCode={loincCode}` for token-optimized content retrieval
- Use `sectionCode=34067-9` for Indications and Usage
- Use `sectionCode=34089-3` for Description (drug class info)
- Returns `fullSectionText` with pre-formatted markdown ready for AI consumption
- Reduces payload from ~88KB (all sections) to ~1-2KB per section

**CRITICAL - Full Label Content Required for any product summarization**:
- Step 3 is REQUIRED, not optional - always call `/api/Label/smarkdown/sections/{DocumentGUID}` without a sectionCode parameter
- This returns ALL sections of the label including Indications, Description, Dosage, Warnings, etc.
- Use this data for product summaries, NOT training data or the reference file alone

**IMPORTANT - Label Link Construction**:
- When UNII/ingredient data is available from step 2, **ALWAYS** use the DocumentGUID returned by `/api/Label/product/latest` to build label links
- **DO NOT** use alternative methods (regex extraction, path parsing, or other endpoints) to construct label links when the UNII-based workflow provides DocumentGUIDs
- The correct label link format is: `/api/Label/generate/{DocumentGUID}/true`
- Include the ProductName from the API response in the link display text for user clarity

**REQUIRED - View Full Labels Section**:
Your response MUST include a "View Full Labels:" section with clickable links to render each product's complete FDA label.

For each product returned by `/api/Label/product/latest`:
1. Extract the `productName` and `documentGUID` from the response
2. Create a markdown link using the **ACTUAL product name** from the API response
3. **DO NOT use generic placeholders** like "Prescription Drug" or "OTC Drug"

Example response format (using actual product names from API):
```
View Full Labels:
• [view Full Label (Atorvastatin Calcium)](/api/Label/generate/48173596-6909-f52b-e063-6294a90a8f22/true)
• [view Full Label (Rosuvastatin)](/api/Label/generate/abc12345-1234-5678-9abc-def012345678/true)
```

**WRONG** (do not use generic terms):
```
• [view Full Label (Prescription Drug)](/api/Label/generate/48173596.../true)  ❌ WRONG
```

This section is SEPARATE from "Data sources:" - Data sources show the API calls used, while "View Full Labels:" provides direct links to the rendered label documents.

**Reference Data File**: `C:\Users\chris\Documents\Repos\MedRecPro\Skills\labelProductIndication.md`
- Format: `ProductNames|UNII|IndicationsSummary` (entries separated by `---`)
- Search the IndicationsSummary field for condition keywords
- Extract UNII codes for API calls

**Note**: This skill uses the reference file for better condition-to-product matching than the API endpoint alone. The `/api/Label/product/latest` endpoint is the authoritative source for DocumentGUIDs when working with UNII-based queries.

**CRITICAL - NO TRAINING DATA**:
- Response content MUST come ONLY from API results and the loaded `labelProductIndication.md` file
- DO NOT generate drug descriptions, mechanisms, or classifications from training data
- List ONLY products returned by the API with their exact `productName` and `activeIngredient` values
- If the API returns 5 products, list exactly those 5 products - no more, no less

---

### label
**Primary pharmaceutical labeling and SPL document operations.**

Use for queries about:
- Drug/product searches and lookups
- Active and inactive ingredient queries
- NDC (National Drug Code) searches
- Manufacturer/labeler searches
- Pharmacologic class and therapeutic category queries
- Document navigation and version history
- Section content retrieval (warnings, dosage, adverse reactions, etc.)
- SPL import/export operations
- Drug interaction information
- Contraindications and precautions

**Keywords**: drug, product, ingredient, NDC, manufacturer, labeler, import, export, label, section, warning, side effect, dosage, dose, interaction, contraindication, adverse, prescribing, pharmaceutical, medication, medicine, tablet, capsule, injection, ANDA, NDA, BLA, pharmacologic, therapeutic, class, FDA, SPL, document, DailyMed, application number, application type, active ingredient, inactive ingredient, excipient, related ingredients, same ingredient products, generic equivalent, UNII, what application number, show application number

---

### userActivity
**User activity monitoring, application log viewing, and endpoint performance statistics.**

Use for queries about:
- **Application Logs**: Viewing, filtering, and analyzing application logs
- **Log Statistics**: Entry counts, level distribution, category summaries
- **Log Filtering**: By level (Error, Warning, Info), category, date range, or user
- **User Activity**: What actions a specific user performed in the system
- **Endpoint Performance**: API response times, controller statistics, performance analysis
- **System Monitoring**: Overall system health and diagnostic information

**Keywords**: log, logs, application log, error, warning, debug, trace, activity, user activity, what did user, endpoint, performance, response time, controller, statistics, how fast, monitoring, diagnostic

**Note**: Requires Admin role. Check `isAuthenticated` before suggesting endpoints.

---

### settings
**Cache management and system configuration.**

Use for queries about:
- Clearing/resetting the managed cache
- Cache invalidation after data changes
- System configuration

**Keywords**: cache, clear cache, reset cache, flush cache, invalidate

**Note**: Requires Admin role. Check `isAuthenticated` before suggesting endpoints.

---

### rescueWorkflow
**Fallback strategies when primary label queries return empty or incomplete results.**

Use for queries where:
- Primary structured data endpoints return empty results
- Information exists in narrative text rather than dedicated fields
- Data is embedded in sections like Description rather than structured tables

**Common Scenarios**:
- Inactive ingredients not in InactiveIngredient table but listed in Description section (LOINC 34089-3)
- Physical characteristics embedded in narrative text
- Storage conditions in non-standard sections

**Keywords**: not found, empty results, where else, alternative, rescue, fallback, text search, description section

**Process**:
1. Search by known identifier (ingredient, product name) to get documentGUID
2. Retrieve all section content for the document
3. Scan section text for target keywords (e.g., "inactive")
4. Extract structured data from narrative text using sentence parsing

**Note**: This skill supplements the `label` skill. Load both when primary queries fail.

---

### general
**AI agent workflow, authentication, and user management operations.**

Use for queries about:
- AI conversation management (start, interpret, synthesize)
- One-shot chat queries
- Authentication (OAuth login, logout, get current user)
- User profile management
- System context checks

**Keywords**: conversation, interpret, synthesize, chat, login, logout, authenticate, sign in, sign out, profile, current user, who am i, context, oauth, google login, microsoft login

**Note**: This skill handles general platform operations. For detailed user activity monitoring and logs, use `userActivity` skill instead.

---

### equianalgesicConversion
**Opioid dose conversion and equianalgesic dosing information from FDA labels.**

Use for queries about:
- Opioid dose conversions (morphine to hydromorphone, fentanyl to morphine, etc.)
- Equianalgesic dosing tables and ratios
- Opioid-tolerant patient definitions
- Morphine milligram equivalents (MME)
- Switching between opioid medications

**Keywords**: equianalgesic, opioid conversion, convert morphine, convert hydromorphone, convert fentanyl, convert oxycodone, morphine equivalent, mme, opioid tolerant, dose conversion, switching opioids, opioid switch, equivalent dose, morphine to hydromorphone, hydromorphone to morphine, fentanyl to morphine, morphine to fentanyl, oxycodone to morphine, morphine to oxycodone

**Process**:
1. **Identify source and target opioids**: Use `labelProductIndication.md` to find UNII codes
2. **Get product labels**: Call `/api/Label/product/latest?unii={UNII}` for each opioid
3. **Retrieve dosage sections**: Call `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34068-7` (Dosage and Administration)
4. **Retrieve indication sections**: Call `/api/Label/markdown/sections/{DocumentGUID}?sectionCode=34067-9` (for opioid-tolerant definitions)
5. **Extract and cite FDA data**: Use only content from label sections - NEVER from training data

**CRITICAL - NO TRAINING DATA FOR CONVERSIONS**:
- Equianalgesic ratios and conversion formulas MUST come from FDA label content
- Extended-release opioid labels (fentanyl transdermal, hydromorphone ER) contain equianalgesic thresholds
- If label does not contain conversion data, say so explicitly and provide label links
- DO NOT generate conversion tables or formulas from training data

**Note**: This skill supplements the `label` skill. Load `labelProductIndication` alongside for UNII lookups.

---

## Selection Rules

### UNIVERSAL REQUIREMENT: Label Links for Every Product

**THIS APPLIES TO ALL SKILLS - NO EXCEPTIONS**

Every response that mentions a pharmaceutical product MUST include a clickable link to view the full FDA label. This is a non-negotiable requirement.

**Required Format:**
```
View Full Labels:
• [View Full Label (ProductName)](/api/Label/generate/{DocumentGUID}/true)
```

**Rules:**
1. **Every product = one link**: If you mention 3 products, include 3 label links
2. **Use the ACTUAL product name**: Get `ProductName` from the API response, NOT from training data
3. **Use the correct DocumentGUID**: Get `DocumentGUID` from `/api/Label/product/latest` or `/api/Label/markdown/sections` response
4. **Link format**: `/api/Label/generate/{DocumentGUID}/true` (the `/true` suffix is required for minified XML)
5. **Never use placeholders**: Do NOT use "Prescription Drug", "OTC Drug", "Document #", or any generic term
6. **ALWAYS use RELATIVE URLs**: Start with `/api/...` - NEVER include `http://`, `https://`, `localhost`, or any domain

**CRITICAL - Relative URLs Only:**
- ✅ CORRECT: `/api/Label/generate/908025cf-e126-4241-b794-47d2fd90078e/true`
- ❌ WRONG: `http://localhost:5001/api/Label/generate/908025cf.../true`
- ❌ WRONG: `http://localhost:5093/api/Label/generate/908025cf.../true`
- ❌ WRONG: `https://medrecpro.com/api/Label/generate/908025cf.../true`

**Never include any host, port, or protocol in label URLs. The frontend will add the correct base URL.**

**CORRECT Examples:**
```
View Full Labels:
• [View Full Label (Cetirizine Hydrochloride)](/api/Label/generate/3b528d29-2957-8382-e063-6294a90aa8a7/true)
• [View Full Label (Fexofenadine Hydrochloride)](/api/Label/generate/e6582loh-6v12-3456-7890-abcdef123456/true)
```

**WRONG Examples (NEVER DO THIS):**
```
• [View Full Label (Prescription Drug)](/api/Label/generate/...)  ❌ Generic term
• [View Full Label (OTC Drug)](/api/Label/generate/...)  ❌ Generic term
• [View Full Label (Drug)](/api/Label/generate/...)  ❌ Generic term
• [View Full Label (antihistamine)](/api/Label/generate/...)  ❌ Drug class, not product name
• Search for cetirizine products (common allergy medication)  ❌ No actual label link
```

**CRITICAL - SELF-CHECK before responding:**
```
□ Does every label link contain an ACTUAL product name from the API (e.g., "Fentanyl Citrate")?
□ Are there ZERO instances of "Prescription Drug", "OTC Drug", "Drug", "Medication" as placeholders?
□ Did I get each ProductName from productLatestLabel.productName in the API response?
□ If I can't find the ProductName, I will NOT create a label link
```

**If you cannot provide a label link:**
- You MUST have called `/api/Label/product/latest` or `/api/Label/section/content` to get the DocumentGUID
- If the API call failed or returned no data, explain this to the user
- NEVER describe a product without providing a link to its label
- NEVER use a placeholder name - if you don't have the real name, don't create the link

---

### CRITICAL: Extract ALL Products from API Arrays

When an API returns multiple products in an array, you MUST:

1. **Process the ENTIRE array** - not just the first item
2. **Extract `productName` and `documentGUID` from EACH item**
3. **Create a label link for EACH relevant product**
4. **Group by dosage form** (ointments, solutions, etc.) when helpful

**Example - API returns 10 neomycin products:**
```json
[
  { "productsByIngredient": { "productName": "MAXITROL", "documentGUID": "6328676b...", "dosageFormName": "OINTMENT" }},
  { "productsByIngredient": { "productName": "Neomycin and Polymyxin B Sulfates", "documentGUID": "459b4410...", "dosageFormName": "SOLUTION" }},
  ... 8 more products
]
```

**WRONG** - Vague summary without links:
```
"Other neomycin products include MAXITROL ointment and various combinations"
← NO LABEL LINKS - WRONG!
```

**CORRECT** - Specific products with links:
```
View Full Labels:
• [View Full Label (MAXITROL)](/api/Label/generate/6328676b-98d9-4548-bc41-117e0e21eba6/true)
• [View Full Label (Neomycin and Polymyxin B Sulfates)](/api/Label/generate/459b4410-1bb0-c1fc-e063-6394a90a08ce/true)
```

**If you mention a product, you MUST provide its label link.** The `documentGUID` is in the API response - use it!

---

### Priority Order

1. **HIGHEST PRIORITY - Condition/treatment queries**: If query asks "what can be used for", "what helps with", "options for", "treatment for", or mentions a medical condition/disease/symptom, **ALWAYS** select `labelIndicationWorkflow` - NEVER use `label` skill for these queries
2. **NO SPECIFIC PRODUCT = labelIndicationWorkflow**: If the user's query does NOT mention a specific drug/product name (e.g., "Lipitor", "aspirin", "metformin"), select `labelIndicationWorkflow`. The `label` skill is ONLY for queries about a SPECIFIC, NAMED product.
3. **Check for monitoring keywords** - If query mentions logs, user activity, or performance, select `userActivity`
4. **Check for cache keywords** - If query mentions cache operations, select `settings`
5. **Check for product identification** - If query mentions specific products, UNII, or ingredients by name, select `labelIndicationWorkflow` (first-line selector)
6. **Default to label skill** - ONLY for detailed label content, sections, and document operations (side effects, warnings, dosing queries for a SPECIFIC product already identified)
7. **Add rescueWorkflow on retry** - When label skill queries return empty results, add `rescueWorkflow` for fallback strategies

### CRITICAL: Skill Selection Decision Tree

**Ask yourself: Does the query contain a SPECIFIC product name?**

```
Query: "What can be used as a surgical analgesic?"
  └─ Contains specific product name? NO
  └─ Contains condition/symptom/therapeutic need? YES ("surgical analgesic")
  └─ SELECT: labelIndicationWorkflow ✅

Query: "What are the side effects of Lipitor?"
  └─ Contains specific product name? YES ("Lipitor")
  └─ Asking about label DETAILS of that product? YES ("side effects")
  └─ SELECT: label ✅

Query: "What helps with pain?"
  └─ Contains specific product name? NO
  └─ Contains condition/symptom? YES ("pain")
  └─ SELECT: labelIndicationWorkflow ✅

Query: "Show me the dosage for metformin"
  └─ Contains specific product name? YES ("metformin")
  └─ Asking about label DETAILS of that product? YES ("dosage")
  └─ SELECT: label ✅
```

**NEVER use `label` skill when:**
- The query does NOT mention a specific product name
- The query is asking to FIND/DISCOVER products for a condition
- The query uses phrases like "what can be used for", "what helps with", "options for"

### CRITICAL: When to Use labelIndicationWorkflow vs label

**Use `labelIndicationWorkflow` (NOT `label`) when:**
- "What can be used for {condition}?" - e.g., "What can be used for seasonal allergies?"
- "What helps with {symptom}?" - e.g., "What helps with pain?"
- "Treatment options for {disease}" - e.g., "Treatment options for diabetes"
- "I have {condition}" - e.g., "I have high blood pressure"
- "Products for {symptom}" - e.g., "Products for anxiety"
- Any query asking to FIND/DISCOVER products for a condition

**Use `label` (NOT `labelIndicationWorkflow`) when:**
- "What are the side effects of {specific product}?" - already have a product name
- "Show warnings for {specific product}" - already have a product name
- "How do I take {specific product}?" - dosing for known product
- Any query about DETAILS of a SPECIFIC, ALREADY-IDENTIFIED product

### Condition-Based Query Workflow

**Important**: The `labelIndicationWorkflow` skill should be selected when:
- User asks about conditions, diseases, or symptoms (depression, hypertension, pain)
- User asks "what helps with X?" or "options for Y"
- User asks about alternatives or generics
- User wants to identify a product by name, UNII, or active ingredient

**Workflow Using Reference Data**:
1. Load `labelProductIndication.md` reference file
2. Search IndicationsSummary for condition keywords
3. Extract matching UNII(s) and ProductNames
4. Call `/api/Label/product/latest?unii={UNII}` for each match
5. Call `/api/Label/markdown/sections?documentGuid={guid}` to get full label content **USE for summaries**
6. Call `/api/Label/product/related?sourceDocumentGuid={guid}` for alternatives
7. Build response with label links

Example scenarios:
```
Query: "I have high blood pressure, what are my options?"
→ labelIndicationWorkflow
→ Search labelProductIndication.md for "hypertension"
→ Find UNIIs: Lisinopril (7Q3P4BS2FD), Amlodipine (1J444QC288)
→ GET /api/Label/product/latest?unii=7Q3P4BS2FD
→ GET /api/Label/markdown/sections?documentGuid={guid}
→ GET /api/Label/product/related?sourceDocumentGuid={guid}

Query: "What is Lipitor?"
→ labelIndicationWorkflow → GET /api/Label/product/latest?productNameSearch=Lipitor

Query: "Alternatives to Prozac"
→ labelIndicationWorkflow → GET /api/Label/product/latest + GET /api/Label/product/related
```

### Rescue Workflow Trigger

**Important**: The `rescueWorkflow` skill should be loaded in addition to `label` when:
- A previous query returned "not found" or empty results
- The user is asking about data that *should* exist (e.g., inactive ingredients for a known drug)
- The AI needs to search narrative text because structured fields are empty

Example scenario:
```
Query: "Show me inactive ingredients for Cephalexin"
→ Primary: label skill → GET /api/label/section/InactiveIngredient → Empty
→ Retry: label + rescueWorkflow → Search Description section for "inactive ingredients:" text
```

### Key Differentiators

| Query Type | Skill to Select |
|------------|-----------------|
| "What helps with depression?" | labelIndicationWorkflow |
| "I have high blood pressure" | labelIndicationWorkflow |
| "Options for diabetes" | labelIndicationWorkflow |
| "What is Lipitor?" | labelIndicationWorkflow |
| "Alternatives to Prozac" | labelIndicationWorkflow |
| "What can be used as a surgical analgesic?" | labelIndicationWorkflow |
| "What anesthetics are available?" | labelIndicationWorkflow |
| "Products for perioperative pain" | labelIndicationWorkflow |
| "Show me the application logs" | userActivity |
| "What errors occurred?" | userActivity |
| "What did [user] do?" | userActivity |
| "How fast is the API?" | userActivity |
| "Endpoint performance for Label" | userActivity |
| "Clear the cache" | settings |
| "What are the side effects of Lipitor?" | label |
| "Show warnings for aspirin" | label |
| "Import SPL files" | label |

### Common Query Patterns

| Query Pattern | Selected Skill(s) |
|--------------|-------------------|
| "What helps with {condition}?" | labelIndicationWorkflow |
| "I have {symptom}" | labelIndicationWorkflow |
| "Options for {disease}" | labelIndicationWorkflow |
| "What is {product}?" | labelIndicationWorkflow |
| "Alternatives to {product}" | labelIndicationWorkflow |
| "Products for {condition}" | labelIndicationWorkflow |
| "What can be used as a {therapeutic use}?" | labelIndicationWorkflow |
| "What {drug category} are available?" | labelIndicationWorkflow |
| "{therapeutic use} options" | labelIndicationWorkflow |
| "What are the side effects of..." | label |
| "Show warnings for..." | label |
| "Find products containing aspirin" | label |
| "Show me application logs" | userActivity |
| "What errors occurred today?" | userActivity |
| "How fast is the API responding?" | userActivity |
| "What did user X do?" | userActivity |
| "Show endpoint performance" | userActivity |
| "Clear the cache" | settings |
| "Import SPL files" | label |
| "Search by NDC code" | label |
| "Log statistics" | userActivity |
| "Filter logs by error level" | userActivity |
| "Inactive ingredients not found" (retry) | label + rescueWorkflow |
| "Data not in expected location" | label + rescueWorkflow |
| "How do I login?" | general |
| "Start a conversation" | general |
| "Who am I logged in as?" | general |
| "Logout" | general |
| "Interpret this query" | general |
| "Convert morphine to hydromorphone" | equianalgesicConversion |
| "Equianalgesic dose" | equianalgesicConversion |
| "Opioid conversion" | equianalgesicConversion |
| "What is the equivalent dose of fentanyl?" | equianalgesicConversion |
| "Morphine milligram equivalent" | equianalgesicConversion |
| "Switching from oxycodone to morphine" | equianalgesicConversion |

---

### CRITICAL: Multi-Product Workflow Triggers

**Use multi-product workflow (fetch from multiple labels) when:**

| Query Type | Reason | Skills |
|------------|--------|--------|
| "What are the contraindications for {ingredient}?" | Safety sections may be truncated | label, labelSection |
| "What are ALL the warnings for {drug}?" | User explicitly wants comprehensive data | label, labelSection |
| "How many {ingredient} products are available?" | User wants product count + comparison | labelIndicationWorkflow |
| Generic ingredient queries (not brand-specific) | Different labels may have different content completeness | All section-based skills |
| Opioid conversion queries | Dosing tables may be truncated in some labels | equianalgesicConversion |
| "Comprehensive information about {drug}" | Multiple sections needed, some may be truncated | label, labelSection |

**Multi-product workflow pattern:**
1. Search for multiple products with same active ingredient (`pageSize=50`)
2. Extract all `documentGUID[]` values (array extraction - **the `[]` suffix is REQUIRED**)
3. Fetch sections from ALL products (batch expansion creates N API calls)
4. During synthesis, select most complete content and aggregate unique information

### CRITICAL: Array Extraction Syntax

| outputMapping Syntax | Behavior | Outcome |
|---------------------|----------|---------|
| `"documentGuid": "documentGUID"` | ❌ Extracts only FIRST value | Only 1 product queried - **WRONG** |
| `"documentGuids": "documentGUID[]"` | ✅ Extracts ALL values | All products queried - **CORRECT** |

**Common Mistake**: Using `"documentGuid": "documentGUID"` (without `[]`) causes the workflow to query only the first product, missing products with complete data.

### Multi-Skill Selection

Select multiple skills when:
- Query spans both product identification AND detailed label content
- Query spans both pharmaceutical data AND monitoring functions
- Complex queries requiring context from multiple domains

Example: "Show me errors related to drug imports" → userActivity (for logs) + label (for import context)
Example: "What helps with depression and what are the side effects?" → labelIndicationWorkflow + label

---

## Response Format

After analyzing the user's query, respond with:

```json
{
  "success": true,
  "selectedSkills": ["skill1", "skill2"],
  "explanation": "Brief explanation of why these skills were selected",
  "isDirectResponse": false
}
```

### Direct Response Cases

If the query can be answered without loading skills:

```json
{
  "success": true,
  "selectedSkills": [],
  "isDirectResponse": true,
  "directResponse": "The answer to your question is...",
  "explanation": "Query answered directly without skill loading"
}
```

Use direct response for:
- General questions about MedRecPro capabilities
- Help/usage questions
- Clarification requests

---

## Authentication Awareness

Before selecting admin skills (userActivity, settings), check the system context:

- If `isAuthenticated == false`: Do NOT select admin skills
- Instead, respond with instructions to sign in first
- Only select admin skills when the user is authenticated

---

## Token Optimization

This manifest is designed to be lightweight (~500 tokens) compared to loading all skills (~10,000+ tokens). The two-stage routing pattern:

1. **Stage 1**: Use this manifest to select skill(s) - fast, low-cost API call
2. **Stage 2**: Load only the selected skill(s) - reduced prompt size

This optimization significantly reduces API costs and improves response latency for simple queries.
