using Microsoft.Extensions.FileProviders;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides composition-root extension methods for MedRecPro middleware setup outside of authentication and Swagger.
    /// </summary>
    /// <remarks>
    /// These helpers keep middleware order explicit in <c>Program.cs</c> while moving setup details out of the composition shell.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.UseMedRecProExceptionHandling();
    /// app.UseMedRecProSplStaticFiles();
    /// app.UseMedRecProCors();
    /// </code>
    /// </example>
    /// <seealso cref="WebApplication"/>
    public static class MedRecProMiddlewareExtensions
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Registers the centralized exception-handling middleware in every environment.
        /// </summary>
        /// <remarks>
        /// MedRecPro uses one sanitized API error contract in Development and non-Development environments. This
        /// intentionally replaces the developer exception page so local API clients and automated tests do not see a
        /// different HTML error surface. HSTS remains restricted to non-Development hosting.
        /// </remarks>
        /// <example>
        /// <code>
        /// app.UseMedRecProExceptionHandling();
        /// </code>
        /// </example>
        /// <param name="app">The web application pipeline to configure.</param>
        /// <returns>The same web application for chaining.</returns>
        /// <seealso cref="ExceptionHandlerExtensions"/>
        /// <seealso cref="HstsBuilderExtensions"/>
        public static WebApplication UseMedRecProExceptionHandling(this WebApplication app)
        {
            #region implementation

            // MedRecProExceptionHandler is registered through AddMedRecProApiControllers. The pathless overload
            // keeps the centralized policy route-independent and active in every environment.
            app.UseExceptionHandler();

            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            return app;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers static-file middleware for SPL stylesheet and transform assets.
        /// </summary>
        /// <remarks>
        /// Request paths and content-type/CORS response headers are preserved, including the existing production <c>/api/stylesheets</c> alias.
        /// </remarks>
        /// <example>
        /// <code>
        /// app.UseMedRecProSplStaticFiles();
        /// </code>
        /// </example>
        /// <param name="app">The web application pipeline to configure.</param>
        /// <returns>The same web application for chaining.</returns>
        /// <seealso cref="StaticFileOptions"/>
        /// <seealso cref="PhysicalFileProvider"/>
        public static WebApplication UseMedRecProSplStaticFiles(this WebApplication app)
        {
            #region implementation

            /**************************************************************/
            // Configure static files with proper CORS and content types for SPL assets
            // Serves from /api/Views/Stylesheets/ as /api/stylesheets/*
            var stylesheetsPath = Path.Combine(app.Environment.ContentRootPath, "Views", "Stylesheets");

            // Only configure if directory exists
            if (Directory.Exists(stylesheetsPath))
            {
                var fileProvider = new PhysicalFileProvider(stylesheetsPath);

                // --- Primary alias: /api/stylesheets ---
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    RequestPath = "/api/stylesheets",
                    OnPrepareResponse = ctx =>
                    {
                        var ext = Path.GetExtension(ctx.File.Name).ToLowerInvariant();

                        // Allow CORS for browser XSL transforms and JS modules
                        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");

                        switch (ext)
                        {
                            case ".xsl":
                                ctx.Context.Response.Headers.Append("Content-Type", "application/xslt+xml; charset=utf-8");
                                break;
                            case ".xml":
                                ctx.Context.Response.Headers.Append("Content-Type", "application/xml; charset=utf-8");
                                break;
                            case ".css":
                                ctx.Context.Response.Headers.Append("Content-Type", "text/css; charset=utf-8");
                                break;
                            case ".js":
                                // JS modules need correct MIME type for ES6 import()
                                ctx.Context.Response.Headers.Append("Content-Type", "application/javascript; charset=utf-8");
                                break;
                        }
                    }
                });

                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(stylesheetsPath),
                    RequestPath = "/stylesheets"
                });

                // --- Optional secondary alias: /stylesheets (for legacy URLs) ---
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    RequestPath = "/stylesheets"
                });

                app.Logger.LogInformation("Static assets served from {Path} at /api/stylesheets and /stylesheets", stylesheetsPath);
            }

            return app;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies the MedRecPro CORS policy branch for development or production environments.
        /// </summary>
        /// <remarks>
        /// The production branch still permits trusted production domains and local-network development origins for live API testing.
        /// </remarks>
        /// <example>
        /// <code>
        /// app.UseMedRecProCors();
        /// </code>
        /// </example>
        /// <param name="app">The web application pipeline to configure.</param>
        /// <returns>The same web application for chaining.</returns>
        /// <seealso cref="CorsMiddlewareExtensions.UseCors(IApplicationBuilder, string)"/>
        public static WebApplication UseMedRecProCors(this WebApplication app)
        {
            #region implementation

            // Apply CORS policies based on environment
            if (app.Environment.IsDevelopment())
            {
                // In development, allow both local and production origins
                app.UseCors("AllowLocalDevelopment");
            }
            else
            {
                // In production, use both policies to allow local dev testing against prod
                // This enables developers to test against the live API
                app.UseCors(policy => policy
                    .SetIsOriginAllowed(origin =>
                    {
                        // Allow production domains
                        if (origin.Contains("medrecpro.com")) return true;

                        // Allow localhost for remote development
                        var uri = new Uri(origin);
                        return uri.Host == "localhost" ||
                               uri.Host == "127.0.0.1" ||
                               uri.Host.StartsWith("192.168.") ||
                               uri.Host.StartsWith("10.");
                    })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders(
                        "X-Page-Number",
                        "X-Page-Size",
                        "X-Total-Count",
                        "X-Chartable-Count")
                    .AllowCredentials()
                );
            }

            return app;

            #endregion
        }

        #endregion
    }
}
