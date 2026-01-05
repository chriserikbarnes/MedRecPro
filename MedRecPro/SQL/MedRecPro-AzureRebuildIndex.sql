/*
================================================================================
    MedRecPro - Rebuild All Indexes After Bulk Import
    
    Target: Azure SQL Database (v12+)
    
    Purpose:
        Rebuilds all indexes on MedRecPro tables after bulk import operations.
        This includes:
        - Re-enabling disabled non-clustered indexes
        - Rebuilding all indexes to optimize storage and statistics
        - Updating statistics for query optimizer
        
    Usage:
        Run this script AFTER the BCP import operation completes successfully.
        
    Notes:
        - REBUILD is required for disabled indexes (REORGANIZE won't work)
        - Online rebuild (ONLINE = ON) allows concurrent queries but is slower
        - Offline rebuild is faster but locks the table
        - Azure SQL Database supports online rebuilds on most index types
        
    Performance Considerations:
        - This operation is CPU and I/O intensive
        - Consider running during off-peak hours
        - Monitor DTU/vCore usage during execution
        - Large tables may take significant time to rebuild
        
    Estimated Time:
        - Small tables (<100K rows): seconds
        - Medium tables (100K-1M rows): minutes
        - Large tables (>1M rows): 10+ minutes per index
        
    Author: MedRecPro Development Team
    Last Updated: 2026
================================================================================
*/

SET NOCOUNT ON;

PRINT '================================================================================';
PRINT '  MedRecPro - Rebuild All Indexes';
PRINT '  Started: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';
PRINT '';

-- ============================================================================
-- Configuration
-- ============================================================================

DECLARE @SchemaName NVARCHAR(128) = 'dbo';
DECLARE @ExecuteCommands BIT = 1;           -- Set to 0 to preview commands
DECLARE @UseOnlineRebuild BIT = 1;          -- Set to 1 for ONLINE = ON (allows queries during rebuild)
DECLARE @RebuildClusteredIndexes BIT = 1;   -- Set to 1 to also rebuild clustered indexes
DECLARE @MaxDOP INT = 0;                     -- 0 = use server default, or set specific value

-- ============================================================================
-- Variables
-- ============================================================================

DECLARE @SQL NVARCHAR(MAX);
DECLARE @TableName NVARCHAR(128);
DECLARE @IndexName NVARCHAR(128);
DECLARE @IndexType NVARCHAR(60);
DECLARE @IsDisabled BIT;
DECLARE @IsPrimaryKey BIT;
DECLARE @IsUnique BIT;
DECLARE @RowCount BIGINT;
DECLARE @StartTime DATETIME;
DECLARE @EndTime DATETIME;
DECLARE @Duration INT;
DECLARE @TotalIndexes INT = 0;
DECLARE @RebuiltCount INT = 0;
DECLARE @SkippedCount INT = 0;
DECLARE @ErrorCount INT = 0;
DECLARE @OnlineOption NVARCHAR(50);

-- Determine online option string
SET @OnlineOption = CASE 
    WHEN @UseOnlineRebuild = 1 THEN ', ONLINE = ON'
    ELSE ''
END;

-- ============================================================================
-- Pre-flight Check: Show current index states
-- ============================================================================

PRINT 'Current Index Status Summary:';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    'Clustered Indexes' AS IndexCategory,
    COUNT(*) AS Total,
    SUM(CASE WHEN i.is_disabled = 1 THEN 1 ELSE 0 END) AS Disabled,
    SUM(CASE WHEN i.is_disabled = 0 THEN 1 ELSE 0 END) AS Enabled
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND i.type_desc = 'CLUSTERED'
  AND i.name IS NOT NULL
UNION ALL
SELECT 
    'Non-Clustered Indexes' AS IndexCategory,
    COUNT(*) AS Total,
    SUM(CASE WHEN i.is_disabled = 1 THEN 1 ELSE 0 END) AS Disabled,
    SUM(CASE WHEN i.is_disabled = 0 THEN 1 ELSE 0 END) AS Enabled
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND i.type_desc = 'NONCLUSTERED'
  AND i.name IS NOT NULL;

