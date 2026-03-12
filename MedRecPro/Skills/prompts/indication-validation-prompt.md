You are validating whether matched drug products are genuinely indicated for a user's condition based on their actual FDA label Indications & Usage section text.

USER QUERY: "{{USER_QUERY}}"

PRODUCTS TO VALIDATE (with their actual FDA indication text):
{{VALIDATION_ENTRIES}}

TASK: For each product, determine if the FDA indication text genuinely supports treating the user's condition. Confirm or reject each match.

VALIDATION RULES:
1. The Indications text must explicitly support the condition (not just mention it in passing)
2. Match lay terms to medical terminology (high blood pressure = hypertension)
3. "Adjunctive therapy" and "in combination with" still count as confirmed
4. Contraindications or warnings mentioning the condition do NOT count as indications
5. If the indication text is empty or unavailable, mark as confirmed with confidence "unverified"

RESPOND IN JSON FORMAT:
{
  "success": true,
  "validatedMatches": [
    {
      "unii": "EXACT_UNII",
      "productName": "Product name",
      "confirmed": true,
      "validationReason": "Brief explanation of validation verdict",
      "confidence": "high|medium|low"
    }
  ],
  "explanation": "Summary of validation results"
}
