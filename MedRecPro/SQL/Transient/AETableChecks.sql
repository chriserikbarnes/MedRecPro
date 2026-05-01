SELECT *
  FROM [MedRecLocal].[dbo].[tmp_FlattenedAdverseEventTable]
  where tmp_FlattenedStandardizedTableID in ( SELECT tmp_FlattenedStandardizedTableID
  FROM [dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID = 8006)


  SELECT *
  FROM [dbo].[tmp_FlattenedStandardizedTable]
  where TextTableID = 8006