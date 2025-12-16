USE [MedRecProDB]
GO

/****** Object:  View [dbo].[vw_SectionContent]    Script Date: 12/16/2025 11:51:51 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[vw_SectionContent]
AS
SELECT dbo.[Document].DocumentID, dbo.Section.SectionID, dbo.[Document].DocumentGUID, dbo.[Document].SetGUID, dbo.Section.SectionGUID, dbo.[Document].VersionNumber, dbo.[Document].DocumentDisplayName, dbo.[Document].Title AS DocumentTitle, dbo.Section.SectionCode, dbo.Section.SectionDisplayName, dbo.Section.Title AS SectionTitle, 
         dbo.SectionTextContent.ContentText, dbo.SectionTextContent.SequenceNumber, dbo.SectionTextContent.ContentType, dbo.Section.SectionCodeSystem
FROM  dbo.[Document] INNER JOIN
         dbo.Section ON dbo.[Document].DocumentID = dbo.Section.DocumentID INNER JOIN
         dbo.SectionTextContent ON dbo.Section.SectionID = dbo.SectionTextContent.SectionID
WHERE (dbo.SectionTextContent.ContentText IS NOT NULL) AND ({ fn LENGTH(dbo.SectionTextContent.ContentText) } > 3) AND (dbo.Section.Title IS NOT NULL)
GO

EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
Begin DesignProperties = 
   Begin PaneConfigurations = 
      Begin PaneConfiguration = 0
         NumPanes = 4
         Configuration = "(H (1[32] 4[33] 2[6] 3) )"
      End
      Begin PaneConfiguration = 1
         NumPanes = 3
         Configuration = "(H (1 [50] 4 [25] 3))"
      End
      Begin PaneConfiguration = 2
         NumPanes = 3
         Configuration = "(H (1 [50] 2 [25] 3))"
      End
      Begin PaneConfiguration = 3
         NumPanes = 3
         Configuration = "(H (4 [30] 2 [40] 3))"
      End
      Begin PaneConfiguration = 4
         NumPanes = 2
         Configuration = "(H (1 [56] 3))"
      End
      Begin PaneConfiguration = 5
         NumPanes = 2
         Configuration = "(H (2 [66] 3))"
      End
      Begin PaneConfiguration = 6
         NumPanes = 2
         Configuration = "(H (4 [50] 3))"
      End
      Begin PaneConfiguration = 7
         NumPanes = 1
         Configuration = "(V (3))"
      End
      Begin PaneConfiguration = 8
         NumPanes = 3
         Configuration = "(H (1[56] 4[18] 2) )"
      End
      Begin PaneConfiguration = 9
         NumPanes = 2
         Configuration = "(H (1 [75] 4))"
      End
      Begin PaneConfiguration = 10
         NumPanes = 2
         Configuration = "(H (1[66] 2) )"
      End
      Begin PaneConfiguration = 11
         NumPanes = 2
         Configuration = "(H (4 [60] 2))"
      End
      Begin PaneConfiguration = 12
         NumPanes = 1
         Configuration = "(H (1) )"
      End
      Begin PaneConfiguration = 13
         NumPanes = 1
         Configuration = "(V (4))"
      End
      Begin PaneConfiguration = 14
         NumPanes = 1
         Configuration = "(V (2))"
      End
      ActivePaneConfig = 0
   End
   Begin DiagramPane = 
      Begin Origin = 
         Top = 0
         Left = 0
      End
      Begin Tables = 
         Begin Table = "Document"
            Begin Extent = 
               Top = 12
               Left = 76
               Bottom = 626
               Right = 501
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "Section"
            Begin Extent = 
               Top = 12
               Left = 577
               Bottom = 623
               Right = 969
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "SectionTextContent"
            Begin Extent = 
               Top = 12
               Left = 1045
               Bottom = 621
               Right = 1462
            End
            DisplayFlags = 280
            TopColumn = 0
         End
      End
   End
   Begin SQLPane = 
   End
   Begin DataPane = 
      Begin ParameterDefaults = ""
      End
      Begin ColumnWidths = 15
         Width = 284
         Width = 1500
         Width = 1500
         Width = 3990
         Width = 3225
         Width = 4463
         Width = 780
         Width = 3368
         Width = 1515
         Width = 1658
         Width = 3803
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
      End
   End
   Begin CriteriaPane = 
      Begin ColumnWidths = 11
         Column = 2858
         Alias = 900
         Table = 1170
         Output = 720
         Append = 1400
         NewValue = 1170
         SortType = 1350
         SortOrder = 1410
         GroupBy = 1350
         Filter = 1350
         Or = 1350
         Or = 1350
         Or = 1350
      End
   End
End
' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_SectionContent'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPaneCount', @value=1 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_SectionContent'
GO


