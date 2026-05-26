;WITH RandomSample AS
(
    SELECT TOP (500) *
    FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventRiskTable]
    WHERE significance <> 'not significant'
      AND NumberNeededUpperBound IS NOT NULL
    ORDER BY NEWID()
)
SELECT
    JsonLine =
    (
        SELECT RandomSample.*
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    )
FROM RandomSample
ORDER BY NumberNeededUpperBound;
