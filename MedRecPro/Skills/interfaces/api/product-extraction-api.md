# Product Extraction - API Interface

Server-side AI-powered extraction of drug/ingredient names from natural language descriptions.

---

## Primary Endpoint

### Extract Product from Description

```
GET /api/Label/extract-product?description={description}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `description` | string | Yes | Natural language description containing drug/ingredient name |

**What it does:**
1. Receives description text (typically from a failed endpoint's description)
2. Uses AI to extract the drug/ingredient name
3. Applies brand-to-generic mapping if applicable
4. Returns structured extraction result

---

## Response Structure

### Successful Extraction

```json
{
  "success": true,
  "productNames": ["sevelamer"],
  "primaryProductName": "sevelamer",
  "confidence": "high",
  "explanation": "Extracted 'sevelamer' from description context",
  "brandMappingApplied": false,
  "originalBrandName": null,
  "error": null
}
```

### Brand Name Conversion

```json
{
  "success": true,
  "productNames": ["empagliflozin"],
  "primaryProductName": "empagliflozin",
  "confidence": "high",
  "explanation": "Converted brand name Jardiance to generic empagliflozin",
  "brandMappingApplied": true,
  "originalBrandName": "Jardiance",
  "error": null
}
```

### Extraction Failed

```json
{
  "success": false,
  "productNames": [],
  "primaryProductName": null,
  "confidence": "low",
  "explanation": "Description mentions drug class but no specific drug name",
  "brandMappingApplied": false,
  "originalBrandName": null,
  "error": "Could not extract product name from description"
}
```

---

## Use Case: UNII Resolution Fallback

This endpoint is called by `endpoint-executor.js` when a UNII-based search fails:

1. **Original Request**: `/api/Label/product/latest?unii=ABC123`
2. **Result**: Empty (UNII was incorrect)
3. **Fallback**: Call `/api/Label/extract-product` with the original endpoint's description
4. **Extraction**: Get the actual product name
5. **Retry**: Search using `/api/Label/product/latest?productNameSearch={extractedName}`

### JavaScript Integration

```javascript
async function extractProductNameFromDescriptionAsync(description, abortController) {
    const extractEndpoint = {
        method: 'GET',
        path: '/api/Label/extract-product',
        queryParameters: { description: description }
    };

    const response = await fetch(fullUrl, fetchOptions);
    const result = await response.json();

    if (result.success && result.primaryProductName) {
        return result.primaryProductName;
    }

    // Fall back to local pattern matching
    return extractProductNameFromDescription(description);
}
```

---

## Error Handling

| HTTP Status | Meaning | Action |
|-------------|---------|--------|
| 200 | Success (check `success` field) | Use `primaryProductName` if available |
| 400 | Missing description parameter | Fall back to local extraction |
| 500 | Server error | Fall back to local extraction |

The endpoint always returns HTTP 200 with structured JSON - check the `success` field to determine if extraction succeeded.

---

## Swagger Documentation

The endpoint includes full Swagger documentation:

```csharp
/// <summary>
/// Uses AI to extract a drug/ingredient name from a natural language description.
/// </summary>
/// <param name="description">
/// The endpoint description containing the product/ingredient name.
/// Examples: "Search for sevelamer - phosphate binder", "Get metformin products"
/// </param>
/// <returns>Extraction result with product names and confidence level</returns>
/// <response code="200">Returns the extraction result</response>
/// <response code="400">If description is missing or empty</response>
[HttpGet("extract-product")]
[ProducesResponseType(typeof(ProductExtractionResult), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<ProductExtractionResult>> ExtractProductFromDescription(
    [FromQuery] string description)
```

---

## Related Documents

- [Product Extraction Skill](../product-extraction.md) - Extraction rules and examples
- [Retry Fallback](./retry-fallback.md) - General fallback strategies
- [Label Content](./label-content.md) - Product search endpoints
