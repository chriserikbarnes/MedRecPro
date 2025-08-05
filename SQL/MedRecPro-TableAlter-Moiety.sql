/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script creates the [dbo].[Moiety] table to support FDA Substance Indexing
*   chemical structure data as defined in the corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[Moiety]:
*       - Creates the table if it doesn't exist with all required columns.
*       - Adds individual columns if table exists but columns are missing (idempotent).
*       - Adds MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - NO foreign key constraints are created (ApplicationDbContext uses reflection).
*   - Supports FDA Substance Registration System and ISO/FDIS 11238 standards.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Create or Modify [dbo].[Moiety] Table
    -- ==========================================================================================
    PRINT 'Creating/Modifying table [dbo].[Moiety]...';

    -- Create table if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'Moiety' AND Schema_ID = Schema_ID(N'dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[Moiety] table.';
        CREATE TABLE [dbo].[Moiety] (
            [MoietyID] INT IDENTITY(1,1) NOT NULL,
            [IdentifiedSubstanceID] INT NULL,
            [MoietyCode] VARCHAR(50) NULL,
            [MoietyCodeSystem] VARCHAR(100) NULL,
            [MoietyDisplayName] VARCHAR(255) NULL,
            [QuantityNumeratorLowValue] DECIMAL(18,6) NULL,
            [QuantityNumeratorUnit] VARCHAR(50) NULL,
            [QuantityNumeratorInclusive] BIT NULL,
            [QuantityDenominatorValue] DECIMAL(18,6) NULL,
            [QuantityDenominatorUnit] VARCHAR(50) NULL,
            CONSTRAINT [PK_Moiety] PRIMARY KEY CLUSTERED ([MoietyID] ASC)
        );
    END
    ELSE
    BEGIN
        PRINT ' -> Table [dbo].[Moiety] already exists. Checking for missing columns...';
        
        -- Add IdentifiedSubstanceID column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'IdentifiedSubstanceID' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [IdentifiedSubstanceID] column.';
            ALTER TABLE [dbo].[Moiety] ADD [IdentifiedSubstanceID] INT NULL;
        END

        -- Add MoietyCode column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'MoietyCode' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [MoietyCode] column.';
            ALTER TABLE [dbo].[Moiety] ADD [MoietyCode] VARCHAR(50) NULL;
        END

        -- Add MoietyCodeSystem column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'MoietyCodeSystem' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [MoietyCodeSystem] column.';
            ALTER TABLE [dbo].[Moiety] ADD [MoietyCodeSystem] VARCHAR(100) NULL;
        END

        -- Add MoietyDisplayName column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'MoietyDisplayName' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [MoietyDisplayName] column.';
            ALTER TABLE [dbo].[Moiety] ADD [MoietyDisplayName] VARCHAR(255) NULL;
        END

        -- Add QuantityNumeratorLowValue column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'QuantityNumeratorLowValue' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [QuantityNumeratorLowValue] column.';
            ALTER TABLE [dbo].[Moiety] ADD [QuantityNumeratorLowValue] DECIMAL(18,6) NULL;
        END

        -- Add QuantityNumeratorUnit column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'QuantityNumeratorUnit' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [QuantityNumeratorUnit] column.';
            ALTER TABLE [dbo].[Moiety] ADD [QuantityNumeratorUnit] VARCHAR(50) NULL;
        END

        -- Add QuantityNumeratorInclusive column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'QuantityNumeratorInclusive' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [QuantityNumeratorInclusive] column.';
            ALTER TABLE [dbo].[Moiety] ADD [QuantityNumeratorInclusive] BIT NULL;
        END

        -- Add QuantityDenominatorValue column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'QuantityDenominatorValue' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [QuantityDenominatorValue] column.';
            ALTER TABLE [dbo].[Moiety] ADD [QuantityDenominatorValue] DECIMAL(18,6) NULL;
        END

        -- Add QuantityDenominatorUnit column if missing
        IF NOT EXISTS (
            SELECT 1 FROM sys.columns 
            WHERE Name = N'QuantityDenominatorUnit' 
              AND Object_ID = Object_ID(N'dbo.Moiety')
        )
        BEGIN
            PRINT ' -> Adding [QuantityDenominatorUnit] column.';
            ALTER TABLE [dbo].[Moiety] ADD [QuantityDenominatorUnit] VARCHAR(50) NULL;
        END
    END

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[Moiety].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'Moiety';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Represents a chemical moiety within an identified substance, containing molecular structure and quantity information that defines part of a substance''s identity. Based on FDA Substance Registration System standards and ISO/FDIS 11238.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: MoietyID
    SET @ColumnName = N'MoietyID';
    SET @PropValue = N'Primary key for the Moiety table.';
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

    -- Column: IdentifiedSubstanceID
    SET @ColumnName = N'IdentifiedSubstanceID';
    SET @PropValue = N'Foreign key to IdentifiedSubstance (The substance this moiety helps define). Links this molecular component to its parent substance record. No database constraint - managed by ApplicationDbContext.';
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

    -- Column: MoietyCode
    SET @ColumnName = N'MoietyCode';
    SET @PropValue = N'Code identifying the type or role of this moiety within the substance definition. Typically indicates whether this is a mixture component or other structural element. Example: "C103243" for "mixture component".';
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

    -- Column: MoietyCodeSystem
    SET @ColumnName = N'MoietyCodeSystem';
    SET @PropValue = N'Code system OID for the moiety code, typically NCI Thesaurus. Standard value: "2.16.840.1.113883.3.26.1.1".';
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

    -- Column: MoietyDisplayName
    SET @ColumnName = N'MoietyDisplayName';
    SET @PropValue = N'Human-readable name for the moiety code. Example: "mixture component".';
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

    -- Column: QuantityNumeratorLowValue
    SET @ColumnName = N'QuantityNumeratorLowValue';
    SET @PropValue = N'Lower bound value for the quantity numerator in mixture ratios. Used to specify ranges or minimum quantities for this moiety component.';
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

    -- Column: QuantityNumeratorUnit
    SET @ColumnName = N'QuantityNumeratorUnit';
    SET @PropValue = N'Unit of measure for the quantity numerator. Typically "1" for dimensionless ratios in mixture specifications.';
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

    -- Column: QuantityNumeratorInclusive
    SET @ColumnName = N'QuantityNumeratorInclusive';
    SET @PropValue = N'Indicates whether the numerator low value boundary is inclusive in range specifications. False typically indicates "greater than" rather than "greater than or equal to".';
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

    -- Column: QuantityDenominatorValue
    SET @ColumnName = N'QuantityDenominatorValue';
    SET @PropValue = N'Denominator value for quantity ratios in mixture specifications. Provides the base for calculating relative proportions of mixture components.';
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

    -- Column: QuantityDenominatorUnit
    SET @ColumnName = N'QuantityDenominatorUnit';
    SET @PropValue = N'Unit of measure for the quantity denominator. Typically "1" for dimensionless ratios in mixture specifications.';
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
    PRINT 'Moiety table created/enhanced for substance indexing support.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO