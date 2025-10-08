using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
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
        /// <seealso cref="Label"/>
        private (decimal? Value, string Unit, string Code, string System, string DisplayName) parseRatioPart(XElement ratioPartEl, string translationElementName)
        {
            #region implementation
            // Attempt to parse the numeric value from the element
            decimal? parsedValue = null;
            if (decimal.TryParse(ratioPartEl.GetAttrVal(sc.A.Value), out var val))
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

            if(substanceEl == null 
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
        /// Finds an existing IngredientSubstance by its UNII. If not found, creates a new one.
        /// This ensures that substance data is normalized in the database.
        /// </summary>
        /// <param name="substanceEl">The XElement representing the ingredientSubstance to process.</param>
        /// <param name="context">The current parsing context containing database access services.</param>
        /// <param name="ingredientClassCode">The class code for the ingredient e.g. classCode="IACT"</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param> 
        /// <returns>An IngredientSubstance entity, either existing or newly created, or null if creation fails.</returns>
        /// <example>
        /// <code>
        /// var substance = await getOrCreateIngredientSubstanceAsync(substanceElement, context);
        /// if (substance != null)
        /// {
        ///     Console.WriteLine($"Substance: {substance.SubstanceName} (UNII: {substance.UNII})");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements substance normalization using UNII (Unique Ingredient Identifier) codes.
        /// The process:
        /// 1. Extracts UNII and name from the XML element
        /// 2. If UNII is missing, creates a non-normalized substance record
        /// 3. If UNII exists, searches for existing substance with same UNII
        /// 4. If found, returns existing substance; otherwise creates new one
        /// 
        /// This approach prevents duplicate substance records for the same chemical entity.
        /// </remarks>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions.GetSplElementAttrVal(XElement, string, string)"/>
        /// <seealso cref="Label"/>
        private async Task<IngredientSubstance?> getOrCreateIngredientSubstanceAsync(XElement substanceEl, 
            SplParseContext context, 
            string? ingredientClassCode,
            Action<string>? reportProgress)
        {
            #region implementation
            if(substanceEl == null
                || context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return null;
            }

            // Extract UNII code from the code element's codeValue attribute
            var unii = substanceEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);

            // Extract substance name from the name element
            var name = substanceEl.GetSplElement(sc.E.Name);

            // Enclosing field. 
            var field = substanceEl.Name.LocalName;

            // Use the DbContext directly for the specific 'find by UNII' query
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get the DbSet for IngredientSubstance
            var substanceDbSet = dbContext.Set<IngredientSubstance>();

            // Search for existing substance with the same UNII code
            var existingSubstance = await substanceDbSet.FirstOrDefaultAsync(s => s != null
                && s.UNII != null
                && s.UNII == unii);

            // Search by name if UNII is not found or is empty
            if ((existingSubstance == null || existingSubstance.IngredientSubstanceID <= 0)
                && name != null && !string.IsNullOrWhiteSpace(name.Value))
                existingSubstance = await substanceDbSet
                    .FirstOrDefaultAsync(s => s != null
                        && !string.IsNullOrEmpty(s.SubstanceName)
                        && s.SubstanceName.ToString().ToLower() == name.Value.ToLower());

            // Return existing substance if found
            if (existingSubstance != null)
            {
                context.Logger.LogDebug("Found existing IngredientSubstance '{Name}' with UNII {UNII}", name, unii);
                reportProgress?.Invoke($"Skipped Existing Ingredient {existingSubstance.SubstanceName} for file {context.FileNameInZip}");
                return existingSubstance;
            }

            // Handle case where UNII is missing - create non-normalized substance
            if (string.IsNullOrWhiteSpace(unii))
            {
                context.Logger.LogWarning("Ingredient substance '{Name}' is missing a UNII. It will not be normalized and a new record may be created.", name);

                // Fallback: create a new substance record every time if UNII is missing
                var nonNormalizedSubstance = new IngredientSubstance
                {
                    SubstanceName = name?.Value?.ToLower(),
                    OriginatingElement = field
                };

                var repo = context.GetRepository<IngredientSubstance>();
                await repo.CreateAsync(nonNormalizedSubstance);
                reportProgress?.Invoke($"Added Ingredient {nonNormalizedSubstance.SubstanceName} for file {context.FileNameInZip}");
                return nonNormalizedSubstance;
            }

            // If not found, create, save, and return the new entity
            context.Logger.LogInformation("Creating new IngredientSubstance '{Name}' with UNII {UNII}", name, unii);

            // Create new substance with extracted UNII and name
            var newSubstance = new IngredientSubstance
            {
                UNII = unii,
                SubstanceName = name?.Value?.ToLower(),
                OriginatingElement = field
            };

            // Add to DbSet and save immediately to get the new ID populated
            substanceDbSet.Add(newSubstance);
            await dbContext.SaveChangesAsync(); // Save immediately to get the new ID

            // If the substance has an active moiety, create it
            if (newSubstance != null && newSubstance.IngredientSubstanceID > 0)
            {
                await createActiveMoietyAsync(substanceEl, newSubstance.IngredientSubstanceID.Value, context);

                // If the ingredient is a reference ingredient for strength, create the link
                if (!string.IsNullOrWhiteSpace(ingredientClassCode) 
                    && ingredientClassCode.Equals(c.ACTIVE_INGREDIENT_REFERENCE_BASIS_CODE, StringComparison.OrdinalIgnoreCase))
                {
                    await getOrCreateReferenceSubstanceAsync(substanceEl, newSubstance.IngredientSubstanceID.Value, context);
                }
            }

            reportProgress?.Invoke($"Added Ingredient {newSubstance?.SubstanceName} for file {context.FileNameInZip}");

            return newSubstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates ActiveMoiety records linked to a parent IngredientSubstance.
        /// </summary>
        /// <param name="substanceEl">The XML element containing the substance data with potential active moiety information.</param>
        /// <param name="ingredientSubstanceId">The ID of the parent ingredient substance to link the active moiety to.</param>
        /// <param name="context">The current parsing context containing database access and logging services.</param>
        /// <seealso cref="ActiveMoiety"/>
        /// <seealso cref="IngredientSubstance"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task createActiveMoietyAsync(XElement substanceEl, int ingredientSubstanceId, SplParseContext context)
        {
            #region implementation
            // The XML can have a confusing <activeMoiety><activeMoiety>... structure.
            // We need to find the inner-most one that contains the code and name.
            var activeMoietyEl = substanceEl.GetSplElement(sc.E.ActiveMoiety)?.GetSplElement(sc.E.ActiveMoiety);

            if (activeMoietyEl == null
                || context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                return; // No active moiety to parse
            }

            // Create the active moiety object with extracted data
            var moiety = new ActiveMoiety
            {
                IngredientSubstanceID = ingredientSubstanceId,
                MoietyUNII = activeMoietyEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                MoietyName = activeMoietyEl.GetSplElementVal(sc.E.Name)
            };

            // Validate that we have the required data before proceeding
            if (string.IsNullOrWhiteSpace(moiety.MoietyUNII) || string.IsNullOrWhiteSpace(moiety.MoietyName))
            {
                context.Logger.LogWarning("Skipping ActiveMoiety for IngredientSubstanceID {ID} due to missing UNII or Name.", ingredientSubstanceId);
                return;
            }

            // Check if this exact moiety link already exists to prevent duplicates
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existingMoiety = await dbContext.Set<ActiveMoiety>().FirstOrDefaultAsync(m =>
                m.IngredientSubstanceID == ingredientSubstanceId && m.MoietyUNII == moiety.MoietyUNII);

            if (existingMoiety != null)
            {
                context.Logger.LogDebug("ActiveMoiety link for UNII {UNII} already exists for IngredientSubstanceID {ID}.", moiety.MoietyUNII, ingredientSubstanceId);
                return;
            }

            // Create and save the new active moiety record
            var moietyRepo = context.GetRepository<ActiveMoiety>();
            await moietyRepo.CreateAsync(moiety);
            context.Logger.LogInformation("Created ActiveMoiety '{Name}' for IngredientSubstanceID {ID}", moiety.MoietyName, ingredientSubstanceId);
            #endregion
        }
    }
}