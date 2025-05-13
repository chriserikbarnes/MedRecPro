-- Automatically manage CreatedAt, UpdatedAt timestamps for all entities
-- This script creates database triggers to enforce audit trail requirements

-- Safety: Check if trigger exists before attempting to create
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Users_InsertUpdateTimestamps')
BEGIN
    DROP TRIGGER [dbo].[TR_Users_InsertUpdateTimestamps];
    PRINT 'Dropped existing Users timestamp trigger';
END;
GO

-- Create trigger to manage timestamps on Users table
CREATE TRIGGER [dbo].[TR_Users_InsertUpdateTimestamps]
ON [dbo].[Users]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Set CreatedAt for new rows if not specified
    UPDATE [dbo].[Users]
    SET 
        [CreatedAt] = COALESCE(i.[CreatedAt], SYSUTCDATETIME()),
        [SecurityStamp] = COALESCE(i.[SecurityStamp], NEWID())
    FROM 
        [dbo].[Users] u
    INNER JOIN 
        inserted i ON u.[UserID] = i.[UserID]
    WHERE 
        i.[CreatedAt] IS NULL OR i.[SecurityStamp] IS NULL;
    
    -- Set UpdatedAt for updated rows
    IF UPDATE([CanonicalUsername]) OR 
       UPDATE([PhoneNumber]) OR 
       UPDATE([DisplayName]) OR 
       UPDATE([PrimaryEmail]) OR 
       UPDATE([EmailVerifiedAt]) OR 
       UPDATE([PasswordHash]) OR 
       UPDATE([PasswordChangedAt]) OR 
       UPDATE([FailedLoginCount]) OR 
       UPDATE([LockoutUntil]) OR 
       UPDATE([MfaEnabled]) OR 
       UPDATE([MfaSecret]) OR 
       UPDATE([UserRole]) OR 
       UPDATE([UserPermissions]) OR 
       UPDATE([UserFollowing]) OR 
       UPDATE([Timezone]) OR 
       UPDATE([Locale]) OR 
       UPDATE([NotificationSettings]) OR 
       UPDATE([UiTheme]) OR 
       UPDATE([TosVersionAccepted]) OR 
       UPDATE([TosAcceptedAt]) OR 
       UPDATE([TosMarketingOptIn]) OR 
       UPDATE([TosEmailNotification]) OR 
       UPDATE([SuspendedAt]) OR 
       UPDATE([SuspensionReason]) OR 
       UPDATE([DeletedAt])
    BEGIN
        UPDATE [dbo].[Users]
        SET 
            [UpdatedAt] = SYSUTCDATETIME()
        FROM 
            [dbo].[Users] u
        INNER JOIN 
            inserted i ON u.[UserID] = i.[UserID]
        WHERE 
            NOT UPDATE([UpdatedAt]); -- Skip if UpdatedAt is explicitly being set
    END;
END;
GO

PRINT 'Created Users timestamp trigger';
GO