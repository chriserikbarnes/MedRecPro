using MedRecPro.Configuration;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Features.AeDashboard.Mapping;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Middleware;
using MedRecPro.Models;
using MedRecPro.Security;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using MedRecPro.Service.LabelQuery;
using MedRecPro.Service.LabelQuery.Common;
using MedRecPro.Service.LabelQuery.Implementation;
using MedRecPro.Services;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ImportApplicationDbContext = MedRecProImportClass.Data.ApplicationDbContext;

namespace MedRecProTest.Architecture
{
    /**************************************************************/
    /// <summary>
    /// Tests public service and middleware registration extension methods.
    /// </summary>
    /// <seealso cref="SplRenderingServiceRegistration"/>
    /// <seealso cref="TarpitMiddlewareExtensions"/>
    [TestClass]
    [TestCategory("Architecture")]
    public class ServiceRegistrationTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies SPL document rendering services are registered with scoped lifetimes.
        /// </summary>
        /// <seealso cref="SplRenderingServiceRegistration.AddDocumentRenderingServices(IServiceCollection)"/>
        [TestMethod]
        public void AddDocumentRenderingServices_DefaultRegistration_AddsExpectedScopedServices()
        {
            #region implementation
            var services = new ServiceCollection();

            var result = services.AddDocumentRenderingServices();

            Assert.AreSame(services, result);
            assertScoped<IDocumentDataService, DocumentDataService>(services);
            assertScoped<IDocumentRenderingService, DocumentRenderingService>(services);
            assertScoped<ITemplateRenderingService, TemplateRenderingService>(services);
            assertScoped<ITextContentRenderingService, TextContentRenderingService>(services);
            assertScoped<IIngredientRenderingService, IngredientRenderingService>(services);
            assertScoped<IPackageRenderingService, PackageRenderingService>(services);
            assertScoped<ISectionHierarchyService, SectionHierarchyService>(services);
            assertScoped<ISectionRenderingService, SectionRenderingService>(services);
            assertScoped<IProductRenderingService, ProductRenderingService>(services);
            assertScoped<ICharacteristicRenderingService, CharacteristicRenderingService>(services);
            assertScoped<IAuthorRenderingService, AuthorRenderingService>(services);
            assertScoped<IStructuredBodyViewModelFactory, StructuredBodyViewModelFactory>(services);
            assertScoped<IStructuredBodyService, StructuredBodyService>(services);
            assertScoped<ISplExportService, SplExportService>(services);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the options overload stores configured rendering options.
        /// </summary>
        /// <seealso cref="SplRenderingServiceRegistration.AddDocumentRenderingServices(IServiceCollection, Action{DocumentRenderingOptions}?)"/>
        [TestMethod]
        public void AddDocumentRenderingServices_OptionsOverload_ConfiguresOptions()
        {
            #region implementation
            var services = new ServiceCollection();

            services.AddDocumentRenderingServices(options =>
            {
                options.EnablePerformanceLogging = true;
                options.MaxConcurrentOperations = 2;
            });
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<DocumentRenderingOptions>>().Value;

            Assert.IsTrue(options.EnablePerformanceLogging);
            Assert.AreEqual(2, options.MaxConcurrentOperations);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the tarpit middleware extension can be added to a minimal builder.
        /// </summary>
        /// <seealso cref="TarpitMiddlewareExtensions.UseTarpitMiddleware"/>
        [TestMethod]
        public void UseTarpitMiddleware_MinimalBuilder_ReturnsBuilder()
        {
            #region implementation
            var provider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<TarpitService>()
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                .Configure<TarpitSettings>(_ => { })
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(provider);

            var result = builder.UseTarpitMiddleware();

            Assert.AreSame(builder, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies AddDatabaseUsageMonitoring registers the throttle state
        /// singleton, its interface forwarding factory, and the hosted
        /// database usage monitor.
        /// </summary>
        /// <seealso cref="DatabaseUsageMonitorExtensions.AddDatabaseUsageMonitoring"/>
        [TestMethod]
        public void AddDatabaseUsageMonitoring_RegistersThrottleStateInterfaceAndHostedMonitor()
        {
            #region implementation
            // Arrange
            var services = new ServiceCollection();

            // Act
            var result = services.AddDatabaseUsageMonitoring();

            // Assert - fluent return.
            Assert.AreSame(services, result);

            // Concrete throttle state singleton.
            var concrete = services.SingleOrDefault(s =>
                s.ServiceType == typeof(ThrottleStateService) &&
                s.ImplementationType == typeof(ThrottleStateService));
            Assert.IsNotNull(concrete, "Missing ThrottleStateService registration.");
            Assert.AreEqual(ServiceLifetime.Singleton, concrete!.Lifetime);

            // Interface registration forwards through a factory (no
            // ImplementationType), so assert the factory-descriptor shape.
            var forwarded = services.SingleOrDefault(s =>
                s.ServiceType == typeof(IThrottleStateService));
            Assert.IsNotNull(forwarded, "Missing IThrottleStateService registration.");
            Assert.AreEqual(ServiceLifetime.Singleton, forwarded!.Lifetime);
            Assert.IsNotNull(forwarded.ImplementationFactory, "Interface registration should forward via factory.");

            // Hosted monitor.
            var hosted = services.SingleOrDefault(s =>
                s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                s.ImplementationType == typeof(DatabaseUsageMonitorService));
            Assert.IsNotNull(hosted, "Missing DatabaseUsageMonitorService hosted registration.");
            Assert.AreEqual(ServiceLifetime.Singleton, hosted!.Lifetime);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the monitoring registrations resolve: the interface and
        /// concrete throttle state share one instance and the hosted monitor
        /// constructs without any Azure dependencies.
        /// </summary>
        /// <seealso cref="DatabaseUsageMonitorExtensions.AddDatabaseUsageMonitoring"/>
        [TestMethod]
        public void AddDatabaseUsageMonitoring_ProviderResolvesSharedStateAndHostedMonitor()
        {
            #region implementation
            // Arrange - monitoring disabled keeps the hosted loop inert.
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
                new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["DatabaseUsageMonitor:Enabled"] = "false"
                    })
                    .Build());
            services.AddLogging();
            services.AddDatabaseUsageMonitoring();

            // Act
            using var provider = services.BuildServiceProvider();
            var concrete = provider.GetRequiredService<ThrottleStateService>();
            var forwarded = provider.GetRequiredService<IThrottleStateService>();
            var monitor = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                .OfType<DatabaseUsageMonitorService>()
                .Single();

            // Assert - interface forwards to the same singleton instance.
            Assert.AreSame(concrete, forwarded);
            Assert.IsNotNull(monitor);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the extracted MedRecPro startup service-registration extensions add the expected representative descriptors.
        /// </summary>
        /// <remarks>
        /// This test keeps configuration isolated from production fallbacks while exercising the Phase 2 composition-root extension methods.
        /// </remarks>
        /// <seealso cref="MedRecProApplicationServiceExtensions"/>
        /// <seealso cref="MedRecProAuthenticationExtensions"/>
        /// <seealso cref="MedRecProMvcExtensions"/>
        /// <seealso cref="MedRecProSwaggerExtensions"/>
        [TestMethod]
        public void MedRecProStartupExtensions_ServiceRegistrations_AddExpectedDescriptors()
        {
            #region implementation
            // Arrange
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=MedRecProStartupExtensionTest;Trusted_Connection=True;",
                ["Security:DB:PKSecret"] = "startup-extension-test-secret",
                ["ClaudeApiSettings:ApiKey"] = "test-api-key",
                ["Authentication:Google:ClientId"] = "google-client-id",
                ["Authentication:Google:ClientSecret"] = "google-client-secret",
                ["Authentication:Microsoft:ClientId"] = "microsoft-client-id",
                ["Authentication:Microsoft:ClientSecret:Dev"] = "microsoft-client-secret",
                ["FeatureFlags:BackgroundProcessingEnabled"] = "true",
                ["TarpitSettings:Enabled"] = "false",
                ["IgnoreEmptyObjectsWhenSerializing"] = "false",
                ["Version"] = "test"
            });

            // Act
            builder.AddMedRecProKeyVault();
            builder.AddMedRecProDataAccess();
            builder.AddMedRecProConfigurationSettings();
            builder.Services.AddMedRecProPlatformServices(builder.Configuration);
            builder.Services.AddMedRecProAi();
            builder.Services.AddMedRecProUserServices();
            builder.Services.AddMedRecProImport();
            builder.Services.AddMedRecProBackgroundServices(builder.Configuration);
            builder.Services.AddMedRecProRendering();
            builder.Services.AddMedRecProSession();
            builder.Services.AddMedRecProAuth(builder.Configuration);
            builder.Services.AddMedRecProApiControllers(builder.Configuration);
            builder.Services.AddMedRecProSwagger(builder.Configuration);
            builder.Services.AddMedRecProRequestLimits();
            builder.Services.AddMedRecProViews(builder.Configuration);

            // Assert - representative registrations from each extracted startup capability.
            assertService<DbContextOptions<ApplicationDbContext>>(builder.Services, ServiceLifetime.Scoped);
            assertService<DbContextOptions<ImportApplicationDbContext>>(builder.Services, ServiceLifetime.Scoped);
            assertService<TimeProvider>(builder.Services, ServiceLifetime.Singleton);
            assertService<IAppCache>(builder.Services, ServiceLifetime.Singleton);
            assertService<QueryCachePolicy>(builder.Services, ServiceLifetime.Singleton);
            assertService<IUserContextAccessor>(builder.Services, ServiceLifetime.Scoped);
            assertService<IPrimaryKeyCipher>(builder.Services, ServiceLifetime.Singleton);
            assertService<UserDataAccess>(builder.Services, ServiceLifetime.Scoped);
            assertService<TarpitService>(builder.Services, ServiceLifetime.Singleton);
            assertService<ActivityLogActionFilter>(builder.Services, ServiceLifetime.Scoped);
            assertService<IClaudeSkillService>(builder.Services, ServiceLifetime.Singleton);
            assertService<IComparisonService>(builder.Services, ServiceLifetime.Scoped);
            assertService<IComparisonJobCoordinator>(builder.Services, ServiceLifetime.Singleton);
            assertService<ICompleteLabelService>(builder.Services, ServiceLifetime.Scoped);
            assertService<ILabelSectionCrudService>(builder.Services, ServiceLifetime.Scoped);
            assertService<ILabelXmlDocumentService>(builder.Services, ServiceLifetime.Scoped);
            assertService<ILabelAiSearchService>(builder.Services, ServiceLifetime.Scoped);
            assertService<IActivityLogService>(builder.Services, ServiceLifetime.Scoped);
            assertService<IPermissionService>(builder.Services, ServiceLifetime.Scoped);
            assertService<IBackgroundTaskQueueService>(builder.Services, ServiceLifetime.Singleton);
            assertService<IOperationStatusStore>(builder.Services, ServiceLifetime.Singleton);
            assertService<IImportOperationStatusStore>(builder.Services, ServiceLifetime.Singleton);
            assertService<IEncryptionService>(builder.Services, ServiceLifetime.Singleton);
            assertService<IDictionaryUtilityService>(builder.Services, ServiceLifetime.Singleton);
            assertService<IPasswordHasher<User>>(builder.Services, ServiceLifetime.Scoped);
            assertService<IViewRenderService>(builder.Services, ServiceLifetime.Scoped);
            assertOpenGeneric(typeof(Repository<>), builder.Services, ServiceLifetime.Scoped);
            assertOpenGeneric(typeof(MedRecProImportClass.DataAccess.Repository<>), builder.Services, ServiceLifetime.Scoped);

            using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();
            using var secondScope = provider.CreateScope();

            Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<ActivityLogActionFilter>());
            Assert.AreSame(
                provider.GetRequiredService<IPrimaryKeyCipher>(),
                scope.ServiceProvider.GetRequiredService<IPrimaryKeyCipher>());
            Assert.AreNotSame(
                scope.ServiceProvider.GetRequiredService<UserDataAccess>(),
                secondScope.ServiceProvider.GetRequiredService<UserDataAccess>());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies label-query feature registration keeps cache policies singleton-safe and data work scoped.
        /// </summary>
        /// <seealso cref="MedRecProApplicationServiceExtensions.AddMedRecProLabelQueryServices(IServiceCollection)"/>
        [TestMethod]
        public void AddMedRecProLabelQueryServices_RegistersExpectedSingletonAndScopedServices()
        {
            #region implementation

            var services = new ServiceCollection();
            services.AddSingleton<IAppCache, PerformanceAppCache>();
            services.AddSingleton<QueryCachePolicy>(serviceProvider => new QueryCachePolicy(
                serviceProvider.GetRequiredService<IAppCache>()));
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = MedRecProTestConfiguration.PkSecret
                })
                .Build());
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(
                $"LabelQueryRegistration_{Guid.NewGuid():N}"));
            services.AddLogging();

            var result = services.AddMedRecProLabelQueryServices();

            Assert.AreSame(services, result);
            assertService<LegacyDtoLabelCacheKeyBuilder>(services, ServiceLifetime.Singleton);
            assertService<QueryCachePolicy>(services, ServiceLifetime.Singleton);
            assertService<LabelQueryDataAccess>(services, ServiceLifetime.Scoped);
            assertScoped<IIngredientSearchService, IngredientSearchService>(services);
            assertScoped<IPharmacologicClassSearchService, PharmacologicClassSearchService>(services);
            assertScoped<IProductSearchService, ProductSearchService>(services);
            assertScoped<ILabelContentQueryService, LabelContentQueryService>(services);
            assertScoped<ILabelMarkdownService, LabelMarkdownService>(services);
            assertScoped<ILabelDocumentQueryService, LabelDocumentQueryService>(services);
            assertScoped<ILabelXmlDocumentService, LabelXmlDocumentService>(services);
            assertScoped<IOrangeBookPatentQueryService, OrangeBookPatentQueryService>(services);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true
            });
            var policy = provider.GetRequiredService<QueryCachePolicy>();
            var keyBuilder = provider.GetRequiredService<LegacyDtoLabelCacheKeyBuilder>();
            using var firstScope = provider.CreateScope();
            using var secondScope = provider.CreateScope();
            var firstQuery = firstScope.ServiceProvider.GetRequiredService<LabelQueryDataAccess>();
            var repeatedFirstQuery = firstScope.ServiceProvider.GetRequiredService<LabelQueryDataAccess>();
            var secondQuery = secondScope.ServiceProvider.GetRequiredService<LabelQueryDataAccess>();

            Assert.AreSame(policy, firstScope.ServiceProvider.GetRequiredService<QueryCachePolicy>());
            Assert.AreSame(keyBuilder, firstScope.ServiceProvider.GetRequiredService<LegacyDtoLabelCacheKeyBuilder>());
            Assert.AreSame(firstQuery, repeatedFirstQuery);
            Assert.AreNotSame(firstQuery, secondQuery);
            Assert.IsNotNull(firstScope.ServiceProvider.GetRequiredService<IProductSearchService>());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies AE dashboard feature registration isolates singleton policies from scoped data services.
        /// </summary>
        /// <seealso cref="MedRecProApplicationServiceExtensions.AddMedRecProAeDashboardServices(IServiceCollection)"/>
        [TestMethod]
        public void AddMedRecProAeDashboardServices_RegistersExpectedSingletonAndScopedServices()
        {
            #region implementation

            var services = new ServiceCollection();

            var result = services.AddMedRecProAeDashboardServices();

            Assert.AreSame(services, result);
            assertService<IAeDashboardCachePolicy>(services, ServiceLifetime.Singleton);
            assertService<IAeDashboardEncryptedIdMapper>(services, ServiceLifetime.Singleton);
            assertService<IAeDashboardCorrelationPolicy>(services, ServiceLifetime.Singleton);
            assertService<IAeDashboardDtoMapper>(services, ServiceLifetime.Singleton);
            assertService<AeDashboardDataAccess>(services, ServiceLifetime.Scoped);
            assertScoped<IAeDashboardProductCatalogService, AeDashboardProductCatalogService>(services);
            assertScoped<IAeDashboardProductDetailService, AeDashboardProductDetailService>(services);
            assertScoped<IAeDashboardFavoriteService, AeDashboardFavoriteService>(services);
            assertScoped<IAeDashboardClassCorrelationService, AeDashboardClassCorrelationService>(services);
            assertScoped<IAeDashboardSystemCorrelationService, AeDashboardSystemCorrelationService>(services);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the extracted MedRecPro middleware extension methods attach to a minimal web application.
        /// </summary>
        /// <remarks>
        /// The test does not start a server; it only proves the extension methods return the same pipeline instance after registration.
        /// </remarks>
        /// <seealso cref="MedRecProMiddlewareExtensions"/>
        /// <seealso cref="MedRecProSwaggerExtensions.UseMedRecProSwagger(WebApplication)"/>
        [TestMethod]
        public void MedRecProStartupExtensions_MiddlewareRegistration_ReturnsApplication()
        {
            #region implementation
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.Services.AddLogging();
            builder.Services.AddRouting();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowLocalDevelopment", policy =>
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            Assert.AreSame(app, app.UseMedRecProExceptionHandling());
            Assert.AreSame(app, app.UseMedRecProSwagger());
            Assert.AreSame(app, app.UseMedRecProSplStaticFiles());
            Assert.AreSame(app, app.UseMedRecProCors());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that a service descriptor exists with the expected implementation and lifetime.
        /// </summary>
        /// <typeparam name="TService">Service contract type.</typeparam>
        /// <typeparam name="TImplementation">Implementation type.</typeparam>
        /// <param name="services">Service collection to inspect.</param>
        /// <seealso cref="ServiceDescriptor"/>
        private static void assertScoped<TService, TImplementation>(IServiceCollection services)
        {
            #region implementation
            var descriptor = services.SingleOrDefault(service =>
                service.ServiceType == typeof(TService) &&
                service.ImplementationType == typeof(TImplementation));

            Assert.IsNotNull(descriptor, $"Missing registration for {typeof(TService).Name}.");
            Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that a service descriptor exists with the expected service type and lifetime.
        /// </summary>
        /// <typeparam name="TService">Service contract type.</typeparam>
        /// <param name="services">Service collection to inspect.</param>
        /// <param name="lifetime">Expected service lifetime.</param>
        /// <seealso cref="ServiceDescriptor"/>
        private static void assertService<TService>(IServiceCollection services, ServiceLifetime lifetime)
        {
            #region implementation
            var descriptor = services.LastOrDefault(service =>
                service.ServiceType == typeof(TService));

            Assert.IsNotNull(descriptor, $"Missing registration for {typeof(TService).Name}.");
            Assert.AreEqual(lifetime, descriptor!.Lifetime);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that an open generic service descriptor exists with the expected lifetime.
        /// </summary>
        /// <param name="serviceType">Open generic service type to inspect.</param>
        /// <param name="services">Service collection to inspect.</param>
        /// <param name="lifetime">Expected service lifetime.</param>
        /// <seealso cref="ServiceDescriptor"/>
        private static void assertOpenGeneric(Type serviceType, IServiceCollection services, ServiceLifetime lifetime)
        {
            #region implementation
            var descriptor = services.SingleOrDefault(service =>
                service.ServiceType == serviceType);

            Assert.IsNotNull(descriptor, $"Missing registration for {serviceType.Name}.");
            Assert.AreEqual(lifetime, descriptor!.Lifetime);
            #endregion
        }

        #endregion
    }
}
