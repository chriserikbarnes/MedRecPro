USE MedRecLocal

SELECT  [TextTableID]
	,[DocumentGUID]
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
	,[ParseRule]
	,[FootnoteMarkers]
	,[FootnoteText]
	,[ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]
where ArmN is not null
--where [ParameterCategory] = 'Gastrointestinal Disorders' and ArmN is not null
--order by ParameterName
--where ParameterSubtype is not null

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