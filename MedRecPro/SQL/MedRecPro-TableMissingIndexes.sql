-- ====================================================================================
-- Script: Missing Index Recommendations Generator
-- ====================================================================================
-- Purpose:
--   Analyzes SQL Server's internal missing index statistics (DMVs) to identify
--   tables that would benefit from additional indexes. Generates ready-to-execute
--   CREATE INDEX statements sorted by potential performance impact.
--
-- How It Works:
--   SQL Server tracks query execution patterns and identifies when indexes would
--   have improved query performance. This script queries those statistics and
--   calculates an "Impact Score" to prioritize recommendations.
--
-- Impact Score Formula:
--   Impact = avg_total_user_cost * avg_user_impact * (user_seeks + user_scans)
--   - avg_total_user_cost : Average cost of queries that would benefit
--   - avg_user_impact     : Estimated % improvement if index existed (0-100)
--   - user_seeks          : Number of seeks the index would have provided
--   - user_scans          : Number of scans the index would have provided
--
-- Output:
--   1. Results Grid  : Detailed analysis with impact scores and index definitions
--   2. Messages Tab  : Copy-paste ready CREATE INDEX statements with GO separators
--
-- Important Notes:
--   - Statistics reset on server restart; allow workload time to accumulate
--   - Review suggestions carefully; not all recommendations should be implemented
--   - Consider existing indexes that may partially cover the recommendation
--   - High index count increases write overhead; balance read vs write performance
--
-- Usage:
--   Execute against target database; review Results grid for analysis,
--   copy CREATE statements from Messages tab as needed
-- ====================================================================================

