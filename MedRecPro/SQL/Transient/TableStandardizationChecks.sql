/*******************************************************************************
 * TableStandardizationChecks.sql
 *
 * Diagnostic queries for dbo.tmp_FlattenedStandardizedTable after a table
 * standardization/import run.
 *
 * Usage:
 * 1. Change the settings below for the category/table under review.
 * 2. Execute the whole script to populate the temporary QC helper tables.
 * 3. Re-run individual sections as needed during parser remediation.
 *
 * Notes:
 * - The QC score extraction reads the full QC_PARSE_QUALITY:{score} token.
 *   Do not replace it with fixed-offset parsing; prior audits misread scores
 *   when the offset landed inside the numeric value.
 * - Review-reason aggregates are explicitly joined to rows below
 *   @ClaudeReviewQualityThreshold so older artifacts cannot overcount API work.
 ******************************************************************************/

USE MedRecLocal;
SET NOCOUNT ON;

/*******************************************************************************
 * Settings
 ******************************************************************************/
DECLARE @TargetTableCategory NVARCHAR(100) = N'ADVERSE_EVENT';
DECLARE @ComparisonTableCategory NVARCHAR(100) = N'PK';
DECLARE @TextTableID INT = 86;


DECLARE @QualityScoreToken NVARCHAR(50) = N'QC_PARSE_QUALITY:';
DECLARE @ReviewReasonsToken NVARCHAR(100) = N'QC_PARSE_QUALITY:REVIEW_REASONS:';
DECLARE @QualityScoreLikeToken NVARCHAR(80) = N'QC[_]PARSE[_]QUALITY:';
DECLARE @ReviewReasonsLikeToken NVARCHAR(130) = N'QC[_]PARSE[_]QUALITY:REVIEW[_]REASONS:';
DECLARE @QualityScorePattern NVARCHAR(100) = N'%' + @QualityScoreLikeToken + N'[0-9]%';
DECLARE @ReviewReasonsPattern NVARCHAR(150) = N'%' + @ReviewReasonsLikeToken + N'%';

/*******************************************************************************
 * QC helper tables
 *
 * These temp tables dedupe the repeated PATINDEX/SUBSTRING logic used by the
 * score summary, histogram, malformed-score audit, and review-reason counts.
 ******************************************************************************/
DROP TABLE IF EXISTS #QcScoreTokens;

WITH ScoreToken AS (
    SELECT
        f.[tmp_FlattenedStandardizedTableID],
        f.[TableCategory],
        f.[TextTableID],
        f.[ParameterName],
        f.[TreatmentArm],
        f.[ArmN],
        f.[RawValue],
        f.[PrimaryValue],
        f.[PrimaryValueType],
        f.[ValidationFlags],
        PATINDEX(@QualityScorePattern, f.[ValidationFlags]) AS ScoreStart
    FROM dbo.[tmp_FlattenedStandardizedTable] AS f
    WHERE f.[ValidationFlags] LIKE N'%' + @QualityScoreLikeToken + N'%'
),
RawScore AS (
    SELECT
        s.[tmp_FlattenedStandardizedTableID],
        s.[TableCategory],
        s.[TextTableID],
        s.[ParameterName],
        s.[TreatmentArm],
        s.[ArmN],
        s.[RawValue],
        s.[PrimaryValue],
        s.[PrimaryValueType],
        s.[ValidationFlags],
        s.[ScoreStart],
        CASE
            WHEN s.[ScoreStart] > 0 THEN
                LTRIM(RTRIM(REPLACE(REPLACE(
                    SUBSTRING(
                        s.[ValidationFlags],
                        s.[ScoreStart] + LEN(@QualityScoreToken),
                        CHARINDEX(
                            N';',
                            s.[ValidationFlags] + N';',
                            s.[ScoreStart] + LEN(@QualityScoreToken)
                        ) - (s.[ScoreStart] + LEN(@QualityScoreToken))
                    ),
                CHAR(13), N''), CHAR(10), N'')))
            ELSE NULL
        END AS RawScore
    FROM ScoreToken AS s
)
SELECT
    r.[tmp_FlattenedStandardizedTableID],
    r.[TableCategory],
    r.[TextTableID],
    r.[ParameterName],
    r.[TreatmentArm],
    r.[ArmN],
    r.[RawValue],
    r.[PrimaryValue],
    r.[PrimaryValueType],
    r.[ValidationFlags],
    r.[ScoreStart],
    r.[RawScore],
    TRY_CAST(r.[RawScore] AS FLOAT) AS Score
