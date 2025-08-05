/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[Characteristic] table to align it with the 
*   corresponding Entity Framework (EF) model for Substance Indexing support.
*
*   CHANGES:
*   1.  Table [dbo].[Characteristic]:
*       - Adds the [MoietyID] column if it doesn't exist.
*       - Adds the [ValueED_CDATAContent] column if it doesn't exist.
*       - Adds or updates MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - NO foreign key constraints are created (ApplicationDbContext uses reflection).
*   - New columns support FDA Substance Registration System chemical structure data.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[Characteristic] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[Characteristic]...';

    -- Add MoietyID column
    PRINT ' -> Adding [MoietyID] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'MoietyID' 
          AND Object_ID = Object_ID(N'dbo.Characteristic')
    )
        ALTER TABLE [dbo].[Characteristic] ADD [MoietyID] INT NULL;

    -- Add ValueED_CDATAContent column
    PRINT ' -> Adding [ValueED_CDATAContent] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'ValueED_CDATAContent' 
          AND Object_ID = Object_ID(N'dbo.Characteristic')
    )
        ALTER TABLE [dbo].[Characteristic] ADD [ValueED_CDATAContent] NVARCHAR(MAX) NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[Characteristic].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'Characteristic';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Update table description to reflect substance indexing support
    SET @PropValue = N'Stores characteristics of products, packages, or substance moieties ([subjectOf][characteristic]). Enhanced for FDA Substance Indexing to support chemical structure data including MOLFILE format and InChI identifiers per ISO/FDIS 11238.';
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
    SET @PropValue = N'Foreign key to Moiety table (if characteristic applies to a chemical moiety). Used for substance indexing to link chemical structure data to specific molecular components within a substance definition per ISO/FDIS 11238. No database constraint - managed by ApplicationDbContext.';
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

    -- Column: ValueED_CDATAContent
    SET @ColumnName = N'ValueED_CDATAContent';
    SET @PropValue = N'Raw CDATA content for ED type chemical structure characteristics. Contains molecular structure data in format specified by ValueED_MediaType (MOLFILE, InChI, InChI-Key). Preserves exact formatting for scientific integrity per FDA Substance Registration System requirements.';
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

    -- Update existing CharacteristicCode column description to include substance indexing
    SET @ColumnName = N'CharacteristicCode';
    SET @PropValue = N'Code identifying the characteristic property. Traditional: SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS. Enhanced for substance indexing: C103240 for Chemical Structure characteristics.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;

    -- Update existing ValueED_MediaType column description to include chemical structure formats
    SET @ColumnName = N'ValueED_MediaType';
    SET @PropValue = N'Media type for ED type. Enhanced for chemical structure data: application/x-mdl-molfile (MDL MOLFILE format), application/x-inchi (IUPAC International Chemical Identifier), application/x-inchi-key (InChI hash keys).';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';
    PRINT 'Characteristic table enhanced for substance indexing support.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO