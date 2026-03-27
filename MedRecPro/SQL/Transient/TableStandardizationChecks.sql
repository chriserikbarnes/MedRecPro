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
--where ParameterSubtype is not null


SELECT  *
FROM [dbo].[tmp_FlattenedStandardizedTable]


SELECT [ValidationFlags]
FROM [dbo].[tmp_FlattenedStandardizedTable]

Select distinct [TableCategory] from [dbo].[tmp_FlattenedStandardizedTable]