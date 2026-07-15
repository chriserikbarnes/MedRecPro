using MedRecPro.Models;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json;

namespace MedRecProTest.Contracts.Label
{
    /**************************************************************/
    /// <summary>
    /// Exercises the Swagger document filter that force-registers Label and
    /// its public nested SPL types in the schema repository.
    /// </summary>
    /// <remarks>
    /// Uses a real Swashbuckle SchemaGenerator backed by a
    /// JsonSerializerDataContractResolver so schema generation matches the
    /// production SwaggerGen pipeline without hosting the application.
    /// Expected schema ids are derived by reflecting over
    /// typeof(Label).GetNestedTypes so the tests keep passing as the SPL
    /// model grows instead of hard-coding type counts.
    /// </remarks>
    /// <seealso cref="IncludeLabelNestedTypesDocumentFilter"/>
    /// <seealso cref="MedRecPro.Models.Label"/>
    [TestClass]
    [TestCategory("Contract")]
    public class OpenApiDocumentFilterTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies Apply registers a schema for every public nested type of
        /// Label plus the Label container itself, keyed by simple type name.
        /// </summary>
        /// <remarks>
        /// The default Swashbuckle SchemaIdSelector uses the simple type name,
        /// so each nested type appears in the repository under its own name.
        /// The filter mutates only the schema repository; the SwaggerGenerator
        /// copies repository schemas into the document after all filters run,
        /// so the document itself must remain untouched.
        /// </remarks>
        /// <seealso cref="IncludeLabelNestedTypesDocumentFilter"/>
        /// <seealso cref="MedRecPro.Models.Label"/>
        [TestMethod]
        public void IncludeLabelNestedTypesDocumentFilter_Apply_GeneratesLabelAndNestedTypeSchemas()
        {
            #region implementation
            // Arrange
            var context = createDocumentFilterContext();
            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo { Title = "Test", Version = "v1" },
                Paths = new OpenApiPaths()
            };
            var filter = new IncludeLabelNestedTypesDocumentFilter();
            var nestedTypes = typeof(MedRecPro.Models.Label).GetNestedTypes(BindingFlags.Public);

            // Act
            filter.Apply(document, context);

            // Assert - guard the reflection-derived expectation itself.
            Assert.IsTrue(nestedTypes.Length > 0, "Label should expose public nested types for the filter to register.");

            // Assert - every nested type is registered under its simple name.
            foreach (var nestedType in nestedTypes)
            {
                Assert.IsTrue(context.SchemaRepository.Schemas.ContainsKey(nestedType.Name),
                    $"Schema repository is missing a schema for nested type '{nestedType.Name}'.");
            }

            // Assert - the Label container class is registered as well.
            Assert.IsTrue(context.SchemaRepository.Schemas.ContainsKey(nameof(MedRecPro.Models.Label)),
                "Schema repository is missing the Label container schema.");
            Assert.IsTrue(context.SchemaRepository.Schemas.Count >= nestedTypes.Length + 1,
                "Schema repository should hold at least one schema per nested type plus Label.");

            // Assert - the document itself must not be mutated by the filter.
            Assert.IsNull(document.Components, "Apply must not attach components to the swagger document.");
            Assert.AreEqual(0, document.Paths.Count, "Apply must not add paths to the swagger document.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies Apply leaves a bare swagger document untouched, proving
        /// the schema repository is the only mutation target.
        /// </summary>
        /// <seealso cref="IncludeLabelNestedTypesDocumentFilter"/>
        [TestMethod]
        public void IncludeLabelNestedTypesDocumentFilter_Apply_BareDocument_DoesNotMutateDocument()
        {
            #region implementation
            // Arrange - bare document with no components, info, or paths.
            var context = createDocumentFilterContext();
            var document = new OpenApiDocument();
            var filter = new IncludeLabelNestedTypesDocumentFilter();

            // Act
            filter.Apply(document, context);

            // Assert - schemas land in the repository, not on the document.
            Assert.IsNull(document.Components, "Apply must not create document components.");
            Assert.IsTrue(context.SchemaRepository.Schemas.Count > 0,
                "Apply should populate the schema repository even for a bare document.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies calling Apply twice against the same repository is safe
        /// and does not duplicate or grow the registered schema set.
        /// </summary>
        /// <remarks>
        /// GenerateSchema returns the existing reference when a schema id is
        /// already registered, so a second pass must be a no-op on the count.
        /// </remarks>
        /// <seealso cref="IncludeLabelNestedTypesDocumentFilter"/>
        [TestMethod]
        public void IncludeLabelNestedTypesDocumentFilter_Apply_CalledTwice_KeepsSchemaRepositoryStable()
        {
            #region implementation
            // Arrange
            var context = createDocumentFilterContext();
            var document = new OpenApiDocument();
            var filter = new IncludeLabelNestedTypesDocumentFilter();

            // Act - first pass populates, second pass must be idempotent.
            filter.Apply(document, context);
            var firstPassCount = context.SchemaRepository.Schemas.Count;

            filter.Apply(document, context);
            var secondPassCount = context.SchemaRepository.Schemas.Count;

            // Assert
            Assert.IsTrue(firstPassCount > 0, "First Apply pass should register schemas.");
            Assert.AreEqual(firstPassCount, secondPassCount, "Second Apply pass must not change the schema count.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a real Swashbuckle document filter context backed by a
        /// fresh schema repository and a JSON data-contract schema generator.
        /// </summary>
        /// <returns>A context whose SchemaRepository starts empty.</returns>
        /// <remarks>
        /// Mirrors the production SwaggerGen wiring: default
        /// SchemaGeneratorOptions (simple-name schema ids) and a
        /// JsonSerializerDataContractResolver over default serializer options.
        /// </remarks>
        /// <seealso cref="DocumentFilterContext"/>
        /// <seealso cref="SchemaGenerator"/>
        private static DocumentFilterContext createDocumentFilterContext()
        {
            #region implementation
            var schemaGenerator = new SchemaGenerator(
                new SchemaGeneratorOptions(),
                new JsonSerializerDataContractResolver(new JsonSerializerOptions()));

            return new DocumentFilterContext(
                apiDescriptions: Array.Empty<ApiDescription>(),
                schemaGenerator: schemaGenerator,
                schemaRepository: new SchemaRepository("v1"));
            #endregion
        }

        #endregion
    }
}
