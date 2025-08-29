using Microsoft.Extensions.DependencyInjection;
using MedRecPro.Service;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Service registration extensions for SPL document rendering functionality.
    /// </summary>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="IDocumentRenderingService"/>
    public static class SplRenderingServiceRegistration
    {
        /**************************************************************/
        /// <summary>
        /// Registers all SPL document rendering services with dependency injection container.
        /// Configures the complete service layer for optimized document rendering and export.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <returns>The configured service collection for method chaining</returns>
        /// <seealso cref="IServiceCollection"/>
        /// <example>
        /// <code>
        /// // In Program.cs or Startup.cs
        /// services.AddDocumentRenderingServices();
        /// </code>
        /// </example>
        /// <remarks>
        /// Services are registered in dependency order to ensure proper resolution.
        /// All services are registered with scoped lifetime for request-based processing.
        /// </remarks>
        public static IServiceCollection AddDocumentRenderingServices(this IServiceCollection services)
        {
            #region implementation

            // Register core services in dependency order
            services.AddScoped<IDocumentDataService, DocumentDataService>();
            services.AddScoped<IDocumentRenderingService, DocumentRenderingService>();
            services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();
            services.AddScoped<ISectionHierarchyService, SectionHierarchyService>();

            // Register structured body and section services (if they exist)
            services.AddScoped<IStructuredBodyViewModelFactory, StructuredBodyViewModelFactory>();
            services.AddScoped<ISectionRenderingService, SectionRenderingService>();
            services.AddScoped<IStructuredBodyService, StructuredBodyService>();

            // Register main export service (depends on all above services)
            services.AddScoped<ISplExportService, SplExportService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers document rendering services with custom configuration options.
        /// Provides flexibility for advanced configuration scenarios.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <param name="configureOptions">Optional configuration action for advanced settings</param>
        /// <returns>The configured service collection for method chaining</returns>
        /// <seealso cref="IServiceCollection"/>
        /// <example>
        /// <code>
        /// services.AddDocumentRenderingServices(options =>
        /// {
        ///     options.EnablePerformanceLogging = true;
        ///     options.CacheTemplates = true;
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddDocumentRenderingServices(
            this IServiceCollection services,
            Action<DocumentRenderingOptions>? configureOptions = null)
        {
            #region implementation

            // Configure options if provided
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            // Register core services
            return services.AddDocumentRenderingServices();

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Configuration options for document rendering services.
    /// Provides settings for performance optimization and advanced features.
    /// </summary>
    public class DocumentRenderingOptions
    {
        /**************************************************************/
        /// <summary>
        /// Enables detailed performance logging for rendering operations.
        /// Default: false for production performance.
        /// </summary>
        public bool EnablePerformanceLogging { get; set; } = false;

        /**************************************************************/
        /// <summary>
        /// Enables template caching for improved rendering performance.
        /// Default: true for optimal performance.
        /// </summary>
        public bool CacheTemplates { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Maximum number of concurrent rendering operations.
        /// Default: Environment.ProcessorCount for optimal resource utilization.
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount;
    }
}