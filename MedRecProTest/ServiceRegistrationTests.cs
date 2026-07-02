using MedRecPro.Configuration;
using MedRecPro.Helpers;
using MedRecPro.Middleware;
using MedRecPro.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public service and middleware registration extension methods.
    /// </summary>
    /// <seealso cref="SplRenderingServiceRegistration"/>
    /// <seealso cref="TarpitMiddlewareExtensions"/>
    [TestClass]
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

        #endregion
    }
}
