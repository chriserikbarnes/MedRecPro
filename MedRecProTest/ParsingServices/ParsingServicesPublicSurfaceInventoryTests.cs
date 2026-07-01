using MedRecProImportClass.Service.ParsingServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Guards the public parser-service method inventory against silent coverage drift.
    /// </summary>
    /// <remarks>
    /// The test enumerates public methods declared by parser-service types and requires each
    /// method to be mapped to an explicit test class. It is intentionally small and readable so
    /// newly added parser public methods force an obvious coverage-map update.
    /// </remarks>
    /// <seealso cref="DocumentSectionParser"/>
    /// <seealso cref="SplParseContextExtensions"/>
    [TestClass]
    public class ParsingServicesPublicSurfaceInventoryTests
    {
        #region implementation
        private static readonly Dictionary<string, string> CoverageMap = new(StringComparer.Ordinal)
        {
            ["AttachedDocumentParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["AuthorSectionParser.GetOrCreateOrganizationByIdentifierAsync"] = nameof(DocumentAndAuthorParserTests),
            ["AuthorSectionParser.GetOrCreateOrganizationByNameAsync"] = nameof(DocumentAndAuthorParserTests),
            ["AuthorSectionParser.ParseAsync"] = nameof(DocumentAndAuthorParserTests),
            ["BusinessOperationParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["CertificationProductLinkParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["ComplianceActionParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["DisciplinaryActionParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["DocumentRelationshipParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["DocumentSectionParser.ParseAsync"] = nameof(DocumentAndAuthorParserTests),
            ["DosingSpecificationValidationService.ValidateDoseQuantity"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateDosingSpecification"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateRouteCode"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationValidationService.ValidateUcumUnit"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationParser.BuildDosingSpecificationAsync"] = nameof(DosingSpecificationParserTests),
            ["DosingSpecificationParser.CreateDefault"] = nameof(DosingSpecificationParserTests),
            ["IngredientParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["LicenseParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["LotDistributionParser.CreateLotHierarchiesAsync"] = nameof(PackagingAndLotParserTests),
            ["LotDistributionParser.ParseAsync"] = nameof(PackagingAndLotParserTests),
            ["ManufacturedProductParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["MarketingStatusParser.ParseAsync"] = nameof(PackagingAndLotParserTests),
            ["OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"] = "OrangeBookExclusivityParsingServiceTests",
            ["OrangeBookPatentParsingService.ProcessPatentsFileAsync"] = "OrangeBookPatentParsingServiceTests",
            ["OrangeBookPatentUseCodeParsingService.ProcessPatentUseCodesAsync"] = "OrangeBookPatentUseCodeParsingServiceTests",
            ["OrangeBookProductParsingService.ProcessProductsFileAsync"] = "OrangeBookProductParsingServiceTests",
            ["PackagingParser.ParseAsync"] = nameof(PackagingAndLotParserTests),
            ["ProductCharacteristicsParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["ProductEventParser.BuildProductEventAsync"] = nameof(ProductEventParserTests),
            ["ProductEventParser.BulkCreateProductEventsAsync"] = nameof(ProductEventParserTests),
            ["ProductEventParser.CreateDefault"] = nameof(ProductEventParserTests),
            ["ProductEventParser.CreateDistributedEvent"] = nameof(ProductEventParserTests),
            ["ProductEventParser.CreateReturnedEvent"] = nameof(ProductEventParserTests),
            ["ProductEventParser.PopulateProductEvent"] = nameof(ProductEventParserTests),
            ["ProductEventValidationService.ValidateEffectiveTime"] = nameof(ProductEventParserTests),
            ["ProductEventValidationService.ValidateEventCode"] = nameof(ProductEventParserTests),
            ["ProductEventValidationService.ValidateProductEvent"] = nameof(ProductEventParserTests),
            ["ProductEventValidationService.ValidateQuantity"] = nameof(ProductEventParserTests),
            ["ProductExtensionParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["ProductIdentityParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["ProductMarketingParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["ProductRelationshipParser.ParseAsync"] = nameof(ProductDataParserTests),
            ["REMSParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["SectionContentParser.ParseAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser.ParseSectionContentAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_BulkCalls.GetOrCreateSectionTextContentsAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_BulkCalls.GetOrCreateTextListAndItemsAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_BulkCalls.GetOrCreateTextTableAndChildrenAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_BulkCalls.ParseAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_BulkCalls.ParseSectionContentAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_SingleCalls.GetOrCreateSectionTextContentsAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_SingleCalls.GetOrCreateTextListAndItemsAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_SingleCalls.GetOrCreateTextTableAndChildrenAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_SingleCalls.ParseAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_SingleCalls.ParseSectionContentAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_StagedBulkCalls.ParseAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_StagedBulkCalls.ParseSectionContentAsync"] = nameof(SectionContentParserTests),
            ["SectionContentParser_StagedBulkCalls.StageSectionTextContentsAsync"] = nameof(SectionContentParserTests),
            ["SectionHierarchyParser.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SectionIndexingParser.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SectionMediaParser.ParseAsync"] = nameof(SectionContentParserTests),
            ["SectionMediaParser.ParseObservationMediaAsync"] = nameof(SectionContentParserTests),
            ["SectionMediaParser.ParseRenderedMediaAsync"] = nameof(SectionContentParserTests),
            ["SectionParser.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SectionParser_BulkCalls.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SectionParser_SingleCalls.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SectionParser_StagedBulk.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["SpecializedKindValidatorService.ValidateCosmeticCategoryRules"] = nameof(ProductDataParserTests),
            ["SplParseContext.GetRepository"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContext.SetBatchSavingFlag"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContext.SetBulkOperationsFlag"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContext.SetBulkStagingFlag"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContext.UpdateFileResult"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContextExtensions.CommitDeferredChangesAsync"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContextExtensions.GetDbContext"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContextExtensions.GetTrackedEntityId"] = nameof(SplParseContextExtensionsTests),
            ["SplParseContextExtensions.SaveChangesIfAllowedAsync"] = nameof(SplParseContextExtensionsTests),
            ["SplParseResult.MergeFrom"] = nameof(SplParseContextExtensionsTests),
            ["StructuredBodySectionParser.ParseAsync"] = nameof(SectionOrchestrationParserTests),
            ["ToleranceSpecificationParser.ParseAsync"] = nameof(SpecialtyParserTests),
            ["ValidationResult.AddError"] = nameof(DosingSpecificationParserTests),
            ["ValidationResult.MergeWith"] = nameof(DosingSpecificationParserTests),
            ["WarningLetterParser.ParseAsync"] = nameof(SpecialtyParserTests)
        };
        #endregion

        /**************************************************************/
        /// <summary>
        /// Verifies every public parser-service method is represented in the coverage map.
        /// </summary>
        /// <seealso cref="CoverageMap"/>
        [TestMethod]
        public void PublicParserSurface_AllPublicMethods_AreMappedToCoverage()
        {
            #region implementation
            var actualMethods = getPublicParserMethods();
            var unmappedMethods = actualMethods.Except(CoverageMap.Keys).OrderBy(x => x).ToList();
            var staleMappings = CoverageMap.Keys.Except(actualMethods).OrderBy(x => x).ToList();
            var availableTestTypes = typeof(ParsingServicesPublicSurfaceInventoryTests).Assembly
                .GetTypes()
                .Where(type => type.IsClass)
                .Select(type => type.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missingTestClasses = CoverageMap.Values
                .Where(testClass => !availableTestTypes.Contains(testClass))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x)
                .ToList();

            Assert.AreEqual(0, unmappedMethods.Count,
                "Add coverage-map entries for public parser methods: " + string.Join(", ", unmappedMethods));
            Assert.AreEqual(0, staleMappings.Count,
                "Remove stale coverage-map entries: " + string.Join(", ", staleMappings));
            Assert.AreEqual(0, missingTestClasses.Count,
                "Coverage map points at missing test classes: " + string.Join(", ", missingTestClasses));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enumerates public methods declared by parser-service implementation classes.
        /// </summary>
        /// <returns>Public parser method keys in Type.Method format.</returns>
        /// <seealso cref="MethodInfo"/>
        private static HashSet<string> getPublicParserMethods()
        {
            #region implementation
            return typeof(DocumentSectionParser).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "MedRecProImportClass.Service.ParsingServices")
                .Where(type => type.IsClass && (type.IsPublic || type.IsNestedPublic))
                .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .SelectMany(type => type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsConstructor)
                    .Where(method => !method.IsSpecialName)
                    .Where(method => !method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                    .Where(method => method.GetBaseDefinition().DeclaringType != typeof(object))
                    .Select(method => $"{type.Name}.{method.Name}"))
                .ToHashSet(StringComparer.Ordinal);
            #endregion
        }
    }
}
