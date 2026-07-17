using MedRecPro.Filters;
using MedRecPro.Api.Controllers;
using MedRecPro.Exceptions;
using MedRecPro.Models;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides composition-root extension methods for MedRecPro Swagger/OpenAPI service and middleware setup.
    /// </summary>
    /// <remarks>
    /// Swagger server URL selection intentionally remains runtime-environment based while controller route prefixes remain compile-time based.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMedRecProSwagger(configuration);
    /// app.UseMedRecProSwagger();
    /// </code>
    /// </example>
    /// <seealso cref="OpenApiInfo"/>
    /// <seealso cref="SwaggerBuilderExtensions"/>
    public static class MedRecProSwaggerExtensions
    {
        private const string SwaggerDescriptionResourceName = "MedRecPro.SwaggerDocs.txt";

        #region implementation

        /**************************************************************/
        /// <summary>
        /// Registers Swagger/OpenAPI document generation and security metadata.
        /// </summary>
        /// <remarks>
        /// The compile-time environment labels, demo banner configuration keys, XML comment inclusion, and Basic authentication scheme match the previous startup behavior.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProSwagger(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Configuration containing version and demo-mode settings.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="IncludeLabelNestedTypesDocumentFilter"/>
        /// <seealso cref="OpenApiSecurityScheme"/>
        /// <seealso cref="SwaggerTagDocumentationDocumentFilter"/>
        public static IServiceCollection AddMedRecProSwagger(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            services.AddSwaggerGen(c =>
            {
                #region environment configuration
#if DEBUG || DEV
                var environment = "Dev";
                var serverName = "Localhost";
#elif RELEASE
                var environment = "Prod";
                var serverName = "ProdHost";
#endif
                #endregion

                #region demo mode detection
                // Check if demo mode is enabled from configuration
                var demoModeEnabled = configuration.GetValue<bool>("DemoModeSettings:Enabled", false);
                var demoRefreshInterval = configuration.GetValue<int>("DemoModeSettings:RefreshIntervalMinutes", 60);
                var version = configuration.GetValue<string>("Version");

                // Build demo mode warning banner if enabled
                var demoModeWarning = demoModeEnabled
                    ? $@"
---
## ⚠️ **DEMO MODE ACTIVE** ⚠️
**This system is running in DEMO MODE.**
- Database is automatically truncated every **{demoRefreshInterval} minutes**
- User authentication and activity logs are preserved
- All other data will be periodically removed
- DO NOT use for production data
---
"
                    : string.Empty;
                #endregion

                c.DocumentFilter<IncludeLabelNestedTypesDocumentFilter>();
                c.OperationFilter<SwaggerGroupOperationFilter>();

                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = $"{version}",
                    Title = "MedRecPro API",
                    Description = getSwaggerDescription(demoModeWarning, environment, serverName)
                });

                c.AddSecurityDefinition("BasicAuthentication", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Basic",
                    In = ParameterLocation.Header,
                    Description = "Basic Authorization header using the Basic scheme. Example: \"Authorization: Basic {base64(email:password)}\""
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "BasicAuthentication"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
                // Set the comments path for the Swagger JSON and UI.
                var dataModelsXmlFile = $"{typeof(Label).Assembly.GetName().Name}.xml";
                var dataModelsXmlPath = Path.Combine(AppContext.BaseDirectory, dataModelsXmlFile);
                if (File.Exists(dataModelsXmlPath))
                {
                    c.IncludeXmlComments(dataModelsXmlPath);

                    // Optional: To include comments from <inheritdoc/> tags
                    c.IncludeXmlComments(dataModelsXmlPath, includeControllerXmlComments: true);
                }
                else
                {
                    // The built application records missing XML documentation through ILogger startup diagnostics.
                }

                var apiXmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var apiXmlPath = Path.Combine(AppContext.BaseDirectory, apiXmlFile);
                if (File.Exists(apiXmlPath))
                {
                    c.IncludeXmlComments(apiXmlPath);
                    c.IncludeXmlComments(apiXmlPath, includeControllerXmlComments: true);
                }
                else
                {
                    // The built application records missing XML documentation through ILogger startup diagnostics.
                }

                // Merge optional group descriptions after XML comments have contributed their document-level tags.
                c.DocumentFilter<SwaggerGroupDocumentFilter>();
                c.DocumentFilter<SwaggerTagDocumentationDocumentFilter>();
            });

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the Swagger information description from its embedded Markdown resource.
        /// </summary>
        /// <remarks>
        /// The resource remains the single source of consumer-facing Swagger orientation text. Only the known runtime
        /// tokens are substituted, preserving literal braces in any future Markdown examples.
        /// </remarks>
        /// <example>
        /// <code>
        /// var description = getSwaggerDescription(demoModeWarning, environment, serverName);
        /// </code>
        /// </example>
        /// <param name="demoModeWarning">Pre-rendered warning Markdown when demo mode is enabled.</param>
        /// <param name="environment">Build-specific SQL Server environment label.</param>
        /// <param name="serverName">Build-specific SQL Server host label.</param>
        /// <returns>The rendered Swagger description, or a concise fallback when the resource is unavailable.</returns>
        /// <seealso cref="OpenApiInfo"/>
        private static string getSwaggerDescription(string demoModeWarning, string environment, string serverName)
        {
            #region implementation

            var assembly = typeof(MedRecProSwaggerExtensions).Assembly;
            using var stream = assembly.GetManifestResourceStream(SwaggerDescriptionResourceName);
            if (stream == null)
            {
                // Keep Swagger available even if a future project-file change omits the documentation resource.
                return "MedRecPro API documentation is temporarily unavailable.";
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd()
                .Replace("{demoModeWarning}", demoModeWarning, StringComparison.Ordinal)
                .Replace("{environment}", environment, StringComparison.Ordinal)
                .Replace("{serverName}", serverName, StringComparison.Ordinal);

            #endregion
        }
        /**************************************************************/
        /// <summary>
        /// Registers Swagger JSON and UI middleware, including long-lived Swagger JSON cache headers and tag families.
        /// </summary>
        /// <remarks>
        /// The server URL pre-serialization logic uses a runtime <c>IsDevelopment()</c> check and must remain separate from compile-time controller routing.
        /// </remarks>
        /// <example>
        /// <code>
        /// app.UseMedRecProSwagger();
        /// </code>
        /// </example>
        /// <param name="app">The web application pipeline to configure.</param>
        /// <returns>The same web application for chaining.</returns>
        /// <seealso cref="OpenApiServer"/>
        /// <seealso cref="OpenApiSecurityScheme"/>
        /// <seealso cref="SwaggerTagDocumentationDocumentFilter"/>
        public static WebApplication UseMedRecProSwagger(this WebApplication app)
        {
            #region implementation

#pragma warning disable CS1587 // XML comment is not placed on a valid language element
            /**************************************************************/
            /// <summary>
            /// Middleware that injects long-lived cache headers for any <c>swagger.json</c> response.
            /// </summary>
            /// <remarks>
            /// This middleware must appear <b>before</b> <see cref="Microsoft.AspNetCore.Builder.SwaggerBuilderExtensions.UseSwagger"/>
            /// so headers are applied before the Swagger middleware writes its response.
            /// It also logs each hit to <see cref="Microsoft.Extensions.Logging.ILogger"/> so that
            /// Azure App Service log streaming and Application Insights can confirm the optimization
            /// is active in production.
            /// </remarks>
            /// <example>
            /// Response headers applied:
            /// <code>
            /// Cache-Control: public,max-age=86400
            /// Access-Control-Allow-Origin: *
            /// Access-Control-Allow-Methods: GET, OPTIONS
            /// </code>
            /// </example>
            /// <seealso cref="Microsoft.AspNetCore.Builder.WebApplication"/>
            /**************************************************************/
            app.Use(async (context, next) =>
            {
                #region implementation
                var path = context.Request.Path.Value ?? string.Empty;

                if (path.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Log before the response is written so we can track in Azure logs
                    app.Logger.LogInformation(
                        "[SwaggerCacheHeaders] Applying caching headers for {Path} (RequestId={TraceId})",
                        path,
                        RequestCorrelation.GetTraceId(context));

                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers["Cache-Control"] = "public,max-age=86400";
                        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";

                        // Log again when headers are actually attached
                        app.Logger.LogDebug(
                            "[SwaggerCacheHeaders] Headers injected successfully for {Path} at {UtcTime}",
                            path,
                            DateTime.UtcNow.ToString("o"));

                        return Task.CompletedTask;
                    });
                }

                await next();

                // post-processing log for visibility in verbose traces
                if (path.EndsWith("swagger.json", StringComparison.OrdinalIgnoreCase))
                {
                    app.Logger.LogInformation(
                        "[SwaggerCacheHeaders] Completed response for {Path} ({StatusCode})",
                        path,
                        context.Response?.StatusCode);
                }
                #endregion
            });

            /**************************************************************/

            /// <summary>
            /// Registers Swagger middleware to generate the OpenAPI JSON schema.
            /// </summary>
            /// <remarks>
            /// Do not prefix with "/api" in <see cref="RouteTemplate"/> because the
            /// Azure App Service already hosts the application under "/api".
            /// </remarks>
            /**************************************************************/

            app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentName}/swagger.json";

                c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                {
                    swaggerDoc.Servers.Clear();

                    // Use RUNTIME environment check, not compile-time #if DEBUG
                    if (app.Environment.IsDevelopment())
                    {
                        // Development/Debug: Controllers have api/[controller] route
                        swaggerDoc.Servers.Add(new OpenApiServer
                        {
                            Url = $"{httpReq.Scheme}://{httpReq.Host.Value}",
                            Description = "Local Development"
                        });
                    }
                    else
                    {
                        // Production: Virtual application adds /api, controllers have [controller] route
                        swaggerDoc.Servers.Add(new OpenApiServer
                        {
                            Url = "/api",
                            Description = "MedRecPro API"
                        });
                    }
                });
            });
#pragma warning restore CS1587 // XML comment is not placed on a valid language element

            var swaggerdocs = app.Environment.IsDevelopment()
                ? $"/swagger/v1/swagger.json?v={DateTime.UtcNow.Ticks}" // Cache buster
                : "/api/swagger/v1/swagger.json";
            var swaggerAssetRoot = app.Environment.IsDevelopment()
                ? "/stylesheets"
                : "/api/stylesheets";

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(swaggerdocs, "MedRecPro API V1");
                c.ConfigObject.AdditionalItems["operationsSorter"] = "method";
                c.ConfigObject.AdditionalItems["tagsSorter"] = "alpha";
                c.InjectStylesheet($"{swaggerAssetRoot}/swagger-tag-families.css");
                c.InjectJavascript($"{swaggerAssetRoot}/swagger-tag-families.js");
                c.RoutePrefix = "swagger";
            });

            return app;

            #endregion
        }

        #endregion
    }
}
