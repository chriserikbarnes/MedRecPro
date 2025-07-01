/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT: Add QuantityDenominator to PackagingLevel
*
*   PURPOSE:
*   Adds a new decimal column [QuantityDenominator] to [dbo].[PackagingLevel] to record the denominator
*   for packaging quantity (e.g., for <quantity><denominator> in SPL).
*
*   CHANGES:
*   1.  Table [dbo].[PackagingLevel]:
*       - Adds [QuantityDenominator] as decimal(18,6) NULL.
*       - Adds or updates MS_Description extended property for the new column.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[PackagingLevel] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[PackagingLevel]...';

    -- Add [QuantityDenominator] if it does not already exist
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'QuantityDenominator' AND Object_ID = Object_ID(N'dbo.PackagingLevel')
    )
    BEGIN
        PRINT ' -> Adding column [QuantityDenominator] (decimal(18,6)) to [dbo].[PackagingLevel].';
        ALTER TABLE [dbo].[PackagingLevel] ADD [QuantityDenominator] decimal(18,6) NULL;
    END
    ELSE
    BEGIN
        PRINT ' -> Column [QuantityDenominator] already exists on [dbo].[PackagingLevel], skipping add.';
    END

    -- Add/Update Extended Property for [QuantityDenominator]
    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'PackagingLevel';
    DECLARE @ColumnName NVARCHAR(128) = N'QuantityDenominator';
    DECLARE @PropValue SQL_VARIANT = N'Corresponds to <quantity><denominator value> for packaging units.';

    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName
        )
    )
        EXEC sp_updateextendedproperty 
            @name = N'MS_Description', 
            @value = @PropValue, 
            @level0type = N'SCHEMA', @level0name = @SchemaName, 
            @level1type = N'TABLE', @level1name = @TableName, 
            @level2type = N'COLUMN', @level2name = @ColumnName;
    ELSE
        EXEC sp_addextendedproperty 
            @name = N'MS_Description', 
            @value = @PropValue, 
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
