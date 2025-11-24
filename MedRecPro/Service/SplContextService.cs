using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using System.Xml.Linq;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    #region Core Interfaces and Context Objects

    /**************************************************************/
    /// <summary>
    /// Defines the contract for a parser that handles a specific section/element of an SPL XML file.
    /// </summary>
    /// <remarks>
    /// This interface enables a modular parsing approach where different parsers can handle
    /// specific sections of the SPL (Structured Product Labeling) XML documents independently.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="SplParseResult"/>
    public interface ISplSectionParser
    {
        /// <summary>
        /// Gets the name of the section this parser handles.
        /// </summary>
        /// <seealso cref="Label"/>
        string SectionName { get; }

        /**************************************************************/
        /// <summary>
        /// Parses the specified XML element asynchronously within the given context.
        /// </summary>
        /// <param name="element">The XML element to parse from the SPL document.</param>
        /// <param name="context">The parsing context containing shared state and dependencies.</param>
        /// <param name="reportProgress">Reporter for progress</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A task representing the asynchronous parsing operation with the parse result.</returns>
        /// <seealso cref="Label"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false);

    }

    /**************************************************************/
    /// <summary>
    /// Carries shared state and dependencies throughout the parsing process for a single XML file.
    /// </summary>
    /// <remarks>
    /// This context object provides a centralized way to share state, services, and tracking
    /// information across all parsers involved in processing a single SPL XML document.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SplFileImportResult"/>
    public class SplParseContext
    {
        #region properties

        private IServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets or sets the sequence number for the current parsing operation.
        /// </summary>
        public int SeqNumber { get; set; } = 0;

        /// <summary>
        /// Gets a value indicating whether bulk database operations should be used.
        /// </summary>
        /// <remarks>
        /// This value is populated from FeatureFlags:UseBulkOperations in appsettings.json
        /// when ServiceProvider is set. Defaults to false if not configured.
        /// </remarks>
        public bool UseBulkOperations { get; private set; }

        /// <summary>
        /// Gets a value indicating whether staged bulk operations should be used.
        /// When enabled, section discovery happens first, then all sections are processed
        /// in flat bulk operations without recursive orchestration.
        /// </summary>
        /// <remarks>
        /// This value is populated from FeatureFlags:UseBulkStagingOperations in appsettings.json
        /// when ServiceProvider is set. Defaults to false if not configured.
        /// Requires UseBulkOperations to also be enabled.
        /// </remarks>
        public bool UseBulkStagingOperations { get; private set; }

        /// <summary>
        /// Gets a value indicating whether bulk saving is enabled for data operations.
        /// </summary>
        public bool UseBatchSaving { get; private set;  }

        /// <summary>
        /// Gets or sets the database context used for data access operations.
        /// </summary>
        public ApplicationDbContext? DbContext { get; set; }

        /// <summary>
        /// Contains the results of section discovery if UseBulkStaging is enabled.
        /// Populated during Pass 1 (discovery), consumed during Pass 2 (processing).
        /// </summary>
        public SectionDiscoveryResult? SectionDiscovery { get; set; }

        /// <summary>
        /// Gets or sets the service provider for dependency injection.
        /// </summary>
        /// <remarks>
        /// When set, automatically populates feature flags from configuration.
        /// </remarks>
        /// <seealso cref="Label"/>
        public IServiceProvider? ServiceProvider
        {
            get => _serviceProvider;
            set
            {
                _serviceProvider = value;

                #region implementation

                // Auto-populate feature flags when ServiceProvider is set
                if (_serviceProvider != null)
                {
                    var configuration = _serviceProvider.GetService<IConfiguration>();
                    if (configuration != null)
                    {
                        UseBulkOperations = configuration.GetValue<bool>("FeatureFlags:UseBulkOperations", false);
                        UseBulkStagingOperations = configuration.GetValue<bool>("FeatureFlags:UseBulkStagingOperations", false);
                        UseBatchSaving = configuration.GetValue<bool>("FeatureFlags:UseBatchSaving", false);
                    }
                }

                #endregion
            }
        }

        /// <summary>
        /// Gets or sets the logger instance for recording parsing events and errors.
        /// </summary>
        /// <seealso cref="Label"/>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets the cumulative result tracking for the current file import operation.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="SplFileImportResult"/>
        public SplFileImportResult? FileResult { get; set; }

        /// <summary>
        /// Gets or sets the name of the file being processed within the ZIP archive.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? FileNameInZip { get; set; }

        /// <summary>
        /// Gets or sets the current document being parsed from the SPL XML.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Document"/>
        public Document? Document { get; set; }

        /// <summary>
        /// Gets or sets the structured body element from the current SPL document.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="StructuredBody"/>
        public StructuredBody? StructuredBody { get; set; }

        /// <summary>
        /// Gets or sets the section currently being processed during parsing.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Section"/>
        public Section? CurrentSection { get; set; }

        /// <summary>
        /// Gets or sets the parser responsible for handling the main section of the SPL document.
        /// This holds the object for recursive parsing within a section
        /// </summary>
        public ISplSectionParser? MainSectionParser { get; set; }

        /// <summary>
        /// Gets or sets the product currently being processed during parsing.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Product"/>
        public Product? CurrentProduct { get; set; }

        /// <summary>
        /// Gets or sets the current packaging level being processed.
        /// </summary>
        public PackagingLevel? CurrentPackagingLevel { get; set; }

        /// <summary>
        /// Gets or sets the current business operation being processed.
        /// </summary>
        public BusinessOperation? CurrentBusinessOperation { get; set; }

        /// <summary>
        /// Holds the current PackageIdentifier being parsed, allowing child parsers
        /// like ComplianceActionParser to link to it.
        /// </summary>
        /// <seealso cref="PackageIdentifier"/>
        /// <seealso cref="MedRecPro.Service.ParsingServices.PackagingParser"/>
        public PackageIdentifier? CurrentPackageIdentifier { get; set; }

        /// <summary>
        /// Holds the current DocumentRelationship being parsed (e.g., for an establishment),
        /// allowing child parsers to link to it.
        /// </summary>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="MedRecPro.Service.ParsingServices.ComplianceActionParser"/>
        /// <seealso cref="MedRecPro.Service.ParsingServices.CertificationProductLinkParser"/>
        public DocumentRelationship? CurrentDocumentRelationship { get; set; }

        /// <summary>
        /// Holds the current ComplianceAction being parsed, allowing child parsers
        /// like AttachedDocumentParser to link to it.
        /// </summary>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="MedRecPro.Service.ParsingServices.AttachedDocumentParser"/>
        public ComplianceAction? CurrentComplianceAction { get; set; }

        /// <summary>
        /// Gets or sets the current ProductIdentifier being processed.
        /// </summary>
        public ProductIdentifier? CurrentProductIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the the current license
        /// </summary>
        public License? CurrentLicense { get; set; }

        /// <summary>
        /// Gets or sets the count of ingredients created during the current parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int IngredientsCreated { get; set; }

        /// <summary>
        /// Optional delegate to post progress messages during parsing.
        /// Parsers can use this to report their current activity.
        /// </summary>
        public Action<string>? ReportProgress { get; set; }

        /// <summary>
        /// Gets or sets the root document XElement for accessing document-level elements.
        /// </summary>
        /// <seealso cref="Label"/>
        public XElement? DocumentElement { get; set; }


        #endregion

        /**************************************************************/
        /// <summary>
        /// Public method for setting the UseBulkOperations flag.
        ///</summary>
        public void SetBulkOperationsFlag(bool value)
        {
            UseBulkOperations = value;
        }

        /**************************************************************/
        /// <summary>
        /// Public method for setting the UseBulkOperations flag.
        ///</summary>
        public void SetBulkStagingFlag(bool value)
        {
            UseBulkStagingOperations = value;
        }

        /**************************************************************/
        /// <summary>
        /// Enables or disables bulk saving mode for subsequent operations.
        /// </summary>
        /// <remarks>When bulk saving mode is enabled, operations may be optimized for batch processing.
        /// The effect of this setting depends on the implementation and may impact performance or transactional
        /// behavior.</remarks>
        /// <param name="value">true to enable bulk saving mode; false to disable it.</param>
        public void SetBatchSavingFlag(bool value)
        {
            UseBatchSaving = value;
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a repository instance for the specified entity type.
        /// </summary>
        /// <typeparam name="T">The entity type for which to resolve a repository.</typeparam>
        /// <returns>A repository instance capable of handling the specified entity type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the repository cannot be resolved from the service provider.</exception>
        /// <example>
        /// <code>
        /// var productRepo = context.GetRepository&lt;Product&gt;();
        /// var documentRepo = context.GetRepository&lt;Document&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="Repository{T}"/>
        public Repository<T> GetRepository<T>() where T : class
        {
            #region implementation

            // Attempt to resolve the repository from the service container
            var repo = ServiceProvider?.GetService<Repository<T>>();

            // Throw descriptive error if repository cannot be resolved
            if (repo == null)
                throw new InvalidOperationException($"Could not resolve repository for type Repository<{typeof(T).Name}>. Ensure it and its dependencies are registered.");

            return repo;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates the file result with statistics and errors from a parsing operation.
        /// </summary>
        /// <param name="parseResult">The parse result containing statistics and errors to merge.</param>
        /// <remarks>
        /// This method aggregates counts and errors from individual parsing operations
        /// into the overall file import result for tracking purposes.
        /// </remarks>
        /// <seealso cref="Label"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplFileImportResult"/>
        public void UpdateFileResult(SplParseResult parseResult)
        {
            #region implementation
            FileResult = FileResult ?? new SplFileImportResult();

            // Aggregate entity creation counts
            FileResult.DocumentsCreated += parseResult.DocumentsCreated;
            FileResult.OrganizationsCreated += parseResult.OrganizationsCreated;
            FileResult.ProductsCreated += parseResult.ProductsCreated;
            FileResult.SectionsCreated += parseResult.SectionsCreated;
            FileResult.IngredientsCreated += parseResult.IngredientsCreated;
            FileResult.ProductElementsCreated += parseResult.ProductElementsCreated;


            // Merge any errors encountered during parsing
            FileResult.Errors.AddRange(parseResult.Errors);

            // Mark file result as failed if this parse operation failed
            if (!parseResult.Success)
                FileResult.Success = false;

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the outcome of a single parsing operation by an ISplSectionParser.
    /// </summary>
    /// <remarks>
    /// This result object encapsulates both success/failure status and detailed statistics
    /// about entities created during a parsing operation, along with any errors encountered.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SplParseContext"/>
    public class SplParseResult
    {
        #region implementation

        /// <summary>
        /// Gets or sets a value indicating whether the parsing operation completed successfully.
        /// </summary>
        /// <seealso cref="Label"/>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of error messages encountered during parsing.
        /// </summary>
        /// <seealso cref="Label"/>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets the primary entity that was parsed and created from the XML element.
        /// </summary>
        /// <seealso cref="Label"/>
        public object? ParsedEntity { get; set; }

        /// <summary>
        /// Gets or sets the document code associated with the parsed entity, if applicable.
        /// 
        /// </summary>
        public string? DocumentCode { get; set; } = null;

        /// <summary>
        /// Gets or sets the number of documents created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Document"/>
        public int DocumentsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number document elements created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int DocumentAttributesCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of organizations created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int OrganizationsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number organization elements created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int OrganizationAttributesCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of products created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Product"/>
        public int ProductsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of sections created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Section"/>
        public int SectionsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number section elements created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int SectionAttributesCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of generics created during this parsing operation.
        /// </summary>
        public int ProductElementsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of licenses created during parsing.
        /// </summary>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        public int LicensesCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of disciplinary actions that have been created.
        /// </summary>
        public int DisciplinaryActionsCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of documents that have been attached and created.
        /// </summary>
        public int AttachedDocumentsCreated { get; set; }

        /// <summary>
        /// Gets or sets the current product being processed, if applicable.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="Product"/>
        public Product? CurrentProduct { get; set; }

        /// <summary>
        /// Gets or sets the number of lot hierarchies created during this parsing operation.
        /// </summary>
        public int LotHierarchiesCreated { get; set; }

        /// <summary>
        /// Gets or sets the number of ingredients created during this parsing operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public int IngredientsCreated { get; set; }

        #endregion

        /**************************************************************/
        /// <summary>
        /// Merges statistics and errors from another parse result into this instance.
        /// </summary>
        /// <param name="other">The parse result to merge from.</param>
        /// <remarks>
        /// This method combines counts and errors from multiple parsing operations,
        /// useful for aggregating results from nested or sequential parsing operations.
        /// </remarks>
        /// <seealso cref="Label"/>
        public void MergeFrom(SplParseResult other)
        {
            #region implementation

            // Propagate failure status if the other operation failed
            if (!other.Success) Success = false;

            // Combine error messages from both results
            Errors.AddRange(other.Errors);

            // Sum up all entity creation counts
            DocumentsCreated += other.DocumentsCreated;
            DocumentAttributesCreated += other.DocumentAttributesCreated;
            OrganizationsCreated += other.OrganizationsCreated;
            OrganizationAttributesCreated += other.OrganizationAttributesCreated;
            ProductsCreated += other.ProductsCreated;
            SectionsCreated += other.SectionsCreated;
            SectionAttributesCreated += other.SectionAttributesCreated;
            ProductElementsCreated += other.ProductElementsCreated;
            LicensesCreated += other.LicensesCreated;
            LotHierarchiesCreated += other.LotHierarchiesCreated;
            IngredientsCreated += other.IngredientsCreated;
            DisciplinaryActionsCreated += other.DisciplinaryActionsCreated;
            AttachedDocumentsCreated += other.AttachedDocumentsCreated;

            #endregion
        }
    }

    #endregion

    #region Section Discovery Data Structures

    /**************************************************************/
    /// <summary>
    /// Data Transfer Object representing a discovered section during XML traversal.
    /// Contains all section metadata parsed from XML before database operations.
    /// </summary>
    /// <seealso cref="Label.Section"/>
    public class SectionDiscoveryDto
    {
        /// <summary>
        /// The unique identifier GUID from the section's id@root attribute.
        /// </summary>
        public Guid SectionGuid { get; set; }

        /// <summary>
        /// The code value from the section's code element.
        /// </summary>
        public string? SectionCode { get; set; }

        /// <summary>
        /// The code system from the section's code element.
        /// </summary>
        public string? SectionCodeSystem { get; set; }

        /// <summary>
        /// The display name from the section's code element.
        /// </summary>
        public string? SectionCodeDisplayName { get; set; }

        /// <summary>
        /// The section title text.
        /// </summary>
        public string? SectionTitle { get; set; }

        /// <summary>
        /// The nesting depth of this section (root sections = 0).
        /// </summary>
        public int NestingLevel { get; set; }

        /// <summary>
        /// The parent section's GUID, if this is a child section.
        /// </summary>
        public Guid? ParentSectionGuid { get; set; }

        /// <summary>
        /// The XElement for this section (for Phase 2 content parsing).
        /// </summary>
        public XElement SourceElement { get; set; } = null!;

        /// <summary>
        /// The database ID once the section is created (populated during Phase 2).
        /// </summary>
        public int? SectionID { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Data Transfer Object representing a discovered section hierarchy relationship.
    /// </summary>
    /// <seealso cref="Label.SectionHierarchy"/>
    public class SectionHierarchyDiscoveryDto
    {
        /// <summary>
        /// The GUID of the parent section.
        /// </summary>
        public Guid ParentSectionGuid { get; set; }

        /// <summary>
        /// The GUID of the child section.
        /// </summary>
        public Guid ChildSectionGuid { get; set; }

        /// <summary>
        /// The sequence number of this child within the parent.
        /// </summary>
        public int SequenceNumber { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Contains the complete results of section discovery traversal.
    /// Stored in SplParseContext for use during bulk processing phases.
    /// </summary>
    public class SectionDiscoveryResult
    {
        /// <summary>
        /// All sections discovered at all nesting levels, in document order.
        /// </summary>
        public List<SectionDiscoveryDto> AllSections { get; set; } = new();

        /// <summary>
        /// All hierarchy relationships discovered during traversal.
        /// </summary>
        public List<SectionHierarchyDiscoveryDto> AllHierarchies { get; set; } = new();

        /// <summary>
        /// Quick lookup: GUID → SectionDiscoveryDto
        /// </summary>
        public Dictionary<Guid, SectionDiscoveryDto> SectionsByGuid { get; set; } = new();

        /// <summary>
        /// Quick lookup: GUID → Database Section ID (populated during bulk creation)
        /// </summary>
        public Dictionary<Guid, int> SectionIdsByGuid { get; set; } = new();
    }

    #endregion

}