
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
        #region Ingredient & Substance Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Ingredient DTOs for the specified product with nested ingredient substance details.
        /// Constructs ingredient hierarchies including substance information and reference data.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find ingredients for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Ingredient DTOs with nested IngredientSubstance data.</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="Label.IngredientSourceProduct"/>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <seealso cref="IngredientSourceProductDto"/>
        /// <seealso cref="IngredientInstanceDto"/>"/>
        /// <seealso cref="SpecifiedSubstanceDto"/>
        /// <remarks>
        /// This method builds complete IngredientDto objects including nested substance and instance data.
        /// Returns an empty list if productId is null or no ingredients are found.
        /// Each Ingredient includes its associated IngredientSubstance and IngredientInstance collections.
        /// </remarks>
        /// <example>
        /// <code>
        /// var ingredients = await buildIngredientsAsync(dbContext, 123, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<IngredientDto>> buildIngredientsAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no productId provided
            if (productId == null)
                return new List<IngredientDto>();

            // Get all ingredients for this product with no change tracking for performance
            var ingredients = await db.Set<Label.Ingredient>()
                .AsNoTracking()
                .Where(i => i.ProductID == productId)
                .ToListAsync();

            var ingredientDtos = new List<IngredientDto>();

            // For each ingredient, build its substance details and instances
            foreach (var ingredient in ingredients)
            {
                // Skip null ingredients or ingredients without valid IDs
                if (ingredient == null || ingredient.IngredientID == null)
                    continue;

                // Build the associated IngredientSubstance (using correct FK: IngredientSubstanceID)
                var ingredientSubstance = await buildIngredientSubstanceAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger);

                var refSubstance = await buildReferenceSubstancesDtoAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger);

                // Build all IngredientInstances for this substance
                var ingredientInstances = await buildIngredientInstancesAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger
                );

                var sourceProducts = await buildIngredientSourceProductsDtoAsync(
                    db,
                    ingredient.IngredientID,
                    pkSecret,
                    logger);

                var specifiedSubstances = await buildSpecifiedSubstancesAsync(
                    db,
                    ingredient.SpecifiedSubstanceID,
                    pkSecret,
                    logger);

                // Set ReferenceSubstanceID on ingredient if reference substances exist
                if (refSubstance != null && refSubstance.Any())
                {
                    ingredient.ReferenceSubstanceID = refSubstance.FirstOrDefault()?.ReferenceSubstanceID;
                }

                // Assemble ingredient DTO with substance data and instances
                ingredientDtos.Add(new IngredientDto
                {
                    Ingredient = ingredient.ToEntityWithEncryptedId(pkSecret, logger),
                    IngredientInstances = ingredientInstances,
                    IngredientSubstance = ingredientSubstance,
                    IngredientSourceProducts = sourceProducts,
                    ReferenceSubstances = refSubstance ?? new List<ReferenceSubstanceDto>(),
                    SpecifiedSubstances = specifiedSubstances
                });
            }
            return ingredientDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores the specified substance code and name linked to an ingredient in 
        /// Biologic/Drug Substance Indexing documents. Retrieves specified 
        /// substance records for a specified ingredient ID and transforms them 
        /// into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="specifiedSubstanceID">The unique identifier of the ingredient to find specified substances for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of SpecifiedSubstanceDto objects representing the specified substances, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IngredientDto > SpecifiedSubstanceDto
        /// Specified substances contain substance codes and names linked to ingredients in Biologic/Drug Substance Indexing documents.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specifiedSubstances = await buildSpecifiedSubstancesAsync(dbContext, 321, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        /// <seealso cref="SpecifiedSubstanceDto"/>
        private static async Task<List<SpecifiedSubstanceDto>> buildSpecifiedSubstancesAsync(ApplicationDbContext db, int? specifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no ingredient ID provided
            if (specifiedSubstanceID == null)
                return new List<SpecifiedSubstanceDto>();

            // Query specified substances for the specified ingredient using read-only tracking
            var entity = await db.Set<Label.SpecifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SpecifiedSubstanceID == specifiedSubstanceID)
                .ToListAsync();

            // Return empty list if no specified substances found
            if (entity == null || !entity.Any())
                return new List<SpecifiedSubstanceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new SpecifiedSubstanceDto { SpecifiedSubstance = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<SpecifiedSubstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds an IngredientSubstance DTO for the specified ingredient substance ID.
        /// Retrieves detailed substance information including UNII codes and substance names.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="ingredientSubstanceId">The ingredient substance ID to retrieve.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>IngredientSubstance DTO with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <seealso cref="ActiveMoietyDto"/>
        /// <seealso cref="buildIngredientInstancesAsync"/>
        /// <seealso cref="buildActiveMoietiesDtoAsync"/>"/>
        /// <remarks>
        /// This method retrieves and transforms a single IngredientSubstance entity into a DTO.
        /// Returns null if ingredientSubstanceId is null or the entity is not found in the database.
        /// The returned DTO includes encrypted primary key for security.
        /// </remarks>
        /// <example>
        /// <code>
        /// var substance = await buildIngredientSubstanceAsync(dbContext, 789, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<IngredientSubstanceDto?> buildIngredientSubstanceAsync(ApplicationDbContext db, int? ingredientSubstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (ingredientSubstanceId == null)
                return null;

            // Query for the specific ingredient substance with no change tracking
            var entity = await db.Set<Label.IngredientSubstance>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IngredientSubstanceID == ingredientSubstanceId);

            // Return null if entity not found
            if (entity == null)
                return null;

            var substanceInstances = await buildIngredientInstancesDtoAsync(
                db,
                entity.IngredientSubstanceID,
                pkSecret,
                logger);

            var moieties = await buildActiveMoietiesDtoAsync(
                db,
                entity.IngredientSubstanceID,
                pkSecret,
                logger);

            // Transform entity to DTO with encrypted ID
            return new IngredientSubstanceDto
            {
                IngredientSubstance = entity.ToEntityWithEncryptedId(pkSecret, logger),
                IngredientInstances = substanceInstances,
                ActiveMoieties = moieties
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Uses the ingredient substance ID to find active moieties. Retrieves 
        /// active moiety records for a specified ingredient substance ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="ingredientSubstanceID">The unique identifier of the ingredient substance to find active moieties for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ActiveMoietyDto objects representing the active moieties, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IngredientSubstanceDto > ActiveMoietyDto
        /// Active moieties represent the therapeutically active portions of ingredient substances.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var activeMoieties = await buildActiveMoietiesDtoAsync(dbContext, 579, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <seealso cref="ActiveMoietyDto"/>
        private static async Task<List<ActiveMoietyDto>> buildActiveMoietiesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no ingredient substance ID provided
            if (ingredientSubstanceID == null)
                return new List<ActiveMoietyDto>();

            // Query active moieties for the specified ingredient substance using read-only tracking
            var entity = await db.Set<Label.ActiveMoiety>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return empty list if no active moieties found
            if (entity == null || !entity.Any())
                return new List<ActiveMoietyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ActiveMoietyDto { ActiveMoiety = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ActiveMoietyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IngredientInstanceDto for a given IngredientSubstanceID.
        /// Each includes its IngredientSubstance (as IngredientSubstanceDto), if available.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="ingredientSubstanceId">The ingredient substance ID to find instances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of IngredientInstanceDto with nested IngredientSubstanceDto where available, or null if not found.</returns>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <remarks>
        /// This method builds complete IngredientInstanceDto objects including nested IngredientSubstance data.
        /// Returns null if ingredientSubstanceId is null or no ingredient instances are found.
        /// Each IngredientInstance that has an associated IngredientSubstanceID will include the full IngredientSubstanceDto.
        /// The method processes all instances associated with the specified substance ID.
        /// </remarks>
        /// <example>
        /// <code>
        /// var instances = await buildIngredientInstancesAsync(dbContext, 456, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<IngredientInstanceDto>> buildIngredientInstancesAsync(ApplicationDbContext db, int? ingredientSubstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (ingredientSubstanceId == null)
                return new List<IngredientInstanceDto>();

            // Query all IngredientInstance rows for this substance with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.IngredientSubstanceID == ingredientSubstanceId)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Build IngredientInstanceDto objects with substance data
            List<IngredientInstanceDto> ingredientInstances = new List<IngredientInstanceDto>();

            foreach (var item in entity)
            {
                // Build the IngredientSubstanceDto for this instance (recursive call for substance details)
                var ingredientSubstance = await buildIngredientSubstanceAsync(
                    db,
                    item.IngredientSubstanceID,
                    pkSecret,
                    logger);

                // Create the IngredientInstanceDto with encrypted IDs
                ingredientInstances.Add(new IngredientInstanceDto
                {
                    IngredientInstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                });
            }

            return ingredientInstances;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds product ingredient instances for a specified product ID. Retrieves 
        /// ingredient instance records associated with a product and transforms 
        /// them into DTOs with encrypted identifiers and related lot identifier data.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find ingredient instances for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of IngredientInstanceDto objects representing the ingredient instances with their lot identifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method queries ingredient instances where FillLotInstanceID matches the provided product ID.
        /// Each ingredient instance is enriched with its associated lot identifier information.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var ingredientInstances = await buildProductIngredientInstancesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="buildIngredientSubstanceAsync"/>
        /// <seealso cref="buildLotIdentifierDtoAsync"/>
        private static async Task<List<IngredientInstanceDto>> buildProductIngredientInstancesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null)
                return new List<IngredientInstanceDto>();

            // Query all ingredient instance rows for this product with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.FillLotInstanceID == productID)
                .ToListAsync();

            // Return empty list if no ingredient instances found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Build IngredientInstanceDto objects with lot identifier data
            List<IngredientInstanceDto> ingredientInstances = new List<IngredientInstanceDto>();

            // Process each ingredient instance and build associated data
            foreach (var item in entity)
            {
                // Build the lot identifier data for this instance
                var lotIdentifier = await buildLotIdentifierDtoAsync(db, item.LotIdentifierID, pkSecret, logger);

                // Create the IngredientInstanceDto with encrypted IDs and lot identifier
                ingredientInstances.Add(new IngredientInstanceDto
                {
                    IngredientInstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    LotIdentifier = lotIdentifier
                });
            }

            // Return processed ingredient instances with lot identifier data, ensuring non-null result
            return ingredientInstances ?? new List<IngredientInstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ingredient instance DTOs for a specified ingredient substance ID.
        /// </summary>
        /// <param name="db">The database context for querying ingredient instance entities.</param>
        /// <param name="ingredientSubstanceID">The ingredient substance identifier to filter ingredient instances.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of IngredientInstanceDto objects, or null if no ingredient substance ID provided or no entities found.</returns>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        private static async Task<List<IngredientInstanceDto>> buildIngredientInstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return new List<IngredientInstanceDto>();

            // Query ingredient instances for the specified ingredient substance
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new IngredientInstanceDto { IngredientInstance = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<IngredientInstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ingredient source product DTOs for a specified ingredient ID.
        /// </summary>
        /// <param name="db">The database context for querying ingredient source product entities.</param>
        /// <param name="ingredientID">The ingredient identifier to filter ingredient source products.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of IngredientSourceProductDto objects, or null if no ingredient ID provided or no entities found.</returns>
        /// <seealso cref="Label.IngredientSourceProduct"/>
        /// <seealso cref="IngredientSourceProductDto"/>
        private static async Task<List<IngredientSourceProductDto>> buildIngredientSourceProductsDtoAsync(ApplicationDbContext db, int? ingredientID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient ID is provided
            if (ingredientID == null)
                return new List<IngredientSourceProductDto>();

            // Query ingredient source products for the specified ingredient
            var entity = await db.Set<Label.IngredientSourceProduct>()
                .AsNoTracking()
                .Where(e => e.IngredientID == ingredientID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientSourceProductDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new IngredientSourceProductDto { IngredientSourceProduct = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<IngredientSourceProductDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of reference substance DTOs for a specified ingredient substance ID.
        /// The reference substance is related to Ingredient via IngredientSubstanceID.
        /// </summary>
        /// <param name="db">The database context for querying reference substance entities.</param>
        /// <param name="ingredientSubstanceID">The ingredient substance identifier to filter reference substances.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of ReferenceSubstanceDto objects, or null if no ingredient substance ID provided or no entities found.</returns>
        /// <seealso cref="Label.ReferenceSubstance"/>
        /// <seealso cref="ReferenceSubstanceDto"/>
        private static async Task<List<ReferenceSubstanceDto>> buildReferenceSubstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return new List<ReferenceSubstanceDto>();

            // Query reference substances for the specified ingredient substance
            var entity = await db.Set<Label.ReferenceSubstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<ReferenceSubstanceDto>();

            // Transform entities to DTOs with encrypted IDs, filtering out null entities
            return entity.Where(e => e != null).Select(e => new ReferenceSubstanceDto
            {
                ReferenceSubstance = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<ReferenceSubstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IdentifiedSubstance DTOs for the specified section.
        /// Retrieves substance details such as active moieties and pharmacologic class identifiers used in indexing contexts.
        /// ENHANCED: Now includes chemical moiety data for substance structure information.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find identified substances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of IdentifiedSubstance DTOs with encrypted IDs including moiety data.</returns>
        /// <seealso cref="Label.IdentifiedSubstance"/>
        /// <seealso cref="Label.SubstanceSpecification"/>
        /// <seealso cref="Label.Moiety"/>
        /// <seealso cref="IdentifiedSubstanceDto"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        /// <seealso cref="MoietyDto"/>
        private static async Task<List<IdentifiedSubstanceDto>> buildIdentifiedSubstancesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<IdentifiedSubstanceDto>();

            // Query identified substances for the specified section
            var items = await db.Set<Label.IdentifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<IdentifiedSubstanceDto>();

            // For each identified substance, build its related entities
            // Transform entities to DTOs with encrypted IDs
            var dtos = new List<IdentifiedSubstanceDto>();
            foreach (var item in items)
            {
                // For each IdentifiedSubstance, build SubstanceSpecifications
                var specs = await buildSubstanceSpecificationsAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                var contributingFactors = await buildContributingFactorsAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                var pharmacologicClasses = await buildPharmacologicClassesAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                // ENHANCED: Build moieties for substance structure information
                var moieties = await buildMoietyAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                dtos.Add(new IdentifiedSubstanceDto
                {
                    IdentifiedSubstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    SubstanceSpecifications = specs,
                    ContributingFactors = contributingFactors,
                    PharmacologicClasses = pharmacologicClasses,
                    Moiety = moieties
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of moiety DTOs for the specified identified substance ID.
        /// Retrieves chemical moiety records that define the molecular structure and 
        /// quantity information for substance indexing contexts.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find moieties for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MoietyDto objects representing the chemical moieties, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > MoietyDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// Moieties contain molecular structure and quantity information per FDA Substance Registration System.
        /// </remarks>
        /// <example>
        /// <code>
        /// var moieties = await buildMoietyAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Moiety"/>
        /// <seealso cref="MoietyDto"/>
        private static async Task<List<MoietyDto>> buildMoietyAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<MoietyDto>();

            // Query moieties for the specified identified substance using read-only tracking
            var items = await db.Set<Label.Moiety>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync();

            // Return empty list if no moieties found
            if (items == null || !items.Any())
                return new List<MoietyDto>();

            // Transform entities to DTOs with encrypted IDs for security
            var dtos = new List<MoietyDto>();
            foreach (var item in items)
            {
                // Skip entities without valid IDs
                if (item.MoietyID == null)
                    continue;

                // ENHANCED: Build characteristics for this moiety
                var characteristics = await buildMoietyCharacteristicsAsync(
                    db,
                    item.MoietyID,
                    pkSecret,
                    logger);

                // Create moiety DTO with encrypted ID and characteristics
                dtos.Add(new MoietyDto
                {
                    Moiety = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Characteristics = characteristics // Add characteristics to the DTO
                });
            }

            // Ensure non-null result
            return dtos ?? new List<MoietyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of characteristic DTOs for a specified moiety ID.
        /// Retrieves chemical structure data including MOLFILE, InChI, and InChI-Key.
        /// </summary>
        private static async Task<List<CharacteristicDto>> buildMoietyCharacteristicsAsync(
            ApplicationDbContext db,
            int? moietyID,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return empty list if no moiety ID is provided
            if (moietyID == null)
                return new List<CharacteristicDto>();

            // Query characteristics for the specified moiety
            var entities = await db.Set<Label.Characteristic>()
                .AsNoTracking()
                .Where(e => e.MoietyID == moietyID)
                .ToListAsync();

            var dtos = new List<CharacteristicDto>();

            // Process each characteristic
            foreach (var item in entities)
            {
                // Create characteristic DTO
                dtos.Add(new CharacteristicDto
                {
                    Characteristic = item.ToEntityWithEncryptedId(pkSecret, logger)
                    // Note: For moiety characteristics, PackagingLevels would be empty
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of contributing factors for the specified factor substance ID 
        /// where FactorSubstanceID equals IdentifiedSubstanceID. Retrieves contributing 
        /// factor records associated with an identified substance and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find contributing factors for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ContributingFactorDto objects representing the contributing factors, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > ContributingFactorDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var contributingFactors = await buildContributingFactorsAsync(dbContext, 456, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ContributingFactor"/>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="ContributingFactorDto"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<ContributingFactorDto>> buildContributingFactorsAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<ContributingFactorDto>();

            var dtos = new List<ContributingFactorDto>();

            // Query contributing factors for the specified identified substance using read-only tracking
            var entity = await db.Set<Label.ContributingFactor>()
                .AsNoTracking()
                .Where(e => e.FactorSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            // Return empty list if no contributing factors found
            if (entity == null || !entity.Any())
                return new List<ContributingFactorDto>();

            foreach (var e in entity)
            {
                // Skip entities without valid IDs
                if (e.ContributingFactorID == null)
                    continue;

                var interactions = await buildContributingFactorInteractionConsequencesAsync(db, e.InteractionIssueID, pkSecret, logger);

                // Create contributing factor DTO with encrypted ID
                dtos.Add(new ContributingFactorDto
                {
                    ContributingFactor = e.ToEntityWithEncryptedId(pkSecret, logger),
                    InteractionConsequences = interactions
                });
            }

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return dtos ?? new List<ContributingFactorDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic classes for the IdentifiedSubstance.
        /// Handles both ActiveMoiety indexing (via PharmacologicClassLink) and 
        /// PharmacologicClass definitions (direct IdentifiedSubstanceID relationship).
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find pharmacologic classes for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassDto objects representing the pharmacologic classes with their associated data, or an empty list if none found</returns>
        private static async Task<List<PharmacologicClassDto>> buildPharmacologicClassesAsync(ApplicationDbContext db,
            int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<PharmacologicClassDto>();

            var dtos = new List<PharmacologicClassDto>();

            // CASE 1: PharmacologicClass Definitions (Section 8.2.3)
            // Direct relationship via IdentifiedSubstanceID
            var definitionClasses = await db.Set<Label.PharmacologicClass>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            // CASE 2: ActiveMoiety Indexing (Section 8.2.2) 
            // Relationship via PharmacologicClassLink table
            var linkedClassIds = await db.Set<Label.PharmacologicClassLink>()
                .AsNoTracking()
                .Where(link => link.ActiveMoietySubstanceID == identifiedSubstanceID)
                .Select(link => link.PharmacologicClassID)
                .ToListAsync();

            var linkedClasses = new List<Label.PharmacologicClass>();
            if (linkedClassIds.Any())
            {
                linkedClasses = await db.Set<Label.PharmacologicClass>()
                    .AsNoTracking()
                    .Where(pc => linkedClassIds.Contains(pc.PharmacologicClassID))
                    .ToListAsync();
            }

            // Combine both types of relationships
            var allClasses = definitionClasses.Concat(linkedClasses).Distinct().ToList();

            logger.LogInformation("Found {DefinitionCount} definition classes and {LinkedCount} linked classes for substance {SubstanceId}",
                definitionClasses.Count, linkedClasses.Count, identifiedSubstanceID);

            // Process each pharmacologic class and build associated data
            foreach (var pharmClass in allClasses)
            {
                // Skip entities without valid IDs
                if (pharmClass.PharmacologicClassID == null)
                    continue;

                // Build the pharmacologic class names, links, and hierarchies for this class
                var pharmacologicClassNames = await buildPharmacologicClassNamesAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);
                var pharmLinks = await buildPharmacologicClassLinksAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);
                var pharmHierarchies = await buildPharmacologicClassHierarchiesAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);

                // Create pharmacologic class DTO with encrypted ID and associated data
                dtos.Add(new PharmacologicClassDto
                {
                    PharmacologicClass = pharmClass.ToEntityWithEncryptedId(pkSecret, logger),
                    PharmacologicClassNames = pharmacologicClassNames,
                    PharmacologicClassLinks = pharmLinks,
                    PharmacologicClassHierarchies = pharmHierarchies
                });
            }

            logger.LogInformation("Built {Count} PharmacologicClassDto objects for substance {SubstanceId}", dtos.Count, identifiedSubstanceID);

            // Return processed pharmacologic classes with associated data, ensuring non-null result
            return dtos ?? new List<PharmacologicClassDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class names for the pharmacologic classes.
        /// Retrieves pharmacologic class name records for a specified pharmacologic 
        /// class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the pharmacologic class to find names for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassNameDto objects representing the pharmacologic class names, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassNameDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classNames = await buildPharmacologicClassNamesAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassName"/>
        /// <seealso cref="PharmacologicClassNameDto"/>
        private static async Task<List<PharmacologicClassNameDto>> buildPharmacologicClassNamesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassNameDto>();

            // Query pharmacologic class names for the specified pharmacologic class using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassName>()
                .AsNoTracking()
                .Where(e => e.PharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class names found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassNameDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassNameDto { PharmacologicClassName = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassNameDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class links for the pharmacologic classes. 
        /// Retrieves pharmacologic class link records for a specified pharmacologic 
        /// class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the pharmacologic class to find links for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassLinkDto objects representing the pharmacologic class links, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassLinkDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classLinks = await buildPharmacologicClassLinksAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassLink"/>
        /// <seealso cref="PharmacologicClassLinkDto"/>
        private static async Task<List<PharmacologicClassLinkDto>> buildPharmacologicClassLinksAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassLinkDto>();

            // Query pharmacologic class links for the specified pharmacologic class using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassLink>()
                .AsNoTracking()
                .Where(e => e.PharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class links found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassLinkDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassLinkDto { PharmacologicClassLink = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassLinkDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class hierarchies for the pharmacologic 
        /// classes where ChildPharmacologicClassID equals pharmacologicClassID.
        /// Retrieves pharmacologic class hierarchy records for a specified 
        /// pharmacologic class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the child pharmacologic class to find hierarchies for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassHierarchyDto objects representing the pharmacologic class hierarchies, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassHierarchyDto
        /// The query filters by ChildPharmacologicClassID to find parent hierarchies for the specified class.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classHierarchies = await buildPharmacologicClassHierarchiesAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassHierarchy"/>
        /// <seealso cref="PharmacologicClassHierarchyDto"/>
        private static async Task<List<PharmacologicClassHierarchyDto>> buildPharmacologicClassHierarchiesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassHierarchyDto>();

            // Query pharmacologic class hierarchies where this class is the child using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildPharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class hierarchies found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassHierarchyDto { PharmacologicClassHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassHierarchyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of consequences for contributing factors. Retrieves interaction 
        /// consequence records for a specified interaction issue ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="interactionIssueID">The unique identifier of the interaction issue to find consequences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of InteractionConsequenceDto objects representing the interaction consequences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > ContributingFactorDto > InteractionConsequenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var interactionConsequences = await buildContributingFactorInteractionConsequencesAsync(dbContext, 654, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<InteractionConsequenceDto>> buildContributingFactorInteractionConsequencesAsync(ApplicationDbContext db, int? interactionIssueID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no interaction issue ID provided
            if (interactionIssueID == null)
                return new List<InteractionConsequenceDto>();

            // Query interaction consequences for the specified interaction issue using read-only tracking
            var entity = await db.Set<Label.InteractionConsequence>()
                .AsNoTracking()
                .Where(e => e.InteractionIssueID == interactionIssueID)
                .ToListAsync();

            // Return empty list if no interaction consequences found
            if (entity == null || !entity.Any())
                return new List<InteractionConsequenceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new InteractionConsequenceDto { InteractionConsequence = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<InteractionConsequenceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of SubstanceSpecification DTOs for the specified IdentifiedSubstance.
        /// Retrieves detailed substance specifications including analyte information and chemical properties.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="identifiedSubstanceID">The IdentifiedSubstance ID to find specifications for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of SubstanceSpecification DTOs with nested Analyte data and encrypted IDs.</returns>
        /// <seealso cref="Label.SubstanceSpecification"/>
        /// <seealso cref="Label.Analyte"/>
        /// <seealso cref="Label.ObservationCriterion"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        /// <seealso cref="AnalyteDto"/>
        /// <seealso cref="ObservationCriterionDto"/>
        /// <remarks>
        /// This method builds complete SubstanceSpecificationDto objects including nested Analyte data.
        /// Returns an empty list if identifiedSubstanceID is null or no specifications are found.
        /// Each SubstanceSpecification includes its associated Analytes collection with encrypted IDs.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specs = await buildSubstanceSpecificationsAsync(dbContext, 456, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<SubstanceSpecificationDto>> buildSubstanceSpecificationsAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no identifiedSubstanceID provided
            if (identifiedSubstanceID == null) return new List<SubstanceSpecificationDto>();

            // Query substance specifications for the specified IdentifiedSubstance with no change tracking
            var items = await db.Set<Label.SubstanceSpecification>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<SubstanceSpecificationDto>();

            var dtos = new List<SubstanceSpecificationDto>();

            // For each substance specification, build its analytes
            foreach (var item in items)
            {
                // For each SubstanceSpecification, build associated Analytes
                var analytes = await buildAnalytesAsync(db, item.SubstanceSpecificationID, pkSecret, logger);

                var observationCriteria = await buildObservationCriterionAsync(db, item.SubstanceSpecificationID, pkSecret, logger);

                // Create SubstanceSpecificationDto with encrypted IDs and nested analytes
                dtos.Add(new SubstanceSpecificationDto
                {
                    SubstanceSpecification = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Analytes = analytes,
                    ObservationCriteria = observationCriteria
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Analyte DTOs for the specified SubstanceSpecification.
        /// This is a junction between SubstanceSpecification and its IdentifiedSubstances.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="substanceSpecificationID">The SubstanceSpecification ID to find analytes for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Analyte DTOs with encrypted IDs, empty list if none found.</returns>
        /// <seealso cref="Label.Analyte"/>
        /// <seealso cref="AnalyteDto"/>
        /// <remarks>
        /// This method builds Analyte DTOs with encrypted primary keys for security.
        /// Returns an empty list if substanceSpecificationID is null or no analytes are found.
        /// Uses LINQ Select for efficient transformation of entities to DTOs.
        /// </remarks>
        /// <example>
        /// <code>
        /// var analytes = await buildAnalytesAsync(dbContext, 789, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<AnalyteDto>> buildAnalytesAsync(ApplicationDbContext db, int? substanceSpecificationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no substanceSpecificationID provided
            if (substanceSpecificationID == null) return new List<AnalyteDto>();

            // Query analytes for the specified SubstanceSpecification with no change tracking
            var items = await db.Set<Label.Analyte>()
                .AsNoTracking()
                .Where(e => e.AnalyteSubstanceID == substanceSpecificationID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<AnalyteDto>();

            // Transform entities to DTOs with encrypted IDs using LINQ Select for efficiency
            return items
                .Select(item => new AnalyteDto { Analyte = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ObservationCriterion DTOs for the specified SubstanceSpecification.
        /// Retrieves observation criterion records for a specified substance specification ID 
        /// and enriches them with application types and commodities, then transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="substanceSpecificationID">The unique identifier of the substance specification to find observation criteria for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ObservationCriterionDto objects representing the observation criteria with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto
        /// Each observation criterion is enriched with its application types and commodities.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var observationCriteria = await buildObservationCriterionAsync(dbContext, 987, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ObservationCriterion"/>
        /// <seealso cref="ObservationCriterionDto"/>
        /// <seealso cref="buildApplicationTypesAsync"/>
        /// <seealso cref="buildCommoditiesAsync"/>
        private static async Task<List<ObservationCriterionDto>> buildObservationCriterionAsync(ApplicationDbContext db, int? substanceSpecificationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no substance specification ID provided
            if (substanceSpecificationID == null)
                return new List<ObservationCriterionDto>();

            var dtos = new List<ObservationCriterionDto>();

            // Query observation criteria for the specified substance specification using read-only tracking
            var entity = await db.Set<Label.ObservationCriterion>()
                .AsNoTracking()
                .Where(e => e.SubstanceSpecificationID == substanceSpecificationID)
                .ToListAsync();

            // Return empty list if no observation criteria found
            if (entity == null || !entity.Any())
                return new List<ObservationCriterionDto>();

            // Process each observation criterion and build associated data
            foreach (var e in entity)
            {
                // Skip entities without valid substance specification IDs
                if (e.SubstanceSpecificationID == null)
                    continue;

                // Build the application types and commodities for this observation criterion
                var applicationTypes = await buildApplicationTypesAsync(db, e.ApplicationTypeID, pkSecret, logger);
                var commodities = await buildCommoditiesAsync(db, e.CommodityID, pkSecret, logger);

                // Create observation criterion DTO with encrypted ID and associated data
                dtos.Add(new ObservationCriterionDto
                {
                    ObservationCriterion = e.ToEntityWithEncryptedId(pkSecret, logger),
                    ApplicationTypes = applicationTypes,
                    Commodities = commodities
                });
            }

            // Return processed observation criteria with associated data, ensuring non-null result
            return dtos ?? new List<ObservationCriterionDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Get the application type IDs from the ObservationCriterion. Retrieves 
        /// application type records for a specified application type ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="applicationTypeID">The unique identifier of the application type to retrieve. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ApplicationTypeDto objects representing the application types, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto > ApplicationTypeDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var applicationTypes = await buildApplicationTypesAsync(dbContext, 147, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ApplicationType"/>
        /// <seealso cref="ApplicationTypeDto"/>
        private static async Task<List<ApplicationTypeDto>> buildApplicationTypesAsync(ApplicationDbContext db, int? applicationTypeID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no application type ID provided
            if (applicationTypeID == null)
                return new List<ApplicationTypeDto>();

            // Query application types for the specified application type ID using read-only tracking
            var entity = await db.Set<Label.ApplicationType>()
                .AsNoTracking()
                .Where(e => e.ApplicationTypeID == applicationTypeID)
                .ToListAsync();

            // Return empty list if no application types found
            if (entity == null || !entity.Any())
                return new List<ApplicationTypeDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ApplicationTypeDto { ApplicationType = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ApplicationTypeDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the commodities for the specified commodity ID. Retrieves 
        /// commodity records for a specified commodity ID and transforms them 
        /// into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="commodityId">The unique identifier of the commodity to retrieve. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of CommodityDto objects representing the commodities, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto > CommodityDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var commodities = await buildCommoditiesAsync(dbContext, 258, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Commodity"/>
        /// <seealso cref="CommodityDto"/>
        private static async Task<List<CommodityDto>> buildCommoditiesAsync(ApplicationDbContext db, int? commodityId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no commodity ID provided
            if (commodityId == null)
                return new List<CommodityDto>();

            // Query commodities for the specified commodity ID using read-only tracking
            var entity = await db.Set<Label.Commodity>()
                .AsNoTracking()
                .Where(e => e.CommodityID == commodityId)
                .ToListAsync();

            // Return empty list if no commodities found
            if (entity == null || !entity.Any())
                return new List<CommodityDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new CommodityDto { Commodity = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<CommodityDto>();
            #endregion
        }

        #endregion
    }
}