INTO #QcScoreTokens
FROM RawScore AS r;

CREATE INDEX IX_QcScoreTokens_CategoryScore
    ON #QcScoreTokens ([TableCategory], [Score]);

DROP TABLE IF EXISTS #QcReviewReasons;

WITH ReasonToken AS (
    SELECT
        f.[tmp_FlattenedStandardizedTableID],
        f.[TableCategory],
        f.[ValidationFlags],
        PATINDEX(@ReviewReasonsPattern, f.[ValidationFlags]) AS ReasonStart
    FROM dbo.[tmp_FlattenedStandardizedTable] AS f
    WHERE f.[ValidationFlags] LIKE @ReviewReasonsPattern
),
RawReasons AS (
    SELECT
        r.[tmp_FlattenedStandardizedTableID],
        r.[TableCategory],
        SUBSTRING(
            r.[ValidationFlags],
            r.[ReasonStart] + LEN(@ReviewReasonsToken),
            CHARINDEX(
                N';',
                r.[ValidationFlags] + N';',
                r.[ReasonStart] + LEN(@ReviewReasonsToken)
            ) - (r.[ReasonStart] + LEN(@ReviewReasonsToken))
        ) AS ReasonList
    FROM ReasonToken AS r
    WHERE r.[ReasonStart] > 0
)
SELECT
    rr.[tmp_FlattenedStandardizedTableID],
    rr.[TableCategory],
    LTRIM(RTRIM(splitReason.[value])) AS Reason
INTO #QcReviewReasons
FROM RawReasons AS rr
CROSS APPLY STRING_SPLIT(rr.[ReasonList], N'|') AS splitReason
WHERE LTRIM(RTRIM(splitReason.[value])) <> N'';

CREATE INDEX IX_QcReviewReasons_Row
    ON #QcReviewReasons ([tmp_FlattenedStandardizedTableID], [TableCategory]);

/*******************************************************************************
 * 1. Row preview for the selected category.
 ******************************************************************************/
SELECT
    f.[TextTableID],
    f.[DocumentGUID],
    f.[UNII],
    f.[ProductTitle],
    f.[TableCategory],
    f.[ParentSectionCode],
    f.[SectionTitle],
    f.[ParameterName],
    f.[ParameterCategory],
    f.[ParameterSubtype],
    f.[TreatmentArm],
    f.[ArmN],
    f.[StudyContext],
    f.[Dose],
    f.[DoseUnit],
    f.[DoseRegimen],
    f.[Population],
    f.[Subpopulation],
    f.[Timepoint],
    f.[Time],
    f.[TimeUnit],
    f.[RawValue],
    f.[PrimaryValue],
    f.[PrimaryValueType],
    f.[SecondaryValue],
    f.[SecondaryValueType],
    f.[LowerBound],
    f.[UpperBound],
    f.[BoundType],
    f.[PValue],
    f.[Unit],
    f.[ParseConfidence],
    f.[ParseRule],
    f.[ValidationFlags]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TableCategory] = @TargetTableCategory
ORDER BY
    f.[TableCategory],
    f.[TextTableID],
    f.[SourceRowSeq],
    f.[SourceCellSeq];

/*******************************************************************************
 * 2. JSON line export for the selected category.
 *
 * This keeps the row shape easy to copy into a file or compare between runs.
 ******************************************************************************/
