/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[Section] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[Section]:
*       - Adds the [SectionLinkGUID] column if it doesn't exist.
*       - Adds the [EffectiveTimeLow] column if it doesn't exist.
*       - Adds the [EffectiveTimeHigh] column if it doesn't exist.
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
    --  Modify [dbo].[Section] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[Section]...';

    -- Add SectionLinkGUID column
    PRINT ' -> Adding [SectionLinkGUID] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'SectionLinkGUID' 
          AND Object_ID = Object_ID(N'dbo.Section')
    )
        ALTER TABLE [dbo].[Section] ADD [SectionLinkGUID] NVARCHAR(255) NULL;

    -- Add EffectiveTimeLow column
    PRINT ' -> Adding [EffectiveTimeLow] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'EffectiveTimeLow' 
          AND Object_ID = Object_ID(N'dbo.Section')
    )
        ALTER TABLE [dbo].[Section] ADD [EffectiveTimeLow] DATETIME NULL;

    -- Add EffectiveTimeHigh column
    PRINT ' -> Adding [EffectiveTimeHigh] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'EffectiveTimeHigh' 
          AND Object_ID = Object_ID(N'dbo.Section')
    )
        ALTER TABLE [dbo].[Section] ADD [EffectiveTimeHigh] DATETIME NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[Section].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'Section';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Sections of the document that group related text and metadata such as titles and codes.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Add SectionCodeSystemName column
PRINT ' -> Adding [SectionCodeSystemName] column if not exists.';
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE Name = N'SectionCodeSystemName' 
      AND Object_ID = Object_ID(N'dbo.Section')
)
    ALTER TABLE [dbo].[Section] ADD [SectionCodeSystemName] NVARCHAR(255) NULL;

    -- Column: SectionCodeSystemName
    SET @ColumnName = N'SectionCodeSystemName';
    SET @PropValue = N'LOINC code name for the section type ([code] codeSystemName).';
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

    -- Column: SectionLinkGUID
    SET @ColumnName = N'SectionLinkGUID';
    SET @PropValue = N'Attribute identifying the section link ([section][ID]), used for cross-references within the document e.g. [section ID="ID_1dc7080f-1d52-4bf7-b353-3c13ec291810"].';
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

    -- Column: EffectiveTimeLow
    SET @ColumnName = N'EffectiveTimeLow';
    SET @PropValue = N'Low boundary of the effective time period for the section ([effectiveTime][low value]). Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.';
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

    -- Column: EffectiveTimeHigh
    SET @ColumnName = N'EffectiveTimeHigh';
    SET @PropValue = N'High boundary of the effective time period for the section ([effectiveTime][high value]). Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.';
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