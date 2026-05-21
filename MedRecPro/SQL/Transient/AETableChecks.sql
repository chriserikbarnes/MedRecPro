use MedRecLocal

  -- 05/18/2026 pick up here to evaluate these ids
  -- that have missing comparitor N values
  select distinct TextTableID
  from [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable]
  where tmp_FlattenedStandardizedTableID in (SELECT tmp_FlattenedStandardizedTableID
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  RR is null and IsPlaceboControlled = 1)

    select distinct TextTableID
  from [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable]
  where tmp_FlattenedStandardizedTableID in (SELECT tmp_FlattenedStandardizedTableID
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  RR is null and ArmN is not null)

SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where RR is not null and IsPlaceboControlled = 1

  SELECT distinct ParameterName, ParameterCategory
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where RR is not null and IsPlaceboControlled = 1

  SELECT COUNT(*) AS CiEligibleCount
FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
WHERE TreatmentArm <> ComparatorArm
  AND ParameterName IS NOT NULL
  AND PrimaryValueType <> 'Text'
  AND RR IS NOT NULL
  AND RRLowerBound IS NOT NULL
  AND RRUpperBound IS NOT NULL
  AND ArmN > 0
  AND ComparatorN > 0;

select distinct *
  from [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where tmp_FlattenedStandardizedTableID in (SELECT tmp_FlattenedStandardizedTableID
  FROM  [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID = 26461)

  select distinct *
  from [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where tmp_FlattenedStandardizedTableID in (SELECT tmp_FlattenedStandardizedTableID
  FROM  [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID in (select distinct TextTableID
  from [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable]
  where tmp_FlattenedStandardizedTableID in (SELECT tmp_FlattenedStandardizedTableID
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  RR is null and IsPlaceboControlled = 1)))
  --

  WITH TargetTables AS
(
    SELECT DISTINCT fst.TextTableID
    FROM [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable] fst
    INNER JOIN [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable] ae
        ON ae.tmp_FlattenedStandardizedTableID = fst.tmp_FlattenedStandardizedTableID
    WHERE ae.RR IS NOT NULL
      AND ae.IsPlaceboControlled = 1
)
SELECT
    (
        SELECT
            tt.TextTableID,
            JSON_QUERY((
                SELECT DISTINCT
                    ae.ParameterName,
                    ae.ParameterCategory
                FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable] ae
                INNER JOIN [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable] fst
                    ON fst.tmp_FlattenedStandardizedTableID = ae.tmp_FlattenedStandardizedTableID
                WHERE fst.TextTableID = tt.TextTableID
                  AND ae.RR IS NOT NULL
                  AND ae.IsPlaceboControlled = 1
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )) AS adverseEventParameters
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS JsonLine
FROM TargetTables tt
ORDER BY tt.TextTableID;

  --
 WITH TargetTables AS
(
    SELECT DISTINCT fst.TextTableID
    FROM [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable] fst
    WHERE fst.tmp_FlattenedStandardizedTableID IN
    (
        SELECT ae.tmp_FlattenedStandardizedTableID
        FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable] ae
        WHERE ae.RR IS NULL
          AND ae.IsPlaceboControlled = 1
    )
)
SELECT
    (
        SELECT
            tt.TextTableID,
            JSON_QUERY((
                SELECT DISTINCT ae.*
                FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable] ae
                INNER JOIN [MedRecLocal].[dbo].[tmp_FlattenedStandardizedTable] fst
                    ON fst.tmp_FlattenedStandardizedTableID = ae.tmp_FlattenedStandardizedTableID
                WHERE fst.TextTableID = tt.TextTableID
                FOR JSON PATH, INCLUDE_NULL_VALUES
            )) AS adverseEvents
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES
    ) AS JsonLine
FROM TargetTables tt
ORDER BY tt.TextTableID;

SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where --(RRLowerBound is not null and RRUpperBound is not null) or
  (RRLowerBound < 1 and RRUpperBound < 1)
  or (RRLowerBound > 1 and RRUpperBound >1)
  and RR is not null
  --where tmp_FlattenedStandardizedTableID in ( SELECT tmp_FlattenedStandardizedTableID
  --FROM [dbo].[tmp_FlattenedStandardizedTable]
  --where TextTableID = 8006)
  order by UNII

  SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  RR is null and IsPlaceboControlled = 1 --ComparatorArm = 'Placebo' and IsPlaceboControlled = 0

  SELECT distinct DocumentGUID
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where RR is null -- ComparatorArm = 'Placebo' 

    SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  --RR is  null and  
  tmp_FlattenedStandardizedTableID in ( SELECT tmp_FlattenedStandardizedTableID
  FROM [dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID in (SELECT TextTableID
  FROM [dbo].[tmp_FlattenedStandardizedTable] where tmp_FlattenedStandardizedTableID = 41033))
    order by ParameterName

    SELECT *
  FROM [dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID in (SELECT TextTableID
  FROM [dbo].[tmp_FlattenedStandardizedTable] where tmp_FlattenedStandardizedTableID = 41033)
  order by ParameterName

  -- (a) StudyContext propagated
SELECT COUNT(*) FROM tmp_FlattenedAdverseEventTable WHERE StudyContext IS NOT NULL;  -- > 0

-- (b) Comparator pairing respects population
SELECT TreatmentArm, ComparatorArm, StudyContext, ArmN, ComparatorN
FROM tmp_FlattenedAdverseEventTable
WHERE tmp_FlattenedStandardizedTableID IN (
    SELECT tmp_FlattenedStandardizedTableID FROM tmp_FlattenedStandardizedTable WHERE TextTableID = 44661
)
AND ParameterName = 'Somnolence';
-- expect: Adults Clomipramine paired with Adults Placebo (319);
--         Children Clomipramine paired with Children Placebo (44).

-- (c) Subpopulation header rows no longer leak into AE table (scoped to 44661)
SELECT COUNT(*) FROM tmp_FlattenedAdverseEventTable
WHERE ParameterName IN ('Female Patients Only','Male Patients Only')
  AND tmp_FlattenedStandardizedTableID IN (
      SELECT tmp_FlattenedStandardizedTableID FROM tmp_FlattenedStandardizedTable WHERE TextTableID = 44661
  );
-- expect 0

-- (d) Subpopulation context stamped on dependent rows with the right N
SELECT ParameterName, Subpopulation, ArmN, PrimaryValue
FROM tmp_FlattenedAdverseEventTable
WHERE ParameterName = 'Dysmenorrhea'
  AND tmp_FlattenedStandardizedTableID IN (
      SELECT tmp_FlattenedStandardizedTableID FROM tmp_FlattenedStandardizedTable WHERE TextTableID = 44661
  );
-- expect: Subpopulation='Female Patients Only', ArmN in {182, 167, 10, 21}

-- (e) False-positive guard: the "Male and Female Patients Combined" row stays structural (scoped)
SELECT COUNT(*) FROM tmp_FlattenedAdverseEventTable
WHERE ParameterName = 'Male and Female Patients Combined'
  AND tmp_FlattenedStandardizedTableID IN (
      SELECT tmp_FlattenedStandardizedTableID FROM tmp_FlattenedStandardizedTable WHERE TextTableID = 44661
  );
-- expect 0

-- (f) Event-count coherence: in a Female-only slice the derived EventsTreatment must
--     be computed from the subpopulation N, not the full-study N
SELECT TOP 5 ParameterName, Subpopulation, ArmN, ComparatorN, EventsTreatment, EventsComparator, RR, DNRR
FROM tmp_FlattenedAdverseEventTable
WHERE Subpopulation = 'Female Patients Only'
  AND tmp_FlattenedStandardizedTableID IN (
      SELECT tmp_FlattenedStandardizedTableID FROM tmp_FlattenedStandardizedTable WHERE TextTableID = 44661
  );
-- expect ArmN/ComparatorN in the female-N space (e.g. 182/167); EventsTreatment ≈ ArmN * PrimaryValue / 100

  SELECT count(*) As  ParameterCategoryNullCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where (RRLowerBound < 1 and RRUpperBound < 1)
  or (RRLowerBound > 1 and RRUpperBound >1)
  or (RRLowerBound is null and RRUpperBound is null)
  and RR is not null
  and ParameterCategory is null

  SELECT count(*) As  ParameterCategoryNotNullCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where (RRLowerBound < 1 and RRUpperBound < 1)
  or (RRLowerBound > 1 and RRUpperBound >1)
  or (RRLowerBound is null and RRUpperBound is null)
  and RR is not null
  and ParameterCategory is not null

  SELECT count(*) As  NotSignificantNotOrNullCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where (RRLowerBound < 1 and RRUpperBound > 1)
  or (RRLowerBound is null and RRUpperBound is null)
  and RR is not null

    SELECT count(*) As  RRNotNullCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where RR is not null

      SELECT count(*) As  RRNullCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where RR is  null


    SELECT count(*) As TotalCt
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  --where tmp_FlattenedStandardizedTableID in ( SELECT tmp_FlattenedStandardizedTableID
  --FROM [dbo].[tmp_FlattenedStandardizedTable]
  --where TextTableID = 8006)

  SELECT *
  FROM [dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID = 8006
  order by ParameterName

  SELECT COUNT(*) AS missed_positives
FROM tmp_FlattenedAdverseEventTable
WHERE CalculationFlags LIKE 'PLACEBO_COMPARATOR%'
  AND IsPlaceboControlled = 0;

  SELECT COUNT(*) AS false_positives
FROM tmp_FlattenedAdverseEventTable
WHERE CalculationFlags NOT LIKE 'PLACEBO_COMPARATOR%'
  AND IsPlaceboControlled = 1;

  SELECT COUNT(*) FROM tmp_FlattenedAdverseEventTable
WHERE ComparatorArm = 'Placebo' AND IsPlaceboControlled = 0;
