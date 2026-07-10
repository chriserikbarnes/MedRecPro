using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ImportDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Provides reusable SQLite fixtures, observations, and assertions for column-standardization tests.
    /// </summary>
    /// <remarks>
    /// The focused helper keeps database construction, product dictionary seeding, and
    /// assertion semantics out of the large service-behavior test file. Each factory
    /// creates an isolated shared-cache database so test data cannot leak between cases.
    /// </remarks>
    /// <seealso cref="ColumnStandardizationService"/>
    /// <seealso cref="ParsedObservation"/>
    internal static class ColumnStandardizationTestFixture
    {
        #region Test Data

        private static readonly string[] _seedProductNames = new[]
        {
            "Mycophenolate Mofetil", "EVISTA", "Dofetilide", "Placebo",
            "Omeprazole", "Ranitidine", "Topiramate", "Doxazosin",
            "Paroxetine", "Enoxaparin", "Imiquimod", "Pregabalin",
            "Bortezomib", "Tocilizumab", "Paclitaxel", "Nalmefene",
            "Glycopyrrolate", "Losartan", "KANUMA", "EMPAVELI",
            "LYTGOBI", "Risperidone", "Cetirizine", "Diltiazem",
            "Warfarin", "VIAGRA", "Alogliptin", "Venlafaxine",
            "Metformin", "Progesterone", "Clarithromycin", "Amoxicillin",
            "MYCAPSSA", "VARITHENA", "Vivelle", "Natesto"
        };

        private static readonly string[] _seedSubstanceNames = new[]
        {
            "mycophenolic acid", "raloxifene hydrochloride", "dofetilide",
            "omeprazole", "topiramate", "doxazosin mesylate",
            "azathioprine", "cyclosporine", "pregabalin"
        };

        #endregion Test Data

        #region Service Fixtures

        /**************************************************************/
        /// <summary>
        /// Creates an initialized standardization service backed by an isolated SQLite database.
        /// </summary>
        /// <returns>The service, its context, and the sentinel connection that owns the database lifetime.</returns>
        /// <remarks>
        /// Callers dispose the returned context and sentinel after their assertions. The open
        /// sentinel preserves the shared in-memory database while the service initializes.
        /// </remarks>
        /// <seealso cref="ColumnStandardizationService.InitializeAsync"/>
        internal static async Task<(ColumnStandardizationService service, ImportDbContext context, SqliteConnection sentinel)>
            createInitializedServiceAsync()
        {
            #region implementation

            var (sentinel, connection) = createSharedMemoryDb();
            var context = createImportContext(connection);
            seedDrugNames(connection);

            var mockLogger = new Mock<ILogger<ColumnStandardizationService>>();
            var service = new ColumnStandardizationService(context, mockLogger.Object);
            await service.InitializeAsync();

            return (service, context, sentinel);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an initialized standardization service with the AE category dictionary enabled.
        /// </summary>
        /// <returns>The service, its context, and the sentinel connection that owns the database lifetime.</returns>
        /// <seealso cref="AeParameterCategoryDictionaryService"/>
        /// <seealso cref="ColumnStandardizationService.InitializeAsync"/>
        internal static async Task<(ColumnStandardizationService service, ImportDbContext context, SqliteConnection sentinel)>
            createInitializedServiceWithDictionaryAsync()
        {
            #region implementation

            var (sentinel, connection) = createSharedMemoryDb();
            var context = createImportContext(connection);
            seedDrugNames(connection);

            var mockLogger = new Mock<ILogger<ColumnStandardizationService>>();
            var dictionary = new AeParameterCategoryDictionaryService();
            var service = new ColumnStandardizationService(context, mockLogger.Object, dictionary);
            await service.InitializeAsync();

            return (service, context, sentinel);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a pair of SQLite shared-cache in-memory connections.
        /// </summary>
        /// <returns>The sentinel connection and a service connection.</returns>
        /// <remarks>
        /// The sentinel stays open until the test completes, which keeps the named memory
        /// database alive even if the service context disposes its connection.
        /// </remarks>
        private static (SqliteConnection sentinel, SqliteConnection connection) createSharedMemoryDb()
        {
            #region implementation

            var dbName = $"file:colstd_{Guid.NewGuid():N}?mode=memory&cache=shared";
            var connectionString = $"DataSource={dbName}";

            var sentinel = new SqliteConnection(connectionString);
            sentinel.Open();

            var connection = new SqliteConnection(connectionString);
            connection.Open();

            return (sentinel, connection);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a SQLite-backed import context with the drug-dictionary backing table.
        /// </summary>
        /// <param name="connection">The open SQLite connection used by the context.</param>
        /// <returns>A configured import database context.</returns>
        /// <seealso cref="ImportDbContext"/>
        private static ImportDbContext createImportContext(SqliteConnection connection)
        {
            #region implementation

            var options = new DbContextOptionsBuilder<ImportDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new ImportDbContext(options);

            // EF Core ToView entities do not appear in generated SQLite DDL, so tests create
            // the view's backing shape explicitly before seeding the product dictionary.
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""vw_ProductsByIngredient"" (
                    ""IngredientSubstanceID"" INTEGER,
                    ""UNII"" TEXT,
                    ""SubstanceName"" TEXT,
                    ""IngredientType"" TEXT,
                    ""IngredientID"" INTEGER,
                    ""IngredientClassCode"" TEXT,
                    ""QuantityNumerator"" REAL,
                    ""QuantityNumeratorUnit"" TEXT,
                    ""QuantityDenominator"" REAL,
                    ""StrengthDisplayName"" TEXT,
                    ""IngredientSequence"" INTEGER,
                    ""ActiveMoietyID"" INTEGER,
                    ""MoietyUNII"" TEXT,
                    ""MoietyName"" TEXT,
                    ""ProductID"" INTEGER,
                    ""ProductName"" TEXT,
                    ""DosageFormCode"" TEXT,
                    ""DosageFormName"" TEXT,
                    ""DocumentID"" INTEGER
                )";
            command.ExecuteNonQuery();

            return context;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds product and substance names used by the standardization dictionary.
        /// </summary>
        /// <param name="connection">The open SQLite connection containing the backing table.</param>
        /// <seealso cref="ColumnStandardizationService.InitializeAsync"/>
        private static void seedDrugNames(SqliteConnection connection)
        {
            #region implementation

            foreach (var name in _seedProductNames)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO \"vw_ProductsByIngredient\" (\"ProductName\") VALUES ($name)";
                command.Parameters.AddWithValue("$name", name);
                command.ExecuteNonQuery();
            }

            foreach (var name in _seedSubstanceNames)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO \"vw_ProductsByIngredient\" (\"SubstanceName\") VALUES ($name)";
                command.Parameters.AddWithValue("$name", name);
                command.ExecuteNonQuery();
            }

            #endregion
        }

        #endregion Service Fixtures

        #region Observation Builders

        /**************************************************************/
        /// <summary>
        /// Creates a minimal adverse-event observation for correction-rule tests.
        /// </summary>
        /// <param name="treatmentArm">The treatment-arm value to exercise.</param>
        /// <param name="studyContext">Optional study context.</param>
        /// <param name="doseRegimen">Optional dose regimen.</param>
        /// <param name="armN">Optional treatment-arm sample size.</param>
        /// <param name="parameterSubtype">Optional parameter subtype.</param>
        /// <param name="category">Table category; defaults to adverse events.</param>
        /// <param name="productTitle">Optional product-title fallback.</param>
        /// <returns>A deterministic parsed observation.</returns>
        /// <seealso cref="ParsedObservation"/>
        internal static ParsedObservation createObservation(
            string? treatmentArm,
            string? studyContext = null,
            string? doseRegimen = null,
            int? armN = null,
            string? parameterSubtype = null,
            string category = "ADVERSE_EVENT",
            string? productTitle = null)
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = category,
                ParameterName = "Nausea",
                ParameterCategory = "Gastrointestinal disorders",
                TreatmentArm = treatmentArm,
                StudyContext = studyContext,
                DoseRegimen = doseRegimen,
                ArmN = armN,
                ParameterSubtype = parameterSubtype,
                ProductTitle = productTitle,
                TextTableID = 100,
                RawValue = "15.3",
                PrimaryValue = 15.3,
                PrimaryValueType = "Percentage"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a PK observation with explicit contract fields for PK-routing tests.
        /// </summary>
        /// <param name="parameterName">The parameter name to exercise.</param>
        /// <param name="parameterSubtype">The parameter subtype to exercise.</param>
        /// <param name="treatmentArm">Optional treatment arm.</param>
        /// <param name="dose">Optional dose regimen.</param>
        /// <param name="unit">Optional measurement unit.</param>
        /// <param name="population">Optional population.</param>
        /// <returns>A deterministic PK observation.</returns>
        /// <seealso cref="ParsedObservation"/>
        internal static ParsedObservation createPkObservation(
            string? parameterName,
            string? parameterSubtype,
            string? treatmentArm = null,
            string? dose = null,
            string? unit = null,
            string? population = null)
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = parameterName,
                ParameterSubtype = parameterSubtype,
                TreatmentArm = treatmentArm,
                DoseRegimen = dose,
                Unit = unit,
                Population = population,
                TextTableID = 999,
                RawValue = "123",
                PrimaryValue = 123.0,
                PrimaryValueType = "Mean",
                ParseConfidence = 0.9
            };

            #endregion
        }

        #endregion Observation Builders

        #region Assertions

        /**************************************************************/
        /// <summary>
        /// Asserts that an observation contains the expected validation flag.
        /// </summary>
        /// <param name="observation">The observation to inspect.</param>
        /// <param name="expectedFlag">The expected validation flag.</param>
        /// <seealso cref="ParsedObservation.ValidationFlags"/>
        internal static void assertHasFlag(ParsedObservation observation, string expectedFlag)
        {
            #region implementation

            var validationFlags = observation.ValidationFlags;
            if (validationFlags is null)
            {
                Assert.Fail($"Expected ValidationFlags to contain '{expectedFlag}' but was null");
                return;
            }

            Assert.IsTrue(validationFlags.Contains(expectedFlag),
                $"Expected ValidationFlags to contain '{expectedFlag}' but was '{validationFlags}'");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that an observation does not contain a validation flag.
        /// </summary>
        /// <param name="observation">The observation to inspect.</param>
        /// <param name="unexpectedFlag">The validation flag that must be absent.</param>
        /// <param name="message">Failure message if the flag is present.</param>
        /// <seealso cref="ParsedObservation.ValidationFlags"/>
        internal static void assertFlagAbsent(ParsedObservation observation, string unexpectedFlag, string message)
        {
            #region implementation

            var validationFlags = observation.ValidationFlags;
            if (validationFlags is null)
                return;

            Assert.IsFalse(validationFlags.Contains(unexpectedFlag), message);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that an observation contains no column-standardization flags.
        /// </summary>
        /// <param name="observation">The observation to inspect.</param>
        /// <seealso cref="ParsedObservation.ValidationFlags"/>
        internal static void assertNoFlags(ParsedObservation observation)
        {
            #region implementation

            if (observation.ValidationFlags != null)
            {
                Assert.IsFalse(observation.ValidationFlags.Contains("COL_STD"),
                    $"Expected no COL_STD flags but found '{observation.ValidationFlags}'");
            }

            #endregion
        }

        #endregion Assertions
    }
}
