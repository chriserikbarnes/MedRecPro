/**************************************************************/
/// <summary>
/// MCP tools for FDA drug label operations.
/// </summary>
/// <remarks>
/// ## Tool Workflow
///
/// ```
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │ search_drug_labels ──► Quick lookups, section queries, comparisons      │
/// │                        │                                                │
/// │                        ├── ProductName, ActiveIngredient, UNII          │
/// │                        ├── Label sections (Indications, Warnings, etc.) │
/// │                        └── ViewLabelUrl - link to FDA label             │
/// └─────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │ export_drug_label_markdown ──► Complete label export (TWO-STEP)         │
/// │                                │                                        │
/// │   Step 1: Search products ─────┼──► User selects DocumentGUID           │
/// │                                │                                        │
/// │   Step 2: Export markdown ─────┼──► FullMarkdown + ViewLabelUrl         │
/// └─────────────────────────────────────────────────────────────────────────┘
/// ```
///
/// ## Tool Selection Guide
///
/// **Use search_drug_labels when user asks SPECIFIC QUESTIONS:**
/// - "What are the side effects of X?"
/// - "What is the dosage for X?"
/// - "What are the warnings for X?"
/// - "What is X used for?"
/// - "Compare X and Y"
/// - Any targeted question about a particular aspect of a drug
///
/// **Use export_drug_label_markdown when user wants COMPLETE LABEL:**
/// - "Show me the label for X"
/// - "Show me all information about X"
/// - "Give me the full label"
/// - "Export the label"
/// - "I want to see the complete label"
/// - Any request to VIEW or DISPLAY the full label document
///
/// ## Common Scenarios
///
/// **Find drug by brand name:**
/// search_drug_labels (productNameSearch="Lipitor") → Complete label details with all sections
///
/// **Find drugs by active ingredient:**
/// search_drug_labels (activeIngredientSearch="atorvastatin") → All products containing ingredient
///
/// **Export complete label:**
/// export_drug_label_markdown (productNameSearch="Lipitor") → Step 1: Get product list
/// export_drug_label_markdown (documentGuid="...") → Step 2: Get full markdown
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

