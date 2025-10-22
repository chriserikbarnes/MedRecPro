/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[SectionTextContent] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[SectionTextContent]:
*       - Adds the [StyleCode] and [ParentSectionTextContentID] columns if they don't exist.
*       - Adds or updates MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[SectionTextContent] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[SectionTextContent]...';

    -- Add StyleCode column
    PRINT ' -> Adding [StyleCode] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'StyleCode' 
          AND Object_ID = Object_ID(N'dbo.SectionTextContent')
    )
        ALTER TABLE [dbo].[SectionTextContent] ADD [StyleCode] VARCHAR(64) NULL;

    -- Add ParentSectionTextContentID column
    PRINT ' -> Adding [ParentSectionTextContentID] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'ParentSectionTextContentID' 
          AND Object_ID = Object_ID(N'dbo.SectionTextContent')
    )
        ALTER TABLE [dbo].[SectionTextContent] ADD [ParentSectionTextContentID] INT NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[SectionTextContent].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'SectionTextContent';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Textual content of sections within a structured document. Supports style codes for font formatting.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: StyleCode
    SET @ColumnName = N'StyleCode';
    SET @PropValue = N'The values for [styleCode] indicate font effects such as bold, italics, underline, or emphasis to aid accessibility for visually impaired users.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;

    -- Column: ParentSectionTextContentID
    SET @ColumnName = N'ParentSectionTextContentID';
    SET @PropValue = N'Parent SectionTextContent for hierarchy (e.g., a paragraph inside a highlight inside an excerpt)';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO
