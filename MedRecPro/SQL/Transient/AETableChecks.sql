use MedRecLocal

SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where (RRLowerBound < 1 and RRUpperBound < 1)
  or (RRLowerBound > 1 and RRUpperBound >1)
  or (RRLowerBound is null and RRUpperBound is null)
  and RR is not null
  --where tmp_FlattenedStandardizedTableID in ( SELECT tmp_FlattenedStandardizedTableID
  --FROM [dbo].[tmp_FlattenedStandardizedTable]
  --where TextTableID = 8006)
  order by ParameterName

  SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where  RR is null --ComparatorArm = 'Placebo' and IsPlaceboControlled = 0

  
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

SELECT COUNT(*) FROM tmp_FlattenedAdverseEventTable a
JOIN tmp_FlattenedStandardizedTable s 
ON a.tmp_FlattenedAdverseEventTableID = s.tmp_FlattenedStandardizedTableID
WHERE s.TextTableID = 23177 AND a.IsPlaceboControlled = 0;

SELECT * FROM tmp_FlattenedAdverseEventTable a
JOIN tmp_FlattenedStandardizedTable s 
ON a.tmp_FlattenedAdverseEventTableID = s.tmp_FlattenedStandardizedTableID
WHERE s.TextTableID = 23177 AND a.IsPlaceboControlled = 0;