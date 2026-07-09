using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MedRecPro.Features.AeDashboard.Mapping
{
    /**************************************************************/
    /// <summary>
    /// Maps AE dashboard persistence rows into API-safe dashboard DTOs.
    /// </summary>
    /// <remarks>
    /// This mapper keeps the hot-path manual projection code beside the AE dashboard
    /// feature while preserving the existing encrypted-ID behavior and response
    /// shapes exposed by <see cref="DtoLabelAccess"/>.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="AeRiskSignalDto"/>
    internal static class AeDashboardDtoMapper
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Builds product summary DTOs from materialized catalog rows.
        /// </summary>
        /// <param name="entities">Materialized product catalog rows.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>API-safe product summary DTOs.</returns>
        /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        internal static List<AeDrugSummaryDto> ToProductCatalogDtos(
            IEnumerable<LabelView.AeDashboardProductCatalog> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities
                .Select(entity => ToProductCatalogDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one product summary DTO from a materialized catalog row.
        /// </summary>
        /// <param name="entity">Materialized product catalog row.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>An API-safe product summary DTO.</returns>
        /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        internal static AeDrugSummaryDto ToProductCatalogDto(
            LabelView.AeDashboardProductCatalog entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var dto = new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(entity.IngredientSubstanceID, pkSecret, logger, nameof(entity.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(entity.PharmacologicClassID, pkSecret, logger, nameof(entity.PharmacologicClassID)),
                DocumentGUID = entity.DocumentGUID,
                ProductName = entity.ProductName,
                SubstanceName = entity.PrimarySubstanceName,
                UNII = entity.PrimaryUNII,
                PharmClassCode = entity.PrimaryPharmClassCode,
                PharmClassName = entity.PrimaryPharmClassName,
                ActiveIngredients = parseCatalogActiveIngredients(entity.ActiveIngredientsJson, logger),
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                RowCount = entity.RowCount,
                SignificantCount = entity.SignificantCount,
                SignificantProtectiveCount = entity.SignificantProtectiveCount,
                SignificantElevatedCount = entity.SignificantElevatedCount,
                PlaceboCoverage = entity.PlaceboCoverage,
                ActiveCoverage = entity.ActiveCoverage,
                DoseCoverage = entity.DoseCoverage,
                SocBreadth = entity.SocBreadth,
                SocTotal = entity.SocTotal > 0 ? entity.SocTotal : AeDashboardMetadata.SocTotal,
                MonoComboMix = parseMonoComboMix(entity.MonoComboMix),
                Score = entity.Score,
                ScoreReason = entity.ScoreReason
            };

            return dto.Score.HasValue && !string.IsNullOrWhiteSpace(dto.ScoreReason)
                ? dto
                : AeDashboardDerivation.DeriveProduct(dto);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE product summary DTOs from EF summary-view rows.
        /// </summary>
        /// <param name="entities">AE product summary view rows.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>API-safe product summary DTOs.</returns>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        internal static List<AeDrugSummaryDto> ToDrugSummaryDtos(
            IEnumerable<LabelView.AeDrugSummary> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return entities
                .Select(entity => ToDrugSummaryDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE product summary DTO from an EF summary-view row.
        /// </summary>
        /// <param name="entity">AE product summary view row.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>An API-safe product summary DTO.</returns>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        internal static AeDrugSummaryDto ToDrugSummaryDto(
            LabelView.AeDrugSummary entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = encryptNullableInt(entity.ActiveMoietyID, pkSecret, logger, nameof(entity.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(entity.IngredientSubstanceID, pkSecret, logger, nameof(entity.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(entity.PharmacologicClassID, pkSecret, logger, nameof(entity.PharmacologicClassID)),
                DocumentGUID = entity.DocumentGUID,
                ProductName = entity.ProductName,
                SubstanceName = entity.SubstanceName,
                UNII = entity.UNII,
                PharmClassCode = entity.PharmClassCode,
                PharmClassName = entity.PharmClassName,
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                RowCount = entity.RowCount,
                SignificantCount = entity.SignificantCount,
                SignificantProtectiveCount = entity.SignificantProtectiveCount,
                SignificantElevatedCount = entity.SignificantElevatedCount,
                PlaceboCoverage = entity.PlaceboCoverage,
                ActiveCoverage = entity.ActiveCoverage,
                DoseCoverage = (double)entity.DoseCoverage,
                SocBreadth = entity.SocBreadth,
                SocTotal = entity.SocTotal > 0 ? entity.SocTotal : AeDashboardMetadata.SocTotal,
                MonoComboMix = parseMonoComboMix(entity.MonoComboMix)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds fallback AE product summary DTOs from risk-table rows.
        /// </summary>
        /// <param name="entities">Materialized risk-table rows to aggregate at the summary grain.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>Product summary DTOs matching the summary-view grain.</returns>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        internal static List<AeDrugSummaryDto> ToFallbackDrugSummaryDtos(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Match vw_AeDrugSummary grouping so fallback rows have the same
            // product/substance/class grain as refreshed summary-view rows.
            return entities
                .GroupBy(entity => (
                    DocumentGUID: entity.DocumentGUID,
                    ProductName: entity.ProductName,
                    SubstanceName: entity.SubstanceName,
                    UNII: entity.UNII,
                    PharmClassCode: entity.PharmClassCode,
                    PharmClassName: entity.PharmClassName,
                    ActiveMoietyID: entity.ActiveMoietyID,
                    IngredientSubstanceID: entity.IngredientSubstanceID,
                    PharmacologicClassID: entity.PharmacologicClassID))
                .Select(group => toFallbackDrugSummaryDto(group, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapped AE risk signal DTOs from risk-table rows.
        /// </summary>
        /// <param name="entities">Materialized risk-table rows for one or more documents.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>API-safe risk signal DTOs collapsed to visible clinical strata.</returns>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        /// <seealso cref="AeRiskSignalDto"/>
        internal static List<AeRiskSignalDto> ToRiskSignalDtos(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Collapse duplicate risk rows to one signal per viewer-visible clinical stratum
            // before mapping. This removes both the pharmacologic-class fan-out from
            // class-enriched vw_AeRisk rows and the multi-arm duplication where the same
            // term/dose/comparator is reported for both pooled and unlabeled subgroup arms.
            return collapseToMostPoweredStratum(entities)
                .Select(entity => ToRiskSignalDto(entity, pkSecret, logger))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one mapped AE risk signal DTO from a risk-table row.
        /// </summary>
        /// <param name="entity">Materialized risk-table row.</param>
        /// <param name="pkSecret">Secret used for integer ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>An API-safe risk signal DTO.</returns>
        /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
        /// <seealso cref="AeRiskSignalDto"/>
        internal static AeRiskSignalDto ToRiskSignalDto(
            LabelView.FlattenedAdverseEventRiskTable entity,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            return new AeRiskSignalDto
            {
                EncryptedFlattenedAdverseEventRiskTableID = encryptNullableInt(entity.Id, pkSecret, logger, nameof(entity.Id)),
                EncryptedFlattenedAdverseEventTableID = encryptNullableInt(entity.FlattenedAdverseEventTableId, pkSecret, logger, nameof(entity.FlattenedAdverseEventTableId)),
                EncryptedFlattenedStandardizedTableID = encryptNullableInt(entity.FlattenedStandardizedTableId, pkSecret, logger, nameof(entity.FlattenedStandardizedTableId)),
                ParameterName = entity.ParameterName,
                ParameterCategory = entity.ParameterCategory,
                Significance = entity.Significance,
                NumberNeededType = entity.NumberNeededType,
                UNII = entity.UNII,
                ProductName = entity.ProductName,
                DocumentGUID = entity.DocumentGUID,
                ArmN = entity.ArmN,
                ComparatorN = entity.ComparatorN,
                EventsTreatment = entity.EventsTreatment,
                EventsComparator = entity.EventsComparator,
                RR = entity.RR,
                RRLowerBound = entity.RRLowerBound,
                RRUpperBound = entity.RRUpperBound,
                LogRR = entity.LogRR,
                LogRRLowerBound = entity.LogRRLowerBound,
                LogRRUpperBound = entity.LogRRUpperBound,
                NumberNeeded = entity.NumberNeeded,
                NumberNeededLowerBound = entity.NumberNeededLowerBound,
                NumberNeededUpperBound = entity.NumberNeededUpperBound,
                IsPlaceboControlled = entity.IsPlaceboControlled,
                IsCombo = entity.IsCombo,
                CalculationFlags = entity.CalculationFlags,
                StudyContext = entity.StudyContext,
                Population = entity.Population,
                Subpopulation = entity.Subpopulation,
                Dose = entity.Dose,
                DoseUnit = entity.DoseUnit
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds one fallback AE product summary DTO from grouped risk-table rows.
        /// </summary>
        private static AeDrugSummaryDto toFallbackDrugSummaryDto(
            IGrouping<(Guid? DocumentGUID, string? ProductName, string? SubstanceName, string? UNII, string? PharmClassCode, string? PharmClassName, int? ActiveMoietyID, int? IngredientSubstanceID, int? PharmacologicClassID), LabelView.FlattenedAdverseEventRiskTable> group,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            var rows = group.ToList();
            var key = group.Key;
            var rowCount = rows.Count;

            return new AeDrugSummaryDto
            {
                EncryptedActiveMoietyID = encryptNullableInt(key.ActiveMoietyID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.ActiveMoietyID)),
                EncryptedIngredientSubstanceID = encryptNullableInt(key.IngredientSubstanceID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.IngredientSubstanceID)),
                EncryptedPharmacologicClassID = encryptNullableInt(key.PharmacologicClassID, pkSecret, logger, nameof(LabelView.FlattenedAdverseEventRiskTable.PharmacologicClassID)),
                DocumentGUID = key.DocumentGUID,
                ProductName = key.ProductName,
                SubstanceName = key.SubstanceName,
                UNII = key.UNII,
                PharmClassCode = key.PharmClassCode,
                PharmClassName = key.PharmClassName,
                ArmN = rows.Max(row => row.ArmN),
                ComparatorN = rows.Max(row => row.ComparatorN),
                RowCount = rowCount,
                SignificantCount = rows.Count(row => isSignificantAeSignal(row.Significance)),
                SignificantProtectiveCount = rows.Count(row => string.Equals(row.Significance, "protective", StringComparison.OrdinalIgnoreCase)),
                SignificantElevatedCount = rows.Count(row => string.Equals(row.Significance, "elevated", StringComparison.OrdinalIgnoreCase)),
                PlaceboCoverage = rows.Any(row => row.IsPlaceboControlled),
                ActiveCoverage = rows.Any(row => !row.IsPlaceboControlled),
                DoseCoverage = rowCount > 0
                    ? rows.Count(row => row.Dose.HasValue) / (double)rowCount
                    : 0.0,
                SocBreadth = rows
                    .Select(row => row.ParameterCategory)
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                SocTotal = AeDashboardMetadata.SocTotal,
                MonoComboMix = getMonoComboMix(rows)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses catalog ingredient JSON into dashboard ingredient DTOs.
        /// </summary>
        private static List<AeActiveIngredientDto>? parseCatalogActiveIngredients(
            string? activeIngredientsJson,
            ILogger logger)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(activeIngredientsJson))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<List<AeActiveIngredientDto>>(activeIngredientsJson);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Unable to parse AE dashboard product catalog active ingredients JSON.");
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a raw significance value contributes to summary counts.
        /// </summary>
        private static bool isSignificantAeSignal(string? significance)
        {
            #region implementation

            return string.Equals(significance, "elevated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(significance, "protective", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Derives the mono/combo mix from materialized risk-table rows.
        /// </summary>
        private static AeMonoComboMix getMonoComboMix(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            var hasCombo = entities.Any(entity => entity.IsCombo);
            var hasMono = entities.Any(entity => !entity.IsCombo);

            return hasCombo && hasMono
                ? AeMonoComboMix.Mixed
                : hasCombo
                    ? AeMonoComboMix.Combo
                    : AeMonoComboMix.Mono;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses duplicate AE risk rows to one representative per clinical stratum.
        /// </summary>
        private static List<LabelView.FlattenedAdverseEventRiskTable> collapseToMostPoweredStratum(
            IEnumerable<LabelView.FlattenedAdverseEventRiskTable> entities)
        {
            #region implementation

            return entities
                .GroupBy(entity => new
                {
                    entity.DocumentGUID,
                    entity.ParameterName,
                    entity.ParameterCategory,
                    entity.Dose,
                    entity.DoseUnit,
                    entity.IsPlaceboControlled,
                    entity.StudyContext,
                    entity.Population,
                    entity.Subpopulation
                })
                .Select(group => group
                    .OrderByDescending(entity => entity.ArmN ?? 0)
                    .ThenByDescending(entity => entity.ComparatorN ?? 0)
                    .ThenBy(entity => string.Equals(entity.Significance, "not significant", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(entity => (entity.RRUpperBound ?? double.MaxValue) - (entity.RRLowerBound ?? 0.0))
                    .ThenBy(entity => entity.FlattenedAdverseEventTableId)
                    .First())
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Encrypts a nullable integer identifier for client-safe DTO exposure.
        /// </summary>
        private static string? encryptNullableInt(
            int? value,
            string pkSecret,
            ILogger logger,
            string fieldName)
        {
            #region implementation

            return AeDashboardEncryptedIdMapper.Shared.EncryptNullableInt(value, pkSecret, logger, fieldName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the mono/combo text persisted by the product summary view.
        /// </summary>
        private static AeMonoComboMix? parseMonoComboMix(string? monoComboMix)
        {
            #region implementation

            return monoComboMix?.Trim().ToLowerInvariant() switch
            {
                "mono" => AeMonoComboMix.Mono,
                "combo" => AeMonoComboMix.Combo,
                "mixed" => AeMonoComboMix.Mixed,
                _ => null
            };

            #endregion
        }

        #endregion
    }
}
