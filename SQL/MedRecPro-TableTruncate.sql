-- ====================================================================================
-- WARNING: THIS SCRIPT DELETES ALL DATA from tables except for the exceptions below.
-- DO NOT RUN THIS ON A PRODUCTION DATABASE.
--
-- Purpose: Resets a development database by truncating all user tables.
-- Exceptions:
--   - __EFMigrationsHistory (to preserve migration history)
--   - AspNet... tables (to preserve user logins, roles, etc.)
-- ====================================================================================

BEGIN TRANSACTION;

BEGIN TRY

    -- 1. Disable all foreign key constraints
    PRINT 'Disabling all foreign key constraints...';
    EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

    -- 2. Truncate all tables except for the specified ones
    PRINT 'Truncating tables...';

    DECLARE @tableName NVARCHAR(MAX);
    DECLARE @schemaName NVARCHAR(MAX);

    -- Create a cursor to loop through all tables that need to be truncated
    DECLARE table_cursor CURSOR FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.type = 'U' -- U = User Table
      AND t.name <> '__EFMigrationsHistory'
      AND t.name NOT LIKE 'AspNet%';

    OPEN table_cursor;

    FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @fullTableName NVARCHAR(MAX) = QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName);
        DECLARE @truncateCommand NVARCHAR(MAX) = 'TRUNCATE TABLE ' + @fullTableName;

        PRINT '  - Truncating ' + @fullTableName;
        EXEC sp_executesql @truncateCommand;

        FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;
    END

    CLOSE table_cursor;
    DEALLOCATE table_cursor;

    -- 3. Re-enable all foreign key constraints
    PRINT 'Re-enabling all foreign key constraints...';
    EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

    PRINT 'Database reset complete.';
    COMMIT TRANSACTION;

END TRY
BEGIN CATCH
    -- If an error occurs, roll back the transaction
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction has been rolled back.';

    -- Re-throw the error to see the details
    THROW;
END CATCH;
GO