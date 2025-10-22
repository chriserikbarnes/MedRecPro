/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[Document] table to add the DocumentCodeSystemName column.
*
*   CHANGES:
*   1.  Table [dbo].[Document]:
*       - Adds the [DocumentCodeSystemName] column if it doesn't exist.
*       - Adds or updates MS_Description extended property for the new column.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - New column supports document code system identification.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[Document] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[Document]...';

    -- Add DocumentCodeSystemName column
    PRINT ' -> Adding [DocumentCodeSystemName] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'DocumentCodeSystemName' 
          AND Object_ID = Object_ID(N'dbo.Document')
    )
        ALTER TABLE [dbo].[Document] ADD [DocumentCodeSystemName] NVARCHAR(255) NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[Document].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'Document';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Column: DocumentCodeSystemName
    SET @ColumnName = N'DocumentCodeSystemName';
    SET @PropValue = N'Name of the code system used for document classification. Identifies the coding standard or vocabulary used to categorize and classify the document type or content.';
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
    PRINT 'Document table enhanced with DocumentCodeSystemName column.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO