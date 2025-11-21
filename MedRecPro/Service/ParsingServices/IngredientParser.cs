using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using static MedRecPro.Models.Label;
using MedRecPro.Helpers;
using MedRecPro.Data;
using MedRecPro.Models;
using AngleSharp.Dom;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses an ingredient element, normalizes the IngredientSubstance via a "get-or-create"
    /// pattern, and links it to the current product in the context.
    /// </summary>
    /// <remarks>
    /// This parser handles ingredient elements within SPL documents, extracting substance
    /// information and creating normalized relationships between products and their ingredient
    /// substances. It implements deduplication logic using UNII codes to prevent duplicate
    /// substance records and supports quantity parsing for ingredient measurements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Ingredient"/>
    /// <seealso cref="IngredientSubstance"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class IngredientParser : ISplSectionParser
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing the ingredient element.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="ISplSectionParser"/>
        public string SectionName => "ingredient";

        /**************************************************************/
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        /// <seealso cref="Label"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses an ingredient element from an SPL document, creating normalized ingredient
        /// substance entities and linking them to the current product.
        /// </summary>
        /// <param name="mmEl">The XElement representing the ingredient section to parse.</param>
        /// <param name="context">The current parsing context containing the product to link ingredients to.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements"></param>
        /// <example>
        /// <code>
        /// var parser = new IngredientParser();
        /// var result = await parser.ParseAsync(ingredientElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Ingredients created: {result.IngredientsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a product context exists
        /// 2. Extracts the ingredientSubstance element
        /// 3. Gets or creates a normalized IngredientSubstance entity
        /// 4. Creates an Ingredient link between the product and substance
        /// 5. Parses quantity information (numerator/denominator)
        /// 6. Saves the ingredient relationship to the database
        /// 
        /// The method uses UNII codes for substance normalization to prevent duplicates.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement mmEl,
        SplParseContext context,
        Action<string>? reportProgress, 
        bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }
            try
            {
                if (context.UseBulkOperations)
                {
                    // Bulk operation mode - process all ingredient elements in one go
                    result = await parseIngredientElementsAsync_bulkCalls(mmEl, context, reportProgress);
                }
                else
                {
                    // Non-bulk mode - process each ingredient element individually
                    result = await parseIngredientElementsAsync_singleCalls(mmEl, context, reportProgress);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error parsing ingredient for ProductID {context?.CurrentProduct?.ProductID}: {ex.Message}");
            }

            return result;
            #endregion
        }

        #region Ingredient Processing - Individual Operations (N + 1)

        /**************************************************************/
        /// <summary>
        /// Processes ingredient elements individually using the N+1 pattern. Each ingredient
        /// results in multiple database calls for validation and entity creation.
        /// </summary>
        /// <param name="manufacturedProdEl">The XElement containing ingredient elements to process.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A task that resolves to a SplParseResult indicating success and the count of created entities.</returns>
        /// <remarks>
        /// Performance Pattern: N+1 database calls (6-11 calls per ingredient).
        /// For large ingredient lists, consider using parseIngredientElementsAsync_bulkCalls instead.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="tryValidatePreconditions(XElement, SplParseContext, SplParseResult, out XElement)"/>
        /// <seealso cref="getOrCreateSubstanceEntitiesAsync(XElement, SplParseContext, string, Action{string})"/>
        /// <seealso cref="buildIngredient(XElement, SplParseContext, int, int, int, string)"/>
        /// <seealso cref="saveIngredientAsync(Ingredient, SplParseContext)"/>
        /// <seealso cref="createIngredientSourceProductAsync(XElement, int, SplParseContext)"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseIngredientElementsAsync_singleCalls(
            XElement manufacturedProdEl,
            SplParseContext context,
            Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation

            var result = new SplParseResult();

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Find all possible ingredient elements across different SPL naming conventions
            // SPL documents may use ingredient, activeIngredient, or inactiveIngredient
            var ingredientElements = manufacturedProdEl.SplFindIngredients(excludingFieldsContaining: "substance");

            reportProgress?.Invoke($"Starting Ingredient Level XML Elements {context.FileNameInZip}");

            context.SeqNumber = 0; // Reset sequence number for ingredients

            // Process each ingredient element found
            foreach (var element in ingredientElements)
            {
                reportProgress?.Invoke($"Starting Ingredient XML Element {context.FileNameInZip}");

                // Validate preconditions and find the core element
                if (!tryValidatePreconditions(element, context, result, out var ingredientSubstanceEl))
                {
                    return result; // Validation failed, result is already populated with errors
                }

                try
                {
                    if (ingredientSubstanceEl == null)
                    {
                        throw new InvalidOperationException("Ingredient substance element is null after validation.");
                    }

                    // Get or create the required substance entities from the database
                    var (substance, specifiedSubstanceId) = await getOrCreateSubstanceEntitiesAsync(
                        ingredientSubstanceEl,
                        context,
                        element.GetAttrVal(sc.A.ClassCode),
                        reportProgress);

                    // Get the substance element name
                    string ingredientSubstanceEnclosingElement = ingredientSubstanceEl.Name.LocalName;

                    if (substance == null || substance.IngredientSubstanceID == null)
                    {
                        throw new InvalidOperationException("Failed to get or create an IngredientSubstance.");
                    }

                    // Build the Ingredient object from the XML data (no DB calls here)
                    Ingredient ingredient = buildIngredient(
                        element,
                        context,
                        substance.IngredientSubstanceID.Value,
                        specifiedSubstanceId,
                        context.SeqNumber,
                        ingredientSubstanceEnclosingElement);

                    // Persist the new Ingredient to the database
                    await saveIngredientAsync(ingredient, context);

                    // If the ingredient has a source product, create the link now that we have an IngredientID
                    if (ingredient.IngredientID.HasValue)
                    {
                        await createIngredientSourceProductAsync(element, ingredient.IngredientID.Value, context);
                    }

                    result.IngredientsCreated++;

                    reportProgress?.Invoke($"Completed Ingredient XML Element {context.FileNameInZip}");

                    context.SeqNumber++; // Increment sequence for next ingredient
                }
                catch (Exception ex)
                {
                    // Centralized error handling for the entire operation
                    result.Success = false;
                    result.Errors.Add($"Error parsing ingredient for ProductID {context?.CurrentProduct?.ProductID}: {ex.Message}");

                    context?.Logger?.LogError(ex,
                        "Error processing <ingredient> element for ProductID {ProductID}.",
                        context.CurrentProduct?.ProductID ?? 0);

                    throw; // Rethrow to allow higher-level handling if needed
                }
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the necessary context and XML elements exist before parsing.
        /// </summary>
        /// <param name="element">The XElement containing the ingredient data.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="result">The result object to populate with errors if validation fails.</param>
        /// <param name="ingredientSubstanceEl">The found ingredient substance element, or null if not found.</param>
        /// <returns>True if validation succeeds, otherwise false.</returns>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private bool tryValidatePreconditions(XElement element, SplParseContext context, SplParseResult result, out XElement? ingredientSubstanceEl)
        {
            #region implementation
            ingredientSubstanceEl = null;

            // Validate that we have a valid product context
            if (context.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse ingredient because no product context exists.");
                return false;
            }

            // Attempt to find the ingredient substance element using various possible names
            ingredientSubstanceEl = element.GetSplElement(sc.E.IngredientSubstance)
                ?? element.GetSplElement(sc.E.InactiveIngredientSubstance)
                ?? element.GetSplElement(sc.E.ActiveIngredientSubstance)
                ?? element.SplFindElements("substance").FirstOrDefault();

            // Validate that we found the required element
            if (ingredientSubstanceEl == null)
            {
                result.Success = false;
                result.Errors.Add($"Could not find <ingredientSubstance> for ProductID {context.CurrentProduct.ProductID}, element {element.Name.LocalName}.");
                return false;
            }

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles the retrieval or creation of substance-related entities from the database.
        /// </summary>
        /// <param name="ingredientSubstanceEl">The XML element containing ingredient substance data.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <param name="ingredientClassCode">The class code for the ingredient e.g. classCode="IACT"</param>
        /// <param name="reportProgress">Report operational progress</param>
        /// <returns>A tuple containing the IngredientSubstance and the SpecifiedSubstanceID.</returns>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SpecifiedSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<(IngredientSubstance substance, int specifiedSubstanceId)> getOrCreateSubstanceEntitiesAsync(XElement ingredientSubstanceEl,
            SplParseContext context,
            string? ingredientClassCode,
            Action<string>? reportProgress)
        {
            #region implementation
            // Get or create the main ingredient substance entity
            var substance = await getOrCreateIngredientSubstanceAsync(ingredientSubstanceEl, context, ingredientClassCode, reportProgress);

            if (substance?.IngredientSubstanceID == null)
            {
                throw new InvalidOperationException("Failed to get or create an IngredientSubstance.");
            }

            // Initialize specified substance ID to default value
            int specifiedSubstanceId = 0;

            // Attempt to get or create a specified substance entity if data exists
            var specifiedSubstance = await getOrCreateSpecifiedSubstanceAsync(ingredientSubstanceEl, context);

            if (specifiedSubstance?.SpecifiedSubstanceID > 0)
            {
                specifiedSubstanceId = specifiedSubstance.SpecifiedSubstanceID.Value;
            }

            return (substance, specifiedSubstanceId);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing ReferenceSubstance by its UNII and parent IngredientSubstanceID.
        /// If not found, creates a new one. This is used when an ingredient's strength is
        /// based on a reference ingredient (classCode="ACTIR").
        /// </summary>
        /// <param name="substanceEl">The XElement representing the ingredientSubstance to process.</param>
        /// <param name="ingredientSubstanceId">The ID of the parent IngredientSubstance.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <returns>A ReferenceSubstance entity, either existing or newly created, or null if no reference substance data is found.</returns>
        /// <remarks>
        /// This method implements the logic for handling reference ingredients as specified in SPL section 3.2.5.
        /// The process:
        /// 1. Navigates to the [asEquivalentEntity] -> [definingSubstance] element.
        /// 2. Extracts the UNII and name of the reference substance.
        /// 3. Checks if a link for this reference substance already exists for the parent ingredient.
        /// 4. If found, returns the existing entity; otherwise, creates a new one.
        /// </remarks>
        /// <seealso cref="ReferenceSubstance"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        private async Task<ReferenceSubstance?> getOrCreateReferenceSubstanceAsync(XElement substanceEl, int ingredientSubstanceId, SplParseContext context)
        {
            #region implementation
            // Navigate to the element containing the reference substance details
            // Path: ingredientSubstance -> asEquivalentSubstance -> definingSubstance
            var definingSubstanceEl = substanceEl.SplElement(sc.E.AsEquivalentSubstance, sc.E.DefiningSubstance);
            if (definingSubstanceEl == null
                || context?.ServiceProvider == null
                || context?.Logger == null)
            {
                // No reference substance data present for this ingredient
                return null;
            }

            // Extract the reference substance's UNII and name from the XML structure
            var refUnii = definingSubstanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var refName = definingSubstanceEl.GetSplElementVal(sc.E.Name);

            // Validate that we have the required data before proceeding with database operations
            if (string.IsNullOrWhiteSpace(refUnii) || string.IsNullOrWhiteSpace(refName))
            {
                // Log warning for missing critical data that prevents reference substance creation
                context.Logger.LogWarning("Skipping ReferenceSubstance for IngredientSubstanceID {ID} due to missing UNII or Name.", ingredientSubstanceId);
                return null;
            }

            // Get the DbContext to check for existing records and perform database operations
            var dbContext = context.GetDbContext();
            var referenceSubstanceDbSet = dbContext.Set<ReferenceSubstance>();

            // Check if this exact reference substance link already exists to prevent duplicates
            // Deduplication based on both IngredientSubstanceID and RefSubstanceUNII
            var existingReference = await referenceSubstanceDbSet.FirstOrDefaultAsync(rs =>
                rs.IngredientSubstanceID == ingredientSubstanceId && rs.RefSubstanceUNII == refUnii);

            if (existingReference != null)
            {
                // Return existing reference substance to avoid creating duplicates
                context.Logger.LogDebug("ReferenceSubstance link for UNII {UNII} already exists for IngredientSubstanceID {ID}.", refUnii, ingredientSubstanceId);
                return existingReference;
            }

            // If not found, create, save, and return the new entity
            context.Logger.LogInformation("Creating new ReferenceSubstance '{Name}' for IngredientSubstanceID {ID}", refName, ingredientSubstanceId);

            // Build new ReferenceSubstance entity with extracted XML data
            var newReferenceSubstance = new ReferenceSubstance
            {
                IngredientSubstanceID = ingredientSubstanceId,
                RefSubstanceUNII = refUnii,
                RefSubstanceName = refName
            };

            // Add to DbSet and save immediately to generate primary key
            referenceSubstanceDbSet.Add(newReferenceSubstance);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted reference substance
            return newReferenceSubstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates an IngredientSourceProduct record if the ingredient XML
        /// specifies a source product NDC, which is common in compounded drug labels.
        /// </summary>
        /// <param name="ingredientEl">The XML element for the [ingredient] which may contain the source product info.</param>
        /// <param name="ingredientId">The ID of the parent Ingredient record to link to.</param>
        /// <param name="context">The current parsing context for database access and logging.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method implements the logic for handling ingredient source products as specified in SPL section 3.1.4.
        /// The process:
        /// 1. Navigates to the [subjectOf] -> [substanceSpecification] -> [code] element.
        /// 2. Extracts the source product NDC and its code system.
        /// 3. Validates that the NDC exists.
        /// 4. Checks for and prevents the creation of duplicate records for the same ingredient.
        /// 5. Creates and saves a new IngredientSourceProduct entity to the database.
        /// </remarks>
        /// <seealso cref="IngredientSourceProduct"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        private async Task createIngredientSourceProductAsync(XElement ingredientEl, int ingredientId, SplParseContext context)
        {
            #region implementation
            // Navigate to the element containing the source product NDC
            // Path: ingredient -> subjectOf -> substanceSpecification -> code
            var codeEl = ingredientEl.SplElement(sc.E.SubjectOf, sc.E.SubstanceSpecification, sc.E.Code);
            if (codeEl == null
                || context?.ServiceProvider == null
                || context?.Logger == null)
            {
                // No source product data present for this ingredient
                return;
            }

            // Extract the source product's NDC and code system from XML attributes
            var sourceNdc = codeEl.GetAttrVal(sc.A.CodeValue);
            var sourceNdcSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

            // Validate that we have the required NDC before proceeding with database operations
            if (string.IsNullOrWhiteSpace(sourceNdc))
            {
                // Log warning for missing NDC which is required for source product tracking
                context.Logger.LogWarning("Skipping IngredientSourceProduct for IngredientID {ID} due to missing source NDC.", ingredientId);
                return;
            }

            // Get the DbContext to check for existing records and perform database operations
            var dbContext = context.GetDbContext();
            var sourceProductDbSet = dbContext.Set<IngredientSourceProduct>();

            // Check if this exact source product link already exists to prevent duplicates
            // Deduplication based on both IngredientID and SourceProductNDC
            var existingSourceProduct = await sourceProductDbSet.FirstOrDefaultAsync(isp =>
                isp.IngredientID == ingredientId && isp.SourceProductNDC == sourceNdc);

            if (existingSourceProduct != null)
            {
                // Return early if source product link already exists to avoid creating duplicates
                context.Logger.LogDebug("IngredientSourceProduct link for NDC {NDC} already exists for IngredientID {ID}.", sourceNdc, ingredientId);
                return;
            }

            // If not found, create and save the new entity
            context.Logger.LogInformation("Creating new IngredientSourceProduct with NDC {NDC} for IngredientID {ID}", sourceNdc, ingredientId);

            // Build new IngredientSourceProduct entity with extracted XML data
            var newSourceProduct = new IngredientSourceProduct
            {
                IngredientID = ingredientId,
                SourceProductNDC = sourceNdc,
                SourceProductNDCSysten = sourceNdcSystem
            };

            // Add to DbSet and save immediately to persist the source product relationship
            sourceProductDbSet.Add(newSourceProduct);
            await dbContext.SaveChangesAsync();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and populates an Ingredient object from the XML element. This method is pure and has no side effects.
        /// </summary>
        /// <param name="element">The root ingredient XML element.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="ingredientSubstanceId">The ID of the associated ingredient substance.</param>
        /// <param name="specifiedSubstanceId">The ID of the associated specified substance.</param>
        /// <param name="sequenceNumber">Sequence number for ordering ingredients.</param>
        /// <param name="ingredientSubElementName">Holds the name of the ingredient substance name</param>
        /// <returns>A new Ingredient object.</returns>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private Ingredient buildIngredient(XElement element,
            SplParseContext context,
            int ingredientSubstanceId,
            int specifiedSubstanceId,
            int sequenceNumber,
            string? ingredientSubElementName)
        {
            #region implementation

            // For those products not marking an ingredient with IACT for inactive ingredients
            string? classCode = !string.IsNullOrEmpty(ingredientSubElementName)
                    && ingredientSubElementName.Contains("inactive", StringComparison.OrdinalIgnoreCase)
                    ? "IACT"
                    : element.GetAttrVal(sc.A.ClassCode);

            // Create the base ingredient object with core properties
            var ingredient = new Ingredient
            {
                ProductID = context?.CurrentProduct?.ProductID,
                IngredientSubstanceID = ingredientSubstanceId,
                SpecifiedSubstanceID = specifiedSubstanceId,
                SequenceNumber = sequenceNumber,
                OriginatingElement = element.Name.LocalName,
                ClassCode = classCode,
                IsConfidential = element.GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };

            // Parse quantity information if present
            var quantityEl = element.GetSplElement(sc.E.Quantity);
            if (quantityEl != null)
            {
                parseQuantity(quantityEl, ingredient);
            }

            return ingredient;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the 'quantity' element and populates the numerator/denominator fields of the ingredient.
        /// </summary>
        /// <param name="quantityEl">The XML element containing quantity information.</param>
        /// <param name="ingredient">The ingredient object to populate with quantity data.</param>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="Label"/>
        private void parseQuantity(XElement quantityEl, Ingredient ingredient)
        {
            #region implementation
            // Parse Numerator portion of the quantity ratio
            var numeratorEl = quantityEl.GetSplElement(sc.E.Numerator);
            if (numeratorEl != null)
            {
                var (value, unit, code, system, displayName) = parseRatioPart(numeratorEl, sc.E.NumeratorIngredientTranslation);
                ingredient.QuantityNumerator = value;
                ingredient.QuantityNumeratorUnit = unit;
                ingredient.NumeratorTranslationCode = code;
                ingredient.NumeratorCodeSystem = system;
                ingredient.NumeratorDisplayName = displayName;
            }

            // Parse Denominator portion of the quantity ratio
            var denominatorEl = quantityEl.GetSplElement(sc.E.Denominator);
            if (denominatorEl != null)
            {
                var (value, unit, code, system, displayName) = parseRatioPart(denominatorEl, sc.E.DenominatorIngredientTranslation);
                ingredient.QuantityDenominator = value;
                ingredient.QuantityDenominatorUnit = unit;
                ingredient.DenominatorTranslationCode = code;
                ingredient.DenominatorCodeSystem = system;
                ingredient.DenominatorDisplayName = displayName;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a part of a ratio (numerator or denominator) from its XML element.
        /// </summary>
        /// <param name="ratioPartEl">The XML element representing either a numerator or denominator.</param>
        /// <param name="translationElementName">The name of the translation element to extract code information from.</param>
        /// <returns>A tuple containing the parsed values.</returns>
        /// <remarks>
        /// Uses InvariantCulture to ensure XML decimal values (which always use period as separator) 
        /// are parsed correctly regardless of server culture settings.
        /// </remarks>
        /// <seealso cref="Label"/>
        private (decimal? Value, string Unit, string Code, string System, string DisplayName) parseRatioPart(XElement ratioPartEl, string translationElementName)
        {
            #region implementation

            // Attempt to parse the numeric value from the element using InvariantCulture
            // XML always uses period as decimal separator, regardless of server locale
            decimal? parsedValue = null;

            if (decimal.TryParse(ratioPartEl.GetAttrVal(sc.A.Value),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var val))
            {
                parsedValue = val;
            }

            // Extract unit and translation information, ensuring non-null values
            var unit = ratioPartEl.GetAttrVal(sc.A.Unit) ?? string.Empty; // Ensure non-null value
            var code = ratioPartEl.GetSplElementAttrVal(translationElementName, sc.A.CodeValue) ?? string.Empty; // Ensure non-null value
            var system = ratioPartEl.GetSplElementAttrVal(translationElementName, sc.A.CodeSystem) ?? string.Empty; // Ensure non-null value
            var displayName = ratioPartEl.GetSplElementAttrVal(translationElementName, sc.A.DisplayName) ?? string.Empty; // Ensure non-null value

            return (parsedValue, unit, code, system, displayName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves the ingredient to the database and validates the result.
        /// </summary>
        /// <param name="ingredient">The ingredient object to save to the database.</param>
        /// <param name="context">The current parsing context containing repository access.</param>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task saveIngredientAsync(Ingredient ingredient,
            SplParseContext context)
        {
            #region implementation
            // Get the ingredient repository from the context and save the entity
            var ingredientRepo = context.GetRepository<Ingredient>();
            await ingredientRepo.CreateAsync(ingredient);

            // Validate that the database assigned an ID to the new ingredient
            if (!ingredient.IngredientID.HasValue)
            {
                throw new InvalidOperationException("IngredientID was not populated by the database after creation.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new SpecifiedSubstance entity asynchronously from an XML element.
        /// </summary>
        /// <param name="substanceEl">The XML element containing substance information from the SPL document.</param>
        /// <param name="context">The parsing context providing access to services and shared state.</param>
        /// <returns>A task representing the asynchronous operation that returns the created SpecifiedSubstance, or null if creation fails.</returns>
        /// <remarks>
        /// This method extracts substance code, code system, and display name from the XML element,
        /// then creates and persists a new SpecifiedSubstance entity to the database.
        /// The entity is saved immediately to ensure the ID is populated for subsequent operations.
        /// </remarks>
        /// <example>
        /// <code>
        /// var substanceElement = splDocument.Descendants("substance").First();
        /// var newSubstance = await createSpecifiedSubstanceAsync(substanceElement, parseContext, 123);
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="SpecifiedSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<SpecifiedSubstance?> getOrCreateSpecifiedSubstanceAsync(XElement substanceEl,
            SplParseContext context)
        {
            #region implementation

            if (substanceEl == null
                || context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return null;
            }

            #region extract substance data from xml
            // Extract substance identification codes from the XML element
            var substanceCode = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var substanceCodeSystem = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
            var codeName = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName);
            #endregion

            #region database operations
            // Get database context and specified substance repository
            var dbContext = context.GetDbContext();
            var substanceDbSet = dbContext.Set<SpecifiedSubstance>();

            // Search for existing substance with matching code and code system
            var existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s =>
                s.SubstanceCode != null
                && s.SubstanceCode == substanceCode
                && substanceCodeSystem != null
                && !string.IsNullOrWhiteSpace(substanceCodeSystem)
                && !string.IsNullOrWhiteSpace(s.SubstanceCodeSystem)
                && substanceCodeSystem.ToLower() == s.SubstanceCodeSystem.ToLower());

            // Return existing substance if found
            if (existingSubstance != null)
            {
                context.Logger.LogDebug("Found existing SpecifiedSubstance '{Name}' with UNII {code}", codeName, substanceCode);
                return existingSubstance;
            }

            // Log creation of new substance
            context.Logger.LogInformation("Creating new SpecifiedSubstance '{code}' with code system {system}", substanceCode, substanceCodeSystem);

            // Create new specified substance entity
            var newSpecifiedSubstance = new SpecifiedSubstance
            {
                SubstanceCode = substanceCode,
                SubstanceCodeSystem = substanceCodeSystem,
                SubstanceCodeSystemName = codeName
            };

            // Save the new substance if it has valid identifying information
            if (!string.IsNullOrWhiteSpace(substanceCode) && !string.IsNullOrWhiteSpace(substanceCodeSystem))
            {
                substanceDbSet.Add(newSpecifiedSubstance);
                await dbContext.SaveChangesAsync();
            }
            #endregion

            return newSpecifiedSubstance;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates the process of finding or creating an IngredientSubstance entity.
        /// This method delegates to specialized helper methods to maintain single responsibility.
        /// </summary>
        /// <param name="substanceEl">The XElement representing the ingredientSubstance to process.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <param name="ingredientClassCode">The class code for the ingredient e.g. classCode="IACT"</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param> 
        /// <returns>An IngredientSubstance entity, either existing or newly created, or null if creation fails.</returns>
        /// <example>
        /// <code>
        /// var substance = await getOrCreateIngredientSubstanceAsync(substanceElement, context, "ACTIB");
        /// if (substance != null)
        /// {
        ///     Console.WriteLine($"Substance: {substance.SubstanceName} (UNII: {substance.UNII})");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This orchestrator method coordinates the substance normalization workflow by:
        /// 1. Validating input parameters
        /// 2. Extracting substance identifiers from XML
        /// 3. Searching for existing substances
        /// 4. Creating new substances if needed
        /// 5. Processing active moieties and reference substances
        /// 
        /// The method delegates each responsibility to focused helper methods,
        /// making the code more maintainable and testable.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="extractSubstanceIdentifiers(XElement)"/>
        /// <seealso cref="findExistingSubstanceAsync(string, string, ApplicationDbContext, ILogger)"/>
        /// <seealso cref="createNonNormalizedSubstanceAsync(string, string, SplParseContext, Action{string})"/>
        /// <seealso cref="createNormalizedSubstanceAsync(string, string, string, ApplicationDbContext, SplParseContext, Action{string})"/>
        /// <seealso cref="processSubstancePostCreationAsync(XElement, IngredientSubstance, string, SplParseContext, bool)"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientSubstance?> getOrCreateIngredientSubstanceAsync(
            XElement substanceEl,
            SplParseContext context,
            string? ingredientClassCode,
            Action<string>? reportProgress)
        {
            #region implementation

            // Validate required parameters
            if (!validateParameters(substanceEl, context))
            {
                return null;
            }

            // Extract identifiers from XML element
            var (unii, substanceName, fieldName) = extractSubstanceIdentifiers(substanceEl);

            // Get database context
            var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            // Search for existing substance
            var existingSubstance = await findExistingSubstanceAsync(
                unii,
                substanceName,
                dbContext,
                context.Logger!);

            // Determine which substance to use
            IngredientSubstance? substanceToUse;
            bool isNewSubstance;

            if (existingSubstance != null)
            {
                // Use existing substance
                reportProgress?.Invoke($"Found Existing Ingredient {existingSubstance.SubstanceName} for file {context.FileNameInZip}");
                substanceToUse = existingSubstance;
                isNewSubstance = false;
            }
            else if (string.IsNullOrWhiteSpace(unii))
            {
                // Create non-normalized substance (missing UNII)
                substanceToUse = await createNonNormalizedSubstanceAsync(
                    substanceName,
                    fieldName,
                    context,
                    reportProgress);
                isNewSubstance = true;
            }
            else
            {
                // Create new normalized substance with UNII
                substanceToUse = await createNormalizedSubstanceAsync(
                    unii,
                    substanceName,
                    fieldName,
                    dbContext,
                    context,
                    reportProgress);
                isNewSubstance = true;
            }

            // Process active moieties and reference substances for all substances
            await processSubstancePostCreationAsync(
                substanceEl,
                substanceToUse,
                ingredientClassCode,
                context,
                isNewSubstance);

            return substanceToUse;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the required parameters for substance processing.
        /// </summary>
        /// <param name="substanceEl">The XElement to validate.</param>
        /// <param name="context">The parsing context to validate.</param>
        /// <returns>True if all required parameters are valid; otherwise, false.</returns>
        /// <remarks>
        /// This method ensures that all critical dependencies are present before
        /// attempting to process substance data. It checks for null values on
        /// the element, context, logger, and service provider.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private bool validateParameters(XElement? substanceEl, SplParseContext? context)
        {
            #region implementation

            return substanceEl != null
                && context != null
                && context.Logger != null
                && context.ServiceProvider != null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts substance identifiers (UNII, name, field) from an XML element.
        /// </summary>
        /// <param name="substanceEl">The XElement containing substance data.</param>
        /// <returns>A tuple containing the UNII code, substance name, and originating field name.</returns>
        /// <example>
        /// <code>
        /// var (unii, name, field) = extractSubstanceIdentifiers(substanceElement);
        /// Console.WriteLine($"UNII: {unii}, Name: {name}, Field: {field}");
        /// </code>
        /// </example>
        /// <remarks>
        /// This method extracts three key identifiers:
        /// - UNII: The unique ingredient identifier code from the code element
        /// - Substance Name: The text value from the name element
        /// - Field Name: The local name of the XML element (for tracking purposes)
        /// 
        /// These identifiers are used for substance normalization and database lookups.
        /// </remarks>
        /// <seealso cref="XElementExtensions.GetSplElementAttrVal(XElement, string, string)"/>
        /// <seealso cref="XElementExtensions.GetSplElement(XElement, string)"/>
        /// <seealso cref="SplConstants.E"/>
        /// <seealso cref="SplConstants.A"/>
        /// <seealso cref="Label"/>
        private (string unii, string substanceName, string fieldName) extractSubstanceIdentifiers(XElement substanceEl)
        {
            #region implementation

            // Extract UNII code from the code element's codeValue attribute
            var unii = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue) ?? string.Empty;

            // Extract substance name from the name element
            var nameElement = substanceEl.GetSplElement(sc.E.Name);
            var substanceName = nameElement?.Value ?? string.Empty;

            // Get enclosing field name for tracking purposes
            var fieldName = substanceEl.Name.LocalName;

            return (unii, substanceName, fieldName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches the database for an existing IngredientSubstance by UNII or name.
        /// </summary>
        /// <param name="unii">The UNII code to search for.</param>
        /// <param name="substanceName">The substance name to search for if UNII lookup fails.</param>
        /// <param name="dbContext">The database context for querying.</param>
        /// <param name="logger">The logger for recording search operations.</param>
        /// <returns>An existing IngredientSubstance if found; otherwise, null.</returns>
        /// <example>
        /// <code>
        /// var existing = await findExistingSubstanceAsync("451W47IQ8X", "Sodium Chloride", dbContext, logger);
        /// if (existing != null)
        /// {
        ///     Console.WriteLine($"Found existing substance: {existing.SubstanceName}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements a two-tier search strategy:
        /// 1. Primary: Search by UNII code (preferred for normalization)
        /// 2. Fallback: Search by substance name (case-insensitive)
        /// 
        /// The UNII search is prioritized because it provides unique identification
        /// across different naming conventions and languages.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientSubstance?> findExistingSubstanceAsync(
            string unii,
            string substanceName,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation

            // Get the DbSet for IngredientSubstance
            var substanceDbSet = dbContext.Set<IngredientSubstance>();

            // Primary search: by UNII code
            var existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s =>
                s != null
                && s.UNII != null
                && s.UNII == unii);

            // Log the primary search result
            if (existingSubstance != null)
            {
                logger.LogDebug("Found existing IngredientSubstance by UNII: {UNII}", unii);
                return existingSubstance;
            }

            // Fallback search: by name (case-insensitive) if UNII search fails
            if (!string.IsNullOrWhiteSpace(substanceName))
            {
                var lowerName = substanceName.ToLower();
                existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s =>
                    s != null
                    && !string.IsNullOrEmpty(s.SubstanceName)
                    && s.SubstanceName.ToLower() == lowerName);

                if (existingSubstance != null)
                {
                    logger.LogDebug("Found existing IngredientSubstance by name: {Name}", substanceName);
                }
            }

            return existingSubstance;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a non-normalized IngredientSubstance when UNII is missing.
        /// </summary>
        /// <param name="substanceName">The name of the substance.</param>
        /// <param name="fieldName">The originating XML element name.</param>
        /// <param name="context">The parsing context containing repository access.</param>
        /// <param name="reportProgress">Optional action to report progress during creation.</param>
        /// <returns>A newly created non-normalized IngredientSubstance.</returns>
        /// <example>
        /// <code>
        /// var substance = await createNonNormalizedSubstanceAsync("Sodium Chloride", "ingredientSubstance", context);
        /// Console.WriteLine($"Created non-normalized substance: {substance.SubstanceName}");
        /// </code>
        /// </example>
        /// <remarks>
        /// This method handles the special case where an ingredient substance lacks a UNII code.
        /// Without UNII normalization, duplicate substances may be created for the same chemical
        /// entity. This is logged as a warning to alert operators to potential data quality issues.
        /// 
        /// Non-normalized substances are created when:
        /// - The UNII field is empty or null
        /// - The substance cannot be uniquely identified
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseContext.GetRepository{T}"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientSubstance> createNonNormalizedSubstanceAsync(
            string substanceName,
            string fieldName,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            // Log warning about missing UNII
            context.Logger!.LogWarning(
                "Ingredient substance '{Name}' is missing a UNII. It will not be normalized and a new record may be created.",
                substanceName);

            // Create new substance without UNII
            var nonNormalizedSubstance = new IngredientSubstance
            {
                SubstanceName = substanceName?.ToLower(),
                OriginatingElement = fieldName
            };

            // Save to database using repository
            var repo = context.GetRepository<IngredientSubstance>();
            await repo.CreateAsync(nonNormalizedSubstance);

            // Report progress
            reportProgress?.Invoke($"Added Ingredient {nonNormalizedSubstance.SubstanceName} for file {context.FileNameInZip}");

            return nonNormalizedSubstance;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new normalized IngredientSubstance with a UNII code.
        /// </summary>
        /// <param name="unii">The UNII code for the substance.</param>
        /// <param name="substanceName">The name of the substance.</param>
        /// <param name="fieldName">The originating XML element name.</param>
        /// <param name="dbContext">The database context for saving.</param>
        /// <param name="context">The parsing context containing repository access.</param>
        /// <param name="reportProgress">Optional action to report progress during creation.</param>
        /// <returns>A newly created normalized IngredientSubstance with populated ID.</returns>
        /// <example>
        /// <code>
        /// var substance = await createNormalizedSubstanceAsync(
        ///     "451W47IQ8X", 
        ///     "Sodium Chloride", 
        ///     "ingredientSubstance", 
        ///     dbContext, 
        ///     context,
        ///     reportProgress);
        /// Console.WriteLine($"Created normalized substance with ID: {substance.IngredientSubstanceID}");
        /// </code>
        /// </example>
        /// <remarks>
        /// This method creates a new substance record with proper UNII-based normalization.
        /// The substance is immediately saved to the database to populate the auto-generated
        /// IngredientSubstanceID, which is required for subsequent processing of active
        /// moieties and reference substances.
        /// 
        /// Normalized substances prevent duplicate records and enable proper substance
        /// relationship tracking across multiple products.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientSubstance> createNormalizedSubstanceAsync(
            string unii,
            string substanceName,
            string fieldName,
            ApplicationDbContext dbContext,
            SplParseContext context,
            Action<string>? reportProgress
         )
        {
            #region implementation

            // Log creation of new normalized substance
            context?.Logger?.LogInformation("Creating new IngredientSubstance '{Name}' with UNII {UNII}", substanceName, unii);

            // Create new substance with UNII normalization
            var newSubstance = new IngredientSubstance
            {
                UNII = unii,
                SubstanceName = substanceName?.ToLower(),
                OriginatingElement = fieldName
            };

            // Add to DbSet and save immediately to populate the ID
            var substanceDbSet = dbContext.Set<IngredientSubstance>();
            substanceDbSet.Add(newSubstance);
            await dbContext.SaveChangesAsync();

            // Report progress
            reportProgress?.Invoke($"Added Ingredient {newSubstance.SubstanceName} for file {context?.FileNameInZip ?? "empty filename"}");

            return newSubstance;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes active moieties and reference substances for an IngredientSubstance.
        /// This method always executes regardless of whether the substance is new or existing.
        /// </summary>
        /// <param name="substanceEl">The XElement containing substance data with nested active moieties.</param>
        /// <param name="substance">The IngredientSubstance entity to process (new or existing).</param>
        /// <param name="ingredientClassCode">The class code indicating if this is a reference ingredient.</param>
        /// <param name="context">The parsing context for database operations.</param>
        /// <param name="isNewSubstance">Flag indicating whether the substance was newly created.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>
        /// <code>
        /// await processSubstancePostCreationAsync(
        ///     substanceElement, 
        ///     substance, 
        ///     "ACTIB", 
        ///     context, 
        ///     isNew: true);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method handles post-creation processing that must occur for all substances:
        /// 
        /// 1. Active Moiety Processing: Parses and creates linked active moiety records
        ///    from the XML structure. Multiple active moieties are supported.
        /// 
        /// 2. Reference Substance Linking: If the ingredient has a class code of "ACTIR"
        ///    (Active Ingredient Reference Basis), creates the reference substance relationship.
        /// 
        /// This processing occurs for both newly created and existing substances to ensure
        /// that all active moiety relationships are properly established, preventing data
        /// loss when substances are reused across multiple products.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="getOrCreateActiveMoietyAsync(XElement, int, SplParseContext)"/>
        /// <seealso cref="getOrCreateReferenceSubstanceAsync(XElement, int, SplParseContext)"/>
        /// <seealso cref="Constant.ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE"/>
        /// <seealso cref="Label"/>
        private async Task processSubstancePostCreationAsync(
            XElement substanceEl,
            IngredientSubstance? substance,
            string? ingredientClassCode,
            SplParseContext context,
            bool isNewSubstance)
        {
            #region implementation

            // Validate substance has a valid ID before processing
            if (substance == null || !substance.IngredientSubstanceID.HasValue || substance.IngredientSubstanceID <= 0)
            {
                return;
            }

            // Process active moieties for this substance (new or existing)
            await getOrCreateActiveMoietyAsync(substanceEl, substance.IngredientSubstanceID.Value, context);

            // Create reference substance link if this is a reference ingredient
            if (!string.IsNullOrWhiteSpace(ingredientClassCode)
                && ingredientClassCode.Equals(c.ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE, StringComparison.OrdinalIgnoreCase))
            {
                await getOrCreateReferenceSubstanceAsync(substanceEl, substance.IngredientSubstanceID.Value, context);
            }

            // Log completion of post-processing
            var substanceType = isNewSubstance ? "new" : "existing";
            context.Logger!.LogDebug(
                "Completed post-creation processing (active moieties and references) for {Type} substance '{Name}'",
                substanceType,
                substance.SubstanceName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates ActiveMoiety records linked to a parent IngredientSubstance.
        /// Handles multiple active moieties per ingredient substance as per SPL specification.
        /// </summary>
        /// <param name="substanceEl">The XML element containing the substance data with potential active moiety information.</param>
        /// <param name="ingredientSubstanceId">The ID of the parent ingredient substance to link the active moieties to.</param>
        /// <param name="context">The current parsing context containing database access and logging services.</param>
        /// <remarks>
        /// This method processes all activeMoiety elements within an ingredientSubstance.
        /// Each activeMoiety has a nested structure where the inner activeMoiety element contains
        /// the actual code and name data. The method validates data completeness and checks for
        /// duplicate entries before creating new records.
        /// </remarks>
        /// <example>
        /// For Sodium Chloride with two active moieties (Chloride Ion and Sodium Cation),
        /// this method will create two separate ActiveMoiety records linked to the same
        /// IngredientSubstance.
        /// </example>
        /// <seealso cref="ActiveMoiety"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task getOrCreateActiveMoietyAsync(XElement substanceEl, int ingredientSubstanceId, SplParseContext context)
        {
            #region implementation

            #region validation
            // Validate context and required services before processing
            if (context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return; // Cannot proceed without valid context
            }
            #endregion

            #region retrieve all active moiety elements
            // The XML can have multiple <activeMoiety> elements at the same level.
            // Each has a confusing nested structure: <activeMoiety><activeMoiety>...
            // We need to find all outer activeMoiety elements first
            var outerActiveMoietyElements = substanceEl.SplElements(sc.E.ActiveMoiety);

            if (outerActiveMoietyElements == null || !outerActiveMoietyElements.Any())
            {
                return; // No active moieties to parse
            }
            #endregion

            #region process each active moiety
            // Get database context once for all moiety operations
            var dbContext = context.GetDbContext();
            var moietyRepo = context.GetRepository<ActiveMoiety>();

            // Process each active moiety element
            foreach (var outerActiveMoietyEl in outerActiveMoietyElements)
            {
                #region extract inner moiety data
                // Navigate to the inner-most activeMoiety element that contains the code and name
                var innerActiveMoietyEl = outerActiveMoietyEl.GetSplElement(sc.E.ActiveMoiety);

                if (innerActiveMoietyEl == null)
                {
                    context.Logger.LogWarning(
                        "Skipping ActiveMoiety for IngredientSubstanceID {ID} due to missing inner activeMoiety element.",
                        ingredientSubstanceId);
                    continue; // Skip this moiety and process the next one
                }
                #endregion

                #region create moiety object
                // Create the active moiety object with extracted data
                var moiety = new ActiveMoiety
                {
                    IngredientSubstanceID = ingredientSubstanceId,
                    MoietyUNII = innerActiveMoietyEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                    MoietyName = innerActiveMoietyEl.GetSplElementVal(sc.E.Name)
                };
                #endregion

                #region validate moiety data
                // Validate that we have the required data before proceeding
                if (string.IsNullOrWhiteSpace(moiety.MoietyUNII) || string.IsNullOrWhiteSpace(moiety.MoietyName))
                {
                    context.Logger.LogWarning(
                        "Skipping ActiveMoiety for IngredientSubstanceID {ID} due to missing UNII or Name.",
                        ingredientSubstanceId);
                    continue; // Skip this moiety and process the next one
                }
                #endregion

                #region check for duplicates
                // Check if this exact moiety link already exists to prevent duplicates
                var existingMoiety = await dbContext.Set<ActiveMoiety>()
                    .FirstOrDefaultAsync(m => m.IngredientSubstanceID == ingredientSubstanceId
                        && m.MoietyUNII == moiety.MoietyUNII);

                if (existingMoiety != null)
                {
                    context.Logger.LogDebug(
                        "ActiveMoiety link for UNII {UNII} already exists for IngredientSubstanceID {ID}.",
                        moiety.MoietyUNII,
                        ingredientSubstanceId);
                    continue; // Skip this duplicate and process the next one
                }
                #endregion

                #region persist moiety record
                // Create and save the new active moiety record
                await moietyRepo.CreateAsync(moiety);
                context.Logger.LogInformation(
                    "Created ActiveMoiety '{Name}' (UNII: {UNII}) for IngredientSubstanceID {ID}",
                    moiety.MoietyName,
                    moiety.MoietyUNII,
                    ingredientSubstanceId);
                #endregion
            }
            #endregion

            #endregion
        }

        #endregion

        #region Ingredient Processing - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Parses all ingredient elements using bulk operations pattern. Collects all ingredients into memory,
        /// deduplicates against existing entities, then performs batch inserts for optimal performance.
        /// </summary>
        /// <param name="manufacturedProdEl">The XElement containing all ingredient elements to process.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A task that resolves to a SplParseResult indicating success and the count of created entities.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: 6N database calls (N ingredients × 6 entity types)
        /// - After: 12-18 queries/inserts total (2-3 per entity type)
        /// This represents a 30-100x performance improvement for large ingredient lists.
        /// 
        /// Handles hierarchical dependencies:
        /// 1. IngredientSubstances ? ActiveMoieties/ReferenceSubstances
        /// 2. SpecifiedSubstances (independent)
        /// 3. Ingredients ? IngredientSourceProducts
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SpecifiedSubstance"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseIngredientElementsAsync_bulkCalls(
            XElement manufacturedProdEl,
            SplParseContext context,
            Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation

            var result = new SplParseResult();

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate product context
            if (context.CurrentProduct?.ProductID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse ingredients because no product context exists.");
                return result;
            }

            try
            {
                var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

                reportProgress?.Invoke($"Starting Bulk Ingredient Processing for {context.FileNameInZip}");

                #region Phase 1: Parse all ingredients to DTOs (0 DB calls)

                var ingredientDtos = parseIngredientsToMemory(manufacturedProdEl, context, reportProgress);

                if (!ingredientDtos.Any())
                {
                    reportProgress?.Invoke($"No ingredients found in {context.FileNameInZip}");
                    return result;
                }

                reportProgress?.Invoke($"Parsed {ingredientDtos.Count} ingredients to memory");

                #endregion

                #region Phase 2 & 3: Bulk create entities with dependencies

                // Step 1: Create IngredientSubstances and get their IDs
                var substanceLookup = await bulkCreateIngredientSubstancesAsync(
                    dbContext, ingredientDtos, context, reportProgress);

                // Step 2: Create dependent entities (ActiveMoieties, ReferenceSubstances)
                await bulkCreateActiveMoietiesAsync(
                    dbContext, ingredientDtos, substanceLookup, context, reportProgress);

                await bulkCreateReferenceSubstancesAsync(
                    dbContext, ingredientDtos, substanceLookup, context, reportProgress);

                // Step 3: Create SpecifiedSubstances (independent entity)
                var specifiedSubstanceLookup = await bulkCreateSpecifiedSubstancesAsync(
                    dbContext, ingredientDtos, context, reportProgress);

                // Step 4: Create Ingredients
                var ingredientLookup = await bulkCreateIngredientsAsync(
                    dbContext,
                    context.CurrentProduct.ProductID.Value,
                    ingredientDtos,
                    substanceLookup,
                    specifiedSubstanceLookup,
                    context,
                    reportProgress);

                result.IngredientsCreated = ingredientLookup.Count;

                // Step 5: Create IngredientSourceProducts
                await bulkCreateIngredientSourceProductsAsync(
                    dbContext, ingredientDtos, ingredientLookup, context, reportProgress);

                #endregion

                reportProgress?.Invoke($"Completed Bulk Ingredient Processing for {context.FileNameInZip}");

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error in bulk ingredient processing: {ex.Message}");
                context.Logger?.LogError(ex, "Error in bulk ingredient processing for ProductID {ProductID}",
                    context.CurrentProduct?.ProductID);
                throw;
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all ingredient elements from XML into memory as DTOs without database operations.
        /// </summary>
        /// <param name="manufacturedProdEl">The XElement containing ingredient elements.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A list of IngredientDto objects with all parsed data.</returns>
        /// <remarks>
        /// This pure parsing method extracts all ingredient data including:
        /// - Core ingredient properties (class code, confidentiality, quantities)
        /// - Substance information (UNII, name)
        /// - Specified substance data
        /// - Active moieties
        /// - Reference substances
        /// - Source product information
        /// </remarks>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private List<IngredientDto> parseIngredientsToMemory(
            XElement manufacturedProdEl,
            SplParseContext context,
            Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation

            var ingredientDtos = new List<IngredientDto>();
            var ingredientElements = manufacturedProdEl.SplFindIngredients(excludingFieldsContaining: "substance");

            int sequenceNumber = 0;

            foreach (var element in ingredientElements)
            {
                var ingredientSubstanceEl = element.GetSplElement(sc.E.IngredientSubstance)
                    ?? element.GetSplElement(sc.E.InactiveIngredientSubstance)
                    ?? element.GetSplElement(sc.E.ActiveIngredientSubstance)
                    ?? element.SplFindElements("substance").FirstOrDefault();

                if (ingredientSubstanceEl == null)
                {
                    context.Logger?.LogWarning(
                        "Skipping ingredient element {ElementName} - no substance element found",
                        element.Name.LocalName);
                    continue;
                }

                var dto = new IngredientDto
                {
                    SequenceNumber = sequenceNumber,
                    OriginatingElement = element.Name.LocalName,
                    IngredientSubElementName = ingredientSubstanceEl.Name.LocalName
                };

                // Parse core ingredient properties
                parseIngredientProperties(element, ingredientSubstanceEl, dto);

                // Parse substance information
                parseSubstanceData(ingredientSubstanceEl, dto);

                // Parse specified substance
                parseSpecifiedSubstanceData(ingredientSubstanceEl, dto);

                // Parse active moieties
                parseActiveMoieties(ingredientSubstanceEl, dto);

                // Parse reference substance
                parseReferenceSubstance(ingredientSubstanceEl, dto);

                // Parse source product
                parseSourceProduct(element, dto);

                ingredientDtos.Add(dto);
                sequenceNumber++;
            }

            return ingredientDtos;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses core ingredient properties from XML elements into the DTO.
        /// </summary>
        /// <param name="element">The root ingredient XML element.</param>
        /// <param name="ingredientSubstanceEl">The ingredient substance sub-element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseIngredientProperties(
            XElement element,
            XElement ingredientSubstanceEl,
            IngredientDto dto)
        {
            #region implementation

            // Determine class code with fallback for inactive ingredients
            string? classCode = !string.IsNullOrEmpty(dto.IngredientSubElementName)
                && dto.IngredientSubElementName.Contains("inactive", StringComparison.OrdinalIgnoreCase)
                ? "IACT"
                : element.GetAttrVal(sc.A.ClassCode);

            dto.ClassCode = classCode;
            dto.IsConfidential = element.GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B";

            // Parse quantity information
            var quantityEl = element.GetSplElement(sc.E.Quantity);
            if (quantityEl != null)
            {
                parseQuantityIntoDto(quantityEl, dto);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses quantity element data into the DTO.
        /// </summary>
        /// <param name="quantityEl">The quantity XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        private void parseQuantityIntoDto(XElement quantityEl, IngredientDto dto)
        {
            #region implementation

            // Parse numerator
            var numeratorEl = quantityEl.GetSplElement(sc.E.Numerator);
            if (numeratorEl != null)
            {
                var (value, unit, code, system, displayName) = parseRatioPart(
                    numeratorEl, sc.E.NumeratorIngredientTranslation);

                dto.QuantityNumerator = value;
                dto.QuantityNumeratorUnit = unit;
                dto.NumeratorTranslationCode = code;
                dto.NumeratorCodeSystem = system;
                dto.NumeratorDisplayName = displayName;
            }

            // Parse denominator
            var denominatorEl = quantityEl.GetSplElement(sc.E.Denominator);
            if (denominatorEl != null)
            {
                var (value, unit, code, system, displayName) = parseRatioPart(
                    denominatorEl, sc.E.DenominatorIngredientTranslation);

                dto.QuantityDenominator = value;
                dto.QuantityDenominatorUnit = unit;
                dto.DenominatorTranslationCode = code;
                dto.DenominatorCodeSystem = system;
                dto.DenominatorDisplayName = displayName;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses substance identification data from XML into the DTO.
        /// </summary>
        /// <param name="substanceEl">The substance XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseSubstanceData(XElement substanceEl, IngredientDto dto)
        {
            #region implementation

            dto.SubstanceUNII = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue) ?? string.Empty;
            var nameElement = substanceEl.GetSplElement(sc.E.Name);
            dto.SubstanceName = nameElement?.Value ?? string.Empty;
            dto.SubstanceFieldName = substanceEl.Name.LocalName;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses specified substance data from XML into the DTO.
        /// </summary>
        /// <param name="substanceEl">The substance XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseSpecifiedSubstanceData(XElement substanceEl, IngredientDto dto)
        {
            #region implementation

            dto.SpecifiedSubstanceCode = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            dto.SpecifiedSubstanceCodeSystem = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
            dto.SpecifiedSubstanceCodeSystemName = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all active moiety elements from the substance element into the DTO.
        /// </summary>
        /// <param name="substanceEl">The substance XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ActiveMoietyDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseActiveMoieties(XElement substanceEl, IngredientDto dto)
        {
            #region implementation

            var outerActiveMoietyElements = substanceEl.SplElements(sc.E.ActiveMoiety);

            foreach (var outerMoietyEl in outerActiveMoietyElements)
            {
                var innerMoietyEl = outerMoietyEl.GetSplElement(sc.E.ActiveMoiety);
                if (innerMoietyEl == null)
                    continue;

                var moietyUNII = innerMoietyEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                var moietyName = innerMoietyEl.GetSplElementVal(sc.E.Name);

                if (!string.IsNullOrWhiteSpace(moietyUNII) && !string.IsNullOrWhiteSpace(moietyName))
                {
                    dto.ActiveMoieties.Add(new ActiveMoietyDto
                    {
                        MoietyUNII = moietyUNII,
                        MoietyName = moietyName
                    });
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses reference substance data from the substance element into the DTO.
        /// </summary>
        /// <param name="substanceEl">The substance XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ReferenceSubstanceDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseReferenceSubstance(XElement substanceEl, IngredientDto dto)
        {
            #region implementation

            var definingSubstanceEl = substanceEl.SplElement(sc.E.AsEquivalentSubstance, sc.E.DefiningSubstance);
            if (definingSubstanceEl == null)
                return;

            var refUnii = definingSubstanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var refName = definingSubstanceEl.GetSplElementVal(sc.E.Name);

            if (!string.IsNullOrWhiteSpace(refUnii) && !string.IsNullOrWhiteSpace(refName))
            {
                dto.ReferenceSubstance = new ReferenceSubstanceDto
                {
                    RefSubstanceUNII = refUnii,
                    RefSubstanceName = refName
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses source product data from the ingredient element into the DTO.
        /// </summary>
        /// <param name="ingredientEl">The ingredient XML element.</param>
        /// <param name="dto">The DTO to populate.</param>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="IngredientSourceProductDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private void parseSourceProduct(XElement ingredientEl, IngredientDto dto)
        {
            #region implementation

            var codeEl = ingredientEl.SplElement(sc.E.SubjectOf, sc.E.SubstanceSpecification, sc.E.Code);
            if (codeEl == null)
                return;

            var sourceNdc = codeEl.GetAttrVal(sc.A.CodeValue);
            var sourceNdcSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

            if (!string.IsNullOrWhiteSpace(sourceNdc))
            {
                dto.SourceProduct = new IngredientSourceProductDto
                {
                    SourceProductNDC = sourceNdc,
                    SourceProductNDCSystem = sourceNdcSystem
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of IngredientSubstance entities, checking for existing substances
        /// and creating only missing ones in batch operations.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs parsed from XML.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A dictionary mapping substance keys to their database IDs.</returns>
        /// <remarks>
        /// Creates substances in bulk then immediately saves to populate IDs needed for dependent entities.
        /// Returns a lookup dictionary enabling downstream operations to link to substances efficiently.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Dictionary<string, int>> bulkCreateIngredientSubstancesAsync(
            ApplicationDbContext dbContext,
            List<IngredientDto> ingredientDtos,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var substanceDbSet = dbContext.Set<IngredientSubstance>();

            // Get distinct substances (by UNII for normalized, by name for non-normalized)
            var distinctSubstances = ingredientDtos
                .GroupBy(dto => new { dto.SubstanceUNII, dto.SubstanceName })
                .Select(g => g.First())
                .ToList();

            reportProgress?.Invoke($"Processing {distinctSubstances.Count} distinct substances");

            // Get all UNIIs and names for querying
            var uniiList = distinctSubstances
                .Where(dto => !string.IsNullOrWhiteSpace(dto.SubstanceUNII))
                .Select(dto => dto.SubstanceUNII)
                .Distinct()
                .ToList();

            var nameList = distinctSubstances
                .Where(dto => string.IsNullOrWhiteSpace(dto.SubstanceUNII) && !string.IsNullOrWhiteSpace(dto.SubstanceName))
                .Select(dto => dto.SubstanceName.ToLower())
                .Distinct()
                .ToList();

            // Query existing substances
            var existingByUNII = await substanceDbSet
                .Where(s => s.UNII != null && uniiList.Contains(s.UNII))
                .Select(s => new { s.UNII, s.IngredientSubstanceID })
                .ToListAsync();

            var existingByName = await substanceDbSet
                .Where(s => s.UNII == null && s.SubstanceName != null && nameList.Contains(s.SubstanceName.ToLower()))
                .Select(s => new { s.SubstanceName, s.IngredientSubstanceID })
                .ToListAsync();

            // Build lookup of existing substances
            var existingLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingByUNII)
            {
                if (existing.UNII != null && existing.IngredientSubstanceID.HasValue)
                {
                    existingLookup[$"UNII:{existing.UNII}"] = existing.IngredientSubstanceID.Value;
                }
            }

            foreach (var existing in existingByName)
            {
                if (existing.SubstanceName != null && existing.IngredientSubstanceID.HasValue)
                {
                    existingLookup[$"NAME:{existing.SubstanceName.ToLower()}"] = existing.IngredientSubstanceID.Value;
                }
            }

            // Create new substances
            var newSubstances = new List<IngredientSubstance>();

            foreach (var dto in distinctSubstances)
            {
                string lookupKey = !string.IsNullOrWhiteSpace(dto.SubstanceUNII)
                    ? $"UNII:{dto.SubstanceUNII}"
                    : $"NAME:{dto.SubstanceName.ToLower()}";

                if (!existingLookup.ContainsKey(lookupKey))
                {
                    var newSubstance = new IngredientSubstance
                    {
                        UNII = !string.IsNullOrWhiteSpace(dto.SubstanceUNII) ? dto.SubstanceUNII : null,
                        SubstanceName = dto.SubstanceName?.ToLower(),
                        OriginatingElement = dto.SubstanceFieldName
                    };

                    newSubstances.Add(newSubstance);

                    if (string.IsNullOrWhiteSpace(dto.SubstanceUNII))
                    {
                        context.Logger?.LogWarning(
                            "Creating non-normalized IngredientSubstance '{Name}' without UNII",
                            dto.SubstanceName);
                    }
                }
            }

            if (newSubstances.Any())
            {
                substanceDbSet.AddRange(newSubstances);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newSubstances.Count} new IngredientSubstances");

                // Add newly created substances to lookup
                foreach (var substance in newSubstances)
                {
                    if (substance.IngredientSubstanceID.HasValue)
                    {
                        string lookupKey = !string.IsNullOrWhiteSpace(substance.UNII)
                            ? $"UNII:{substance.UNII}"
                            : $"NAME:{substance.SubstanceName?.ToLower()}";

                        existingLookup[lookupKey] = substance.IngredientSubstanceID.Value;
                    }
                }
            }

            return existingLookup;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of ActiveMoiety entities linked to IngredientSubstances.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs with active moiety data.</param>
        /// <param name="substanceLookup">Dictionary mapping substance keys to their IDs.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="ActiveMoiety"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task bulkCreateActiveMoietiesAsync(
            ApplicationDbContext dbContext,
            List<IngredientDto> ingredientDtos,
            Dictionary<string, int> substanceLookup,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var moietyDbSet = dbContext.Set<ActiveMoiety>();

            // Collect all moieties with their parent substance IDs
            var moietyData = new List<(int SubstanceId, ActiveMoietyDto Moiety)>();

            foreach (var dto in ingredientDtos)
            {
                if (!dto.ActiveMoieties.Any())
                    continue;

                string substanceKey = !string.IsNullOrWhiteSpace(dto.SubstanceUNII)
                    ? $"UNII:{dto.SubstanceUNII}"
                    : $"NAME:{dto.SubstanceName.ToLower()}";

                if (substanceLookup.TryGetValue(substanceKey, out int substanceId))
                {
                    foreach (var moiety in dto.ActiveMoieties)
                    {
                        moietyData.Add((substanceId, moiety));
                    }
                }
            }

            if (!moietyData.Any())
                return;

            reportProgress?.Invoke($"Processing {moietyData.Count} active moieties");

            // Get distinct substance IDs to query existing moieties
            var substanceIds = moietyData.Select(m => m.SubstanceId).Distinct().ToList();

            var existingMoieties = await moietyDbSet
                .Where(m => m.IngredientSubstanceID.HasValue && substanceIds.Contains(m.IngredientSubstanceID.Value))
                .Select(m => new { m.IngredientSubstanceID, m.MoietyUNII })
                .ToListAsync();

            var existingKeys = new HashSet<(int SubstanceId, string MoietyUNII)>(
                existingMoieties
                    .Where(m => m.IngredientSubstanceID.HasValue && !string.IsNullOrWhiteSpace(m.MoietyUNII))
                    .Select(m => (m.IngredientSubstanceID!.Value, m.MoietyUNII!))
            );

            // Create new moieties
            var newMoieties = moietyData
                .Where(m => !existingKeys.Contains((m.SubstanceId, m.Moiety.MoietyUNII)))
                .Select(m => new ActiveMoiety
                {
                    IngredientSubstanceID = m.SubstanceId,
                    MoietyUNII = m.Moiety.MoietyUNII,
                    MoietyName = m.Moiety.MoietyName
                })
                .ToList();

            if (newMoieties.Any())
            {
                moietyDbSet.AddRange(newMoieties);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newMoieties.Count} new ActiveMoieties");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of ReferenceSubstance entities linked to IngredientSubstances.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs with reference substance data.</param>
        /// <param name="substanceLookup">Dictionary mapping substance keys to their IDs.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Only creates reference substances for ingredients with classCode "ACTIR" (Active Ingredient Reference Basis).
        /// </remarks>
        /// <seealso cref="ReferenceSubstance"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task bulkCreateReferenceSubstancesAsync(
            ApplicationDbContext dbContext,
            List<IngredientDto> ingredientDtos,
            Dictionary<string, int> substanceLookup,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var refSubstanceDbSet = dbContext.Set<ReferenceSubstance>();

            // Collect reference substances for ACTIR ingredients only
            var refData = new List<(int SubstanceId, ReferenceSubstanceDto RefSub)>();

            foreach (var dto in ingredientDtos)
            {
                if (dto.ReferenceSubstance == null)
                    continue;

                if (!string.Equals(dto.ClassCode, c.ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE, StringComparison.OrdinalIgnoreCase))
                    continue;

                string substanceKey = !string.IsNullOrWhiteSpace(dto.SubstanceUNII)
                    ? $"UNII:{dto.SubstanceUNII}"
                    : $"NAME:{dto.SubstanceName.ToLower()}";

                if (substanceLookup.TryGetValue(substanceKey, out int substanceId))
                {
                    refData.Add((substanceId, dto.ReferenceSubstance));
                }
            }

            if (!refData.Any())
                return;

            reportProgress?.Invoke($"Processing {refData.Count} reference substances");

            // Get distinct substance IDs to query existing references
            var substanceIds = refData.Select(r => r.SubstanceId).Distinct().ToList();

            var existingRefs = await refSubstanceDbSet
                .Where(r => r.IngredientSubstanceID.HasValue && substanceIds.Contains(r.IngredientSubstanceID.Value))
                .Select(r => new { r.IngredientSubstanceID, r.RefSubstanceUNII })
                .ToListAsync();

            var existingKeys = new HashSet<(int SubstanceId, string RefUNII)>(
                existingRefs
                    .Where(r => r.IngredientSubstanceID.HasValue && !string.IsNullOrWhiteSpace(r.RefSubstanceUNII))
                    .Select(r => (r.IngredientSubstanceID!.Value, r.RefSubstanceUNII!))
            );

            // Create new reference substances
            var newRefs = refData
                .Where(r => !existingKeys.Contains((r.SubstanceId, r.RefSub.RefSubstanceUNII)))
                .Select(r => new ReferenceSubstance
                {
                    IngredientSubstanceID = r.SubstanceId,
                    RefSubstanceUNII = r.RefSub.RefSubstanceUNII,
                    RefSubstanceName = r.RefSub.RefSubstanceName
                })
                .ToList();

            if (newRefs.Any())
            {
                refSubstanceDbSet.AddRange(newRefs);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newRefs.Count} new ReferenceSubstances");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of SpecifiedSubstance entities.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs with specified substance data.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A dictionary mapping specified substance keys to their database IDs.</returns>
        /// <remarks>
        /// SpecifiedSubstances are independent entities identified by code and code system.
        /// Returns a lookup dictionary for linking to Ingredient entities.
        /// </remarks>
        /// <seealso cref="SpecifiedSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Dictionary<string, int>> bulkCreateSpecifiedSubstancesAsync(
            ApplicationDbContext dbContext,
            List<IngredientDto> ingredientDtos,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var specSubstanceDbSet = dbContext.Set<SpecifiedSubstance>();

            // Get distinct specified substances
            var distinctSpecSubstances = ingredientDtos
                .Where(dto => !string.IsNullOrWhiteSpace(dto.SpecifiedSubstanceCode) &&
                              !string.IsNullOrWhiteSpace(dto.SpecifiedSubstanceCodeSystem))
                .GroupBy(dto => new { dto.SpecifiedSubstanceCode, dto.SpecifiedSubstanceCodeSystem })
                .Select(g => g.First())
                .ToList();

            if (!distinctSpecSubstances.Any())
                return new Dictionary<string, int>();

            reportProgress?.Invoke($"Processing {distinctSpecSubstances.Count} specified substances");

            // Get all codes for querying
            var codeList = distinctSpecSubstances.Select(dto => dto.SpecifiedSubstanceCode).ToList();

            var existingSpecSubstances = await specSubstanceDbSet
                .Where(s => s.SubstanceCode != null && codeList.Contains(s.SubstanceCode))
                .Select(s => new { s.SubstanceCode, s.SubstanceCodeSystem, s.SpecifiedSubstanceID })
                .ToListAsync();

            // Build lookup with case-insensitive comparison for code system
            var existingLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingSpecSubstances)
            {
                if (existing.SubstanceCode != null &&
                    existing.SubstanceCodeSystem != null &&
                    existing.SpecifiedSubstanceID.HasValue)
                {
                    string key = $"{existing.SubstanceCode}|{existing.SubstanceCodeSystem}";
                    existingLookup[key] = existing.SpecifiedSubstanceID.Value;
                }
            }

            // Create new specified substances
            var newSpecSubstances = new List<SpecifiedSubstance>();

            foreach (var dto in distinctSpecSubstances)
            {
                string key = $"{dto.SpecifiedSubstanceCode}|{dto.SpecifiedSubstanceCodeSystem}";

                if (!existingLookup.ContainsKey(key))
                {
                    newSpecSubstances.Add(new SpecifiedSubstance
                    {
                        SubstanceCode = dto.SpecifiedSubstanceCode,
                        SubstanceCodeSystem = dto.SpecifiedSubstanceCodeSystem,
                        SubstanceCodeSystemName = dto.SpecifiedSubstanceCodeSystemName
                    });
                }
            }

            if (newSpecSubstances.Any())
            {
                specSubstanceDbSet.AddRange(newSpecSubstances);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newSpecSubstances.Count} new SpecifiedSubstances");

                // Add newly created to lookup
                foreach (var specSubstance in newSpecSubstances)
                {
                    if (specSubstance.SpecifiedSubstanceID.HasValue &&
                        !string.IsNullOrWhiteSpace(specSubstance.SubstanceCode) &&
                        !string.IsNullOrWhiteSpace(specSubstance.SubstanceCodeSystem))
                    {
                        string key = $"{specSubstance.SubstanceCode}|{specSubstance.SubstanceCodeSystem}";
                        existingLookup[key] = specSubstance.SpecifiedSubstanceID.Value;
                    }
                }
            }

            return existingLookup;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of Ingredient entities, linking to substances.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="productId">The parent product ID for all ingredients.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs.</param>
        /// <param name="substanceLookup">Dictionary mapping substance keys to IDs.</param>
        /// <param name="specifiedSubstanceLookup">Dictionary mapping specified substance keys to IDs.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A dictionary mapping ingredient sequence numbers to their database IDs.</returns>
        /// <remarks>
        /// Creates all ingredient records in bulk, returning a lookup for source product creation.
        /// </remarks>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Dictionary<int, int>> bulkCreateIngredientsAsync(
            ApplicationDbContext dbContext,
            int productId,
            List<IngredientDto> ingredientDtos,
            Dictionary<string, int> substanceLookup,
            Dictionary<string, int> specifiedSubstanceLookup,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var ingredientDbSet = dbContext.Set<Ingredient>();

            reportProgress?.Invoke($"Processing {ingredientDtos.Count} ingredients");

            // Query existing ingredients for this product
            var existingIngredients = await ingredientDbSet
                .Where(i => i.ProductID == productId)
                .Select(i => new { i.SequenceNumber, i.IngredientID })
                .ToListAsync();

            var existingKeys = new HashSet<int>(
                existingIngredients
                    .Where(i => i.SequenceNumber.HasValue && i.IngredientID.HasValue)
                    .Select(i => i.SequenceNumber!.Value)
            );

            // Create new ingredients
            var newIngredients = new List<Ingredient>();

            foreach (var dto in ingredientDtos)
            {
                if (existingKeys.Contains(dto.SequenceNumber))
                    continue;

                // Resolve substance ID
                string substanceKey = !string.IsNullOrWhiteSpace(dto.SubstanceUNII)
                    ? $"UNII:{dto.SubstanceUNII}"
                    : $"NAME:{dto.SubstanceName.ToLower()}";

                if (!substanceLookup.TryGetValue(substanceKey, out int substanceId))
                {
                    context.Logger?.LogWarning(
                        "Could not find IngredientSubstanceID for substance '{Name}', skipping ingredient",
                        dto.SubstanceName);
                    continue;
                }

                // Resolve specified substance ID (optional)
                int? specifiedSubstanceId = null;
                if (!string.IsNullOrWhiteSpace(dto.SpecifiedSubstanceCode) &&
                    !string.IsNullOrWhiteSpace(dto.SpecifiedSubstanceCodeSystem))
                {
                    string specKey = $"{dto.SpecifiedSubstanceCode}|{dto.SpecifiedSubstanceCodeSystem}";
                    if (specifiedSubstanceLookup.TryGetValue(specKey, out int specId))
                    {
                        specifiedSubstanceId = specId;
                    }
                }

                var ingredient = new Ingredient
                {
                    ProductID = productId,
                    IngredientSubstanceID = substanceId,
                    SpecifiedSubstanceID = specifiedSubstanceId ?? 0,
                    SequenceNumber = dto.SequenceNumber,
                    OriginatingElement = dto.OriginatingElement,
                    ClassCode = dto.ClassCode,
                    IsConfidential = dto.IsConfidential,
                    QuantityNumerator = dto.QuantityNumerator,
                    QuantityNumeratorUnit = dto.QuantityNumeratorUnit,
                    NumeratorTranslationCode = dto.NumeratorTranslationCode,
                    NumeratorCodeSystem = dto.NumeratorCodeSystem,
                    NumeratorDisplayName = dto.NumeratorDisplayName,
                    QuantityDenominator = dto.QuantityDenominator,
                    QuantityDenominatorUnit = dto.QuantityDenominatorUnit,
                    DenominatorTranslationCode = dto.DenominatorTranslationCode,
                    DenominatorCodeSystem = dto.DenominatorCodeSystem,
                    DenominatorDisplayName = dto.DenominatorDisplayName
                };

                newIngredients.Add(ingredient);
            }

            if (newIngredients.Any())
            {
                ingredientDbSet.AddRange(newIngredients);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newIngredients.Count} new Ingredients");
            }

            // Build lookup of all ingredients (existing + new)
            var ingredientLookup = new Dictionary<int, int>();

            foreach (var existing in existingIngredients)
            {
                if (existing.SequenceNumber.HasValue && existing.IngredientID.HasValue)
                {
                    ingredientLookup[existing.SequenceNumber.Value] = existing.IngredientID.Value;
                }
            }

            foreach (var newIngredient in newIngredients)
            {
                if (newIngredient.SequenceNumber.HasValue && newIngredient.IngredientID.HasValue)
                {
                    ingredientLookup[newIngredient.SequenceNumber.Value] = newIngredient.IngredientID.Value;
                }
            }

            return ingredientLookup;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of IngredientSourceProduct entities.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="ingredientDtos">The list of ingredient DTOs with source product data.</param>
        /// <param name="ingredientLookup">Dictionary mapping ingredient sequence numbers to IDs.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <seealso cref="IngredientSourceProduct"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task bulkCreateIngredientSourceProductsAsync(
            ApplicationDbContext dbContext,
            List<IngredientDto> ingredientDtos,
            Dictionary<int, int> ingredientLookup,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var sourceProductDbSet = dbContext.Set<IngredientSourceProduct>();

            // Collect source product data
            var sourceProductData = new List<(int IngredientId, IngredientSourceProductDto SourceProduct)>();

            foreach (var dto in ingredientDtos)
            {
                if (dto.SourceProduct == null)
                    continue;

                if (ingredientLookup.TryGetValue(dto.SequenceNumber, out int ingredientId))
                {
                    sourceProductData.Add((ingredientId, dto.SourceProduct));
                }
            }

            if (!sourceProductData.Any())
                return;

            reportProgress?.Invoke($"Processing {sourceProductData.Count} ingredient source products");

            // Get distinct ingredient IDs to query existing source products
            var ingredientIds = sourceProductData.Select(sp => sp.IngredientId).Distinct().ToList();

            var existingSourceProducts = await sourceProductDbSet
                .Where(sp => sp.IngredientID.HasValue && ingredientIds.Contains(sp.IngredientID.Value))
                .Select(sp => new { sp.IngredientID, sp.SourceProductNDC })
                .ToListAsync();

            var existingKeys = new HashSet<(int IngredientId, string NDC)>(
                existingSourceProducts
                    .Where(sp => sp.IngredientID.HasValue && !string.IsNullOrWhiteSpace(sp.SourceProductNDC))
                    .Select(sp => (sp.IngredientID!.Value, sp.SourceProductNDC!))
            );

            // Create new source products
            var newSourceProducts = sourceProductData
                .Where(sp => !existingKeys.Contains((sp.IngredientId, sp.SourceProduct.SourceProductNDC)))
                .Select(sp => new IngredientSourceProduct
                {
                    IngredientID = sp.IngredientId,
                    SourceProductNDC = sp.SourceProduct.SourceProductNDC,
                    SourceProductNDCSysten = sp.SourceProduct.SourceProductNDCSystem
                })
                .ToList();

            if (newSourceProducts.Any())
            {
                sourceProductDbSet.AddRange(newSourceProducts);
                await dbContext.SaveChangesAsync();
                reportProgress?.Invoke($"Created {newSourceProducts.Count} new IngredientSourceProducts");
            }

            #endregion
        }

        #endregion

        #region Ingredient DTO Classes

        /**************************************************************/
        /// <summary>
        /// Data transfer object representing an ingredient parsed from XML.
        /// Contains all ingredient data and related entity information.
        /// </summary>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="Label"/>
        private class IngredientDto
        {
            #region implementation

            public int SequenceNumber { get; set; }
            public string OriginatingElement { get; set; } = string.Empty;
            public string? IngredientSubElementName { get; set; }
            public string? ClassCode { get; set; }
            public bool IsConfidential { get; set; }

            // Quantity data
            public decimal? QuantityNumerator { get; set; }
            public string? QuantityNumeratorUnit { get; set; }
            public string? NumeratorTranslationCode { get; set; }
            public string? NumeratorCodeSystem { get; set; }
            public string? NumeratorDisplayName { get; set; }
            public decimal? QuantityDenominator { get; set; }
            public string? QuantityDenominatorUnit { get; set; }
            public string? DenominatorTranslationCode { get; set; }
            public string? DenominatorCodeSystem { get; set; }
            public string? DenominatorDisplayName { get; set; }

            // Substance identification
            public string SubstanceUNII { get; set; } = string.Empty;
            public string SubstanceName { get; set; } = string.Empty;
            public string SubstanceFieldName { get; set; } = string.Empty;

            // Specified substance
            public string? SpecifiedSubstanceCode { get; set; }
            public string? SpecifiedSubstanceCodeSystem { get; set; }
            public string? SpecifiedSubstanceCodeSystemName { get; set; }

            // Related entities
            public List<ActiveMoietyDto> ActiveMoieties { get; set; } = new();
            public ReferenceSubstanceDto? ReferenceSubstance { get; set; }
            public IngredientSourceProductDto? SourceProduct { get; set; }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object representing an active moiety.
        /// </summary>
        /// <seealso cref="ActiveMoiety"/>
        /// <seealso cref="Label"/>
        private class ActiveMoietyDto
        {
            #region implementation

            public string MoietyUNII { get; set; } = string.Empty;
            public string MoietyName { get; set; } = string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object representing a reference substance.
        /// </summary>
        /// <seealso cref="ReferenceSubstance"/>
        /// <seealso cref="Label"/>
        private class ReferenceSubstanceDto
        {
            #region implementation

            public string RefSubstanceUNII { get; set; } = string.Empty;
            public string RefSubstanceName { get; set; } = string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object representing an ingredient source product.
        /// </summary>
        /// <seealso cref="IngredientSourceProduct"/>
        /// <seealso cref="Label"/>
        private class IngredientSourceProductDto
        {
            #region implementation

            public string SourceProductNDC { get; set; } = string.Empty;
            public string? SourceProductNDCSystem { get; set; }

            #endregion
        }

        #endregion

    }
}