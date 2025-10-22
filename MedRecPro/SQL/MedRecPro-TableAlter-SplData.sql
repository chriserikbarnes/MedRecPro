/***************************************************************************************************
*   DATABASE MODIFICATION SCRIPT
*
*   PURPOSE:
*   This script modifies the [dbo].[SplData] table to add support for content hash storage
*   for data integrity verification and caching optimization.
*
*   CHANGES:
*   1.  Table [dbo].[SplData]:
*       - Adds the [SplXMLHash] column if it doesn't exist.
*       - Adds or updates MS_Description extended properties for the new column.
*
*   NOTES:
*   - The script is idempotent and can be run multiple times safely.
*   - New column supports hex hash storage (64 characters) for content verification.
*   - Hash typically represents SHA-256 or similar cryptographic hash of SplXML content.
*
***************************************************************************************************/

USE [MedRecLocal]
GO

BEGIN TRANSACTION;

BEGIN TRY

    -- ==========================================================================================
    --  Modify [dbo].[SplData] Table
    -- ==========================================================================================
    PRINT 'Modifying table [dbo].[SplData]...';

    -- Add SplXMLHash column
    PRINT ' -> Adding [SplXMLHash] column if not exists.';
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE Name = N'SplXMLHash' 
          AND Object_ID = Object_ID(N'dbo.SplData')
    )
        ALTER TABLE [dbo].[SplData] ADD [SplXMLHash] CHAR(64) NULL;

    -- Add/Update Extended Properties
    PRINT ' -> Updating extended properties for [dbo].[SplData].';

    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'SplData';
    DECLARE @ColumnName NVARCHAR(128);
    DECLARE @PropValue SQL_VARIANT;

    -- Update table description to reflect hash support
    SET @PropValue = N'Stores SPL (Structured Product Labeling) data records with XML content and metadata. Enhanced with content hash support for data integrity verification and caching optimization.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;

    -- Column: SplXMLHash
    SET @ColumnName = N'SplXMLHash';
    SET @PropValue = N'Hexadecimal hash value (64 characters) representing the cryptographic hash of the SplXML content. Used for data integrity verification, duplicate detection, and caching optimization. Typically contains SHA-256 hash in hexadecimal format. NULL values indicate hash has not been computed.';
    IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, 'COLUMN', @ColumnName))
        EXEC sp_updateextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;
    ELSE
        EXEC sp_addextendedproperty @name = N'MS_Description', @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName,
            @level2type = N'COLUMN', @level2name = @ColumnName;

    COMMIT TRANSACTION;
    PRINT 'Script completed successfully.';
    PRINT 'SplData table enhanced with content hash support.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'An error occurred. Transaction rolled back.';
    THROW;
END CATCH;
GO