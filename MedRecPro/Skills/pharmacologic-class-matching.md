# Pharmacologic Class Matching Skill

Match user's natural language pharmacologic class queries to actual database class names using AI-powered terminology translation.

---

## Purpose

Users ask about drug classes using common terminology (e.g., "beta blockers", "ACE inhibitors", "SSRIs") that differs from how classes are stored in the database (e.g., "Beta-Adrenergic Blockers [EPC]", "Angiotensin Converting Enzyme Inhibitors [EPC]").

This skill bridges the vocabulary gap by using AI to match user terminology to exact database class names.

---

## Matching Rules

1. Match common drug class terminology to formal classification names:
   - "beta blockers" matches "Beta-Adrenergic Blockers [EPC]" or similar
   - "ACE inhibitors" matches "Angiotensin Converting Enzyme Inhibitors [EPC]"
   - "SSRIs" matches "Selective Serotonin Reuptake Inhibitors [EPC]"
   - "statins" matches "HMG-CoA Reductase Inhibitors [EPC]"
2. Return EXACT class names from the database list (copy-paste accuracy required)
3. Include multiple classes if the user query could match several
4. If no reasonable match exists, return empty matches array with suggestions

---

## Common Terminology Mappings

| User Query | Database Class Name |
|------------|---------------------|
| beta blockers | Beta-Adrenergic Blockers [EPC] |
| ACE inhibitors | Angiotensin Converting Enzyme Inhibitors [EPC] |
| calcium channel blockers | Calcium Channel Blockers [EPC] |
| SSRIs | Selective Serotonin Reuptake Inhibitors [EPC] |
| statins | HMG-CoA Reductase Inhibitors [EPC] |
| proton pump inhibitors | Proton Pump Inhibitors [EPC] |
| opioids | Opioid Agonists [EPC] |
| benzodiazepines | Benzodiazepines [EPC] |
| NSAIDs | Non-steroidal Anti-inflammatory Drugs [EPC] |
| anticoagulants | Anticoagulants [EPC] |
| diuretics | Diuretics [EPC] |
| aminoglycosides | Aminoglycosides [Chemical/Ingredient] |

---

## JSON Response Format

### Successful Match

```json
{
  "success": true,
  "matchedClassNames": ["Exact Class Name 1", "Exact Class Name 2"],
  "explanation": "Brief explanation of matching logic",
  "confidence": "high|medium|low"
}
```

### No Match Found

```json
{
  "success": false,
  "matchedClassNames": [],
  "explanation": "Why no match was found",
  "suggestions": ["Alternative query 1", "Alternative query 2"]
}
```

---

## Example Matches

| User Query | Matched Classes | Confidence |
|------------|-----------------|------------|
| "beta blockers" | ["Beta-Adrenergic Blockers [EPC]"] | high |
| "blood pressure meds" | ["Angiotensin Converting Enzyme Inhibitors [EPC]", "Beta-Adrenergic Blockers [EPC]", "Calcium Channel Blockers [EPC]"] | medium |
| "SSRIs" | ["Selective Serotonin Reuptake Inhibitors [EPC]"] | high |
| "statins" | ["HMG-CoA Reductase Inhibitors [EPC]"] | high |
| "antibiotics" | Multiple antibiotic classes | medium |

---

## Fallback: Simple String Matching

If AI matching fails, the system falls back to simple string matching using term mappings:

```csharp
var termMappings = new Dictionary<string, string[]>
{
    { "beta blocker", new[] { "beta", "blocker", "adrenergic" } },
    { "ace inhibitor", new[] { "ace", "angiotensin", "converting", "enzyme" } },
    { "ssri", new[] { "ssri", "serotonin", "reuptake" } },
    { "statin", new[] { "statin", "hmg-coa", "reductase" } },
    // ... additional mappings
};
```

---

## Integration with Search Workflow

This skill is used internally by `ClaudeSearchService.MatchUserQueryToClassesAsync()`:

1. **Get Classes**: Retrieve all pharmacologic class summaries from database
2. **Build Prompt**: Create prompt with user query and available class names
3. **AI Match**: Call Claude to identify matching classes
4. **Validate**: Ensure returned class names exist in database
5. **Return**: Validated class names for product search

---

## Related Documents

- [Pharmacologic Class API](./interfaces/api/pharmacologic-class.md) - Search endpoints
- [Label Content](./interfaces/api/label-content.md) - Product retrieval after class match
