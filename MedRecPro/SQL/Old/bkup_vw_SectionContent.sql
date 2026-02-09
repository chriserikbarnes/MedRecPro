USE [MedRecLocal]
GO

/****** Object:  View [dbo].[vw_SectionContent]    Script Date: 2/9/2026 8:38:58 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


ALTER VIEW [dbo].[vw_SectionContent]
AS
SELECT
    dbo.[Document].DocumentID,
    dbo.Section.SectionID,
    dbo.[Document].DocumentGUID,
    dbo.[Document].SetGUID,
    dbo.Section.SectionGUID,
    dbo.[Document].VersionNumber,
    dbo.[Document].DocumentDisplayName,
    dbo.[Document].Title AS DocumentTitle,
    dbo.Section.SectionCode,
    dbo.Section.SectionDisplayName,
    dbo.Section.Title AS SectionTitle,
    dbo.SectionTextContent.ContentText,
    dbo.SectionTextContent.SequenceNumber,
    dbo.SectionTextContent.ContentType,
    dbo.Section.SectionCodeSystem
FROM dbo.[Document]
    INNER JOIN dbo.Section ON dbo.[Document].DocumentID = dbo.Section.DocumentID
    INNER JOIN dbo.SectionTextContent ON dbo.Section.SectionID = dbo.SectionTextContent.SectionID
WHERE (dbo.SectionTextContent.ContentText IS NOT NULL)
    AND (LEN(dbo.SectionTextContent.ContentText) > 3)
    AND (dbo.Section.Title IS NOT NULL)

GO


