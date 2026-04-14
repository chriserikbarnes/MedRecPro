SELECT Count(*) As TCount FROM (SELECT distinct dbo.vw_ActiveIngredients.UNII
	,dbo.TextTableCell.TextTableCellID
	,dbo.TextTableCell.CellType
	,dbo.TextTableCell.CellText
	,dbo.TextTableCell.SequenceNumber
	,dbo.TextTableCell.RowSpan
	,dbo.TextTableCell.ColSpan
	,dbo.TextTableRow.TextTableRowID
	,dbo.TextTableRow.RowGroupType
	,dbo.TextTableRow.SequenceNumber AS SequenceNumberTextTableRow
	,dbo.TextTable.TextTableID
	,dbo.TextTable.SectionTextContentID
	,dbo.TextTable.Caption
	,dbo.SectionTextContent.ContentType
	,dbo.SectionTextContent.SequenceNumber AS SequenceNumberSectionTextContent
	,dbo.SectionTextContent.ContentText
	,dbo.[Document].DocumentGUID
	,dbo.vw_SectionNavigation.SectionGUID
	,dbo.vw_SectionNavigation.SectionCode
	,dbo.vw_SectionNavigation.SectionType
	,dbo.vw_SectionNavigation.SectionTitle
	,dbo.vw_SectionNavigation.ParentSectionID
	,dbo.vw_SectionNavigation.ParentSectionCode
	,dbo.vw_SectionNavigation.ParentSectionTitle
	,dbo.vw_SectionNavigation.LabelerName
	,dbo.[Document].Title
	,dbo.[Document].VersionNumber
FROM dbo.TextTable
INNER JOIN dbo.SectionTextContent ON dbo.TextTable.SectionTextContentID = dbo.SectionTextContent.SectionTextContentID
INNER JOIN dbo.TextTableRow ON dbo.TextTable.TextTableID = dbo.TextTableRow.TextTableID
INNER JOIN dbo.TextTableCell ON dbo.TextTableRow.TextTableRowID = dbo.TextTableCell.TextTableRowID
INNER JOIN dbo.vw_SectionNavigation ON dbo.SectionTextContent.SectionID = dbo.vw_SectionNavigation.SectionID
INNER JOIN dbo.[Document] ON dbo.vw_SectionNavigation.DocumentID = dbo.[Document].DocumentID
INNER JOIN dbo.vw_ActiveIngredients ON dbo.[Document].DocumentID = dbo.vw_ActiveIngredients.DocumentID) A

SELECT Count(*) As TCount FROM (SELECT dbo.TextTableCell.TextTableCellID
	,dbo.TextTableCell.CellType
	,dbo.TextTableCell.CellText
	,dbo.TextTableCell.SequenceNumber
	,dbo.TextTableCell.RowSpan
	,dbo.TextTableCell.ColSpan
	,dbo.TextTableRow.TextTableRowID
	,dbo.TextTableRow.RowGroupType
	,dbo.TextTableRow.SequenceNumber AS SequenceNumberTextTableRow
	,dbo.TextTable.TextTableID
	,dbo.TextTable.SectionTextContentID
	,dbo.TextTable.Caption
	,dbo.SectionTextContent.ContentType
	,dbo.SectionTextContent.SequenceNumber AS SequenceNumberSectionTextContent
	,dbo.SectionTextContent.ContentText
	,dbo.[Document].DocumentGUID
	,dbo.vw_SectionNavigation.SectionGUID
	,dbo.vw_SectionNavigation.SectionCode
	,dbo.vw_SectionNavigation.SectionType
	,dbo.vw_SectionNavigation.SectionTitle
	,dbo.vw_SectionNavigation.ParentSectionID
	,dbo.vw_SectionNavigation.ParentSectionCode
	,dbo.vw_SectionNavigation.ParentSectionTitle
	,dbo.vw_SectionNavigation.LabelerName
	,dbo.[Document].Title
	,dbo.[Document].VersionNumber
FROM dbo.TextTable
INNER JOIN dbo.SectionTextContent ON dbo.TextTable.SectionTextContentID = dbo.SectionTextContent.SectionTextContentID
INNER JOIN dbo.TextTableRow ON dbo.TextTable.TextTableID = dbo.TextTableRow.TextTableID
INNER JOIN dbo.TextTableCell ON dbo.TextTableRow.TextTableRowID = dbo.TextTableCell.TextTableRowID
INNER JOIN dbo.vw_SectionNavigation ON dbo.SectionTextContent.SectionID = dbo.vw_SectionNavigation.SectionID
INNER JOIN dbo.[Document] ON dbo.vw_SectionNavigation.DocumentID = dbo.[Document].DocumentID) A