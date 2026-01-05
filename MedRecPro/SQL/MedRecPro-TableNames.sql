USE MedRecLocal
GO

-- ====================================================================================
-- Script: Get Label Tables for BCP Export
-- ====================================================================================
-- Purpose: 
--   Retrieves names of all user tables eligible for BCP (Bulk Copy Program) 
--   data import/export operations. Output can be used as input for PowerShell
--   scripts that automate database migration or backup processes.
--
-- Exclusions:
--   - __EFMigrationsHistory : Preserves Entity Framework migration state
--   - AspNet* tables       : Preserves ASP.NET Identity data (users, roles, claims)
--
-- Output:
--   Prints table names to Messages tab, sorted alphabetically by table name
--
-- Usage:
--   Execute in SSMS; copy output from Messages tab for use in PS scripts
-- ====================================================================================

BEGIN TRY
    -- ---------------------------------------------------------------------------
    -- Variable Declarations
    -- ---------------------------------------------------------------------------
    DECLARE @tableName NVARCHAR(128);   -- Current table name from cursor
    DECLARE @schemaName NVARCHAR(128);  -- Current schema name from cursor
    
    -- ---------------------------------------------------------------------------
    -- Temp Table: Stores filtered table list for sorting before cursor iteration
    -- ---------------------------------------------------------------------------
    -- Using temp table allows ORDER BY (not supported directly in CURSOR SELECT)
    -- and provides a clean dataset for iteration
    -- ---------------------------------------------------------------------------
    CREATE TABLE #EligibleTables
    (
        SchemaName NVARCHAR(128) NOT NULL,
        TableName  NVARCHAR(128) NOT NULL
    );

    -- ---------------------------------------------------------------------------
    -- Populate Temp Table: Filter system/excluded tables, prepare for sorting
    -- ---------------------------------------------------------------------------
    INSERT INTO #EligibleTables (SchemaName, TableName)
    SELECT 
        s.name AS SchemaName,
        t.name AS TableName
    FROM sys.tables t
    INNER JOIN sys.schemas s 
        ON t.schema_id = s.schema_id
    WHERE t.type = 'U'                              -- U = User-defined table
      AND t.name <> '__EFMigrationsHistory'         -- Exclude EF migrations
      AND t.name NOT LIKE 'AspNet%';                -- Exclude Identity tables

    -- ---------------------------------------------------------------------------
    -- Cursor: Iterate through sorted table list
    -- ---------------------------------------------------------------------------
    -- Sorting by TableName ensures consistent, predictable output order
    -- for downstream PowerShell script consumption
    -- ---------------------------------------------------------------------------
    DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName, TableName
    FROM #EligibleTables
    ORDER BY TableName ASC;

    OPEN table_cursor;
    FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;

    -- ---------------------------------------------------------------------------
    -- Process Loop: Output each table name
    -- ---------------------------------------------------------------------------
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Note: @fullTableName available if schema-qualified name needed later
        DECLARE @fullTableName NVARCHAR(261) = QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName);
        
        -- Output table name for PS script consumption
        PRINT @tableName;

        FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;
    END

    -- ---------------------------------------------------------------------------
    -- Cleanup: Release cursor and temp table resources
    -- ---------------------------------------------------------------------------
    CLOSE table_cursor;
    DEALLOCATE table_cursor;
    DROP TABLE #EligibleTables;

END TRY
BEGIN CATCH
    -- ---------------------------------------------------------------------------
    -- Error Handling: Ensure cursor cleanup on failure, then re-throw
    -- ---------------------------------------------------------------------------
    IF CURSOR_STATUS('local', 'table_cursor') >= 0
    BEGIN
        CLOSE table_cursor;
        DEALLOCATE table_cursor;
    END

    IF OBJECT_ID('tempdb..#EligibleTables') IS NOT NULL
        DROP TABLE #EligibleTables;

    PRINT 'Error: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO