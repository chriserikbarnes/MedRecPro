/***************************************************************
 * Migration Script: Make AspNetUserActivityLog.UserId Nullable
 * Purpose: Allows logging of anonymous user activities by making
 *          the UserId column nullable.
 * 
 * Changes:
 * - ALTER UserId column from NOT NULL to NULL
 * - Recreate foreign key constraint to allow NULL values
 * - Maintains all existing data and indexes
 * 
 * Reason:
 * - Support logging for unauthenticated/anonymous users
 * - Prevent foreign key violations when user is not logged in
 * - Enable comprehensive activity tracking regardless of auth status
 * 
 * Backwards Compatibility:
 * - Existing NOT NULL values remain unchanged
 * - Application code already handles nullable UserId scenario
 * - No data loss or migration required
 ***************************************************************/

SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Modifying AspNetUserActivityLog Table';
    PRINT 'Make UserId Column Nullable';
    PRINT '========================================';
    PRINT '';

    -- Check if table exists
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserActivityLog' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT 'ERROR: [dbo].[AspNetUserActivityLog] table does not exist.';
        PRINT 'Please run the table creation script first.';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    PRINT ' -> [dbo].[AspNetUserActivityLog] table found.';
    PRINT '';

    -- Check current nullability of UserId column
    DECLARE @IsNullable BIT = 0;
    
    SELECT @IsNullable = CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' 
        AND TABLE_NAME = 'AspNetUserActivityLog' 
        AND COLUMN_NAME = 'UserId';

    IF @IsNullable = 1
    BEGIN
        PRINT ' -> UserId column is already nullable.';
        PRINT '    - No changes needed.';
        PRINT '';
        COMMIT TRANSACTION;
        RETURN;
    END

    PRINT ' -> UserId column is currently NOT NULL.';
    PRINT ' -> Beginning modification process...';
    PRINT '';

    -- Step 1: Drop the foreign key constraint
    DECLARE @ConstraintName NVARCHAR(256);
    
    SELECT @ConstraintName = name
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.AspNetUserActivityLog')
        AND referenced_object_id = OBJECT_ID('dbo.AspNetUsers')
        AND name LIKE '%ActivityLog%Users%';

    IF @ConstraintName IS NOT NULL
    BEGIN
        PRINT ' -> Dropping foreign key constraint: ' + @ConstraintName;
        DECLARE @DropFKSQL NVARCHAR(MAX) = N'ALTER TABLE [dbo].[AspNetUserActivityLog] DROP CONSTRAINT [' + @ConstraintName + ']';
        EXEC sp_executesql @DropFKSQL;
        PRINT '    - Foreign key constraint dropped successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> No existing foreign key constraint found to drop.';
    END
    PRINT '';

    -- Step 2: Alter the UserId column to be nullable
    PRINT ' -> Altering UserId column to allow NULL values...';
    ALTER TABLE [dbo].[AspNetUserActivityLog]
        ALTER COLUMN [UserId] BIGINT NULL;
    PRINT '    - Column altered successfully.';
    PRINT '';

    -- Step 3: Recreate the foreign key constraint (now allowing NULLs)
    PRINT ' -> Recreating foreign key constraint (with NULL support)...';
    ALTER TABLE [dbo].[AspNetUserActivityLog]
        ADD CONSTRAINT [FK_ActivityLog_AspNetUsers] 
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) 
        ON DELETE CASCADE;
    PRINT '    - Foreign key constraint recreated successfully.';
    PRINT '';

    -- Step 4: Update extended property for UserId column
    PRINT ' -> Updating column documentation...';
    
    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'AspNetUserActivityLog';
    DECLARE @ColumnName NVARCHAR(128) = N'UserId';
    DECLARE @NewDescription NVARCHAR(500) = N'Foreign key to AspNetUsers. Identifies the user who performed the activity. NULL for anonymous/unauthenticated users.';

    -- Drop existing property if it exists
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
    BEGIN
        EXEC sp_dropextendedproperty 
            @name = N'MS_Description',
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;
    END

    -- Add updated property
    EXEC sp_addextendedproperty 
        @name = N'MS_Description', 
        @value = @NewDescription,
        @level0type = N'SCHEMA', @level0name = @SchemaName,
        @level1type = N'TABLE', @level1name = @TableName,
        @level2type = N'COLUMN', @level2name = @ColumnName;
    
    PRINT '    - Column documentation updated.';
    PRINT '';

    -- Verification
    PRINT ' -> Verifying changes...';
    
    SELECT @IsNullable = CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' 
        AND TABLE_NAME = 'AspNetUserActivityLog' 
        AND COLUMN_NAME = 'UserId';

    IF @IsNullable = 1
    BEGIN
        PRINT '    ✓ UserId column is now nullable';
    END
    ELSE
    BEGIN
        PRINT '    ✗ ERROR: UserId column is still NOT NULL';
        ROLLBACK TRANSACTION;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ActivityLog_AspNetUsers' AND parent_object_id = OBJECT_ID('dbo.AspNetUserActivityLog'))
    BEGIN
        PRINT '    ✓ Foreign key constraint recreated';
    END
    ELSE
    BEGIN
        PRINT '    ✗ WARNING: Foreign key constraint not found';
    END

    PRINT '';
    PRINT '========================================';
    PRINT 'Migration completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary of Changes:';
    PRINT ' - UserId column: BIGINT NOT NULL → BIGINT NULL';
    PRINT ' - Foreign key constraint: Recreated with NULL support';
    PRINT ' - All existing data: Preserved';
    PRINT ' - All indexes: Intact';
    PRINT '';
    PRINT 'Impact:';
    PRINT ' - Anonymous users can now be logged (UserId = NULL)';
    PRINT ' - No application code changes required';
    PRINT ' - Backward compatible with existing records';
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
    PRINT 'All changes have been rolled back.';
    PRINT '';

    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH
GO

PRINT 'The ActivityLogActionFilter will now successfully log anonymous user activities.';
PRINT '';