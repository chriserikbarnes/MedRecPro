/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT (Corrected Version 2)
*
*   PURPOSE:
*   This script modifies the [dbo].[SpecifiedSubstance] and [dbo].[Ingredient] tables
*   to align them with their corresponding Entity Framework (EF) models.
*
*   CHANGES:
*   1.  Table [dbo].[SpecifiedSubstance]:
*       - Drops the [IngredientID] column.
*       - Renames the [SubstanceDisplayName] column to [SubstanceCodeSystemName].
*       - Adds or updates MS_Description extended properties for the table and its columns.
*
*   2.  Table [dbo].[Ingredient]:
*       - Adds the [SpecifiedSubstanceID] foreign key column and other new columns.
*       - Alters the [IsConfidential] column to allow NULLs.
*       - Adds or updates MS_Description extended properties for the table and its columns.
*
*   CORRECTIONS FROM PREVIOUS VERSION:
*   - Removed all `GO` batch separators from within the TRY...CATCH block to maintain
*     transaction and variable scope.
*   - Removed the invalid attempt to declare a procedure as a variable. Replaced it with a
*     standard, reliable, and idempotent pattern for adding/updating extended properties.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[SpecifiedSubstance] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[SpecifiedSubstance]...';

    -- Drop the IngredientID column
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'IngredientID' AND Object_ID = Object_ID(N'dbo.SpecifiedSubstance'))
    BEGIN
        PRINT ' -> Dropping column [IngredientID] from [dbo].[SpecifiedSubstance].';
        ALTER TABLE [dbo].[SpecifiedSubstance] DROP COLUMN [IngredientID];
    END

    -- Rename SubstanceDisplayName to SubstanceCodeSystemName
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SubstanceDisplayName' AND Object_ID = Object_ID(N'dbo.SpecifiedSubstance'))
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SubstanceCodeSystemName' AND Object_ID = Object_ID(N'dbo.SpecifiedSubstance'))
    BEGIN
        PRINT ' -> Renaming column [SubstanceDisplayName] to [SubstanceCodeSystemName] in [dbo].[SpecifiedSubstance].';
        EXEC sp_rename 'dbo.SpecifiedSubstance.SubstanceDisplayName', 'SubstanceCodeSystemName', 'COLUMN';
    END

    -- Add/Update Extended Properties for [dbo].[SpecifiedSubstance]
    PRINT ' -> Updating extended properties for [dbo].[SpecifiedSubstance].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128);
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table: SpecifiedSubstance
    SET @TableName = N'SpecifiedSubstance';
    SET @PropValue = N'Stores the specified substance code and name linked to an ingredient in Biologic/Drug Substance Indexing documents. Based on Section 20.2.6.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Column: SpecifiedSubstanceID
    SET @ColumnName = N'SpecifiedSubstanceID'; SET @PropValue = N'Primary key for the SpecifiedSubstance table.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    -- Column: SubstanceCode
    SET @ColumnName = N'SubstanceCode'; SET @PropValue = N'The code assigned to the specified substance.(Atribute code="70097M6I30")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    -- Column: SubstanceCodeSystem
    SET @ColumnName = N'SubstanceCodeSystem'; SET @PropValue = N'Code system for the specified substance code (Atribute codeSystem="2.16.840.1.113883.4.9").';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    -- Column: SubstanceCodeSystemName
    SET @ColumnName = N'SubstanceCodeSystemName'; SET @PropValue = N'Code name for the specified substance code (Atribute codeSystemName="FDA SRS").';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;


    -- ==========================================================================================
    --  Modify [dbo].[Ingredient] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[Ingredient]...';

    -- Add new columns to match the EF model
    PRINT ' -> Adding new columns to [dbo].[Ingredient].';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SpecifiedSubstanceID' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [SpecifiedSubstanceID] INT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'NumeratorTranslationCode' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [NumeratorTranslationCode] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'NumeratorCodeSystem' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [NumeratorCodeSystem] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'NumeratorDisplayName' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [NumeratorDisplayName] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'NumeratorValue' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [NumeratorValue] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DenominatorTranslationCode' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [DenominatorTranslationCode] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DenominatorCodeSystem' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [DenominatorCodeSystem] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DenominatorDisplayName' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [DenominatorDisplayName] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DenominatorValue' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [DenominatorValue] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DisplayName' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [DisplayName] VARCHAR(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OriginatingElement' AND Object_ID = Object_ID(N'dbo.Ingredient')) ALTER TABLE [dbo].[Ingredient] ADD [OriginatingElement] VARCHAR(255) NULL;

    -- Alter IsConfidential to allow NULLs
    PRINT ' -> Altering column [IsConfidential] to allow NULLs in [dbo].[Ingredient].';
    ALTER TABLE [dbo].[Ingredient] ALTER COLUMN [IsConfidential] BIT NULL;

    -- Add/Update Extended Properties for [dbo].[Ingredient]
    PRINT ' -> Updating extended properties for [dbo].[Ingredient].';

    -- Table: Ingredient
    SET @TableName = N'Ingredient';
    SET @PropValue = N'Represents an ingredient instance within a product, part, or product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.4, 15.2.3.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Columns for Ingredient table
    SET @ColumnName = N'IngredientID'; SET @PropValue = N'Primary key for the Ingredient table.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'ProductID'; SET @PropValue = N'Foreign key to Product or Product representing a Part. Null if linked via ProductConceptID.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'IngredientSubstanceID'; SET @PropValue = N'Foreign key to IngredientSubstance.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'SpecifiedSubstanceID'; SET @PropValue = N'Foreign key for the SpecifiedSubstance table.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'ClassCode'; SET @PropValue = N'Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'QuantityNumerator'; SET @PropValue = N'Strength expressed as numerator/denominator value and unit (<quantity>). Null for CNTM unless zero numerator.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'QuantityNumeratorUnit'; SET @PropValue = N'Corresponds to <quantity><numerator unit>.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'NumeratorTranslationCode'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation code="C28253")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'NumeratorCodeSystem'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'NumeratorDisplayName'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'NumeratorValue'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation value="50")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'QuantityDenominator'; SET @PropValue = N'Corresponds to <quantity><denominator value>.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'DenominatorTranslationCode'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation code="C28253")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'DenominatorCodeSystem'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'DenominatorDisplayName'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'DenominatorValue'; SET @PropValue = N'Translation attribute for the numerator (e.g., translation value="50")';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'QuantityDenominatorUnit'; SET @PropValue = N'Corresponds to <quantity><denominator unit>.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'ReferenceSubstanceID'; SET @PropValue = N'Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'IsConfidential'; SET @PropValue = N'Flag indicating if the inactive ingredient information is confidential (<confidentialityCode code="B">).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'SequenceNumber'; SET @PropValue = N'Order of the ingredient as listed in the SPL file (important for cosmetics).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'DisplayName'; SET @PropValue = N'Display name (displayName="MILLIGRAM").';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    SET @ColumnName = N'ProductConceptID'; SET @PropValue = N'FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product. Null if linked via ProductID.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName)) EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName; ELSE EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
     SET @ColumnName = N'OriginatingElement'; SET @PropValue = N' The name of the XML element this ingredient was parsed from (e.g., "ingredient", "activeIngredient").';
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