BEGIN TRY
    -- ---------------------------------------------------------------------------
    -- Temp Table: Consolidate index recommendations to avoid duplicate DMV queries
    -- ---------------------------------------------------------------------------
    -- Querying DMVs once and storing results improves performance and ensures
    -- consistent data between the Results grid and Messages output
    -- ---------------------------------------------------------------------------
    CREATE TABLE #MissingIndexRecommendations
    (
        RecommendationId    INT IDENTITY(1,1) PRIMARY KEY,
        Impact              DECIMAL(18,2)   NOT NULL,   -- Calculated priority score
        UserSeeks           BIGINT          NOT NULL,   -- Seek operations that would benefit
        UserScans           BIGINT          NOT NULL,   -- Scan operations that would benefit
        AvgTotalUserCost    FLOAT           NOT NULL,   -- Average query cost reduction
        AvgUserImpact       FLOAT           NOT NULL,   -- Estimated % improvement
        TableName           NVARCHAR(128)   NULL,       -- Target table name
        EqualityColumns     NVARCHAR(4000)  NULL,       -- Columns for = predicates
        InequalityColumns   NVARCHAR(4000)  NULL,       -- Columns for <, >, BETWEEN, etc.
        IncludedColumns     NVARCHAR(4000)  NULL,       -- Non-key columns for covering
        CreateIndexStatement NVARCHAR(MAX)  NOT NULL    -- Ready-to-execute DDL
    );

    -- ---------------------------------------------------------------------------
    -- Populate Recommendations: Query DMVs and calculate index suggestions
    -- ---------------------------------------------------------------------------
    INSERT INTO #MissingIndexRecommendations
    (
        Impact,
        UserSeeks,
        UserScans,
        AvgTotalUserCost,
        AvgUserImpact,
        TableName,
        EqualityColumns,
        InequalityColumns,
        IncludedColumns,
        CreateIndexStatement
    )
    SELECT 
        -- Impact score: higher = more beneficial to implement
        CONVERT(DECIMAL(18,2), 
            migs.avg_total_user_cost 
            * migs.avg_user_impact 
            * (migs.user_seeks + migs.user_scans)
        ) AS Impact,
        
        migs.user_seeks,
        migs.user_scans,
        migs.avg_total_user_cost,
        migs.avg_user_impact,
        OBJECT_NAME(mid.object_id, mid.database_id) AS TableName,
        mid.equality_columns,
        mid.inequality_columns,
        mid.included_columns,
        
        -- ---------------------------------------------------------------------------
        -- Build CREATE INDEX Statement
        -- ---------------------------------------------------------------------------
        -- Index naming convention: IX_TableName_Column1_Column2[_Column3...]
        -- Key columns: equality columns first (better selectivity), then inequality
        -- Included columns: added via INCLUDE clause for covering index benefits
        -- ---------------------------------------------------------------------------
        'CREATE NONCLUSTERED INDEX [IX_' 
            + OBJECT_NAME(mid.object_id, mid.database_id) + '_'
            + REPLACE(REPLACE(REPLACE(ISNULL(mid.equality_columns, ''), ', ', '_'), '[', ''), ']', '')
            + CASE 
                WHEN mid.equality_columns IS NOT NULL 
                 AND mid.inequality_columns IS NOT NULL THEN '_'
                ELSE ''
              END
            + REPLACE(REPLACE(REPLACE(ISNULL(mid.inequality_columns, ''), ', ', '_'), '[', ''), ']', '')
            + '] ON '
            + mid.statement
            + ' ('
            + ISNULL(mid.equality_columns, '')
            + CASE 
                WHEN mid.equality_columns IS NOT NULL 
                 AND mid.inequality_columns IS NOT NULL THEN ', '
                ELSE ''
              END
            + ISNULL(mid.inequality_columns, '')
            + ')'
            + CASE 
                WHEN mid.included_columns IS NOT NULL 
                THEN ' INCLUDE (' + mid.included_columns + ')'
                ELSE ''
              END
            + ';' AS CreateIndexStatement
            
    FROM sys.dm_db_missing_index_group_stats migs
    INNER JOIN sys.dm_db_missing_index_groups mig 
        ON migs.group_handle = mig.index_group_handle
    INNER JOIN sys.dm_db_missing_index_details mid 
        ON mig.index_handle = mid.index_handle
    WHERE mid.database_id = DB_ID();  -- Current database only

    -- ---------------------------------------------------------------------------
    -- Output 1: Results Grid - Detailed analysis for review
    -- ---------------------------------------------------------------------------
    SELECT 
        Impact,
        UserSeeks,
        UserScans,
        AvgTotalUserCost,
        AvgUserImpact,
        TableName,
        EqualityColumns,
        InequalityColumns,
        IncludedColumns,
        CreateIndexStatement
    FROM #MissingIndexRecommendations
    ORDER BY Impact DESC;

    -- ---------------------------------------------------------------------------
    -- Output 2: Messages Tab - Copy-paste ready CREATE INDEX statements
    -- ---------------------------------------------------------------------------
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @impact DECIMAL(18,2);

    DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT Impact, CreateIndexStatement
    FROM #MissingIndexRecommendations
    ORDER BY Impact DESC;

    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @impact, @sql;

    -- Print header block
    PRINT '-- ============================================';
    PRINT '-- MISSING INDEX RECOMMENDATIONS';
    PRINT '-- Database: ' + DB_NAME();
    PRINT '-- Generated: ' + CONVERT(VARCHAR(20), GETDATE(), 120);
    PRINT '-- Total Recommendations: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    PRINT '-- ============================================';
    PRINT '-- REVIEW BEFORE IMPLEMENTING:';
    PRINT '--   - Check for overlapping existing indexes';
    PRINT '--   - Consider consolidating similar recommendations';
    PRINT '--   - Evaluate write overhead vs read benefit';
    PRINT '-- ============================================';
    PRINT '';

    -- ---------------------------------------------------------------------------
    -- Process Loop: Output each CREATE INDEX statement
    -- ---------------------------------------------------------------------------
    WHILE @@FETCH_STATUS = 0
    BEGIN
        PRINT '-- Impact Score: ' + CAST(@impact AS VARCHAR(20));
        PRINT @sql;
        PRINT 'GO';
        PRINT '';
        
        FETCH NEXT FROM index_cursor INTO @impact, @sql;
    END

    -- ---------------------------------------------------------------------------
    -- Cleanup: Release cursor and temp table resources
    -- ---------------------------------------------------------------------------
    CLOSE index_cursor;
    DEALLOCATE index_cursor;
    DROP TABLE #MissingIndexRecommendations;

END TRY
BEGIN CATCH
    -- ---------------------------------------------------------------------------
    -- Error Handling: Ensure resource cleanup on failure
    -- ---------------------------------------------------------------------------
    IF CURSOR_STATUS('local', 'index_cursor') >= 0
    BEGIN
        CLOSE index_cursor;
        DEALLOCATE index_cursor;
    END

    IF OBJECT_ID('tempdb..#MissingIndexRecommendations') IS NOT NULL
        DROP TABLE #MissingIndexRecommendations;

    PRINT '-- ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO