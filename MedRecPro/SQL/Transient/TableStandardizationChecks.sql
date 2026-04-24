USE MedRecLocal

SELECT  --[TextTableID]
    --,[DocumentGUID]
    [TableCategory]
	,[ParameterName]
	,[ParameterCategory]
	,[ParameterSubtype]
	,[TreatmentArm]
	,[ArmN]
	,[StudyContext]
	,[Dose]
	,[DoseUnit]
	,[DoseRegimen]
	,[RawValue]
	,[PrimaryValue]
	,[PrimaryValueType]
	,[SecondaryValue]
	,[SecondaryValueType]
	,[LowerBound]
	,[UpperBound]
	,[BoundType]
	,[Unit]
	,[ParseConfidence]
	,[ParseRule]
	,[ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TableCategory = 'PK' --and PrimaryValue is null -- and PrimaryValueType = 'ArithmeticMean' and ParameterName is not null
order by TableCategory
--Where PValue is not null
--where ArmN is not null and PrimaryValue is not null
--where [ParameterCategory] = 'Gastrointestinal Disorders' and ArmN is not null
--order by ParameterName
--where ParameterSubtype is not null
--where [TextTableID] = 203


SELECT distinct [DocumentGUID]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TableCategory = 'PK' 

SELECT distinct [DocumentGUID]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TableCategory = 'ADVERSE_EVENT'

--

SELECT [TableCategory]
    ,[ParameterName]
	,[ParameterCategory]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TableCategory <> 'ADVERSE_EVENT'
order by TableCategory

select distinct count(unii) as UNIICount from (select distinct unii from [dbo].[tmp_FlattenedStandardizedTable]) A

-- This will output each row as pipe-delimited text in the Messages tab. You can then copy the entire output and paste it into a file.
DECLARE @ParameterName NVARCHAR(MAX)
DECLARE @ParameterCategory NVARCHAR(MAX)

DECLARE cur CURSOR FOR
SELECT DISTINCT [ParameterName]
	,[ParameterCategory]
FROM [dbo].[tmp_FlattenedStandardizedTable]
ORDER BY [ParameterName]

OPEN cur
FETCH NEXT FROM cur INTO @ParameterName, @ParameterCategory

WHILE @@FETCH_STATUS = 0
BEGIN
    RAISERROR('%s|%s', 0, 1, @ParameterName, @ParameterCategory) WITH NOWAIT
    FETCH NEXT FROM cur INTO @ParameterName, @ParameterCategory
END

CLOSE cur
DEALLOCATE cur

--order by UNII
--where TextTableID = 185

Select Count(*) As DCount from (Select Distinct * from [dbo].[tmp_FlattenedStandardizedTable]) A -- 255111
Select Count(*) As DCount from [dbo].[tmp_FlattenedStandardizedTable] A

select max(TextTableID) as Biggest from TextTable --46453
select max(TextTableID) as Biggest from [dbo].[tmp_FlattenedStandardizedTable] --46451



SELECT  *
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TextTableID = 86


SELECT [ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]

SELECT Distinct [TableCategory]
    ,[ParameterName]
	,[ParameterCategory]
from [dbo].[tmp_FlattenedStandardizedTable]
where [TableCategory] = 'ADVERSE_EVENT'
and ParameterCategory is null
--and ParameterName = 'Transient elevation in ALT'
--([ParameterCategory] = 'Investigations'
--or ParameterName = 'Transient elevation in ALT')
order by ParameterName

Select distinct [TableCategory] from [dbo].[tmp_FlattenedStandardizedTable]

-- API burden summary (single glance at total volume and rate)

DECLARE @Threshold FLOAT = 0.75;  -- keep in sync with ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold

WITH ScoredRows AS (
    SELECT
        [TableCategory],
        TRY_CAST(
            LTRIM(RTRIM(REPLACE(REPLACE(
                SUBSTRING(
                    ValidationFlags,
                    PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20,
                    CHARINDEX(';', ValidationFlags + ';',
                        PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
                    - (PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
                ),
            CHAR(13), ''), CHAR(10), ''))) AS FLOAT) AS Score
    FROM dbo.tmp_FlattenedStandardizedTable
    WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:[0-9]%'
)
SELECT
    ISNULL([TableCategory], '— ALL —')                          AS [Category],
    COUNT(*)                                                     AS [Total Rows],
    SUM(CASE WHEN Score < @Threshold THEN 1 ELSE 0 END)          AS [Claude-Forwarded],
    SUM(CASE WHEN Score >= @Threshold THEN 1 ELSE 0 END)         AS [Skipped],
    FORMAT(
        SUM(CASE WHEN Score < @Threshold THEN 1.0 ELSE 0 END) / NULLIF(COUNT(*), 0),
        'P1'
    )                                                            AS [Forward Rate]
FROM ScoredRows
WHERE Score IS NOT NULL
GROUP BY GROUPING SETS (([TableCategory]), ())
ORDER BY GROUPING([TableCategory]) DESC, [Forward Rate] DESC;

-- Enhanced histogram with threshold banding:

DECLARE @Threshold FLOAT = 0.75;

WITH RawExtract AS (
    SELECT
        LTRIM(RTRIM(REPLACE(REPLACE(
            SUBSTRING(
                ValidationFlags,
                PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20,
                CHARINDEX(';', ValidationFlags + ';',
                    PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
                - (PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
            ),
        CHAR(13), ''), CHAR(10), ''))) AS RawScore
    FROM dbo.tmp_FlattenedStandardizedTable
    WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:[0-9]%'
),
Scores AS (
    SELECT TRY_CAST(RawScore AS FLOAT) AS Score
    FROM RawExtract
    WHERE TRY_CAST(RawScore AS FLOAT) IS NOT NULL
),
Histogram AS (
    SELECT
        FLOOR(Score / 0.05) * 0.05 AS BinStart,
        COUNT(*)                    AS Cnt,
        MIN(Score)                  AS BinMin,
        MAX(Score)                  AS BinMax,
        AVG(Score)                  AS BinAvg
    FROM Scores
    GROUP BY FLOOR(Score / 0.05) * 0.05
)
SELECT
    FORMAT(BinStart, '0.00') + N' – ' + FORMAT(BinStart + 0.05, '0.00') AS [Score Range],
    CASE WHEN BinStart + 0.05 <= @Threshold THEN N'→ Claude'
         WHEN BinStart       <  @Threshold THEN N'↘ straddle'
         ELSE                                    N'  skip'   END            AS [Fate],
    Cnt                                                                     AS [Count],
    FORMAT(BinMin, '0.0000')                                                AS [Min],
    FORMAT(BinMax, '0.0000')                                                AS [Max],
    FORMAT(BinAvg, '0.0000')                                                AS [Avg],
    REPLICATE(N'█', CAST(ROUND(Cnt * 50.0 / MAX(Cnt) OVER (), 0) AS INT))  AS [Distribution]
FROM Histogram
ORDER BY BinStart;

-- Reasons on forwarded rows only (after the code change, REVIEW_REASONS only 
-- emits sub-threshold, so this is now a direct count of what's driving API calls)

SELECT
    [TableCategory],
    LTRIM(RTRIM(value)) AS Reason,
    COUNT(*)             AS Cnt
FROM dbo.tmp_FlattenedStandardizedTable
CROSS APPLY STRING_SPLIT(
    SUBSTRING(
        ValidationFlags,
        PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
            + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'),
        CHARINDEX(';', ValidationFlags + ';',
            PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
                + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
        - (PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
            + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
    ),
    '|')
WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:REVIEW_REASONS:%'
GROUP BY [TableCategory], LTRIM(RTRIM(value))
ORDER BY [TableCategory], Cnt DESC;

--

SELECT (
    SELECT
        agg.TableCategory,
        agg.Reason,
        agg.Cnt
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
) AS JsonLine
FROM (
    SELECT
        [TableCategory],
        LTRIM(RTRIM(value)) AS Reason,
        COUNT(*)             AS Cnt
    FROM dbo.tmp_FlattenedStandardizedTable
    CROSS APPLY STRING_SPLIT(
        SUBSTRING(
            ValidationFlags,
            PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
                + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'),
            CHARINDEX(';', ValidationFlags + ';',
                PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
                    + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
            - (PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
                + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
        ),
        '|')
    WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:REVIEW_REASONS:%'
    GROUP BY [TableCategory], LTRIM(RTRIM(value))
) agg
ORDER BY agg.TableCategory, agg.Cnt DESC;

-- Which reasons fire most often on Claude-forwarded rows, broken down by TableCategory?
SELECT
    [TableCategory],
    LTRIM(RTRIM(value)) AS Reason,
    COUNT(*)             AS Cnt
FROM dbo.tmp_FlattenedStandardizedTable
CROSS APPLY STRING_SPLIT(
    SUBSTRING(
        ValidationFlags,
        PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
            + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'),
        CHARINDEX(';', ValidationFlags + ';',
            PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
                + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
        - (PATINDEX('%QC_PARSE_QUALITY:REVIEW_REASONS:%', ValidationFlags)
            + LEN('QC_PARSE_QUALITY:REVIEW_REASONS:'))
    ),
    '|')
WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:REVIEW_REASONS:%'
GROUP BY [TableCategory], LTRIM(RTRIM(value))
ORDER BY [TableCategory], Cnt DESC;


-- Show rows that failed to parse
SELECT [TableCategory]
    ,[ParameterName]
    ,[ArmN]
    ,[RawValue]
    ,[PrimaryValue]
    ,[PrimaryValueType]
    ,ValidationFlags
    ,'[' + LTRIM(RTRIM(REPLACE(REPLACE(
        SUBSTRING(
            ValidationFlags,
            PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20,
            CHARINDEX(';', ValidationFlags + ';',
                PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
            - (PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
        ),
    CHAR(13), ''), CHAR(10), ''))) + ']' AS ExtractedValue
FROM dbo.tmp_FlattenedStandardizedTable
WHERE ValidationFlags LIKE '%QC_PARSE_QUALITY:%'
  AND (
    ValidationFlags NOT LIKE '%QC_PARSE_QUALITY:[0-9]%'
    OR TRY_CAST(
        LTRIM(RTRIM(REPLACE(REPLACE(
            SUBSTRING(
                ValidationFlags,
                PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20,
                CHARINDEX(';', ValidationFlags + ';',
                    PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
                - (PATINDEX('%QC_PARSE_QUALITY:[0-9]%', ValidationFlags) + 20)
            ),
        CHAR(13), ''), CHAR(10), '')))
      AS FLOAT) IS NULL
  )
ORDER BY ExtractedValue;


SELECT dbo.vw_ActiveIngredients.UNII
	,dbo.tmp_FlattenedStandardizedTable.*
FROM dbo.tmp_FlattenedStandardizedTable
INNER JOIN dbo.vw_ActiveIngredients ON dbo.tmp_FlattenedStandardizedTable.DocumentGUID = dbo.vw_ActiveIngredients.DocumentGUID