SELECT
    (
        SELECT
            f.[TextTableID],
            f.[DocumentGUID],
            f.[UNII],
            f.[ProductTitle],
            f.[TableCategory],
            f.[ParentSectionCode],
            f.[SectionTitle],
            f.[ParameterName],
            f.[ParameterCategory],
            f.[ParameterSubtype],
            f.[TreatmentArm],
            f.[ArmN],
            f.[StudyContext],
            f.[Dose],
            f.[DoseUnit],
            f.[DoseRegimen],
            f.[Population],
            f.[Subpopulation],
            f.[Timepoint],
            f.[Time],
            f.[TimeUnit],
            f.[RawValue],
            f.[PrimaryValue],
            f.[PrimaryValueType],
            f.[SecondaryValue],
            f.[SecondaryValueType],
            f.[LowerBound],
            f.[UpperBound],
            f.[BoundType],
            f.[PValue],
            f.[Unit],
            f.[ParseConfidence],
            f.[ParseRule],
            f.[ValidationFlags]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS RowJson
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TableCategory] = @TargetTableCategory
ORDER BY
    f.[TextTableID],
    f.[SourceRowSeq],
    f.[SourceCellSeq];

/*******************************************************************************
 * 3. Unit JSON export for the comparison category.
 *
 * Helpful for quickly checking whether PK or another category is leaking unit
 * text into the wrong field after a standardization change.
 ******************************************************************************/
SELECT
    (
        SELECT u.[Unit]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS RowJson
FROM (
    SELECT DISTINCT
        f.[Unit]
    FROM dbo.[tmp_FlattenedStandardizedTable] AS f
    WHERE f.[TableCategory] = @ComparisonTableCategory
) AS u
ORDER BY u.[Unit];

/*******************************************************************************
 * 4. Document coverage for the selected categories.
 ******************************************************************************/
SELECT DISTINCT
    f.[TableCategory],
    f.[DocumentGUID]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TableCategory] IN (@TargetTableCategory, @ComparisonTableCategory)
ORDER BY
    f.[TableCategory],
    f.[DocumentGUID];

SELECT
    f.[TableCategory],
    COUNT(*) AS [RowCount],
    COUNT(DISTINCT f.[DocumentGUID]) AS DistinctDocumentCount
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
GROUP BY f.[TableCategory]
ORDER BY f.[TableCategory];

/*******************************************************************************
 * 5. Parameter taxonomy checks.
 ******************************************************************************/
SELECT DISTINCT
    f.[TableCategory],
    f.[ParameterName],
    f.[ParameterCategory]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TableCategory] <> @TargetTableCategory
ORDER BY
    f.[TableCategory],
    f.[ParameterName],
    f.[ParameterCategory];

SELECT DISTINCT
    f.[TableCategory],
    f.[ParameterName],
    f.[ParameterCategory]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TableCategory] = @TargetTableCategory
  AND f.[ParameterCategory] IS NULL
ORDER BY
    f.[ParameterName],
    f.[ParameterCategory];

SELECT DISTINCT
    CONCAT(
        COALESCE(f.[ParameterName], N''),
        N'|',
        COALESCE(f.[ParameterCategory], N'')
    ) AS ParameterNameCategoryPipe
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
ORDER BY ParameterNameCategoryPipe;

/*******************************************************************************
 * 6. Table, ingredient, and duplicate sanity checks.
 ******************************************************************************/
SELECT
    COUNT(DISTINCT f.[UNII]) AS DistinctUNIICount
FROM dbo.[tmp_FlattenedStandardizedTable] AS f;

SELECT
    COUNT(*) AS TotalRows
FROM dbo.[tmp_FlattenedStandardizedTable] AS f;

