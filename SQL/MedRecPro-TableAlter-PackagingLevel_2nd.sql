/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[PackagingLevel] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[PackagingLevel]:
*       - Adds the [PackageCode] and [PackageCodeSystem] columns if they don't exist.
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
    --  Modify [dbo].[PackagingLevel] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[PackagingLevel]...';

    -- Add new columns to match the EF model
    PRINT ' -> Adding new columns to [dbo].[PackagingLevel].';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PackageCode' AND Object_ID = Object_ID(N'dbo.PackagingLevel')) 
        ALTER TABLE [dbo].[PackagingLevel] ADD [PackageCode] VARCHAR(64) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PackageCodeSystem' AND Object_ID = Object_ID(N'dbo.PackagingLevel')) 
        ALTER TABLE [dbo].[PackagingLevel] ADD [PackageCodeSystem] VARCHAR(64) NULL;

    -- Add/Update Extended Properties for [dbo].[PackagingLevel]
    PRINT ' -> Updating extended properties for [dbo].[PackagingLevel].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128);
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table: PackagingLevel
    SET @TableName = N'PackagingLevel';
    SET @PropValue = N'Represents a level of packaging (asContent/containerPackagedProduct). Links to ProductID/PartProductID for definitions OR ProductInstanceID for lot distribution container data.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Column: PackageCode
    SET @ColumnName = N'PackageCode';
    SET @PropValue = N'The package item code value (<containerPackagedProduct><code code=.../>). For example, NDC package code or other item code for the package.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: PackageCodeSystem
    SET @ColumnName = N'PackageCodeSystem';
    SET @PropValue = N'The code system OID for the package item code (<containerPackagedProduct><code codeSystem=.../>).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

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
