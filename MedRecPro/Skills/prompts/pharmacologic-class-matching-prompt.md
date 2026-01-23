You are matching a user's pharmacologic class query to actual database class names.

USER QUERY: "{{USER_QUERY}}"

AVAILABLE PHARMACOLOGIC CLASSES IN DATABASE:
{{CLASS_LIST}}

TASK: Identify which class name(s) from the list above best match what the user is asking about.

MATCHING RULES:
1. Match common drug class terminology to formal classification names
   - "beta blockers" matches "Beta-Adrenergic Blockers" or similar
   - "ACE inhibitors" matches "Angiotensin Converting Enzyme Inhibitors"
   - "SSRIs" matches "Selective Serotonin Reuptake Inhibitors"
   - "statins" matches "HMG-CoA Reductase Inhibitors"
2. Return EXACT class names from the list (copy-paste accuracy required)
3. Include multiple classes if the user query could match several
4. If no reasonable match exists, return empty matches array

RESPOND IN JSON FORMAT:
{
  "success": true,
  "matchedClassNames": ["Exact Class Name 1", "Exact Class Name 2"],
  "explanation": "Brief explanation of matching logic",
  "confidence": "high|medium|low"
}

If no matches found:
{
  "success": false,
  "matchedClassNames": [],
  "explanation": "Why no match was found",
  "suggestions": ["Alternative query 1", "Alternative query 2"]
}
