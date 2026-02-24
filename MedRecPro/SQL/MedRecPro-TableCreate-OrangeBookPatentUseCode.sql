/***************************************************************
 * Migration Script: Create Orange Book Patent Use Code Lookup Table
 * Purpose: Creates a lookup table for FDA Orange Book patent use
 *          code definitions. The patent.txt file in the Orange Book
 *          ZIP contains use code values (e.g., "U-141") but NOT their
 *          definitions. Definitions are published separately on the
 *          FDA website and are maintained as an embedded JSON resource
 *          in the MedRecProImportClass project.
 *
 * Tables Created:
 * - OrangeBookPatentUseCode   Lookup for patent use code definitions
 *
 * Design:
 * - Uses PatentUseCode as the natural primary key (no surrogate ID)
 * - Small, long-lived dataset (~4,400 rows) populated from embedded JSON
 * - No foreign key constraints (OrangeBookPatent.PatentUseCode stores
 *   the code value directly, not a FK ID)
 *
 * Dependencies:
 * - None (standalone lookup table)
 *
 * Backwards Compatibility:
 * - New table only, no impact on existing tables
 ***************************************************************/

SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Creating Orange Book Patent Use Code Table';
    PRINT '========================================';
    PRINT '';

    ---------------------------------------------------------------------------
    -- 1. OrangeBookPatentUseCode
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookPatentUseCode' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookPatentUseCode] table...';

        CREATE TABLE [dbo].[OrangeBookPatentUseCode] (
            -- Natural Primary Key
            [PatentUseCode] VARCHAR(6)    NOT NULL,

            -- Definition
            [Definition]    VARCHAR(1000) NOT NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookPatentUseCode] PRIMARY KEY CLUSTERED ([PatentUseCode] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookPatentUseCode]';
        PRINT '    - Skipping table creation.';
    END

    -- Extended properties for OrangeBookPatentUseCode
    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'OrangeBookPatentUseCode';
    DECLARE @PropValue SQL_VARIANT;

    SET @PropValue = N'Lookup table for FDA Orange Book patent use code definitions. Maps each Patent_Use_Code value (e.g., "U-141") to its human-readable description of the approved indication covered by the patent. Populated from an embedded JSON resource in MedRecProImportClass. Data sourced from the FDA Orange Book website Patent Use Codes and Definitions page.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DECLARE @ColumnDescriptions TABLE (
        ColumnName NVARCHAR(128),
        Description NVARCHAR(500)
    );

    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'PatentUseCode', N'Patent use code identifier (e.g., "U-1", "U-141", "U-4412"). Serves as the natural primary key. Matches Patent_Use_Code values in patent.txt and the OrangeBookPatent.PatentUseCode column.'),
        (N'Definition', N'Human-readable description of the approved indication or method of use covered by the patent (e.g., "PREVENTION OF PREGNANCY", "TREATMENT OF HYPERTENSION"). Sourced from the FDA Patent Use Codes and Definitions publication.');

    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @Description NVARCHAR(500);
    DECLARE column_cursor CURSOR FOR SELECT ColumnName, Description FROM @ColumnDescriptions;

    OPEN column_cursor;
    FETCH NEXT FROM column_cursor INTO @ColumnName, @Description;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        BEGIN
            EXEC sp_addextendedproperty
                @name = N'MS_Description',
                @value = @Description,
                @level0type = N'SCHEMA', @level0name = @SchemaName,
                @level1type = N'TABLE', @level1name = @TableName,
                @level2type = N'COLUMN', @level2name = @ColumnName;
        END
        FETCH NEXT FROM column_cursor INTO @ColumnName, @Description;
    END

    CLOSE column_cursor;
    DEALLOCATE column_cursor;

    PRINT '    - Added extended properties for all columns';

    ---------------------------------------------------------------------------
    -- Summary
    ---------------------------------------------------------------------------
    PRINT '';
    PRINT '========================================';
    PRINT 'Migration completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT ' - OrangeBookPatentUseCode table created (if not exists)';
    PRINT ' - Natural primary key on PatentUseCode (no surrogate ID)';
    PRINT ' - Extended properties added for documentation';
    PRINT '';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    PRINT '';
    PRINT '========================================';
    PRINT 'ERROR: Migration failed!';
    PRINT '========================================';
    PRINT 'Error Message: ' + @ErrorMessage;
    PRINT 'Error Severity: ' + CAST(@ErrorSeverity AS NVARCHAR(10));
    PRINT 'Error State: ' + CAST(@ErrorState AS NVARCHAR(10));
    PRINT '';

    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH
GO

PRINT '';
PRINT 'OrangeBookPatentUseCode table is ready for data import.';
PRINT 'Data source: embedded JSON resource (OrangeBookPatentUseCodes.json).';
PRINT '';
