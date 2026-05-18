using MedRecProImportClass.Data;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="TableStandardizationServiceCollectionExtensions"/>.
    /// </summary>
    /// <remarks>
    /// The extension is the shared composition root for table-standardization
    /// services. These tests keep DI drift visible as parser, validation,
    /// standardization, and denormalization services move between classes.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    [TestClass]
    public class TableStandardizationServiceCollectionExtensionsTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies the extension registers the complete core service graph and
        /// remains idempotent when a host calls it more than once.
        /// </summary>
        [TestMethod]
        public void AddTableStandardization_RegistersResolvableCoreGraph()
        {
            #region implementation

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"TableStdDi_{Guid.NewGuid()}"));

            services.AddTableStandardization(includeValidation: true, dropRowsMissingArmNOrPrimaryValue: true);
            services.AddTableStandardization(includeValidation: true, dropRowsMissingArmNOrPrimaryValue: true);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();

            var scopedProvider = scope.ServiceProvider;

            Assert.IsNotNull(scopedProvider.GetRequiredService<ITableParsingOrchestrator>());
            Assert.IsNotNull(scopedProvider.GetRequiredService<IColumnStandardizationService>());
            Assert.IsNotNull(scopedProvider.GetRequiredService<IAdverseEventDenormalizationService>());
            Assert.IsNotNull(scopedProvider.GetRequiredService<IBatchValidationService>());
            Assert.IsNotNull(scopedProvider.GetRequiredService<IParseQualityService>());
            Assert.IsNotNull(scopedProvider.GetRequiredService<IPlaceboArmClassifier>());

            var parsers = scopedProvider.GetServices<ITableParser>().ToList();
            Assert.AreEqual(5, parsers.Count, "TryAddEnumerable should keep parser registrations idempotent.");

            #endregion
        }
    }
}
