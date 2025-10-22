/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script adds a DocumentID column to the [dbo].[ObservationMedia] and [dbo].[RenderedMedia] 
*   tables to support document associations.
*
*   CHANGES:
*   1.  Table [dbo].[ObservationMedia]:
*       - Adds DocumentID INT NULL column if it doesn't exist.
*       - Adds MS_Description extended property for the new column.
*
*   2.  Table [dbo].[RenderedMedia]:
*       - Adds DocumentID INT NULL column if it doesn't exist.
*       - Adds MS_Description extended property for the new column.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - NO foreign key constraints are created (ApplicationDbContext uses reflection).
*   - DocumentID is nullable to support existing records without documents.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Add DocumentID Column to [dbo].[ObservationMedia] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[ObservationMedia]...';

    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.ObservationMedia') AND type = N'U')
    BEGIN
        -- Add DocumentID column if it doesn't exist
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DocumentID' AND Object_ID = Object_ID(N'dbo.ObservationMedia'))
        BEGIN
            PRINT ' -> Adding [DocumentID] column to [dbo].[ObservationMedia].';
            ALTER TABLE [dbo].[ObservationMedia] ADD [DocumentID] INT NULL;
        END
        ELSE
        BEGIN
            PRINT ' -> Column [DocumentID] already exists in [dbo].[ObservationMedia].';
        END

        -- Add/Update Extended Property for DocumentID column
        PRINT ' -> Updating extended property for [DocumentID] in [dbo].[ObservationMedia].';
        
        DECLARE @SchemaName1 NVARCHAR(128) = N'dbo';
        DECLARE @TableName1 NVARCHAR(128) = N'ObservationMedia';
        DECLARE @ColumnName1 NVARCHAR(128) = N'DocumentID';
        DECLARE @PropValue1 SQL_VARIANT = N'Foreign key to Document. No database constraint - managed by ApplicationDbContext.';

        IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName1, 'TABLE', @TableName1, 'COLUMN', @ColumnName1))
            EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue1,
                @level0type = N'SCHEMA', @level0name = @SchemaName1,
                @level1type = N'TABLE', @level1name = @TableName1,
                @level2type = N'COLUMN', @level2name = @ColumnName1;
        ELSE
            EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue1,
                @level0type = N'SCHEMA', @level0name = @SchemaName1,
                @level1type = N'TABLE', @level1name = @TableName1,
                @level2type = N'COLUMN', @level2name = @ColumnName1;
    END
    ELSE
    BEGIN
        PRINT ' -> WARNING: Table [dbo].[ObservationMedia] does not exist. Skipping...';
    END

    -- ==========================================================================================
    --  Add DocumentID Column to [dbo].[RenderedMedia] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[RenderedMedia]...';

    IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.RenderedMedia') AND type = N'U')
    BEGIN
        -- Add DocumentID column if it doesn't exist
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DocumentID' AND Object_ID = Object_ID(N'dbo.RenderedMedia'))
        BEGIN
            PRINT ' -> Adding [DocumentID] column to [dbo].[RenderedMedia].';
            ALTER TABLE [dbo].[RenderedMedia] ADD [DocumentID] INT NULL;
        END
        ELSE
        BEGIN
            PRINT ' -> Column [DocumentID] already exists in [dbo].[RenderedMedia].';
        END

        -- Add/Update Extended Property for DocumentID column
        PRINT ' -> Updating extended property for [DocumentID] in [dbo].[RenderedMedia].';
        
        DECLARE @SchemaName2 NVARCHAR(128) = N'dbo';
        DECLARE @TableName2 NVARCHAR(128) = N'RenderedMedia';
        DECLARE @ColumnName2 NVARCHAR(128) = N'DocumentID';
        DECLARE @PropValue2 SQL_VARIANT = N'Foreign key to Document. No database constraint - managed by ApplicationDbContext.';

        IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName2, 'TABLE', @TableName2, 'COLUMN', @ColumnName2))
            EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue2,
                @level0type = N'SCHEMA', @level0name = @SchemaName2,
                @level1type = N'TABLE', @level1name = @TableName2,
                @level2type = N'COLUMN', @level2name = @ColumnName2;
        ELSE
            EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue2,
                @level0type = N'SCHEMA', @level0name = @SchemaName2,
                @level1type = N'TABLE', @level1name = @TableName2,
                @level2type = N'COLUMN', @level2name = @ColumnName2;
    END
    ELSE
    BEGIN
        PRINT ' -> WARNING: Table [dbo].[RenderedMedia] does not exist. Skipping...';
    END

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';
    PRINT 'DocumentID column added to ObservationMedia and RenderedMedia tables.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO