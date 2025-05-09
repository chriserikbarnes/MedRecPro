/**********************************************************************************************
  File:      MedRecPro-TableCreation-Users.sql
  Purpose:   Create a fully documented Users table for SQL Server 2016+ using PascalCase fields.
             The design covers authentication, security, compliance, auditing, and preferences.
  Author:    Chris Barnes
  Created:   09 May 2025
  Notes:     • Uses IDENTITY for surrogate PK (UserID).  
             • All dates/times are stored in UTC (DATETIME2).  
             • Inline comments document the intent of every column.  
             • Filtered unique indexes enforce uniqueness only for active (non‑deleted) rows.  
 
**********************************************************************************************/

-- --------------------------------------------------------------------------------------------
-- Safety: drop the table if it already exists (for repeatable deployments / CI pipelines)
-- --------------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
    DROP TABLE dbo.Users;
GO

-- ============================================================================================
-- Users
-- ============================================================================================
CREATE TABLE dbo.Users
(
    /* -------------------------------------------------------------------------
       Primary key & identity
    ------------------------------------------------------------------------- */
    UserID               BIGINT         IDENTITY(1,1)           NOT NULL
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED,
    
    /* -------------------------------------------------------------------------
       Identity & contact info
    ------------------------------------------------------------------------- */
    CanonicalUsername    NVARCHAR(256)  NULL,       -- Case‑folded username for unique checks
    PhoneNumber          NVARCHAR(20)   NULL,       -- Optional phone number for 2FA / recovery
    DisplayName          NVARCHAR(256)  NULL,       -- Friendly name shown in the UI
    PrimaryEmail         NVARCHAR(320)  NOT NULL,   -- RFC 5322 max email length
    EmailVerifiedAt      DATETIME2      NULL,       -- NULL = not yet verified

    /* -------------------------------------------------------------------------
       Authentication & security
    ------------------------------------------------------------------------- */
    PasswordHash         NVARCHAR(500)  NULL,       -- Store ONLY the hash, never plaintext
    PasswordChangedAt    DATETIME2      NULL,       -- Track for breach response / rotation
    FailedLoginCount     INT            NOT NULL    CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    LockoutUntil         DATETIME2      NULL,       -- Account locked until this UTC time
    MfaEnabled           BIT            NOT NULL    CONSTRAINT DF_Users_MfaEnabled DEFAULT (0),
    MfaSecret            NVARCHAR(200)  NULL,       -- Encrypted TOTP / WebAuthn secret multi‑factor auth
    SecurityStamp        UNIQUEIDENTIFIER NOT NULL  CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID(),

    /* -------------------------------------------------------------------------
       Authorization & role‑based access
    ------------------------------------------------------------------------- */
    UserRole                 NVARCHAR(100)  NOT NULL    CONSTRAINT DF_Users_Role DEFAULT ('User'),
    UserPermissions          NVARCHAR(MAX)  NULL,       -- JSON blob of permissions (e.g., for RBAC)
    UserFollowing            NVARCHAR(MAX)  NULL,       -- JSON blob of objects this user follows (e.g., for social features)

    /* -------------------------------------------------------------------------
       Preferences & locale
    ------------------------------------------------------------------------- */
    Timezone             NVARCHAR(100)  NOT NULL    CONSTRAINT DF_Users_Timezone DEFAULT ('UTC'),
    Locale               NVARCHAR(20)   NOT NULL    CONSTRAINT DF_Users_Locale DEFAULT ('en-US'),
    NotificationSettings NVARCHAR(MAX)  NULL,       -- JSON blob of notification toggles
    UiTheme              NVARCHAR(50)   NULL,       -- Dark, Light, System, etc.

    /* -------------------------------------------------------------------------
       Terms & compliance
    ------------------------------------------------------------------------- */
    TosVersionAccepted   NVARCHAR(20)   NULL,       -- Stores ToS version string (e.g., 'v3.2')
    TosAcceptedAt        DATETIME2      NULL,       -- When ToS was accepted
    TosMarketingOptIn    BIT            NOT NULL    CONSTRAINT DF_Users_TosMarketingOptIn DEFAULT (0), -- GDPR opt-in for marketing emails
    TosEmailNotification BIT            NOT NULL    CONSTRAINT DF_Users_TosEmailNotification DEFAULT (0), -- User agreed to recieve email notification 

    /* -------------------------------------------------------------------------
       Lifecycle & auditing
    ------------------------------------------------------------------------- */
    CreatedAt            DATETIME2      NOT NULL    CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedByID          BIGINT         NULL,       -- FK to Users.UserID or system user
    UpdatedAt            DATETIME2      NULL,
    UpdatedBy            BIGINT         NULL,
    DeletedAt            DATETIME2      NULL,       -- Soft‑delete marker (GDPR “right to forget”)
    SuspendedAt          DATETIME2      NULL,       -- Explicit suspension separate from delete
    SuspensionReason     NVARCHAR(500)  NULL,
    LastLoginAt          DATETIME2      NULL,       -- Last successful login timestamp
    LastActivityAt       DATETIME2      NULL,       -- Last API / UI activity
    LastIpAddress        VARCHAR(45)    NULL        -- Enough for IPv6
);
GO

/* ============================================================================================
   Indexes & constraints
   ============================================================================================ */

/* 1. Ensure active usernames are unique (ignoring soft‑deleted rows) */
CREATE UNIQUE INDEX UX_Users_CanonicalUsername_Active
    ON dbo.Users (CanonicalUsername)
    WHERE DeletedAt IS NULL;
GO

/* 2. Ensure active primary emails are unique (ignoring soft‑deleted rows) */
CREATE UNIQUE INDEX UX_Users_PrimaryEmail_Active
    ON dbo.Users (PrimaryEmail)
    WHERE DeletedAt IS NULL;
GO

/* 3. Speed look‑ups by security stamp (token validation, session refresh) */
CREATE INDEX IX_Users_SecurityStamp
    ON dbo.Users (SecurityStamp);
GO

/* 4. partition recent vs. dormant accounts via LastActivityAt */
    CREATE INDEX IX_Users_LastActivityAt 
    ON dbo.Users (LastActivityAt);
GO

/**********************************************************************************************
  Purpose:   Add MS_Description extended properties to the dbo.Users table and all columns so
             that developers and DBAs can discover field‑level documentation directly from
             SQL Server Management Studio.
  Author:    Chris Barnes
  Created:   09 May 2025
  Notes:     • Uses sys.sp_addextendedproperty; if the property already exists you can swap the
               call to sys.sp_updateextendedproperty.
             • Extended properties surface in SSMS (right‑click → Properties → Extended Properties)
               and are queryable via sys.extended_properties.
**********************************************************************************************/

SET NOCOUNT ON;
GO

/* ============================================================================================
   Table‑level description
   ============================================================================================ */
EXEC sys.sp_addextendedproperty
     @name  = N'MS_Description',
     @value = N'Stores one row per application user, including authentication settings, contact details, preferences, compliance indicators, and full auditing metadata.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users';
GO

/* ============================================================================================
   Column‑level descriptions
   ============================================================================================ */
-- Primary key & identity
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Surrogate primary key (BIGINT IDENTITY). Never reuse or update.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UserID';
GO

-- Identity & contact info
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Lower‑/case‑folded username used for uniqueness checks and login.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'CanonicalUsername';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Password recovery/MFA',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'PhoneNumber';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Friendly name displayed in the UI; can be non‑unique and user‑modifiable.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'DisplayName';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Primary email address (RFC 5322 max length 320 chars).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'PrimaryEmail';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Timestamp when PrimaryEmail was verified; NULL until verification succeeds.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'EmailVerifiedAt';
GO

-- Authentication & security
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Password hash produced by PBKDF2/bcrypt/argon2id; plaintext is never stored.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'PasswordHash';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp of the most recent password change/reset.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'PasswordChangedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Consecutive failed login attempts since last success; reset on successful login.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'FailedLoginCount';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp until which the account is locked out after repeated failures.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'LockoutUntil';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Boolean flag (0/1) indicating whether multi‑factor authentication is required.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'MfaEnabled';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Encrypted TOTP seed or WebAuthn credential ID used to validate MFA challenges.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'MfaSecret';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Random GUID rotated on password/MFA changes; invalidates cached sessions.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'SecurityStamp';
GO

-- Authorization
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Coarse‑grained role for RBAC (e.g., User, Admin). Extend via lookup if needed.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UserRole';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'JSON blob of permissions (e.g., for RBAC).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UserPermissions';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'JSON blob of objects this user follows (e.g., for social features).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UserFollowing';
GO

-- Preferences & locale
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'IANA timezone identifier to localize dates/times in the UI.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'Timezone';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Locale/region for content localization (e.g., en-US, fr-FR).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'Locale';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'JSON‑encoded set of email/SMS/push notification preferences.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'NotificationSettings';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Preferred UI theme (Dark, Light, System, ReducedMotion, etc.).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UiTheme';
GO

-- Terms & compliance
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Terms of Service version the user accepted (e.g., v3.2).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'TosVersionAccepted';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp when the user accepted the current Terms of Service.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'TosAcceptedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Bit noting that the user opted-int for email marketing.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'TosMarketingOptIn';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Bit noting that the user agrees to email notifications.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'TosEmailNotification';
GO

-- Lifecycle & auditing
EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Record creation timestamp (UTC).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'CreatedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UserID or system process that created the record; NULL for self‑registration.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'CreatedByID';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp of the most recent profile update.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UpdatedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UserID or system process that performed the most recent update.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'UpdatedBy';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Soft‑delete marker (UTC); non‑NULL rows are invisible to normal queries.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'DeletedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Timestamp when the account was administratively suspended.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'SuspendedAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'Reason provided for suspension (policy violation, fraud, etc.).',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'SuspensionReason';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp of the last successful login.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'LastLoginAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'UTC timestamp of the user''s most recent API or UI activity.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'LastActivityAt';
GO

EXEC sys.sp_addextendedproperty
     @name = N'MS_Description',
     @value = N'IP address (IPv4/IPv6) recorded at last login; can be anonymized per policy.',
     @level0type  = N'SCHEMA', @level0name  = N'dbo',
     @level1type  = N'TABLE',  @level1name  = N'Users',
     @level2type  = N'COLUMN', @level2name  = N'LastIpAddress';
GO

/**********************************************************************************************
  End of extended‑property script
**********************************************************************************************/


/**********************************************************************************************
  End of script
**********************************************************************************************/
