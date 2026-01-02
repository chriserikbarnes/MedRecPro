using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using static MedRecProImportClass.Models.Label;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Data;
using Microsoft.EntityFrameworkCore;

namespace MedRecProImportClass.Service.ParsingServices
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
        /// <seealso cref="MedRecProImportClass.Models.Constant"/>
        /// <seealso cref="Label"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /**************************************************************/
        /// <summary>
        /// Result container for get-or-save operations.
        /// </summary>
        /// <typeparam name="T">The entity type.</typeparam>
        /// <remarks>
        /// Provides information about whether an entity was newly created or already existed.
        /// </remarks>
        /// <seealso cref="Label"/>
        private class getOrSaveResult<T>
        {
            #region implementation

            /// <summary>
            /// The entity that was retrieved or created.
            /// </summary>
            public T Entity { get; set; } = default!;

            /// <summary>
            /// Indicates whether the entity was newly created (true) or already existed (false).
            /// </summary>
            public bool WasCreated { get; set; }

            #endregion
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an XML element to extract and save product marketing and regulatory information.
        /// </summary>
        /// <param name="element">The XElement representing the product section to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
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
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
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
        /// Maintains backward compatibility while implementing get-or-save pattern internally.
        /// </summary>
        /// <param name="parentEl">XElement (either [manufacturedProduct] or [partProduct]) to scan for marketing categories.</param>
        /// <param name="product">The Product entity associated.</param>
        /// <param name="context">The parsing context (repo, logger, etc).</param>
        /// <returns>The count of MarketingCategory records created (new records only for backward compatibility).</returns>
        /// <remarks>
        /// Extracts marketing category information from approval nodes including category codes, 
        /// application/monograph IDs, approval dates, and territory codes. Handles the complex
        /// XML structure of subjectOf/approval elements according to SPL standards.
        /// Uses get-or-save pattern internally to prevent duplicates.
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
            int createdCount = 0;
            var repo = context.GetRepository<MarketingCategory>();
            if (context == null || repo == null || context.Logger == null)
            {
                return createdCount;
            }

            // Find all <subjectOf><approval> nodes for processing
            foreach (var subjOf in parentEl.SplElements(sc.E.SubjectOf))
            {
                var approvalEl = subjOf.SplElement(sc.E.Approval);
                if (approvalEl == null)
                    continue;

                // Parse the marketing category data from XML
                var categoryData = parseMarketingCategoryFromApproval(approvalEl, product.ProductID);

                // Get or save the marketing category (prevents duplicates)
                var result = await getOrSaveMarketingCategoryAsync(categoryData, repo, context);

                // Only count newly created records for backward compatibility
                if (result.WasCreated)
                {
                    createdCount++;
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses marketing category data from an approval XML element.
        /// </summary>
        /// <param name="approvalEl">The approval XElement containing marketing category data.</param>
        /// <param name="productId">The associated product ID.</param>
        /// <returns>A MarketingCategory entity populated with parsed data.</returns>
        /// <remarks>
        /// Extracts all relevant marketing category fields from the approval node including
        /// ID extension/root, category codes, approval dates, and territory information.
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="Label"/>
        private MarketingCategory parseMarketingCategoryFromApproval(XElement approvalEl, int? productId)
        {
            #region implementation

            #region id_parsing
            // 1. <id> - Application/monograph number and root
            var idEl = approvalEl.SplElement(sc.E.Id);
            string? idExtension = idEl?.GetAttrVal(sc.A.Extension);
            string? idRoot = idEl?.GetAttrVal(sc.A.Root);
            #endregion

            #region code_parsing
            // 2. <code> - Marketing category code, codeSystem, displayName
            var codeEl = approvalEl.SplElement(sc.E.Code);
            string? categoryCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            string? categoryCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            string? categoryDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);
            #endregion

            #region date_parsing
            // 3. <effectiveTime><low value="YYYYMMDD"> - Parse with Util.ParseNullableDateTime
            DateTime? approvalDate = null;
            var effTimeEl = approvalEl.SplElement(sc.E.EffectiveTime);
            var lowEl = effTimeEl?.SplElement(sc.E.Low);
            string? dateStr = lowEl?.GetAttrVal(sc.A.Value);
            if (!string.IsNullOrWhiteSpace(dateStr))
            {
                approvalDate = Util.ParseNullableDateTime(dateStr);
            }
            #endregion

            #region territory_parsing
            // 4. <author><territorialAuthority><territory><code code="USA">
            string? territoryCode = null;
            var terrCodeEl = approvalEl
                .SplElement(sc.E.Author)?
                .SplElement(sc.E.TerritorialAuthority)?
                .SplElement(sc.E.Territory)?
                .SplElement(sc.E.Code);
            if (terrCodeEl != null)
                territoryCode = terrCodeEl.GetAttrVal(sc.A.CodeValue);
            #endregion

            #region entity_creation
            return new MarketingCategory
            {
                ProductID = productId,
                CategoryCode = categoryCode,
                CategoryCodeSystem = categoryCodeSystem,
                CategoryDisplayName = categoryDisplayName,
                ApplicationOrMonographIDValue = idExtension,
                ApplicationOrMonographIDOID = idRoot,
                ApprovalDate = approvalDate,
                TerritoryCode = territoryCode,
            };
            #endregion

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing MarketingCategory or saves a new one to prevent duplicates.
        /// </summary>
        /// <param name="categoryData">The MarketingCategory entity to get or save.</param>
        /// <param name="repo">The repository for MarketingCategory operations.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A result containing the entity and whether it was newly created.</returns>
        /// <remarks>
        /// Uses composite key matching (ProductID + CategoryCode + ApplicationOrMonographIDValue) 
        /// to identify existing records. Updates existing records if data has changed.
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<getOrSaveResult<MarketingCategory>> getOrSaveMarketingCategoryAsync(
            MarketingCategory categoryData,
            Repository<MarketingCategory> repo,
            SplParseContext context)
        {

            if(context?.ServiceProvider == null || repo == null || context.Logger == null)
            {
                return new getOrSaveResult<MarketingCategory>
                {
                    Entity = categoryData,
                    WasCreated = false
                };
            }

            #region implementation
            try
            {
                #region duplicate_check
                // Use the DbContext directly for the specific 'find by composite key' query
                var dbContext = context.GetDbContext();

                // Get the DbSet for MarketingCategory
                var categoryDbSet = dbContext.Set<MarketingCategory>();

                // Search for existing category with the same ProductID, CategoryCode, and ApplicationOrMonographIDValue
                var existingCategory = await categoryDbSet
                    .FirstOrDefaultAsync(mc => mc != null
                        && mc.ProductID == categoryData.ProductID
                        && mc.CategoryCode != null
                        && mc.CategoryCode == categoryData.CategoryCode
                        && mc.ApplicationOrMonographIDValue != null
                        && mc.ApplicationOrMonographIDValue == categoryData.ApplicationOrMonographIDValue);

                // Fallback search by ProductID and CategoryCode only if ApplicationOrMonographIDValue is null/empty
                if ((existingCategory == null || existingCategory.MarketingCategoryID <= 0)
                    && categoryData.CategoryCode != null && !string.IsNullOrWhiteSpace(categoryData.CategoryCode))
                {
                    existingCategory = await categoryDbSet
                        .FirstOrDefaultAsync(mc => mc != null
                            && mc.ProductID == categoryData.ProductID
                            && !string.IsNullOrEmpty(mc.CategoryCode)
                            && mc.CategoryCode == categoryData.CategoryCode
                            && (mc.ApplicationOrMonographIDValue == null || mc.ApplicationOrMonographIDValue == string.Empty));
                }

                // Return existing category if found
                if (existingCategory != null)
                {
                    context.Logger.LogDebug("Found existing MarketingCategory for ProductID {ProductID} with CategoryCode '{CategoryCode}' and ApplicationID '{ApplicationID}'",
                        categoryData.ProductID, categoryData.CategoryCode, categoryData.ApplicationOrMonographIDValue);

                    // Check if update is needed for existing category
                    bool hasChanges = checkForMarketingCategoryChanges(existingCategory, categoryData);

                    if (hasChanges)
                    {
                        updateMarketingCategoryFields(existingCategory, categoryData);

                        await dbContext.SaveChangesAsync();

                        context.Logger.LogInformation($"MarketingCategory updated: ProductID={categoryData.ProductID}, Code={categoryData.CategoryCode}, ApplicationID={categoryData.ApplicationOrMonographIDValue}");
                    }

                    return new getOrSaveResult<MarketingCategory>
                    {
                        Entity = existingCategory,
                        WasCreated = false
                    };
                }
                #endregion

                #region new_entity_creation
                // Entity doesn't exist - create new one using repository pattern
                await repo.CreateAsync(categoryData);

                context.Logger.LogInformation($"MarketingCategory created: ProductID={categoryData.ProductID}, Code={categoryData.CategoryCode}, ApplicationID={categoryData.ApplicationOrMonographIDValue}");

                // Return entity if successfully created with valid ID
                return new getOrSaveResult<MarketingCategory>
                {
                    Entity = categoryData,
                    WasCreated = true
                };
                #endregion
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating or updating MarketingCategory entity");
                return new getOrSaveResult<MarketingCategory>
                {
                    Entity = categoryData,
                    WasCreated = false
                };
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if an existing MarketingCategory has changes compared to new data.
        /// </summary>
        /// <param name="existing">The existing MarketingCategory entity.</param>
        /// <param name="newData">The new MarketingCategory data to compare against.</param>
        /// <returns>True if there are changes that require an update, false otherwise.</returns>
        /// <remarks>
        /// Compares all updatable fields to determine if the existing entity needs to be updated.
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="Label"/>
        private bool checkForMarketingCategoryChanges(MarketingCategory existing, MarketingCategory newData)
        {
            #region implementation
            return existing.CategoryCodeSystem != newData.CategoryCodeSystem ||
                   existing.CategoryDisplayName != newData.CategoryDisplayName ||
                   existing.ApplicationOrMonographIDOID != newData.ApplicationOrMonographIDOID ||
                   existing.ApprovalDate != newData.ApprovalDate ||
                   existing.TerritoryCode != newData.TerritoryCode;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates the fields of an existing MarketingCategory with new data.
        /// </summary>
        /// <param name="existing">The existing MarketingCategory entity to update.</param>
        /// <param name="newData">The new MarketingCategory data to apply.</param>
        /// <remarks>
        /// Updates all changeable fields while preserving the entity's identity (ID, ProductID, etc.).
        /// </remarks>
        /// <seealso cref="MarketingCategory"/>
        /// <seealso cref="Label"/>
        private void updateMarketingCategoryFields(MarketingCategory existing, MarketingCategory newData)
        {
            #region implementation
            existing.CategoryCodeSystem = newData.CategoryCodeSystem;
            existing.CategoryDisplayName = newData.CategoryDisplayName;
            existing.ApplicationOrMonographIDOID = newData.ApplicationOrMonographIDOID;
            existing.ApprovalDate = newData.ApprovalDate;
            existing.TerritoryCode = newData.TerritoryCode;
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