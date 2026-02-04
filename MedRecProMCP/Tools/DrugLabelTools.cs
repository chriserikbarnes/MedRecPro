/**************************************************************/
/// <summary>
/// MCP tools for FDA drug label operations.
/// </summary>
/// <remarks>
/// ## Tool Workflow
///
/// ```
/// search_drug_labels ──► ProductLatestLabel + Sections + ViewLabelUrl
///                        │
///                        ├── ProductName, ActiveIngredient, UNII, DocumentGUID
///                        ├── Markdown-formatted label sections (Indications, Warnings, etc.)
///                        └── ViewLabelUrl - clickable link to view FDA label in browser
/// ```
///
/// ## Common Scenarios
///
/// **Find drug by brand name:**
/// search_drug_labels (productNameSearch="Lipitor") → Complete label details with all sections
///
/// **Find drugs by active ingredient:**
/// search_drug_labels (activeIngredientSearch="atorvastatin") → All products containing ingredient
///
/// **Find drug by UNII code:**
/// search_drug_labels (unii="A0JWA85V8F") → Exact ingredient match
///
/// **Get specific section only:**
/// search_drug_labels (productNameSearch="Lipitor", sectionCode="34067-9") → Only Indications section
///
/// ## CRITICAL: Section Fallback Pattern
///
/// **When a specific sectionCode query returns empty content, retry WITHOUT sectionCode to get ALL sections.**
/// Not all FDA labels have every section code - the information may exist under a different section.
///
/// ## MANDATORY: Data Source Traceability
///
/// **YOU MUST ALWAYS include the ViewLabelUrl in EVERY response to users.**
/// This is non-negotiable. The link provides source verification and allows users to view
/// the official FDA label document. Format: [View Full Label ({ProductName})]({ViewLabelUrl})
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/

using MedRecProMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MedRecProMCP.Tools;

