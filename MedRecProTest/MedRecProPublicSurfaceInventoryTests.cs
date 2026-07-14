using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MedRecProTest;
using MedRecPro.Service.Test.ParsingServices;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Public surface inventory guard for the MedRecPro assembly. Enumerates every
    /// eligible public method via reflection and asserts that each one is accounted
    /// for in either <see cref="CoverageMap"/> (mapped to the test class that covers
    /// it) or <see cref="KnownGapMap"/> (an acknowledged, reasoned coverage gap).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keys use Option A format: "{TypeSimpleName}.{MethodName}" with generic arity
    /// stripped from the type name (Repository`1 becomes Repository) and generic
    /// arguments stripped from the method name, compared with
    /// <see cref="StringComparer.Ordinal"/>. Overloads collapse into a single key.
    /// </para>
    /// <para>
    /// Eligible methods are the public declared methods of public (or nested public)
    /// classes, excluding: delegates, compiler-generated types/methods, null-namespace
    /// types (top-level Program), Migrations namespaces, Controllers namespaces
    /// (MedRecPro.Controllers and MedRecPro.Api.Controllers), ControllerBase-derived
    /// types, property/event accessors and operators (IsSpecialName), and overrides
    /// of <see cref="object"/> members.
    /// </para>
    /// <para>
    /// When this test fails, the assertion message lists the exact keys to add or
    /// remove. Adding a public method to MedRecPro requires either a covering test
    /// (add the key to <see cref="CoverageMap"/>) or an explicit reasoned entry in
    /// <see cref="KnownGapMap"/>. Do NOT loosen the reflection filters to make the
    /// test pass.
    /// </para>
    /// <para>
    /// This guard covers only the MedRecPro assembly. The companion guard
    /// <see cref="ParsingServicesPublicSurfaceInventoryTests"/> covers the separate
    /// MedRecProImportClass assembly; identical Type.Method keys may legitimately
    /// appear in both guards' maps because the parsing service types are duplicated
    /// across the two assemblies.
    /// </para>
    /// </remarks>
    /// <seealso cref="ParsingServicesPublicSurfaceInventoryTests"/>
    /// <seealso cref="MedRecPro.Helpers.TextUtil"/>
    [TestClass]
    public class MedRecProPublicSurfaceInventoryTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Maps every covered public method key ("TypeSimpleName.MethodName") to the
        /// name of the test class that exercises it. Values must be existing class
        /// names in this test assembly (verified by the guard test).
        /// </summary>
        /// <remarks>
        /// Entries were generated from the ground-truth coverage classification of the
        /// 2026-07-02 assembly build: grep-covered methods map to the test file that
        /// references them, and grep-uncovered methods whose declaring type has a
        /// matching {Type}Tests class (the done plan's coverage signal rule) map to
        /// that class. Sorted ordinally by key.
        /// </remarks>
        /// <seealso cref="KnownGapMap"/>
        private static readonly Dictionary<string, string> CoverageMap = new(StringComparer.Ordinal)
        {
            ["ActivityLogDto.FromActivityLogs"] = nameof(ModelPublicSurfaceTests),
            ["ActivityLogService.GetActivityByEndpointAsync"] = nameof(ActivityLogServiceTests),
            ["ActivityLogService.GetActivityByTimeRangeAsync"] = nameof(ActivityLogServiceTests),
            ["ActivityLogService.GetUserActivityAsync"] = nameof(ActivityLogServiceTests),
            ["ActivityLogService.GetUserActivityByDateRangeAsync"] = nameof(ActivityLogServiceTests),
            ["ActivityLogService.LogActivityAsync"] = nameof(ActivityLogServiceTests),
            ["AeDashboardDerivation.AggregatePerClassDrug"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.AggregatePerClassTerm"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.AggregatePerDrugSoc"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardDerivation.BuildActiveIngredients"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildCorrelationCellDetail"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildCorrelationHeatmap"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildCorrelationMap"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildForestPlot"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildInterchangeComparison"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildQuadrantView"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildReverseLookupResult"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildSystemClassCellDetail"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildSystemClassCorrelationMap"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildSystemClassHeatmap"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.BuildTriageView"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ClassifyCounselingTier"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ClassifyPrecision"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ClassifyReverseLookupVerdict"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ComputeCorrelation"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardDerivation.DeriveProduct"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.DeriveProducts"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.DeriveSignal"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.DeriveSignals"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ExtractPharmacologicClassType"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardDerivation.NormalizePharmacologicClassTypeFilter"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardDerivation.ParseFlags"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ParseNumberNeededType"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.ParseRiskSignificance"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.StableDrugKey"] = nameof(AeDashboardDerivationTests),
            ["AeDashboardDerivation.StudentTTwoSidedP"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCachePolicy.GenerateKey"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCachePolicy.Get"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCachePolicy.Set"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardClassCorrelationService.GetCorrelationCellDetailAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardClassCorrelationService.GetCorrelationClassesAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardClassCorrelationService.GetCorrelationHeatmapAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardClassCorrelationService.GetCorrelationMapAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardCorrelationPolicy.BuildSystemCorrelationFilters"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCorrelationPolicy.CanonicalizeSelectedSystems"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCorrelationPolicy.IsSeriousCorrelationSoc"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCorrelationPolicy.NormalizeSystemInputs"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardCorrelationPolicy.ValidateCorrelationEnums"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardEncryptedIdMapper.DecryptNullableInt"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardEncryptedIdMapper.EncryptNullableInt"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardFavoriteService.GetFavoriteDrugSummariesAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardFavoriteService.SetProductFavoriteAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductCatalogService.GetDrugSummariesAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductCatalogService.GetProductCatalogAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductCatalogService.GetProductCountAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductDetailService.GetForestPlotAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductDetailService.GetInterchangeAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductDetailService.GetProductDetailDataAsync"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardProductDetailService.GetQuadrantViewAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductDetailService.GetReverseLookupAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardProductDetailService.GetRiskSignalsByDocumentAsync"] = nameof(AeDashboardDataAccessTests),
            ["AeDashboardProductDetailService.GetTriageViewAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardSystemCorrelationService.GetCorrelationSystemsAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardSystemCorrelationService.GetSystemCorrelationCellDetailAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardSystemCorrelationService.GetSystemCorrelationHeatmapAsync"] = nameof(AdverseEventControllerTests),
            ["AeDashboardSystemCorrelationService.GetSystemCorrelationMapAsync"] = nameof(AdverseEventControllerTests),
            ["ApplicationDbContext.Difference"] = nameof(ApplicationDbContextFunctionTests),
            ["ApplicationDbContext.Soundex"] = nameof(ApplicationDbContextFunctionTests),
            ["AuthorRenderingService.PrepareAuthorsForRendering"] = nameof(SplAuthorRenderingServiceTests),
            ["AuthorRenderingService.PrepareForRendering"] = nameof(SplAuthorRenderingServiceTests),
            ["BackgroundTaskQueueService.Enqueue"] = nameof(BackgroundTaskQueueServiceTests),
            ["BackgroundTaskQueueService.TryDequeue"] = nameof(BackgroundTaskQueueServiceTests),
            ["BufferedFile.BufferFilesToTempAsync"] = nameof(ModelPublicSurfaceTests),
            ["CharacteristicRenderingService.FormatBooleanValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.FormatIntegerValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.GetNormalizedValueType"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasBooleanValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasCodedValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasIntegerValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasOriginalText"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasQuantityValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.HasStringValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.PrepareForRendering"] = nameof(ProductRenderingServiceTests),
            ["CharacteristicRenderingService.ShouldRenderAsBoolean"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.ShouldRenderAsCodedElement"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.ShouldRenderAsInteger"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.ShouldRenderAsPhysicalQuantity"] = nameof(SplCharacteristicRenderingServiceTests),
            ["CharacteristicRenderingService.ShouldRenderAsString"] = nameof(SplCharacteristicRenderingServiceTests),
            ["ClaimHelper.GetEncryptedUserIdOrThrow"] = nameof(ClaimHelperTests),
            ["ClaimHelper.GetUserIdFromClaims"] = nameof(ClaimHelperTests),
            ["ClaudeApiService.GenerateDocumentComparisonAsync"] = nameof(ComparisonServiceTests),
            ["ClaudeSearchService.SearchByIndicationAsync"] = nameof(ClaudeSearchServiceIndicationTests),
            ["ClaudeSkillService.GetAvailableSkillsAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetCapabilityContractsAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetFullSkillsDocumentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetInterfaceDocumentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetResponseFormatDocumentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetSelectorsDocumentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetSkillByNameAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetSkillContentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetSkillManifestAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.GetSynthesisRulesDocumentAsync"] = nameof(ClaudeSkillServiceTests),
            ["ClaudeSkillService.SelectSkillsAsync"] = nameof(ClaudeSkillServiceTests),
            ["ComparisonService.GenerateComparisonAsync"] = nameof(ComparisonServiceTests),
            ["ComparisonService.GenerateDocumentComparisonAsync"] = nameof(ComparisonServiceTests),
            ["ComparisonService.IsSplDataReadyForComparisonAsync"] = nameof(ComparisonServiceTests),
            ["ComparisonJobCoordinator.Enqueue"] = nameof(LabelComparisonControllerScopeTests),
            ["CompleteLabelService.GetAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelAiSearchService.ExtractProductAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelAiSearchService.GetCachedClassSummariesAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelAiSearchService.SearchByIndicationAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelAiSearchService.SearchByPharmacologicClassAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.CreateAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.DeleteAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.GetAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.GetByIdAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.GetDocumentation"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.GetMenu"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelSectionCrudService.UpdateAsync"] = nameof(LabelControllerDecompositionServiceTests),
            ["LabelXmlDocumentService.GetGeneratedAsync"] = nameof(LabelXmlDocumentServiceTests),
            ["LabelXmlDocumentService.GetOriginalAsync"] = nameof(LabelXmlDocumentServiceTests),
            ["PrimaryKeyCipher.Encrypt"] = nameof(LabelControllerDecompositionServiceTests),
            ["PrimaryKeyCipher.TryDecrypt"] = nameof(LabelControllerDecompositionServiceTests),
            ["ConversationStore.AddMessage"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Clear"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Create"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Exists"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Get"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.GetMessages"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.GetOrCreate"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.GetStats"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Remove"] = nameof(ClaudeConversationStoreTests),
            ["ConversationStore.Touch"] = nameof(ClaudeConversationStoreTests),
            ["DatabaseKeepAliveService.Dispose"] = nameof(DatabaseKeepAliveServiceTests),
            ["DatabaseKeepAliveService.StartAsync"] = nameof(DatabaseKeepAliveServiceTests),
            ["DatabaseKeepAliveService.StopAsync"] = nameof(DatabaseKeepAliveServiceTests),
            ["DictionaryUtilityService.GetAvailableKeys"] = nameof(CommonServiceTests),
            ["DictionaryUtilityService.SafeGet"] = nameof(CommonServiceTests),
            ["DocumentRenderingService.GenerateIdRootAttribute"] = nameof(SplDocumentRenderingServiceTests),
            ["DocumentRenderingService.GetOrderedAuthors"] = nameof(SplDocumentRenderingServiceTests),
            ["DocumentRenderingService.GetOrderedStructuredBodies"] = nameof(SplDocumentRenderingServiceTests),
            ["DocumentRenderingService.HasValidDocument"] = nameof(SplDocumentRenderingServiceTests),
            ["DocumentRenderingService.PrepareForRendering"] = nameof(SplDocumentRenderingServiceTests),
            ["DosingSpecificationValidationService.ValidateDoseQuantity"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateDosingSpecification"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateRouteCode"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateUcumUnit"] = nameof(DosingSpecificationParserTests),
            ["DtoLabelAccess.BuildDocumentsAsync"] = nameof(DtoLabelAccessDocumentTests),
            ["DtoLabelAccess.CountExpiringPatentsAsync"] = nameof(DtoLabelAccessOrangeBookTests),
            ["DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.FindRelatedIngredientsAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GenerateCleanLabelMarkdownAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GenerateLabelMarkdownAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetAPIEndpointGuideAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetAeCorrelationCellDetailAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeCorrelationClassesAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeCorrelationHeatmapAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeCorrelationMapAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeCorrelationSystemsAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeDrugSummariesAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeFavoriteDrugSummariesAsync"] = nameof(AeDashboardFavoriteAccessTests),
            ["DtoLabelAccess.GetAeForestPlotAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeInterchangeAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeProductCatalogAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeProductCountAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeProductDetailDataAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeQuadrantViewAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeReverseLookupAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeRiskSignalsByDocumentAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeSystemCorrelationMapAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetAeTriageViewAsync"] = nameof(AeDashboardDataAccessTests),
            ["DtoLabelAccess.GetApplicationNumberSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetDEAScheduleProductsAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetDocumentNavigationAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetDocumentVersionHistoryAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetDrugInteractionsAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetIngredientActiveSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetIngredientInactiveSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetIngredientSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetInventorySummaryAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetLabelSectionMarkdownAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetLabelerSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetPackageIdentifierAsync"] = nameof(DtoLabelAccessDocumentTests),
            ["DtoLabelAccess.GetPharmacologicClassHierarchyAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetPharmacologicClassSummariesAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.GetProductIndicationsAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetProductLatestLabelsAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetRelatedProductsAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetSectionContentAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.GetSectionTypeSummariesAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.SearchByApplicationNumberAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByIngredientAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByLabelerAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByNDCAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByPackageNDCAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByPharmacologicClassAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchByPharmacologicClassExactAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchBySectionCodeAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.SearchIngredientsAdvancedAsync"] = nameof(DtoLabelAccessViewNavigationTests),
            ["DtoLabelAccess.SearchOrangeBookPatentsAsync"] = nameof(DtoLabelAccessOrangeBookTests),
            ["DtoLabelAccess.SearchProductSummaryAsync"] = nameof(DtoLabelAccessContentTests),
            ["DtoLabelAccess.SetAeProductFavoriteAsync"] = nameof(AeDashboardFavoriteAccessTests),
            ["DtoTransform.GetClassDocumentation"] = nameof(DtoTransformTests),
            ["DtoTransform.ToEntityMenu"] = nameof(DtoTransformTests),
            ["DtoTransform.ToEntityWithEncryptedId"] = nameof(DtoTransformTests),
            ["EncryptionService.DecryptToInt"] = nameof(CommonServiceTests),
            ["EncryptionService.DecryptToString"] = nameof(CommonServiceTests),
            ["ErrorHelper.AddErrorMsg"] = nameof(ErrorHelperTests),
            ["ErrorHelper.GetErrorMsg"] = nameof(ErrorHelperTests),
            ["ErrorHelper.GetLineNumber"] = nameof(ErrorHelperTests),
            ["ErrorHelper.IsErrorLogged"] = nameof(ErrorHelperTests),
            ["FdaProductConceptHelper.GenerateKitConceptCode"] = nameof(FdaProductConceptHelperTests),
            ["FdaProductConceptHelper.GenerateProductConceptCode"] = nameof(FdaProductConceptHelperTests),
            ["FdaProductConceptHelper.ValidateConceptCodeFormat"] = nameof(FdaProductConceptHelperTests),
            ["ImportResultMapper.ToWebResults"] = nameof(ImportResultMapperTests),
            ["ImportResultMapper.ToWebStatus"] = nameof(ImportResultMapperTests),
            ["InMemoryOperationStatusStore.Set"] = nameof(OperationStatusStoreTests),
            ["InMemoryOperationStatusStore.TryGet"] = nameof(OperationStatusStoreTests),
            ["IngredientRenderingService.FormatSubstanceName"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.GenerateClassCodeAttribute"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.GetOrderedActiveMoieties"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.GetOrderedSpecifiedSubstances"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.GetPrimaryReferenceSubstance"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.HasDenominatorTranslation"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.HasNumeratorTranslation"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.HasQuantityData"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.HasSubstanceData"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.IsActiveIngredient"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.PrepareForRendering"] = nameof(SplIngredientRenderingServiceTests),
            ["IngredientRenderingService.RequiresReferenceSubstance"] = nameof(SplIngredientRenderingServiceTests),
            ["JsonPipeHelper.IsJsonType"] = nameof(JsonPipeHelperTests),
            ["JsonPipeHelper.TryConvertToPipe"] = nameof(JsonPipeHelperTests),
            ["LoggerExtensions.AddUserLogger"] = nameof(LogHelperTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProAi"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProAeDashboardServices"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProBackgroundServices"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProConfigurationSettings"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProDataAccess"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProImport"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProKeyVault"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProLabelQueryServices"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProPlatformServices"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProRendering"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProRequestLimits"] = nameof(ServiceRegistrationTests),
            ["MedRecProApplicationServiceExtensions.AddMedRecProUserServices"] = nameof(ServiceRegistrationTests),
            ["MedRecProAuthenticationExtensions.AddMedRecProAuth"] = nameof(ServiceRegistrationTests),
            ["MedRecProAuthenticationExtensions.AddMedRecProSession"] = nameof(ServiceRegistrationTests),
            ["MedRecProExceptionHandler.TryHandleAsync"] = nameof(MedRecProExceptionHandlerTests),
            ["MedRecProMiddlewareExtensions.UseMedRecProCors"] = nameof(ServiceRegistrationTests),
            ["MedRecProMiddlewareExtensions.UseMedRecProExceptionHandling"] = nameof(ServiceRegistrationTests),
            ["MedRecProMiddlewareExtensions.UseMedRecProSplStaticFiles"] = nameof(ServiceRegistrationTests),
            ["MedRecProMvcExtensions.AddMedRecProApiControllers"] = nameof(ServiceRegistrationTests),
            ["MedRecProMvcExtensions.AddMedRecProViews"] = nameof(ServiceRegistrationTests),
            ["MedRecProSwaggerExtensions.AddMedRecProSwagger"] = nameof(ServiceRegistrationTests),
            ["MedRecProSwaggerExtensions.UseMedRecProSwagger"] = nameof(ServiceRegistrationTests),
            ["NewUser.ToUser"] = nameof(ModelPublicSurfaceTests),
            ["OperationStatusStoreExtensions.ClearStatusesByType"] = nameof(OperationStatusStoreTests),
            ["OperationStatusStoreExtensions.GetSupportedTypes"] = nameof(OperationStatusStoreTests),
            ["OperationStatusStoreExtensions.Set"] = nameof(OperationStatusStoreTests),
            ["OperationStatusStoreExtensions.SetComparisonStatus"] = nameof(OperationStatusStoreTests),
            ["OperationStatusStoreExtensions.TryGet"] = nameof(OperationStatusStoreTests),
            ["OperationStatusStoreExtensions.TryGetComparisonStatus"] = nameof(OperationStatusStoreTests),
            ["PackageRenderingService.FormatQuantity"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.GenerateDisplayAttributes"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.GetOrderedCharacteristicsForPackaging"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.GetOrderedChildPackaging"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.GetOrderedMarketingStatusesForPackage"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.GetOrderedPackageIdentifiers"] = nameof(SplPackageRenderingServiceTests),
            ["PackageRenderingService.HasValidData"] = nameof(ProductRenderingServiceTests),
            ["PackageRenderingService.PrepareForRendering"] = nameof(ProductRenderingServiceTests),
            ["PerformanceHelper.GetCache"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.GetCachedJson"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.RemoveCache"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.ResetManagedCache"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.SetCache"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.SetCacheManageKey"] = nameof(PerformanceHelperTests),
            ["PerformanceHelper.depricated_SetCache"] = nameof(PerformanceHelperTests),
            ["Permission.New"] = nameof(ModelPublicSurfaceTests),
            ["PermissionService.Append"] = nameof(PermissionServiceTests),
            ["PermissionService.Clone"] = nameof(PermissionServiceTests),
            ["PermissionService.Decrypt"] = nameof(PermissionServiceTests),
            ["PermissionService.Encrypt"] = nameof(PermissionServiceTests),
            ["PermissionService.FromJson"] = nameof(PermissionServiceTests),
            ["PermissionService.GetActorTypes"] = nameof(PermissionServiceTests),
            ["PermissionService.HasAnyActorType"] = nameof(PermissionServiceTests),
            ["PermissionService.HasPermission"] = nameof(PermissionServiceTests),
            ["PermissionService.Remove"] = nameof(PermissionServiceTests),
            ["PermissionService.ToDictionary"] = nameof(PermissionServiceTests),
            ["PermissionService.ToJson"] = nameof(PermissionServiceTests),
            ["PermissionService.TryDecrypt"] = nameof(PermissionServiceTests),
            ["PermissionService.Update"] = nameof(PermissionServiceTests),
            ["PermissionService.ValidateUserRole"] = nameof(PermissionServiceTests),
            ["PhoneticMatchOptions.WithScore"] = nameof(EntitySearchHelperTests),
            ["ProductEventValidationService.ValidateEffectiveTime"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["ProductEventValidationService.ValidateEventCode"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["ProductEventValidationService.ValidateProductEvent"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["ProductEventValidationService.ValidateQuantity"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["ProductRenderingService.GetNdcProductIdentifier"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedActiveIngredients"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedCharacteristics"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedEquivalentEntities"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedInactiveIngredients"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedMarketingCategories"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedMarketingStatuses"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedRoutes"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetOrderedTopLevelPackaging"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.GetPrimaryMarketingCategory"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.HasValidData"] = nameof(ProductRenderingServiceTests),
            ["ProductRenderingService.PrepareForRendering"] = nameof(ProductRenderingServiceTests),
            ["REMSValidationService.ValidateProtocol"] = nameof(REMSValidationServiceTests),
            ["REMSValidationService.ValidateRequirement"] = nameof(REMSValidationServiceTests),
            ["REMSValidationService.ValidateStakeholder"] = nameof(REMSValidationServiceTests),
            ["SearchFilterExtensions.FilterBySearchTerms"] = nameof(EntitySearchHelperTests),
            ["SearchFilterExtensions.ParseSearchTerms"] = nameof(EntitySearchHelperTests),
            ["SectionHierarchyService.BuildChildSections"] = nameof(SplSectionHierarchyServiceTests),
            ["SectionHierarchyService.CreateSectionLookup"] = nameof(SplSectionHierarchyServiceTests),
            ["SectionHierarchyService.GetValidSections"] = nameof(SplSectionHierarchyServiceTests),
            ["SectionHierarchyService.OrganizeSections"] = nameof(SplSectionHierarchyServiceTests),
            ["SectionRendering.GetOrderedChildren"] = nameof(ModelPublicSurfaceTests),
            ["SectionRenderingService.GenerateSectionIdAttribute"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.GetOrderedExcerptHighlights"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.GetOrderedMedia"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.GetOrderedProducts"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.GetOrderedTextContent"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.GetSectionCodeSystemName"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.HasSectionCodeData"] = nameof(SplSectionRenderingServiceTests),
            ["SectionRenderingService.PrepareSectionForRendering"] = nameof(SplSectionRenderingServiceTests),
            ["SpecializedKindValidatorService.ValidateCosmeticCategoryRules"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplDataService.GetOrCreateSplDataAsync"] = nameof(SplImportServiceTests),
            ["SplDataService.GetSplDataByGuidAsync"] = nameof(ComparisonServiceTests),
            ["SplDataService.IsDuplicateSplDataAsync"] = nameof(SplImportServiceTests),
            ["SplImportService.ProcessZipFilesAsync"] = nameof(SplImportServiceTests),
            ["SplParseContext.GetRepository"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplParseContext.SetBatchSavingFlag"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplParseContext.SetBulkOperationsFlag"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplParseContext.SetBulkStagingFlag"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplParseContext.UpdateFileResult"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplParseResult.MergeFrom"] = nameof(ParsingServicesPublicSurfaceInventoryTests),
            ["SplRenderingServiceRegistration.AddDocumentRenderingServices"] = nameof(ServiceRegistrationTests),
            ["SplTemplateHelpers.AttrOrNull"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.Attribute"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.AttributePreEncoded"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.BoolToSplFormat"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.EscapeXmlContent"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.FormatNdcCode"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.FormatNumeric"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.GetAvailableKeys"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.GuidDown"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.GuidUp"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.SafeAttribute"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.SafeAttributeDateTime"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.SafeGet"] = nameof(SplTemplateHelperTests),
            ["SplTemplateHelpers.ToSplDate"] = nameof(SplTemplateHelperTests),
            ["StringCipher.Encrypt"] = nameof(StringCipherTests),
            ["StructuredBodyService.GetSectionHierarchies"] = nameof(SplStructuredBodyRenderingServiceTests),
            ["StructuredBodyService.HasHierarchicalSections"] = nameof(SplStructuredBodyRenderingServiceTests),
            ["StructuredBodyService.HasStandaloneSections"] = nameof(SplStructuredBodyRenderingServiceTests),
            ["TarpitEndpointPolicyResolver.Resolve"] = nameof(TarpitMiddlewareTests),
            ["TarpitMiddleware.InvokeAsync"] = nameof(TarpitMiddlewareTests),
            ["TarpitMiddlewareExtensions.UseTarpitMiddleware"] = nameof(ServiceRegistrationTests),
            ["TarpitService.CalculateDelay"] = nameof(TarpitServiceTests),
            ["TarpitService.CalculateEndpointDelay"] = nameof(TarpitServiceTests),
            ["TarpitService.Dispose"] = nameof(TarpitServiceTests),
            ["TarpitService.GetEndpointHitCount"] = nameof(TarpitServiceTests),
            ["TarpitService.GetHitCount"] = nameof(TarpitServiceTests),
            ["TarpitService.RecordEndpointHit"] = nameof(TarpitServiceTests),
            ["TarpitService.RecordHit"] = nameof(TarpitServiceTests),
            ["TarpitService.ResetClient"] = nameof(TarpitServiceTests),
            ["TarpitSettingsValidator.Validate"] = nameof(TarpitServiceTests),
            ["TextContentRenderingService.AnalyzeContentCharacteristics"] = nameof(SplTextContentRenderingServiceTests),
            ["TextContentRenderingService.DetermineContentType"] = nameof(SplTextContentRenderingServiceTests),
            ["TextContentRenderingService.PrepareTextContentForRendering"] = nameof(SplTextContentRenderingServiceTests),
            ["TextContentRenderingService.PrepareTextContentItemForRendering"] = nameof(SplTextContentRenderingServiceTests),
            ["TextTableColumn.GetEffectiveAlign"] = nameof(ModelPublicSurfaceTests),
            ["TextTableColumn.GetEffectiveStyleCode"] = nameof(ModelPublicSurfaceTests),
            ["TextTableColumn.GetEffectiveVAlign"] = nameof(ModelPublicSurfaceTests),
            ["TextUtil.Base64Decode"] = nameof(TextUtilTests),
            ["TextUtil.Base64Encode"] = nameof(TextUtilTests),
            ["TextUtil.CleanFileName"] = nameof(TextUtilTests),
            ["TextUtil.CommaDelimitedToList"] = nameof(TextUtilTests),
            ["TextUtil.Decrypt"] = nameof(StringCipherTests),
            ["TextUtil.Encrypt"] = nameof(StringCipherTests),
            ["TextUtil.ExtractDivision"] = nameof(TextUtilTests),
            ["TextUtil.ExtractOffice"] = nameof(TextUtilTests),
            ["TextUtil.FixFilePath"] = nameof(TextUtilTests),
            ["TextUtil.FixURLPath"] = nameof(TextUtilTests),
            ["TextUtil.FormatElapsedTime"] = nameof(TextUtilTests),
            ["TextUtil.FromUrlSafeBase64StringManual"] = nameof(TextUtilTests),
            ["TextUtil.GetAssignmentTransferLogEntry"] = nameof(TextUtilTests),
            ["TextUtil.GetDocumentGuidRowXML"] = nameof(TextUtilTests),
            ["TextUtil.GetDocumentTypeAbbreviation"] = nameof(TextUtilTests),
            ["TextUtil.GetFileNameFromUrl"] = nameof(TextUtilTests),
            ["TextUtil.GetLongDateTime"] = nameof(TextUtilTests),
            ["TextUtil.IsCreditCard"] = nameof(TextUtilTests),
            ["TextUtil.IsEmail"] = nameof(TextUtilTests),
            ["TextUtil.IsGuid"] = nameof(TextUtilTests),
            ["TextUtil.IsHexColor"] = nameof(TextUtilTests),
            ["TextUtil.IsHslColor"] = nameof(TextUtilTests),
            ["TextUtil.IsIpAddress"] = nameof(TextUtilTests),
            ["TextUtil.IsIsbn10"] = nameof(TextUtilTests),
            ["TextUtil.IsIsbn13"] = nameof(TextUtilTests),
            ["TextUtil.IsJson"] = nameof(TextUtilTests),
            ["TextUtil.IsMacAddress"] = nameof(TextUtilTests),
            ["TextUtil.IsRgbColor"] = nameof(TextUtilTests),
            ["TextUtil.IsUrl"] = nameof(TextUtilTests),
            ["TextUtil.IsValidEmail"] = nameof(TextUtilTests),
            ["TextUtil.IsXml"] = nameof(TextUtilTests),
            ["TextUtil.ListToCommaString"] = nameof(TextUtilTests),
            ["TextUtil.MinifyXml"] = nameof(TextUtilTests),
            ["TextUtil.NormalizeXmlWhitespace"] = nameof(TextUtilTests),
            ["TextUtil.PhoneNumber"] = nameof(TextUtilTests),
            ["TextUtil.RemoveHtmlXss"] = nameof(TextUtilTests),
            ["TextUtil.RemoveJSONChars"] = nameof(TextUtilTests),
            ["TextUtil.RemoveMiddleInitial"] = nameof(TextUtilTests),
            ["TextUtil.RemoveTags"] = nameof(TextUtilTests),
            ["TextUtil.RemoveUnwantedTags"] = nameof(TextUtilTests),
            ["TextUtil.RemoveUnwantedTagsRegEx"] = nameof(TextUtilTests),
            ["TextUtil.SanitizeXML"] = nameof(TextUtilTests),
            ["TextUtil.SplitName"] = nameof(TextUtilTests),
            ["TextUtil.TimeElapsedPercent"] = nameof(TextUtilTests),
            ["TextUtil.TimeRemainingPercent"] = nameof(TextUtilTests),
            ["TextUtil.ToCommaString"] = nameof(TextUtilTests),
            ["TextUtil.ToCsv"] = nameof(TextUtilTests),
            ["TextUtil.ToCsvFromXml"] = nameof(TextUtilTests),
            ["TextUtil.ToPipe"] = nameof(TextUtilTests),
            ["TextUtil.ToRGBA"] = nameof(TextUtilTests),
            ["TextUtil.ToTitle"] = nameof(TextUtilTests),
            ["TextUtil.ToUrlSafeBase64StringManual"] = nameof(TextUtilTests),
            ["TextUtil.ToXML"] = nameof(TextUtilTests),
            ["TextUtil.Truncate"] = nameof(TextUtilTests),
            ["TextUtil.TruncateMiddle"] = nameof(TextUtilTests),
            ["TextUtil.UnpackDelimitedValues"] = nameof(TextUtilTests),
            ["ThrottleStateService.Dispose"] = nameof(ThrottleStateServiceTests),
            ["ThrottleStateService.GetStateDescription"] = nameof(ThrottleStateServiceTests),
            ["TokenCacheMiddleware.GetTokenCacheKey"] = nameof(TokenCacheMiddlewareHelperTests),
            ["TokenCacheMiddleware.GetTokenFromCache"] = nameof(TokenCacheMiddlewareHelperTests),
            ["TokenCacheMiddleware.InvokeAsync"] = nameof(TokenCacheMiddlewareHelperTests),
            ["TokenCacheMiddlewareExtensions.UseTokenCache"] = nameof(TokenCacheMiddlewareHelperTests),
            ["User.IsUserAdmin"] = nameof(ModelPublicSurfaceTests),
            ["User.SetConfiguration"] = nameof(AdverseEventControllerTests),
            ["UserDataAccess.AuthenticateAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.CreateAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.DeleteAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.GetAllAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.GetByEmailAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.GetByIdAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.RotatePasswordAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.SignUpAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.UpdateAdminAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.UpdateAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.UpdateLastLoginAsync"] = nameof(UserDataAccessTests),
            ["UserDataAccess.UpdateProfileAsync"] = nameof(UserDataAccessTests),
            ["UserFacingUpdateDto.ToUser"] = nameof(ModelPublicSurfaceTests),
            ["UserLogger.BeginScope"] = nameof(LogHelperTests),
            ["UserLogger.CleanupExpiredEntries"] = nameof(LogHelperTests),
            ["UserLogger.GetEntryCount"] = nameof(LogHelperTests),
            ["UserLogger.GetLogs"] = nameof(LogHelperTests),
            ["UserLogger.IsEnabled"] = nameof(LogHelperTests),
            ["UserLogger.Log"] = nameof(LogHelperTests),
            ["UserLogger.RemoveOldestEntries"] = nameof(LogHelperTests),
            ["UserLoggerProvider.CreateLogger"] = nameof(LogHelperTests),
            ["UserLoggerProvider.Dispose"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetCategories"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetLogs"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetLogsByCategory"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetLogsByDateRange"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetLogsByLevel"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetLogsByUser"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetSettings"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetStatistics"] = nameof(LogHelperTests),
            ["UserLoggerProvider.GetUserSummaries"] = nameof(LogHelperTests),
            ["UserLoggerProvider.PerformCleanup"] = nameof(LogHelperTests),
            ["Util.Clone"] = nameof(UtilTests),
            ["Util.ConvertToGUID"] = nameof(UtilTests),
            ["Util.DecryptAndParseInt"] = nameof(UtilTests),
            ["Util.DecryptAndParseString"] = nameof(UtilTests),
            ["Util.GetBearerToken"] = nameof(UtilTests),
            ["Util.GetInterpolatedRedToGreen"] = nameof(UtilTests),
            ["Util.GetJavaScriptTimestamp"] = nameof(UtilTests),
            ["Util.GetListHashString"] = nameof(UtilTests),
            ["Util.GetPropertyValue"] = nameof(UtilTests),
            ["Util.GetPropertyValueAsString"] = nameof(UtilTests),
            ["Util.GetRandomColor"] = nameof(UtilTests),
            ["Util.GetSHA1HashString"] = nameof(UtilTests),
            ["Util.GetSHA256HashString"] = nameof(UtilTests),
            ["Util.GetTokenType"] = nameof(UtilTests),
            ["Util.GetUserName"] = nameof(UtilTests),
            ["Util.Initialize"] = nameof(UtilTests),
            ["Util.IsEqual"] = nameof(UtilTests),
            ["Util.IsNullOrEmpty"] = nameof(UtilTests),
            ["Util.IsNullOrZero"] = nameof(UtilTests),
            ["Util.IsZero"] = nameof(UtilTests),
            ["Util.Normalize"] = nameof(UtilTests),
            ["Util.ParseNullableBool"] = nameof(UtilTests),
            ["Util.ParseNullableBoolWithStringValue"] = nameof(UtilTests),
            ["Util.ParseNullableDateTime"] = nameof(UtilTests),
            ["Util.ParseNullableDecimal"] = nameof(UtilTests),
            ["Util.ParseNullableGuid"] = nameof(UtilTests),
            ["Util.ParseNullableInt"] = nameof(UtilTests),
            ["Util.SafeGet"] = nameof(UtilTests),
            ["Util.SetValueFromString"] = nameof(UtilTests),
            ["Util.TimeoutAfter"] = nameof(UtilTests),
            ["Util.ToFiscalYear"] = nameof(UtilTests),
            ["Util.ToSecureUri"] = nameof(UtilTests),
            ["Util.TryGetGuid"] = nameof(UtilTests),
            ["Util.TryGuidParse"] = nameof(UtilTests),
            ["Util.WaitUntil"] = nameof(UtilTests),
            ["Util.WaitWhile"] = nameof(UtilTests),
            ["Util.parseNullableDecimal"] = nameof(UtilTests),
            ["ValidationResult.AddError"] = nameof(DosingSpecificationParserTests),
            ["ValidationResult.MergeWith"] = nameof(DosingSpecificationParserTests),
            ["WarningLetterDate.ValidateAll"] = nameof(ModelPublicSurfaceTests),
            ["WarningLetterProductInfo.ValidateAll"] = nameof(ModelPublicSurfaceTests),

            // Entries below were added when the pending 52-method test harness
            // plan (Phases A-F plus inventory-triage extras) was implemented.
            ["ActivityLogActionFilter.OnActionExecutionAsync"] = nameof(ActivityLogActionFilterTests),
            ["ActorAuthorizationFilter.OnAuthorizationAsync"] = nameof(AuthorizationFilterTests),
            ["AppOnlyTokenCredential.GetToken"] = nameof(AzureTokenCredentialServiceTests),
            ["AppOnlyTokenCredential.GetTokenAsync"] = nameof(AzureTokenCredentialServiceTests),
            ["ApplicationNumberSearch.Parse"] = nameof(ModelPublicSurfaceTests),
            ["AuthorizationExceptionFilter.OnException"] = nameof(AuthorizationFilterTests),
            ["AzureAppTokenProvider.GetAccessTokenAsync"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureAppTokenProvider.GetAccessTokenWithMetadataAsync"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureAppTokenProvider.GetCredential"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureAppTokenProvider.GetEnvironment"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureAppTokenProvider.GetTokenExpiration"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureAppTokenProvider.TestCredentialAsync"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureManagementTokenProvider.GetAccessTokenAsync"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureManagementTokenProvider.GetTokenExpiration"] = nameof(AzureTokenCredentialServiceTests),
            ["AzureSqlMetricsService.GetFreeTierStatusAsync"] = nameof(AzureSqlMetricsServiceTests),
            ["AzureSqlMetricsService.GetProjectedMonthlyCostAsync"] = nameof(AzureSqlMetricsServiceTests),
            ["AzureSqlMetricsService.GetRemainingFreeTierVCoreSecondsAsync"] = nameof(AzureSqlMetricsServiceTests),
            ["AzureSqlMetricsService.GetUsedVCoreSecondsThisMonthAsync"] = nameof(AzureSqlMetricsServiceTests),
            ["AzureSqlMetricsService.ShouldThrottleAsync"] = nameof(AzureSqlMetricsServiceTests),
            ["CharacteristicRenderingService.FormatQuantityValue"] = nameof(SplCharacteristicRenderingServiceTests),
            ["ClaudeApiService.GenerateCleanMarkdownAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.GetSkillsDocumentAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.GetSystemContextAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.InterpretRequestAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.RetryInterpretationAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.SelectSkillsViaAiAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeApiService.SynthesizeResultsAsync"] = nameof(ClaudeApiServicePublicSurfaceTests),
            ["ClaudeConversationService.CreateConversationAsync"] = nameof(ClaudeConversationServiceTests),
            ["ClaudeConversationService.DeleteConversationAsync"] = nameof(ClaudeConversationServiceTests),
            ["ClaudeConversationService.GetConversationAsync"] = nameof(ClaudeConversationServiceTests),
            ["ClaudeConversationService.GetConversationHistoryAsync"] = nameof(ClaudeConversationServiceTests),
            ["ClaudeConversationService.GetConversationStatsAsync"] = nameof(ClaudeConversationServiceTests),
            ["ClaudeSearchService.ExtractProductFromDescriptionAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ClaudeSearchService.GetAllClassSummariesAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ClaudeSearchService.GetIndicationReferenceDataAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ClaudeSearchService.MatchUserQueryToClassesAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ClaudeSearchService.MatchUserQueryToIndicationsAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ClaudeSearchService.SearchByUserQueryAsync"] = nameof(ClaudeSearchServicePublicSurfaceTests),
            ["ConnectionString.Get"] = nameof(MiscHelperCoverageTests),
            ["DatabaseIntensiveAttribute.CreateInstance"] = nameof(AzureThrottleFilterTests),
            ["DatabaseLimitAttribute.CreateInstance"] = nameof(AzureThrottleFilterTests),
            ["DatabaseLimitFilter.OnActionExecutionAsync"] = nameof(AzureThrottleFilterTests),
            ["DatabaseUsageMonitorExtensions.AddDatabaseUsageMonitoring"] = nameof(ServiceRegistrationTests),
            ["DemoModeService.Dispose"] = nameof(DemoModeServiceTests),
            ["DemoModeService.StartAsync"] = nameof(DemoModeServiceTests),
            ["DemoModeService.StopAsync"] = nameof(DemoModeServiceTests),
            ["DocumentDataService.GetDocumentAsync"] = nameof(SplExportServiceTests),
            ["DosingSpecification.Validate"] = nameof(ModelPublicSurfaceTests),
            ["IncludeLabelNestedTypesDocumentFilter.Apply"] = nameof(OpenApiDocumentFilterTests),
            ["ProductEvent.Validate"] = nameof(ModelPublicSurfaceTests),
            ["Repository.CreateAsync"] = nameof(RepositoryDataAccessTests),
            ["Repository.DeleteAsync"] = nameof(RepositoryDataAccessTests),
            ["Repository.GetCompleteLabelsAsync"] = nameof(RepositoryDataAccessTests),
            ["Repository.ReadAllAsync"] = nameof(RepositoryDataAccessTests),
            ["Repository.ReadByIdAsync"] = nameof(RepositoryDataAccessTests),
            ["Repository.UpdateAsync"] = nameof(RepositoryDataAccessTests),
            ["SplDataService.ArchiveSplDataAsync"] = nameof(SplDataServiceTests),
            ["SplDataService.CreateSplDataAsync"] = nameof(SplDataServiceTests),
            ["SplDataService.GetSplDataListAsync"] = nameof(SplDataServiceTests),
            ["SplExportService.ExportDocumentToSplAsync"] = nameof(SplExportServiceTests),
            ["StructuredBodyViewModelFactory.Create"] = nameof(MiscHelperCoverageTests),
            ["TemplateRenderingService.Dispose"] = nameof(SplExportServiceTests),
            ["TemplateRenderingService.RenderAsync"] = nameof(SplExportServiceTests),
            ["ThrottleCheckFilter.OnActionExecuted"] = nameof(AzureThrottleFilterTests),
            ["ThrottleCheckFilter.OnActionExecuting"] = nameof(AzureThrottleFilterTests),
            ["UserRoleAuthorizationFilter.OnAuthorizationAsync"] = nameof(AuthorizationFilterTests),
            ["Util.GetLoginName"] = nameof(UtilTests),
            ["ViewRenderService.RenderToStringAsync"] = nameof(ViewRenderServiceTests),
        };

        /**************************************************************/
        /// <summary>
        /// Acknowledged coverage gaps: public method keys that currently have NO
        /// covering test, each with a one-line reason. Empty by design — the
        /// pending 52-method test harness plan (plus the inventory-triage extras)
        /// has been fully implemented. Any future entry here is tracked work,
        /// not accepted debt.
        /// </summary>
        /// <remarks>
        /// New public methods must ship with a covering test (add the key to
        /// <see cref="CoverageMap"/>). Add a KnownGapMap entry only for a
        /// genuinely deferred gap with a one-line reason, and remove it as soon
        /// as the covering test lands.
        /// </remarks>
        /// <seealso cref="CoverageMap"/>
        private static readonly Dictionary<string, string> KnownGapMap = new(StringComparer.Ordinal)
        {
        };

        #endregion

        /**************************************************************/
        /// <summary>
        /// Asserts that the runtime public method inventory of the MedRecPro assembly
        /// exactly matches the union of <see cref="CoverageMap"/> and
        /// <see cref="KnownGapMap"/>: no unmapped methods, no stale map entries, no
        /// key present in both maps, and every CoverageMap value names a class that
        /// exists in this test assembly.
        /// </summary>
        /// <remarks>
        /// The runtime inventory is authoritative. On failure, fix the map entries
        /// listed in the assertion message; never widen the reflection exclusion
        /// filters beyond the documented specification.
        /// </remarks>
        /// <example>
        /// <code>
        /// dotnet test MedRecProTest\MedRecProTest.csproj --no-restore -p:UseAppHost=false `
        ///     --filter "FullyQualifiedName~MedRecProPublicSurfaceInventoryTests"
        /// </code>
        /// </example>
        /// <seealso cref="CoverageMap"/>
        /// <seealso cref="KnownGapMap"/>
        [TestMethod]
        public void PublicSurface_AllPublicMethods_AreMappedToCoverageOrKnownGap()
        {
            #region implementation
            // authoritative runtime inventory of the MedRecPro assembly
            var actual = getPublicMethodKeys();

            // union of both maps is the expected universe
            var mapped = CoverageMap.Keys.Concat(KnownGapMap.Keys).ToHashSet(StringComparer.Ordinal);

            // methods that exist but are not accounted for in either map
            var unmapped = actual.Except(mapped).OrderBy(x => x, StringComparer.Ordinal).ToList();

            // map entries whose method no longer exists (renamed, removed, de-publicized)
            var stale = mapped.Except(actual).OrderBy(x => x, StringComparer.Ordinal).ToList();

            // a key must live in exactly one map
            var overlap = CoverageMap.Keys.Intersect(KnownGapMap.Keys, StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            // every CoverageMap value must be a real class in this test assembly
            var testTypes = typeof(MedRecProPublicSurfaceInventoryTests).Assembly.GetTypes()
                .Where(t => t.IsClass)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missing = CoverageMap.Values
                .Where(v => !testTypes.Contains(v))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.AreEqual(0, unmapped.Count,
                "Unmapped public methods - add CoverageMap or KnownGapMap entries for: " + string.Join(", ", unmapped));
            Assert.AreEqual(0, stale.Count,
                "Stale map entries - remove keys that no longer exist: " + string.Join(", ", stale));
            Assert.AreEqual(0, overlap.Count,
                "Keys present in BOTH maps - keep each key in exactly one: " + string.Join(", ", overlap));
            Assert.AreEqual(0, missing.Count,
                "CoverageMap values that are not classes in this test assembly: " + string.Join(", ", missing));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the set of eligible public method keys for the MedRecPro assembly
        /// using the documented reflection exclusion filters.
        /// </summary>
        /// <returns>
        /// Ordinal-compared set of "TypeSimpleName.MethodName" keys (overloads
        /// collapsed, generic arity stripped).
        /// </returns>
        /// <remarks>
        /// Filters (in order): public or nested-public classes only; no delegates;
        /// no compiler-generated types; no null-namespace types; no Migrations
        /// namespaces; no Controllers namespace segments; no ControllerBase-derived
        /// types. Methods: public declared-only instance/static; no IsSpecialName
        /// accessors/operators; no compiler-generated methods; no object overrides.
        /// </remarks>
        /// <seealso cref="keyFor"/>
        private static HashSet<string> getPublicMethodKeys()
        {
            #region implementation
            return typeof(MedRecPro.Helpers.TextUtil).Assembly.GetTypes()
                .Where(t => t.IsClass && (t.IsPublic || t.IsNestedPublic))
                .Where(t => !typeof(MulticastDelegate).IsAssignableFrom(t))
                .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .Where(t => !string.IsNullOrEmpty(t.Namespace))
                .Where(t => !t.Namespace!.Contains("Migrations"))
                .Where(t => !t.Namespace!.Split('.').Contains("Controllers"))
                .Where(t => !typeof(ControllerBase).IsAssignableFrom(t))
                .SelectMany(t => t
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName)
                    .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                    .Where(m => m.GetBaseDefinition().DeclaringType != typeof(object))
                    .Select(m => keyFor(t, m)))
                .ToHashSet(StringComparer.Ordinal);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Produces the Option A map key for a method: the declaring type's simple
        /// name with generic arity stripped, a dot, and the method name.
        /// </summary>
        /// <param name="type">Declaring type (nested types use their leaf name).</param>
        /// <param name="method">The public method.</param>
        /// <returns>Key such as "Repository.GetCompleteLabelsAsync".</returns>
        /// <seealso cref="getPublicMethodKeys"/>
        private static string keyFor(Type type, MethodInfo method)
        {
            #region implementation
            // Repository`1 -> Repository (generic arity stripped from the type name)
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            return $"{name}.{method.Name}";
            #endregion
        }
    }
}
