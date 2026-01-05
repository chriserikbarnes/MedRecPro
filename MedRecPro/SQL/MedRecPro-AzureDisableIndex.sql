/*
================================================================================
    MedRecPro - Disable Non-Clustered Indexes for Bulk Import
    
    Target: Azure SQL Database (v12+)
    
    Purpose:
        Disables all non-clustered indexes on MedRecPro tables to dramatically
        improve bulk insert performance. Disabled indexes are not maintained
        during INSERT operations, eliminating index update overhead.
        
    Usage:
        Run this script BEFORE executing the BCP import operation.
        After import completes, run MedRecPro-RebuildIndexes.sql to restore.
        
    Notes:
        - Clustered indexes cannot be disabled (they ARE the table data)
        - Primary key indexes are skipped to maintain entity integrity
        - Unique constraints are skipped to maintain data integrity during load
        - Disabled indexes consume no space but queries cannot use them
        - Foreign key constraints are not affected (none defined per design)
        
    Performance Impact:
        - Bulk insert speed improvement: typically 2x-10x faster
        - Query performance during import: degraded (no index access)
        
    Author: MedRecPro Development Team
    Last Updated: 2026
================================================================================
*/

SET NOCOUNT ON;

PRINT '================================================================================';
PRINT '  MedRecPro - Disable Non-Clustered Indexes';
PRINT '  Started: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';
PRINT '';

-- ============================================================================
-- Configuration
-- ============================================================================

DECLARE @SchemaName NVARCHAR(128) = 'dbo';
DECLARE @ExecuteCommands BIT = 1;  -- Set to 0 to preview commands without executing

-- ============================================================================
-- Variables
-- ============================================================================

DECLARE @SQL NVARCHAR(MAX);
DECLARE @TableName NVARCHAR(128);
DECLARE @IndexName NVARCHAR(128);
DECLARE @IndexType NVARCHAR(60);
DECLARE @IsUnique BIT;
DECLARE @IsPrimaryKey BIT;
DECLARE @IsUniqueConstraint BIT;
DECLARE @CurrentState NVARCHAR(20);
DECLARE @TotalIndexes INT = 0;
DECLARE @DisabledCount INT = 0;
DECLARE @SkippedCount INT = 0;
DECLARE @ErrorCount INT = 0;

-- ============================================================================
-- Cursor: Iterate through all non-clustered indexes
-- ============================================================================

DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT 
        t.name AS TableName,
        i.name AS IndexName,
        i.type_desc AS IndexType,
        i.is_unique AS IsUnique,
        i.is_primary_key AS IsPrimaryKey,
        i.is_unique_constraint AS IsUniqueConstraint,
        CASE 
            WHEN i.is_disabled = 1 THEN 'DISABLED'
            ELSE 'ENABLED'
        END AS CurrentState
    FROM sys.indexes i
    INNER JOIN sys.tables t ON i.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = @SchemaName
      AND i.type_desc = 'NONCLUSTERED'  -- Only non-clustered indexes
      AND i.name IS NOT NULL             -- Exclude heap indicators
    ORDER BY t.name, i.name;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO 
    @TableName, @IndexName, @IndexType, @IsUnique, @IsPrimaryKey, @IsUniqueConstraint, @CurrentState;

PRINT 'Processing non-clustered indexes...';
PRINT '';
PRINT 'Legend: [D] = Disabled, [S] = Skipped (PK/Unique), [E] = Error, [A] = Already Disabled';
PRINT '--------------------------------------------------------------------------------';

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TotalIndexes = @TotalIndexes + 1;
    
    -- Skip primary keys and unique constraints (maintain integrity during load)
    IF @IsPrimaryKey = 1 OR @IsUniqueConstraint = 1
    BEGIN
        PRINT '[S] ' + @TableName + '.' + @IndexName + ' (Skipped: ' + 
              CASE WHEN @IsPrimaryKey = 1 THEN 'Primary Key' ELSE 'Unique Constraint' END + ')';
        SET @SkippedCount = @SkippedCount + 1;
    END
    -- Skip already disabled indexes
    ELSE IF @CurrentState = 'DISABLED'
    BEGIN
        PRINT '[A] ' + @TableName + '.' + @IndexName + ' (Already disabled)';
        SET @SkippedCount = @SkippedCount + 1;
    END
    ELSE
    BEGIN
        -- Build ALTER INDEX statement
        SET @SQL = 'ALTER INDEX ' + QUOTENAME(@IndexName) + 
                   ' ON ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName) + 
                   ' DISABLE;';
        
        IF @ExecuteCommands = 1
        BEGIN
            BEGIN TRY
                EXEC sp_executesql @SQL;
                PRINT '[D] ' + @TableName + '.' + @IndexName;
                SET @DisabledCount = @DisabledCount + 1;
            END TRY
            BEGIN CATCH
                PRINT '[E] ' + @TableName + '.' + @IndexName + ' - Error: ' + ERROR_MESSAGE();
                SET @ErrorCount = @ErrorCount + 1;
            END CATCH
        END
        ELSE
        BEGIN
            -- Preview mode - just print the command
            PRINT '-- PREVIEW: ' + @SQL;
            SET @DisabledCount = @DisabledCount + 1;
        END
    END
    
    FETCH NEXT FROM index_cursor INTO 
        @TableName, @IndexName, @IndexType, @IsUnique, @IsPrimaryKey, @IsUniqueConstraint, @CurrentState;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

-- ============================================================================
-- Summary Report
-- ============================================================================

PRINT '';
PRINT '================================================================================';
PRINT '  Summary';
PRINT '================================================================================';
PRINT '  Total non-clustered indexes found: ' + CAST(@TotalIndexes AS VARCHAR(10));
PRINT '  Indexes disabled:                  ' + CAST(@DisabledCount AS VARCHAR(10));
PRINT '  Indexes skipped:                   ' + CAST(@SkippedCount AS VARCHAR(10));
PRINT '  Errors encountered:                ' + CAST(@ErrorCount AS VARCHAR(10));
PRINT '';
PRINT '  Completed: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '================================================================================';

IF @ExecuteCommands = 0
BEGIN
    PRINT '';
    PRINT '  *** PREVIEW MODE - No changes were made ***';
    PRINT '  Set @ExecuteCommands = 1 to execute the disable commands.';
    PRINT '';
END

-- ============================================================================
-- Verification Query (optional - run separately to verify state)
-- ============================================================================

/*
-- Run this query to verify index states after execution:

SELECT 
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    CASE WHEN i.is_primary_key = 1 THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
    CASE WHEN i.is_unique = 1 THEN 'Yes' ELSE 'No' END AS IsUnique,
    CASE WHEN i.is_disabled = 1 THEN 'DISABLED' ELSE 'ENABLED' END AS [Status]
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = 'dbo'
  AND i.type_desc = 'NONCLUSTERED'
  AND i.name IS NOT NULL
ORDER BY t.name, i.name;

*/