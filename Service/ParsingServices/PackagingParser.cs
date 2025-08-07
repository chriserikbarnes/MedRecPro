
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
﻿using System.Xml.Linq;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses all packaging-related elements, including the recursive packaging hierarchy,
    /// package identifiers (like NDC Package Codes), and associated product events.
    /// </summary>
    /// <remarks>
    /// This parser is responsible for the entire <c>asContent</c> section of a product. It recursively
    /// processes nested packaging levels and creates the necessary database entities to represent the
    /// full packaging tree. It assumes `SplParseContext.CurrentProduct` is set by the calling parser.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Product"/>
    /// <seealso cref="PackagingLevel"/>
    /// <seealso cref="PackagingHierarchy"/>
    /// <seealso cref="PackageIdentifier"/>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="SplParseContext"/>
    public class PackagingParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "packaging";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a parent XML element to find and process all top-level packaging sections (`asContent`).
        /// </summary>
        /// <param name="element">The XElement (e.g., manufacturedProduct) containing packaging information.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentProduct.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <remarks>
        /// This method serves as the entry point for packaging parsing. It finds all direct `asContent`
        /// child elements and initiates the recursive parsing for each, aggregating the results.
        /// </remarks>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();
            var product = context.CurrentProduct;

            // Validate that a product is available in the context to link entities to.
            if (product?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse packaging because no product context exists.");
                context?.Logger?.LogError("PackagingParser was called without a valid product in the context.");
                return result;
            }

            // Find all top-level <asContent> elements to begin the recursive parsing.
            var asContentEls = element.SplFindElements(sc.E.AsContent);
            if (asContentEls != null && asContentEls.Any())
            {
                reportProgress?.Invoke($"Starting Packaging Level XML Elements {context.FileNameInZip}");
                foreach (var asContentEl in asContentEls)
                {
                    // Use the enhanced method that includes event parsing. This method calls the base
                    // packaging parser internally, ensuring no duplication.
                    result.ProductElementsCreated +=
                        await parseAndSavePackagingLevelsAsync(asContentEl, product, context);
                }
                reportProgress?.Invoke($"Completed Packaging Level XML Elements {context.FileNameInZip}");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all PackagingLevel entities under a given 'asContent'
        /// node (including nested asContent/containerPackagedProduct nodes).
        /// </summary>
        /// <param name="asContentEl">Root [asContent] XElement.</param>
        /// <param name="product">The Product entity associated (if outermost).</param>
        /// <param name="context">The parsing context (repo, logger, docTypeCode, etc).</param>
        /// <param name="parentPackagingLevelId">The ID of the parent (outer) packaging level for creating hierarchy links. Null for the top level.</param>
        /// <param name="sequenceNumber">The sequence of this package within its parent. Null for the top level.</param>
        /// <param name="parentProductInstanceId">For lot/container context (16.2.8), null otherwise.</param>
        /// <returns>The count of PackagingLevel records created (recursively).</returns>
        /// <remarks>
        /// Handles both outermost and nested package levels. Recursively processes nested packaging
        /// structures to create a full packaging tree using PackagingHierarchy links.
        /// Extracts quantity, package codes, and form codes from the XML.
        /// This version also integrates the parsing of package identifiers.
        /// </remarks>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="PackagingHierarchy"/>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSavePackagingLevelsAsync(
            XElement asContentEl,
            Product? product,
            SplParseContext context,
            int? parentPackagingLevelId = null,
            int? sequenceNumber = null,
            int? parentProductInstanceId = null)
        {
            #region implementation
            int count = 0;

            if (context?.ServiceProvider == null || context.Logger == null)
            {
                return count; // Exit early if context is not properly initialized
            }

            var repo = context.GetRepository<PackagingLevel>();
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Extract quantity information from the <asContent> element.
            var quantityEl = asContentEl.SplElement(sc.E.Quantity);
            decimal? quantityValue = null;
            decimal? quantityDenominator = null;
            string? quantityUnit = null;

            if (quantityEl != null)
            {
                var numeratorEl = quantityEl.SplElement(sc.E.Numerator);
                if (numeratorEl != null)
                {
                    quantityValue = numeratorEl.GetAttrDecimal(sc.A.Value);
                    quantityUnit = numeratorEl.GetAttrVal(sc.A.Unit);
                }
                var denominatorEl = quantityEl.SplElement(sc.E.Denominator);
                if (denominatorEl != null)
                {
                    quantityDenominator = denominatorEl.GetAttrDecimal(sc.A.Value);
                }
            }

            // 2. Extract information from the <containerPackagedProduct> element.
            var cppEl = asContentEl.SplElement(sc.E.ContainerPackagedProduct);
            string? packageFormCode = null, packageFormCodeSystem = null, packageFormDisplayName = null;

            if (cppEl != null)
            {
                var formCodeEl = cppEl.SplElement(sc.E.FormCode);
                if (formCodeEl != null)
                {
                    packageFormCode = formCodeEl.GetAttrVal(sc.A.CodeValue);
                    packageFormCodeSystem = formCodeEl.GetAttrVal(sc.A.CodeSystem);
                    packageFormDisplayName = formCodeEl.GetAttrVal(sc.A.DisplayName);
                }
            }

            // 3. Create and save the current packaging level entity.
            var packagingLevel = new PackagingLevel
            {
                ProductID = (parentPackagingLevelId == null && product != null) ? product.ProductID : null,
                ProductInstanceID = parentProductInstanceId,
                QuantityNumerator = quantityValue,
                QuantityNumeratorUnit = quantityUnit,
                QuantityDenominator = quantityDenominator,
                PackageFormCode = packageFormCode,
                PackageFormCodeSystem = packageFormCodeSystem,
                PackageFormDisplayName = packageFormDisplayName,
            };

            await repo.CreateAsync(packagingLevel);
            count++;
            context.Logger.LogInformation($"PackagingLevel created: ID={packagingLevel.PackagingLevelID}, ProductID={packagingLevel.ProductID}, FormCode={packageFormCode}");

            // 4. Parse and save package identifiers (e.g., NDC Package Code) for this level.
            if (cppEl != null)
            {
                count += await parseAndSavePackageIdentifiersAsync(cppEl, asContentEl, packagingLevel, context);

                // Use the orphaned method to get the ID of the level we just created.
                var newPackagingLevelId = await getPackagingLevelIdFromContextAsync(cppEl, context);

                if (newPackagingLevelId.HasValue)
                {
                    // Now, parse any ProductEvent entities associated with this packaging level.
                    // This assumes you have a ProductEventParser like in the "Old" code.
                    var eventCount = await ProductEventParser.BuildProductEventAsync(
                        cppEl,
                        new PackagingLevel { PackagingLevelID = newPackagingLevelId },
                        context);

                    if (eventCount > 0)
                    {
                        context?.Logger?.LogInformation(
                            "Created {EventCount} ProductEvent records for PackagingLevelID {PackagingLevelID}",
                            eventCount, newPackagingLevelId);
                        count += eventCount;
                    }
                }
            }

            // 5. If this is an inner package, create the hierarchy link to its parent.
            if (parentPackagingLevelId.HasValue && packagingLevel.PackagingLevelID.HasValue)
            {
                await saveOrGetPackagingHierarchyAsync(dbContext, parentPackagingLevelId.Value, packagingLevel.PackagingLevelID.Value, sequenceNumber);
                context?.Logger?.LogInformation($"PackagingHierarchy created: OuterID={parentPackagingLevelId}, InnerID={packagingLevel.PackagingLevelID}, Seq={sequenceNumber}");
            }

            // 6. Recursively process nested <asContent> for child packaging levels.
            if (cppEl != null && packagingLevel.PackagingLevelID.HasValue)
            {
                int innerSequence = 1;
                foreach (var nestedAsContent in cppEl.SplElements(sc.E.AsContent))
                {
                    if(context == null || context.ServiceProvider == null)
                    {
                        context?.Logger?.LogWarning("Context is null, skipping nested packaging level parsing.");
                        continue; // Skip if context is not available
                    }

                    count += await parseAndSavePackagingLevelsAsync(
                        nestedAsContent, product, context, packagingLevel.PackagingLevelID, innerSequence, parentProductInstanceId);
                    innerSequence++;
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves PackageIdentifier entities from a [containerPackagedProduct] element for a given packaging level.
        /// It also orchestrates the parsing of any compliance actions associated with this specific package.
        /// This version addresses duplication issues with NDC codes by checking for existing identifiers.
        /// </summary>
        /// <param name="containerPackagedProductEl">The [containerPackagedProduct] XElement to parse.</param>
        /// <param name="asContentEl">Alternative element for indexing files</param>
        /// <param name="packagingLevel">The PackagingLevel entity to link the identifiers to.</param>
        /// <param name="context">The parsing context for repository access and logging.</param>
        /// <returns>The number of PackageIdentifier and related records created.</returns>
        /// <remarks>
        /// Handles the [code] element within a [containerPackagedProduct] node, which represents the
        /// package item code (e.g., NDC Package Code). After creating the identifier, it sets the context
        /// and calls the ComplianceActionParser for any nested [action] elements to ensure they are
        /// correctly linked to this package.
        /// 
        /// FIXES:
        /// - Removed duplicate processing of subjectOf elements
        /// - Added logging for potential NDC/NDCPackage duplication without preventing creation
        /// - Consolidated compliance action parsing
        /// </remarks>
        /// <example>
        /// <code>
        /// var containerEl = xmlDoc.Element("containerPackagedProduct");
        /// var asContentEl = xmlDoc.Element("asContent");
        /// var packagingLevel = new PackagingLevel { PackagingLevelID = 123 };
        /// var count = await parseAndSavePackageIdentifiersAsync(containerEl, asContentEl, packagingLevel, context);
        /// </code>
        /// </example>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="ComplianceActionParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSavePackageIdentifiersAsync(
            XElement containerPackagedProductEl,
            XElement asContentEl,
            PackagingLevel packagingLevel,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var repo = context.GetRepository<PackageIdentifier>();
            var productRepo = context.GetRepository<ProductIdentifier>(); // Added for duplicate check

            // Validate required parameters and context
            if (context?.Logger == null || repo == null || !packagingLevel.PackagingLevelID.HasValue)
            {
                context?.Logger?.LogWarning("Could not parse PackageIdentifier due to invalid context or missing PackagingLevelID.");
                return 0;
            }

            // Extract the package item code from the containerPackagedProduct element
            var codeEl = containerPackagedProductEl.SplElement(sc.E.Code);
            if (codeEl == null)
            {
                return 0; // This is valid in some cases (e.g., compounded drugs).
            }

            string? identifierValue = codeEl.GetAttrVal(sc.A.CodeValue);
            string? identifierSystemOID = codeEl.GetAttrVal(sc.A.CodeSystem);

            // Validate that we have the minimum required identifier information
            if (string.IsNullOrWhiteSpace(identifierValue) || string.IsNullOrWhiteSpace(identifierSystemOID))
            {
                return 0;
            }

            // Infer the type (e.g., 'NDCPackage', 'GS1Package') from the system OID.
            string? identifierType = inferPackageIdentifierType(identifierSystemOID);

            // --- START: DUPLICATION HANDLING ---
            // Check for existing ProductIdentifier to log potential duplication but still create PackageIdentifier
            if (productRepo != null && await checkForDuplicateIdentifier(productRepo, identifierValue, identifierSystemOID, context))
            {
                context.Logger.LogWarning(
                    "Found existing ProductIdentifier with same NDC code. Creating PackageIdentifier anyway: Value={IdentifierValue}, System={IdentifierSystemOID}",
                    identifierValue, identifierSystemOID);
            }
            // --- END: DUPLICATION HANDLING ---

            // Create the new PackageIdentifier entity
            var packageIdentifier = new PackageIdentifier
            {
                PackagingLevelID = packagingLevel.PackagingLevelID,
                IdentifierValue = identifierValue,
                IdentifierSystemOID = identifierSystemOID,
                IdentifierType = identifierType
            };

            await repo.CreateAsync(packageIdentifier);
            count++;

            context.Logger.LogInformation(
                "PackageIdentifier created: ID={PackageIdentifierID}, Value={IdentifierValue}, Type={IdentifierType}",
                packageIdentifier.PackageIdentifierID, identifierValue, identifierType);

            // --- START: CONSOLIDATED COMPLIANCE ACTION PROCESSING ---
            var oldIdentifier = context.CurrentPackageIdentifier;
            context.CurrentPackageIdentifier = packageIdentifier; // Set the context

            try
            {
                // Process all compliance actions associated with this package
                count += await processComplianceActions(containerPackagedProductEl, asContentEl, context);
            }
            finally
            {
                // CRITICAL: Restore the context to prevent this package from "leaking"
                // into the parsing of sibling or parent elements.
                context.CurrentPackageIdentifier = oldIdentifier;
            }
            // --- END: CONSOLIDATED COMPLIANCE ACTION PROCESSING ---

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks for an existing ProductIdentifier with the same value to identify potential duplication.
        /// This method logs potential duplicates but does not prevent PackageIdentifier creation.
        /// </summary>
        /// <param name="productRepo">Repository for ProductIdentifier entities</param>
        /// <param name="identifierValue">The identifier value to check</param>
        /// <param name="identifierSystemOID">The identifier system OID</param>
        /// <param name="context">The parsing context</param>
        /// <returns>True if a duplicate ProductIdentifier exists, false otherwise</returns>
        /// <remarks>
        /// This method helps identify when the same NDC code is being processed by multiple parsers.
        /// It only applies to NDC-related identifiers to maintain backward compatibility.
        /// The duplicate detection is for logging and monitoring purposes only.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<bool> checkForDuplicateIdentifier(
            Repository<ProductIdentifier> productRepo,
            string identifierValue,
            string identifierSystemOID,
            SplParseContext context)
        {
            #region implementation
            try
            {
                // Only check for NDC-related identifiers to avoid unnecessary queries
                if (!isNdcRelatedIdentifier(identifierSystemOID))
                {
                    return false;
                }

                // Check if there's already a ProductIdentifier with the same value and system
                // This helps identify when both ProductIdentifier and PackageIdentifier parsers process the same element
                var existingIdentifiers = await productRepo.ReadAllAsync(null, null);
                var duplicateExists = existingIdentifiers.Any(pi =>
                    pi.IdentifierValue == identifierValue &&
                    pi.IdentifierSystemOID == identifierSystemOID);

                return duplicateExists;
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the process
                context.Logger?.LogWarning(ex, "Error checking for duplicate identifier: {IdentifierValue}", identifierValue);
                return false;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the given OID represents an NDC-related identifier system.
        /// NDC (National Drug Code) identifiers use a specific OID that needs special handling for duplication prevention.
        /// </summary>
        /// <param name="identifierSystemOID">The OID to check</param>
        /// <returns>True if this is an NDC-related identifier</returns>
        /// <remarks>
        /// The NDC system OID is 2.16.840.1.113883.6.69 as defined by FDA standards.
        /// This method enables targeted duplication prevention for NDC codes only.
        /// </remarks>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private bool isNdcRelatedIdentifier(string identifierSystemOID)
        {
            #region implementation
            // NDC system OID is 2.16.840.1.113883.6.69 as per FDA standards
            return identifierSystemOID == "2.16.840.1.113883.6.69";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes compliance actions for both containerPackagedProduct and asContent levels.
        /// This consolidates the previously duplicated logic into a single, maintainable method.
        /// </summary>
        /// <param name="containerPackagedProductEl">The containerPackagedProduct element</param>
        /// <param name="asContentEl">The asContent element</param>
        /// <param name="context">The parsing context</param>
        /// <returns>The number of compliance action records created</returns>
        /// <remarks>
        /// This method processes compliance actions from two different XML hierarchy levels:
        /// 1. containerPackagedProduct level - direct child actions
        /// 2. asContent level - parent-level actions that apply to the package
        /// Both levels are processed to ensure complete compliance action coverage.
        /// </remarks>
        /// <seealso cref="ComplianceActionParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> processComplianceActions(
            XElement containerPackagedProductEl,
            XElement asContentEl,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            var complianceParser = new ComplianceActionParser();

            // Process compliance actions from containerPackagedProduct level
            // Structure: <containerPackagedProduct><subjectOf><action>...</action></subjectOf></containerPackagedProduct>
            var containerSubjectOfElements = containerPackagedProductEl.SplElements(sc.E.SubjectOf);
            count += await processSubjectOfElements(containerSubjectOfElements, complianceParser, context, "containerPackagedProduct");

            // Process compliance actions from asContent level  
            // Structure: <asContent><subjectOf><action>...</action></subjectOf></asContent>
            var asContentSubjectOfElements = asContentEl.SplElements(sc.E.SubjectOf);
            count += await processSubjectOfElements(asContentSubjectOfElements, complianceParser, context, "asContent");

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to process a collection of subjectOf elements for compliance actions.
        /// This method iterates through XML elements and delegates to the ComplianceActionParser.
        /// </summary>
        /// <param name="subjectOfElements">The collection of subjectOf elements to process</param>
        /// <param name="complianceParser">The compliance action parser instance</param>
        /// <param name="context">The parsing context</param>
        /// <param name="sourceLevel">Description of the source level for logging purposes</param>
        /// <returns>The number of compliance action records created</returns>
        /// <remarks>
        /// This method provides centralized processing logic for subjectOf elements containing action elements.
        /// It handles error logging and success tracking consistently across different XML hierarchy levels.
        /// </remarks>
        /// <example>
        /// <code>
        /// var elements = xmlElement.SplElements(sc.E.SubjectOf);
        /// var parser = new ComplianceActionParser();
        /// var count = await processSubjectOfElements(elements, parser, context, "containerLevel");
        /// </code>
        /// </example>
        /// <seealso cref="ComplianceActionParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> processSubjectOfElements(
            IEnumerable<XElement> subjectOfElements,
            ComplianceActionParser complianceParser,
            SplParseContext context,
            string sourceLevel)
        {
            #region implementation
            int count = 0;

            // Iterate through each subjectOf element looking for action elements
            foreach (var subjectEl in subjectOfElements)
            {
                // Check if this subjectOf element contains an action element
                if (subjectEl.SplElement(sc.E.Action) != null)
                {
                    // Parse the compliance action using the dedicated parser
                    var complianceResult = await complianceParser.ParseAsync(subjectEl, context, null);

                    if (complianceResult.Success)
                    {
                        // Accumulate the count of successfully created elements
                        count += complianceResult.ProductElementsCreated;
                        context.Logger?.LogDebug(
                            "Processed compliance action from {SourceLevel} level: {Count} elements created",
                            sourceLevel, complianceResult.ProductElementsCreated);
                    }
                    else
                    {
                        // Log parsing failures with context information
                        context.Logger?.LogError(
                            "Failed to parse compliance action from {SourceLevel} level for PackageIdentifierID {PackageIdentifierID}.",
                            sourceLevel, context.CurrentPackageIdentifier?.PackageIdentifierID);
                    }
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the PackagingLevel ID from the database context for a given container element.
        /// </summary>
        /// <param name="containerEl">The container XML element to find the packaging level for.</param>
        /// <param name="context">The parsing context containing database access services.</param>
        /// <returns>The PackagingLevel ID if found, otherwise null.</returns>
        /// <seealso cref="PackagingLevel"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int?> getPackagingLevelIdFromContextAsync(XElement containerEl, SplParseContext context)
        {
            #region implementation
            if (context?.ServiceProvider == null)
            {
                return null;
            }

            try
            {
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Corrected: Query the PackageIdentifier table instead.
                var packageIdentifierDbSet = dbContext.Set<PackageIdentifier>();

                var codeEl = containerEl.SplElement(sc.E.Code);
                var packageCode = codeEl?.GetAttrVal(sc.A.CodeValue);

                if (!string.IsNullOrWhiteSpace(packageCode))
                {
                    // Find the most recent identifier with this code value.
                    var packageIdentifier = await packageIdentifierDbSet
                        .Where(pi => pi.IdentifierValue == packageCode)
                        .OrderByDescending(pi => pi.PackageIdentifierID)
                        .FirstOrDefaultAsync();

                    // Return the foreign key to the packaging level.
                    return packageIdentifier?.PackagingLevelID;
                }
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error getting PackagingLevel ID from context");
            }

            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PackagingHierarchy or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="outerId">The ID of the outer (containing) packaging level.</param>
        /// <param name="innerId">The ID of the inner (contained) packaging level.</param>
        /// <param name="sequence">The sequence number of the inner package within the outer package.</param>
        /// <returns>The existing or newly created PackagingHierarchy entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate hierarchy records.
        /// </remarks>
        /// <seealso cref="PackagingHierarchy"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="PackagingLevel"/>
        private async Task<PackagingHierarchy> saveOrGetPackagingHierarchyAsync(
            ApplicationDbContext dbContext,
            int outerId,
            int innerId,
            int? sequence)
        {
            #region implementation
            var existing = await dbContext.Set<PackagingHierarchy>().FirstOrDefaultAsync(ph =>
                ph.OuterPackagingLevelID == outerId &&
                ph.InnerPackagingLevelID == innerId &&
                ph.SequenceNumber == sequence);

            if (existing != null)
                return existing;

            var hierarchyLink = new PackagingHierarchy
            {
                OuterPackagingLevelID = outerId,
                InnerPackagingLevelID = innerId,
                SequenceNumber = sequence
            };

            dbContext.Set<PackagingHierarchy>().Add(hierarchyLink);
            await dbContext.SaveChangesAsync();
            return hierarchyLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers the package identifier type (e.g., 'NDCPackage') based on the OID.
        /// </summary>
        /// <param name="oid">The OID string for the code system.</param>
        /// <returns>A formatted package identifier type string, or null if not recognized.</returns>
        /// <remarks>
        /// This helper calls the general <see cref="inferIdentifierType"/> method and then formats
        /// the result to match the package-specific naming convention (e.g., 'ISBT 128' becomes 'ISBTPackage').
        /// </remarks>
        /// <seealso cref="inferIdentifierType"/>
        /// <seealso cref="PackageIdentifier"/>
        private static string? inferPackageIdentifierType(string? oid)
        {
            #region implementation
            var baseType = inferIdentifierType(oid);
            if (string.IsNullOrEmpty(baseType))
            {
                return null;
            }
            // Format to match model examples: "ISBT 128" -> "ISBTPackage"
            return $"{baseType.Replace(" ", "")}Package";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers the identifier type based on the OID (Object Identifier) of the code system.
        /// </summary>
        /// <param name="oid">The OID string representing the code system.</param>
        /// <returns>A human-readable identifier type string, or null if the OID is not recognized.</returns>
        /// <remarks>
        /// Maps standard healthcare OIDs to their corresponding identifier types. This is a general
        /// helper used by other methods.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="Label"/>
        private static string? inferIdentifierType(string? oid)
        {
            #region implementation
            return oid switch
            {
                "2.16.840.1.113883.6.69" => "NDC",
                "1.3.160" => "GS1",
                "2.16.840.1.113883.6.40" => "HIBCC",
                "2.16.840.1.113883.6.18" => "ISBT 128",
                "2.16.840.1.113883.6.301.5" => "UDI",
                _ => null
            };
            #endregion
        }
    }
}

