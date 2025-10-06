/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script creates or modifies the [dbo].[TextTableColumn] table to align it with the 
*   corresponding Entity Framework (EF) model for structured product labeling table support.
*
*   CHANGES:
*   1.  Table [dbo].[TextTableColumn]:
*       - Creates the table if it doesn't exist.
*       - Adds columns for table column definitions per Section 2.2.2.5.
*       - Adds MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - NO foreign key constraints are created (ApplicationDbContext uses reflection).
*   - Supports [col] element attributes for table column formatting and alignment.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Create/Modify [dbo].[TextTableColumn] Table
    -- ==========================================================================================
    PRINT 'Creating/Modifying table [dbo].[TextTableColumn]...';

    -- Create table if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.TextTableColumn') AND type = N'U')
    BEGIN
        PRINT ' -> Creating [dbo].[TextTableColumn] table.';
        CREATE TABLE [dbo].[TextTableColumn] (
            [TextTableColumnID] INT IDENTITY(1,1) NOT NULL,
            [TextTableID] INT NULL,
            [SequenceNumber] INT NULL,
            [Width] NVARCHAR(50) NULL,
            [Align] NVARCHAR(50) NULL,
            [VAlign] NVARCHAR(50) NULL,
            [StyleCode] NVARCHAR(256) NULL,
            CONSTRAINT [PK_TextTableColumn] PRIMARY KEY CLUSTERED ([TextTableColumnID] ASC)
        );
    END
    ELSE
    BEGIN
        PRINT ' -> Table [dbo].[TextTableColumn] already exists. Adding missing columns...';
        
        -- Add TextTableID column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TextTableID' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [TextTableID] INT NULL;

        -- Add SequenceNumber column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'SequenceNumber' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [SequenceNumber] INT NULL;

        -- Add Width column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Width' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [Width] NVARCHAR(50) NULL;

        -- Add Align column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Align' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [Align] NVARCHAR(50) NULL;

        -- Add VAlign column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'VAlign' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [VAlign] NVARCHAR(50) NULL;

        -- Add StyleCode column
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'StyleCode' AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
            ALTER TABLE [dbo].[TextTableColumn] ADD [StyleCode] NVARCHAR(256) NULL;
    END

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[TextTableColumn].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'TextTableColumn';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Stores individual [col] elements within a [table]. Based on Section 2.2.2.5. Column definitions specify default formatting and alignment for table columns.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: TextTableColumnID
    SET @ColumnName = N'TextTableColumnID';
    SET @PropValue = N'Primary key for the TextTableColumn table.';
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

    -- Column: TextTableID
    SET @ColumnName = N'TextTableID';
    SET @PropValue = N'Foreign key to TextTable. No database constraint - managed by ApplicationDbContext.';
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

    -- Column: SequenceNumber
    SET @ColumnName = N'SequenceNumber';
    SET @PropValue = N'Order of the column within the table.';
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

    -- Column: Width
    SET @ColumnName = N'Width';
    SET @PropValue = N'Optional width attribute on [col] element.';
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

    -- Column: Align
    SET @ColumnName = N'Align';
    SET @PropValue = N'Optional align attribute on [col] for horizontal alignment.';
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

    -- Column: VAlign
    SET @ColumnName = N'VAlign';
    SET @PropValue = N'Optional valign attribute on [col] for vertical alignment.';
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

    -- Column: StyleCode
    SET @ColumnName = N'StyleCode';
    SET @PropValue = N'Optional styleCode attribute on [col] for formatting rules.';
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
    PRINT 'TextTableColumn table created/modified for structured product labeling table support.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO