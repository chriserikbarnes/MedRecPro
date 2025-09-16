using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.DataAccess;
using System;
using System.Threading.Tasks;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses product marketing and regulatory elements, including marketing categories (approvals)
    /// and DEA policies from an SPL document. Marketing status parsing is delegated to the dedicated MarketingStatusParser.
    /// </summary>
    /// <remarks>
    /// This parser focuses on the commercial and regulatory aspects of a product. It is designed to be
    /// called by a parent parser (like ManufacturedProductParser) and assumes that the current product
    /// context has been set. Marketing status parsing has been separated into its own dedicated parser
    /// to support independent usage and packaging-level marketing status processing.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="MarketingCategory"/>
    /// <seealso cref="MarketingStatusParser"/>
    /// <seealso cref="Policy"/>
    /// <seealso cref="SplParseContext"/>
    public class ProductMarketingParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        /// <seealso cref="Label"/>
        public string SectionName => "productmarketing";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        /// <seealso cref="Label"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an XML element to extract and save product marketing and regulatory information.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method orchestrates the parsing of marketing and policy data by calling specialized
        /// parsers for each section. It requires `context.CurrentProduct` to be set by the caller.
        /// Marketing status parsing is now delegated to the dedicated MarketingStatusParser to maintain
        /// separation of concerns and support independent usage.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="MarketingStatusParser"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that a product is available in the context to link entities to.
            if (context?.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse product marketing information because no product context exists.");
                context?.Logger?.LogError("ProductMarketingParser was called without a valid product in the context.");
                return result;
            }
            var product = context.CurrentProduct;

            try
            {
                // --- PARSE MARKETING CATEGORY ---
                reportProgress?.Invoke($"Starting Marketing Category XML Elements {context.FileNameInZip}");
                var marketingCatCreated = await parseAndSaveMarketingCategoriesAsync(element, product, context);
                result.ProductElementsCreated += marketingCatCreated;

                // --- PARSE MARKETING STATUS USING DEDICATED PARSER ---
                reportProgress?.Invoke($"Starting Marketing Status XML Elements {context.FileNameInZip}");
                var marketingStatusParser = new MarketingStatusParser();
                var marketingStatusResult = await marketingStatusParser.ParseAsync(element, context, reportProgress);

                // Merge results from the marketing status parser
                result.MergeFrom(marketingStatusResult);

                // --- PARSE POLICY ---
                reportProgress?.Invoke($"Starting Policy XML Elements {context.FileNameInZip}");
                var policyCt = await parseAndSavePoliciesAsync(element, product, context);
                result.ProductElementsCreated += policyCt;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error in ProductMarketingParser: {ex.Message}");
                context?.Logger?.LogError(ex, "Error in ProductMarketingParser.ParseAsync");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all MarketingCategory entities under [subjectOf][approval] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (either [manufacturedProduct] or [partProduct]) to scan for marketing categories.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of MarketingCategory records created.</returns>
        /// <remarks>
        /// Extracts marketing category information from approval nodes including category codes, 
        /// application/monograph IDs, approval dates, and territory codes. Handles the complex
        /// XML structure of subjectOf/approval elements according to SPL standards.
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveMarketingCategoriesAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<MarketingCategory>();

            if (context == null || repo == null || context.Logger == null)
            {
                return count;
            }

            // Find all <subjectOf><approval> nodes for processing
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                var approvalEl = subjOf.SplElement(sc.E.Approval);
                if (approvalEl == null)
                    continue;

                // 1. <id> - Application/monograph number and root
                var idEl = approvalEl.SplElement(sc.E.Id);
                string? idExtension = idEl?.GetAttrVal(sc.A.Extension);
                string? idRoot = idEl?.GetAttrVal(sc.A.Root);

                // 2. <code> - Marketing category code, codeSystem, displayName
                var codeEl = approvalEl.SplElement(sc.E.Code);
                string? categoryCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? categoryCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? categoryDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                // 3. <effectiveTime><low value="YYYYMMDD"> - Parse with Util.ParseNullableDateTime
                DateTime? approvalDate = null;
                var effTimeEl = approvalEl.SplElement(sc.E.EffectiveTime);
                var lowEl = effTimeEl?.SplElement(sc.E.Low);
                string? dateStr = lowEl?.GetAttrVal(sc.A.Value);

                if (!string.IsNullOrWhiteSpace(dateStr))
                {
                    approvalDate = Util.ParseNullableDateTime(dateStr);
                }

                // 4. <author><territorialAuthority><territory><code code="USA">
                string? territoryCode = null;
                var terrCodeEl = approvalEl
                    .SplElement(sc.E.Author)?
                    .SplElement(sc.E.TerritorialAuthority)?
                    .SplElement(sc.E.Territory)?
                    .SplElement(sc.E.Code);

                if (terrCodeEl != null)
                    territoryCode = terrCodeEl.GetAttrVal(sc.A.CodeValue);

                // 5. Build and save the marketing category entity
                var marketingCategory = new MarketingCategory
                {
                    ProductID = product.ProductID,
                    CategoryCode = categoryCode,
                    CategoryCodeSystem = categoryCodeSystem,
                    CategoryDisplayName = categoryDisplayName,
                    ApplicationOrMonographIDValue = idExtension,
                    ApplicationOrMonographIDOID = idRoot,
                    ApprovalDate = approvalDate,
                    TerritoryCode = territoryCode,
                };

                // Persist the marketing category to the database
                await repo.CreateAsync(marketingCategory);
                count++;
                context.Logger.LogInformation($"MarketingCategory created: ProductID={product.ProductID}, Code={categoryCode}, ApplicationID={idExtension}");
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all DEA Policy entities under [subjectOf][policy] nodes for a given product.
        /// </summary>
        /// <param name="parentEl">XElement (usually [manufacturedProduct]) to scan for DEA schedule policies.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of Policy records created.</returns>
        /// <remarks>
        /// Handles DEA schedule code, system, display name, and class code according to SPL IG Section 3.2.11.
        /// Only processes policies with classCode="DEADrugSchedule" and correct FDA code system.
        /// Requires both policy code and display name to be present for data integrity.
        /// </remarks>
        /// <seealso cref="Policy"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSavePoliciesAsync(
            XElement parentEl,
            Product product,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<Policy>();

            // Validate required dependencies before processing
            if (context == null || repo == null || context.Logger == null)
                return count;

            // Process all subjectOf/policy structures
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                foreach (var policyEl in subjOf.SplElements(sc.E.Policy))
                {
                    // <policy> must have classCode="DEADrugSchedule"
                    string? classCode = policyEl.GetAttrVal(sc.A.ClassCode);
                    if (!string.Equals(classCode, "DEADrugSchedule", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // <code> is required for DEA schedule identification
                    var codeEl = policyEl.GetSplElement(sc.E.Code);
                    string? policyCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                    string? policyCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                    string? displayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                    // Only allow correct FDA SPL code system
                    if (policyCodeSystem != "2.16.840.1.113883.3.26.1.1")
                        continue;

                    // Display name must be present and match code (for safety, allow override if needed)
                    if (string.IsNullOrWhiteSpace(policyCode) || string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Build and save the Policy entity
                    var policy = new Policy
                    {
                        ProductID = product.ProductID,
                        PolicyClassCode = classCode,
                        PolicyCode = policyCode,
                        PolicyCodeSystem = policyCodeSystem,
                        PolicyDisplayName = displayName
                    };

                    await repo.CreateAsync(policy);
                    count++;
                    context.Logger.LogInformation(
                        $"Policy (DEA Schedule) created: ProductID={product.ProductID}, PolicyCode={policyCode}, DisplayName={displayName}");
                }
            }

            return count;
            #endregion
        }
    }
}