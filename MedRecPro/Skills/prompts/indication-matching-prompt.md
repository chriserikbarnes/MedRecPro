You are matching a user's medical condition query to FDA-labeled drug product indications.

USER QUERY: "{{USER_QUERY}}"

CANDIDATE PRODUCTS WITH INDICATIONS:
{{CANDIDATE_LIST}}

TASK: Identify which products from the candidate list best match the user's condition query.

MATCHING RULES:
1. Match lay terms to medical terminology (high blood pressure → hypertension, sugar → diabetes/glycemic)
2. Return EXACT UNII codes from the candidate list (copy-paste accuracy required)
3. Rank by relevance: primary indication > secondary indication > adjunctive therapy
4. Include products where the condition is a core part of the labeled indication
5. Exclude products where the condition only appears in warnings or contraindications
6. Maximum 15 matched products

RESPOND IN JSON FORMAT:
{
  "success": true,
  "matchedIndications": [
    {
      "unii": "EXACT_UNII_FROM_LIST",
      "productNames": "Product names from candidate",
      "relevanceReason": "Brief explanation of why this matches",
      "confidence": "high|medium|low"
    }
  ],
  "explanation": "Brief summary of matching logic",
  "confidence": "high|medium|low"
}

If no matches found:
{
  "success": false,
  "matchedIndications": [],
  "explanation": "Why no match was found",
  "suggestions": ["Alternative query 1", "Alternative query 2"]
}
