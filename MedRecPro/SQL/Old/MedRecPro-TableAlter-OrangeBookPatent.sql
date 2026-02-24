/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   Widens [dbo].[OrangeBookPatent].[PatentNo] from VARCHAR(11) to VARCHAR(17) to accommodate
*   FDA Orange Book patent numbers with appended exclusivity code suffixes.
*
*   BACKGROUND:
*   The FDA patent.txt file includes patent numbers with exclusivity code suffixes such as
*   *PED (pediatric), *ODE (orphan drug), *NCE (new chemical entity), *GAIN (antibiotic),
*   *PC (patent challenge), *CGT (competitive generic therapy), and others.
*   Example: "11931377*PED" (12 chars). The original VARCHAR(11) column truncated these at
*   "11931377*PE", causing import failures. VARCHAR(17) accommodates the longest possible
*   combination: 8-digit patent + "*" + 4-char code = 13 today, with headroom for future
*   9-digit patents + longest suffix (*GAIN = 5 chars) = 15, plus buffer.
*
*   CHANGES:
*   1.  Drops 3 nonclustered indexes that reference [PatentNo] (required before ALTER COLUMN).
*   2.  Widens [PatentNo] from VARCHAR(11) to VARCHAR(17).
*   3.  Recreates all 3 indexes with identical definitions.
*   4.  Updates the MS_Description extended property on the column.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - If the column is already VARCHAR(17) or wider, the ALTER is still safe (no-op behavior).
*   - Existing data is preserved — widening a VARCHAR column does not modify stored values.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Step 1: Drop indexes that reference [PatentNo]
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[OrangeBookPatent]...';
    PRINT '';

    -- Drop IX_OrangeBookPatent_PatentNo (PatentNo is the key column)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_PatentNo' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        DROP INDEX [IX_OrangeBookPatent_PatentNo] ON [dbo].[OrangeBookPatent];
        PRINT ' -> Dropped index: IX_OrangeBookPatent_PatentNo';
    END

    -- Drop IX_OrangeBookPatent_OrangeBookProductID (PatentNo is in INCLUDE)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_OrangeBookProductID' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        DROP INDEX [IX_OrangeBookPatent_OrangeBookProductID] ON [dbo].[OrangeBookPatent];
        PRINT ' -> Dropped index: IX_OrangeBookPatent_OrangeBookProductID';
    END

    -- Drop IX_OrangeBookPatent_ApplType_ApplNo_ProductNo (PatentNo is in INCLUDE)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_ApplType_ApplNo_ProductNo' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        DROP INDEX [IX_OrangeBookPatent_ApplType_ApplNo_ProductNo] ON [dbo].[OrangeBookPatent];
        PRINT ' -> Dropped index: IX_OrangeBookPatent_ApplType_ApplNo_ProductNo';
    END

    PRINT '';

    -- ==========================================================================================
    --  Step 2: Widen [PatentNo] from VARCHAR(11) to VARCHAR(17)
    -- ==========================================================================================
    PRINT ' -> Altering column [PatentNo] from VARCHAR(11) to VARCHAR(17)...';

    ALTER TABLE [dbo].[OrangeBookPatent]
        ALTER COLUMN [PatentNo] VARCHAR(17) NOT NULL;

    PRINT '    - Column altered successfully.';
    PRINT '';

    -- ==========================================================================================
    --  Step 3: Recreate indexes
    -- ==========================================================================================
    PRINT ' -> Recreating indexes...';

    -- Product lookup (filtered)
    CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_OrangeBookProductID]
        ON [dbo].[OrangeBookPatent]([OrangeBookProductID])
        INCLUDE ([PatentNo], [PatentExpireDate])
        WHERE [OrangeBookProductID] IS NOT NULL;
    PRINT '    - Created index: IX_OrangeBookPatent_OrangeBookProductID';

    -- Import matching via natural key
    CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_ApplType_ApplNo_ProductNo]
        ON [dbo].[OrangeBookPatent]([ApplType], [ApplNo], [ProductNo])
        INCLUDE ([PatentNo]);
    PRINT '    - Created index: IX_OrangeBookPatent_ApplType_ApplNo_ProductNo';

    -- Patent number search
    CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_PatentNo]
        ON [dbo].[OrangeBookPatent]([PatentNo])
        INCLUDE ([OrangeBookProductID], [PatentExpireDate]);
    PRINT '    - Created index: IX_OrangeBookPatent_PatentNo';

    PRINT '';

    -- ==========================================================================================
    --  Step 4: Update extended property on [PatentNo]
    -- ==========================================================================================
    PRINT ' -> Updating extended property for [PatentNo]...';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'OrangeBookPatent';
    DECLARE @ColumnName NVARCHAR(128) = N'PatentNo';
    DECLARE @PropValue SQL_VARIANT = N'U.S. patent number. Typically 7-8 digits but may include an exclusivity code suffix (e.g., "11931377*PED", "10300065*GAIN"). VARCHAR(17) accommodates all known suffix types (*NCE, *ODE, *PED, *GAIN, *PC, *CGT, etc.) with headroom for future patent number growth.';

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

    PRINT '    - Extended property updated.';

    COMMIT TRANSACTION;
    PRINT '';
    PRINT 'Script completed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO
