using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MedRecPro.Helpers;
using MedRecPro.Data;
using MedRecPro.DataModels;


namespace MedRecPro.DataAccess
{
    /// <summary>
    /// A generic repository for performing basic CRUD operations on database entities
    /// corresponding to the data models defined in MedRecPro.DataModels.
    /// It uses Entity Framework Core for data access.
    /// Assumes primary keys follow the pattern [ClassName]ID or [ClassName]Id for reflection-based PK retrieval,
    /// but primarily relies on EF Core's conventions and configurations for PK handling.
    /// </summary>
    /// <typeparam name="T">The type of the data model class (e.g., Document, Organization), which must be an EF Core entity.</typeparam>
    public class Repository<T> where T : class
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly string _tableName; // Retained for informational purposes, EF Core handles mapping
        private readonly string _primaryKeyName;
        private readonly PropertyInfo? _primaryKeyProperty;
        private readonly StringCipher _stringCipher;
        private readonly ILogger<T> _logger;
        private readonly IConfiguration _configuration;
        private string _encryptionKey;


        // Static lock for thread-safe
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the Repository class.
        /// </summary>
        /// <param name="context">The Entity Framework ApplicationDbContext instance.</param>
        /// <param name="configuration">Configuration settings for the application, used to retrieve encryption keys.</param>
        /// <param name="logger">Logger instance for logging operations and errors.</param>
        /// <param name="stringCipher">StringCipher instance for encrypting and decrypting sensitive data.</param>
        /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key property cannot be found on type T using expected conventions.</exception>
        public Repository(ApplicationDbContext context,
            StringCipher stringCipher,
            ILogger<T> logger,
            IConfiguration configuration)
        {
            #region implementation
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _encryptionKey = getPkSecret() ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing."); ;

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "DbContext cannot be null.");
            }
            _context = context;
            _dbSet = _context.Set<T>();
            _tableName = typeof(T).Name; // EF Core might use a different table name based on conventions/configuration

            // Try to find the primary key property using reflection for
            // methods that might need it (e.g., getting PK value from an entity instance)
            string tentativePkName = $"{_tableName}ID";

            _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);

            if (_primaryKeyProperty == null)
            {
                tentativePkName = $"{_tableName}Id"; // Common variation

                _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);
            }

            if (_primaryKeyProperty == null)
            {
                // Find PKs named just "Id" by convention
                tentativePkName = "Id";

                _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);
            }

            if (_primaryKeyProperty == null)
            {
                // If we still can't find it, try to find the primary key column using EF Core metadata
                string? keyColumn = getPrimaryKeyColumn(_context, _tableName);

                if (!string.IsNullOrWhiteSpace(keyColumn))
                {
                    // If we found a primary key column name, try to get the property by that name
                    _primaryKeyProperty = typeof(T).GetProperty(keyColumn);
                }
            }

            if (_primaryKeyProperty == null)
            {
                // This reflection-based PK discovery is a fallback or for specific utility.
                // EF Core's primary operations (FindAsync, Add, Update, Remove) use its own metadata.
                // However, if we cannot find it by common conventions, some helper methods might fail.
                throw new InvalidOperationException($"Could not find a primary key property (e.g., '{typeof(T).Name}ID', '{typeof(T).Name}Id', or 'Id') on type {typeof(T).Name} using reflection. " +
                    "Ensure the entity has a primary key discoverable by EF Core and/or matching these " +
                    "conventions if used by repository helper methods.");
            }

            _primaryKeyName = _primaryKeyProperty.Name;
            #endregion
        }

        #region Private
        /**************************************/
        /// <summary>
        /// Retrieves the primary key column name for a given table in the database context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static string? getPrimaryKeyColumn(DbContext context, string tableName)
        {
            #region implementation
            // Find the entity type mapped to the given table name (case-insensitive)
            var entityType = context.Model.GetEntityTypes()
                .FirstOrDefault(et =>
                    et.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (entityType == null)
                return null; // Table not mapped

            var primaryKey = entityType.FindPrimaryKey();

            if (primaryKey == null)
                return null; // No primary key defined

            // Return the property (column) names of the primary key
            return primaryKey.Properties
                .Select(p => p.GetColumnName(StoreObjectIdentifier.Table(tableName, entityType.GetSchema())))
                .FirstOrDefault(); 
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Retrieves the Primary Key encryption secret from configuration.
        /// </summary>
        /// <returns>The encryption secret string.</returns>
        /// <exception cref="InvalidOperationException">Thrown if configuration is not set or the secret is missing.</exception>
        private string getPkSecret()
        {
            #region Implementation
            if (_encryptionKey == null)
            {
                lock (_lock)
                {
                    if (_encryptionKey == null)
                    {
                        string? secret = _configuration.GetSection("Security:DB:PKSecret").Value;

                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            _logger.LogCritical("Required configuration key 'Security:DB:PKSecret' is missing or empty.");

                            throw new InvalidOperationException("Required configuration key 'Security:DB:PKSecret' is missing or empty.");
                        }
                        _encryptionKey = secret;
                    }
                }
            }
            return _encryptionKey;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to decrypt a user ID string.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID string.</param>
        /// <param name="parameterName">Name of the parameter being decrypted (for logging).</param>
        /// <param name="decryptedId">The decrypted long ID, if successful.</param>
        /// <returns>True if decryption and parsing were successful and ID is positive; false otherwise.</returns>
        private bool tryDecryptId(string? encryptedId, string parameterName, out long decryptedId)
        {
            #region Implementation
            decryptedId = 0;
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                _logger.LogWarning("{ParameterName} is null or whitespace.", parameterName);
                return false;
            }

            try
            {
                string decryptedString = new StringCipher().Decrypt(encryptedId, getPkSecret());
                if (long.TryParse(decryptedString, out long id) && id > 0)
                {
                    decryptedId = id;
                    return true;
                }
                _logger.LogWarning("Invalid or non-positive ID after decrypting {ParameterName}. Encrypted value: {EncryptedValue}, Decrypted string: {DecryptedString}", parameterName, encryptedId, decryptedString);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting {ParameterName}. Encrypted value: {EncryptedValue}", parameterName, encryptedId);
                return false;
            }
            #endregion
        }

        #endregion

        /**************************************/
        /// <summary>
        /// Creates a new record in the database corresponding to the provided entity.
        /// The primary key is assumed to be database-generated and will be populated on the entity.
        /// </summary>
        /// <param name="entity">The entity object to insert.</param>
        /// <returns>The Encrypted ID of the newly created record.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved after creation.</exception>
        public virtual async Task<string?> CreateAsync(T entity)
        {
            #region implementation

            string? ret = null;

            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (_primaryKeyProperty == null)
            {
                throw new InvalidOperationException($"Primary key property NULL on type {typeof(T).Name}. Cannot create entity without a primary key.");
            }

            await _dbSet.AddAsync(entity);

            await _context.SaveChangesAsync();

            // EF Core populates the PK on the entity object after SaveChangesAsync.
            // Retrieve it using the discovered primary key property.
            var pkValue = _primaryKeyProperty.GetValue(entity);

            if (pkValue == null)
            {
                _logger.LogError("Primary key '{PrimaryKeyName}' on type {EntityType} was not populated after creation.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Primary key '{_primaryKeyName}' on type {typeof(T).Name} was not populated after creation.");
            }

            ret = pkValue?.ToString()?.Encrypt(_encryptionKey);

            if (string.IsNullOrWhiteSpace(ret))
            {
                _logger.LogError("Encryption of primary key '{PrimaryKeyName}' on type {EntityType} resulted in null or empty string.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Encryption of primary key '{_primaryKeyName}' on type {typeof(T).Name} resulted in null or empty string.");
            }

            return ret;

            #endregion
        }

        /**************************************/
        /// <summary>
        /// Reads a single record from the database based on its encrypted primary key ID.
        /// </summary>
        /// <param name="encryptedId">The encrypted primary key ID of the record to retrieve.</param>
        /// <returns>The entity object if found; otherwise, null.</returns>
        public virtual async Task<T?> ReadByIdAsync(string encryptedId)
        {
            #region implementation

            long decryptedId;

            if (!tryDecryptId(encryptedId, nameof(encryptedId), out decryptedId))
            {
                _logger.LogWarning("Failed to decrypt ID for ReadByIdAsync. Encrypted ID: {EncryptedId}", encryptedId);

                return null;
            }

            return await _dbSet.FindAsync(Convert.ToInt32(decryptedId));
            #endregion
        }

        /**************************************/
        ///<summary>
        /// Reads a paged set of records from the database table corresponding to type T.
        /// If either pageNumber or pageSize is null, all records are returned.
        /// </summary>
        /// <param name="pageNumber">Page index (1 = first page). If null, returns all records.</param>
        /// <param name="pageSize">Number of records per page. If null, returns all records.</param>
        /// <returns>An enumerable collection of entity objects for the specified page, or all entities if paging is not specified.</returns>
        public virtual async Task<IEnumerable<T>> ReadAllAsync(int? pageNumber, int? pageSize)
        {
            #region implementation
            IQueryable<T> query = _dbSet;

            if (pageNumber.HasValue 
                && pageSize.HasValue)
            {
                if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
                if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

                query = query
                    .Skip((pageNumber.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }

            return await query.ToListAsync();
            #endregion
        }

       /**************************************/
        /// <summary>
        /// Reads a paged set of "complete label" data, structured hierarchically starting from the Document entity.
        /// This method is specialized and will only execute if the repository's type T is Label.Document.
        /// It manually constructs the object graph since the POCOs lack navigation properties.
        /// </summary>
        /// <param name="pageNumber">Optional. The 1-based page number to retrieve.</param>
        /// <param name="pageSize">Optional. The number of records per page.</param>
        /// <returns>A list of dictionaries, where each dictionary represents a complete, hierarchical Document label.</returns>
        /// <exception cref="NotSupportedException">Thrown if this method is called 
        /// on a repository other than Repository&gt;Label.Document&lt;.
        /// </exception>
        /// <seealso cref="Label"/>
        /// <seealso cref="Label.Document"/>
        public virtual async Task<IEnumerable<Dictionary<string, object?>>> ReadAllCompleteLabelsAsync(int? pageNumber, int? pageSize)
        {
            #region Pre-computation and Validation
            if (typeof(T) != typeof(Label.Document))
            {
                throw new NotSupportedException($"ReadAllCompleteLabelsAsync is only supported on Repository<MedRecPro.DataModels.Label.Document>, not on Repository<{typeof(T).Name}>.");
            }

            var documentsDbSet = _dbSet as DbSet<Label.Document> ?? throw new InvalidOperationException("Internal error: DbSet could not be cast to DbSet<Label.Document>.");

            // 1. FETCH ROOT DOCUMENTS
            IQueryable<Label.Document> query = documentsDbSet.AsNoTracking();

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
                if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

                query = query.OrderBy(d => d.DocumentID).Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value);
            }

            var documents = await query.ToListAsync();
            if (!documents.Any())
            {
                return Enumerable.Empty<Dictionary<string, object?>>();
            }

            var documentIds = documents.Select(d => d.DocumentID).ToList();
            #endregion

            #region Batch Fetch All Related Data
            // This section performs all database queries upfront based on the collected IDs.

            // Level 1 Children (of Document)
            var documentAuthors = await _context.Set<Label.DocumentAuthor>().AsNoTracking().Where(e => documentIds.Contains(e.DocumentID)).ToListAsync();
            var documentRelationships = await _context.Set<Label.DocumentRelationship>().AsNoTracking().Where(e => documentIds.Contains(e.DocumentID)).ToListAsync();
            var legalAuthenticators = await _context.Set<Label.LegalAuthenticator>().AsNoTracking().Where(e => documentIds.Contains(e.DocumentID)).ToListAsync();
            var relatedDocuments = await _context.Set<Label.RelatedDocument>().AsNoTracking().Where(e => documentIds.Contains(e.SourceDocumentID)).ToListAsync();
            var structuredBodies = await _context.Set<Label.StructuredBody>().AsNoTracking().Where(e => documentIds.Contains(e.DocumentID)).ToListAsync();

            // Level 2 Children
            var documentRelationshipIds = documentRelationships.Select(e => e.DocumentRelationshipID).ToList();
            var structuredBodyIds = structuredBodies.Select(e => e.StructuredBodyID).ToList();
            var businessOperations = await _context.Set<Label.BusinessOperation>().AsNoTracking().Where(e => documentRelationshipIds.Contains(e.DocumentRelationshipID)).ToListAsync();
            var sections = await _context.Set<Label.Section>().AsNoTracking().Where(e => structuredBodyIds.Contains(e.StructuredBodyID)).ToListAsync();

            // Level 3 Children
            var businessOperationIds = businessOperations.Select(e => e.BusinessOperationID).ToList();
            var sectionIds = sections.Select(e => e.SectionID).ToList();
            var businessOperationQualifiers = await _context.Set<Label.BusinessOperationQualifier>().AsNoTracking().Where(e => businessOperationIds.Contains(e.BusinessOperationID)).ToListAsync();
            var businessOperationProductLinks = await _context.Set<Label.BusinessOperationProductLink>().AsNoTracking().Where(e => businessOperationIds.Contains(e.BusinessOperationID)).ToListAsync();
            var licenses = await _context.Set<Label.License>().AsNoTracking().Where(e => businessOperationIds.Contains(e.BusinessOperationID)).ToListAsync();
            var sectionHierarchies = await _context.Set<Label.SectionHierarchy>().AsNoTracking().Where(e => sectionIds.Contains(e.ParentSectionID) || sectionIds.Contains(e.ChildSectionID)).ToListAsync();
            var sectionTextContents = await _context.Set<Label.SectionTextContent>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var observationMedia = await _context.Set<Label.ObservationMedia>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var sectionExcerptHighlights = await _context.Set<Label.SectionExcerptHighlight>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var products = await _context.Set<Label.Product>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var identifiedSubstances = await _context.Set<Label.IdentifiedSubstance>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var productConcepts = await _context.Set<Label.ProductConcept>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var interactionIssues = await _context.Set<Label.InteractionIssue>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            // Other direct children of Section...
            var billingUnitIndexes = await _context.Set<Label.BillingUnitIndex>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var warningLetterProductInfos = await _context.Set<Label.WarningLetterProductInfo>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var warningLetterDates = await _context.Set<Label.WarningLetterDate>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var protocols = await _context.Set<Label.Protocol>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var remsMaterials = await _context.Set<Label.REMSMaterial>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var remsElectronicResources = await _context.Set<Label.REMSElectronicResource>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var complianceActions = await _context.Set<Label.ComplianceAction>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();
            var nctLinks = await _context.Set<Label.NCTLink>().AsNoTracking().Where(e => sectionIds.Contains(e.SectionID)).ToListAsync();

            // Level 4 Children
            var licenseIds = licenses.Select(e => e.LicenseID).ToList();
            var sectionTextContentIds = sectionTextContents.Select(e => e.SectionTextContentID).ToList();
            var productIds = products.Select(e => e.ProductID).ToList();
            var productConceptIds = productConcepts.Select(e => e.ProductConceptID).ToList();
            var identifiedSubstanceIds = identifiedSubstances.Select(e => e.IdentifiedSubstanceID).ToList();
            var interactionIssueIds = interactionIssues.Select(e => e.InteractionIssueID).ToList();
            var protocolIds = protocols.Select(e => e.ProtocolID).ToList();
            var remsMaterialIds = remsMaterials.Select(e => e.REMSMaterialID).ToList();
            var disciplinaryActions = await _context.Set<Label.DisciplinaryAction>().AsNoTracking().Where(e => licenseIds.Contains(e.LicenseID)).ToListAsync();
            var textLists = await _context.Set<Label.TextList>().AsNoTracking().Where(e => sectionTextContentIds.Contains(e.SectionTextContentID)).ToListAsync();
            var textTables = await _context.Set<Label.TextTable>().AsNoTracking().Where(e => sectionTextContentIds.Contains(e.SectionTextContentID)).ToListAsync();
            var renderedMedia = await _context.Set<Label.RenderedMedia>().AsNoTracking().Where(e => sectionTextContentIds.Contains(e.SectionTextContentID)).ToListAsync();
            var productIdentifiers = await _context.Set<Label.ProductIdentifier>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync();
            var genericMedicines = await _context.Set<Label.GenericMedicine>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync();
            var ingredients = await _context.Set<Label.Ingredient>().AsNoTracking().Where(e => productIds.Contains(e.ProductID) || productConceptIds.Contains(e.ProductConceptID)).ToListAsync();

            var packagingLevels = await _context.Set<Label.PackagingLevel>().AsNoTracking().Where(e => productIds.Contains(e.ProductID) || productIds.Contains(e.PartProductID)).ToListAsync();
            var marketingCategories = await _context.Set<Label.MarketingCategory>().AsNoTracking().Where(e => productIds.Contains(e.ProductID) || productConceptIds.Contains(e.ProductConceptID)).ToListAsync();
            var marketingStatuses = await _context.Set<Label.MarketingStatus>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync(); // Note: also links to PackagingLevel
            var characteristics = await _context.Set<Label.Characteristic>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync(); // Note: also links to PackagingLevel
            var productParts = await _context.Set<Label.ProductPart>().AsNoTracking().Where(e => productIds.Contains(e.KitProductID)).ToListAsync();
            var policies = await _context.Set<Label.Policy>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync();
            var productRoutes = await _context.Set<Label.ProductRouteOfAdministration>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync();
            var productWebLinks = await _context.Set<Label.ProductWebLink>().AsNoTracking().Where(e => productIds.Contains(e.ProductID)).ToListAsync();
            var pharmacologicClasses = await _context.Set<Label.PharmacologicClass>().AsNoTracking().Where(e => identifiedSubstanceIds.Contains(e.IdentifiedSubstanceID)).ToListAsync();
            var substanceSpecifications = await _context.Set<Label.SubstanceSpecification>().AsNoTracking().Where(e => identifiedSubstanceIds.Contains(e.IdentifiedSubstanceID)).ToListAsync();
            var contributingFactors = await _context.Set<Label.ContributingFactor>().AsNoTracking().Where(e => interactionIssueIds.Contains(e.InteractionIssueID)).ToListAsync();
            var interactionConsequences = await _context.Set<Label.InteractionConsequence>().AsNoTracking().Where(e => interactionIssueIds.Contains(e.InteractionIssueID)).ToListAsync();
            var requirements = await _context.Set<Label.Requirement>().AsNoTracking().Where(e => protocolIds.Contains(e.ProtocolID)).ToListAsync();
            var remsApprovals = await _context.Set<Label.REMSApproval>().AsNoTracking().Where(e => protocolIds.Contains(e.ProtocolID)).ToListAsync();

            // Level 5 Children
            var disciplinaryActionIds = disciplinaryActions.Select(e => e.DisciplinaryActionID).ToList();
            var textListIds = textLists.Select(e => e.TextListID).ToList();
            var textTableIds = textTables.Select(e => e.TextTableID).ToList();
            var ingredientIds = ingredients.Select(e => e.IngredientID).ToList();
            var ingredientSubstanceIdsFromIngredients = ingredients.Select(e => e.IngredientSubstanceID).ToList();

            var packagingLevelIds = packagingLevels.Select(e => e.PackagingLevelID).ToList();
            var marketingCategoryIds = marketingCategories.Select(e => e.MarketingCategoryID).ToList();
            var pharmacologicClassIds = pharmacologicClasses.Select(e => e.PharmacologicClassID).ToList();
            var substanceSpecificationIds = substanceSpecifications.Select(e => e.SubstanceSpecificationID).ToList();

            var attachedDocsForDisciplinary = await _context.Set<Label.AttachedDocument>().AsNoTracking().Where(e => e.ParentEntityType == "DisciplinaryAction" && disciplinaryActionIds.Contains(e.ParentEntityID)).ToListAsync();

            var attachedDocsForRems = await _context.Set<Label.AttachedDocument>().AsNoTracking().Where(e => e.ParentEntityType == "REMSMaterial" && remsMaterialIds.Contains(e.ParentEntityID)).ToListAsync();
            var textListItems = await _context.Set<Label.TextListItem>().AsNoTracking().Where(e => textListIds.Contains(e.TextListID)).ToListAsync();
            var textTableRows = await _context.Set<Label.TextTableRow>().AsNoTracking().Where(e => textTableIds.Contains(e.TextTableID)).ToListAsync();
            var ingredientSourceProducts = await _context.Set<Label.IngredientSourceProduct>().AsNoTracking().Where(e => ingredientIds.Contains(e.IngredientID)).ToListAsync();
            var specifiedSubstances = await _context.Set<Label.SpecifiedSubstance>().AsNoTracking().Where(e => ingredientIds.Contains(e.IngredientID)).ToListAsync();
            var packageIdentifiers = await _context.Set<Label.PackageIdentifier>().AsNoTracking().Where(e => packagingLevelIds.Contains(e.PackagingLevelID)).ToListAsync();
            var packagingHierarchies = await _context.Set<Label.PackagingHierarchy>().AsNoTracking().Where(e => packagingLevelIds.Contains(e.OuterPackagingLevelID) || packagingLevelIds.Contains(e.InnerPackagingLevelID)).ToListAsync();
            var marketingStatusForPackage = await _context.Set<Label.MarketingStatus>().AsNoTracking().Where(e => packagingLevelIds.Contains(e.PackagingLevelID)).ToListAsync();
            var characteristicsForPackage = await _context.Set<Label.Characteristic>().AsNoTracking().Where(e => packagingLevelIds.Contains(e.PackagingLevelID)).ToListAsync();
            var holders = await _context.Set<Label.Holder>().AsNoTracking().Where(e => marketingCategoryIds.Contains(e.MarketingCategoryID)).ToListAsync();
            var pharmClassNames = await _context.Set<Label.PharmacologicClassName>().AsNoTracking().Where(e => pharmacologicClassIds.Contains(e.PharmacologicClassID)).ToListAsync();
            var pharmClassLinks = await _context.Set<Label.PharmacologicClassLink>().AsNoTracking().Where(e => pharmacologicClassIds.Contains(e.PharmacologicClassID)).ToListAsync();
            var analytes = await _context.Set<Label.Analyte>().AsNoTracking().Where(e => substanceSpecificationIds.Contains(e.SubstanceSpecificationID)).ToListAsync();
            var observationCriteria = await _context.Set<Label.ObservationCriterion>().AsNoTracking().Where(e => substanceSpecificationIds.Contains(e.SubstanceSpecificationID)).ToListAsync();

            var productInstances = await _context.Set<Label.ProductInstance>().AsNoTracking().Where(pi => productIds.Contains(pi.ProductID)).ToListAsync();
            var productInstanceIds = productInstances.Select(pi => pi.ProductInstanceID).ToList();
            var ingredientInstances = await _context.Set<Label.IngredientInstance>().AsNoTracking().Where(ii => productInstanceIds.Contains(ii.FillLotInstanceID)).ToListAsync();
            var ingredientSubstanceIdsFromIngredientInstances = ingredientInstances.Select(ii => ii.IngredientSubstanceID).ToList();

            // Combine all IngredientSubstanceIDs and fetch them
            var allIngredientSubstanceIds = ingredientSubstanceIdsFromIngredients
                .Concat(ingredientSubstanceIdsFromIngredientInstances)
                .Where(id => id.HasValue)
                .Distinct()
                .ToList();

            var ingredientSubstances = await _context.Set<Label.IngredientSubstance>().AsNoTracking().Where(e => allIngredientSubstanceIds.Contains(e.IngredientSubstanceID)).ToListAsync();

            // Fetch children of IngredientSubstance
            var ingredientSubstanceIds = ingredientSubstances.Select(e => e.IngredientSubstanceID).ToList();
            var activeMoieties = await _context.Set<Label.ActiveMoiety>().AsNoTracking().Where(e => ingredientSubstanceIds.Contains(e.IngredientSubstanceID)).ToListAsync();
            var referenceSubstances = await _context.Set<Label.ReferenceSubstance>().AsNoTracking().Where(e => ingredientSubstanceIds.Contains(e.IngredientSubstanceID)).ToListAsync();

            // Fetch the other children of ProductInstance
            var lotHierarchies = await _context.Set<Label.LotHierarchy>().AsNoTracking().Where(lh => productInstanceIds.Contains(lh.ParentInstanceID) || productInstanceIds.Contains(lh.ChildInstanceID)).ToListAsync();
            var packagingByProductInstance = await _context.Set<Label.PackagingLevel>().AsNoTracking().Where(pl => productInstanceIds.Contains(pl.ProductInstanceID)).ToListAsync();

            // Level 6 Children
            var textTableRowIds = textTableRows.Select(e => e.TextTableRowID).ToList();
            var textTableCells = await _context.Set<Label.TextTableCell>().AsNoTracking().Where(e => textTableRowIds.Contains(e.TextTableRowID)).ToListAsync();
            #endregion

            #region Create Lookups for Efficient Access
            // Create lookups for every fetched table, keyed by their parent's ID.
            var authorsLookup = documentAuthors.ToLookup(e => e.DocumentID);
            var relationshipsLookup = documentRelationships.ToLookup(e => e.DocumentID);
            var authenticatorsLookup = legalAuthenticators.ToLookup(e => e.DocumentID);
            var relatedDocsLookup = relatedDocuments.ToLookup(e => e.SourceDocumentID);
            var bodiesLookup = structuredBodies.ToLookup(e => e.DocumentID);
            var bizOpsLookup = businessOperations.ToLookup(e => e.DocumentRelationshipID);
            var sectionsLookup = sections.ToLookup(e => e.StructuredBodyID);
            var bizOpQualifiersLookup = businessOperationQualifiers.ToLookup(e => e.BusinessOperationID);
            var bizOpProductsLookup = businessOperationProductLinks.ToLookup(e => e.BusinessOperationID);
            var licensesLookup = licenses.ToLookup(e => e.BusinessOperationID);
            var sectionHierarchyLookup = sectionHierarchies.ToLookup(e => e.ParentSectionID);
            var textContentsLookup = sectionTextContents.ToLookup(e => e.SectionID);
            var mediaLookup = observationMedia.ToLookup(e => e.SectionID);
            var highlightsLookup = sectionExcerptHighlights.ToLookup(e => e.SectionID);
            var productsBySectionLookup = products.ToLookup(e => e.SectionID);
            var identifiedSubstancesBySectionLookup = identifiedSubstances.ToLookup(e => e.SectionID);
            var productConceptsBySectionLookup = productConcepts.ToLookup(e => e.SectionID);
            var interactionIssuesBySectionLookup = interactionIssues.ToLookup(e => e.SectionID);
            var disciplinaryActionsLookup = disciplinaryActions.ToLookup(e => e.LicenseID);
            var listsLookup = textLists.ToLookup(e => e.SectionTextContentID);
            var tablesLookup = textTables.ToLookup(e => e.SectionTextContentID);
            var renderedMediaLookup = renderedMedia.ToLookup(e => e.SectionTextContentID);
            var productIdentifiersLookup = productIdentifiers.ToLookup(e => e.ProductID);
            var genericMedicinesLookup = genericMedicines.ToLookup(e => e.ProductID);
            var ingredientsByProductLookup = ingredients.Where(i => i.ProductID.HasValue).ToLookup(e => e.ProductID);
            var ingredientsByConceptLookup = ingredients.Where(i => i.ProductConceptID.HasValue).ToLookup(e => e.ProductConceptID);
            var packagingByProductLookup = packagingLevels.Where(p => p.ProductID.HasValue).ToLookup(e => e.ProductID);
            var packagingByPartProductLookup = packagingLevels.Where(p => p.PartProductID.HasValue).ToLookup(e => e.PartProductID);
            var marketingCategoriesByProductLookup = marketingCategories.Where(mc => mc.ProductID.HasValue).ToLookup(e => e.ProductID);
            var marketingCategoriesByConceptLookup = marketingCategories.Where(mc => mc.ProductConceptID.HasValue).ToLookup(e => e.ProductConceptID);
            var marketingStatusByProductLookup = marketingStatuses.ToLookup(e => e.ProductID);
            var characteristicsByProductLookup = characteristics.ToLookup(e => e.ProductID);
            var productPartsLookup = productParts.ToLookup(e => e.KitProductID);
            var policiesLookup = policies.ToLookup(e => e.ProductID);
            var routesLookup = productRoutes.ToLookup(e => e.ProductID);
            var webLinksLookup = productWebLinks.ToLookup(e => e.ProductID);
            var pharmClassesLookup = pharmacologicClasses.ToLookup(e => e.IdentifiedSubstanceID);
            var substanceSpecsLookup = substanceSpecifications.ToLookup(e => e.IdentifiedSubstanceID);
            var contributingFactorsLookup = contributingFactors.ToLookup(e => e.InteractionIssueID);
            var consequencesLookup = interactionConsequences.ToLookup(e => e.InteractionIssueID);
            var ingredientSubstancesLookup = ingredientSubstances.ToLookup(e => e.IngredientSubstanceID);
            var activeMoietiesLookup = activeMoieties.ToLookup(e => e.IngredientSubstanceID);
            var referenceSubstancesLookup = referenceSubstances.ToLookup(e => e.IngredientSubstanceID);
            var productInstancesByProductLookup = productInstances.ToLookup(pi => pi.ProductID);
            var ingredientInstancesByFillLotLookup = ingredientInstances.ToLookup(ii => ii.FillLotInstanceID);
            var lotHierarchyByParentLookup = lotHierarchies.ToLookup(lh => lh.ParentInstanceID);
            var packagingByInstanceLookup = packagingByProductInstance.ToLookup(pl => pl.ProductInstanceID);
            var attachedDocsDisciplinaryLookup = attachedDocsForDisciplinary.ToLookup(e => e.ParentEntityID);
            var attachedDocsRemsLookup = attachedDocsForRems.ToLookup(e => e.ParentEntityID);
            var listItemsLookup = textListItems.ToLookup(e => e.TextListID);
            var tableRowsLookup = textTableRows.ToLookup(e => e.TextTableID);
            var ingredientSourcesLookup = ingredientSourceProducts.ToLookup(e => e.IngredientID);
            var specifiedSubstancesLookup = specifiedSubstances.ToLookup(e => e.IngredientID);
            var packageIdentifiersLookup = packageIdentifiers.ToLookup(e => e.PackagingLevelID);
            var packagingHierarchyLookup = packagingHierarchies.ToLookup(e => e.OuterPackagingLevelID);
            var marketingStatusByPackageLookup = marketingStatusForPackage.ToLookup(e => e.PackagingLevelID);
            var characteristicsByPackageLookup = characteristicsForPackage.ToLookup(e => e.PackagingLevelID);
            var holdersLookup = holders.ToLookup(e => e.MarketingCategoryID);
            var pharmClassNameLookup = pharmClassNames.ToLookup(e => e.PharmacologicClassID);
            var pharmClassLinkLookup = pharmClassLinks.ToLookup(e => e.PharmacologicClassID);
            var analytesLookup = analytes.ToLookup(e => e.SubstanceSpecificationID);
            var criteriaLookup = observationCriteria.ToLookup(e => e.SubstanceSpecificationID);
            var tableCellsLookup = textTableCells.ToLookup(e => e.TextTableRowID);
            var billingUnitIndexLookup = billingUnitIndexes.ToLookup(e => e.SectionID);
            var warningLetterProductInfoLookup = warningLetterProductInfos.ToLookup(e => e.SectionID);
            var warningLetterDateLookup = warningLetterDates.ToLookup(e => e.SectionID);
            var protocolLookup = protocols.ToLookup(e => e.SectionID);
            var remsMaterialLookup = remsMaterials.ToLookup(e => e.SectionID);
            var remsElectronicResourceLookup = remsElectronicResources.ToLookup(e => e.SectionID);
            var complianceActionLookup = complianceActions.ToLookup(e => e.SectionID);
            var nctLinkLookup = nctLinks.ToLookup(e => e.SectionID);
            var requirementLookup = requirements.ToLookup(e => e.ProtocolID);
            var remsApprovalLookup = remsApprovals.ToLookup(e => e.ProtocolID);
            #endregion

            #region Stitch Data Together
            var results = new List<Dictionary<string, object?>>();

            foreach (var doc in documents)
            {
                var docDto = doc.ToEntityWithEncryptedId(_encryptionKey, _logger);

                // Add direct children of Document
                docDto["DocumentAuthors"] = authorsLookup[doc.DocumentID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                docDto["LegalAuthenticators"] = authenticatorsLookup[doc.DocumentID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                docDto["RelatedDocuments"] = relatedDocsLookup[doc.DocumentID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                // DocumentRelationships -> BusinessOperations -> Qualifiers/Links/Licenses -> DisciplinaryActions -> AttachedDocs
                docDto["DocumentRelationships"] = relationshipsLookup[doc.DocumentID].Select(rel => {
                    var relDto = rel.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    relDto["BusinessOperations"] = bizOpsLookup[rel.DocumentRelationshipID].Select(op => {
                        var opDto = op.ToEntityWithEncryptedId(_encryptionKey, _logger);

                        opDto["Qualifiers"] = bizOpQualifiersLookup[op.BusinessOperationID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                        opDto["ProductLinks"] = bizOpProductsLookup[op.BusinessOperationID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                        opDto["Licenses"] = licensesLookup[op.BusinessOperationID].Select(lic => {
                            var licDto = lic.ToEntityWithEncryptedId(_encryptionKey, _logger);
                            licDto["DisciplinaryActions"] = disciplinaryActionsLookup[lic.LicenseID].Select(da => {
                                var daDto = da.ToEntityWithEncryptedId(_encryptionKey, _logger);

                                daDto["AttachedDocuments"] = attachedDocsDisciplinaryLookup[da.DisciplinaryActionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                                return daDto;
                            }).ToList();

                            return licDto;
                        }).ToList();

                        return opDto;
                    }).ToList();

                    return relDto;
                }).ToList();

                // StructuredBodies -> Sections -> [All Section Children]
                docDto["StructuredBodies"] = bodiesLookup[doc.DocumentID].Select(body => {
                    var bodyDto = body.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    // Recursive function to build section hierarchy
                    Func<int?, List<Dictionary<string, object?>>> buildSectionTree = null!;
                    buildSectionTree = (parentId) => {
                        return sectionHierarchyLookup[parentId].Select(sh => {

                            var childSection = sections.FirstOrDefault(s => s.SectionID == sh.ChildSectionID);

                            if (childSection == null) return null;

                            var childDto = StitchSection(childSection); // Use helper to stitch all section children
                            childDto["SubSections"] = buildSectionTree(childSection.SectionID);

                            return childDto;
                        }).Where(s => s != null).ToList()!;
                    };

                    // Stitch top-level sections and their hierarchies
                    bodyDto["Sections"] = sectionsLookup[body.StructuredBodyID].Select(section => {
                        var sectionDto = StitchSection(section);
                        sectionDto["SubSections"] = buildSectionTree(section.SectionID);
                        return sectionDto;
                    }).ToList();
                    return bodyDto;
                }).ToList();

                results.Add(docDto);
            }

            return results;
            #endregion

            #region Stitching Helper Methods
            // Helper to stitch all children of a single Section
            Dictionary<string, object?> StitchSection(Label.Section section)
            {
                var sectionDto = section.ToEntityWithEncryptedId(_encryptionKey, _logger);

                sectionDto["TextContents"] = textContentsLookup[section.SectionID].Select(textContent => StitchTextContent(textContent)).ToList();

                sectionDto["ObservationMedia"] = mediaLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["ExcerptHighlights"] = highlightsLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["Products"] = productsBySectionLookup[section.SectionID].Select(product => StitchProduct(product)).ToList();

                sectionDto["IdentifiedSubstances"] = identifiedSubstancesBySectionLookup[section.SectionID].Select(substance => StitchIdentifiedSubstance(substance)).ToList();

                sectionDto["ProductConcepts"] = productConceptsBySectionLookup[section.SectionID].Select(concept => StitchProductConcept(concept)).ToList();

                sectionDto["InteractionIssues"] = interactionIssuesBySectionLookup[section.SectionID].Select(issue => {
                    var issueDto = issue.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    issueDto["ContributingFactors"] = contributingFactorsLookup[issue.InteractionIssueID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    issueDto["Consequences"] = consequencesLookup[issue.InteractionIssueID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return issueDto;
                }).ToList();

                sectionDto["BillingUnitIndexes"] = billingUnitIndexLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["WarningLetterProductInfos"] = warningLetterProductInfoLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["WarningLetterDates"] = warningLetterDateLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["Protocols"] = protocolLookup[section.SectionID].Select(p => {

                    var pDto = p.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    pDto["Requirements"] = requirementLookup[p.ProtocolID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    pDto["REMSApprovals"] = remsApprovalLookup[p.ProtocolID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return pDto;
                }).ToList();

                sectionDto["REMSMaterials"] = remsMaterialLookup[section.SectionID].Select(m => {

                    var mDto = m.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    mDto["AttachedDocuments"] = attachedDocsRemsLookup[m.REMSMaterialID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return mDto;
                }).ToList();

                sectionDto["REMSElectronicResources"] = remsElectronicResourceLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["ComplianceActions"] = complianceActionLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                sectionDto["NCTLinks"] = nctLinkLookup[section.SectionID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();
                return sectionDto;
            }

            // Helper to stitch children of SectionTextContent
            Dictionary<string, object?> StitchTextContent(Label.SectionTextContent textContent)
            {
                var textContentDto = textContent.ToEntityWithEncryptedId(_encryptionKey, _logger);
                textContentDto["Lists"] = listsLookup[textContent.SectionTextContentID].Select(list => {

                    var listDto = list.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    listDto["Items"] = listItemsLookup[list.TextListID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return listDto;
                }).ToList();

                textContentDto["Tables"] = tablesLookup[textContent.SectionTextContentID].Select(table => {

                    var tableDto = table.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    tableDto["Rows"] = tableRowsLookup[table.TextTableID].Select(row => {

                        var rowDto = row.ToEntityWithEncryptedId(_encryptionKey, _logger);

                        rowDto["Cells"] = tableCellsLookup[row.TextTableRowID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                        return rowDto;
                    }).ToList();

                    return tableDto;
                }).ToList();

                textContentDto["RenderedMedia"] = renderedMediaLookup[textContent.SectionTextContentID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                return textContentDto;
            }

            // Helper to stitch children of Product
            Dictionary<string, object?> StitchProduct(Label.Product product)
            {
                var productDto = product.ToEntityWithEncryptedId(_encryptionKey, _logger);

                productDto["Identifiers"] = productIdentifiersLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["GenericMedicines"] = genericMedicinesLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["Ingredients"] = ingredientsByProductLookup[product.ProductID].Select(ing => {

                    var ingDto = ing.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    ingDto["SourceProducts"] = ingredientSourcesLookup[ing.IngredientID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    ingDto["SpecifiedSubstances"] = specifiedSubstancesLookup[ing.IngredientID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    ingDto["IngredientSubstance"] = ingredientSubstancesLookup[ing.IngredientSubstanceID]
                        .Select(sub => StitchIngredientSubstance(sub))
                        .FirstOrDefault();

                    return ingDto;
                }).ToList();

                productDto["MarketingCategories"] = marketingCategoriesByProductLookup[product.ProductID].Select(mc => {

                    var mcDto = mc.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    mcDto["Holders"] = holdersLookup[mc.MarketingCategoryID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return mcDto;
                }).ToList();

                productDto["MarketingStatuses"] = marketingStatusByProductLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["Characteristics"] = characteristicsByProductLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["Parts"] = productPartsLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["Policies"] = policiesLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["RoutesOfAdministration"] = routesLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["WebLinks"] = webLinksLookup[product.ProductID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                productDto["Packaging"] = packagingByProductLookup[product.ProductID].Select(pkg => StitchPackagingLevel(pkg)).ToList();

                productDto["ProductInstances"] = productInstancesByProductLookup[product.ProductID].Select(pi => StitchProductInstance(pi)).ToList();

                return productDto;
            }

            // Helper method to stich products
            Dictionary<string, object?> StitchProductInstance(Label.ProductInstance pi)
            {
                var piDto = pi.ToEntityWithEncryptedId(_encryptionKey, _logger);

                piDto["IngredientInstances"] = ingredientInstancesByFillLotLookup[pi.ProductInstanceID].Select(ii => {
                   
                    var iiDto = ii.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    // Stitch the IngredientSubstance for this IngredientInstance
                    iiDto["IngredientSubstance"] = ingredientSubstancesLookup[ii.IngredientSubstanceID]
                        .Select(sub => StitchIngredientSubstance(sub))
                        .FirstOrDefault();

                    return iiDto;
                }).ToList();

                piDto["LotHierarchyMembers"] = lotHierarchyByParentLookup[pi.ProductInstanceID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();
                piDto["Packaging"] = packagingByInstanceLookup[pi.ProductInstanceID].Select(pkg => StitchPackagingLevel(pkg)).ToList();
                return piDto;
            }

            // Helper method to stich Ingredient Substances
            Dictionary<string, object?> StitchIngredientSubstance(Label.IngredientSubstance substance)
            {
                var substanceDto = substance.ToEntityWithEncryptedId(_encryptionKey, _logger);

                substanceDto["ActiveMoieties"] = activeMoietiesLookup[substance.IngredientSubstanceID]
                    .Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                substanceDto["ReferenceSubstances"] = referenceSubstancesLookup[substance.IngredientSubstanceID]
                    .Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                return substanceDto;
            }

            // Helper to stitch children of PackagingLevel (recursively)
            Dictionary<string, object?> StitchPackagingLevel(Label.PackagingLevel pkg)
            {
                var pkgDto = pkg.ToEntityWithEncryptedId(_encryptionKey, _logger);

                pkgDto["Identifiers"] = packageIdentifiersLookup[pkg.PackagingLevelID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                pkgDto["MarketingStatuses"] = marketingStatusByPackageLookup[pkg.PackagingLevelID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                pkgDto["Characteristics"] = characteristicsByPackageLookup[pkg.PackagingLevelID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                pkgDto["ContainedPackages"] = packagingHierarchyLookup[pkg.PackagingLevelID].Select(h => {
                    var innerPkg = packagingLevels.FirstOrDefault(p => p.PackagingLevelID == h.InnerPackagingLevelID);
                    return innerPkg != null ? StitchPackagingLevel(innerPkg) : null;
                }).Where(p => p != null).ToList();

                return pkgDto;
            }

            // Helper to stitch children of IdentifiedSubstance
            Dictionary<string, object?> StitchIdentifiedSubstance(Label.IdentifiedSubstance substance)
            {
                var substanceDto = substance.ToEntityWithEncryptedId(_encryptionKey, _logger);

                substanceDto["PharmacologicClasses"] = pharmClassesLookup[substance.IdentifiedSubstanceID].Select(pc => {

                    var pcDto = pc.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    pcDto["Names"] = pharmClassNameLookup[pc.PharmacologicClassID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    pcDto["Links"] = pharmClassLinkLookup[pc.PharmacologicClassID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return pcDto;
                }).ToList();

                substanceDto["Specifications"] = substanceSpecsLookup[substance.IdentifiedSubstanceID].Select(spec => {

                    var specDto = spec.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    specDto["Analytes"] = analytesLookup[spec.SubstanceSpecificationID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    specDto["ObservationCriteria"] = criteriaLookup[spec.SubstanceSpecificationID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return specDto;
                }).ToList();

                return substanceDto;
            }

            // Helper to stitch children of ProductConcept
            Dictionary<string, object?> StitchProductConcept(Label.ProductConcept concept)
            {
                var conceptDto = concept.ToEntityWithEncryptedId(_encryptionKey, _logger);

                conceptDto["Ingredients"] = ingredientsByConceptLookup[concept.ProductConceptID].Select(ing => {

                    var ingDto = ing.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    ingDto["SourceProducts"] = ingredientSourcesLookup[ing.IngredientID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    ingDto["SpecifiedSubstances"] = specifiedSubstancesLookup[ing.IngredientID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    ingDto["IngredientSubstance"] = ingredientSubstancesLookup[ing.IngredientSubstanceID]
                        .Select(sub => StitchIngredientSubstance(sub))
                        .FirstOrDefault();

                    return ingDto;
                }).ToList();

                conceptDto["MarketingCategories"] = marketingCategoriesByConceptLookup[concept.ProductConceptID].Select(mc => {
                    
                    var mcDto = mc.ToEntityWithEncryptedId(_encryptionKey, _logger);

                    mcDto["Holders"] = holdersLookup[mc.MarketingCategoryID].Select(e => e.ToEntityWithEncryptedId(_encryptionKey, _logger)).ToList();

                    return mcDto;
                }).ToList();

                return conceptDto;
            }
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Updates an existing record in the database based on the provided entity.
        /// The entity must be tracked or attachable by EF Core, and its primary key property must be set.
        /// </summary>
        /// <param name="entity">The entity object with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved from the entity.</exception>
        public virtual async Task<int> UpdateAsync(T entity)
        {
            #region implementation
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Ensure the PK property is set on the entity for EF Core to identify it.
            var primaryKeyValue = _primaryKeyProperty?.GetValue(entity);

            if (primaryKeyValue == null)
            {
                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' cannot be null for update operation on type {typeof(T).Name}.");
            }

            // _dbSet.Update(entity); // Marks all properties as modified.
            // More robust way if entity might be detached:
            _context.Entry(entity).State = EntityState.Modified;

            return await _context.SaveChangesAsync();
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on its encrypted primary key ID.
        /// </summary>
        /// <param name="encryptedId">The encrypted primary key ID of the record to delete.</param>
        /// <returns>The number of rows affected (should be 1 if successful, 0 if not found).</returns>
        public virtual async Task<int> DeleteAsync(string encryptedId)
        {
            #region implementation
            long id;

            if (!tryDecryptId(encryptedId, nameof(encryptedId), out id))
            {
                _logger.LogWarning("Failed to decrypt ID for DeleteAsync. Encrypted ID: {EncryptedId}", encryptedId);

                throw new InvalidOperationException($"Failed to decrypt ID for DeleteAsync. Encrypted ID: {encryptedId}");
            }

            var entityToDelete = await _dbSet.FindAsync(Convert.ToInt32(id));

            if (entityToDelete == null)
            {
                throw new KeyNotFoundException($"No entity found with primary key '{_primaryKeyName}' value '{encryptedId}' for deletion in type {typeof(T).Name}.");
            }

            _dbSet.Remove(entityToDelete);

            return await _context.SaveChangesAsync();
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on the primary key of the provided entity.
        /// </summary>
        /// <param name="entity">The entity object whose corresponding record should be deleted. The primary key property must be set.</param>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved or is invalid.</exception>
        public virtual async Task<int> DeleteAsync(T entity)
        {
            #region implementation
            string encryptedId;

            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var primaryKeyValueObject = _primaryKeyProperty?.GetValue(entity);

            if (primaryKeyValueObject == null)
            {
                _logger.LogError("Primary key '{PrimaryKeyName}' on type {EntityType} is null for delete operation.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' cannot be null for delete operation on type {typeof(T).Name}.");
            }

            if (primaryKeyValueObject is long longValue)
            {
                // Additional check for typical auto-incrementing int PKs
                if (longValue <= 0 && typeof(T).GetProperty(_primaryKeyName)?.PropertyType == typeof(long))
                {
                    throw new InvalidOperationException($"Primary key value '{longValue}' for '{_primaryKeyName}' is not valid for delete operation on type {typeof(T).Name}.");
                }

                // Encrypt the primary key value to match the expected format
                encryptedId = longValue.ToString().Encrypt(_encryptionKey);

                // Call the ID-based delete for simplicity and consistency
                return await DeleteAsync(encryptedId);
            }

            if (primaryKeyValueObject is int intValue)
            {
                // Additional check for typical auto-incrementing int PKs
                if (intValue <= 0 && typeof(T).GetProperty(_primaryKeyName)?.PropertyType == typeof(int))
                {
                    throw new InvalidOperationException($"Primary key value '{intValue}' for '{_primaryKeyName}' is not valid for delete operation on type {typeof(T).Name}.");
                }

                // Encrypt the primary key value to match the expected format
                encryptedId = intValue.ToString().Encrypt(_encryptionKey);

                // Call the ID-based delete for simplicity and consistency
                return await DeleteAsync(encryptedId);
            }

            // If the primary key is not a long, we need to handle it differently.
            // Here it assumed that the key is encrypted.
            if (primaryKeyValueObject is string stringValue)
            {
                return await DeleteAsync(stringValue);
            }

            throw new InvalidOperationException($"Primary key for '{_primaryKeyName}' on type {typeof(T).Name} is not an integer, or could not be retrieved correctly for deletion. PK type found: {primaryKeyValueObject.GetType().Name}");
            #endregion
        }
    }
}
