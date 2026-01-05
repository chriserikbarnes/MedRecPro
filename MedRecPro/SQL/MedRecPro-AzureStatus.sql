/*
================================================================================
    MedRecPro - Index and Table Status Verification
    
    Target: Azure SQL Database (v12+)
    
    Purpose:
        Provides comprehensive status information about tables and indexes
        to verify state before import, after import, and after index rebuild.
        
    Usage:
        Run this script at any time to check the current state of the database.
        
    Author: MedRecPro Development Team
    Last Updated: 2026
================================================================================
*/

SET NOCOUNT ON;

DECLARE @SchemaName NVARCHAR(128) = 'dbo';

PRINT '================================================================================';
PRINT '  MedRecPro - Database Status Report';
PRINT '  Generated: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';
PRINT '';

-- ============================================================================
-- Section 1: Table Row Counts
-- ============================================================================

PRINT '1. TABLE ROW COUNTS';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    t.name AS TableName,
    FORMAT(SUM(p.rows), 'N0') AS [RowCount],
    FORMAT(SUM(a.total_pages) * 8 / 1024.0, 'N2') AS SizeMB
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE s.name = @SchemaName
GROUP BY t.name
ORDER BY SUM(p.rows) DESC;

PRINT '';

-- ============================================================================
-- Section 2: Index Summary by State
-- ============================================================================

PRINT '2. INDEX SUMMARY BY STATE';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    i.type_desc AS IndexType,
    CASE WHEN i.is_disabled = 1 THEN 'DISABLED' ELSE 'ENABLED' END AS [Status],
    COUNT(*) AS [Count]
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND i.name IS NOT NULL
  AND i.type > 0
GROUP BY i.type_desc, i.is_disabled
ORDER BY i.type_desc, i.is_disabled;

PRINT '';

-- ============================================================================
-- Section 3: Disabled Indexes (if any)
-- ============================================================================

PRINT '3. DISABLED INDEXES (requires attention before queries will work)';
PRINT '--------------------------------------------------------------------------------';

SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    CASE WHEN i.is_primary_key = 1 THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
    CASE WHEN i.is_unique = 1 THEN 'Yes' ELSE 'No' END AS IsUnique
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND i.is_disabled = 1
  AND i.name IS NOT NULL
ORDER BY t.name, i.name;

IF @@ROWCOUNT = 0
    PRINT '   (No disabled indexes found - all indexes are active)';

PRINT '';

-- ============================================================================
-- Section 4: Index Fragmentation (Top 20 most fragmented)
-- ============================================================================

PRINT '4. INDEX FRAGMENTATION (Top 20 most fragmented)';
PRINT '--------------------------------------------------------------------------------';
PRINT '   Note: <5% = Excellent, 5-30% = Consider REORGANIZE, >30% = Consider REBUILD';
PRINT '';

SELECT TOP 20
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    FORMAT(ps.avg_fragmentation_in_percent, 'N1') AS [Fragmentation%],
    FORMAT(ps.page_count, 'N0') AS PageCount,
    CASE 
        WHEN ps.avg_fragmentation_in_percent < 5 THEN 'Excellent'
        WHEN ps.avg_fragmentation_in_percent < 30 THEN 'Reorganize'
        ELSE 'Rebuild'
    END AS Recommendation
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND i.name IS NOT NULL
  AND ps.page_count > 100  -- Only indexes with significant page count
  AND i.is_disabled = 0    -- Skip disabled indexes
ORDER BY ps.avg_fragmentation_in_percent DESC;

PRINT '';

-- ============================================================================
-- Section 5: Database Size Summary
-- ============================================================================

PRINT '5. DATABASE SIZE SUMMARY';
PRINT '--------------------------------------------------------------------------------';

SELECT
    FORMAT(SUM(CASE WHEN type = 0 THEN size END) * 8 / 1024.0, 'N2') AS DataSizeMB,
    FORMAT(SUM(CASE WHEN type = 1 THEN size END) * 8 / 1024.0, 'N2') AS LogSizeMB,
    FORMAT(SUM(size) * 8 / 1024.0, 'N2') AS TotalSizeMB
FROM sys.database_files;

PRINT '';

-- ============================================================================
-- Section 6: Recent Statistics Updates
-- ============================================================================

PRINT '6. STATISTICS AGE (Tables with oldest statistics)';
PRINT '--------------------------------------------------------------------------------';

SELECT TOP 20
    t.name AS TableName,
    s.name AS StatisticName,
    STATS_DATE(s.object_id, s.stats_id) AS LastUpdated,
    DATEDIFF(DAY, STATS_DATE(s.object_id, s.stats_id), GETDATE()) AS DaysOld
FROM sys.stats s
INNER JOIN sys.tables t ON s.object_id = t.object_id
INNER JOIN sys.schemas sch ON t.schema_id = sch.schema_id
WHERE sch.name = @SchemaName
  AND STATS_DATE(s.object_id, s.stats_id) IS NOT NULL
ORDER BY STATS_DATE(s.object_id, s.stats_id) ASC;

PRINT '';
PRINT '================================================================================';
PRINT '  End of Report';
PRINT '================================================================================';