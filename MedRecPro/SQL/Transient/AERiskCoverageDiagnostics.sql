/*******************************************************************************
 * AERiskCoverageDiagnostics.sql
 *
 * Database-read-only helper for auditing Stage 5 AE risk-table coverage.
 * Replace the #MissingTextTables VALUES list with the TextTableID set under
 * investigation, then run after Stage 5 has refreshed
 * tmp_FlattenedAdverseEventCoverageTable, tmp_FlattenedAdverseEventTable, and
 * tmp_FlattenedAdverseEventRiskTable.
 ******************************************************************************/
Use MedRecLocal
IF OBJECT_ID('tempdb..#MissingTextTables') IS NOT NULL
    DROP TABLE #MissingTextTables;

CREATE TABLE #MissingTextTables (
    TextTableID INT NOT NULL
);

-- Replace with the pasted TextTableID list.
INSERT INTO #MissingTextTables (TextTableID)
VALUES
    (20763),
    (44409);

;WITH
Std AS (
    SELECT DISTINCT TextTableID
    FROM dbo.tmp_FlattenedStandardizedTable
    WHERE TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
),
AeCoverage AS (
    SELECT DISTINCT TextTableID
    FROM dbo.tmp_FlattenedAdverseEventCoverageTable
    WHERE TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
),
AeStats AS (
    SELECT DISTINCT b.TextTableID
    FROM dbo.tmp_FlattenedAdverseEventTable AS a
    INNER JOIN dbo.tmp_FlattenedStandardizedTable AS b
        ON a.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
    WHERE b.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
),
AeRiskView AS (
    SELECT DISTINCT b.TextTableID
    FROM dbo.vw_AeRisk AS r
    INNER JOIN dbo.tmp_FlattenedStandardizedTable AS b
        ON r.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
    WHERE b.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
),
AeRiskTable AS (
    SELECT DISTINCT b.TextTableID
    FROM dbo.tmp_FlattenedAdverseEventRiskTable AS r
    INNER JOIN dbo.tmp_FlattenedStandardizedTable AS b
        ON r.tmp_FlattenedStandardizedTableID = b.tmp_FlattenedStandardizedTableID
    WHERE b.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
)
SELECT 'missing_input' AS Bucket, COUNT(*) AS TableCount FROM #MissingTextTables
UNION ALL SELECT 'in_standardized', COUNT(*) FROM Std
UNION ALL SELECT 'in_coverage_table', COUNT(*) FROM AeCoverage
UNION ALL SELECT 'in_ae_stats_table', COUNT(*) FROM AeStats
UNION ALL SELECT 'in_vw_AeRisk', COUNT(*) FROM AeRiskView
UNION ALL SELECT 'in_risk_table', COUNT(*) FROM AeRiskTable
UNION ALL SELECT 'no_stage5_source_row', COUNT(*) FROM #MissingTextTables m WHERE NOT EXISTS (SELECT 1 FROM Std s WHERE s.TextTableID = m.TextTableID)
UNION ALL SELECT 'source_coverage_no_ae_stats', COUNT(*) FROM AeCoverage c WHERE NOT EXISTS (SELECT 1 FROM AeStats s WHERE s.TextTableID = c.TextTableID)
UNION ALL SELECT 'missing_before_ae_stats', COUNT(*) FROM #MissingTextTables m WHERE NOT EXISTS (SELECT 1 FROM AeStats s WHERE s.TextTableID = m.TextTableID)
UNION ALL SELECT 'ae_stats_but_not_vw_AeRisk', COUNT(*) FROM AeStats s WHERE NOT EXISTS (SELECT 1 FROM AeRiskView v WHERE v.TextTableID = s.TextTableID)
UNION ALL SELECT 'vw_AeRisk_but_not_risk_table', COUNT(*) FROM AeRiskView v WHERE NOT EXISTS (SELECT 1 FROM AeRiskTable r WHERE r.TextTableID = v.TextTableID);

SELECT
    c.CoverageStatus,
    c.ExclusionReason,
    COUNT(*) AS CoverageRowCount,
    COUNT(DISTINCT c.TextTableID) AS TextTableCount
FROM dbo.tmp_FlattenedAdverseEventCoverageTable AS c
WHERE c.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
GROUP BY
    c.CoverageStatus,
    c.ExclusionReason
ORDER BY
    CoverageRowCount DESC,
    c.CoverageStatus,
    c.ExclusionReason;

SELECT
    c.TextTableID,
    c.tmp_FlattenedStandardizedTableID,
    c.DocumentGUID,
    c.ParameterName,
    c.TreatmentArm,
    c.ArmN,
    c.PrimaryValue,
    c.PrimaryValueType,
    c.ComparatorArm,
    c.ComparatorN,
    c.ComparatorPrimaryValue,
    c.ComparatorPrimaryValueType,
    c.RR,
    c.CoverageStatus,
    c.ExclusionReason,
    c.CoverageFlags,
    c.CalculationFlags
FROM dbo.tmp_FlattenedAdverseEventCoverageTable AS c
WHERE c.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
  AND NULLIF(LTRIM(RTRIM(c.ParameterName)), '') IS NOT NULL
  AND c.PrimaryValue IS NOT NULL
  AND c.ArmN > 0
  AND c.ComparatorPrimaryValue IS NOT NULL
  AND c.ComparatorN > 0
  AND c.RR IS NULL
ORDER BY
    c.TextTableID,
    c.tmp_FlattenedStandardizedTableID;

SELECT
    s.TextTableID,
    COUNT(*) AS RiskRowsWithoutProductClass
FROM dbo.tmp_FlattenedAdverseEventRiskTable AS r
INNER JOIN dbo.tmp_FlattenedStandardizedTable AS s
    ON r.tmp_FlattenedStandardizedTableID = s.tmp_FlattenedStandardizedTableID
WHERE s.TextTableID IN (SELECT TextTableID FROM #MissingTextTables)
  AND r.PharmacologicClassID IS NULL
GROUP BY
    s.TextTableID
ORDER BY
    RiskRowsWithoutProductClass DESC,
    s.TextTableID;
