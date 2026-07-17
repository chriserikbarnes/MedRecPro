using MedRecPro.Data;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using MedRecPro.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using ImportApplicationDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Hosts the real MedRecPro ASP.NET Core pipeline with deterministic infrastructure substitutions.
/// </summary>
/// <remarks>
/// The factory preserves the production composition root while replacing outbound Claude access,
/// both SQL Server contexts, unsafe hosted workers, and request authentication only in the test
/// service provider. The two open SQLite sentinel connections keep their named in-memory databases
/// alive for the lifetime of the shared factory.
/// </remarks>
/// <seealso cref="Program"/>
/// <seealso cref="MedRecProTestConfiguration"/>
/// <seealso cref="TestAuthenticationHandler"/>
public sealed class MedRecProWebApplicationFactory : WebApplicationFactory<Program>
{
    #region implementation

    private readonly SqliteConnection applicationDatabaseConnection = createSharedMemoryConnection("application");
    private readonly SqliteConnection importDatabaseConnection = createSharedMemoryConnection("import");
    private readonly Dictionary<string, string?> originalEnvironmentValues = new(StringComparer.Ordinal);
    private readonly string hostEnvironment;
    private readonly bool throwOnTestRequest;
    private readonly IReadOnlyDictionary<string, string?> configurationOverrides;
    private bool startupEnvironmentRestored;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="MedRecProWebApplicationFactory"/> class.
    /// </summary>
    /// <remarks>
    /// Top-level startup reads configuration before the normal factory callback runs. The constructor
    /// therefore applies test-only process configuration before host creation and restores it during disposal.
    /// </remarks>
    /// <param name="environmentName">Host environment used for production-middleware contract coverage.</param>
    /// <param name="throwOnTestRequest">Whether to register the explicit test-only exception-pipeline probe.</param>
    /// <param name="configurationOverrides">Optional values that override the deterministic test configuration for this host only.</param>
    /// <seealso cref="applyStartupEnvironment"/>
    /// <seealso cref="TestExceptionThrowingStartupFilter"/>
    public MedRecProWebApplicationFactory(
        string environmentName = "Development",
        bool throwOnTestRequest = false,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        #region implementation

        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        hostEnvironment = environmentName;
        this.throwOnTestRequest = throwOnTestRequest;
        this.configurationOverrides = configurationOverrides == null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(configurationOverrides, StringComparer.Ordinal);
        applyStartupEnvironment();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the SQLite schema used by the real host and asynchronously seeds deterministic application data.
    /// </summary>
    /// <param name="seed">Seed operation that receives an EF Core context over the shared host database.</param>
    /// <returns>A task representing schema creation and the supplied seed operation.</returns>
    /// <remarks>
    /// This reuses the proven DtoLabelAccess SQLite DDL patching and view-backing-table setup, so HTTP tests
    /// can seed data through test fixtures while all assertions travel through the public API surface.
    /// </remarks>
    /// <seealso cref="DtoLabelAccessTestHelper.CreateTestContext"/>
    public async Task SeedApplicationDatabaseAsync(Func<ApplicationDbContext, Task> seed)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(seed);

        await using var context = DtoLabelAccessTestHelper.CreateTestContext(applicationDatabaseConnection);
        await seed(context);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Configures the test application before its real startup pipeline builds.
    /// </summary>
    /// <param name="builder">The host builder supplied by <see cref="WebApplicationFactory{TEntryPoint}"/>.</param>
    /// <remarks>
    /// Development is an explicit test-host environment, not a substitute for Release compilation.
    /// This keeps the Kestrel-style OpenAPI server value free of the IIS virtual <c>/api</c> prefix;
    /// Debug/Release route changes still come solely from the production compiler directives.
    /// </remarks>
    /// <seealso cref="ConfigureTestServices"/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        #region implementation

        builder.UseEnvironment(hostEnvironment);
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(MedRecProTestConfiguration.Values);
            configuration.AddInMemoryCollection(configurationOverrides);
        });
        builder.ConfigureTestServices(services =>
        {
            removeHostedService<ZipImportWorkerService>(services);
            removeHostedService<DemoModeService>(services);
            removeHostedService<DatabaseKeepAliveService>(services);
            removeHostedService<DatabaseUsageMonitorService>(services);
            // Activity persistence is covered by focused dispatcher tests, not shared HTTP-host execution.
            removeHostedService<ActivityLogDispatcherHostedService>(services);

            replaceWithSqlite<ApplicationDbContext>(services, applicationDatabaseConnection);
            replaceWithSqlite<ImportApplicationDbContext>(services, importDatabaseConnection);

            // Prevent every hosted test from reaching the configured Claude endpoint.
            services.RemoveAll<IClaudeApiService>();
            services.AddScoped<IClaudeApiService>(_ => Mock.Of<IClaudeApiService>());

            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            });
            services.PostConfigure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            {
                var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                        TestAuthenticationHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();

                options.DefaultPolicy = policy;
                options.AddPolicy("ApiAccess", policy);
            });

            if (throwOnTestRequest)
            {
                services.AddSingleton<IStartupFilter, TestExceptionThrowingStartupFilter>();
            }
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Releases the kept-open SQLite connections after the hosted application is disposed.
    /// </summary>
    /// <param name="disposing">Whether managed resources are being disposed.</param>
    /// <seealso cref="createSharedMemoryConnection"/>
    protected override void Dispose(bool disposing)
    {
        #region implementation

        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                restoreStartupEnvironment();
                applicationDatabaseConnection.Dispose();
                importDatabaseConnection.Dispose();
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Removes a specific application hosted-service descriptor without touching framework host services.
    /// </summary>
    /// <typeparam name="T">Unsafe hosted-service implementation type.</typeparam>
    /// <param name="services">Service collection being configured for the test host.</param>
    /// <seealso cref="IHostedService"/>
    private static void removeHostedService<T>(IServiceCollection services)
        where T : class, IHostedService
    {
        #region implementation

        var descriptor = services.SingleOrDefault(service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(T));

        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Replaces one production DbContext registration with a supplied SQLite in-memory connection.
    /// </summary>
    /// <typeparam name="TContext">DbContext type registered by the production composition root.</typeparam>
    /// <param name="services">Service collection being configured for the test host.</param>
    /// <param name="connection">Open sentinel connection that keeps the in-memory database alive.</param>
    /// <seealso cref="ApplicationDbContext"/>
    /// <seealso cref="ImportApplicationDbContext"/>
    private static void replaceWithSqlite<TContext>(IServiceCollection services, SqliteConnection connection)
        where TContext : DbContext
    {
        #region implementation

        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(options => options.UseSqlite(connection));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Opens a named shared-cache SQLite in-memory connection for the factory lifetime.
    /// </summary>
    /// <param name="databaseName">Stable factory-local name fragment for the database.</param>
    /// <returns>An open connection that prevents the named in-memory database from being destroyed.</returns>
    /// <seealso cref="replaceWithSqlite{TContext}"/>
    private static SqliteConnection createSharedMemoryConnection(string databaseName)
    {
        #region implementation

        var connection = new SqliteConnection(
            $"Data Source=file:medrecpro_{databaseName}_{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();

        return connection;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies deterministic configuration through the environment before top-level startup executes.
    /// </summary>
    /// <remarks>
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> discovers and invokes the top-level entry point before
    /// its ordinary web-host callbacks. Environment configuration is a standard application provider, so this
    /// narrow, restored override supplies the values required by startup validation at the correct boundary.
    /// </remarks>
    /// <seealso cref="restoreStartupEnvironment"/>
    private void applyStartupEnvironment()
    {
        #region implementation

        setStartupEnvironmentValue("ASPNETCORE_ENVIRONMENT", hostEnvironment);
        setStartupEnvironmentValue("DOTNET_ENVIRONMENT", hostEnvironment);

        foreach (var pair in MedRecProTestConfiguration.Values)
        {
            setStartupEnvironmentValue(pair.Key.Replace(":", "__", StringComparison.Ordinal), pair.Value);
        }

        foreach (var pair in configurationOverrides)
        {
            setStartupEnvironmentValue(pair.Key.Replace(":", "__", StringComparison.Ordinal), pair.Value);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Records and sets one process environment value used only while the shared factory is alive.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="value">Test value to apply.</param>
    /// <seealso cref="applyStartupEnvironment"/>
    private void setStartupEnvironmentValue(string name, string? value)
    {
        #region implementation

        originalEnvironmentValues.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Restores process environment values changed for top-level startup validation.
    /// </summary>
    /// <remarks>
    /// Restoration is idempotent so repeated test cleanup cannot leak test configuration into later work.
    /// </remarks>
    /// <seealso cref="applyStartupEnvironment"/>
    private void restoreStartupEnvironment()
    {
        #region implementation

        if (startupEnvironmentRestored)
        {
            return;
        }

        foreach (var pair in originalEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }

        startupEnvironmentRestored = true;

        #endregion
    }

    #endregion
}
