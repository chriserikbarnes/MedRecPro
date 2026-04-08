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

SELECT  [ParameterName]
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
where TextTableID = 185


SELECT  *
FROM [dbo].[tmp_FlattenedStandardizedTable]
where TextTableID = 86


SELECT [ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]

Select distinct [TableCategory] from [dbo].[tmp_FlattenedStandardizedTable]