USE MedRecLocal

SELECT  [TextTableID]
    ,[DocumentGUID]
    ,[TableCategory]
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

-- Json output
DECLARE @header NVARCHAR(MAX) = '================================================================================';
DECLARE @title NVARCHAR(MAX) = 'PK TABLE DATA - NDJSON FORMAT (each line is valid JSON)';

PRINT @header;
PRINT @title;
PRINT @header;

SELECT 
    (SELECT 
        [TextTableID],
        [TableCategory],
        [ParameterName],
        [ParameterCategory],
        [ParameterSubtype],
        [TreatmentArm],
        [ArmN],
        [StudyContext],
        [Dose],
        [DoseUnit],
        [DoseRegimen],
        [RawValue],
        [PrimaryValue],
        [PrimaryValueType],
        [SecondaryValue],
        [SecondaryValueType],
        [LowerBound],
        [UpperBound],
        [BoundType],
        [Unit],
        [ParseConfidence],
        [ParseRule],
        [ValidationFlags]
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS JsonLine
FROM [dbo].[tmp_FlattenedStandardizedTable]
WHERE TableCategory = 'PK' and  PrimaryValue is null
ORDER BY [TextTableID];

PRINT @header;

--

SELECT [TableCategory]
    ,[ParameterName]
	,[ParameterCategory]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TableCategory <> 'ADVERSE_EVENT'
order by TableCategory

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


-- No Model checks
Select * from (SELECT 
    LTRIM(RTRIM(REPLACE(REPLACE(
                SUBSTRING(
                    ValidationFlags,
                    CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20,
                    CHARINDEX(';', ValidationFlags + ';',
                        CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
                    - (CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
                ),
            CHAR(13), ''), CHAR(10), ''))) AS RawScore
    ,[TableCategory]
    ,[TreatmentArm]
	,[ParameterName]
    ,[UNII]
    ,[TextTableID]
	,[DocumentGUID]
FROM [dbo].[tmp_FlattenedStandardizedTable] A
) B
Where B.RawScore = 'NOMODEL'
order by UNII, TreatmentArm, [ParameterName] -- ct 5675
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

-- Histogram of MLNET_ANOMALY_SCORE from [ValidationFlags]
-- Bin width: 0.05 | Visual bar scaled to max bucket

WITH RawExtract AS (
    SELECT
        LTRIM(RTRIM(REPLACE(REPLACE(
            SUBSTRING(
                ValidationFlags,
                CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20,
                CHARINDEX(';', ValidationFlags + ';',
                    CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
                - (CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
            ),
        CHAR(13), ''), CHAR(10), ''))) AS RawScore
    FROM dbo.tmp_FlattenedStandardizedTable
    WHERE ValidationFlags LIKE '%MLNET_ANOMALY_SCORE:%'
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
    Cnt                                                                   AS [Count],
    FORMAT(BinMin, '0.0000')                                              AS [Min],
    FORMAT(BinMax, '0.0000')                                              AS [Max],
    FORMAT(BinAvg, '0.0000')                                              AS [Avg],
    REPLICATE(N'█', CAST(ROUND(Cnt * 50.0 / MAX(Cnt) OVER (), 0) AS INT)) AS [Distribution]
FROM Histogram
ORDER BY BinStart;



-- Show rows that failed to parse
SELECT [TableCategory]
    ,[ParameterName]
	,[ArmN]
    ValidationFlags,
    '[' + LTRIM(RTRIM(REPLACE(REPLACE(
        SUBSTRING(
            ValidationFlags,
            CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20,
            CHARINDEX(';', ValidationFlags + ';',
                CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
            - (CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
        ),
    CHAR(13), ''), CHAR(10), ''))) + ']' AS ExtractedValue
FROM dbo.tmp_FlattenedStandardizedTable
WHERE ValidationFlags LIKE '%MLNET_ANOMALY_SCORE:%'
  AND TRY_CAST(
        LTRIM(RTRIM(REPLACE(REPLACE(
            SUBSTRING(
                ValidationFlags,
                CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20,
                CHARINDEX(';', ValidationFlags + ';',
                    CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
                - (CHARINDEX('MLNET_ANOMALY_SCORE:', ValidationFlags) + 20)
            ),
        CHAR(13), ''), CHAR(10), '')))
      AS FLOAT) IS NULL
Order By ExtractedValue


SELECT dbo.vw_ActiveIngredients.UNII
	,dbo.tmp_FlattenedStandardizedTable.*
FROM dbo.tmp_FlattenedStandardizedTable
INNER JOIN dbo.vw_ActiveIngredients ON dbo.tmp_FlattenedStandardizedTable.DocumentGUID = dbo.vw_ActiveIngredients.DocumentGUID