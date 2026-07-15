using MedRecPro.Api.Controllers;
using MedRecPro.Configuration;
using MedRecPro.Controllers;
using MedRecPro.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace MedRecProTest.Contracts.Label
{
    /**************************************************************/
    /// <summary>
    /// Route compatibility guard for the split Label controller surface.
    /// </summary>
    /// <remarks>
    /// These tests inspect MVC controller metadata without starting the application, keeping Debug and Release route checks
    /// isolated from production configuration fallback behavior.
    /// </remarks>
    /// <seealso cref="ApiControllerBase"/>
    /// <seealso cref="LabelFeatureControllerModelConvention"/>
    [TestClass]
    [TestCategory("Contract")]
    public class LabelControllerRouteCompatibilityTests
    {
        #region implementation

#if DEBUG
        private const string LabelRoutePrefix = "api/Label";
#else
        private const string LabelRoutePrefix = "Label";
#endif

        private static readonly NullabilityInfoContext NullabilityContext = new();

        /**************************************************************/
        /// <summary>
        /// Verifies every concrete controller inherits the build-conditional base route and does not declare its own type route.
        /// </summary>
        /// <remarks>
        /// This mechanically protects the Debug <c>api/[controller]</c> and Release <c>[controller]</c> route split from
        /// controller-level route leakage.
        /// </remarks>
        /// <seealso cref="ApiControllerBase"/>
        /// <seealso cref="RouteAttribute"/>
        [TestMethod]
        public void Controllers_AllConcreteControllers_InheritApiControllerBaseAndDeclareNoTypeRoutes()
        {
            #region implementation

            var controllerTypes = getConcreteControllerTypes();

            foreach (var controllerType in controllerTypes)
            {
                Assert.IsTrue(
                    typeof(ApiControllerBase).IsAssignableFrom(controllerType),
                    $"{controllerType.FullName} must derive from {nameof(ApiControllerBase)} to preserve build-conditional routing.");

                var declaredRoutes = controllerType.GetCustomAttributes<RouteAttribute>(inherit: false).ToArray();
                Assert.AreEqual(
                    0,
                    declaredRoutes.Length,
                    $"{controllerType.FullName} must not declare a type-level RouteAttribute.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the Label feature convention pins marked controllers to the original Label controller name.
        /// </summary>
        /// <remarks>
        /// The route table must not expose implementation names such as <c>LabelMarkdown</c> when actions are moved out of
        /// <see cref="LabelController"/>.
        /// </remarks>
        /// <seealso cref="LabelFeatureControllerAttribute"/>
        /// <seealso cref="LabelFeatureControllerModelConvention"/>
        [TestMethod]
        public void LabelFeatureConvention_MarkedControllers_ResolveToLabelControllerName()
        {
            #region implementation

            var expectedControllerNames = new[]
            {
                typeof(LabelComparisonController).FullName,
                typeof(LabelApplicationController).FullName,
                typeof(LabelClassificationController).FullName,
                typeof(LabelDocumentController).FullName,
                typeof(LabelIngredientController).FullName,
                typeof(LabelImportController).FullName,
                typeof(LabelMarkdownController).FullName,
                typeof(LabelMetadataController).FullName,
                typeof(LabelProductIdentifierController).FullName,
                typeof(LabelProductSearchController).FullName,
                typeof(LabelSearchController).FullName,
                typeof(LabelSectionNavigationController).FullName,
                typeof(LabelSectionController).FullName
            }
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

            var markedControllers = getLabelFeatureControllerTypes();

            CollectionAssert.AreEqual(expectedControllerNames, markedControllers.Select(type => type.FullName).ToArray());

            foreach (var controllerType in markedControllers)
            {
                var model = createControllerModel(controllerType);

                Assert.AreEqual(
                    LabelFeatureControllerModelConvention.LabelControllerName,
                    model.ControllerName,
                    $"{controllerType.FullName} must resolve to the legacy Label controller route name.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies split Label feature controllers declare explicit Swagger section tags.
        /// </summary>
        /// <remarks>
        /// Route compatibility stays tied to the legacy Label controller name, while the Swagger operation filter uses
        /// these tags to split the documentation page into feature sections.
        /// </remarks>
        /// <seealso cref="LabelFeatureSwaggerTagAttribute"/>
        /// <seealso cref="LabelFeatureSwaggerTagOperationFilter"/>
        [TestMethod]
        public void LabelFeatureSwaggerTags_MarkedControllers_HaveSectionTags()
        {
            #region implementation

            var expectedTags = new[]
            {
                "Label Comparison",
                "Label Documents",
                "Label Import",
                "Label Markdown",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Search",
                "Label Sections"
            };

            var actualTags = getLabelFeatureControllerTypes()
                .Select(controllerType =>
                {
                    var attributes = controllerType.GetCustomAttributes<LabelFeatureSwaggerTagAttribute>(inherit: true).ToArray();

                    Assert.AreEqual(
                        1,
                        attributes.Length,
                        $"{controllerType.FullName} must declare exactly one {nameof(LabelFeatureSwaggerTagAttribute)}.");

                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(attributes[0].Tag),
                        $"{controllerType.FullName} must declare a non-empty Swagger tag.");

                    return attributes[0].Tag;
                })
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expectedTags, actualTags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the normal API controller registration installs the Label feature controller-name convention.
        /// </summary>
        /// <remarks>
        /// This keeps the route-preserving convention attached to application startup instead of only to unit-test helpers.
        /// </remarks>
        /// <seealso cref="MedRecProMvcExtensions.AddMedRecProApiControllers(IServiceCollection, IConfiguration)"/>
        /// <seealso cref="LabelFeatureControllerModelConvention"/>
        [TestMethod]
        public void AddMedRecProApiControllers_DefaultRegistration_AddsLabelFeatureConvention()
        {
            #region implementation

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["IgnoreEmptyObjectsWhenSerializing"] = "false"
                })
                .Build();

            services.AddLogging();
            services.AddMedRecProApiControllers(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

            Assert.IsTrue(
                options.Conventions.OfType<LabelFeatureControllerModelConvention>().Any(),
                $"{nameof(LabelFeatureControllerModelConvention)} must be registered with MVC options.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the effective Label action route inventory remains stable while actions move between controllers.
        /// </summary>
        /// <remarks>
        /// Run this test in both Debug and Release configurations because <see cref="ApiControllerBase"/> changes its
        /// inherited route prefix at compile time.
        /// </remarks>
        /// <seealso cref="LabelController"/>
        /// <seealso cref="LabelMarkdownController"/>
        [TestMethod]
        public void LabelRoutes_AllPublicActions_MatchGoldenMasterInventory()
        {
            #region implementation

            var expected = new[]
            {
                route("DELETE", "{menuSelection}/{encryptedId}", "DeleteAsync(string menuSelection, string encryptedId)"),
                route("GET", "application-number/search", "SearchByApplicationNumber(string applicationNumber, int? pageNumber, int? pageSize)"),
                route("GET", "application-number/summaries", "GetApplicationNumberSummaries(string? marketingCategory, int? pageNumber, int? pageSize)"),
                route("GET", "complete/{pageNumber?}/{pageSize?}", "GetCompleteLabels(int pageNumber, int pageSize)"),
                route("GET", "comparison/analysis/{documentGuid}", "GetDocumentComparisonAnalysis(Guid documentGuid)"),
                route("GET", "comparison/progress/{operationId}", "GetComparisonProgress(string operationId)"),
                route("GET", "document/navigation", "GetDocumentNavigation(bool latestOnly, Guid? setGuid, int? pageNumber, int? pageSize)"),
                route("GET", "document/version-history/{setGuidOrDocumentGuid}", "GetDocumentVersionHistory(Guid setGuidOrDocumentGuid)"),
                route("GET", "drug-safety/dea-schedule", "GetDEAScheduleProducts(string? scheduleCode, int? pageNumber, int? pageSize)"),
                route("GET", "extract-product", "ExtractProductFromDescription(string description)"),
                route("GET", "generate/{documentGuid:guid}/{minify:bool}", "GenerateXmlDocument(Guid documentGuid, bool minify)"),
                route("GET", "guide", "GetAPIEndpointGuide(string? category)"),
                route("GET", "import/progress/{operationId}", "GetImportProgress(string operationId)"),
                route("GET", "indication/search", "SearchByIndication(string query, int maxProductsPerIndication)"),
                route("GET", "ingredient/active/summaries", "GetIngredientActiveSummaries(string? ingredient, int? minProductCount, int? pageNumber, int? pageSize)"),
                route("GET", "ingredient/advanced", "SearchIngredientsAdvanced(string? unii, string? substanceNameSearch, string? applicationNumber, string? applicationType, string? productNameSearch, bool? activeOnly, int? pageNumber, int? pageSize)"),
                route("GET", "ingredient/by-application", "SearchIngredientByApplicationNumber(string applicationNumber, int? pageNumber, int? pageSize)"),
                route("GET", "ingredient/inactive/summaries", "GetIngredientInactiveSummaries(string? ingredient, int? minProductCount, int? pageNumber, int? pageSize)"),
                route("GET", "ingredient/related", "GetRelatedIngredients(string? unii, string? substanceNameSearch, bool? isActive)"),
                route("GET", "ingredient/search", "SearchByIngredient(string? unii, string? substanceNameSearch, int? pageNumber, int? pageSize)"),
                route("GET", "ingredient/summaries", "GetIngredientSummaries(string? ingredient, int? minProductCount, int? pageNumber, int? pageSize)"),
                route("GET", "inventory/summary", "GetInventorySummary(string? category)"),
                route("GET", "labeler/search", "SearchByLabeler(string labelerNameSearch, int? pageNumber, int? pageSize)"),
                route("GET", "labeler/summaries", "GetLabelerSummaries(int? pageNumber, int? pageSize)"),
                route("GET", "markdown/display/{documentGuid:guid}", "GetCleanLabelMarkdown(Guid documentGuid)"),
                route("GET", "markdown/download/{documentGuid:guid}", "DownloadLabelMarkdown(Guid documentGuid)"),
                route("GET", "markdown/export/{documentGuid:guid}", "GetLabelMarkdownExport(Guid documentGuid)"),
                route("GET", "markdown/sections/{documentGuid:guid}", "GetLabelSectionMarkdown(Guid documentGuid, string? sectionCode)"),
                route("GET", "ndc/package/search", "SearchByPackageNDC(string packageCode, int? pageNumber, int? pageSize)"),
                route("GET", "ndc/search", "SearchByNDC(string productCode, int? pageNumber, int? pageSize)"),
                route("GET", "original/{documentGuid:guid}/{minify:bool}", "OriginalXmlDocument(Guid documentGuid, bool minify)"),
                route("GET", "pharmacologic-class/hierarchy", "GetPharmacologicClassHierarchy(int? pageNumber, int? pageSize)"),
                route("GET", "pharmacologic-class/search", "SearchByPharmacologicClass(string? query, string? classNameSearch, int maxProductsPerClass, int? pageNumber, int? pageSize)"),
                route("GET", "pharmacologic-class/summaries", "GetPharmacologicClassSummaries(bool useAiCache, int? pageNumber, int? pageSize)"),
                route("GET", "product/indications", "GetProductIndications(string? unii, string? productNameSearch, string? substanceNameSearch, string? indicationSearch, int? pageNumber, int? pageSize)"),
                route("GET", "product/latest", "GetProductLatestLabels(string? unii, string? productNameSearch, string? activeIngredientSearch, int? pageNumber, int? pageSize)"),
                route("GET", "product/latest/details", "GetProductLatestLabelDetails(string? unii, string? productNameSearch, string? activeIngredientSearch, string? sectionCode, int? pageNumber, int? pageSize)"),
                route("GET", "product/related", "GetRelatedProducts(int? sourceProductId, Guid? sourceDocumentGuid, string? relationshipType, int? pageNumber, int? pageSize)"),
                route("GET", "product/search", "SearchProductSummary(string productNameSearch, int? pageNumber, int? pageSize)"),
                route("GET", "section/search", "SearchBySectionCode(string sectionCode, int? pageNumber, int? pageSize)"),
                route("GET", "section/summaries", "GetSectionTypeSummaries(int? pageNumber, int? pageSize)"),
                route("GET", "section/content/{documentGuid}", "GetSectionContent(Guid documentGuid, Guid? sectionGuid, string? sectionCode, int? pageNumber, int? pageSize)"),
                route("GET", "section/{menuSelection}", "GetSection(string menuSelection, int? pageNumber, int? pageSize)"),
                route("GET", "sectionMenu", "GetLabelSectionMenu()"),
                route("GET", "single/{documentGuid}", "GetSingleCompleteLabel(Guid documentGuid)"),
                route("GET", "{menuSelection}/documentation", "GetSectionDocumentation(string menuSelection)"),
                route("GET", "{menuSelection}/{encryptedId}", "GetByIdAsync(string menuSelection, string encryptedId)"),
                route("POST", "comparison/analysis/{documentGuid}", "QueueDocumentComparisonAnalysis(Guid documentGuid, CancellationToken cancellationToken)"),
                route("POST", "import", "UploadSplZips(List<IFormFile> files, CancellationToken cancellationToken)"),
                route("POST", "{menuSelection}", "CreateAsync(string menuSelection, object? jsonData)"),
                route("PUT", "{menuSelection}/{encryptedId}", "UpdateAsync(string menuSelection, string encryptedId, object? jsonData)")
            }
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

            var actual = getLabelRouteInventory();

            Assert.AreEqual(
                expected.Length,
                actual.Length,
                "Label route inventory count changed." + Environment.NewLine +
                "Missing:" + Environment.NewLine + string.Join(Environment.NewLine, expected.Except(actual, StringComparer.Ordinal)) + Environment.NewLine +
                "Extra:" + Environment.NewLine + string.Join(Environment.NewLine, actual.Except(expected, StringComparer.Ordinal)));

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.AreEqual(
                    expected[index],
                    actual[index],
                    $"Label route inventory mismatch at index {index}.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies actions sharing the Label controller name keep unique action names for link generation.
        /// </summary>
        /// <seealso cref="UrlHelperExtensions.Action(IUrlHelper, string?, object?)"/>
        /// <seealso cref="LabelFeatureControllerModelConvention"/>
        [TestMethod]
        public void LabelRoutes_SharedControllerName_KeepsActionNamesUnique()
        {
            #region implementation

            var duplicateNames = getLabelActionMethods()
                .GroupBy(method => method.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.AreEqual(
                0,
                duplicateNames.Count,
                "Duplicate Label action names break Url.Action resolution: " + string.Join(", ", duplicateNames));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the reflected Label metadata inventory retains contract-critical binding, authorization, filter, and response details.
        /// </summary>
        /// <remarks>
        /// The complete generated OpenAPI snapshots own the 51-operation wire contract. This companion reflection inventory
        /// protects metadata that Swagger does not necessarily export, including database operation filters and role requirements.
        /// </remarks>
        /// <seealso cref="LabelRoutes_AllPublicActions_MatchGoldenMasterInventory"/>
        /// <seealso cref="MedRecProTest.Contracts.LabelOpenApiContractTests"/>
        [TestMethod]
        public void LabelRoutes_MetadataInventory_CapturesBindingDefaultsAuthorizationFiltersAndResponses()
        {
            #region implementation

            var inventory = getLabelActionMetadataInventory();

            Assert.AreEqual(51, inventory.Length, "Every public Label action must participate in metadata inventory coverage.");

            var completeLabels = inventory.Single(value => value.StartsWith(
                "LabelDocumentController.GetCompleteLabels ::", StringComparison.Ordinal));
            StringAssert.Contains(completeLabels, "pageNumber:Int32:optional=True:default=1:binding=inferred");
            StringAssert.Contains(completeLabels, "pageSize:Int32:optional=True:default=10:binding=inferred");

            var markdownDownload = inventory.Single(value => value.StartsWith(
                "LabelMarkdownController.DownloadLabelMarkdown ::", StringComparison.Ordinal));
            StringAssert.Contains(markdownDownload, "documentGuid:Guid:optional=False:default=<none>:binding=FromRouteAttribute");
            StringAssert.Contains(markdownDownload, "DatabaseLimitAttribute");
            StringAssert.Contains(markdownDownload, "DatabaseIntensiveAttribute");
            StringAssert.Contains(markdownDownload, "text/markdown");
            StringAssert.Contains(markdownDownload, "200:FileContentResult");

            var queueComparison = inventory.Single(value => value.StartsWith(
                "LabelComparisonController.QueueDocumentComparisonAnalysis ::", StringComparison.Ordinal));
            StringAssert.Contains(queueComparison, "AuthorizeAttribute");
            StringAssert.Contains(queueComparison, "RequireUserRoleAttribute");
            StringAssert.Contains(queueComparison, "202:ComparisonOperationStatus");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one golden-master route inventory row.
        /// </summary>
        /// <param name="verb">HTTP verb.</param>
        /// <param name="template">Action route template beneath the Label route prefix.</param>
        /// <param name="signature">Action method signature summary.</param>
        /// <returns>Formatted inventory row.</returns>
        /// <seealso cref="LabelRoutePrefix"/>
        private static string route(string verb, string template, string signature)
        {
            #region implementation
            return $"{verb} {LabelRoutePrefix}/{template} :: {signature}";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one deterministic inventory row containing reflected public-contract metadata for a Label action.
        /// </summary>
        /// <returns>All Label action metadata rows sorted by declaring type and method name.</returns>
        /// <remarks>
        /// This retains reflection only for public metadata inspection. It never invokes or mutates production members.
        /// </remarks>
        /// <seealso cref="getLabelActionMethods"/>
        private static string[] getLabelActionMetadataInventory()
        {
            #region implementation

            return getLabelActionMethods()
                .Select(method =>
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(formatParameterMetadata));
                    var attributes = method.GetCustomAttributes(inherit: true).ToArray();
                    var authorization = string.Join(", ", attributes
                        .Where(attribute => attribute is IAuthorizeData ||
                                            attribute.GetType().Name == "AllowAnonymousAttribute")
                        .Select(attribute => attribute.GetType().Name)
                        .OrderBy(name => name, StringComparer.Ordinal));
                    var filters = string.Join(", ", attributes
                        .Where(attribute => attribute.GetType().Name.Contains("Database", StringComparison.Ordinal) ||
                                            attribute.GetType().Name.Contains("Feature", StringComparison.Ordinal) ||
                                            attribute.GetType().Name.Contains("RequireUserRole", StringComparison.Ordinal))
                        .Select(attribute => attribute.GetType().Name)
                        .OrderBy(name => name, StringComparer.Ordinal));
                    var produces = string.Join(", ", attributes
                        .OfType<ProducesAttribute>()
                        .SelectMany(attribute => attribute.ContentTypes.Select(contentType => contentType.ToString()))
                        .OrderBy(contentType => contentType, StringComparer.Ordinal));
                    var responses = string.Join(", ", attributes
                        .OfType<ProducesResponseTypeAttribute>()
                        .Select(attribute => $"{attribute.StatusCode}:{attribute.Type?.Name ?? "<none>"}")
                        .OrderBy(value => value, StringComparer.Ordinal));

                    return $"{method.DeclaringType!.Name}.{method.Name} :: " +
                           $"parameters=[{parameters}] | authorization=[{authorization}] | filters=[{filters}] | " +
                           $"produces=[{produces}] | responses=[{responses}]";
                })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats one action parameter's public binding and default-value metadata.
        /// </summary>
        /// <param name="parameter">Reflected action parameter to describe.</param>
        /// <returns>A stable inventory fragment for the action parameter.</returns>
        /// <seealso cref="getLabelActionMetadataInventory"/>
        private static string formatParameterMetadata(ParameterInfo parameter)
        {
            #region implementation

            var binding = parameter.GetCustomAttributes(inherit: true)
                .Select(attribute => attribute.GetType().Name)
                .FirstOrDefault(name => name.StartsWith("From", StringComparison.Ordinal) &&
                                        name.EndsWith("Attribute", StringComparison.Ordinal))
                ?? "inferred";
            var defaultValue = parameter.HasDefaultValue
                ? Convert.ToString(parameter.DefaultValue, System.Globalization.CultureInfo.InvariantCulture)
                : "<none>";

            return $"{parameter.Name}:{parameter.ParameterType.Name}:optional={parameter.IsOptional}:default={defaultValue}:binding={binding}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the concrete controller types declared by the MedRecPro assembly.
        /// </summary>
        /// <returns>Concrete MVC controller types sorted by full name.</returns>
        /// <seealso cref="ControllerBase"/>
        private static IReadOnlyList<Type> getConcreteControllerTypes()
        {
            #region implementation
            return typeof(LabelController).Assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all Label action methods after applying the controller-name convention.
        /// </summary>
        /// <returns>Public Label action methods sorted by method name.</returns>
        /// <seealso cref="createControllerModel"/>
        private static IReadOnlyList<MethodInfo> getLabelActionMethods()
        {
            #region implementation
            return getConcreteControllerTypes()
                .Where(type => createControllerModel(type).ControllerName == LabelFeatureControllerModelConvention.LabelControllerName)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: false).Any())
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the controllers participating in the route-preserving Label feature split.
        /// </summary>
        /// <returns>Marked Label feature controllers sorted by full name.</returns>
        /// <seealso cref="LabelFeatureControllerAttribute"/>
        private static IReadOnlyList<Type> getLabelFeatureControllerTypes()
        {
            #region implementation
            return getConcreteControllerTypes()
                .Where(type => type.GetCustomAttributes<LabelFeatureControllerAttribute>(inherit: true).Any())
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the Label route inventory from reflected controller and action route attributes.
        /// </summary>
        /// <returns>Sorted route inventory rows.</returns>
        /// <seealso cref="RouteAttribute"/>
        /// <seealso cref="HttpMethodAttribute"/>
        private static string[] getLabelRouteInventory()
        {
            #region implementation
            return getConcreteControllerTypes()
                .Select(type => new { Type = type, Model = createControllerModel(type) })
                .Where(item => item.Model.ControllerName == LabelFeatureControllerModelConvention.LabelControllerName)
                .SelectMany(item => getActionRoutes(item.Type, item.Model.ControllerName))
                .OrderBy(route => route, StringComparer.Ordinal)
                .ToArray();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a lightweight controller model and applies the Label feature convention.
        /// </summary>
        /// <param name="controllerType">Controller type to model.</param>
        /// <returns>The configured controller model.</returns>
        /// <seealso cref="LabelFeatureControllerModelConvention"/>
        private static ControllerModel createControllerModel(Type controllerType)
        {
            #region implementation
            var attributes = controllerType.GetCustomAttributes(inherit: true).Cast<object>().ToList();
            var model = new ControllerModel(controllerType.GetTypeInfo(), attributes)
            {
                ControllerName = trimControllerSuffix(controllerType.Name)
            };

            new LabelFeatureControllerModelConvention().Apply(model);

            return model;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the action route rows for one controller type.
        /// </summary>
        /// <param name="controllerType">Controller type containing actions.</param>
        /// <param name="controllerName">Effective controller name after conventions.</param>
        /// <returns>Route inventory rows for the controller.</returns>
        /// <seealso cref="RouteAttribute"/>
        private static IEnumerable<string> getActionRoutes(Type controllerType, string controllerName)
        {
            #region implementation
            var controllerRoutes = controllerType
                .GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(attribute => replaceControllerToken(attribute.Template!, controllerName))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.AreEqual(
                1,
                controllerRoutes.Count,
                $"{controllerType.FullName} should resolve one inherited controller route.");

            var controllerRoute = controllerRoutes[0];

            foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var httpMethod in method.GetCustomAttributes<HttpMethodAttribute>(inherit: false))
                {
                    var verbs = string.Join(",", httpMethod.HttpMethods.OrderBy(verb => verb, StringComparer.Ordinal));
                    var fullRoute = combineRoute(controllerRoute, httpMethod.Template);
                    yield return $"{verbs} {fullRoute} :: {method.Name}({getParameterInventory(method)})";
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Replaces the controller token in an MVC route template.
        /// </summary>
        /// <param name="template">Route template containing the controller token.</param>
        /// <param name="controllerName">Effective controller name.</param>
        /// <returns>Template with the controller token resolved.</returns>
        /// <seealso cref="ControllerModel.ControllerName"/>
        private static string replaceControllerToken(string template, string controllerName)
        {
            #region implementation
            return template.Replace("[controller]", controllerName, StringComparison.Ordinal);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Combines controller and action route templates without changing route casing or constraints.
        /// </summary>
        /// <param name="controllerRoute">Resolved controller route template.</param>
        /// <param name="actionRoute">Action route template.</param>
        /// <returns>Combined route template.</returns>
        /// <seealso cref="HttpMethodAttribute.Template"/>
        private static string combineRoute(string controllerRoute, string? actionRoute)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(actionRoute))
            {
                return controllerRoute.Trim('/');
            }

            return $"{controllerRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats an action method's parameter list for route inventory comparison.
        /// </summary>
        /// <param name="method">Action method to inspect.</param>
        /// <returns>Comma-separated parameter type and name list.</returns>
        /// <seealso cref="friendlyTypeName"/>
        private static string getParameterInventory(MethodInfo method)
        {
            #region implementation
            return string.Join(", ", method.GetParameters()
                .Select(parameter => $"{friendlyTypeName(parameter)} {parameter.Name}"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a parameter type using C# aliases and nullable annotations where metadata is available.
        /// </summary>
        /// <param name="parameter">Parameter to inspect.</param>
        /// <returns>Friendly C# type name.</returns>
        /// <seealso cref="NullabilityInfoContext"/>
        private static string friendlyTypeName(ParameterInfo parameter)
        {
            #region implementation
            var type = parameter.ParameterType;
            var nullableValueType = Nullable.GetUnderlyingType(type);

            if (nullableValueType != null)
            {
                return friendlyTypeName(nullableValueType) + "?";
            }

            var typeName = friendlyTypeName(type);
            if (!type.IsValueType && NullabilityContext.Create(parameter).ReadState == NullabilityState.Nullable)
            {
                typeName += "?";
            }

            return typeName;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a type using common C# aliases and generic type argument names.
        /// </summary>
        /// <param name="type">Type to format.</param>
        /// <returns>Friendly C# type name without parameter nullability.</returns>
        /// <seealso cref="friendlyTypeName(ParameterInfo)"/>
        private static string friendlyTypeName(Type type)
        {
            #region implementation
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(object)) return "object";
            if (type == typeof(Guid)) return "Guid";
            if (type == typeof(CancellationToken)) return "CancellationToken";

            if (type.IsGenericType)
            {
                var typeName = type.Name[..type.Name.IndexOf('`')];
                var arguments = string.Join(", ", type.GetGenericArguments().Select(friendlyTypeName));
                return $"{typeName}<{arguments}>";
            }

            return type.Name;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes the conventional Controller suffix from a controller class name.
        /// </summary>
        /// <param name="typeName">Controller type name.</param>
        /// <returns>Conventional MVC controller name.</returns>
        /// <seealso cref="ControllerModel.ControllerName"/>
        private static string trimControllerSuffix(string typeName)
        {
            #region implementation
            const string suffix = "Controller";
            return typeName.EndsWith(suffix, StringComparison.Ordinal)
                ? typeName[..^suffix.Length]
                : typeName;
            #endregion
        }

        #endregion
    }
}
