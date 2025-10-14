/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script creates the [dbo].[DocumentRelationshipIdentifier] table to preserve
*   which organization identifiers were used at which hierarchy levels in SPL documents.
*
*   CHANGES:
*   1.  Table [dbo].[DocumentRelationshipIdentifier]:
*       - Creates the table if it doesn't exist with primary key and foreign key columns.
*       - NO database foreign key constraints (relationships managed in application code).
*       - Adds indexes for query performance.
*       - Adds unique composite index to prevent duplicate identifier-relationship links.
*       - Adds MS_Description extended properties for the table and its columns.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - Foreign key relationships exist logically but NOT as database constraints.
*   - Referential integrity must be maintained in application code.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Create [dbo].[DocumentRelationshipIdentifier] Table
    -- ==========================================================================================
    PRINT 'Creating/verifying table [dbo].[DocumentRelationshipIdentifier]...';

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DocumentRelationshipIdentifier' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating table [dbo].[DocumentRelationshipIdentifier].';
        
        CREATE TABLE [dbo].[DocumentRelationshipIdentifier] (
            [DocumentRelationshipIdentifierID] INT IDENTITY(1,1) NOT NULL,
            [DocumentRelationshipID] INT NULL,
            [OrganizationIdentifierID] INT NULL,
            
            CONSTRAINT [PK_DocumentRelationshipIdentifier] PRIMARY KEY CLUSTERED 
            (
                [DocumentRelationshipIdentifierID] ASC
            ) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
        ) ON [PRIMARY];

        PRINT ' -> Table [dbo].[DocumentRelationshipIdentifier] created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table [dbo].[DocumentRelationshipIdentifier] already exists.';
    END

    -- ==========================================================================================
    --  Create Indexes for Query Performance
    -- ==========================================================================================
    PRINT ' -> Creating indexes on [dbo].[DocumentRelationshipIdentifier].';

    -- Index on DocumentRelationshipID for lookups by relationship
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'IX_DocumentRelationshipIdentifier_DocumentRelationshipID' 
                   AND object_id = OBJECT_ID('dbo.DocumentRelationshipIdentifier'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_DocumentRelationshipID] 
        ON [dbo].[DocumentRelationshipIdentifier] ([DocumentRelationshipID])
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, 
              DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON);
        
        PRINT '    -> Created index: IX_DocumentRelationshipIdentifier_DocumentRelationshipID';
    END

    -- Index on OrganizationIdentifierID for lookups by identifier
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'IX_DocumentRelationshipIdentifier_OrganizationIdentifierID' 
                   AND object_id = OBJECT_ID('dbo.DocumentRelationshipIdentifier'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_OrganizationIdentifierID] 
        ON [dbo].[DocumentRelationshipIdentifier] ([OrganizationIdentifierID])
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, 
              DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON);
        
        PRINT '    -> Created index: IX_DocumentRelationshipIdentifier_OrganizationIdentifierID';
    END

    -- Unique composite index to prevent duplicate links
    IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                   WHERE name = 'UX_DocumentRelationshipIdentifier_Unique' 
                   AND object_id = OBJECT_ID('dbo.DocumentRelationshipIdentifier'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_DocumentRelationshipIdentifier_Unique] 
        ON [dbo].[DocumentRelationshipIdentifier] 
        (
            [DocumentRelationshipID] ASC,
            [OrganizationIdentifierID] ASC
        )
        WHERE ([DocumentRelationshipID] IS NOT NULL AND [OrganizationIdentifierID] IS NOT NULL)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, 
              IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, 
              ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON);
        
        PRINT '    -> Created unique index: UX_DocumentRelationshipIdentifier_Unique';
    END

    -- ==========================================================================================
    --  Add/Update Extended Properties (MS_Description)
    -- ==========================================================================================
    PRINT ' -> Updating extended properties for [dbo].[DocumentRelationshipIdentifier].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128);
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Table: DocumentRelationshipIdentifier
    SET @TableName = N'DocumentRelationshipIdentifier';
    SET @PropValue = N'Links organization identifiers to specific document relationships, preserving which identifier (e.g., DUNS number) was used at which hierarchy level in the SPL author section. Enables accurate rendering that matches the original XML structure. No database FK constraints for performance; referential integrity maintained in application code.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue, @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- Column: DocumentRelationshipIdentifierID
    SET @ColumnName = N'DocumentRelationshipIdentifierID';
    SET @PropValue = N'Primary key for the DocumentRelationshipIdentifier table. Auto-incrementing identity column.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: DocumentRelationshipID
    SET @ColumnName = N'DocumentRelationshipID';
    SET @PropValue = N'Foreign key to DocumentRelationship table. Identifies which document relationship (hierarchy level) this identifier was used in. Logical relationship only - no database FK constraint. Example: links to the registrant-to-establishment relationship where DUNS 830995189 appeared.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    -- Column: OrganizationIdentifierID
    SET @ColumnName = N'OrganizationIdentifierID';
    SET @PropValue = N'Foreign key to OrganizationIdentifier table. Identifies which specific identifier (DUNS, FEI, etc.) appeared at this hierarchy level in the original XML. Logical relationship only - no database FK constraint. Example: links to the OrganizationIdentifier record containing DUNS 830995189 for Henry Schein.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name=N'MS_Description', @value=@PropValue, @level0type=N'SCHEMA', @level0name=@SchemaName, @level1type=N'TABLE', @level1name=@TableName, @level2type=N'COLUMN', @level2name=@ColumnName;

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';
    PRINT '';
    PRINT '======================================================================';
    PRINT 'IMPORTANT: This table uses logical relationships without FK constraints.';
    PRINT 'Application code must maintain referential integrity.';
    PRINT '======================================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    PRINT 'Error Details:';
    PRINT '  Error Number: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
    PRINT '  Error Message: ' + ERROR_MESSAGE();
    PRINT '  Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    
    -- Raise the original error to the client
    THROW;
END CATCH;
GO

-- ==========================================================================================
--  Verification Query
-- ==========================================================================================
PRINT '';
PRINT 'Verification: Checking table structure...';
PRINT '';

SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.DocumentRelationshipIdentifier')
ORDER BY c.column_id;

PRINT '';
PRINT 'Verification: Checking indexes...';
PRINT '';

SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS IndexColumns
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('dbo.DocumentRelationshipIdentifier')
  AND i.name IS NOT NULL
ORDER BY i.name;

PRINT '';
PRINT 'Setup complete!';
GO