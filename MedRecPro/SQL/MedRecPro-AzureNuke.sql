/*
================================================================================
    MedRecPro - Truncate All Tables Before Import
    
    Target: Azure SQL Database (v12+)
    
    Purpose:
        Removes all data from MedRecPro tables to prepare for a clean bulk import.
        TRUNCATE is significantly faster than DELETE and resets identity columns.
        
    Usage:
        Run this script BEFORE the BCP import if you want to replace all data
        rather than append to existing data.
        
    ⚠️  WARNING: This operation is IRREVERSIBLE and will delete ALL data!
    
    Excluded Tables (preserved during truncate):
        - __EFMigrationsHistory  : Entity Framework Core migration tracking
        - AspNet*                : ASP.NET Identity tables (users, roles, claims, etc.)
        
    Notes:
        - TRUNCATE is DDL, not DML - it's not logged row-by-row
        - Identity columns are reset to their seed values
        - No foreign keys defined, so no dependency ordering required
        - Tables with indexed views cannot use TRUNCATE (will use DELETE)
        
    Author: MedRecPro Development Team
    Last Updated: 2026
================================================================================
*/

SET NOCOUNT ON;

PRINT '================================================================================';
PRINT '  MedRecPro - Truncate All Tables';
PRINT '  Started: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';
PRINT '';

-- ============================================================================
-- Configuration
-- ============================================================================

DECLARE @SchemaName NVARCHAR(128) = 'dbo';
DECLARE @ExecuteCommands BIT = 0;  -- ⚠️ SET TO 1 TO ACTUALLY TRUNCATE!

-- ============================================================================
-- Safety Check
-- ============================================================================

IF @ExecuteCommands = 0
BEGIN
    PRINT '╔══════════════════════════════════════════════════════════════════╗';
    PRINT '║                         SAFETY MODE                              ║';
    PRINT '║                                                                  ║';
    PRINT '║  @ExecuteCommands is set to 0 (preview mode).                    ║';
    PRINT '║  No data will be deleted. Commands will be displayed only.       ║';
    PRINT '║                                                                  ║';
    PRINT '║  To execute: SET @ExecuteCommands = 1                            ║';
    PRINT '║                                                                  ║';
    PRINT '╚══════════════════════════════════════════════════════════════════╝';
    PRINT '';
END
ELSE
BEGIN
    PRINT '╔══════════════════════════════════════════════════════════════════╗';
    PRINT '║                       DESTRUCTIVE MODE                           ║';
    PRINT '║                                                                  ║';
    PRINT '║  @ExecuteCommands is set to 1.                                   ║';
    PRINT '║  ALL DATA WILL BE PERMANENTLY DELETED!                           ║';
    PRINT '║                                                                  ║';
    PRINT '╚══════════════════════════════════════════════════════════════════╝';
    PRINT '';
    
    -- Give user a chance to cancel
    WAITFOR DELAY '00:00:03';  -- 3 second pause
END

-- ============================================================================
-- Variables
-- ============================================================================

DECLARE @SQL NVARCHAR(MAX);
DECLARE @TableName NVARCHAR(128);
DECLARE @RowCount BIGINT;
DECLARE @TotalTables INT = 0;
DECLARE @TruncatedCount INT = 0;
DECLARE @DeletedCount INT = 0;
DECLARE @ErrorCount INT = 0;
DECLARE @TotalRowsCleared BIGINT = 0;

-- ============================================================================
-- Pre-Truncate Summary
-- ============================================================================

PRINT 'Current Data Summary (will be deleted):';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    t.name AS TableName,
    FORMAT(SUM(p.rows), 'N0') AS [RowCount]
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE s.name = @SchemaName
  AND t.type = 'U'                          -- User tables only
  AND t.name <> '__EFMigrationsHistory'     -- Preserve EF Core migration history
  AND t.name NOT LIKE 'AspNet%'             -- Preserve ASP.NET Identity tables
GROUP BY t.name
HAVING SUM(p.rows) > 0
ORDER BY SUM(p.rows) DESC;

SELECT @TotalRowsCleared = SUM(p.rows)
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE s.name = @SchemaName
  AND t.type = 'U'                          -- User tables only
  AND t.name <> '__EFMigrationsHistory'     -- Preserve EF Core migration history
  AND t.name NOT LIKE 'AspNet%';            -- Preserve ASP.NET Identity tables

PRINT '';
PRINT 'Total rows to be deleted: ' + FORMAT(@TotalRowsCleared, 'N0');
PRINT '';

-- Show preserved tables
PRINT 'Preserved Tables (will NOT be truncated):';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    t.name AS TableName,
    FORMAT(SUM(p.rows), 'N0') AS [RowCount],
    CASE 
        WHEN t.name = '__EFMigrationsHistory' THEN 'EF Core Migrations'
        WHEN t.name LIKE 'AspNet%' THEN 'ASP.NET Identity'
        ELSE 'Other'
    END AS Reason
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE s.name = @SchemaName
  AND t.type = 'U'
  AND (t.name = '__EFMigrationsHistory' OR t.name LIKE 'AspNet%')
