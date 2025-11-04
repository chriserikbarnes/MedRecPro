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
        /// <param name="element">The XElement representing the ingredient section to parse.</param>
        /// <param name="context">The current parsing context containing the product to link ingredients to.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
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
        public async Task<SplParseResult> ParseAsync(XElement element,
        SplParseContext context,
        Action<string>? reportProgress)
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

            reportProgress?.Invoke($"Starting Ingredient XML Element {context.FileNameInZip}");

            // 1. Validate preconditions and find the core element
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

                // 2. Get or create the required substance entities from the database
                var (substance, specifiedSubstanceId) = await getOrCreateSubstanceEntitiesAsync(ingredientSubstanceEl,
                    context,
                    element.GetAttrVal(sc.A.ClassCode),
                    reportProgress);

                // Get the substance element name
                string ingredientSubstanceEnclosingElement = ingredientSubstanceEl.Name.LocalName;

                if (substance == null || substance.IngredientSubstanceID == null)
                {
                    throw new InvalidOperationException("Failed to get or create an IngredientSubstance.");
                }

                // 3. Build the Ingredient object from the XML data (no DB calls here)
                Ingredient ingredient = buildIngredient(element,
                    context,
                    substance.IngredientSubstanceID.Value,
                    specifiedSubstanceId,
                    context.SeqNumber,
                    ingredientSubstanceEnclosingElement);

                // 4. Persist the new Ingredient to the database
                await saveIngredientAsync(ingredient, context);

                // 5. If the ingredient has a source product, create the link now that we have an IngredientID
                if (ingredient.IngredientID.HasValue)
                {
                    await createIngredientSourceProductAsync(element, ingredient.IngredientID.Value, context);
                }

                result.IngredientsCreated++;

                reportProgress?.Invoke($"Completed Ingredient XML Element {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Centralized error handling for the entire operation
                result.Success = false;

                result.Errors.Add($"Error parsing ingredient for ProductID {context?.CurrentProduct?.ProductID}: {ex.Message}");

                context?.Logger.LogError(ex, "Error processing <ingredient> element for ProductID {ProductID}.", context?.CurrentProduct?.ProductID);

                context?.Logger?.LogError(ex, "Error processing <ingredient> element for ProductID {ProductID}.", context.CurrentProduct?.ProductID ?? 0);
            }

            return result;
            #endregion
        }

        #region Private Helper Methods

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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        #endregion

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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
    }
}