WITH DuplicateObservationGroups AS (
    SELECT
        f.[DocumentGUID],
        f.[UNII],
        f.[TextTableID],
        f.[SourceRowSeq],
        f.[SourceCellSeq],
        f.[TableCategory],
        f.[ParameterName],
        f.[ParameterCategory],
        f.[ParameterSubtype],
        f.[TreatmentArm],
        f.[ArmN],
        f.[StudyContext],
        f.[Dose],
        f.[DoseUnit],
        f.[DoseRegimen],
        f.[Population],
        f.[Subpopulation],
        f.[Timepoint],
        f.[Time],
        f.[TimeUnit],
        f.[RawValue],
        f.[PrimaryValue],
        f.[PrimaryValueType],
        f.[SecondaryValue],
        f.[SecondaryValueType],
        f.[LowerBound],
        f.[UpperBound],
        f.[BoundType],
        f.[PValue],
        f.[Unit],
        f.[ParseConfidence],
        f.[ParseRule],
        f.[ValidationFlags],
        COUNT(*) AS [RowCount]
    FROM dbo.[tmp_FlattenedStandardizedTable] AS f
    GROUP BY
        f.[DocumentGUID],
        f.[UNII],
        f.[TextTableID],
        f.[SourceRowSeq],
        f.[SourceCellSeq],
        f.[TableCategory],
        f.[ParameterName],
        f.[ParameterCategory],
        f.[ParameterSubtype],
        f.[TreatmentArm],
        f.[ArmN],
        f.[StudyContext],
        f.[Dose],
        f.[DoseUnit],
        f.[DoseRegimen],
        f.[Population],
        f.[Subpopulation],
        f.[Timepoint],
        f.[Time],
        f.[TimeUnit],
        f.[RawValue],
        f.[PrimaryValue],
        f.[PrimaryValueType],
        f.[SecondaryValue],
        f.[SecondaryValueType],
        f.[LowerBound],
        f.[UpperBound],
        f.[BoundType],
        f.[PValue],
        f.[Unit],
        f.[ParseConfidence],
        f.[ParseRule],
        f.[ValidationFlags]
    HAVING COUNT(*) > 1
)
SELECT
    COUNT(*) AS DuplicateGroupCount,
    COALESCE(SUM(d.[RowCount] - 1), 0) AS DuplicateRowCount
FROM DuplicateObservationGroups AS d;

SELECT
    N'dbo.TextTable' AS SourceName,
    MAX(t.[TextTableID]) AS MaxTextTableID
FROM dbo.[TextTable] AS t
UNION ALL
SELECT
    N'dbo.tmp_FlattenedStandardizedTable' AS SourceName,
    MAX(f.[TextTableID]) AS MaxTextTableID
FROM dbo.[tmp_FlattenedStandardizedTable] AS f;

SELECT
    f.*
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
WHERE f.[TextTableID] = @TextTableID
ORDER BY
    f.[SourceRowSeq],
    f.[SourceCellSeq];

SELECT
    f.[ValidationFlags],
    COUNT(*) AS [RowCount]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
GROUP BY f.[ValidationFlags]
ORDER BY
    [RowCount] DESC,
    f.[ValidationFlags];

SELECT
    f.[TableCategory],
    COUNT(*) AS [RowCount]
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
GROUP BY f.[TableCategory]
ORDER BY f.[TableCategory];

/*******************************************************************************
 * 7. Claude/API burden summary.
 ******************************************************************************/
DECLARE @ClaudeReviewQualityThreshold FLOAT = 0.75;
SELECT
    ISNULL(q.[TableCategory], N'- ALL -') AS Category,
    COUNT(*) AS TotalRows,
    SUM(CASE WHEN q.[Score] < @ClaudeReviewQualityThreshold THEN 1 ELSE 0 END) AS ClaudeForwarded,
    SUM(CASE WHEN q.[Score] >= @ClaudeReviewQualityThreshold THEN 1 ELSE 0 END) AS Skipped,
    CAST(
        SUM(CASE WHEN q.[Score] < @ClaudeReviewQualityThreshold THEN 1.0 ELSE 0.0 END)
            / NULLIF(COUNT(*), 0)
        AS DECIMAL(9, 4)
    ) AS ForwardRate,
    FORMAT(
        SUM(CASE WHEN q.[Score] < @ClaudeReviewQualityThreshold THEN 1.0 ELSE 0.0 END)
            / NULLIF(COUNT(*), 0),
        N'P1'
    ) AS ForwardRateDisplay
FROM #QcScoreTokens AS q
WHERE q.[Score] IS NOT NULL
GROUP BY GROUPING SETS ((q.[TableCategory]), ())
ORDER BY
    GROUPING(q.[TableCategory]) DESC,
    ForwardRate DESC,
    Category;

/*******************************************************************************
 * 8. QC score histogram.
 ******************************************************************************/
