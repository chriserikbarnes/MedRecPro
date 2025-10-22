/***************************************************************
 * Migration Script: Add AspNetUserActivityLog Table
 * Purpose: Creates the AspNetUserActivityLog table for comprehensive
 *          activity logging of user actions and controller executions.
 * 
 * Features:
 * - Tracks user activities with detailed request/response information
 * - Captures controller and endpoint execution details
 * - Records performance metrics (execution time, status codes)
 * - Stores error information (exceptions, stack traces)
 * - Supports activity analysis and auditing
 * 
 * Dependencies:
 * - Requires AspNetUsers table (foreign key relationship)
 * 
 * Backwards Compatibility:
 * - New table addition, no impact on existing tables
 * - All columns support NULL except primary key, UserId, ActivityType, and ActivityTimestamp
 * - Foreign key with CASCADE delete ensures data integrity
 ***************************************************************/

SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Creating AspNetUserActivityLog Table';
    PRINT '========================================';
    PRINT '';

    -- Verify AspNetUsers table exists before creating foreign key
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUsers' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT 'ERROR: [dbo].[AspNetUsers] table does not exist.';
        PRINT 'Please ensure the Identity framework tables are created before applying this migration.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    PRINT ' -> [dbo].[AspNetUsers] table found.';
    PRINT '';

    -- Create AspNetUserActivityLog table if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserActivityLog' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[AspNetUserActivityLog] table...';
        
        CREATE TABLE [dbo].[AspNetUserActivityLog] (
            -- Primary Key
            [ActivityLogId] BIGINT IDENTITY(1,1) NOT NULL,
            
            -- User and Activity Information
            [UserId] BIGINT NOT NULL,
            [ActivityType] NVARCHAR(100) NOT NULL,
            [ActivityTimestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            [Description] NVARCHAR(500) NULL,
            
            -- Request Details
            [IpAddress] NVARCHAR(45) NULL,
            [UserAgent] NVARCHAR(500) NULL,
            [RequestPath] NVARCHAR(500) NULL,
            
            -- Controller/Endpoint Details
            [ControllerName] NVARCHAR(100) NULL,
            [ActionName] NVARCHAR(100) NULL,
            [HttpMethod] NVARCHAR(10) NULL,
            
            -- Parameters and Performance
            [RequestParameters] NVARCHAR(MAX) NULL,
            [ResponseStatusCode] INT NULL,
            [ExecutionTimeMs] INT NULL,
            
            -- Result and Error Tracking
            [Result] NVARCHAR(50) NULL,
            [ErrorMessage] NVARCHAR(MAX) NULL,
            [ExceptionType] NVARCHAR(200) NULL,
            [StackTrace] NVARCHAR(MAX) NULL,
            
            -- Additional Context
            [SessionId] NVARCHAR(100) NULL,
            
            -- Constraints
            CONSTRAINT [PK_AspNetUserActivityLog] PRIMARY KEY CLUSTERED ([ActivityLogId] ASC),
            CONSTRAINT [FK_ActivityLog_AspNetUsers] FOREIGN KEY ([UserId]) 
                REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
        );
        
        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[AspNetUserActivityLog]';
        PRINT '    - Skipping table creation.';
    END

    PRINT '';
    PRINT ' -> Creating indexes for optimal query performance...';

    -- Index on UserId for user-specific queries
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_UserId' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_UserId] ON [dbo].[AspNetUserActivityLog]([UserId]);
        PRINT '    - Created index: IX_ActivityLog_UserId';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_UserId';
    END

    -- Index on Timestamp for time-based queries
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_Timestamp' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_Timestamp] ON [dbo].[AspNetUserActivityLog]([ActivityTimestamp]);
        PRINT '    - Created index: IX_ActivityLog_Timestamp';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_Timestamp';
    END

    -- Index on ActivityType for activity filtering
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_ActivityType' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_ActivityType] ON [dbo].[AspNetUserActivityLog]([ActivityType]);
        PRINT '    - Created index: IX_ActivityLog_ActivityType';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_ActivityType';
    END

    -- Composite index on Controller and Action for endpoint-specific queries
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_Controller_Action' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_Controller_Action] ON [dbo].[AspNetUserActivityLog]([ControllerName], [ActionName]);
        PRINT '    - Created index: IX_ActivityLog_Controller_Action';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_Controller_Action';
    END

    -- Index on ExecutionTime for performance analysis
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_ExecutionTime' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_ExecutionTime] ON [dbo].[AspNetUserActivityLog]([ExecutionTimeMs]);
        PRINT '    - Created index: IX_ActivityLog_ExecutionTime';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_ExecutionTime';
    END

    -- Index on ResponseStatus for error tracking
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActivityLog_ResponseStatus' AND object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        CREATE INDEX [IX_ActivityLog_ResponseStatus] ON [dbo].[AspNetUserActivityLog]([ResponseStatusCode]);
        PRINT '    - Created index: IX_ActivityLog_ResponseStatus';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_ActivityLog_ResponseStatus';
    END

    PRINT '';
    PRINT ' -> Adding extended properties for documentation...';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'AspNetUserActivityLog';
    DECLARE @PropValue SQL_VARIANT;

    -- Table description
    SET @PropValue = N'Stores comprehensive activity logs for user actions and controller executions. Tracks request details, performance metrics, and error information for auditing and analysis.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty 
            @name = N'MS_Description', 
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    -- Column descriptions
    DECLARE @ColumnDescriptions TABLE (
        ColumnName NVARCHAR(128),
        Description NVARCHAR(500)
    );

    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'ActivityLogId', N'Unique identifier for the activity log entry (auto-increment)'),
        (N'UserId', N'Foreign key to AspNetUsers. Identifies the user who performed the activity'),
        (N'ActivityType', N'Type of activity performed (Login, Logout, Create, Read, Update, Delete, Other)'),
        (N'ActivityTimestamp', N'UTC timestamp when the activity occurred'),
        (N'Description', N'Human-readable description of the activity'),
        (N'IpAddress', N'IP address of the client (IPv4 or IPv6, supports X-Forwarded-For)'),
        (N'UserAgent', N'User-Agent string from the HTTP request header'),
        (N'RequestPath', N'URL path of the request (without query string)'),
        (N'ControllerName', N'Name of the controller that handled the request'),
        (N'ActionName', N'Name of the action method that was executed'),
        (N'HttpMethod', N'HTTP method used (GET, POST, PUT, PATCH, DELETE, etc.)'),
        (N'RequestParameters', N'Action parameters serialized as JSON (sensitive data excluded)'),
        (N'ResponseStatusCode', N'HTTP response status code (200, 400, 404, 500, etc.)'),
        (N'ExecutionTimeMs', N'Execution time of the action in milliseconds'),
        (N'Result', N'Overall result status (Success, Error, Warning)'),
        (N'ErrorMessage', N'Error message if an exception occurred'),
        (N'ExceptionType', N'Type name of the exception that occurred'),
        (N'StackTrace', N'Full stack trace for debugging (populated on errors)'),
        (N'SessionId', N'Session identifier for correlating requests within the same session');

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

    PRINT '';
    PRINT '========================================';
    PRINT 'Migration completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT ' - AspNetUserActivityLog table created (if not exists)';
    PRINT ' - 6 indexes created for optimal query performance';
    PRINT ' - Extended properties added for documentation';
    PRINT ' - Foreign key constraint to AspNetUsers (CASCADE delete)';
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
PRINT 'You can now use the ActivityLogService and ActivityLogActionFilter';
PRINT 'to automatically log user activities and controller executions.';
PRINT '';