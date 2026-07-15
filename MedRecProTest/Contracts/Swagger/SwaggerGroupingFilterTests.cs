using MedRecPro.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json;

namespace MedRecProTest.Contracts.Swagger;

/**************************************************************/
/// <summary>
/// Verifies generic controller-name and Swagger-group metadata behavior independently of any API family.
/// </summary>
/// <remarks>
/// Synthetic controller actions cover normalization, nearest-wins resolution, operation tags, rendered-action filtering,
/// deterministic document-tag ordering, merge behavior, and description conflicts.
/// </remarks>
/// <seealso cref="FeatureControllerNameConvention"/>
/// <seealso cref="SwaggerGroupOperationFilter"/>
/// <seealso cref="SwaggerGroupDocumentFilter"/>
[TestClass]
[TestCategory("Contract")]
public class SwaggerGroupingFilterTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies both attributes reject blank names and normalize supplied metadata.
    /// </summary>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    /// <seealso cref="SwaggerGroupAttribute"/>
    [TestMethod]
    public void Attributes_NamesAndDescriptions_ValidateAndNormalize()
    {
        #region implementation

        Assert.ThrowsException<ArgumentException>(() => new FeatureControllerNameAttribute("  "));
        Assert.ThrowsException<ArgumentException>(() => new SwaggerGroupAttribute("\t"));

        var controllerName = new FeatureControllerNameAttribute("  Widgets  ");
        var describedGroup = new SwaggerGroupAttribute("  Widget Search  ", "  Finds widgets.  ");
        var undescribedGroup = new SwaggerGroupAttribute("Widget Search", "   ");

        Assert.AreEqual("Widgets", controllerName.ControllerName);
        Assert.AreEqual("Widget Search", describedGroup.Name);
        Assert.AreEqual("Finds widgets.", describedGroup.Description);
        Assert.IsNull(undescribedGroup.Description);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an arbitrary annotated controller is renamed while an unmarked controller is untouched.
    /// </summary>
    /// <seealso cref="FeatureControllerNameConvention"/>
    /// <seealso cref="WidgetsImplementationController"/>
    [TestMethod]
    public void FeatureControllerNameConvention_AnnotatedAndUnmarkedControllers_AppliesOnlyDeclaredName()
    {
        #region implementation

        var convention = new FeatureControllerNameConvention();
        var marked = createControllerModel(typeof(WidgetsImplementationController));
        var unmarked = createControllerModel(typeof(UnmarkedController));

        convention.Apply(marked);
        convention.Apply(unmarked);

        Assert.AreEqual("Widgets", marked.ControllerName);
        Assert.AreEqual("Unmarked", unmarked.ControllerName);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies method-level Swagger group metadata takes precedence over controller metadata.
    /// </summary>
    /// <seealso cref="SwaggerGroupMetadataResolver"/>
    /// <seealso cref="GroupedController"/>
    [TestMethod]
    public void SwaggerGroupMetadataResolver_MethodMetadata_TakesPrecedenceOverControllerMetadata()
    {
        #region implementation

        var classMethod = getGroupedMethod(nameof(GroupedController.ClassLevelGroup));
        var methodOverride = getGroupedMethod(nameof(GroupedController.MethodLevelGroup));

        var classMetadata = SwaggerGroupMetadataResolver.Resolve(classMethod);
        var methodMetadata = SwaggerGroupMetadataResolver.Resolve(methodOverride);

        Assert.IsNotNull(classMetadata);
        Assert.AreEqual("Controller Group", classMetadata.Name);
        Assert.IsNotNull(methodMetadata);
        Assert.AreEqual("Method Group", methodMetadata.Name);
        Assert.AreEqual("Method-level description.", methodMetadata.Description);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the operation filter replaces default tags with exactly the resolved Swagger group.
    /// </summary>
    /// <seealso cref="SwaggerGroupOperationFilter"/>
    [TestMethod]
    public void SwaggerGroupOperationFilter_MethodOverride_ReplacesOperationTags()
    {
        #region implementation

        var method = getGroupedMethod(nameof(GroupedController.MethodLevelGroup));
        var apiDescription = createApiDescription(method);
        var (schemaGenerator, schemaRepository) = createSchemaServices();
        var context = new OperationFilterContext(apiDescription, schemaGenerator, schemaRepository, method);
        var operation = new OpenApiOperation
        {
            Tags = new List<OpenApiTag>
            {
                new() { Name = "Default" },
                new() { Name = "Second Default" }
            }
        };

        new SwaggerGroupOperationFilter().Apply(operation, context);

        Assert.AreEqual(1, operation.Tags.Count);
        Assert.AreEqual("Method Group", operation.Tags[0].Name);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies null tag collections are created and newly described rendered groups are appended ordinally.
    /// </summary>
    /// <remarks>
    /// The ignored action is intentionally omitted from the rendered API descriptions and therefore must not create a tag.
    /// </remarks>
    /// <seealso cref="SwaggerGroupDocumentFilter"/>
    [TestMethod]
    public void SwaggerGroupDocumentFilter_NullTags_AddsRenderedDescriptionsInOrdinalOrder()
    {
        #region implementation

        var document = new OpenApiDocument { Tags = null! };
        var context = createDocumentFilterContext(
            getGroupedMethod(nameof(GroupedController.ZuluGroup)),
            getGroupedMethod(nameof(GroupedController.AlphaGroup)));

        new SwaggerGroupDocumentFilter().Apply(document, context);

        Assert.IsNotNull(document.Tags);
        CollectionAssert.AreEqual(new[] { "Alpha Group", "Zulu Group" }, document.Tags.Select(tag => tag.Name).ToArray());
        Assert.AreEqual("Alpha description.", document.Tags[0].Description);
        Assert.IsFalse(document.Tags.Any(tag => tag.Name == "Ignored Group"),
            "Non-rendered metadata must not create a document tag.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies existing order and nonblank descriptions are preserved while empty descriptions are filled.
    /// </summary>
    /// <seealso cref="SwaggerGroupDocumentFilter"/>
    [TestMethod]
    public void SwaggerGroupDocumentFilter_ExistingTags_MergesWithoutReorderingOrOverwriting()
    {
        #region implementation

        var document = new OpenApiDocument
        {
            Tags = new List<OpenApiTag>
            {
                new() { Name = "Existing Group", Description = "XML description." },
                new() { Name = "Alpha Group", Description = "" }
            }
        };
        var context = createDocumentFilterContext(
            getGroupedMethod(nameof(GroupedController.ExistingGroup)),
            getGroupedMethod(nameof(GroupedController.AlphaGroup)),
            getGroupedMethod(nameof(GroupedController.ZuluGroup)));

        new SwaggerGroupDocumentFilter().Apply(document, context);

        CollectionAssert.AreEqual(
            new[] { "Existing Group", "Alpha Group", "Zulu Group" },
            document.Tags.Select(tag => tag.Name).ToArray());
        Assert.AreEqual("XML description.", document.Tags[0].Description,
            "An existing nonblank XML description must win.");
        Assert.AreEqual("Alpha description.", document.Tags[1].Description,
            "An existing blank description should be filled.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies identical descriptions coalesce and conflicting descriptions fail explicitly.
    /// </summary>
    /// <seealso cref="SwaggerGroupDocumentFilter"/>
    [TestMethod]
    public void SwaggerGroupDocumentFilter_RepeatedAndConflictingDescriptions_EnforcesConsistency()
    {
        #region implementation

        var repeatedDocument = new OpenApiDocument();
        var repeatedContext = createDocumentFilterContext(
            getGroupedMethod(nameof(GroupedController.RepeatedGroupOne)),
            getGroupedMethod(nameof(GroupedController.RepeatedGroupTwo)));

        new SwaggerGroupDocumentFilter().Apply(repeatedDocument, repeatedContext);

        Assert.AreEqual(1, repeatedDocument.Tags.Count(tag => tag.Name == "Repeated Group"));
        Assert.AreEqual("Repeated description.", repeatedDocument.Tags.Single().Description);

        var conflictingContext = createDocumentFilterContext(
            getGroupedMethod(nameof(GroupedController.ConflictingGroupOne)),
            getGroupedMethod(nameof(GroupedController.ConflictingGroupTwo)));

        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            new SwaggerGroupDocumentFilter().Apply(new OpenApiDocument(), conflictingContext));
        StringAssert.Contains(exception.Message, "Conflicting Group");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a lightweight MVC controller model using conventional naming.
    /// </summary>
    /// <param name="controllerType">Synthetic controller type.</param>
    /// <returns>A controller model initialized to the implementation class name.</returns>
    private static ControllerModel createControllerModel(Type controllerType)
    {
        #region implementation

        const string suffix = "Controller";
        var controllerName = controllerType.Name.EndsWith(suffix, StringComparison.Ordinal)
            ? controllerType.Name[..^suffix.Length]
            : controllerType.Name;

        return new ControllerModel(
            controllerType.GetTypeInfo(),
            controllerType.GetCustomAttributes(inherit: true).Cast<object>().ToList())
        {
            ControllerName = controllerName
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets one public synthetic grouping action by name.
    /// </summary>
    /// <param name="methodName">Action method name.</param>
    /// <returns>Reflected action method.</returns>
    /// <seealso cref="GroupedController"/>
    private static MethodInfo getGroupedMethod(string methodName)
    {
        #region implementation

        return typeof(GroupedController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            ?? throw new InvalidOperationException($"Synthetic method {methodName} was not found.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a document-filter context containing only the supplied rendered actions.
    /// </summary>
    /// <param name="methods">Synthetic actions considered rendered by ApiExplorer.</param>
    /// <returns>A document filter context with a fresh schema repository.</returns>
    private static DocumentFilterContext createDocumentFilterContext(params MethodInfo[] methods)
    {
        #region implementation

        var (schemaGenerator, schemaRepository) = createSchemaServices();
        return new DocumentFilterContext(
            methods.Select(createApiDescription).ToArray(),
            schemaGenerator,
            schemaRepository);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates one API description backed by a controller action descriptor.
    /// </summary>
    /// <param name="method">Rendered controller action method.</param>
    /// <returns>An API description exposing the reflected method.</returns>
    private static ApiDescription createApiDescription(MethodInfo method)
    {
        #region implementation

        return new ApiDescription
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = method.DeclaringType!.GetTypeInfo(),
                MethodInfo = method
            }
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates real Swashbuckle schema services for filter contexts.
    /// </summary>
    /// <returns>A schema generator and fresh repository.</returns>
    private static (SchemaGenerator Generator, SchemaRepository Repository) createSchemaServices()
    {
        #region implementation

        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(new JsonSerializerOptions()));

        return (schemaGenerator, new SchemaRepository("v1"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Synthetic controller proving arbitrary public controller-name pinning.
    /// </summary>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [FeatureControllerName("  Widgets  ")]
    private sealed class WidgetsImplementationController : ControllerBase
    {
        #region implementation
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Synthetic controller proving unmarked MVC naming remains unchanged.
    /// </summary>
    private sealed class UnmarkedController : ControllerBase
    {
        #region implementation
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Synthetic controller supplying class-, method-, repeated-, conflicting-, and ignored-group metadata.
    /// </summary>
    /// <seealso cref="SwaggerGroupAttribute"/>
    [SwaggerGroup("Controller Group", "Controller-level description.")]
    private sealed class GroupedController : ControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Supplies only the controller-level group metadata.
        /// </summary>
        [HttpGet]
        public void ClassLevelGroup()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies method-level metadata that overrides the controller group.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Method Group", "Method-level description.")]
        public void MethodLevelGroup()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the ordinal-first described group.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Alpha Group", "Alpha description.")]
        public void AlphaGroup()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the ordinal-last described group.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Zulu Group", "Zulu description.")]
        public void ZuluGroup()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies metadata for an existing document tag.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Existing Group", "Attribute description.")]
        public void ExistingGroup()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the first identical repeated description.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Repeated Group", "Repeated description.")]
        public void RepeatedGroupOne()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the second identical repeated description.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Repeated Group", "Repeated description.")]
        public void RepeatedGroupTwo()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the first conflicting description.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Conflicting Group", "First description.")]
        public void ConflictingGroupOne()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies the second conflicting description.
        /// </summary>
        [HttpGet]
        [SwaggerGroup("Conflicting Group", "Second description.")]
        public void ConflictingGroupTwo()
        {
            #region implementation
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Supplies metadata that ApiExplorer ignores and the document filter must never discover globally.
        /// </summary>
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        [SwaggerGroup("Ignored Group", "Ignored description.")]
        public void IgnoredGroup()
        {
            #region implementation
            #endregion
        }

        #endregion
    }

    #endregion
}
