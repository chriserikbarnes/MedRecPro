using MedRecPro.Api.Controllers;
using MedRecPro.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace MedRecProTest.Contracts.Swagger;

/**************************************************************/
/// <summary>
/// Guards the Settings and Users route surfaces with build-specific golden-master inventories.
/// </summary>
/// <remarks>
/// The inventory contains only the HTTP verb, resolved route, and public method signature so later Swagger grouping
/// metadata changes cannot churn the route baseline.
/// </remarks>
/// <seealso cref="SettingsController"/>
/// <seealso cref="UsersController"/>
/// <seealso cref="ApiControllerBase"/>
[TestClass]
[TestCategory("Contract")]
public class SettingsAndUsersRouteCompatibilityTests
{
    #region implementation

#if DEBUG
    private const string SettingsRoutePrefix = "api/Settings";
    private const string UsersRoutePrefix = "api/Users";
#else
    private const string SettingsRoutePrefix = "Settings";
    private const string UsersRoutePrefix = "Users";
#endif

    private static readonly NullabilityInfoContext NullabilityContext = new();

    /**************************************************************/
    /// <summary>
    /// Verifies all Settings actions retain their current verb, route, and public signature.
    /// </summary>
    /// <remarks>
    /// Run in Debug and Release because <see cref="ApiControllerBase"/> supplies a compile-time route prefix.
    /// </remarks>
    /// <seealso cref="SettingsController"/>
    [TestMethod]
    public void SettingsRoutes_AllPublicActions_MatchGoldenMasterInventory()
    {
        #region implementation

        var expected = new[]
        {
            route("GET", SettingsRoutePrefix, "database-limits", "GetDatabaseLimits()"),
            route("GET", SettingsRoutePrefix, "demomode", "GetDemoModeStatus()"),
            route("GET", SettingsRoutePrefix, "features", "GetFeatures()"),
            route("GET", SettingsRoutePrefix, "info", "GetApplicationInfo()"),
            route("GET", SettingsRoutePrefix, "logs", "GetLogs(int pageNumber, int pageSize, string? minLevel)"),
            route("GET", SettingsRoutePrefix, "logs/by-category", "GetLogsByCategory(string category, int pageNumber, int pageSize)"),
            route("GET", SettingsRoutePrefix, "logs/by-date", "GetLogsByDate(DateTime startDate, DateTime endDate, int pageNumber, int pageSize)"),
            route("GET", SettingsRoutePrefix, "logs/by-user", "GetLogsByUser(string userId, int pageNumber, int pageSize)"),
            route("GET", SettingsRoutePrefix, "logs/categories", "GetLogCategories()"),
            route("GET", SettingsRoutePrefix, "logs/statistics", "GetLogStatistics()"),
            route("GET", SettingsRoutePrefix, "logs/users", "GetLogUsers()"),
            route("GET", SettingsRoutePrefix, "metrics/database-cost", "GetDatabaseMetrics()"),
            route("GET", SettingsRoutePrefix, "test/app-credential", "TestAppCredential()"),
            route("GET", SettingsRoutePrefix, "test/app-metrics-pipeline", "TestAppMetricsPipeline()"),
            route("POST", SettingsRoutePrefix, "clearmanagedcache", "ClearManagedCache()")
        }
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

        assertInventory(typeof(SettingsController), expected);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies all Users actions retain their current verb, route, and public signature.
    /// </summary>
    /// <remarks>
    /// Existing casing and literal route segments are intentionally frozen for compatibility.
    /// </remarks>
    /// <seealso cref="UsersController"/>
    [TestMethod]
    public void UsersRoutes_AllPublicActions_MatchGoldenMasterInventory()
    {
        #region implementation

        var expected = new[]
        {
            route("DELETE", UsersRoutePrefix, "{encryptedUserId}", "DeleteUser(string encryptedUserId)"),
            route("GET", UsersRoutePrefix, string.Empty, "GetAllUsers(bool includeDeleted, int skip, int take)"),
            route("GET", UsersRoutePrefix, "byemail", "GetUserByEmail(string email)"),
            route("GET", UsersRoutePrefix, "endpoint-stats", "GetEndpointStats(string controllerName, string? actionName, int limit)"),
            route("GET", UsersRoutePrefix, "me", "GetMe()"),
            route("GET", UsersRoutePrefix, "user/{encryptedUserId}/activity", "GetUserActivity(string encryptedUserId, int pageNumber, int pageSize)"),
            route("GET", UsersRoutePrefix, "user/{encryptedUserId}/activity/daterange", "GetUserActivityByDateRange(string encryptedUserId, DateTime startDate, DateTime endDate, int pageNumber, int pageSize)"),
            route("GET", UsersRoutePrefix, "{encryptedUserId}", "GetUser(string encryptedUserId)"),
            route("POST", UsersRoutePrefix, "authenticate", "AuthenticateUser(LoginRequestDto loginRequest)"),
            route("POST", UsersRoutePrefix, "resolve-mcp", "ResolveMcpUser(McpUserResolveRequest request)"),
            route("POST", UsersRoutePrefix, "rotate-password", "RotatePassword(RotatePasswordRequestDto rotatePasswordRequest)"),
            route("POST", UsersRoutePrefix, "signup", "SignUpUser(UserSignUpRequestDto signUpRequest)"),
            route("PUT", UsersRoutePrefix, "admin-update", "AdminUpdateUser(AdminUserUpdateDto adminUpdateData)"),
            route("PUT", UsersRoutePrefix, "{encryptedUserId}/profile", "UpdateUserProfile(string encryptedUserId, UserFacingUpdateDto profileUpdate)")
        }
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

        assertInventory(typeof(UsersController), expected);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Settings and Users retain their conventional controller names.
    /// </summary>
    /// <remarks>
    /// Neither controller needs or carries route-name pinning because its implementation class already matches its public
    /// route family.
    /// </remarks>
    /// <seealso cref="ControllerModel.ControllerName"/>
    [TestMethod]
    public void ControllerNames_SettingsAndUsers_ResolveFromClassNames()
    {
        #region implementation

        Assert.AreEqual("Settings", createControllerModel(typeof(SettingsController)).ControllerName);
        Assert.AreEqual("Users", createControllerModel(typeof(UsersController)).ControllerName);
        Assert.AreEqual(0,
            typeof(SettingsController).GetCustomAttributes<FeatureControllerNameAttribute>(inherit: true).Count(),
            "Settings must continue to resolve from its implementation class name.");
        Assert.AreEqual(0,
            typeof(UsersController).GetCustomAttributes<FeatureControllerNameAttribute>(inherit: true).Count(),
            "Users must continue to resolve from its implementation class name.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every Settings action declares one intent group and every used group owns one description source.
    /// </summary>
    /// <remarks>
    /// This reflection guard deliberately excludes route and response metadata; the route golden master and hosted
    /// OpenAPI snapshot independently protect those contracts.
    /// </remarks>
    /// <seealso cref="SettingsController"/>
    /// <seealso cref="SwaggerGroupAttribute"/>
    [TestMethod]
    public void SettingsSwaggerGroups_ApiActions_HaveReviewedIntentMetadata()
    {
        #region implementation

        var expectedGroupCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Settings Application Info"] = 4,
            ["Settings Cache"] = 1,
            ["Settings Diagnostics"] = 3,
            ["Settings Logs"] = 7
        };

        assertSwaggerGroupInventory(typeof(SettingsController), "Settings ", expectedGroupCounts);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Asserts one reflected route inventory against its reviewed golden master.
    /// </summary>
    /// <param name="controllerType">Controller type whose actions are inspected.</param>
    /// <param name="expected">Reviewed verb, path, and signature rows.</param>
    /// <seealso cref="getRouteInventory"/>
    private static void assertInventory(Type controllerType, string[] expected)
    {
        #region implementation

        var actual = getRouteInventory(controllerType);

        Assert.AreEqual(
            expected.Length,
            actual.Length,
            $"{controllerType.Name} route inventory count changed.{Environment.NewLine}" +
            $"Missing:{Environment.NewLine}{string.Join(Environment.NewLine, expected.Except(actual, StringComparer.Ordinal))}{Environment.NewLine}" +
            $"Extra:{Environment.NewLine}{string.Join(Environment.NewLine, actual.Except(expected, StringComparer.Ordinal))}");

        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], actual[index],
                $"{controllerType.Name} route inventory mismatch at index {index}.");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Asserts exact Swagger group counts, family prefixing, and single-description ownership for one controller.
    /// </summary>
    /// <param name="controllerType">Controller type whose API actions are inspected.</param>
    /// <param name="groupPrefix">Required family prefix for every action group.</param>
    /// <param name="expectedGroupCounts">Expected action count for every exact group name.</param>
    /// <seealso cref="SwaggerGroupAttribute"/>
    private static void assertSwaggerGroupInventory(
        Type controllerType,
        string groupPrefix,
        IReadOnlyDictionary<string, int> expectedGroupCounts)
    {
        #region implementation

        var groupedMetadata = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: false).Any())
            .Select(method =>
            {
                var attributes = method.GetCustomAttributes<SwaggerGroupAttribute>(inherit: true).ToArray();
                Assert.AreEqual(1, attributes.Length,
                    $"{controllerType.Name}.{method.Name} must declare exactly one {nameof(SwaggerGroupAttribute)}.");
                Assert.IsTrue(attributes[0].Name.StartsWith(groupPrefix, StringComparison.Ordinal),
                    $"{controllerType.Name}.{method.Name} group must start with '{groupPrefix}'.");
                return attributes[0];
            })
            .GroupBy(attribute => attribute.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(
            expectedGroupCounts.Keys.ToArray(),
            groupedMetadata.Keys.ToArray(),
            $"{controllerType.Name} Swagger group names changed.");

        foreach (var expectedGroup in expectedGroupCounts)
        {
            var metadata = groupedMetadata[expectedGroup.Key];
            Assert.AreEqual(expectedGroup.Value, metadata.Length,
                $"{expectedGroup.Key} action count changed.");
            Assert.AreEqual(1, metadata.Count(attribute => !string.IsNullOrWhiteSpace(attribute.Description)),
                $"{expectedGroup.Key} must have exactly one nonblank description source.");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds one golden-master route row.
    /// </summary>
    /// <param name="verb">HTTP verb.</param>
    /// <param name="prefix">Build-specific controller route prefix.</param>
    /// <param name="template">Action route template.</param>
    /// <param name="signature">Action method signature.</param>
    /// <returns>Formatted inventory row.</returns>
    private static string route(string verb, string prefix, string template, string signature)
    {
        #region implementation

        return $"{verb} {combineRoute(prefix, template)} :: {signature}";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a lightweight MVC controller model using conventional controller naming.
    /// </summary>
    /// <param name="controllerType">Controller type to model.</param>
    /// <returns>A controller model whose name is derived from the class name.</returns>
    /// <seealso cref="ControllerModel"/>
    private static ControllerModel createControllerModel(Type controllerType)
    {
        #region implementation

        return new ControllerModel(
            controllerType.GetTypeInfo(),
            controllerType.GetCustomAttributes(inherit: true).Cast<object>().ToList())
        {
            ControllerName = trimControllerSuffix(controllerType.Name)
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reflects all HTTP actions on one controller into deterministic route rows.
    /// </summary>
    /// <param name="controllerType">Controller type to inspect.</param>
    /// <returns>Ordinal-sorted route inventory rows.</returns>
    /// <seealso cref="HttpMethodAttribute"/>
    private static string[] getRouteInventory(Type controllerType)
    {
        #region implementation

        var model = createControllerModel(controllerType);
        var controllerRoutes = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template!.Replace("[controller]", model.ControllerName, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(1, controllerRoutes.Count,
            $"{controllerType.FullName} should resolve one inherited controller route.");

        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: false)
                .Select(attribute =>
                {
                    var verbs = string.Join(",", attribute.HttpMethods.OrderBy(verb => verb, StringComparer.Ordinal));
                    var fullRoute = combineRoute(controllerRoutes[0], attribute.Template);
                    return $"{verbs} {fullRoute} :: {method.Name}({getParameterInventory(method)})";
                }))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Combines controller and action templates without changing route casing or constraints.
    /// </summary>
    /// <param name="controllerRoute">Resolved controller route.</param>
    /// <param name="actionRoute">Optional action route.</param>
    /// <returns>Combined route template.</returns>
    private static string combineRoute(string controllerRoute, string? actionRoute)
    {
        #region implementation

        return string.IsNullOrWhiteSpace(actionRoute)
            ? controllerRoute.Trim('/')
            : $"{controllerRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Formats an action method's public parameter list.
    /// </summary>
    /// <param name="method">Action method to inspect.</param>
    /// <returns>Comma-separated friendly type and parameter names.</returns>
    /// <seealso cref="friendlyTypeName(ParameterInfo)"/>
    private static string getParameterInventory(MethodInfo method)
    {
        #region implementation

        return string.Join(", ", method.GetParameters()
            .Select(parameter => $"{friendlyTypeName(parameter)} {parameter.Name}"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Formats a parameter type with C# aliases and nullable annotations.
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
    /// Formats a reflected type using common C# aliases and generic arguments.
    /// </summary>
    /// <param name="type">Type to format.</param>
    /// <returns>Friendly type name without parameter nullability.</returns>
    /// <seealso cref="friendlyTypeName(ParameterInfo)"/>
    private static string friendlyTypeName(Type type)
    {
        #region implementation

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(object)) return "object";
        if (type == typeof(Guid)) return "Guid";

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
    /// Removes the conventional Controller suffix from a class name.
    /// </summary>
    /// <param name="typeName">Controller class name.</param>
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