GROUP BY t.name
ORDER BY t.name;

PRINT '';

-- ============================================================================
-- Cursor: Truncate each table
-- ============================================================================

DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT 
        t.name AS TableName,
        SUM(p.rows) AS [RowCount]
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
    WHERE s.name = @SchemaName
      AND t.type = 'U'                          -- User tables only
      AND t.name <> '__EFMigrationsHistory'     -- Preserve EF Core migration history
      AND t.name NOT LIKE 'AspNet%'             -- Preserve ASP.NET Identity tables
    GROUP BY t.name
    ORDER BY t.name;

OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @TableName, @RowCount;

PRINT 'Processing tables...';
PRINT '--------------------------------------------------------------------------------';
PRINT 'Legend: [T] = Truncated, [D] = Deleted (fallback), [E] = Error, [S] = Skipped (empty)';
PRINT '';

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TotalTables = @TotalTables + 1;
    
    -- Skip empty tables
    IF @RowCount = 0
    BEGIN
        PRINT '[S] ' + @TableName + ' (already empty)';
        FETCH NEXT FROM table_cursor INTO @TableName, @RowCount;
        CONTINUE;
    END
    
    -- Try TRUNCATE first (faster, resets identity)
    SET @SQL = 'TRUNCATE TABLE ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + ';';
    
    IF @ExecuteCommands = 1
    BEGIN
        BEGIN TRY
            EXEC sp_executesql @SQL;
            PRINT '[T] ' + @TableName + ' (' + FORMAT(@RowCount, 'N0') + ' rows removed)';
            SET @TruncatedCount = @TruncatedCount + 1;
        END TRY
        BEGIN CATCH
            -- TRUNCATE can fail if table has indexed views or other constraints
            -- Fall back to DELETE
            BEGIN TRY
                SET @SQL = 'DELETE FROM ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + ';';
                EXEC sp_executesql @SQL;
                PRINT '[D] ' + @TableName + ' (' + FORMAT(@RowCount, 'N0') + ' rows deleted via DELETE)';
                SET @DeletedCount = @DeletedCount + 1;
            END TRY
            BEGIN CATCH
                PRINT '[E] ' + @TableName + ' - Error: ' + ERROR_MESSAGE();
                SET @ErrorCount = @ErrorCount + 1;
            END CATCH
        END CATCH
    END
    ELSE
    BEGIN
        -- Preview mode
        PRINT '-- PREVIEW: ' + @SQL + ' (' + FORMAT(@RowCount, 'N0') + ' rows)';
        SET @TruncatedCount = @TruncatedCount + 1;
    END
    
    FETCH NEXT FROM table_cursor INTO @TableName, @RowCount;
END

CLOSE table_cursor;
DEALLOCATE table_cursor;

-- ============================================================================
-- Summary Report
-- ============================================================================

PRINT '';
PRINT '================================================================================';
PRINT '  Summary';
PRINT '================================================================================';
PRINT '  Total tables:              ' + CAST(@TotalTables AS VARCHAR(10));
PRINT '  Tables truncated:          ' + CAST(@TruncatedCount AS VARCHAR(10));
PRINT '  Tables deleted (fallback): ' + CAST(@DeletedCount AS VARCHAR(10));
PRINT '  Errors encountered:        ' + CAST(@ErrorCount AS VARCHAR(10));
PRINT '  Total rows cleared:        ' + FORMAT(@TotalRowsCleared, 'N0');
PRINT '';
PRINT '  Completed: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';

IF @ExecuteCommands = 0
BEGIN
    PRINT '';
    PRINT '  *** PREVIEW MODE - No data was deleted ***';
    PRINT '  Set @ExecuteCommands = 1 to execute the truncate commands.';
    PRINT '';
END
ELSE
BEGIN
    PRINT '';
    PRINT '  ✓ Tables are now empty and ready for bulk import.';
    PRINT '';
END

-- ============================================================================
-- Post-Truncate Verification
-- ============================================================================

IF @ExecuteCommands = 1
BEGIN
    PRINT 'Verification - Tables with remaining data (should be none):';
    PRINT '--------------------------------------------------------------------------------';
    
    SELECT 
        t.name AS TableName,
        FORMAT(SUM(p.rows), 'N0') AS [RowCount]
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
    WHERE s.name = @SchemaName
      AND t.type = 'U'                          -- User tables only
      AND t.name <> '__EFMigrationsHistory'     -- Preserve EF Core migration history
      AND t.name NOT LIKE 'AspNet%'             -- Preserve ASP.NET Identity tables
    GROUP BY t.name
    HAVING SUM(p.rows) > 0
    ORDER BY SUM(p.rows) DESC;
    
    IF @@ROWCOUNT = 0
        PRINT '   (All tables are empty - ready for import)';
END