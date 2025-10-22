/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[ObservationMedia] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[ObservationMedia]:
*       - Adds the [XsiType] column if it doesn't exist.
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
    --  Modify [dbo].[ObservationMedia] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[ObservationMedia]...';

    -- Add XsiType column
    PRINT ' -> Adding [XsiType] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'XsiType' 
          AND Object_ID = Object_ID(N'dbo.ObservationMedia')
    )
        ALTER TABLE [dbo].[ObservationMedia] ADD [XsiType] VARCHAR(32) NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[ObservationMedia].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'ObservationMedia';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Stores metadata for images ([observationMedia]). Based on Section 2.2.3.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: XsiType
    SET @ColumnName = N'XsiType';
    SET @PropValue = N'Xsi type of the file ([value xsi:type=]), e.g., "ED".';
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
