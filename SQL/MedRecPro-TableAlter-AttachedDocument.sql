/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[AttachedDocument] table to align it with the updated
*   Entity Framework (EF) model. It adds specific foreign key columns (SectionID,
*   ComplianceActionID, ProductID) and fields for REMS metadata, while retaining the
*   existing ParentEntityType and ParentEntityID columns for full backward compatibility.
*
*   CHANGES:
*   1.  Table [dbo].[AttachedDocument]:
*       - Adds [SectionID], [ComplianceActionID], [ProductID], [DocumentIdRoot], [Title],
*         and [TitleReference] columns if they do not already exist.
*       - Retains the existing [ParentEntityType] and [ParentEntityID] columns.
*       - Adds or updates MS_Description extended properties for the table and all columns.
*
*   NOTES:
*   - This script is non-destructive to existing columns and is designed for full
*     backward compatibility.
*   - The script is idempotent and can be run multiple times safely.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[AttachedDocument] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[AttachedDocument]...';

    -- Add new specific foreign key columns and REMS support columns
    PRINT ' -> Adding new columns to [dbo].[AttachedDocument].';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SectionID' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [SectionID] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ComplianceActionID' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [ComplianceActionID] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ProductID' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [ProductID] INT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DocumentIdRoot' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [DocumentIdRoot] VARCHAR(255) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Title' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [Title] VARCHAR(MAX) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TitleReference' AND Object_ID = Object_ID(N'dbo.AttachedDocument'))
        ALTER TABLE [dbo].[AttachedDocument] ADD [TitleReference] VARCHAR(255) NULL;

    -- Add/Update Extended Properties for [dbo].[AttachedDocument]
    PRINT ' -> Updating extended properties for [dbo].[AttachedDocument].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'AttachedDocument';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table: AttachedDocument
    SET @PropValue = N'Stores references to attached documents (e.g., PDFs for Disciplinary Actions, REMS Materials). Based on SPL IG 18.1.7 and 23.2.9.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Column: AttachedDocumentID
    SET @ColumnName = N'AttachedDocumentID'; SET @PropValue = N'Primary key for the AttachedDocument table.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: SectionID
    SET @ColumnName = N'SectionID'; SET @PropValue = N'Foreign key to the Section where this document is referenced. Can be null.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: ComplianceActionID
    SET @ColumnName = N'ComplianceActionID'; SET @PropValue = N'Foreign key to a ComplianceAction, if the document is part of a drug listing or establishment inactivation. Can be null.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: ProductID
    SET @ColumnName = N'ProductID'; SET @PropValue = N'Foreign key to a Product, if the document is related to a specific product (e.g., REMS material). Can be null.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: ParentEntityType
    SET @ColumnName = N'ParentEntityType'; SET @PropValue = N'(Legacy) Identifies the type of the parent element containing the document reference (e.g., "DisciplinaryAction").';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: ParentEntityID
    SET @ColumnName = N'ParentEntityID'; SET @PropValue = N'(Legacy) Foreign key to the parent table (e.g., DisciplinaryActionID).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: MediaType
    SET @ColumnName = N'MediaType'; SET @PropValue = N'MIME type of the attached document (e.g., "application/pdf").';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: FileName
    SET @ColumnName = N'FileName'; SET @PropValue = N'File name of the attached document.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: DocumentIdRoot
    SET @ColumnName = N'DocumentIdRoot'; SET @PropValue = N'The root identifier of the document from the <id> element, required for REMS materials (SPL IG 23.2.9.1).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: Title
    SET @ColumnName = N'Title'; SET @PropValue = N'The title of the document reference (SPL IG 23.2.9.2).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: TitleReference
    SET @ColumnName = N'TitleReference'; SET @PropValue = N'The ID referenced within the document''s title, linking it to content in the section text (SPL IG 23.2.9.3).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    -- Raise the original error to the client
    THROW;
END CATCH;
GO