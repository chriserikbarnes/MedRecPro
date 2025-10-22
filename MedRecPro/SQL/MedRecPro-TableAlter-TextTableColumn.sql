/***************************************************************
 * Migration Script: Add Colgroup Support to TextTableColumn
 * Purpose: Extends TextTableColumn table to support [colgroup] elements
 *          while maintaining backwards compatibility with existing data.
 * 
 * Based On: SPL Implementation Guide Section 2.2.2.5
 * 
 * Changes:
 * - Adds ColGroupSequenceNumber (nullable) to identify colgroup membership
 * - Adds ColGroupStyleCode (nullable) for colgroup-level formatting rules
 * - Adds ColGroupAlign (nullable) for colgroup-level horizontal alignment
 * - Adds ColGroupVAlign (nullable) for colgroup-level vertical alignment
 * 
 * Backwards Compatibility:
 * - All new columns are nullable, existing rows remain valid with NULL values
 * - Standalone [col] elements will have NULL ColGroupSequenceNumber
 * - No data migration required
 ***************************************************************/

SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Adding Colgroup Support to TextTableColumn';
    PRINT '========================================';
    PRINT '';

    -- Verify table exists before attempting modifications
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TextTableColumn' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT 'ERROR: [dbo].[TextTableColumn] table does not exist.';
        PRINT 'Please ensure the base table is created before applying this migration.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    PRINT ' -> [dbo].[TextTableColumn] table found.';
    PRINT '';

    -- Add new columns if they don't exist
    PRINT ' -> Adding new colgroup-related columns...';

    -- Add ColGroupSequenceNumber column
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = N'ColGroupSequenceNumber' 
                   AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
    BEGIN
        ALTER TABLE [dbo].[TextTableColumn] 
        ADD [ColGroupSequenceNumber] INT NULL;
        PRINT '    - Added column: ColGroupSequenceNumber';
    END
    ELSE
    BEGIN
        PRINT '    - Column already exists: ColGroupSequenceNumber';
    END

    -- Add ColGroupStyleCode column
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = N'ColGroupStyleCode' 
                   AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
    BEGIN
        ALTER TABLE [dbo].[TextTableColumn] 
        ADD [ColGroupStyleCode] NVARCHAR(256) NULL;
        PRINT '    - Added column: ColGroupStyleCode';
    END
    ELSE
    BEGIN
        PRINT '    - Column already exists: ColGroupStyleCode';
    END

    -- Add ColGroupAlign column
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = N'ColGroupAlign' 
                   AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
    BEGIN
        ALTER TABLE [dbo].[TextTableColumn] 
        ADD [ColGroupAlign] NVARCHAR(50) NULL;
        PRINT '    - Added column: ColGroupAlign';
    END
    ELSE
    BEGIN
        PRINT '    - Column already exists: ColGroupAlign';
    END

    -- Add ColGroupVAlign column
    IF NOT EXISTS (SELECT 1 FROM sys.columns 
                   WHERE Name = N'ColGroupVAlign' 
                   AND Object_ID = Object_ID(N'dbo.TextTableColumn'))
    BEGIN
        ALTER TABLE [dbo].[TextTableColumn] 
        ADD [ColGroupVAlign] NVARCHAR(50) NULL;
        PRINT '    - Added column: ColGroupVAlign';
    END
    ELSE
    BEGIN
        PRINT '    - Column already exists: ColGroupVAlign';
    END

    PRINT '';
    PRINT ' -> Updating extended properties for colgroup columns...';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'TextTableColumn';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Update table description to reflect colgroup support
    SET @PropValue = N'Stores individual [col] elements within a [table]. Based on Section 2.2.2.5. Column definitions specify default formatting and alignment for table columns. Supports both standalone [col] elements and [col] elements nested within [colgroup].';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: ColGroupSequenceNumber
    SET @ColumnName = N'ColGroupSequenceNumber';
    SET @PropValue = N'Identifies which colgroup this column belongs to (if any). Null indicates a standalone [col] element not within a [colgroup]. Multiple columns with the same ColGroupSequenceNumber belong to the same [colgroup].';
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

    -- Column: ColGroupStyleCode
    SET @ColumnName = N'ColGroupStyleCode';
    SET @PropValue = N'Optional styleCode attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no styleCode. Individual [col] styleCode attributes take precedence over colgroup-level styles.';
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

    -- Column: ColGroupAlign
    SET @ColumnName = N'ColGroupAlign';
    SET @PropValue = N'Optional align attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no align. Individual [col] align attributes take precedence. Valid values: left, center, right, justify, char.';
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

    -- Column: ColGroupVAlign
    SET @ColumnName = N'ColGroupVAlign';
    SET @PropValue = N'Optional valign attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no valign. Individual [col] valign attributes take precedence.';
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

    PRINT '    - Extended properties updated successfully.';
    PRINT '';

    PRINT '';
    COMMIT TRANSACTION;
    
    PRINT '========================================';
    PRINT 'Migration completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT '  - Added 4 new nullable columns for colgroup support';
    PRINT '  - Updated extended properties/documentation';
    PRINT '';
    PRINT 'Next: Creating performance index...';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '========================================';
    PRINT 'ERROR: Migration failed!';
    PRINT '========================================';
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    PRINT 'Transaction rolled back.';
    
    THROW;
END CATCH;
GO

/*********************************************************************
 * PART 2: Create Performance Index (Separate Batch)
 * This must run after the columns are committed to the database
 *********************************************************************/
BEGIN TRY
    PRINT '';
    PRINT '========================================';
    PRINT 'Creating Performance Index';
    PRINT '========================================';

    -- Create index for colgroup queries (optional but recommended for performance)
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'IX_TextTableColumn_ColGroupSequenceNumber' 
                   AND object_id = OBJECT_ID('dbo.TextTableColumn'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_TextTableColumn_ColGroupSequenceNumber]
        ON [dbo].[TextTableColumn] ([TextTableID], [ColGroupSequenceNumber])
        INCLUDE ([SequenceNumber])
        WHERE [ColGroupSequenceNumber] IS NOT NULL;
        
        PRINT ' -> Created index: IX_TextTableColumn_ColGroupSequenceNumber';
        PRINT '    (Optimizes queries for columns within colgroups)';
    END
    ELSE
    BEGIN
        PRINT ' -> Index already exists: IX_TextTableColumn_ColGroupSequenceNumber';
    END
    
    PRINT '';
    PRINT '========================================';
    PRINT 'Index creation completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT '========================================';
    PRINT 'COMPLETE SUCCESS!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT '  - Added 4 new nullable columns for colgroup support';
    PRINT '  - Updated extended properties/documentation';
    PRINT '  - Created performance index for colgroup queries';
    PRINT '  - All existing data remains valid (backwards compatible)';
    PRINT '';
    PRINT 'Next Steps:';
    PRINT '  - Update parsing logic to populate colgroup fields';
    PRINT '  - Update rendering templates to use GetEffective* methods';
    PRINT '  - Test with both standalone [col] and [colgroup] structures';

END TRY
BEGIN CATCH
    PRINT '';
    PRINT '========================================';
    PRINT 'WARNING: Index creation failed!';
    PRINT '========================================';
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT '';
    PRINT 'Note: Column creation was successful.';
    PRINT 'The index can be created manually later if needed.';
    PRINT 'The application will work without the index (with slightly reduced performance).';
END CATCH;
GO