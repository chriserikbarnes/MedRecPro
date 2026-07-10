using MedRecPro.Models;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides structured startup diagnostics after the MedRecPro service provider has been built.
    /// </summary>
    /// <remarks>
    /// Service-registration delegates run before an application logger is available. This extension re-evaluates the
    /// non-secret configuration conditions that affect optional authentication and Swagger documentation, then records
    /// them through the application's configured <see cref="ILogger"/> pipeline.
    /// </remarks>
    /// <seealso cref="MedRecProAuthenticationExtensions"/>
    /// <seealso cref="MedRecProSwaggerExtensions"/>
    internal static class MedRecProStartupDiagnosticsExtensions
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Writes structured diagnostics for optional authentication providers and XML documentation files.
        /// </summary>
        /// <param name="app">Built application whose logger and configuration are used for diagnostics.</param>
        /// <returns>The same application for composition-root chaining.</returns>
        /// <remarks>
        /// The Microsoft client-secret lookup intentionally retains the Debug/production directive split used during
        /// authentication registration. No endpoint mappings or route templates are created by this method.
        /// </remarks>
        /// <seealso cref="MedRecProAuthenticationExtensions.AddMedRecProAuth(IServiceCollection, IConfiguration)"/>
        /// <seealso cref="MedRecProSwaggerExtensions.AddMedRecProSwagger(IServiceCollection, IConfiguration)"/>
        internal static WebApplication LogMedRecProStartupDiagnostics(this WebApplication app)
        {
            #region implementation

            var configuration = app.Configuration;

            if (string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) ||
                string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]))
            {
                app.Logger.LogWarning(
                    "Google authentication is disabled because its client ID or client secret is not configured");
            }

#if DEBUG
            var microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret:Dev"];
#else
            var microsoftClientSecret = configuration["Authentication:Microsoft:ClientSecret:Prod"];
#endif

            if (string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientId"]) ||
                string.IsNullOrWhiteSpace(microsoftClientSecret))
            {
                app.Logger.LogWarning(
                    "Microsoft authentication is disabled because its client ID or client secret is not configured");
            }

            var mcpServerUrl = configuration["McpServer:Url"];
            var mcpSigningKey = configuration["McpServer:JwtSigningKey"];
            if (!string.IsNullOrWhiteSpace(mcpServerUrl) && !string.IsNullOrWhiteSpace(mcpSigningKey))
            {
                app.Logger.LogInformation(
                    "MCP JWT authentication is enabled for issuer {McpServerUrl}",
                    mcpServerUrl);
            }
            else
            {
                app.Logger.LogInformation(
                    "MCP JWT authentication is not configured because its server URL or signing key is missing");
            }

            logMissingDocumentationFile(
                app,
                $"{typeof(Label).Assembly.GetName().Name}.xml",
                "data models");
            logMissingDocumentationFile(
                app,
                $"{typeof(global::Program).Assembly.GetName().Name}.xml",
                "API");

            return app;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs a warning when a Swagger XML documentation file is not available in the application directory.
        /// </summary>
        /// <param name="app">Built application that owns the content and output directory context.</param>
        /// <param name="fileName">XML documentation file expected by Swagger configuration.</param>
        /// <param name="sourceName">Human-readable source name used in the structured log record.</param>
        /// <seealso cref="MedRecProSwaggerExtensions"/>
        private static void logMissingDocumentationFile(WebApplication app, string fileName, string sourceName)
        {
            #region implementation

            var filePath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(filePath))
            {
                app.Logger.LogWarning(
                    "Swagger XML documentation file is missing for {DocumentationSource} at {DocumentationPath}",
                    sourceName,
                    filePath);
            }

            #endregion
        }

        #endregion
    }
}
