# Product Extraction Skill

Extract drug/ingredient names from natural language descriptions. Used for UNII resolution fallback when the interpret phase provides an incorrect UNII but includes the product name in the description.

---

## Purpose

When the AI interpret phase returns an endpoint like `/api/Label/product/latest?unii=ABC123` but the UNII is incorrect (hallucinated from reference data), the endpoint returns empty results. However, the endpoint's description often contains the actual product name that can be used for a name-based search instead.

This skill extracts the drug/ingredient name from that description to enable a fallback search.

---

## Extraction Rules

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

---

## Common Brand to Generic Mappings

| Brand Name | Generic Name |
|------------|--------------|
| Kerendia | finerenone |
| Jardiance | empagliflozin |
| Farxiga | dapagliflozin |
| Invokana | canagliflozin |
| Ozempic | semaglutide |
| Wegovy | semaglutide |
| Mounjaro | tirzepatide |
| Trulicity | dulaglutide |
| Victoza | liraglutide |
| Renvela | sevelamer |
| Renagel | sevelamer |
| Lipitor | atorvastatin |
| Crestor | rosuvastatin |
| Zocor | simvastatin |
| Plavix | clopidogrel |
| Eliquis | apixaban |
| Xarelto | rivaroxaban |
| Pradaxa | dabigatran |
| Entresto | sacubitril |
| Januvia | sitagliptin |
| Tradjenta | linagliptin |

---

## JSON Response Format

### Successful Extraction

```json
{
  "success": true,
  "productNames": ["generic_name_1", "generic_name_2"],
  "confidence": "high|medium|low",
  "explanation": "Brief explanation of extraction",
  "brandMappingApplied": false,
  "originalBrandName": null
}
```

### Brand Name Converted to Generic

```json
{
  "success": true,
  "productNames": ["finerenone"],
  "confidence": "high",
  "explanation": "Converted brand name Kerendia to generic finerenone",
  "brandMappingApplied": true,
  "originalBrandName": "Kerendia"
}
```

### No Drug Name Found (Only Drug Class)

```json
{
  "success": false,
  "productNames": [],
  "confidence": "low",
  "explanation": "Description mentions drug class but no specific drug name",
  "drugClassMentioned": "SGLT2 inhibitors"
}
```

---

## Example Extractions

| Description | Extracted Name | Notes |
|-------------|----------------|-------|
| "Search for sevelamer - phosphate binder for CKD" | sevelamer | Generic name |
| "Search for finerenone (Kerendia) - non-steroidal MRA" | finerenone | Generic preferred |
| "Get metformin products for type 2 diabetes" | metformin | Ignore indication |
| "Find SGLT2 inhibitors for diabetes" | (none) | Drug class, not drug name |
| "Retrieve Jardiance label information" | empagliflozin | Brand to generic mapping |

---

## Related Documents

- [Retry Fallback](./interfaces/api/retry-fallback.md) - Fallback strategies for failed API calls
- [Product Extraction API](./interfaces/api/product-extraction-api.md) - API endpoint specification