using MedRecProMCP.Configuration;
using MedRecProMCP.Services;
using Microsoft.Extensions.Options;
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
    private readonly MedRecProApiSettings _apiSettings;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of DrugLabelTools.
    /// </summary>
    /// <param name="apiClient">The MedRecPro API client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="apiSettings">MedRecPro API configuration settings.</param>
    /// <seealso cref="MedRecProApiClient"/>
    /// <seealso cref="MedRecProApiSettings"/>
    /**************************************************************/
    public DrugLabelTools(
        MedRecProApiClient apiClient,
        ILogger<DrugLabelTools> logger,
        IOptions<MedRecProApiSettings> apiSettings)
    {
        _apiClient = apiClient;
        _logger = logger;
        _apiSettings = apiSettings.Value;
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
    🔍 SEARCH: Find FDA drug labels and answer SPECIFIC QUESTIONS about drugs.

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ ⚠️ CRITICAL: TOOL SELECTION - READ THIS FIRST                           ┃
    ┃                                                                         ┃
    ┃ USE THIS TOOL (search_drug_labels) when user asks:                      ┃
    ┃ • "What are the side effects of X?"                                     ┃
    ┃ • "What is the dosage for X?"                                           ┃
    ┃ • "What are the warnings for X?"                                        ┃
    ┃ • "What is X used for?"                                                 ┃
    ┃ • "Compare X and Y"                                                     ┃
    ┃ • Any SPECIFIC QUESTION about a drug                                    ┃
    ┃                                                                         ┃
    ┃ USE export_drug_label_markdown INSTEAD when user asks:                  ┃
    ┃ • "Show me the label for X"                                             ┃
    ┃ • "Show me all information about X"                                     ┃
    ┃ • "Give me the full label"                                              ┃
    ┃ • "Export the label"                                                    ┃
    ┃ • "I want to see the complete label"                                    ┃
    ┃ • Any request for COMPLETE/FULL label information                       ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    📋 PURPOSE: Answer specific drug questions with targeted section data.
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

    /**************************************************************/
    /// <summary>
    /// Exports a complete FDA drug label as clean, well-formatted markdown.
    /// Implements a two-step workflow: search for products, then export selected document.
    /// </summary>
    /// <remarks>
    /// ## Two-Step Workflow
    ///
    /// This tool implements a two-step workflow for accurate label export:
    ///
    /// ```
    /// Step 1: Search Products ──► User selects correct product ──► DocumentGUID
    ///                              │
    ///                              └── Multiple products may have same name
    ///                                  (different strengths, dosage forms)
    ///
    /// Step 2: Export Markdown ──► Complete formatted label with source links
    ///                              │
    ///                              ├── FullMarkdown: Ready-to-display content
    ///                              └── ViewLabelUrl: Source verification link
    /// ```
    ///
    /// ## When to Use This Tool
    ///
    /// Use `export_drug_label_markdown` when the user wants:
    /// - A complete, formatted drug label document
    /// - Clean markdown suitable for display or export
    /// - The full label content, not just specific sections
    ///
    /// Use `search_drug_labels` instead when:
    /// - User has a quick question about a specific aspect (side effects, dosage)
    /// - User wants to compare multiple products
    /// - User only needs specific sections (Indications, Warnings, etc.)
    ///
    /// ## Workflow Steps
    ///
    /// **Step 1 - Product Search (without documentGuid):**
    /// Call this tool without documentGuid to search for products.
    /// Present results to user for selection, showing:
    /// - ProductName
    /// - ActiveIngredient
    /// - UNII
    /// - DocumentGUID
    ///
    /// **Step 2 - Export Markdown (with documentGuid):**
    /// Once user selects a product, call again WITH documentGuid.
    /// Returns complete markdown export with source links.
    ///
    /// ## CRITICAL: Always present Step 1 results to user for selection.
    /// Do NOT automatically select a product. Multiple products may match
    /// (different strengths, dosage forms, or manufacturers).
    /// </remarks>
    /// <param name="productNameSearch">Brand/product name to search (Step 1).</param>
    /// <param name="activeIngredientSearch">Generic ingredient name to search (Step 1).</param>
    /// <param name="unii">UNII code for exact match (Step 1).</param>
    /// <param name="documentGuid">DocumentGUID from Step 1 selection (Step 2).</param>
    /// <returns>Product list (Step 1) or complete markdown export (Step 2).</returns>
    /// <example>
    /// <code>
    /// // Step 1: Search for products
    /// ExportDrugLabelMarkdown(productNameSearch: "Lipitor")
    /// // Returns list of matching products for user selection
    ///
    /// // Step 2: Export selected product (user chose DocumentGUID from Step 1)
    /// ExportDrugLabelMarkdown(documentGuid: "052493C7-89A3-452E-8140-04DD95F0D9E2")
    /// // Returns complete markdown with source URLs
    /// </code>
    /// </example>
    /// <seealso cref="SearchDrugLabels"/>
    /// <seealso cref="MedRecProApiClient.GetStringAsync"/>
    /**************************************************************/
    [McpServerTool(Name = "export_drug_label_markdown")]
    [Description("""
    📄 EXPORT: Get a complete FDA drug label as clean, formatted markdown.

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ ⚠️ CRITICAL: TOOL SELECTION - READ THIS FIRST                           ┃
    ┃                                                                         ┃
    ┃ USE THIS TOOL (export_drug_label_markdown) when user asks:              ┃
    ┃ • "Show me the label for X"                                             ┃
    ┃ • "Show me all information about X"                                     ┃
    ┃ • "Give me the full label"                                              ┃
    ┃ • "Export the label"                                                    ┃
    ┃ • "I want to see the complete label"                                    ┃
    ┃ • Any request for COMPLETE/FULL label information                       ┃
    ┃                                                                         ┃
    ┃ USE search_drug_labels INSTEAD when user asks:                          ┃
    ┃ • "What are the side effects of X?"                                     ┃
    ┃ • "What is the dosage for X?"                                           ┃
    ┃ • "What are the warnings for X?"                                        ┃
    ┃ • "What is X used for?"                                                 ┃
    ┃ • "Compare X and Y"                                                     ┃
    ┃ • Any SPECIFIC QUESTION about a drug                                    ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ 🔄 TWO-STEP WORKFLOW - ALWAYS FOLLOW THIS PROCESS                       ┃
    ┃                                                                         ┃
    ┃ Step 1: Call WITHOUT documentGuid → Get product list                    ┃
    ┃         Present results to user for selection                           ┃
    ┃                                                                         ┃
    ┃ Step 2: Call WITH documentGuid → Get complete markdown export           ┃
    ┃         Render markdown and include source link                         ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    🎯 STEP 1 - Product Search (no documentGuid):
    ├── Use productNameSearch for brand names (Lipitor, Advil)
    ├── Use activeIngredientSearch for generic names (atorvastatin)
    ├── Use unii for exact ingredient match
    └── Returns: List of ProductName, ActiveIngredient, UNII, DocumentGUID

    ⚠️ CRITICAL: Present Step 1 results to user for selection.
    Do NOT automatically select a product. Multiple products may match
    (different strengths, dosage forms, or manufacturers).

    📋 STEP 2 - Export Markdown (with documentGuid):
    ├── Provide documentGuid from user's Step 1 selection
    ├── Returns: Complete markdown document with source links
    └── FullMarkdown contains the entire formatted label

    📊 MARKDOWN RENDERING INSTRUCTIONS:
    When you receive the Step 2 response, render the FullMarkdown field
    as formatted markdown for the user. The content includes:
    - Document title as # header
    - All sections with ## headers (Indications, Warnings, Dosage, etc.)
    - Properly formatted tables, lists, and emphasis
    - Clean content without XML/HTML artifacts

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ 🚨 MANDATORY: Include ViewLabelUrl in EVERY Step 2 response 🚨          ┃
    ┃                                                                         ┃
    ┃ Format: **Source:** [View Full Label ({ProductName})]({ViewLabelUrl})   ┃
    ┃                                                                         ┃
    ┃ Place this link at the END of the rendered markdown content.            ┃
    ┃ This provides source verification for users.                            ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛
    """)]
    public async Task<string> ExportDrugLabelMarkdown(
        [Description("Brand/product name search (Step 1). Use for brand names like 'Lipitor', 'Advil'. Leave empty when providing documentGuid.")]
        string? productNameSearch = null,

        [Description("Generic ingredient name search (Step 1). Use for generic names like 'atorvastatin'. Leave empty when providing documentGuid.")]
        string? activeIngredientSearch = null,

        [Description("UNII code for exact ingredient match (Step 1). Leave empty when providing documentGuid.")]
        string? unii = null,

        [Description("DocumentGUID from Step 1 selection (Step 2). When provided, returns complete markdown export with source URLs. Format: GUID string like '052493C7-89A3-452E-8140-04DD95F0D9E2'.")]
        string? documentGuid = null)
    {
        #region implementation

        _logger.LogInformation(
            "[Tool] ExportDrugLabelMarkdown: productName={ProductName}, ingredient={Ingredient}, unii={UNII}, documentGuid={DocumentGuid}",
            productNameSearch, activeIngredientSearch, unii, documentGuid);

        try
        {
            // STEP 2: If documentGuid is provided, fetch the markdown export
            if (!string.IsNullOrWhiteSpace(documentGuid))
            {
                // Validate GUID format
                if (!Guid.TryParse(documentGuid, out var parsedGuid))
                {
                    _logger.LogWarning("[Tool] ExportDrugLabelMarkdown: Invalid GUID format: {DocumentGuid}", documentGuid);
                    return JsonSerializer.Serialize(new
                    {
                        error = "Invalid DocumentGUID",
                        message = "The provided documentGuid is not a valid GUID format. Expected format: '052493C7-89A3-452E-8140-04DD95F0D9E2'."
                    });
                }

                // Call GetLabelMarkdownExport endpoint
                var exportEndpoint = $"api/Label/markdown/export/{parsedGuid}";
                _logger.LogDebug("[Tool] ExportDrugLabelMarkdown: Calling {Endpoint}", exportEndpoint);

                var exportResult = await _apiClient.GetStringAsync(exportEndpoint);

                // Parse the response to add source URLs (the API doesn't include them)
                var exportDto = JsonSerializer.Deserialize<JsonElement>(exportResult);

                // Construct base URL for source links
                // The API base URL is like "https://www.medrecpro.com/api" - we need "https://www.medrecpro.com"
                var baseUrl = _apiSettings.BaseUrl.TrimEnd('/');
                if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = baseUrl[..^4]; // Remove "/api" suffix
                }

                // Build enriched response with source links
                var enrichedResponse = new
                {
                    documentGUID = parsedGuid,
                    setGUID = exportDto.TryGetProperty("setGUID", out var setGuid) ? setGuid.GetString() : null,
                    documentTitle = exportDto.TryGetProperty("documentTitle", out var title) ? title.GetString() : null,
                    sectionCount = exportDto.TryGetProperty("sectionCount", out var sCount) ? sCount.GetInt32() : 0,
                    totalContentBlocks = exportDto.TryGetProperty("totalContentBlocks", out var tBlocks) ? tBlocks.GetInt32() : 0,
                    fullMarkdown = exportDto.TryGetProperty("fullMarkdown", out var markdown) ? markdown.GetString() : null,
                    viewLabelUrl = $"{baseUrl}/api/Label/original/{parsedGuid}/false",
                    viewLabelMinifiedUrl = $"{baseUrl}/api/Label/original/{parsedGuid}/true"
                };

                _logger.LogInformation(
                    "[Tool] ExportDrugLabelMarkdown: Successfully exported document {DocumentGuid} with {SectionCount} sections",
                    parsedGuid, enrichedResponse.sectionCount);

                return JsonSerializer.Serialize(enrichedResponse);
            }

            // STEP 1: Search for products
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(unii))
                queryParams.Add($"unii={Uri.EscapeDataString(unii)}");

            if (!string.IsNullOrWhiteSpace(productNameSearch))
                queryParams.Add($"productNameSearch={Uri.EscapeDataString(productNameSearch)}");

            if (!string.IsNullOrWhiteSpace(activeIngredientSearch))
                queryParams.Add($"activeIngredientSearch={Uri.EscapeDataString(activeIngredientSearch)}");

            // Default pagination for search results
            queryParams.Add("pageNumber=1");
            queryParams.Add("pageSize=10");

            var searchEndpoint = $"api/Label/product/latest?{string.Join("&", queryParams)}";
            _logger.LogDebug("[Tool] ExportDrugLabelMarkdown: Searching products with {Endpoint}", searchEndpoint);

            var searchResult = await _apiClient.GetStringAsync(searchEndpoint);

            _logger.LogInformation("[Tool] ExportDrugLabelMarkdown: Product search completed");
            return searchResult;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] ExportDrugLabelMarkdown failed: documentGuid={DocumentGuid}, productName={ProductName}",
                documentGuid, productNameSearch);

            return JsonSerializer.Serialize(new
            {
                error = "Export failed",
                message = ex.Message
            });
        }

        #endregion
    }
}