DECLARE @ScoreBinWidth FLOAT = 0.05;
WITH Scores AS (
    SELECT
        q.[Score]
    FROM #QcScoreTokens AS q
    WHERE q.[Score] IS NOT NULL
),
Histogram AS (
    SELECT
        FLOOR(s.[Score] / @ScoreBinWidth) * @ScoreBinWidth AS BinStart,
        COUNT(*) AS [RowCount],
        MIN(s.[Score]) AS BinMin,
        MAX(s.[Score]) AS BinMax,
        AVG(s.[Score]) AS BinAvg
    FROM Scores AS s
    GROUP BY FLOOR(s.[Score] / @ScoreBinWidth) * @ScoreBinWidth
)
SELECT
    FORMAT(h.[BinStart], N'0.00') + N' - ' + FORMAT(h.[BinStart] + @ScoreBinWidth, N'0.00') AS ScoreRange,
    CASE
        WHEN h.[BinStart] + @ScoreBinWidth <= @ClaudeReviewQualityThreshold THEN N'CLAUDE'
        WHEN h.[BinStart] < @ClaudeReviewQualityThreshold THEN N'STRADDLE'
        ELSE N'SKIP'
    END AS Fate,
    h.[RowCount],
    FORMAT(h.[BinMin], N'0.0000') AS BinMin,
    FORMAT(h.[BinMax], N'0.0000') AS BinMax,
    FORMAT(h.[BinAvg], N'0.0000') AS BinAvg,
    REPLICATE(N'█', CAST(ROUND(h.[RowCount] * 50.0 / MAX(h.[RowCount]) OVER (), 0) AS INT)) AS Distribution
FROM Histogram AS h
ORDER BY h.[BinStart];

/*******************************************************************************
 * 9. Review reasons on Claude-forwarded rows.
 ******************************************************************************/
DROP TABLE IF EXISTS #QcForwardedReasonCounts;

SELECT
    r.[TableCategory],
    r.[Reason],
    COUNT(*) AS [RowCount]
INTO #QcForwardedReasonCounts
FROM #QcReviewReasons AS r
INNER JOIN #QcScoreTokens AS q
    ON q.[tmp_FlattenedStandardizedTableID] = r.[tmp_FlattenedStandardizedTableID]
WHERE q.[Score] < @ClaudeReviewQualityThreshold
GROUP BY
    r.[TableCategory],
    r.[Reason];

SELECT
    c.[TableCategory],
    c.[Reason],
    c.[RowCount]
FROM #QcForwardedReasonCounts AS c
ORDER BY
    c.[TableCategory],
    c.[RowCount] DESC,
    c.[Reason];

SELECT
    (
        SELECT
            c.[TableCategory],
            c.[Reason],
            c.[RowCount]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ) AS JsonLine
FROM #QcForwardedReasonCounts AS c
ORDER BY
    c.[TableCategory],
    c.[RowCount] DESC,
    c.[Reason];

/*******************************************************************************
 * 10. Rows with malformed or missing numeric QC score tokens.
 ******************************************************************************/
SELECT
    q.[TableCategory],
    q.[TextTableID],
    q.[ParameterName],
    q.[TreatmentArm],
    q.[ArmN],
    q.[RawValue],
    q.[PrimaryValue],
    q.[PrimaryValueType],
    q.[ValidationFlags],
    CONCAT(N'[', COALESCE(q.[RawScore], N''), N']') AS ExtractedValue
FROM #QcScoreTokens AS q
WHERE q.[ScoreStart] = 0
   OR q.[Score] IS NULL
ORDER BY
    ExtractedValue,
    q.[TableCategory],
    q.[TextTableID];

/*******************************************************************************
 * 11. Active ingredient join for category-focused drill-down.
 ******************************************************************************/
SELECT
    ai.[UNII] AS ActiveIngredientUNII,
    f.*
FROM dbo.[tmp_FlattenedStandardizedTable] AS f
INNER JOIN dbo.[vw_ActiveIngredients] AS ai
    ON ai.[DocumentGUID] = f.[DocumentGUID]
WHERE f.[TableCategory] = @TargetTableCategory
ORDER BY
    ai.[UNII],
    f.[TextTableID],
    f.[SourceRowSeq],
    f.[SourceCellSeq];
