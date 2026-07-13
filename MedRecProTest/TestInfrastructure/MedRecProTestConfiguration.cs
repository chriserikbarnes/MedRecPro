using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Provides deterministic, non-production configuration for MedRecPro tests and test hosts.
/// </summary>
/// <remarks>
/// The values satisfy startup validation without reading machine configuration, developer secrets,
/// Key Vault, or production connection-string fallbacks. Database registrations are replaced by
/// the integration-test host before a context is resolved.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
public static class MedRecProTestConfiguration
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the fixed encryption secret used only by deterministic tests.
    /// </summary>
    /// <remarks>
    /// This value is deliberately non-production and exists only to exercise real encryption and
    /// DTO dictionary behavior without an external secret provider.
    /// </remarks>
    public const string PkSecret = "MedRecPro-Test-Only-PK-Secret";

    /**************************************************************/
    /// <summary>
    /// Gets a sentinel SQL Server connection string that must never be contacted by tests.
    /// </summary>
    /// <remarks>
    /// The host factory replaces both application contexts with SQLite before resolution. The
    /// deliberately invalid server name makes an accidental production or local SQL connection visible.
    /// </remarks>
    public const string SqlConnectionString = "Server=medrecpro-test-sentinel.invalid;Database=MedRecProTest;User Id=test;Password=test;TrustServerCertificate=True;Connect Timeout=1";

    private static readonly IReadOnlyDictionary<string, string?> testValues =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
        {
            ["Security:DB:PKSecret"] = PkSecret,
            ["ConnectionStrings:DefaultConnection"] = SqlConnectionString,
            ["Dev:DB:Connection"] = SqlConnectionString,
            ["Prod:DB:Connection"] = SqlConnectionString,
            ["KeyVaultUrl"] = string.Empty,
            ["ClaudeApiSettings:ApiKey"] = "test-api-key",
            ["Authentication:Google:ClientId"] = "test-google-client-id",
            ["Authentication:Google:ClientSecret"] = "test-google-client-secret",
            ["Authentication:Microsoft:ClientId"] = "test-microsoft-client-id",
            ["Authentication:Microsoft:ClientSecret:Dev"] = "test-microsoft-client-secret",
            ["Authentication:Microsoft:ClientSecret:Prod"] = "test-microsoft-client-secret",
            ["McpServer:Url"] = string.Empty,
            ["McpServer:JwtSigningKey"] = string.Empty,
            ["Logging:EventLog:LogLevel:Default"] = "None",
            ["FeatureFlags:BackgroundProcessingEnabled"] = "false",
            ["FeatureFlags:ActivityTrackingEnabled"] = "false",
            ["FeatureFlags:UseEnhancedDebugging"] = "false",
            ["DatabaseUsageMonitor:Enabled"] = "false",
            ["DatabaseKeepAlive:Enabled"] = "false",
            ["DemoModeSettings:Enabled"] = "false",
            ["TarpitSettings:Enabled"] = "false",
            ["TarpitSettings:TriggerThreshold"] = "1",
            ["TarpitSettings:MaxDelayMs"] = "0",
            ["TarpitSettings:StaleEntryTimeoutMinutes"] = "1",
            ["TarpitSettings:CleanupIntervalMinutes"] = "1",
            ["TarpitSettings:MaxTrackedIps"] = "1",
            ["TarpitSettings:EndpointRateThreshold"] = "1",
            ["TarpitSettings:EndpointWindowSeconds"] = "1",
            ["TarpitSettings:EndpointMonitoring:Enabled"] = "false",
            ["TarpitSettings:EndpointMonitoring:DefaultRateThreshold"] = "1",
            ["TarpitSettings:EndpointMonitoring:DefaultWindowSeconds"] = "1",
            ["TarpitSettings:EndpointMonitoring:DefaultMaxDelayMs"] = "0",
            ["TarpitSettings:EndpointMonitoring:Rules:0:Enabled"] = "false",
            ["TarpitSettings:EndpointMonitoring:Rules:1:Enabled"] = "false"
        });

    /**************************************************************/
    /// <summary>
    /// Gets the complete deterministic configuration value set.
    /// </summary>
    /// <remarks>
    /// Host tests add these values after normal application configuration so test values take
    /// precedence over checked-in, environment, and machine-level configuration providers.
    /// </remarks>
    /// <seealso cref="Create"/>
    public static IReadOnlyDictionary<string, string?> Values => testValues;

    /**************************************************************/
    /// <summary>
    /// Creates an in-memory configuration root from the deterministic test values.
    /// </summary>
    /// <returns>A configuration root suitable for service construction in unit tests.</returns>
    /// <remarks>
    /// Callers receive a new configuration root, preventing one test from mutating another
    /// test's provider state.
    /// </remarks>
    /// <seealso cref="Values"/>
    public static IConfiguration Create()
    {
        #region implementation

        return new ConfigurationBuilder()
            .AddInMemoryCollection(Values)
            .Build();

        #endregion
    }

    #endregion
}
