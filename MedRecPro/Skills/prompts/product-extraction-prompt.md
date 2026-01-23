You are extracting drug/ingredient names from API endpoint descriptions.

DESCRIPTION: "{{DESCRIPTION}}"

TASK: Identify the specific drug or ingredient name(s) mentioned in this description.

EXTRACTION RULES:
1. Extract the GENERIC (non-brand) drug name if possible
2. If only a brand name is given, identify it and provide the generic equivalent
3. Handle multi-word drug names (e.g., "sevelamer carbonate", "metformin hydrochloride")
4. Distinguish drug NAMES from drug CLASSES:
   - "metformin" = drug name (extract this)
   - "SGLT2 inhibitor" = drug class (do NOT extract as a drug name)
   - "finerenone" = drug name (extract this)
   - "MRA" = drug class abbreviation (do NOT extract)
5. Extract ALL drug names if multiple are mentioned
6. Ignore descriptive text about indications/conditions

COMMON BRAND TO GENERIC MAPPINGS:
- Kerendia = finerenone
- Jardiance = empagliflozin
- Farxiga = dapagliflozin
- Ozempic/Wegovy = semaglutide
- Mounjaro = tirzepatide
- Renvela/Renagel = sevelamer
- Lipitor = atorvastatin
- Eliquis = apixaban

RESPOND IN JSON FORMAT:
{
  "success": true,
  "productNames": ["generic_name_1", "generic_name_2"],
  "confidence": "high|medium|low",
  "explanation": "Brief explanation of extraction",
  "brandMappingApplied": false,
  "originalBrandName": null
}

If a brand name was converted to generic:
{
  "success": true,
  "productNames": ["finerenone"],
  "confidence": "high",
  "explanation": "Converted brand name Kerendia to generic finerenone",
  "brandMappingApplied": true,
  "originalBrandName": "Kerendia"
}

If no drug name found (only drug class):
{
  "success": false,
  "productNames": [],
  "confidence": "low",
  "explanation": "Description mentions drug class but no specific drug name",
  "drugClassMentioned": "SGLT2 inhibitors"
}
