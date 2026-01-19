/*
================================================================================
  MedRecPro - Azure SQL Database Disabled Index Rebuild Script
================================================================================

  PURPOSE:
    Rebuilds all disabled indexes in the 'dbo' schema using online rebuild mode.
    This script is designed to run in Azure Query Editor and maintains database
    availability during index rebuilds.

  USAGE:
    1. Open Azure Portal > Navigate to your SQL Database
    2. Open Query Editor (preview)
    3. Replace 'YourDatabaseName' with the actual database name
    4. Execute the script

  REQUIREMENTS:
    - Azure SQL Database (Basic, Standard S3+, or Premium tier for ONLINE rebuild)
    - Sufficient permissions: ALTER on tables, VIEW DATABASE STATE
    - Note: ONLINE = ON is not supported on Basic/S0-S2 tiers

  BEHAVIOR:
    - Identifies all disabled indexes in the 'dbo' schema
    - Generates and executes ALTER INDEX ... REBUILD statements
    - Uses ONLINE = ON to allow concurrent read/write operations during rebuild
    - Only processes named indexes (excludes heap structures)

  OUTPUT:
    - No result set returned on success
    - Indexes are rebuilt and re-enabled
    - If no disabled indexes exist, script completes with no action

  TROUBLESHOOTING:
    - "ONLINE is not supported" error: Upgrade to Standard S3+ or Premium tier
    - Permission denied: Ensure user has ALTER permission on affected tables
    - Long execution time: Large indexes may take several minutes to rebuild

  MAINTENANCE:
    Schedule this script to run periodically if indexes become disabled due to
    schema changes or failed operations.

  LAST UPDATED: 2026-01-19
================================================================================
*/

USE [YourDatabaseName];  -- Replace with your database name

DECLARE @sql NVARCHAR(MAX) = '';

-- Build dynamic SQL to rebuild all disabled indexes in the dbo schema
SELECT @sql = @sql +
    'ALTER INDEX ' + QUOTENAME(i.name)
	+ ' ON ' + QUOTENAME(s.name) + '.'
	+ QUOTENAME(t.name)
	+ ' REBUILD WITH (ONLINE = ON);' + CHAR(13)
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = 'dbo'
  AND i.is_disabled = 1
  AND i.name IS NOT NULL;

-- Execute the generated rebuild statements
EXEC sp_executesql @sql;