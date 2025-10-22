/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[IngredientSubstance] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[IngredientSubstance]:
*       - Adds the [OriginatingElement] column if it doesn't exist.
*       - Alters existing columns to allow NULLs where required by the EF model.
*       - Adds or updates MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - All columns in the EF model are nullable, so existing columns will be altered 
*     to allow NULLs if they currently don't.
*   - The script is idempotent and can be run multiple times safely.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[IngredientSubstance] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[IngredientSubstance]...';

    -- Add new columns to match the EF model
    PRINT ' -> Adding new columns to [dbo].[IngredientSubstance].';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OriginatingElement' AND Object_ID = Object_ID(N'dbo.IngredientSubstance')) 
        ALTER TABLE [dbo].[IngredientSubstance] ADD [OriginatingElement] VARCHAR(255) NULL;

    -- Alter existing columns to allow NULLs (if they don't already)
    PRINT ' -> Altering existing columns to allow NULLs in [dbo].[IngredientSubstance].';
    
    -- Check and alter UNII to allow NULLs (if not already nullable)
    IF EXISTS (SELECT 1 FROM sys.columns c 
               WHERE c.Name = N'UNII' 
               AND c.Object_ID = Object_ID(N'dbo.IngredientSubstance') 
               AND c.is_nullable = 0)
    BEGIN
        PRINT '   -> Altering column [UNII] to allow NULLs.';
        ALTER TABLE [dbo].[IngredientSubstance] ALTER COLUMN [UNII] VARCHAR(255) NULL;
    END

    -- Check and alter SubstanceName to allow NULLs (if not already nullable)
    IF EXISTS (SELECT 1 FROM sys.columns c 
               WHERE c.Name = N'SubstanceName' 
               AND c.Object_ID = Object_ID(N'dbo.IngredientSubstance') 
               AND c.is_nullable = 0)
    BEGIN
        PRINT '   -> Altering column [SubstanceName] to allow NULLs.';
        ALTER TABLE [dbo].[IngredientSubstance] ALTER COLUMN [SubstanceName] VARCHAR(255) NULL;
    END

    -- Add/Update Extended Properties for [dbo].[IngredientSubstance]
    PRINT ' -> Updating extended properties for [dbo].[IngredientSubstance].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128);
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table: IngredientSubstance
    SET @TableName = N'IngredientSubstance';
    SET @PropValue = N'Stores details about a unique substance (identified primarily by UNII). Based on Section 3.1.4.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Column: IngredientSubstanceID
    SET @ColumnName = N'IngredientSubstanceID'; SET @PropValue = N'Primary key for the IngredientSubstance table.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    
    -- Column: UNII
    SET @ColumnName = N'UNII'; SET @PropValue = N'Unique Ingredient Identifier (<code code=> where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    
    -- Column: SubstanceName
    SET @ColumnName = N'SubstanceName'; SET @PropValue = N'Name of the substance (name).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    
    -- Column: OriginatingElement
    SET @ColumnName = N'OriginatingElement'; SET @PropValue = N'The name of the XML element this ingredient was parsed from (e.g., "inactiveIngredientSubstance").';
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