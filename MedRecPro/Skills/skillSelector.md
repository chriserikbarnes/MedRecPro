# MedRecPro AI Skill Selector Manifest

This lightweight manifest enables efficient two-stage routing for AI skill selection. Use this document to determine which skill(s) to load for a user query.

---

## Available Skills

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

**Keywords**: drug, product, ingredient, NDC, manufacturer, labeler, import, export, label, section, warning, side effect, dosage, dose, interaction, contraindication, adverse, prescribing, pharmaceutical, medication, medicine, tablet, capsule, injection, ANDA, NDA, BLA, pharmacologic, therapeutic, class, FDA, SPL, document, DailyMed

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

## Selection Rules

### Priority Order

1. **Check for monitoring keywords first** - If query mentions logs, user activity, or performance, select `userActivity`
2. **Check for cache keywords** - If query mentions cache operations, select `settings`
3. **Default to label skill** - Most queries about pharmaceutical data use the label skill

### Key Differentiators

| Query Type | Skill to Select |
|------------|-----------------|
| "Show me the application logs" | userActivity |
| "What errors occurred?" | userActivity |
| "What did [user] do?" | userActivity |
| "How fast is the API?" | userActivity |
| "Endpoint performance for Label" | userActivity |
| "Clear the cache" | settings |
| "Find drugs with aspirin" | label |
| "What are the side effects?" | label |
| "Import SPL files" | label |

### Common Query Patterns

| Query Pattern | Selected Skill(s) |
|--------------|-------------------|
| "Find products containing aspirin" | label |
| "What are the side effects of..." | label |
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

### Multi-Skill Selection

Select multiple skills when:
- Query spans both pharmaceutical data AND monitoring functions
- Complex queries requiring context from multiple domains

Example: "Show me errors related to drug imports" → userActivity (for logs) + label (for import context)

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
