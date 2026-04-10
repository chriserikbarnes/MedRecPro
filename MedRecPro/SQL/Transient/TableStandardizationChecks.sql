USE MedRecLocal

SELECT  [TextTableID]
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
	--,[PValue]
	,[Unit]
	,[ParseConfidence]
	,[ParseRule]
	,[ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]
--Where PValue is not null
--where ArmN is not null and PrimaryValue is not null
--where [ParameterCategory] = 'Gastrointestinal Disorders' and ArmN is not null
--order by ParameterName
--where ParameterSubtype is not null
--where [TextTableID] = 203

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
    ,[TableCategory]           
	,[ParameterName]
	,[ParameterCategory]
	,[ParameterSubtype]
	,[TreatmentArm]
	,[ArmN]
	,[StudyContext]
	,[DoseRegimen]
	,[RawValue]
	,[PrimaryValue]
	,[PrimaryValueType]
	,[SecondaryValue]
	,[SecondaryValueType]
	,[LowerBound]
	,[UpperBound]
	,[BoundType]
	,[PValue]
	,[Unit]
	,[ParseConfidence]
	,[DocumentGUID]
FROM [dbo].[tmp_FlattenedStandardizedTable]
--where TextTableID = 185


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

