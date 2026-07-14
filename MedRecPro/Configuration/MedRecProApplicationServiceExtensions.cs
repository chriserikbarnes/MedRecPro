using Azure.Identity;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Features.AeDashboard.Mapping;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Security;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using MedRecPro.Service.LabelQuery;
using MedRecPro.Service.LabelQuery.Common;
using MedRecPro.Service.LabelQuery.Implementation;
using MedRecPro.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ImportApplicationDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecPro.Configuration
{
    /**************************************************************/
    /// <summary>
    /// Provides composition-root extension methods for MedRecPro infrastructure, feature, and background service registrations.
    /// </summary>
    /// <remarks>
    /// These methods preserve the original registration order from <c>Program.cs</c> while separating startup concerns by capability.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.AddMedRecProDataAccess();
    /// builder.Services.AddMedRecProPlatformServices(builder.Configuration);
    /// </code>
    /// </example>
    /// <seealso cref="WebApplicationBuilder"/>
    /// <seealso cref="IServiceCollection"/>
    public static class MedRecProApplicationServiceExtensions
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Adds Azure Key Vault configuration when the application is running in production.
        /// </summary>
        /// <remarks>
        /// This method runs before connection strings and authentication secrets are read so Key Vault values can participate in normal configuration lookup.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.AddMedRecProKeyVault();
        /// </code>
        /// </example>
        /// <param name="builder">The web application builder being configured.</param>
        /// <returns>The same builder for startup chaining.</returns>
        /// <seealso cref="DefaultAzureCredential"/>
        public static WebApplicationBuilder AddMedRecProKeyVault(this WebApplicationBuilder builder)
        {
            #region implementation

            // Key Vault Configuration for Production.
            if (builder.Environment.IsProduction())
            {
                var keyVaultUrl = builder.Configuration["KeyVaultUrl"];

                if (!string.IsNullOrEmpty(keyVaultUrl))
                {
                    builder.Configuration.AddAzureKeyVault(
                        new Uri(keyVaultUrl),
                        new DefaultAzureCredential());
                }
            }

            return builder;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers the MedRecPro EF Core contexts using the configuration-specific connection-string fallback.
        /// </summary>
        /// <remarks>
        /// The <c>#if DEBUG</c> fallback is intentionally preserved because Debug and production builds resolve different fallback keys.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.AddMedRecProDataAccess();
        /// </code>
        /// </example>
        /// <param name="builder">The web application builder that owns configuration and services.</param>
        /// <returns>The same builder for startup chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no database connection string can be resolved.</exception>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="ImportApplicationDbContext"/>
        public static WebApplicationBuilder AddMedRecProDataAccess(this WebApplicationBuilder builder)
        {
            #region implementation

            string? connectionString = null;

            /**************************************************************/
            // Access the connection string.
#if DEBUG
            connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                               ?? builder.Configuration.GetSection("Dev:DB:Connection")?.Value;
#else
            connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                               ?? builder.Configuration.GetSection("Prod:DB:Connection")?.Value;
#endif

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            /**************************************************************/
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(60);
                }));

            builder.Services.AddDbContext<ImportApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(60);
                }));

            return builder;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers settings objects and startup diagnostics that must run before feature services are added.
        /// </summary>
        /// <remarks>
        /// The Debug-only EF logging filter is preserved as compile-time conditional behavior without exposing
        /// configuration-secret metadata through direct console output.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.AddMedRecProConfigurationSettings();
        /// </code>
        /// </example>
        /// <param name="builder">The web application builder being configured.</param>
        /// <returns>The same builder for startup chaining.</returns>
        /// <seealso cref="ClaudeApiSettings"/>
        /// <seealso cref="ComparisonSettings"/>
        /// <seealso cref="AppSettings"/>
        public static WebApplicationBuilder AddMedRecProConfigurationSettings(this WebApplicationBuilder builder)
        {
            #region implementation

            builder.Services.Configure<ClaudeApiSettings>(builder.Configuration.GetSection("ClaudeApiSettings"));

#if DEBUG
            // Preserve the Debug-only EF Core connection diagnostic filter.
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Debug);
#endif

            builder.Services.Configure<ComparisonSettings>(builder.Configuration.GetSection("ComparisonSettings"));
            builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("appSettings"));
            builder.Services.AddOptions<DatabaseSecurityOptions>()
                .Bind(builder.Configuration.GetSection(DatabaseSecurityOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.PKSecret),
                    "Security:DB:PKSecret is required")
                .ValidateOnStart();

            return builder;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers platform services used broadly by controllers, filters, monitoring, and request throttling.
        /// </summary>
        /// <remarks>
        /// Registration order mirrors the original composition root, including database monitoring before tarpit service registration.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProPlatformServices(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Application configuration used by dependent services.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="UserDataAccess"/>
        /// <seealso cref="TarpitService"/>
        /// <seealso cref="DatabaseUsageMonitorExtensions"/>
        public static IServiceCollection AddMedRecProPlatformServices(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            services.AddHttpContextAccessor();

            services.AddSingleton(TimeProvider.System);

            services.AddSingleton<IAppCache, PerformanceAppCache>();
            services.AddSingleton<IPrimaryKeyCipher, PrimaryKeyCipher>();
            services.AddMedRecProLabelQueryServices();

            services.AddScoped<IUserContextAccessor, HttpUserContextAccessor>();

            // --- Custom Services ---
            services.AddScoped<UserDataAccess>();

            services.AddMemoryCache();

            services.AddMedRecProAeDashboardServices();

            services.AddSingleton<AzureAppTokenProvider>();

            services.AddSingleton<AzureManagementTokenProvider>();

            services.AddScoped<AzureSqlMetricsService>();

            services.AddDatabaseUsageMonitoring();

            // Tarpit middleware configuration and service.
            services.AddSingleton<IValidateOptions<TarpitSettings>, TarpitSettingsValidator>();
            services.AddOptions<TarpitSettings>()
                .Bind(configuration.GetSection("TarpitSettings"))
                .ValidateOnStart();
            services.AddSingleton<TarpitService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers the feature-owned label query services and their cache policies.
        /// </summary>
        /// <remarks>
        /// The caller must register <see cref="IAppCache"/> first. Query services are scoped because
        /// they depend on <see cref="ApplicationDbContext"/>, while cache-key construction and the
        /// cache policy are singleton-safe and carry no scoped state.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddSingleton&lt;IAppCache, PerformanceAppCache&gt;();
        /// services.AddMedRecProLabelQueryServices();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="ILabelDocumentQueryService"/>
        /// <seealso cref="LabelQueryCachePolicy"/>
        public static IServiceCollection AddMedRecProLabelQueryServices(this IServiceCollection services)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<LegacyDtoLabelCacheKeyBuilder>(_ => new LegacyDtoLabelCacheKeyBuilder());
            services.AddSingleton<LabelQueryCachePolicy>(serviceProvider => new LabelQueryCachePolicy(
                serviceProvider.GetRequiredService<IAppCache>(),
                serviceProvider.GetRequiredService<LegacyDtoLabelCacheKeyBuilder>()));

            services.AddScoped<LabelQueryDataAccess>();
            services.AddScoped<IIngredientSearchService, IngredientSearchService>();
            services.AddScoped<IPharmacologicClassSearchService, PharmacologicClassSearchService>();
            services.AddScoped<IProductSearchService, ProductSearchService>();
            services.AddScoped<ILabelContentQueryService, LabelContentQueryService>();
            services.AddScoped<ILabelMarkdownService, LabelMarkdownService>();
            services.AddScoped<ILabelDocumentQueryService, LabelDocumentQueryService>();
            services.AddScoped<ICompleteLabelService, CompleteLabelService>();
            services.AddScoped<ILabelSectionCrudService, LabelSectionCrudService>();
            services.AddScoped<ILabelXmlDocumentService, LabelXmlDocumentService>();
            services.AddScoped<ILabelAiSearchService, LabelAiSearchService>();
            services.AddScoped<IOrangeBookPatentQueryService, OrangeBookPatentQueryService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers the feature-owned AE dashboard services and policies.
        /// </summary>
        /// <remarks>
        /// The cache, encrypted-ID mapper, correlation policy, and DTO mapper are singleton-safe.
        /// The data implementation and all public AE services are scoped because they work with
        /// request-owned <see cref="ApplicationDbContext"/> instances.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProAeDashboardServices();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="IAeDashboardProductCatalogService"/>
        /// <seealso cref="AeDashboardDataAccess"/>
        public static IServiceCollection AddMedRecProAeDashboardServices(this IServiceCollection services)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IAeDashboardCachePolicy, AeDashboardCachePolicy>();
            services.AddSingleton<IAeDashboardEncryptedIdMapper, AeDashboardEncryptedIdMapper>();
            services.AddSingleton<IAeDashboardCorrelationPolicy, AeDashboardCorrelationPolicy>();
            services.AddSingleton<IAeDashboardDtoMapper>(serviceProvider => new AeDashboardDtoMapper(
                serviceProvider.GetRequiredService<IAeDashboardEncryptedIdMapper>()));

            services.AddScoped<AeDashboardDataAccess>(serviceProvider => new AeDashboardDataAccess(
                serviceProvider.GetRequiredService<IAeDashboardCachePolicy>(),
                serviceProvider.GetRequiredService<IAeDashboardEncryptedIdMapper>(),
                serviceProvider.GetRequiredService<IAeDashboardCorrelationPolicy>(),
                serviceProvider.GetRequiredService<IAeDashboardDtoMapper>()));

            services.AddScoped<IAeDashboardProductCatalogService, AeDashboardProductCatalogService>();
            services.AddScoped<IAeDashboardProductDetailService, AeDashboardProductDetailService>();
            services.AddScoped<IAeDashboardFavoriteService, AeDashboardFavoriteService>();
            services.AddScoped<IAeDashboardClassCorrelationService, AeDashboardClassCorrelationService>();
            services.AddScoped<IAeDashboardSystemCorrelationService, AeDashboardSystemCorrelationService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers AI, Claude, comparison, and conversation services.
        /// </summary>
        /// <remarks>
        /// This keeps the Claude service registration order intact, including the configured <see cref="HttpClient"/> for Claude API access.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProAi();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="IClaudeApiService"/>
        /// <seealso cref="IComparisonService"/>
        /// <seealso cref="IClaudeConversationService"/>
        public static IServiceCollection AddMedRecProAi(this IServiceCollection services)
        {
            #region implementation

            // Register ClaudeSkillService for two-stage routing skill management.
            services.AddSingleton<IClaudeSkillService, ClaudeSkillService>();

            // Configure ClaudeApiService with HttpClient and inject settings.
            services.AddHttpClient<IClaudeApiService, ClaudeApiService>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<ClaudeApiSettings>>().Value;
                client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                client.BaseAddress = new Uri("https://api.anthropic.com/");
            });

            services.AddScoped<IComparisonService, ComparisonService>();

            services.AddSingleton<ConversationStore>();

            // Register ClaudeConversationService for conversation management.
            services.AddScoped<IClaudeConversationService, ClaudeConversationService>();

            // Register PharmacologicClassSearchService for intelligent drug class search.
            services.AddScoped<IClaudeSearchService, ClaudeSearchService>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers user, activity logging, permission, and web-side repository services.
        /// </summary>
        /// <remarks>
        /// These registrations remain after the Claude services and before import services to preserve the original startup sequence.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProUserServices();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="ActivityLogActionFilter"/>
        /// <seealso cref="IPermissionService"/>
        /// <seealso cref="Repository{T}"/>
        public static IServiceCollection AddMedRecProUserServices(this IServiceCollection services)
        {
            #region implementation

            services.AddUserLogger(); // custom service

            services.AddScoped<IActivityLogService, ActivityLogService>();

            services.AddSingleton<ActivityLogDispatcher>();
            services.AddSingleton<IActivityLogDispatcher>(serviceProvider =>
                serviceProvider.GetRequiredService<ActivityLogDispatcher>());
            services.AddHostedService<ActivityLogDispatcherHostedService>();

            services.AddScoped<ActivityLogActionFilter>();

            services.AddScoped<IPermissionService, PermissionService>();

            services.AddScoped(typeof(Repository<>), typeof(Repository<>));

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers import-library repositories, parsers, import services, and cipher compatibility services.
        /// </summary>
        /// <remarks>
        /// Both MedRecProImportClass services and web-side compatibility adapters stay registered during the import-boundary migration.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProImport();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="MedRecProImportClass.Service.SplImportService"/>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="StringCipher"/>
        public static IServiceCollection AddMedRecProImport(this IServiceCollection services)
        {
            #region implementation

            services.AddScoped(typeof(MedRecProImportClass.DataAccess.Repository<>), typeof(MedRecProImportClass.DataAccess.Repository<>));

            services.AddTransient<MedRecProImportClass.Helpers.StringCipher>();

            services.AddScoped<MedRecProImportClass.Service.SplXmlParser>();

            services.AddScoped<MedRecProImportClass.Service.SplImportService>();

            services.AddScoped<MedRecProImportClass.Service.SplDataService>();

            services.AddScoped<SplXmlParser>();

            services.AddScoped<SplImportService>();

            services.AddScoped<SplDataService>();

            services.AddTransient<StringCipher>();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers background processing, status, encryption, dictionary, and endpoint-discovery services.
        /// </summary>
        /// <remarks>
        /// Hosted-service order is preserved: ZIP import worker, demo mode, then database keep-alive after the monitoring service registered earlier.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProBackgroundServices(configuration);
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">Application configuration that controls background-processing feature flags.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        /// <seealso cref="ZipImportWorkerService"/>
        /// <seealso cref="DatabaseKeepAliveService"/>
        public static IServiceCollection AddMedRecProBackgroundServices(this IServiceCollection services, IConfiguration configuration)
        {
            #region implementation

            // The queue is a lightweight singleton dependency of comparison coordination even when a host opts out
            // of processing work. Keeping the descriptor present lets DI validation remain deterministic; the
            // feature flag controls worker execution rather than whether controllers can be constructed.
            services.AddSingleton<IBackgroundTaskQueueService, BackgroundTaskQueueService>();

            services.AddSingleton<IComparisonJobCoordinator, ComparisonJobCoordinator>();

            services.AddSingleton<InMemoryOperationStatusStore>();

            services.AddSingleton<IOperationStatusStore>(provider => provider.GetRequiredService<InMemoryOperationStatusStore>());

            services.AddSingleton<IImportOperationStatusStore>(provider => provider.GetRequiredService<InMemoryOperationStatusStore>());

            services.AddHostedService<ZipImportWorkerService>();

            services.AddSingleton<IEncryptionService, EncryptionService>();

            services.AddSingleton<IDictionaryUtilityService, DictionaryUtilityService>();

            services.AddHostedService<DemoModeService>();

            services.AddHostedService<DatabaseKeepAliveService>();

            services.AddEndpointsApiExplorer();

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers SPL document rendering services with their existing performance-oriented options.
        /// </summary>
        /// <remarks>
        /// Rendering registration remains immediately after endpoint discovery and before session/auth registration.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProRendering();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="SplRenderingServiceRegistration"/>
        public static IServiceCollection AddMedRecProRendering(this IServiceCollection services)
        {
            #region implementation

            services.AddDocumentRenderingServices(options =>
            {
                options.EnablePerformanceLogging = true;
                options.CacheTemplates = true;
                options.MaxConcurrentOperations = Environment.ProcessorCount;
            });

            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers request size limits for multipart uploads and Kestrel request bodies.
        /// </summary>
        /// <remarks>
        /// These limits preserve the existing two-gigabyte upload behavior for SPL ZIP import workflows.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddMedRecProRequestLimits();
        /// </code>
        /// </example>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="FormOptions"/>
        /// <seealso cref="KestrelServerOptions"/>
        public static IServiceCollection AddMedRecProRequestLimits(this IServiceCollection services)
        {
            #region implementation

            services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = int.MaxValue; // 2GB
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = int.MaxValue; // 2GB
            });

            return services;

            #endregion
        }

        #endregion
    }
}
