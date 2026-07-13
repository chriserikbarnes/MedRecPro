using MedRecPro.Data;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImportApplicationDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecProTest.Integration;

/**************************************************************/
/// <summary>
/// Exercises real MedRecPro startup, dependency injection, middleware, and Swagger through the shared test host.
/// </summary>
/// <remarks>
/// These are gray-box integration tests: they seed or inspect infrastructure through the factory but
/// assert the public hosted pipeline rather than calling controller actions directly.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
[DoNotParallelize]
[TestCategory("Integration")]
public class StartupSmokeTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies the real service provider starts with scope and build validation enabled.
    /// </summary>
    /// <remarks>
    /// Any captive dependency or missing registration fails while resolving the factory service provider.
    /// </remarks>
    /// <seealso cref="MedRecProHostFixture"/>
    [TestMethod]
    public void Host_StartsWithValidatedServiceProvider()
    {
        #region implementation

        var services = MedRecProHostFixture.Factory.Services;

        Assert.IsNotNull(services);
        Assert.IsNotNull(services.GetRequiredService<IServiceScopeFactory>());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies both production DbContext registrations resolve against SQLite rather than SQL Server.
    /// </summary>
    /// <remarks>
    /// The connections are opened by the factory's kept-open sentinels, so this check proves host
    /// resolution and connection use without creating a production database dependency.
    /// </remarks>
    /// <seealso cref="ApplicationDbContext"/>
    /// <seealso cref="ImportApplicationDbContext"/>
    [TestMethod]
    public void Host_ResolvesBothContextsAgainstSqlite()
    {
        #region implementation

        using var scope = MedRecProHostFixture.Factory.Services.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var importContext = scope.ServiceProvider.GetRequiredService<ImportApplicationDbContext>();

        Assert.IsInstanceOfType(applicationContext.Database.GetDbConnection(), typeof(SqliteConnection));
        Assert.IsInstanceOfType(importContext.Database.GetDbConnection(), typeof(SqliteConnection));
        Assert.IsTrue(applicationContext.Database.CanConnect());
        Assert.IsTrue(importContext.Database.CanConnect());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies startup configuration uses the deterministic sentinel values instead of machine configuration.
    /// </summary>
    /// <remarks>
    /// A blank Key Vault URL is also asserted so a Release-compiled test host cannot contact Key Vault.
    /// </remarks>
    /// <seealso cref="MedRecProTestConfiguration"/>
    [TestMethod]
    public void Host_UsesOnlyDeterministicConfigurationValues()
    {
        #region implementation

        var configuration = MedRecProHostFixture.Factory.Services.GetRequiredService<IConfiguration>();

        Assert.AreEqual(MedRecProTestConfiguration.PkSecret, configuration["Security:DB:PKSecret"]);
        Assert.AreEqual(MedRecProTestConfiguration.SqlConnectionString, configuration.GetConnectionString("DefaultConnection"));
        Assert.AreEqual(string.Empty, configuration["KeyVaultUrl"]);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies only the framework host service remains after unsafe application workers are removed.
    /// </summary>
    /// <remarks>
    /// This exact allowlist prevents a future application worker from silently starting in integration tests.
    /// </remarks>
    /// <seealso cref="IHostedService"/>
    [TestMethod]
    public void Host_RunsOnlyAllowlistedHostedServices()
    {
        #region implementation

        var actual = MedRecProHostFixture.Factory.Services
            .GetServices<IHostedService>()
            .Select(service => service.GetType().Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = new[] { "DataProtectionHostedService", "GenericWebHostService" };

        CollectionAssert.AreEquivalent(expected, actual,
            "An unreviewed hosted service is starting in the MedRecPro test host. Actual: " +
            string.Join(", ", actual));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Swagger is generated through the real startup and middleware pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecProWebApplicationFactory"/>
    [TestMethod]
    public async Task Host_ServesSwaggerJson()
    {
        #region implementation

        using var client = MedRecProHostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "\"openapi\"");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an unknown path traverses the real middleware pipeline and returns the hosted 404 response.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.Middleware.TarpitMiddleware"/>
    [TestMethod]
    public async Task Host_ActivatesMiddlewareForUnknownPath()
    {
        #region implementation

        using var client = MedRecProHostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/test-host-unknown-route");

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        #endregion
    }

    #endregion
}
