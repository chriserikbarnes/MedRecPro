using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.ParsingServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Shared fixture and XML factory helpers for parser-service tests.
    /// </summary>
    /// <remarks>
    /// The helpers keep parser tests on the real import DbContext and repository stack while
    /// avoiding repeated SQLite setup code across every parser-focused test class.
    /// </remarks>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="ApplicationDbContext"/>
    /// <seealso cref="Repository{T}"/>
    internal static class ParsingServiceTestHelper
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// SPL namespace used by fixture XML and focused parser snippets.
        /// </summary>
        /// <seealso cref="XNamespace"/>
        public static readonly XNamespace Spl = "urn:hl7-org:v3";

        /**************************************************************/
        /// <summary>
        /// Full SPL fixture file name copied into the test project.
        /// </summary>
        /// <seealso cref="LoadFullSplDocument"/>
        public const string FullFixtureFileName = "f16d9f01-d515-40fe-a0ff-ac70627cb512.xml";

        #endregion

        /**************************************************************/
        /// <summary>
        /// Creates a SQLite-backed parser test database with repository services wired.
        /// </summary>
        /// <param name="configurationOverrides">Optional configuration values layered over parser defaults.</param>
        /// <returns>A disposable parser test database fixture.</returns>
        /// <remarks>
        /// Uses a named shared-cache in-memory database and keeps a sentinel connection open so
        /// parser code can save and query through the same schema for the fixture lifetime.
        /// </remarks>
        /// <example>
        /// <code>
        /// using var database = ParsingServiceTestHelper.CreateDatabase();
        /// var context = database.CreateParseContext(ParserMode.SingleCall);
        /// </code>
        /// </example>
        /// <seealso cref="ParserTestDatabase"/>
        public static ParserTestDatabase CreateDatabase(Dictionary<string, string?>? configurationOverrides = null)
        {
            #region implementation
            var dbName = $"file:parsing_services_{Guid.NewGuid():N}?mode=memory&cache=shared";
            var connectionString = $"DataSource={dbName}";

            var sentinelConnection = new SqliteConnection(connectionString);
            sentinelConnection.Open();

            var connection = new SqliteConnection(connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new ApplicationDbContext(options);
            applySqliteSchema(dbContext, connection);

            var configuration = buildConfiguration(configurationOverrides);
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(dbContext);
            services.AddSingleton(new StringCipher());
            services.AddSingleton<ILogger>(NullLogger.Instance);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddTransient(typeof(Repository<>));

            var serviceProvider = services.BuildServiceProvider();

            return new ParserTestDatabase(sentinelConnection, connection, dbContext, serviceProvider, configuration);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the copied full SPL fixture document from the test output directory.
        /// </summary>
        /// <returns>The parsed SPL fixture document.</returns>
        /// <remarks>
        /// The fixture is copied through the test project file so tests do not depend on a
        /// user-specific attachment path outside the repository.
        /// </remarks>
        /// <seealso cref="FullFixtureFileName"/>
        /// <seealso cref="XDocument"/>
        public static XDocument LoadFullSplDocument()
        {
            #region implementation
            var path = Path.Combine(AppContext.BaseDirectory, "TestData", "SPL", FullFixtureFileName);
            return XDocument.Load(path, LoadOptions.PreserveWhitespace);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a focused SPL XML snippet into an XElement.
        /// </summary>
        /// <param name="xml">The XML snippet to parse.</param>
        /// <returns>The parsed XML element.</returns>
        /// <seealso cref="XElement"/>
        public static XElement Element(string xml)
        {
            #region implementation
            return XElement.Parse(xml, LoadOptions.PreserveWhitespace);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal author element with a represented organization.
        /// </summary>
        /// <param name="name">Organization name to include.</param>
        /// <param name="identifier">Organization identifier extension.</param>
        /// <returns>A minimal author element.</returns>
        /// <seealso cref="AuthorSectionParser"/>
        public static XElement MinimalAuthor(string name = "A-S Medication Solutions", string identifier = "830016429")
        {
            #region implementation
            return Element($$"""
                <author xmlns="urn:hl7-org:v3">
                  <assignedEntity>
                    <representedOrganization>
                      <id root="1.3.6.1.4.1.519.1" extension="{{identifier}}" />
                      <name>{{name}}</name>
                    </representedOrganization>
                  </assignedEntity>
                </author>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal manufactured product element with product identity and form data.
        /// </summary>
        /// <param name="productName">Product name to include.</param>
        /// <returns>A minimal manufactured product element.</returns>
        /// <seealso cref="ManufacturedProductParser"/>
        public static XElement MinimalManufacturedProduct(string productName = "CIPROFLOXACIN")
        {
            #region implementation
            return Element($$"""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <manufacturedMedicine>
                    <code code="50090-5373" codeSystem="2.16.840.1.113883.6.69" />
                    <name>{{productName}}</name>
                    <formCode code="C42998" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="TABLET, FILM COATED" />
                  </manufacturedMedicine>
                </manufacturedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a manufactured product element with active and inactive ingredient snippets.
        /// </summary>
        /// <returns>A manufactured product element with ingredient content.</returns>
        /// <seealso cref="IngredientParser"/>
        public static XElement ProductWithIngredients()
        {
            #region implementation
            return Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <manufacturedMedicine>
                    <name>CIPROFLOXACIN</name>
                    <ingredient classCode="ACTIB">
                      <quantity>
                        <numerator value="500" unit="mg" />
                        <denominator value="1" unit="1" />
                      </quantity>
                      <ingredientSubstance>
                        <code code="4BA73M5E37" codeSystem="2.16.840.1.113883.4.9" />
                        <name>CIPROFLOXACIN HYDROCHLORIDE</name>
                      </ingredientSubstance>
                    </ingredient>
                    <ingredient classCode="IACT">
                      <ingredientSubstance>
                        <name>MICROCRYSTALLINE CELLULOSE</name>
                      </ingredientSubstance>
                    </ingredient>
                  </manufacturedMedicine>
                </manufacturedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal product marketing category snippet.
        /// </summary>
        /// <returns>A manufactured product element with marketing category data.</returns>
        /// <seealso cref="ProductMarketingParser"/>
        public static XElement ProductMarketingCategory()
        {
            #region implementation
            return Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <subjectOf>
                    <marketingAct>
                      <code code="C73584" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="NDA" />
                      <id root="2.16.840.1.113883.3.150" extension="NDA123456" />
                    </marketingAct>
                  </subjectOf>
                </manufacturedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a package element with NDC package identifier and marketing status.
        /// </summary>
        /// <returns>A minimal asContent package element.</returns>
        /// <seealso cref="PackagingParser"/>
        /// <seealso cref="MarketingStatusParser"/>
        public static XElement PackageWithNdcIdentifier()
        {
            #region implementation
            return Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <asContent>
                    <quantity>
                      <numerator value="30" unit="1" />
                      <denominator value="1" unit="1" />
                    </quantity>
                    <containerPackagedProduct>
                      <code code="50090-5373-0" codeSystem="2.16.840.1.113883.6.69" />
                      <subjectOf>
                        <marketingAct>
                          <statusCode code="active" />
                          <effectiveTime>
                            <low value="20250101" />
                          </effectiveTime>
                        </marketingAct>
                      </subjectOf>
                    </containerPackagedProduct>
                  </asContent>
                </manufacturedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a section with paragraphs, list, table, excerpt, highlight, and nested section content.
        /// </summary>
        /// <returns>A section element with representative text structures.</returns>
        /// <seealso cref="SectionContentParser"/>
        public static XElement SectionWithRichText()
        {
            #region implementation
            return Element("""
                <section xmlns="urn:hl7-org:v3" ID="ID_test_section">
                  <id root="11111111-1111-1111-1111-111111111111" />
                  <code code="34067-9" codeSystem="2.16.840.1.113883.6.1" displayName="INDICATIONS AND USAGE" />
                  <title>INDICATIONS AND USAGE</title>
                  <text>
                    <paragraph>Ciprofloxacin is indicated for treatment.</paragraph>
                    <list>
                      <item>First item</item>
                      <item>Second item<list><item>Nested item</item></list></item>
                    </list>
                    <table>
                      <thead><tr><th>Arm</th><th>Dose</th></tr></thead>
                      <tbody><tr><td>Cipro</td><td>500 mg</td></tr></tbody>
                    </table>
                    <excerpt><highlight>Important safety text.</highlight></excerpt>
                  </text>
                  <component>
                    <section>
                      <id root="22222222-2222-2222-2222-222222222222" />
                      <title>Nested</title>
                      <text><paragraph>Nested paragraph.</paragraph></text>
                    </section>
                  </component>
                </section>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a section with observation media and rendered media references.
        /// </summary>
        /// <returns>A section element with media structures.</returns>
        /// <seealso cref="SectionMediaParser"/>
        public static XElement SectionWithMedia()
        {
            #region implementation
            var wrapper = Element("""
                <structuredBody xmlns="urn:hl7-org:v3">
                  <component>
                    <section>
                      <id root="33333333-3333-3333-3333-333333333333" />
                      <text>
                        <paragraph>Image: <renderMultimedia referencedObject="image-1" /></paragraph>
                        <renderMultimedia referencedObject="image-1" />
                      </text>
                    </section>
                  </component>
                  <component>
                    <observationMedia ID="image-1">
                      <text>Ciprofloxacin product image</text>
                      <value mediaType="image/jpeg" xsi:type="ED" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                        <reference value="ciprofloxacin.jpg" />
                      </value>
                    </observationMedia>
                  </component>
                </structuredBody>
                """);
            return wrapper.Descendants(Spl + "section").Single();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a lot hierarchy snippet for lot distribution parser tests.
        /// </summary>
        /// <returns>A representative lot hierarchy element.</returns>
        /// <seealso cref="LotDistributionParser"/>
        public static XElement LotHierarchy()
        {
            #region implementation
            return Element("""
                <subjectOf xmlns="urn:hl7-org:v3">
                  <productEvent>
                    <code code="C106325" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Distributed per reporting interval" />
                    <effectiveTime><low value="20250101" /></effectiveTime>
                  </productEvent>
                </subjectOf>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a dosing specification consumedIn snippet.
        /// </summary>
        /// <returns>A dosing specification parent element.</returns>
        /// <seealso cref="DosingSpecificationParser"/>
        public static XElement DosingSpecification()
        {
            #region implementation
            return Element("""
                <manufacturedProduct xmlns="urn:hl7-org:v3">
                  <consumedIn>
                    <substanceAdministration>
                      <routeCode code="C38288" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="ORAL" />
                      <doseQuantity value="500" unit="mg" />
                    </substanceAdministration>
                  </consumedIn>
                </manufacturedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a product event parent snippet with distributed and returned events.
        /// </summary>
        /// <returns>A product-event parent element.</returns>
        /// <seealso cref="ProductEventParser"/>
        public static XElement ProductEvents()
        {
            #region implementation
            return Element("""
                <containerPackagedProduct xmlns="urn:hl7-org:v3">
                  <subjectOf>
                    <quantity value="12" unit="1" />
                    <productEvent>
                      <code code="C106325" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Distributed per reporting interval" />
                      <effectiveTime><low value="20250101" /></effectiveTime>
                    </productEvent>
                  </subjectOf>
                  <subjectOf>
                    <quantity value="2" unit="1" />
                    <productEvent>
                      <code code="C106328" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Returned" />
                    </productEvent>
                  </subjectOf>
                </containerPackagedProduct>
                """);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates in-memory configuration values required by parser repositories and flags.
        /// </summary>
        /// <param name="overrides">Optional configuration overrides.</param>
        /// <returns>Configured application settings.</returns>
        /// <seealso cref="IConfiguration"/>
        private static IConfiguration buildConfiguration(Dictionary<string, string?>? overrides)
        {
            #region implementation
            var values = new Dictionary<string, string?>
            {
                ["Security:DB:PKSecret"] = "ParsingServicesTestsSecret123!",
                ["FeatureFlags:UseBulkOperations"] = "false",
                ["FeatureFlags:UseBulkStagingOperations"] = "false",
                ["FeatureFlags:UseBatchSaving"] = "false"
            };

            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies the import DbContext schema to SQLite with SQL Server DDL compatibility patches.
        /// </summary>
        /// <param name="context">Import database context used to generate the schema.</param>
        /// <param name="connection">Open SQLite connection receiving the schema.</param>
        /// <remarks>
        /// The import model carries SQL Server-oriented type metadata. This patches those type
        /// names into SQLite-compatible affinities, then executes statements one at a time so
        /// unsupported index fragments do not block table creation.
        /// </remarks>
        /// <seealso cref="ApplicationDbContext"/>
        private static void applySqliteSchema(ApplicationDbContext context, SqliteConnection connection)
        {
            #region implementation
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var ddl = context.Database.GenerateCreateScript();
            ddl = Regex.Replace(ddl, @"\b(n?varchar|nchar|char|varbinary|binary)\s*\(\s*max\s*\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\b(n?varchar|nchar|char|varbinary|binary)\s*\(\s*\d+\s*\)", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bdecimal\s*\(\s*\d+\s*,\s*\d+\s*\)", "REAL", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bdatetime2\s*(\(\s*\d+\s*\))?", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\bdatetimeoffset\s*(\(\s*\d+\s*\))?", "TEXT", RegexOptions.IgnoreCase);
            ddl = Regex.Replace(ddl, @"\buniqueidentifier\b", "TEXT", RegexOptions.IgnoreCase);
            ddl = ddl.Replace("SYSUTCDATETIME()", "datetime('now')");
            ddl = ddl.Replace("GETUTCDATE()", "datetime('now')");

            foreach (var statement in ddl.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = statement.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = trimmed;
                    command.ExecuteNonQuery();
                }
                catch (SqliteException ex) when (trimmed.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase)
                    || ex.SqliteErrorCode == 1)
                {
                    // Some SQL Server index fragments remain provider-specific after EF script generation.
                    // Parser tests assert row behavior, so unsupported nonessential indexes can be skipped.
                }
            }
            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Parser execution modes supported by the shared parser test helper.
    /// </summary>
    /// <seealso cref="SplParseContext"/>
    internal enum ParserMode
    {
        /// <summary>
        /// Single-call parser behavior with bulk flags disabled.
        /// </summary>
        SingleCall,

        /// <summary>
        /// Bulk parser behavior without staged section discovery.
        /// </summary>
        Bulk,

        /// <summary>
        /// Staged bulk parser behavior with deferred batch saves enabled.
        /// </summary>
        StagedBulk
    }

    /**************************************************************/
    /// <summary>
    /// Disposable SQLite-backed parser test database and service provider.
    /// </summary>
    /// <remarks>
    /// Holds both the sentinel and working connections required by SQLite shared-cache
    /// in-memory databases, plus helpers for parser context construction and core seed rows.
    /// </remarks>
    /// <seealso cref="ParsingServiceTestHelper"/>
    /// <seealso cref="ApplicationDbContext"/>
    internal sealed class ParserTestDatabase : IDisposable
    {
        #region implementation
        private readonly SqliteConnection _sentinelConnection;
        private readonly SqliteConnection _workingConnection;
        private bool _disposed;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new parser test database wrapper.
        /// </summary>
        /// <param name="sentinelConnection">Open sentinel connection keeping the database alive.</param>
        /// <param name="workingConnection">Open working connection used by the DbContext.</param>
        /// <param name="dbContext">Import DbContext for parser persistence.</param>
        /// <param name="serviceProvider">Service provider with parser repositories.</param>
        /// <param name="configuration">Configuration used by parser services.</param>
        /// <seealso cref="ParsingServiceTestHelper.CreateDatabase"/>
        public ParserTestDatabase(
            SqliteConnection sentinelConnection,
            SqliteConnection workingConnection,
            ApplicationDbContext dbContext,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            #region implementation
            _sentinelConnection = sentinelConnection;
            _workingConnection = workingConnection;
            DbContext = dbContext;
            ServiceProvider = serviceProvider;
            Configuration = configuration;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the import DbContext used by parser tests.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        public ApplicationDbContext DbContext { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the service provider that resolves parser repositories and configuration.
        /// </summary>
        /// <seealso cref="IServiceProvider"/>
        public IServiceProvider ServiceProvider { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the parser test configuration.
        /// </summary>
        /// <seealso cref="IConfiguration"/>
        public IConfiguration Configuration { get; }

        /**************************************************************/
        /// <summary>
        /// Creates a parser context configured for the requested parser mode.
        /// </summary>
        /// <param name="mode">Parser mode flags to apply.</param>
        /// <returns>A configured parser context.</returns>
        /// <seealso cref="ParserMode"/>
        /// <seealso cref="SplParseContext"/>
        public SplParseContext CreateParseContext(ParserMode mode = ParserMode.SingleCall)
        {
            #region implementation
            var context = new SplParseContext
            {
                DbContext = DbContext,
                ServiceProvider = ServiceProvider,
                Logger = NullLogger.Instance,
                FileNameInZip = ParsingServiceTestHelper.FullFixtureFileName,
                FileResult = new SplFileImportResult(),
                MainSectionParser = new SectionParser()
            };

            context.SetBulkOperationsFlag(mode is ParserMode.Bulk or ParserMode.StagedBulk);
            context.SetBulkStagingFlag(mode is ParserMode.StagedBulk);
            context.SetBatchSavingFlag(mode is ParserMode.StagedBulk);

            return context;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds the common parser context rows used by focused parser tests.
        /// </summary>
        /// <param name="context">Parser context to populate with current entities.</param>
        /// <returns>The seeded entity bundle.</returns>
        /// <seealso cref="SeededParserEntities"/>
        /// <seealso cref="SplParseContext"/>
        public async Task<SeededParserEntities> SeedCommonContextAsync(SplParseContext context)
        {
            #region implementation
            var document = new Document
            {
                DocumentGUID = Guid.Parse("f16d9f01-d515-40fe-a0ff-ac70627cb512"),
                SetGUID = Guid.Parse("cc768d3e-a7cc-46a2-ae6d-0f5eb2f05406"),
                DocumentCode = "34391-3",
                DocumentCodeSystem = "2.16.840.1.113883.6.1",
                DocumentDisplayName = "Human Prescription Drug Label",
                Title = "CIPROFLOXACIN tablet, for oral use",
                EffectiveTime = new DateTime(2025, 4, 30),
                VersionNumber = 9,
                SubmissionFileName = ParsingServiceTestHelper.FullFixtureFileName
            };

            DbContext.Set<Document>().Add(document);
            await DbContext.SaveChangesAsync();

            var structuredBody = new StructuredBody
            {
                DocumentID = document.DocumentID
            };

            DbContext.Set<StructuredBody>().Add(structuredBody);
            await DbContext.SaveChangesAsync();

            var section = new Section
            {
                DocumentID = document.DocumentID,
                StructuredBodyID = structuredBody.StructuredBodyID,
                SectionGUID = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                SectionCode = "34067-9",
                SectionCodeSystem = "2.16.840.1.113883.6.1",
                SectionDisplayName = "INDICATIONS AND USAGE",
                Title = "INDICATIONS AND USAGE"
            };

            DbContext.Set<Section>().Add(section);
            await DbContext.SaveChangesAsync();

            var product = new Product
            {
                SectionID = section.SectionID,
                ProductName = "CIPROFLOXACIN",
                FormCode = "C42998",
                FormCodeSystem = "2.16.840.1.113883.3.26.1.1",
                FormDisplayName = "TABLET, FILM COATED"
            };

            DbContext.Set<Product>().Add(product);
            await DbContext.SaveChangesAsync();

            var packagingLevel = new PackagingLevel
            {
                ProductID = product.ProductID,
                QuantityNumerator = 30,
                QuantityDenominator = 1,
                QuantityNumeratorUnit = "1",
                PackageCode = "50090-5373-0",
                PackageCodeSystem = "2.16.840.1.113883.6.69"
            };

            DbContext.Set<PackagingLevel>().Add(packagingLevel);
            await DbContext.SaveChangesAsync();

            var productIdentifier = new ProductIdentifier
            {
                ProductID = product.ProductID,
                IdentifierValue = "50090-5373",
                IdentifierSystemOID = "2.16.840.1.113883.6.69",
                IdentifierType = "NDC"
            };

            DbContext.Set<ProductIdentifier>().Add(productIdentifier);

            var parentOrganization = new Organization
            {
                OrganizationName = "A-S Medication Solutions"
            };

            var childOrganization = new Organization
            {
                OrganizationName = "Test Establishment"
            };

            DbContext.Set<Organization>().AddRange(parentOrganization, childOrganization);
            await DbContext.SaveChangesAsync();

            var documentRelationship = new DocumentRelationship
            {
                DocumentID = document.DocumentID,
                ParentOrganizationID = parentOrganization.OrganizationID,
                ChildOrganizationID = childOrganization.OrganizationID,
                RelationshipType = "LabelerToEstablishment",
                RelationshipLevel = 1
            };

            DbContext.Set<DocumentRelationship>().Add(documentRelationship);
            await DbContext.SaveChangesAsync();

            var businessOperation = new BusinessOperation
            {
                DocumentRelationshipID = documentRelationship.DocumentRelationshipID,
                PerformingOrganizationID = childOrganization.OrganizationID,
                OperationCode = "C73607",
                OperationCodeSystem = "2.16.840.1.113883.3.26.1.1",
                OperationDisplayName = "RELABEL"
            };

            DbContext.Set<BusinessOperation>().Add(businessOperation);
            await DbContext.SaveChangesAsync();

            var complianceAction = new ComplianceAction
            {
                SectionID = section.SectionID,
                DocumentRelationshipID = documentRelationship.DocumentRelationshipID,
                ActionCode = "C162847",
                ActionCodeSystem = "2.16.840.1.113883.3.26.1.1",
                ActionDisplayName = "Inactivated",
                EffectiveTimeLow = new DateTime(2025, 1, 1)
            };

            var license = new License
            {
                BusinessOperationID = businessOperation.BusinessOperationID,
                LicenseNumber = "LIC-12345",
                LicenseRootOID = "1.2.3.4.5",
                LicenseTypeCode = "C118777",
                LicenseTypeCodeSystem = "2.16.840.1.113883.3.26.1.1",
                LicenseTypeDisplayName = "State license"
            };

            DbContext.Set<ComplianceAction>().Add(complianceAction);
            DbContext.Set<License>().Add(license);
            await DbContext.SaveChangesAsync();

            context.Document = document;
            context.StructuredBody = structuredBody;
            context.CurrentSection = section;
            context.CurrentProduct = product;
            context.CurrentPackagingLevel = packagingLevel;
            context.CurrentProductIdentifier = productIdentifier;
            context.CurrentDocumentRelationship = documentRelationship;
            context.CurrentBusinessOperation = businessOperation;
            context.CurrentComplianceAction = complianceAction;
            context.CurrentLicense = license;

            return new SeededParserEntities(
                document,
                structuredBody,
                section,
                product,
                packagingLevel,
                productIdentifier,
                parentOrganization,
                childOrganization,
                documentRelationship,
                businessOperation,
                complianceAction,
                license);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Disposes the parser test database and its backing connections.
        /// </summary>
        /// <seealso cref="IDisposable"/>
        public void Dispose()
        {
            #region implementation
            if (_disposed)
            {
                return;
            }

            DbContext.Dispose();
            _workingConnection.Dispose();
            _sentinelConnection.Dispose();

            if (ServiceProvider is IDisposable disposableServiceProvider)
            {
                disposableServiceProvider.Dispose();
            }

            _disposed = true;
            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Bundle of common parser entities seeded into focused parser tests.
    /// </summary>
    /// <param name="Document">Seeded document.</param>
    /// <param name="StructuredBody">Seeded structured body.</param>
    /// <param name="Section">Seeded section.</param>
    /// <param name="Product">Seeded product.</param>
    /// <param name="PackagingLevel">Seeded packaging level.</param>
    /// <param name="ProductIdentifier">Seeded product identifier.</param>
    /// <param name="ParentOrganization">Seeded parent organization.</param>
    /// <param name="ChildOrganization">Seeded child organization.</param>
    /// <param name="DocumentRelationship">Seeded document relationship.</param>
    /// <param name="BusinessOperation">Seeded business operation.</param>
    /// <param name="ComplianceAction">Seeded compliance action.</param>
    /// <param name="License">Seeded license.</param>
    /// <seealso cref="ParserTestDatabase.SeedCommonContextAsync"/>
    internal sealed record SeededParserEntities(
        Document Document,
        StructuredBody StructuredBody,
        Section Section,
        Product Product,
        PackagingLevel PackagingLevel,
        ProductIdentifier ProductIdentifier,
        Organization ParentOrganization,
        Organization ChildOrganization,
        DocumentRelationship DocumentRelationship,
        BusinessOperation BusinessOperation,
        ComplianceAction ComplianceAction,
        License License);
}
