/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[TerritorialAuthority] table to align it with the 
*   corresponding Entity Framework (EF) model.
*
*   CHANGES:
*   1.  Table [dbo].[TerritorialAuthority]:
*       - Drops [GoverningAgencyOrgID] column if it exists.
*       - Adds new columns with definitions if they don't exist.
*       - Adds or updates MS_Description extended properties.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    PRINT 'Modifying table [dbo].[TerritorialAuthority]...';

    -- Drop deprecated column
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'GoverningAgencyOrgID' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority'))
        ALTER TABLE [dbo].[TerritorialAuthority] DROP COLUMN [GoverningAgencyOrgID];

    -- Add new columns if they do not exist
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TerritoryCode' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority')) 
        ALTER TABLE [dbo].[TerritorialAuthority] ADD [TerritoryCode] NVARCHAR(64) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TerritoryCodeSystem' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority')) 
        ALTER TABLE [dbo].[TerritorialAuthority] ADD [TerritoryCodeSystem] NVARCHAR(64) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'GoverningAgencyIdExtension' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority')) 
        ALTER TABLE [dbo].[TerritorialAuthority] ADD [GoverningAgencyIdExtension] NVARCHAR(64) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'GoverningAgencyIdRoot' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority')) 
        ALTER TABLE [dbo].[TerritorialAuthority] ADD [GoverningAgencyIdRoot] NVARCHAR(64) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'GoverningAgencyName' AND Object_ID = Object_ID(N'dbo.TerritorialAuthority')) 
        ALTER TABLE [dbo].[TerritorialAuthority] ADD [GoverningAgencyName] NVARCHAR(256) NULL;

    -- Extended properties for table
    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'TerritorialAuthority';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Update table-level description
    SET @PropValue = N'Represents the issuing authority (State or Federal Agency like DEA) for licenses ([author][territorialAuthority]). Based on Section 18.1.5.';
    IF EXISTS (
        SELECT 1 FROM sys.extended_properties 
        WHERE name = N'MS_Description' AND 
              major_id = OBJECT_ID(@SchemaName + '.' + @TableName) AND 
              minor_id = 0 AND 
              class = 1
    )
        EXEC sp_updateextendedproperty 
            @name = N'MS_Description', 
            @value = @PropValue, 
            @level0type = N'SCHEMA', @level0name = @SchemaName, 
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty 
            @name = N'MS_Description', 
            @value = @PropValue, 
            @level0type = N'SCHEMA', @level0name = @SchemaName, 
            @level1type = N'TABLE', @level1name = @TableName;

    -- Helper to update or add a column-level description
    DECLARE @Descriptions TABLE(ColumnName NVARCHAR(128), Description NVARCHAR(4000));

    INSERT INTO @Descriptions (ColumnName, Description)
    VALUES 
        (N'TerritoryCode', N'ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA). Used to identify the territorial scope of the licensing authority.'),
        (N'TerritoryCodeSystem', N'Code system OID for the territory code (e.g., ''1.0.3166.2'' for state, ''1.0.3166.1.2.3'' for country).'),
        (N'GoverningAgencyIdExtension', N'DUNS number of the federal governing agency (e.g., "004234790" for DEA). Required when territory code is "USA", prohibited otherwise.'),
        (N'GoverningAgencyIdRoot', N'Root OID for governing agency identification ("1.3.6.1.4.1.519.1").'),
        (N'GoverningAgencyName', N'Name of the federal governing agency (e.g., "DEA"). Required when territory code is "USA", prohibited otherwise.');

    DECLARE desc_cursor CURSOR FOR 
        SELECT ColumnName, Description FROM @Descriptions;

    OPEN desc_cursor;

    FETCH NEXT FROM desc_cursor INTO @ColumnName, @PropValue;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF EXISTS (
            SELECT 1 FROM sys.extended_properties 
            WHERE name = N'MS_Description' 
            AND major_id = OBJECT_ID(@SchemaName + '.' + @TableName) 
            AND minor_id = (SELECT column_id FROM sys.columns WHERE object_id = OBJECT_ID(@SchemaName + '.' + @TableName) AND name = @ColumnName)
            AND class = 1
        )
            EXEC sp_updateextendedproperty 
                @name = N'MS_Description', 
                @value = @PropValue, 
                @level0type = N'SCHEMA', @level0name = @SchemaName, 
                @level1type = N'TABLE',  @level1name = @TableName, 
                @level2type = N'COLUMN', @level2name = @ColumnName;
        ELSE
            EXEC sp_addextendedproperty 
                @name = N'MS_Description', 
                @value = @PropValue, 
                @level0type = N'SCHEMA', @level0name = @SchemaName, 
                @level1type = N'TABLE',  @level1name = @TableName, 
                @level2type = N'COLUMN', @level2name = @ColumnName;

        FETCH NEXT FROM desc_cursor INTO @ColumnName, @PropValue;
    END

    CLOSE desc_cursor;
    DEALLOCATE desc_cursor;

    COMMIT TRANSACTION;
    PRINT 'TerritorialAuthority table updated successfully.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Error occurred during TerritorialAuthority table update.';
    THROW;
END CATCH;
GO
