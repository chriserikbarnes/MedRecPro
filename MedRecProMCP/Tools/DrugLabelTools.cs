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
///
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │ search_expiring_patents ──► Patent expiration / generic availability    │
/// │                              │                                          │
/// │   Brand/generic search ──────┼──► Pre-rendered markdown table           │
/// │                              │    with FDA label links                  │
/// │                              │                                          │
/// │   Months horizon ────────────┼──► Upcoming generic availability         │
/// └─────────────────────────────────────────────────────────────────────────┘
///
/// ┌─────────────────────────────────────────────────────────────────────────┐
/// │ search_by_pharmacologic_class ──► Drug class/group discovery           │
/// │                                    │                                   │
/// │   AI-powered query ───────────────┼──► Products grouped by class      │
/// │                                    │    with FDA label links           │
/// │                                    │                                   │
/// │   Direct class name search ───────┼──► Raw product list (fallback)    │
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
/// **Use search_expiring_patents when user asks about PATENT EXPIRATION / GENERICS:**
/// - "When will generic Ozempic be available?"
/// - "What patents expire in the next 6 months?"
/// - "When will there be a semaglutide generic?"
/// - "What new generics will be available soon?"
/// - Any question about patent expiration, generic availability, or Orange Book patents
///
/// **Use search_by_pharmacologic_class when user asks about DRUG CLASSES / GROUPS:**
/// - "What beta blockers are available?"
/// - "List all SSRIs"
/// - "Show me statin drugs"
/// - "What drugs are in the ACE inhibitor class?"
/// - "Find all calcium channel blockers"
/// - "What opioids are in the database?"
/// - Any question about discovering GROUPS of drugs by therapeutic/pharmacologic class
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
/// **Find expiring patents by brand name:**
/// search_expiring_patents (tradeName="Ozempic") → Patent table with expiration dates + label links
///
/// **Find expiring patents by generic ingredient:**
/// search_expiring_patents (ingredient="semaglutide") → All patents for the ingredient
///
/// **Find patents expiring within a time window:**
/// search_expiring_patents (expiringInMonths=6) → All patents expiring in next 6 months
///
/// **Find drugs by pharmacologic class (AI-powered):**
/// search_by_pharmacologic_class (query="beta blockers") → All beta-adrenergic blockers with label links
///
/// **Find drugs by direct class name (fallback):**
/// search_by_pharmacologic_class (classNameSearch="Beta-Adrenergic Blockers") → Direct database search
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
using Microsoft.AspNetCore.Authorization;
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
///
/// The [Authorize] attribute ensures the MCP SDK returns a 401 challenge
/// with WWW-Authenticate headers when an unauthenticated client invokes
/// these tools, triggering the OAuth flow.
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/
[McpServerToolType]
#if !DEBUG
[Authorize]
#endif
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
    [McpServerTool(Name = "search_drug_labels", Title = "Search Drug Labels", ReadOnly = true, Destructive = false, OpenWorld = false)]
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
    [McpServerTool(Name = "export_drug_label_markdown", Title = "Export Drug Label", ReadOnly = true, Destructive = false, OpenWorld = false)]
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

    /**************************************************************/
    /// <summary>
    /// Searches for Orange Book NDA patents expiring within a specified time horizon,
    /// by trade/brand name, or by active ingredient.
    /// </summary>
    /// <remarks>
    /// ## Purpose
    /// Discovers when patent protection expires for branded drugs, enabling
    /// answers to questions about generic drug availability. Returns structured
    /// patent data with a pre-rendered markdown table containing FDA label links.
    ///
    /// ## Workflow
    /// This is an INDEPENDENT entry point — it does not require data from other tools.
    /// Results include:
    /// - **Patents**: Structured list of matching patent DTOs
    /// - **Markdown**: Pre-rendered markdown table ready for display
    /// - **TotalCount / TotalPages**: Pagination metadata
    ///
    /// ## Fallback Strategy: Brand vs Generic Name Ambiguity
    /// Users often don't know whether they're using a brand name or generic name.
    /// When a search by tradeName returns zero results, RETRY the same query using
    /// the ingredient parameter instead (and vice versa).
    ///
    /// Example fallback scenario:
    /// 1. User asks: "When will there be a generic semaglutide?"
    /// 2. Try tradeName="semaglutide" → 0 results (semaglutide is the generic name)
    /// 3. Retry with ingredient="semaglutide" → Patent results found
    /// 4. Present the markdown table from the successful response
    ///
    /// ## Open-Ended Date Range
    /// When expiringInMonths is omitted and tradeName or ingredient is provided,
    /// the API returns all future-expiring patents for the matching product.
    /// This supports questions like "when will there be a generic Ozempic?"
    /// where the caller doesn't know the expiration timeframe.
    ///
    /// ## Pediatric Exclusivity
    /// When a patent has a *PED companion (pediatric exclusivity), only the *PED row
    /// is returned — it carries the extended expiration date. Rows with pediatric
    /// exclusivity are marked with a warning emoji (&#x26A0;&#xFE0F;) in the markdown table.
    ///
    /// ## Pre-Rendered Markdown
    /// The API returns a Markdown field containing a properly formatted table.
    /// Render this markdown directly to the user. Columns:
    /// | Type | Application# | Prod# | Trade Name | Strength | Patent# | Expires |
    ///
    /// Trade names with a DocumentGUID become clickable links to the FDA label.
    /// A legend is appended when pediatric rows exist.
    ///
    /// ## Label Links
    /// When a patent row has a cross-referenced FDA label (DocumentGUID exists),
    /// the Trade Name column contains a clickable markdown link to the original
    /// FDA label. Not all products have label links — only those with SPL cross-references.
    /// </remarks>
    /// <param name="tradeName">Brand/trade name to search for. Supports partial matching.</param>
    /// <param name="ingredient">Active ingredient name to search for. Supports partial matching.</param>
    /// <param name="expiringInMonths">Number of months from today to search for expiring patents. Must be greater than 0.</param>
    /// <param name="pageNumber">Page number for pagination.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <returns>
    /// JSON response containing Patents (list), Markdown (pre-rendered table),
    /// TotalCount, and TotalPages.
    /// </returns>
    /// <example>
    /// <code>
    /// // When will generic Ozempic be available?
    /// SearchExpiringPatents(tradeName: "Ozempic")
    ///
    /// // Search by generic ingredient
    /// SearchExpiringPatents(ingredient: "semaglutide")
    ///
    /// // What patents expire in the next 6 months?
    /// SearchExpiringPatents(expiringInMonths: 6)
    ///
    /// // Combined: brand name + time horizon
    /// SearchExpiringPatents(tradeName: "Lipitor", expiringInMonths: 12)
    ///
    /// // FALLBACK: tradeName returns 0 results → retry with ingredient
    /// SearchExpiringPatents(ingredient: "semaglutide")
    /// </code>
    /// </example>
    /// <seealso cref="SearchDrugLabels"/>
    /// <seealso cref="ExportDrugLabelMarkdown"/>
    /// <seealso cref="MedRecProApiClient"/>
    /**************************************************************/
    [McpServerTool(Name = "search_expiring_patents", Title = "Search Expiring Patents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
    🔍 SEARCH: Find Orange Book NDA patents by expiration date, brand name, or active ingredient.

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ ⚠️ CRITICAL: TOOL SELECTION - READ THIS FIRST                           ┃
    ┃                                                                         ┃
    ┃ USE THIS TOOL (search_expiring_patents) when user asks:                 ┃
    ┃ • "When will generic Ozempic be available?"                             ┃
    ┃ • "What new generics will be available this month?"                     ┃
    ┃ • "What patents expire in the next N months?"                           ┃
    ┃ • "When will there be a semaglutide generic?"                           ┃
    ┃ • Any question about patent expiration or generic drug availability     ┃
    ┃                                                                         ┃
    ┃ USE search_drug_labels INSTEAD when user asks:                          ┃
    ┃ • "What are the side effects of Ozempic?"                               ┃
    ┃ • "What is semaglutide used for?"                                       ┃
    ┃ • Any question about a drug's LABEL CONTENT (dosing, warnings, etc.)   ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    📋 PURPOSE: Discover patent expiration dates and generic drug availability.
    ├── Returns: Patents (structured list), Markdown (pre-rendered table), TotalCount, TotalPages
    ├── 🔗 LINKS: Trade names appear as clickable FDA label links when a cross-referenced
    │   SPL label exists (DocumentGUID available). Not all rows have links.
    │   When links ARE present, they let users jump directly to the official FDA label.
    └── Pediatric exclusivity dates marked with ⚠️ emoji

    🎯 PARAMETER SELECTION - Choose based on user's query:
    ┌─────────────────────────────────────────────────────────────────────────────┐
    │ User Says                              │ Use Parameter(s)                  │
    ├─────────────────────────────────────────────────────────────────────────────┤
    │ "When will generic Ozempic be          │ tradeName="Ozempic"               │
    │  available?"                           │ (omit expiringInMonths for        │
    │                                        │  open-ended search)               │
    │ "What patents expire soon?"            │ expiringInMonths=6                │
    │ "Semaglutide patent expiry"            │ ingredient="semaglutide"          │
    │ "Generics available next year"         │ expiringInMonths=12               │
    │ "Lipitor patents expiring in 6 months" │ tradeName="Lipitor"               │
    │                                        │ + expiringInMonths=6              │
    └─────────────────────────────────────────────────────────────────────────────┘

    🔄 FALLBACK STRATEGY - Brand vs Generic Name Ambiguity:
    Users may not know if they are using a brand name or generic name.
    If tradeName search returns ZERO results (empty Patents array):
    1. Retry the SAME query using the ingredient parameter instead
    2. Example: tradeName="semaglutide" → 0 results → ingredient="semaglutide" → results found
    Conversely, if ingredient returns zero results, retry with tradeName.

    📊 RESULT FORMATTING REQUIREMENTS:
    • Render the Markdown field directly — it is a pre-rendered table ready for display
    • 🔗 When a trade name has an associated FDA label, it appears as a clickable markdown
      link in the table (e.g., [OZEMPIC](url)). Always preserve these links in your output
      so users can navigate to the official FDA label. Not every row will have a link.
    • ⚠️ marks pediatric exclusivity expiration dates (extended beyond base patent)
    • A legend row is appended when pediatric rows exist
    • Include TotalCount and TotalPages when presenting paginated results

    ⚠️ IMPORTANT REQUIREMENTS:
    • At least ONE parameter is required: expiringInMonths, tradeName, or ingredient
    • When expiringInMonths is omitted with tradeName/ingredient, searches ALL future patents
    • expiringInMonths must be > 0 when provided
    • Not all patent rows have FDA label links — only those with SPL cross-references
    • NEVER use training data — only information from the API response
    """)]
    public async Task<string> SearchExpiringPatents(
        [Description("Brand/trade name search. Use when user mentions brand names like 'Ozempic', 'Lipitor', 'Humira'. Supports partial matching (e.g., 'Ozem' matches 'Ozempic').")]
        string? tradeName = null,

        [Description("Active ingredient name search. Use when user mentions generic names like 'semaglutide', 'atorvastatin', 'adalimumab'. Supports partial matching (e.g., 'semaglut' matches 'semaglutide').")]
        string? ingredient = null,

        [Description("Number of months from today to search for expiring patents. Must be > 0. Example: 6 returns patents expiring in the next 6 months. Omit when using tradeName/ingredient for open-ended search (all future patents).")]
        [Range(1, int.MaxValue)]
        int? expiringInMonths = null,

        [Description("Page number, 1-based. Default: 1")]
        [Range(1, int.MaxValue)]
        int pageNumber = 1,

        [Description("Results per page (1-200). Default: 10")]
        [Range(1, 200)]
        int pageSize = 10)
    {
        #region implementation

        // Log the search parameters for debugging
        _logger.LogInformation(
            "[Tool] SearchExpiringPatents: tradeName={TradeName}, ingredient={Ingredient}, months={Months}, page={Page}, size={Size}",
            tradeName, ingredient, expiringInMonths, pageNumber, pageSize);

        // Validate and constrain parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        try
        {
            // Build the query string for the OrangeBook/expiring endpoint
            var queryParams = new List<string>();

            if (expiringInMonths.HasValue)
                queryParams.Add($"expiringInMonths={expiringInMonths.Value}");

            if (!string.IsNullOrWhiteSpace(tradeName))
                queryParams.Add($"tradeName={Uri.EscapeDataString(tradeName)}");

            if (!string.IsNullOrWhiteSpace(ingredient))
                queryParams.Add($"ingredient={Uri.EscapeDataString(ingredient)}");

            // Always add pagination parameters
            queryParams.Add($"pageNumber={pageNumber}");
            queryParams.Add($"pageSize={pageSize}");

            // Construct the endpoint URL
            var endpoint = $"api/OrangeBook/expiring?{string.Join("&", queryParams)}";

            // Execute the API call
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] SearchExpiringPatents failed: tradeName={TradeName}, ingredient={Ingredient}, months={Months}",
                tradeName, ingredient, expiringInMonths);

            return JsonSerializer.Serialize(new
            {
                error = "Patent search failed",
                message = ex.Message
            });
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Searches for FDA drug products by pharmacologic or therapeutic class using AI-powered terminology matching.
    /// Discovers GROUPS of drugs in a class (e.g., beta blockers, SSRIs, statins) rather than individual products.
    /// </summary>
    /// <remarks>
    /// ## Purpose
    /// Finds all FDA-labeled drug products belonging to a pharmacologic or therapeutic class.
    /// Uses AI-powered terminology matching to translate common drug class names (like "beta blockers")
    /// to their formal FDA pharmacologic class names (like "Beta-Adrenergic Blockers [EPC]").
    ///
    /// ## Workflow
    /// This is an INDEPENDENT entry point for drug class discovery.
    /// Results include:
    /// - **matchedClasses**: The formal pharmacologic class names matched by AI
    /// - **productsByClass**: All products organized by class, each with a DocumentGUID
    /// - **labelLinks**: Pre-built clickable links to every product's FDA label
    /// - **totalProductCount**: Total number of products found across all matched classes
    /// - **explanation**: How the AI mapped the user's query to formal class names
    /// - **suggestedFollowUps**: Recommended next queries for deeper exploration
    ///
    /// ## Pharmacologic Class Types (Suffix Codes)
    /// The FDA categorizes drugs into four class types:
    /// - **[EPC]** — Established Pharmacologic Class: Most common. Mechanism-based grouping.
    ///   Examples: Beta-Adrenergic Blockers [EPC], HMG-CoA Reductase Inhibitors [EPC]
    /// - **[MoA]** — Mechanism of Action: How the drug works at the molecular level.
    ///   Examples: Cyclooxygenase Inhibitors [MoA], Sodium Channel Blockers [MoA]
    /// - **[Chemical/Ingredient]** — Chemical Structure: Grouped by chemical family.
    ///   Examples: Aminoglycosides [Chemical/Ingredient], Sulfonamides [Chemical/Ingredient]
    /// - **[CS/PE]** — Chemical Structure / Physiologic Effect: Dual classification.
    ///   Examples: Fluoroquinolones [CS/PE]
    ///
    /// ## Common Terminology Mappings
    /// The AI endpoint handles these translations automatically:
    /// | User Says              | Formal Class Name                                  |
    /// |------------------------|-----------------------------------------------------|
    /// | beta blockers          | Beta-Adrenergic Blockers [EPC]                      |
    /// | SSRIs                  | Selective Serotonin Reuptake Inhibitors [EPC]       |
    /// | statins                | HMG-CoA Reductase Inhibitors [EPC]                  |
    /// | ACE inhibitors         | Angiotensin Converting Enzyme Inhibitors [EPC]      |
    /// | calcium channel blockers| Calcium Channel Blockers [EPC]                     |
    /// | proton pump inhibitors | Proton Pump Inhibitors [EPC]                        |
    /// | opioids                | Opioid Agonists [EPC]                               |
    /// | benzodiazepines        | Benzodiazepines [EPC]                               |
    /// | NSAIDs                 | Non-steroidal Anti-inflammatory Drugs [EPC]         |
    /// | anticoagulants         | Anticoagulants [EPC]                                |
    /// | diuretics              | Diuretics [EPC]                                     |
    /// | aminoglycosides        | Aminoglycosides [Chemical/Ingredient]               |
    ///
    /// ## Fallback Strategy
    /// If the AI-powered `query` parameter returns no results or fails,
    /// retry the same search using `classNameSearch` for direct partial matching.
    ///
    /// ## MANDATORY: Label Links
    /// Every product in the response has a clickable FDA label link in the `labelLinks` field.
    /// These MUST be presented to the user for source verification.
    ///
    /// ## Key Distinction from Other Tools
    /// - **search_drug_labels**: Find a SPECIFIC drug by name and get its label content
    /// - **search_by_pharmacologic_class**: Find ALL drugs in a therapeutic GROUP/CLASS
    /// - **export_drug_label_markdown**: Get a complete label document for ONE specific drug
    /// </remarks>
    /// <param name="query">Natural language drug class search term.</param>
    /// <param name="classNameSearch">Direct partial match on pharmacologic class names (fallback/advanced).</param>
    /// <param name="maxProductsPerClass">Maximum number of products to return per matched class.</param>
    /// <param name="pageNumber">Page number for pagination (classNameSearch mode only).</param>
    /// <param name="pageSize">Results per page (classNameSearch mode only).</param>
    /// <returns>
    /// JSON response containing matched classes, products grouped by class, label links,
    /// and suggested follow-up queries.
    /// </returns>
    /// <example>
    /// <code>
    /// // AI-powered search (recommended)
    /// SearchByPharmacologicClass(query: "beta blockers")
    ///
    /// // Direct class name search (fallback)
    /// SearchByPharmacologicClass(classNameSearch: "Beta-Adrenergic Blockers")
    ///
    /// // Limit products per class
    /// SearchByPharmacologicClass(query: "SSRIs", maxProductsPerClass: 10)
    /// </code>
    /// </example>
    /// <seealso cref="SearchDrugLabels"/>
    /// <seealso cref="ExportDrugLabelMarkdown"/>
    /// <seealso cref="SearchExpiringPatents"/>
    /**************************************************************/
    [McpServerTool(Name = "search_by_pharmacologic_class", Title = "Search by Pharmacologic Class", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
    🔍 SEARCH: Discover GROUPS of drugs by therapeutic or pharmacologic class (e.g., beta blockers, SSRIs, statins).

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ ⚠️ CRITICAL: TOOL SELECTION — READ THIS FIRST                              ┃
    ┃                                                                             ┃
    ┃ USE THIS TOOL (search_by_pharmacologic_class) when user asks:               ┃
    ┃ • "What beta blockers are available?"                                       ┃
    ┃ • "List all SSRIs"                                                          ┃
    ┃ • "Show me statin drugs"                                                    ┃
    ┃ • "What drugs are in the ACE inhibitor class?"                              ┃
    ┃ • "Find all calcium channel blockers"                                       ┃
    ┃ • "What opioids are in the database?"                                       ┃
    ┃ • "List anticoagulant medications"                                          ┃
    ┃ • "What benzodiazepines are there?"                                         ┃
    ┃ • "Show me NSAID products"                                                  ┃
    ┃ • "What proton pump inhibitors are available?"                              ┃
    ┃ • "Find aminoglycoside antibiotics"                                         ┃
    ┃ • "What diuretics do you have?"                                             ┃
    ┃ • Any question about discovering GROUPS of drugs by therapeutic class       ┃
    ┃                                                                             ┃
    ┃ USE search_drug_labels INSTEAD when user asks:                              ┃
    ┃ • "What are the side effects of metoprolol?"                                ┃
    ┃ • "What is atorvastatin used for?"                                          ┃
    ┃ • "What are the warnings for Lipitor?"                                      ┃
    ┃ • Any SPECIFIC QUESTION about a SINGLE drug's label content                ┃
    ┃                                                                             ┃
    ┃ USE export_drug_label_markdown INSTEAD when user asks:                      ┃
    ┃ • "Show me the full label for Lipitor"                                      ┃
    ┃ • Any request for COMPLETE/FULL label information for ONE drug              ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    📋 PURPOSE: Discover all FDA-labeled products in a pharmacologic/therapeutic class.
    ├── Returns: matchedClasses, productsByClass (grouped), labelLinks, totalProductCount
    ├── AI-powered: Translates common terms (beta blockers → Beta-Adrenergic Blockers [EPC])
    ├── Every product includes a clickable FDA label link for source verification
    └── Next: Use DocumentGUID from results → 'search_drug_labels' for specific section content,
             or → 'export_drug_label_markdown' for complete label export

    ┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
    ┃ 🚨 MANDATORY: Present labelLinks for EVERY product in EVERY response 🚨    ┃
    ┃                                                                             ┃
    ┃ The response contains a labelLinks dictionary with pre-built links:         ┃
    ┃   "View Full Label (METOPROLOL TARTRATE)": "https://host/api/Label/..."     ┃
    ┃                                                                             ┃
    ┃ Format each link as: [View Full Label ({ProductName})]({labelLink})         ┃
    ┃                                                                             ┃
    ┃ This is NON-NEGOTIABLE. Users need these links for source verification.     ┃
    ┃ Present ALL label links — do not truncate or omit any.                      ┃
    ┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

    💊 PHARMACOLOGIC CLASS TYPES (suffix codes in class names):
    • [EPC] — Established Pharmacologic Class (most common, mechanism-based)
    • [MoA] — Mechanism of Action (molecular-level mechanism)
    • [Chemical/Ingredient] — Chemical Structure (grouped by chemical family)
    • [CS/PE] — Chemical Structure / Physiologic Effect (dual classification)

    🎯 PARAMETER SELECTION — Choose based on user's query:
    ┌─────────────────────────────────────────────────────────────────────────────┐
    │ User Says                              │ Use Parameter(s)                  │
    ├─────────────────────────────────────────────────────────────────────────────┤
    │ "What beta blockers are available?"    │ query="beta blockers"             │
    │ "List all SSRIs"                       │ query="SSRIs"                     │
    │ "Show me statin drugs"                 │ query="statins"                   │
    │ "ACE inhibitor medications"            │ query="ACE inhibitors"            │
    │ "What opioids are there?"              │ query="opioids"                   │
    │ "NSAIDs in the database"               │ query="NSAIDs"                    │
    │ "Proton pump inhibitor drugs"          │ query="proton pump inhibitors"    │
    │ "Benzodiazepine medications"           │ query="benzodiazepines"           │
    │ "Calcium channel blockers"             │ query="calcium channel blockers"  │
    │ "Anticoagulant drugs"                  │ query="anticoagulants"            │
    │ "Diuretic medications"                 │ query="diuretics"                 │
    │ "Aminoglycoside antibiotics"           │ query="aminoglycosides"           │
    │ "Blood pressure medications"           │ query="blood pressure medications"│
    └─────────────────────────────────────────────────────────────────────────────┘

    📚 COMMON TERMINOLOGY MAPPINGS (handled automatically by AI):
    • "beta blockers" → Beta-Adrenergic Blockers [EPC]
    • "SSRIs" → Selective Serotonin Reuptake Inhibitors [EPC]
    • "statins" → HMG-CoA Reductase Inhibitors [EPC]
    • "ACE inhibitors" → Angiotensin Converting Enzyme Inhibitors [EPC]
    • "calcium channel blockers" → Calcium Channel Blockers [EPC]
    • "PPIs" / "proton pump inhibitors" → Proton Pump Inhibitors [EPC]
    • "opioids" → Opioid Agonists [EPC]
    • "benzos" / "benzodiazepines" → Benzodiazepines [EPC]
    • "NSAIDs" → Non-steroidal Anti-inflammatory Drugs [EPC]
    • "blood thinners" / "anticoagulants" → Anticoagulants [EPC]
    • "water pills" / "diuretics" → Diuretics [EPC]
    • "aminoglycosides" → Aminoglycosides [Chemical/Ingredient]

    🔄 FALLBACK STRATEGY — When query returns no results:
    If the AI-powered query parameter returns no results or empty matchedClasses:
    1. Retry the SAME search using classNameSearch parameter instead
    2. Example: query="xyz blockers" → 0 results → classNameSearch="xyz blockers" → partial match
    The classNameSearch parameter does direct partial matching against database class names.

    📊 RESULT FORMATTING REQUIREMENTS:
    • Present products grouped by pharmacologic class name
    • Include the class type suffix in brackets: [EPC], [MoA], [Chemical/Ingredient], [CS/PE]
    • Show totalProductCount and number of matched classes
    • **MANDATORY**: Present ALL labelLinks as clickable markdown links
    • Format: [View Full Label ({ProductName})]({labelLink})
    • Include the explanation field showing how the AI mapped the query
    • Present suggestedFollowUps to guide the user to deeper exploration
    • Use actual product names from API — NEVER use placeholders

    ⚠️ IMPORTANT REQUIREMENTS:
    • Use query parameter for natural language searches (recommended)
    • Use classNameSearch only as fallback or when exact class name is known
    • The query parameter handles ALL terminology translation — do NOT pre-translate terms
    • NEVER use training data — only information from the API response
    • ALWAYS present label links for every product found
    """)]
    public async Task<string> SearchByPharmacologicClass(
        [Description("Natural language drug class search (AI-powered, recommended). Use common terms like 'beta blockers', 'SSRIs', 'statins', 'ACE inhibitors', 'opioids', 'NSAIDs'. The API handles terminology translation automatically.")]
        string? query = null,

        [Description("Direct partial match on pharmacologic class names (fallback/advanced). Use when you already know the exact class name like 'Beta-Adrenergic Blockers' or when the query parameter returns no results. Supports partial matching.")]
        string? classNameSearch = null,

        [Description("Maximum products to return per matched class (1-1000). Lower values for overview, higher for comprehensive lists. Default: 500")]
        [Range(1, 1000)]
        int maxProductsPerClass = 500,

        [Description("Page number, 1-based. Used only with classNameSearch mode. Default: 1")]
        [Range(1, int.MaxValue)]
        int pageNumber = 1,

        [Description("Results per page (1-200). Used only with classNameSearch mode. Default: 25")]
        [Range(1, 200)]
        int pageSize = 25)
    {
        #region implementation

        _logger.LogInformation(
            "[Tool] SearchByPharmacologicClass: query={Query}, classNameSearch={ClassNameSearch}, maxPerClass={MaxPerClass}, page={Page}, size={Size}",
            query, classNameSearch, maxProductsPerClass, pageNumber, pageSize);

        // Validate that at least one search parameter is provided
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(classNameSearch))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Missing search parameter",
                message = "Either 'query' (for AI-powered search) or 'classNameSearch' (for direct search) is required. Use 'query' for natural language terms like 'beta blockers', 'SSRIs', 'statins'."
            });
        }

        // Validate and constrain parameters
        maxProductsPerClass = Math.Clamp(maxProductsPerClass, 1, 1000);
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        try
        {
            // Build the query string for the pharmacologic-class/search endpoint
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(query))
                queryParams.Add($"query={Uri.EscapeDataString(query)}");

            if (!string.IsNullOrWhiteSpace(classNameSearch))
                queryParams.Add($"classNameSearch={Uri.EscapeDataString(classNameSearch)}");

            queryParams.Add($"maxProductsPerClass={maxProductsPerClass}");

            // Pagination parameters apply to classNameSearch mode
            if (!string.IsNullOrWhiteSpace(classNameSearch))
            {
                queryParams.Add($"pageNumber={pageNumber}");
                queryParams.Add($"pageSize={pageSize}");
            }

            var endpoint = $"api/Label/pharmacologic-class/search?{string.Join("&", queryParams)}";

            var result = await _apiClient.GetStringAsync(endpoint);

            // When using query mode, rewrite relative labelLinks to absolute URLs
            if (!string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    var responseDoc = JsonSerializer.Deserialize<JsonElement>(result);

                    if (responseDoc.TryGetProperty("labelLinks", out var labelLinks)
                        && labelLinks.ValueKind == JsonValueKind.Object)
                    {
                        // Construct base URL (same pattern as ExportDrugLabelMarkdown)
                        var baseUrl = _apiSettings.BaseUrl.TrimEnd('/');
                        if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                            baseUrl = baseUrl[..^4];

                        // Build dictionary with absolute URLs
                        var absoluteLinks = new Dictionary<string, string>();
                        foreach (var link in labelLinks.EnumerateObject())
                        {
                            var relativeUrl = link.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(relativeUrl))
                                absoluteLinks[link.Name] = $"{baseUrl}{relativeUrl}";
                        }

                        // Re-serialize with absolute label links
                        var responseDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result);
                        if (responseDict != null)
                        {
                            responseDict["labelLinks"] = JsonSerializer.SerializeToElement(absoluteLinks);
                            return JsonSerializer.Serialize(responseDict);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    // If JSON manipulation fails, return the raw result unchanged
                    _logger.LogWarning(ex, "[Tool] SearchByPharmacologicClass: Failed to rewrite labelLinks to absolute URLs");
                }
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] SearchByPharmacologicClass failed: query={Query}, classNameSearch={ClassNameSearch}",
                query, classNameSearch);

            return JsonSerializer.Serialize(new
            {
                error = "Pharmacologic class search failed",
                message = ex.Message
            });
        }

        #endregion
    }
}