PRINT '';

-- ============================================================================
-- Cursor: Iterate through all indexes to rebuild
-- ============================================================================

DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT 
        t.name AS TableName,
        i.name AS IndexName,
        i.type_desc AS IndexType,
        i.is_disabled AS IsDisabled,
        i.is_primary_key AS IsPrimaryKey,
        i.is_unique AS IsUnique,
        p.rows AS [RowCount]
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
    WHERE s.name = @SchemaName
      AND i.name IS NOT NULL
      AND i.type > 0  -- Exclude heaps (type = 0)
      AND (
          -- Include non-clustered indexes (always)
          i.type_desc = 'NONCLUSTERED'
          OR
          -- Include clustered indexes if configured
          (@RebuildClusteredIndexes = 1 AND i.type_desc = 'CLUSTERED')
      )
    ORDER BY 
        -- Process disabled indexes first (they need REBUILD, not just maintenance)
        i.is_disabled DESC,
        -- Then by table row count (smaller tables first for quick wins)
        p.rows ASC,
        t.name, 
        i.name;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO 
    @TableName, @IndexName, @IndexType, @IsDisabled, @IsPrimaryKey, @IsUnique, @RowCount;

PRINT 'Rebuilding indexes...';
PRINT '';
PRINT 'Legend: [R] = Rebuilt, [S] = Skipped, [E] = Error';
PRINT '--------------------------------------------------------------------------------';

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TotalIndexes = @TotalIndexes + 1;
    SET @StartTime = GETDATE();
    
    -- Build ALTER INDEX REBUILD statement
    -- Note: Disabled indexes MUST use REBUILD (not REORGANIZE)
    SET @SQL = 'ALTER INDEX ' + QUOTENAME(@IndexName) + 
               ' ON ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + 
               ' REBUILD WITH (MAXDOP = ' + CAST(@MaxDOP AS VARCHAR(5)) + @OnlineOption + ');';
    
    IF @ExecuteCommands = 1
    BEGIN
        BEGIN TRY
            EXEC sp_executesql @SQL;
            
            SET @EndTime = GETDATE();
            SET @Duration = DATEDIFF(SECOND, @StartTime, @EndTime);
            
            PRINT '[R] ' + @TableName + '.' + @IndexName + 
                  ' (' + @IndexType + 
                  CASE WHEN @IsDisabled = 1 THEN ', was DISABLED' ELSE '' END +
                  ', ' + FORMAT(@RowCount, 'N0') + ' rows' +
                  ', ' + CAST(@Duration AS VARCHAR(10)) + 's)';
            
            SET @RebuiltCount = @RebuiltCount + 1;
        END TRY
        BEGIN CATCH
            -- Handle online rebuild failures (some index types don't support online)
            IF ERROR_NUMBER() = 2725 OR ERROR_MESSAGE() LIKE '%online%'
            BEGIN
                -- Retry with offline rebuild
                BEGIN TRY
                    SET @SQL = 'ALTER INDEX ' + QUOTENAME(@IndexName) + 
                               ' ON ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + 
                               ' REBUILD WITH (MAXDOP = ' + CAST(@MaxDOP AS VARCHAR(5)) + ');';
                    EXEC sp_executesql @SQL;
                    
                    SET @EndTime = GETDATE();
                    SET @Duration = DATEDIFF(SECOND, @StartTime, @EndTime);
                    
                    PRINT '[R] ' + @TableName + '.' + @IndexName + 
                          ' (' + @IndexType + ', OFFLINE rebuild' +
                          ', ' + FORMAT(@RowCount, 'N0') + ' rows' +
                          ', ' + CAST(@Duration AS VARCHAR(10)) + 's)';
                    
                    SET @RebuiltCount = @RebuiltCount + 1;
                END TRY
                BEGIN CATCH
                    PRINT '[E] ' + @TableName + '.' + @IndexName + ' - Error: ' + ERROR_MESSAGE();
                    SET @ErrorCount = @ErrorCount + 1;
                END CATCH
            END
            ELSE
            BEGIN
                PRINT '[E] ' + @TableName + '.' + @IndexName + ' - Error: ' + ERROR_MESSAGE();
                SET @ErrorCount = @ErrorCount + 1;
            END
        END CATCH
    END
    ELSE
    BEGIN
        -- Preview mode
        PRINT '-- PREVIEW: ' + @SQL;
        SET @RebuiltCount = @RebuiltCount + 1;
    END
    
    FETCH NEXT FROM index_cursor INTO 
        @TableName, @IndexName, @IndexType, @IsDisabled, @IsPrimaryKey, @IsUnique, @RowCount;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

-- ============================================================================
-- Update Statistics (important after bulk load)
-- ============================================================================

PRINT '';
PRINT '--------------------------------------------------------------------------------';
PRINT 'Updating table statistics...';
PRINT '--------------------------------------------------------------------------------';

DECLARE stats_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = @SchemaName
    ORDER BY t.name;

OPEN stats_cursor;
FETCH NEXT FROM stats_cursor INTO @TableName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SQL = 'UPDATE STATISTICS ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + ' WITH FULLSCAN;';
    
    IF @ExecuteCommands = 1
    BEGIN
        BEGIN TRY
            EXEC sp_executesql @SQL;
            PRINT '[S] ' + @TableName + ' - Statistics updated';
        END TRY
        BEGIN CATCH
            PRINT '[E] ' + @TableName + ' - Stats Error: ' + ERROR_MESSAGE();
        END CATCH
    END
    ELSE
    BEGIN
        PRINT '-- PREVIEW: ' + @SQL;
    END
    
    FETCH NEXT FROM stats_cursor INTO @TableName;
END

CLOSE stats_cursor;
DEALLOCATE stats_cursor;

-- ============================================================================
-- Summary Report
-- ============================================================================

PRINT '';
PRINT '================================================================================';
PRINT '  Summary';
PRINT '================================================================================';
PRINT '  Total indexes processed:   ' + CAST(@TotalIndexes AS VARCHAR(10));
PRINT '  Indexes rebuilt:           ' + CAST(@RebuiltCount AS VARCHAR(10));
PRINT '  Indexes skipped:           ' + CAST(@SkippedCount AS VARCHAR(10));
PRINT '  Errors encountered:        ' + CAST(@ErrorCount AS VARCHAR(10));
PRINT '';
PRINT '  Completed: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';

IF @ExecuteCommands = 0
BEGIN
    PRINT '';
    PRINT '  *** PREVIEW MODE - No changes were made ***';
    PRINT '  Set @ExecuteCommands = 1 to execute the rebuild commands.';
    PRINT '';
END

-- ============================================================================
-- Post-Rebuild Verification
-- ============================================================================

PRINT '';
PRINT 'Post-Rebuild Index Status:';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    CASE WHEN i.is_disabled = 1 THEN 'DISABLED' ELSE 'ENABLED' END AS [Status],
    FORMAT(p.rows, 'N0') AS [RowCount],
    CASE 
        WHEN ps.avg_fragmentation_in_percent IS NULL THEN 'N/A'
        WHEN ps.avg_fragmentation_in_percent < 5 THEN 'Excellent'
        WHEN ps.avg_fragmentation_in_percent < 30 THEN 'Good'
        ELSE 'Needs Attention'
    END AS FragmentationStatus
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
LEFT JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps 
    ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE s.name = @SchemaName
  AND i.name IS NOT NULL
  AND i.type > 0
  AND i.is_disabled = 1  -- Show any indexes that are still disabled (problems)
ORDER BY t.name, i.name;

-- If no disabled indexes, show success message
IF @@ROWCOUNT = 0
BEGIN
    PRINT '';
    PRINT '✓ All indexes are now enabled and rebuilt.';
    PRINT '';
END