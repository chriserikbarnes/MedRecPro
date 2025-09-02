using MedRecPro.Models;
using MedRecPro.Service;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Enhanced service registration extensions for SPL document rendering functionality.
    /// Now includes ingredient rendering capabilities.
    /// </summary>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="IDocumentRenderingService"/>
    /// <seealso cref="IIngredientRenderingService"/>
    public static class SplRenderingServiceRegistration
    {
        /**************************************************************/
        /// <summary>
        /// Enhanced registration of all SPL document rendering services with dependency injection container.
        /// Configures the complete service layer for optimized document rendering and export including ingredient processing.
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
        /// Enhanced to include ingredient rendering service for comprehensive ingredient processing.
        /// </remarks>
        public static IServiceCollection AddDocumentRenderingServices(this IServiceCollection services)
        {
            #region implementation

            // Register core services in dependency order
            services.AddScoped<IDocumentDataService, DocumentDataService>();
            services.AddScoped<IDocumentRenderingService, DocumentRenderingService>();
            services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();

            // Register hierarchy and rendering services
            services.AddScoped<ISectionHierarchyService, SectionHierarchyService>();
            services.AddScoped<ISectionRenderingService, SectionRenderingService>();
            services.AddScoped<IProductRenderingService, ProductRenderingService>();
            services.AddScoped<IIngredientRenderingService, IngredientRenderingService>(); // NEW

            // Register structured body and section services
            services.AddScoped<IStructuredBodyViewModelFactory, StructuredBodyViewModelFactory>();
            services.AddScoped<IStructuredBodyService, StructuredBodyService>();

            // Register main export service (depends on all above services)
            services.AddScoped<ISplExportService, SplExportService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enhanced registration of document rendering services with custom configuration options.
        /// Provides flexibility for advanced configuration scenarios including ingredient processing settings.
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
        ///     options.EnableIngredientOptimization = true; // NEW
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
    /// Enhanced configuration options for document rendering services.
    /// Provides settings for performance optimization and advanced features including ingredient processing.
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

        /**************************************************************/
        /// <summary>
        /// Enables optimized ingredient processing with pre-computed properties.
        /// When enabled, ingredients are processed with enhanced rendering contexts.
        /// Default: true for optimal ingredient rendering performance.
        /// </summary>
        public bool EnableIngredientOptimization { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Maximum number of ingredients to process per product.
        /// Prevents performance issues with products containing excessive ingredients.
        /// Default: 1000 ingredients per product.
        /// </summary>
        public int MaxIngredientsPerProduct { get; set; } = 1000;
    }
}