/*******************************************************************************
 * Create_AspNetUserFavorite.sql
 *
 * Idempotent DDL for authenticated AE dashboard product favorites.
 * Each row = one user-saved dashboard product identified by SPL DocumentGUID.
 *
 * Re-runnable: schema/table/column/index guards only. No destructive drops.
 ******************************************************************************/
USE MedRecProDB
IF NOT EXISTS (SELECT * FROM sys.tables AS t INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'AspNetUsers')
BEGIN
    RAISERROR('dbo.AspNetUsers must exist before dbo.AspNetUserFavorite can be created.', 16, 1);
END
ELSE IF NOT EXISTS (SELECT * FROM sys.tables AS t INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'AspNetUserFavorite')
BEGIN
    CREATE TABLE dbo.AspNetUserFavorite (
        AspNetUserFavoriteID BIGINT IDENTITY(1,1) NOT NULL,
        UserId BIGINT NOT NULL,
        DocumentGUID UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AspNetUserFavorite_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,

        CONSTRAINT PK_AspNetUserFavorite PRIMARY KEY CLUSTERED (AspNetUserFavoriteID ASC),
        CONSTRAINT FK_AspNetUserFavorite_AspNetUsers_UserId
            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );
END

-- Idempotent column additions for existing databases (forward-compatible upgrades).
IF OBJECT_ID('dbo.AspNetUserFavorite', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.AspNetUserFavorite', 'UserId') IS NULL
        ALTER TABLE dbo.AspNetUserFavorite ADD UserId BIGINT NOT NULL DEFAULT 0;
    IF COL_LENGTH('dbo.AspNetUserFavorite', 'DocumentGUID') IS NULL
        ALTER TABLE dbo.AspNetUserFavorite ADD DocumentGUID UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH('dbo.AspNetUserFavorite', 'CreatedAt') IS NULL
        ALTER TABLE dbo.AspNetUserFavorite ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AspNetUserFavorite_CreatedAt DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH('dbo.AspNetUserFavorite', 'UpdatedAt') IS NULL
        ALTER TABLE dbo.AspNetUserFavorite ADD UpdatedAt DATETIME2 NULL;

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetUserFavorite') AND name = 'UX_AspNetUserFavorite_User_Document')
        CREATE UNIQUE NONCLUSTERED INDEX UX_AspNetUserFavorite_User_Document ON dbo.AspNetUserFavorite(UserId, DocumentGUID);
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetUserFavorite') AND name = 'IX_AspNetUserFavorite_UserId')
        CREATE NONCLUSTERED INDEX IX_AspNetUserFavorite_UserId ON dbo.AspNetUserFavorite(UserId) INCLUDE (DocumentGUID, CreatedAt);
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AspNetUserFavorite') AND name = 'IX_AspNetUserFavorite_DocumentGUID')
        CREATE NONCLUSTERED INDEX IX_AspNetUserFavorite_DocumentGUID ON dbo.AspNetUserFavorite(DocumentGUID);
END
