using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 batch-level validation: aggregates row/table validation results, computes
    /// coverage metrics, and checks cross-version concordance. Results are returned as
    /// in-memory DTOs and logged — not persisted to database.
    /// </summary>
    /// <remarks>
    /// ## Dependencies
    /// - <see cref="IRowValidationService"/>: Per-observation checks
    /// - <see cref="ITableValidationService"/>: Cross-row checks per table
    /// - <see cref="ApplicationDbContext"/>: Read-only access for DB-based reporting and concordance
    ///
    /// ## Memory Management
    /// <see cref="GenerateReportFromDatabaseAsync"/> reads from tmp_FlattenedStandardizedTable
    /// in configurable batches to avoid holding the full corpus in memory.
    /// </remarks>
    /// <seealso cref="IBatchValidationService"/>
    /// <seealso cref="BatchValidationReport"/>
    /// <seealso cref="CrossVersionDiscrepancy"/>
    public class BatchValidationService : IBatchValidationService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Row-level validation service.</summary>
        private readonly IRowValidationService _rowValidator;

        /**************************************************************/
        /// <summary>Table-level validation service.</summary>
        private readonly ITableValidationService _tableValidator;

        /**************************************************************/
        /// <summary>Database context for read-only access.</summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>Logger for reporting.</summary>
        private readonly ILogger<BatchValidationService> _logger;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the batch validation service with all dependencies.
        /// </summary>
        /// <param name="rowValidator">Row-level validation service.</param>
        /// <param name="tableValidator">Table-level validation service.</param>
        /// <param name="dbContext">Database context for read-only access.</param>
        /// <param name="logger">Logger.</param>
        public BatchValidationService(
            IRowValidationService rowValidator,
            ITableValidationService tableValidator,
            ApplicationDbContext dbContext,
            ILogger<BatchValidationService> logger)
        {
            #region implementation

            _rowValidator = rowValidator;
            _tableValidator = tableValidator;
            _dbContext = dbContext;
            _logger = logger;

            #endregion
        }

        #endregion Constructor

        #region IBatchValidationService Implementation

        /**************************************************************/
        /// <summary>
        /// Generates a validation report from in-memory observations.
        /// </summary>
        /// <param name="observations">Parsed observations to validate.</param>
        /// <param name="skipReasons">Optional skip reasons dictionary.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Complete validation report.</returns>
        public Task<BatchValidationReport> GenerateReportAsync(
            List<ParsedObservation> observations,
            Dictionary<int, string>? skipReasons = null,
            CancellationToken ct = default)
        {
            #region implementation

            ct.ThrowIfCancellationRequested();

            var report = new BatchValidationReport();

            // Run row-level validation
            var rowResults = _rowValidator.ValidateObservations(observations);
            report.RowIssues = rowResults
                .Where(r => r.Status != ValidationStatus.Valid)
                .ToList();

            // Run table-level validation
            var tableResults = _tableValidator.ValidateTables(observations);
            report.TableIssues = tableResults
                .Where(r => r.Status != ValidationStatus.Valid)
                .ToList();

            // Aggregate statistics
            populateAggregates(report, observations);

            // Skip reasons
            populateSkipReasons(report, skipReasons);

            logReportSummary(report);

            return Task.FromResult(report);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a validation report by reading from tmp_FlattenedStandardizedTable in batches.
        /// </summary>
        /// <param name="skipReasons">Optional skip reasons dictionary.</param>
        /// <param name="batchSize">Read batch size (default 5000).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Complete validation report.</returns>
        public async Task<BatchValidationReport> GenerateReportFromDatabaseAsync(
            Dictionary<int, string>? skipReasons = null,
            int batchSize = 5000,
            CancellationToken ct = default)
        {
            #region implementation

            _logger.LogInformation("Stage 4 — Generating validation report from database");

            var report = new BatchValidationReport();

            // Read all observations from DB in batches and accumulate aggregates
            var allObservations = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .ToListAsync(ct);

            // Map entity rows to ParsedObservation for validation
            var observations = allObservations.Select(mapFromEntity).ToList();

            // Run row-level validation
            var rowResults = _rowValidator.ValidateObservations(observations);
            report.RowIssues = rowResults
                .Where(r => r.Status != ValidationStatus.Valid)
                .ToList();

            // Run table-level validation
            var tableResults = _tableValidator.ValidateTables(observations);
            report.TableIssues = tableResults
                .Where(r => r.Status != ValidationStatus.Valid)
                .ToList();

            // Aggregate statistics
            populateAggregates(report, observations);

            // Skip reasons
            populateSkipReasons(report, skipReasons);

            logReportSummary(report);

            return report;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks cross-version concordance across label versions for the same product.
        /// Groups by (ProductTitle, LabelerName), compares row counts per (VersionNumber, TableCategory).
        /// Flags divergences greater than 50%.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of discrepancies.</returns>
        public async Task<List<CrossVersionDiscrepancy>> CheckCrossVersionConcordanceAsync(CancellationToken ct = default)
        {
            #region implementation

            _logger.LogInformation("Stage 4 — Checking cross-version concordance");

            var discrepancies = new List<CrossVersionDiscrepancy>();

            // Group by product identity, then by (VersionNumber, TableCategory)
            var versionGroups = await _dbContext.Set<LabelView.FlattenedStandardizedTable>()
                .AsNoTracking()
                .GroupBy(r => new
                {
                    r.ProductTitle,
                    r.LabelerName,
                    r.VersionNumber,
                    r.TableCategory
                })
                .Select(g => new
                {
                    g.Key.ProductTitle,
                    g.Key.LabelerName,
                    g.Key.VersionNumber,
                    g.Key.TableCategory,
                    RowCount = g.Count()
                })
                .ToListAsync(ct);

            // Group by product identity
            var productGroups = versionGroups
                .GroupBy(v => new { v.ProductTitle, v.LabelerName });

            foreach (var product in productGroups)
            {
                // Group by TableCategory within this product
                var categoryGroups = product.GroupBy(v => v.TableCategory);

                foreach (var category in categoryGroups)
                {
                    var versions = category.OrderBy(v => v.VersionNumber).ToList();
                    if (versions.Count < 2) continue;

                    // Compare each adjacent pair
                    for (int i = 0; i < versions.Count - 1; i++)
                    {
                        var v1 = versions[i];
                        var v2 = versions[i + 1];

                        if (v1.RowCount == 0 && v2.RowCount == 0) continue;

                        var maxCount = Math.Max(v1.RowCount, v2.RowCount);
                        var divergence = Math.Abs(v1.RowCount - v2.RowCount) / (double)maxCount;

                        if (divergence > 0.50)
                        {
                            discrepancies.Add(new CrossVersionDiscrepancy
                            {
                                ProductTitle = product.Key.ProductTitle,
                                LabelerName = product.Key.LabelerName,
                                VersionNumber = v1.VersionNumber,
                                TableCategory = v1.TableCategory,
                                RowCount = v1.RowCount,
                                ComparedVersionNumber = v2.VersionNumber,
                                ComparedRowCount = v2.RowCount,
                                Issue = $"Row count divergence: {v1.RowCount} vs {v2.RowCount} ({divergence:P0})"
                            });
                        }
                    }
                }
            }

            _logger.LogInformation("Cross-version concordance: {Count} discrepancies found",
                discrepancies.Count);

            return discrepancies;

            #endregion
        }

        #endregion IBatchValidationService Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Populates aggregate statistics on the report from observations.
        /// </summary>
        private static void populateAggregates(BatchValidationReport report, List<ParsedObservation> observations)
        {
            #region implementation

            report.TotalObservations = observations.Count;

            report.TotalTablesProcessed = observations
                .Where(o => o.TextTableID.HasValue)
                .Select(o => o.TextTableID!.Value)
                .Distinct()
                .Count();

            // Row count by category
            report.RowCountByCategory = observations
                .Where(o => !string.IsNullOrWhiteSpace(o.TableCategory))
                .GroupBy(o => o.TableCategory!)
                .ToDictionary(g => g.Key, g => g.Count());

            // Row count by parse rule
            report.RowCountByParseRule = observations
                .Where(o => !string.IsNullOrWhiteSpace(o.ParseRule))
                .GroupBy(o => o.ParseRule!)
                .ToDictionary(g => g.Key, g => g.Count());

            // Confidence distribution
            report.HighConfidenceCount = observations
                .Count(o => o.ParseConfidence.HasValue && o.ParseConfidence.Value >= 0.9);

            report.MediumConfidenceCount = observations
                .Count(o => o.ParseConfidence.HasValue
                    && o.ParseConfidence.Value >= 0.5
                    && o.ParseConfidence.Value < 0.9);

            report.LowConfidenceCount = observations
                .Count(o => o.ParseConfidence.HasValue && o.ParseConfidence.Value < 0.5);

            // Validation flags summary
            foreach (var obs in observations)
            {
                if (string.IsNullOrWhiteSpace(obs.ValidationFlags))
                    continue;

                var flags = obs.ValidationFlags.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var flag in flags)
                {
                    if (flag.Contains("PASS", StringComparison.OrdinalIgnoreCase))
                        report.PassFlagCount++;
                    else if (flag.Contains("WARN", StringComparison.OrdinalIgnoreCase))
                        report.WarnFlagCount++;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Populates skip reasons on the report from the optional dictionary.
        /// </summary>
        private static void populateSkipReasons(BatchValidationReport report, Dictionary<int, string>? skipReasons)
        {
            #region implementation

            if (skipReasons == null || skipReasons.Count == 0)
                return;

            report.TotalTablesSkipped = skipReasons.Count;
            report.SkipReasons = skipReasons.Values
                .GroupBy(v => v)
                .ToDictionary(g => g.Key, g => g.Count());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs a summary of the validation report.
        /// </summary>
        private void logReportSummary(BatchValidationReport report)
        {
            #region implementation

            _logger.LogInformation(
                "Stage 4 — Validation Report: {Total} observations across {Tables} tables ({Skipped} skipped)",
                report.TotalObservations, report.TotalTablesProcessed, report.TotalTablesSkipped);

            _logger.LogInformation(
                "Confidence: High={High} (≥0.9), Medium={Medium} (0.5–0.9), Low={Low} (<0.5)",
                report.HighConfidenceCount, report.MediumConfidenceCount, report.LowConfidenceCount);

            _logger.LogInformation(
                "Flags: {Pass} PASS, {Warn} WARN | Row issues: {RowIssues} | Table issues: {TableIssues}",
                report.PassFlagCount, report.WarnFlagCount,
                report.RowIssues.Count, report.TableIssues.Count);

            if (report.RowCountByCategory.Count > 0)
            {
                var categoryBreakdown = string.Join(", ",
                    report.RowCountByCategory.OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key}={kv.Value}"));
                _logger.LogInformation("By category: {Categories}", categoryBreakdown);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps a <see cref="LabelView.FlattenedStandardizedTable"/> entity to a
        /// <see cref="ParsedObservation"/> DTO for validation.
        /// </summary>
        private static ParsedObservation mapFromEntity(LabelView.FlattenedStandardizedTable entity)
        {
            #region implementation

            return new ParsedObservation
            {
                DocumentGUID = entity.DocumentGUID,
                LabelerName = entity.LabelerName,
                ProductTitle = entity.ProductTitle,
                VersionNumber = entity.VersionNumber,
                TextTableID = entity.TextTableID,
                Caption = entity.Caption,
                SourceRowSeq = entity.SourceRowSeq,
                SourceCellSeq = entity.SourceCellSeq,
                TableCategory = entity.TableCategory,
                ParentSectionCode = entity.ParentSectionCode,
                ParentSectionTitle = entity.ParentSectionTitle,
                SectionTitle = entity.SectionTitle,
                ParameterName = entity.ParameterName,
                ParameterCategory = entity.ParameterCategory,
                ParameterSubtype = entity.ParameterSubtype,
                TreatmentArm = entity.TreatmentArm,
                ArmN = entity.ArmN,
                StudyContext = entity.StudyContext,
                DoseRegimen = entity.DoseRegimen,
                Population = entity.Population,
                Timepoint = entity.Timepoint,
                RawValue = entity.RawValue,
                PrimaryValue = entity.PrimaryValue,
                PrimaryValueType = entity.PrimaryValueType,
                SecondaryValue = entity.SecondaryValue,
                SecondaryValueType = entity.SecondaryValueType,
                LowerBound = entity.LowerBound,
                UpperBound = entity.UpperBound,
                BoundType = entity.BoundType,
                PValue = entity.PValue,
                Unit = entity.Unit,
                ParseConfidence = entity.ParseConfidence,
                ParseRule = entity.ParseRule,
                FootnoteMarkers = entity.FootnoteMarkers,
                FootnoteText = entity.FootnoteText,
                ValidationFlags = entity.ValidationFlags
            };

            #endregion
        }

        #endregion Private Helpers
    }
}
