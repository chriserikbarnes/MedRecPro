USE [MedRecProDB]
GO

/****** Object:  View [dbo].[vw_IngredientActiveSummary]    Script Date: 12/15/2025 11:52:47 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW [dbo].[vw_IngredientActiveSummary]
AS
SELECT ins.IngredientSubstanceID, ins.UNII, ins.SubstanceName, ins.OriginatingElement AS IngredientType, COUNT(DISTINCT i.ProductID) AS ProductCount, COUNT(DISTINCT d.DocumentID) AS DocumentCount, COUNT(DISTINCT o.OrganizationID) AS LabelerCount
FROM  dbo.IngredientSubstance AS ins LEFT OUTER JOIN
         dbo.Ingredient AS i ON ins.IngredientSubstanceID = i.IngredientSubstanceID LEFT OUTER JOIN
         dbo.Product AS p ON i.ProductID = p.ProductID LEFT OUTER JOIN
         dbo.Section AS s ON p.SectionID = s.SectionID LEFT OUTER JOIN
         dbo.StructuredBody AS sb ON s.StructuredBodyID = sb.StructuredBodyID LEFT OUTER JOIN
         dbo.[Document] AS d ON sb.DocumentID = d.DocumentID LEFT OUTER JOIN
         dbo.DocumentAuthor AS da ON d.DocumentID = da.DocumentID AND da.AuthorType = 'Labeler' LEFT OUTER JOIN
         dbo.Organization AS o ON da.OrganizationID = o.OrganizationID
GROUP BY ins.IngredientSubstanceID, ins.UNII, ins.SubstanceName, ins.OriginatingElement, i.ClassCode
HAVING (i.ClassCode <> 'IACT')
GO

IF NOT EXISTS (SELECT * FROM sys.fn_listextendedproperty(N'MS_DiagramPane1' , N'SCHEMA',N'dbo', N'VIEW',N'vw_IngredientActiveSummary', NULL,NULL))
	EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
Begin DesignProperties = 
   Begin PaneConfigurations = 
      Begin PaneConfiguration = 0
         NumPanes = 4
         Configuration = "(H (1[40] 4[20] 2[20] 3) )"
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
         Begin Table = "ins"
            Begin Extent = 
               Top = 12
               Left = 76
               Bottom = 259
               Right = 450
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "i"
            Begin Extent = 
               Top = 12
               Left = 526
               Bottom = 259
               Right = 972
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "p"
            Begin Extent = 
               Top = 12
               Left = 1048
               Bottom = 259
               Right = 1376
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "s"
            Begin Extent = 
               Top = 264
               Left = 76
               Bottom = 511
               Right = 484
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "sb"
            Begin Extent = 
               Top = 264
               Left = 560
               Bottom = 443
               Right = 880
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "d"
            Begin Extent = 
               Top = 264
               Left = 956
               Bottom = 511
               Right = 1397
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "da"
            Begin Extent = 
               Top = 444
               Left = 560
               Bottom = 691
               Right = 900
            End
            DisplayFlags = 280
            TopColumn = 0
     ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
ELSE
BEGIN
	EXEC sys.sp_updateextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
Begin DesignProperties = 
   Begin PaneConfigurations = 
      Begin PaneConfiguration = 0
         NumPanes = 4
         Configuration = "(H (1[40] 4[20] 2[20] 3) )"
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
         Begin Table = "ins"
            Begin Extent = 
               Top = 12
               Left = 76
               Bottom = 259
               Right = 450
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "i"
            Begin Extent = 
               Top = 12
               Left = 526
               Bottom = 259
               Right = 972
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "p"
            Begin Extent = 
               Top = 12
               Left = 1048
               Bottom = 259
               Right = 1376
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "s"
            Begin Extent = 
               Top = 264
               Left = 76
               Bottom = 511
               Right = 484
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "sb"
            Begin Extent = 
               Top = 264
               Left = 560
               Bottom = 443
               Right = 880
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "d"
            Begin Extent = 
               Top = 264
               Left = 956
               Bottom = 511
               Right = 1397
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "da"
            Begin Extent = 
               Top = 444
               Left = 560
               Bottom = 691
               Right = 900
            End
            DisplayFlags = 280
            TopColumn = 0
     ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
END
GO

IF NOT EXISTS (SELECT * FROM sys.fn_listextendedproperty(N'MS_DiagramPane2' , N'SCHEMA',N'dbo', N'VIEW',N'vw_IngredientActiveSummary', NULL,NULL))
	EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane2', @value=N'    End
         Begin Table = "o"
            Begin Extent = 
               Top = 516
               Left = 76
               Bottom = 729
               Right = 409
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
   End
   Begin CriteriaPane = 
      Begin ColumnWidths = 12
         Column = 1440
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
' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
ELSE
BEGIN
	EXEC sys.sp_updateextendedproperty @name=N'MS_DiagramPane2', @value=N'    End
         Begin Table = "o"
            Begin Extent = 
               Top = 516
               Left = 76
               Bottom = 729
               Right = 409
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
   End
   Begin CriteriaPane = 
      Begin ColumnWidths = 12
         Column = 1440
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
' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
END
GO

IF NOT EXISTS (SELECT * FROM sys.fn_listextendedproperty(N'MS_DiagramPaneCount' , N'SCHEMA',N'dbo', N'VIEW',N'vw_IngredientActiveSummary', NULL,NULL))
	EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPaneCount', @value=2 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
ELSE
BEGIN
	EXEC sys.sp_updateextendedproperty @name=N'MS_DiagramPaneCount', @value=2 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'vw_IngredientActiveSummary'
END
GO


