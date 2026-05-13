SELECT  
      [ParameterName]
      ,[ParameterCategory]
      ,[ArmN]     
      ,[PrimaryValueType]
      ,[StudyContext]
      ,[Population]
      ,[Subpopulation]
      ,[TreatmentArm]
      ,[ComparatorArm]
      ,[ComparatorN]
      ,[IsPlaceboControlled]
      ,[EventsTreatment]
      ,[EventsComparator]
      ,[RR]
      ,[DNRR]
      ,[RRLowerBound]
      ,[RRUpperBound]
      ,[DNRRLowerBound]
      ,[DNRRUpperBound]
      ,[LogRR]
      ,[LogRRLowerBound]
      ,[LogRRUpperBound]
      ,[LogDNRR]
      ,[LogDNRRLowerBound]
      ,[LogDNRRUpperBound]
      ,[CalculationMethod]
      ,[CalculationFlags]
      ,[UNII_IndexKey]
      ,[ParameterName_IndexKey]
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
   where TreatmentArm <> ComparatorArm
  and ParameterName is not null
  and PrimaryValueType <> 'Text'
  and RRLowerBound is not null
  and RRUpperBound is not null
  and RR is not null

  Select count(*) as UsableCount 
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where TreatmentArm <> ComparatorArm
  and ParameterName is not null
  and PrimaryValueType <> 'Text'
  and RR is not null

  Select count(unii) as DrugCt
  FROM (select distinct unii from [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where TreatmentArm <> ComparatorArm
  and ParameterName is not null
  and PrimaryValueType <> 'Text'
  and RR is not null) A