/**************************************************************/
/// <summary>
/// MCP tools for searching and retrieving FDA drug label information.
/// Provides AI-optimized access to drug product data including label sections and XML documents.
/// </summary>
/// <remarks>
/// These tools provide Claude with the ability to search and retrieve
/// drug label information from the MedRecPro API. All operations
/// require authentication and forward the user's token to the API.
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/
[McpServerToolType]
public class DrugLabelTools
{
    private readonly MedRecProApiClient _apiClient;
    private readonly ILogger<DrugLabelTools> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of DrugLabelTools.
    /// </summary>
    /// <param name="apiClient">The MedRecPro API client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <seealso cref="MedRecProApiClient"/>
    /**************************************************************/
    public DrugLabelTools(MedRecProApiClient apiClient, ILogger<DrugLabelTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Searches for FDA drug labels and returns complete product details with label sections.
    /// </summary>
    /// <remarks>
    /// ## Purpose
    /// Finds the most recent FDA drug labels for products matching the search criteria.
    /// Returns complete structured data including product information, markdown-formatted
    /// label sections, and a clickable URL to view the label in a browser.
    ///
    /// ## Workflow
    /// This is the PRIMARY entry point for drug information lookups.
    /// Results include everything needed to answer drug-related questions:
    /// - Product identification (name, ingredients, UNII, DocumentGUID)
    /// - Full label content in markdown format
    /// - **ViewLabelUrl** - clickable link to view FDA label in browser (renders as HTML)
    ///
    /// ## Returns
    /// Array of ProductLatestLabelDetailsDto objects containing:
    /// - ProductLatestLabel: Product name, active ingredient, UNII, DocumentGUID
    /// - Sections: Array of markdown-formatted label sections
    /// - **ViewLabelUrl**: Clickable link to view the FDA label (renders as HTML in browser)
    /// - ViewLabelMinifiedUrl: Same link but with minified content for reduced bandwidth
    ///
    /// ## Intelligent Parameter Selection
    /// Choose the search parameter based on user intent:
    /// - **Brand names** (Lipitor, Advil, Tylenol): Use productNameSearch
    /// - **Generic names** (atorvastatin, ibuprofen): Use activeIngredientSearch
    /// - **Chemical identifiers**: Use unii for exact ingredient matching
    ///
    /// ## CRITICAL: Section Fallback Pattern
    /// **When sectionCode returns empty or 404, RETRY WITHOUT sectionCode to get ALL sections.**
    /// Not all FDA labels have every LOINC section code. The information may exist under
    /// a different section heading. After retrieving all sections, search the content for
    /// the relevant information and extract it.
    ///
    /// Example fallback scenario:
    /// 1. Query with sectionCode="43685-7" (Warnings) returns empty
    /// 2. Retry the same query WITHOUT sectionCode parameter
    /// 3. Parse all returned sections for warning-related content
    /// 4. Present findings with attribution to source section
    ///
    /// ## MANDATORY: Include ViewLabelUrl in EVERY Response
    ///
    /// **THIS IS NON-NEGOTIABLE.** You MUST include the ViewLabelUrl link in every response.
    /// Failure to include this link is a critical error. The link provides:
    /// - Source verification for users
    /// - Traceability to official FDA label documents
    /// - Ability for users to view the complete label
    ///
    /// **Required format:** [View Full Label ({ProductName})]({ViewLabelUrl})
    ///
    /// Example: [View Full Label (Warfarin Sodium)](https://medrecpro.example.com/api/Label/original/abc-123/false)
    ///
    /// ## Result Formatting Guidelines
    /// When presenting results to users:
    /// 1. Format as conversational markdown, NOT raw JSON
    /// 2. **ALWAYS include the ViewLabelUrl link** - this is mandatory, not optional
    /// 3. Use ACTUAL ProductName from API - NEVER use generic placeholders
    /// 4. Extract and highlight: drug class, indications, major warnings
    /// 5. Group multiple products by dosage form when applicable
    /// 6. Only use information from the API response, never training data
    ///
    /// ## Common LOINC Section Codes
    /// - 34067-9: Indications and Usage (what is it used for)
    /// - 34084-4: Adverse Reactions (side effects)
    /// - 34070-3: Contraindications (when NOT to use)
    /// - 43685-7: Warnings and Precautions
    /// - 34068-7: Dosage and Administration
    /// - 34073-7: Drug Interactions
    /// - 34088-5: Overdosage
    /// - 34066-1: Boxed Warning (black box warning)
    /// - 34090-1: Clinical Pharmacology (mechanism of action)
    /// - 34076-0: Use in Specific Populations (pregnancy, nursing)
    /// </remarks>
    /// <param name="productNameSearch">Brand/product name search term.</param>
    /// <param name="activeIngredientSearch">Generic/active ingredient name search term.</param>
    /// <param name="unii">UNII code for exact ingredient match.</param>
    /// <param name="sectionCode">Optional LOINC code to filter to specific label section. If empty results, retry without this parameter.</param>
    /// <param name="pageNumber">Page number for pagination.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <returns>JSON array of ProductLatestLabelDetailsDto with complete label information.</returns>
    /// <example>
    /// <code>
    /// // Find by brand name
    /// SearchDrugLabels(productNameSearch: "Lipitor")
    ///
    /// // Find by active ingredient
    /// SearchDrugLabels(activeIngredientSearch: "atorvastatin")
    ///
    /// // Find by UNII with only indications section
    /// SearchDrugLabels(unii: "A0JWA85V8F", sectionCode: "34067-9")
    ///
    /// // FALLBACK: If sectionCode returns empty, retry without it
    /// SearchDrugLabels(productNameSearch: "Lipitor")  // Gets ALL sections
    /// </code>
    /// </example>
    /// <seealso cref="MedRecProApiClient.GetStringAsync"/>
    /**************************************************************/
    [McpServerTool(Name = "search_drug_labels")]
    [Description("""
    🔍 SEARCH: Find FDA drug labels by product name, ingredient, or UNII code.

    📋 WORKFLOW: Start here for ANY drug information lookup.
    ├── Returns: ProductName, ActiveIngredient, UNII, DocumentGUID, Label Sections
    ├── Sections: Full markdown text for Indications, Warnings, Dosage, Adverse Reactions, etc.
    └── **ViewLabelUrl**: Clickable link to view the FDA label in a browser (renders as HTML)

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ 🚨 MANDATORY: YOU MUST INCLUDE ViewLabelUrl IN EVERY RESPONSE 🚨        ┃
    ┃                                                                         ┃
    ┃ Format: [View Full Label ({ProductName})]({ViewLabelUrl})               ┃
    ┃                                                                         ┃
    ┃ This is NON-NEGOTIABLE. Failure to include this link is a critical      ┃
    ┃ error. Users need this link for source verification.                    ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    🎯 PARAMETER SELECTION - Choose based on user's query:
    ┌─────────────────────────────────────────────────────────────────────────┐
    │ User Says                    │ Use Parameter           │ Example       │
    ├─────────────────────────────────────────────────────────────────────────┤
    │ Brand name (Lipitor, Advil)  │ productNameSearch       │ "Lipitor"     │
    │ Generic name (ibuprofen)     │ activeIngredientSearch  │ "ibuprofen"   │
    │ Chemical identifier          │ unii                    │ "R16CO5Y76E"  │
    │ "side effects of X"          │ + sectionCode="34084-4" │ Adverse React │
    │ "what is X used for"         │ + sectionCode="34067-9" │ Indications   │
    │ "warnings for X"             │ + sectionCode="43685-7" │ Warnings/Prec │
    │ "dosage of X"                │ + sectionCode="34068-7" │ Dosage/Admin  │
    │ "drug interactions"          │ + sectionCode="34073-7" │ Interactions  │
    └─────────────────────────────────────────────────────────────────────────┘

    🔄 FALLBACK STRATEGY - When sectionCode returns empty:
    If a specific sectionCode query returns no section content, retry WITHOUT sectionCode
    to get ALL available sections. The information may exist under a different LOINC code.
    Example: Query for "43685-7" (Warnings) returns empty → Retry without sectionCode →
    Search all returned sections for warning-related content.

    📊 RESULT FORMATTING REQUIREMENTS:
    • Present as conversational markdown summary, NOT raw JSON
    • **MANDATORY**: Include [View Full Label ({ProductName})]({ViewLabelUrl}) in EVERY response
    • Extract key facts: drug class, indications, major warnings
    • Group by dosage form when multiple products returned
    • Use actual ProductName from API - NEVER use placeholders like "Prescription Drug"

    ⚠️ IMPORTANT REQUIREMENTS:
    • ALWAYS use pageSize=3 for initial searches (labels are very large)
    • **ALWAYS include ViewLabelUrl** - this link opens the FDA label in a browser
    • NEVER use training data - only information from the API response
    • If data is not in the API response, state "Not available in label data"

    📚 COMMON LOINC SECTION CODES:
    • 34067-9: Indications and Usage (what is it used for)
    • 34084-4: Adverse Reactions (side effects)
    • 34070-3: Contraindications (when NOT to use)
    • 43685-7: Warnings and Precautions
    • 34068-7: Dosage and Administration
    • 34073-7: Drug Interactions
    • 34088-5: Overdosage
    • 34066-1: Boxed Warning (black box)
    • 34090-1: Clinical Pharmacology
    • 34076-0: Use in Specific Populations (pregnancy, nursing)
    """)]
    public async Task<string> SearchDrugLabels(
        [Description("Brand/product name search. Use when user mentions brand names like 'Lipitor', 'Advil', 'Tylenol'. Supports partial matching.")]
        string? productNameSearch = null,

        [Description("Generic/active ingredient name search. Use when user mentions generic names like 'atorvastatin', 'ibuprofen', 'acetaminophen'. Supports partial matching.")]
        string? activeIngredientSearch = null,

        [Description("UNII (Unique Ingredient Identifier) code for exact chemical match. Use for precise ingredient lookups. Examples: 'R16CO5Y76E' (aspirin), 'A0JWA85V8F' (atorvastatin).")]
        string? unii = null,

        [Description("LOINC section code to filter results. FALLBACK: If this returns empty/404, RETRY without sectionCode to get ALL sections, then search content. Codes: '34067-9' (Indications - 'used for'), '34084-4' (Adverse Reactions - 'side effects'), '34070-3' (Contraindications - 'do not use'), '43685-7' (Warnings/Precautions), '34068-7' (Dosage), '34073-7' (Drug Interactions), '34088-5' (Overdosage), '34066-1' (Black Box Warning).")]
        string? sectionCode = null,

        [Description("Page number, 1-based. Default: 1")]
        [Range(1, int.MaxValue)]
        int pageNumber = 1,

        [Description("Results per page (1-50). Use pageSize=3 for initial searches as labels are large. Default: 10")]
        [Range(1, 50)]
        int pageSize = 10)
    {
        #region implementation

        // Log the search parameters for debugging
        _logger.LogInformation(
            "[Tool] SearchDrugLabels: productName={ProductName}, ingredient={Ingredient}, unii={UNII}, section={Section}, page={Page}, size={Size}",
            productNameSearch, activeIngredientSearch, unii, sectionCode, pageNumber, pageSize);

        // Validate and constrain parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        try
        {
            // Build the query string for GetProductLatestLabelDetails endpoint
            var queryParams = new List<string>();

            // Add search parameters if provided
            if (!string.IsNullOrWhiteSpace(unii))
                queryParams.Add($"unii={Uri.EscapeDataString(unii)}");

            if (!string.IsNullOrWhiteSpace(productNameSearch))
                queryParams.Add($"productNameSearch={Uri.EscapeDataString(productNameSearch)}");

            if (!string.IsNullOrWhiteSpace(activeIngredientSearch))
                queryParams.Add($"activeIngredientSearch={Uri.EscapeDataString(activeIngredientSearch)}");

            if (!string.IsNullOrWhiteSpace(sectionCode))
                queryParams.Add($"sectionCode={Uri.EscapeDataString(sectionCode)}");

            // Always add pagination parameters
            queryParams.Add($"pageNumber={pageNumber}");
            queryParams.Add($"pageSize={pageSize}");

            // Construct the endpoint URL
            var endpoint = $"api/Label/product/latest/details?{string.Join("&", queryParams)}";

            // Execute the API call
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] SearchDrugLabels failed: productName={ProductName}, ingredient={Ingredient}, unii={UNII}",
                productNameSearch, activeIngredientSearch, unii);

            return JsonSerializer.Serialize(new
            {
                error = "Search failed",
                message = ex.Message
            });
        }

        #endregion
    }
}
