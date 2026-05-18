using MedRecProImportClass.Data;
using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Registers the table-standardization service graph used by Stage 1 through
    /// Stage 5 table parsing, validation, standardization, and AE denormalization.
    /// </summary>
    /// <remarks>
    /// The extension centralizes table-standardization composition while leaving
    /// host-specific concerns such as database provider setup, Claude settings,
    /// QC model settings, and logging providers with the application host.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    /// <seealso cref="ColumnStandardizationService"/>
    public static class TableStandardizationServiceCollectionExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Adds table-standardization services to the supplied service collection.
        /// </summary>
        /// <remarks>
        /// Registrations are idempotent where practical through <c>TryAdd</c> and
        /// <c>TryAddEnumerable</c>. Optional services such as
        /// <see cref="IClaudeApiCorrectionService"/> and
        /// <see cref="IQCNetCorrectionService"/> are not created here; if the host
        /// registers them, the orchestrator factory will consume them.
        /// </remarks>
        /// <param name="services">Service collection to update.</param>
        /// <param name="includeValidation">Whether to register Stage 4 validation services.</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">Whether Stage 3.25 should drop rows missing both ArmN and PrimaryValue.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <seealso cref="ITableParsingOrchestrator"/>
        /// <seealso cref="IColumnStandardizationService"/>
        /// <seealso cref="IAdverseEventDenormalizationService"/>
        public static IServiceCollection AddTableStandardization(
            this IServiceCollection services,
            bool includeValidation = false,
            bool dropRowsMissingArmNOrPrimaryValue = false)
        {
            #region implementation

            services.TryAddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            services.TryAddScoped<ITableCellContextService, TableCellContextService>();
            services.TryAddScoped<ITableReconstructionService, TableReconstructionService>();

            services.TryAddEnumerable(ServiceDescriptor.Scoped<ITableParser, PkTableParser>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ITableParser, SimpleArmTableParser>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ITableParser, MultilevelAeTableParser>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ITableParser, AeWithSocTableParser>());
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ITableParser, EfficacyMultilevelTableParser>());
            services.TryAddScoped<ITableParserRouter, TableParserRouter>();

            if (includeValidation)
            {
                services.TryAddScoped<IRowValidationService, RowValidationService>();
                services.TryAddScoped<ITableValidationService, TableValidationService>();
                services.TryAddScoped<IBatchValidationService, BatchValidationService>();
            }

            services.TryAddSingleton<IPlaceboArmClassifier, PlaceboArmClassifier>();
            services.TryAddSingleton<IAeParameterCategoryDictionaryService, AeParameterCategoryDictionaryService>();
            services.TryAddSingleton<IColumnContractRegistry, ColumnContractRegistry>();
            services.TryAddSingleton<IParseQualityService, ParseQualityService>();

            services.TryAddScoped<IColumnStandardizationService, ColumnStandardizationService>();
            services.TryAddScoped<IBioequivalentLabelDedupService, BioequivalentLabelDedupService>();
            services.TryAddScoped<IAdverseEventDenormalizationService, AdverseEventDenormalizationService>();

            services.TryAddScoped<ITableParsingOrchestrator>(sp => new TableParsingOrchestrator(
                sp.GetRequiredService<ITableReconstructionService>(),
                sp.GetRequiredService<ITableCellContextService>(),
                sp.GetRequiredService<ITableParserRouter>(),
                sp.GetRequiredService<ApplicationDbContext>(),
                sp.GetRequiredService<ILogger<TableParsingOrchestrator>>(),
                batchValidator: sp.GetService<IBatchValidationService>(),
                columnStandardizer: sp.GetService<IColumnStandardizationService>(),
                qcNetCorrectionService: sp.GetService<IQCNetCorrectionService>(),
                correctionService: sp.GetService<IClaudeApiCorrectionService>(),
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue,
                bioequivalentDedup: sp.GetService<IBioequivalentLabelDedupService>(),
                aeDenormalizer: sp.GetService<IAdverseEventDenormalizationService>()));

            return services;

            #endregion
        }
    }
}
