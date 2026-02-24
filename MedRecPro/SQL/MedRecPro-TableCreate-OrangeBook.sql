/***************************************************************
 * Migration Script: Create Orange Book Tables
 * Purpose: Creates normalized tables for FDA Orange Book data
 *          sourced from the three published text files:
 *          products.txt, patent.txt, and exclusivity.txt.
 *
 * Tables Created:
 * - OrangeBookApplicant        Lookup for pharmaceutical companies
 * - OrangeBookProduct          Central product fact table (products.txt)
 * - OrangeBookPatent           Patent records per product (patent.txt)
 * - OrangeBookExclusivity      Exclusivity periods per product (exclusivity.txt)
 * - OrangeBookProductMarketingCategory    Junction: OB Product <-> MarketingCategory
 * - OrangeBookProductIngredientSubstance  Junction: OB Product <-> IngredientSubstance
 * - OrangeBookApplicantOrganization       Junction: OB Applicant <-> Organization
 * - OrangeBookPatentUseCode    Lookup for patent use code definitions
 *
 * Normalization:
 * - Applicant short name + full name extracted to lookup table
 * - DF;Route column split into DosageForm and Route
 * - Yes/No and Y/NULL flags converted to BIT
 * - "Approved Prior to Jan 1, 1982" handled via separate BIT flag
 * - Natural keys (ApplType + ApplNo + ProductNo) preserved on
 *   child tables for import matching
 *
 * Dependencies:
 * - None (no foreign key constraints created)
 * - Junction tables reference existing MarketingCategory,
 *   IngredientSubstance, and Organization tables
 *
 * Backwards Compatibility:
 * - New tables only, no impact on existing tables
 ***************************************************************/

SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Creating Orange Book Tables';
    PRINT '========================================';
    PRINT '';

    ---------------------------------------------------------------------------
    -- 1. OrangeBookApplicant
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookApplicant' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookApplicant] table...';

        CREATE TABLE [dbo].[OrangeBookApplicant] (
            -- Primary Key
            [OrangeBookApplicantID] INT IDENTITY(1,1) NOT NULL,

            -- Applicant Information
            [ApplicantName]     VARCHAR(200)  NOT NULL,
            [ApplicantFullName] VARCHAR(500)  NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookApplicant] PRIMARY KEY CLUSTERED ([OrangeBookApplicantID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookApplicant]';
        PRINT '    - Skipping table creation.';
    END

    -- Indexes for OrangeBookApplicant
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OrangeBookApplicant_ApplicantName' AND object_id = OBJECT_ID('dbo.OrangeBookApplicant'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_OrangeBookApplicant_ApplicantName]
            ON [dbo].[OrangeBookApplicant]([ApplicantName]);
        PRINT '    - Created index: UX_OrangeBookApplicant_ApplicantName';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: UX_OrangeBookApplicant_ApplicantName';
    END

    -- Extended properties for OrangeBookApplicant
    DECLARE @SchemaName NVARCHAR(128) = N'dbo';
    DECLARE @TableName NVARCHAR(128) = N'OrangeBookApplicant';
    DECLARE @PropValue SQL_VARIANT;

    SET @PropValue = N'Lookup table for pharmaceutical companies that hold FDA application approvals. Sourced from the Applicant and Applicant_Full_Name columns in the Orange Book products.txt file. Eliminates repetition of applicant names across product rows.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DECLARE @ColumnDescriptions TABLE (
        ColumnName NVARCHAR(128),
        Description NVARCHAR(500)
    );

    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookApplicantID', N'Surrogate primary key (auto-increment)'),
        (N'ApplicantName', N'Short applicant name/code as published in the Orange Book (max 20 chars, e.g., "TEVA", "SALIX"). Maps to Applicant column in products.txt. Serves as the natural key for import deduplication.'),
        (N'ApplicantFullName', N'Full legal name of the applicant company (e.g., "TEVA PHARMACEUTICALS USA INC"). Maps to Applicant_Full_Name column in products.txt.');

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

    ---------------------------------------------------------------------------
    -- 2. OrangeBookProduct
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookProduct' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookProduct] table...';

        CREATE TABLE [dbo].[OrangeBookProduct] (
            -- Primary Key
            [OrangeBookProductID] INT IDENTITY(1,1) NOT NULL,

            -- Natural Key (Application Identity)
            [ApplType]   CHAR(1)     NOT NULL,
            [ApplNo]     VARCHAR(6)  NOT NULL,
            [ProductNo]  CHAR(3)     NOT NULL,

            -- Product Information
            [Ingredient]  VARCHAR(1000) NULL,
            [DosageForm]  VARCHAR(255)  NULL,
            [Route]       VARCHAR(255)  NULL,
            [TradeName]   VARCHAR(500)  NULL,
            [Strength]    VARCHAR(500)  NULL,

            -- Applicant Reference
            [OrangeBookApplicantID] INT NULL,

            -- Classification and Equivalence
            [TECode] VARCHAR(20) NULL,
            [Type]   VARCHAR(10) NULL,

            -- Approval Information
            [ApprovalDate]             DATE NULL,
            [ApprovalDateIsPremarket]  BIT  NOT NULL DEFAULT 0,

            -- Reference Flags
            [IsRLD] BIT NOT NULL DEFAULT 0,
            [IsRS]  BIT NOT NULL DEFAULT 0,

            -- Constraints
            CONSTRAINT [PK_OrangeBookProduct] PRIMARY KEY CLUSTERED ([OrangeBookProductID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookProduct]';
        PRINT '    - Skipping table creation.';
    END

    PRINT '';
    PRINT ' -> Creating indexes for [dbo].[OrangeBookProduct]...';

    -- Unique natural compound key
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OrangeBookProduct_ApplType_ApplNo_ProductNo' AND object_id = OBJECT_ID('dbo.OrangeBookProduct'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_OrangeBookProduct_ApplType_ApplNo_ProductNo]
            ON [dbo].[OrangeBookProduct]([ApplType], [ApplNo], [ProductNo]);
        PRINT '    - Created index: UX_OrangeBookProduct_ApplType_ApplNo_ProductNo';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: UX_OrangeBookProduct_ApplType_ApplNo_ProductNo';
    END

    -- Applicant lookup
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookProduct_OrangeBookApplicantID' AND object_id = OBJECT_ID('dbo.OrangeBookProduct'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookProduct_OrangeBookApplicantID]
            ON [dbo].[OrangeBookProduct]([OrangeBookApplicantID])
            WHERE [OrangeBookApplicantID] IS NOT NULL;
        PRINT '    - Created index: IX_OrangeBookProduct_OrangeBookApplicantID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookProduct_OrangeBookApplicantID';
    END

    -- Trade name search
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookProduct_TradeName' AND object_id = OBJECT_ID('dbo.OrangeBookProduct'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookProduct_TradeName]
            ON [dbo].[OrangeBookProduct]([TradeName]);
        PRINT '    - Created index: IX_OrangeBookProduct_TradeName';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookProduct_TradeName';
    END

    -- Application number lookup
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookProduct_ApplNo' AND object_id = OBJECT_ID('dbo.OrangeBookProduct'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookProduct_ApplNo]
            ON [dbo].[OrangeBookProduct]([ApplNo]);
        PRINT '    - Created index: IX_OrangeBookProduct_ApplNo';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookProduct_ApplNo';
    END

    -- Extended properties for OrangeBookProduct
    SET @TableName = N'OrangeBookProduct';

    SET @PropValue = N'Central fact table storing FDA-approved drug products from the Orange Book products.txt file. Each row represents one product number under one application number. The composite natural key (ApplType, ApplNo, ProductNo) uniquely identifies each record.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookProductID', N'Surrogate primary key (auto-increment)'),
        (N'ApplType', N'Application type: "N" for NDA (New Drug Application / innovator) or "A" for ANDA (Abbreviated New Drug Application / generic). Maps to Appl_Type in products.txt.'),
        (N'ApplNo', N'FDA-assigned application number, zero-padded to 6 digits (e.g., "020610"). Maps to Appl_No in products.txt. Stored as varchar to preserve leading zeros.'),
        (N'ProductNo', N'FDA-assigned product number within the application, always 3 digits (e.g., "001"). Each strength is a separate product. Maps to Product_No in products.txt.'),
        (N'Ingredient', N'Active ingredient(s) as published. Multiple ingredients are semicolon-delimited in alphabetical order (e.g., "ACETAMINOPHEN; CODEINE PHOSPHATE"). Maps to Ingredient in products.txt.'),
        (N'DosageForm', N'Pharmaceutical dosage form, extracted from the portion before the semicolon in the DF;Route source column (e.g., "TABLET", "AEROSOL, FOAM").'),
        (N'Route', N'Route of administration, extracted from the portion after the semicolon in the DF;Route source column (e.g., "ORAL", "TOPICAL", "RECTAL").'),
        (N'TradeName', N'Proprietary/brand name of the drug product as shown on the labeling (e.g., "LIPITOR", "UCERIS"). Maps to Trade_Name in products.txt.'),
        (N'Strength', N'Potency of the active ingredient as published. May repeat for multi-part products (e.g., "10MG", "2MG/ACTUATION"). Maps to Strength in products.txt.'),
        (N'OrangeBookApplicantID', N'References OrangeBookApplicant. Resolved during import by matching the Applicant short name from products.txt. No foreign key constraint enforced.'),
        (N'TECode', N'Therapeutic Equivalence evaluation code (e.g., "AB", "AP", "BX"). Indicates the TE rating of generic to innovator products. Maps to TE_Code in products.txt.'),
        (N'Type', N'Product type classification: "RX" (prescription), "OTC" (over-the-counter), or "DISCN" (discontinued). Maps to Type in products.txt.'),
        (N'ApprovalDate', N'FDA approval date as stated in the approval letter. NULL when the product was approved prior to Jan 1, 1982. Maps to Approval_Date in products.txt.'),
        (N'ApprovalDateIsPremarket', N'Set to 1 when the source Approval_Date text equals "Approved Prior to Jan 1, 1982" (approval date not recorded). Default 0.'),
        (N'IsRLD', N'Reference Listed Drug flag. 1 = product is an RLD approved under section 505(c) of the FD&C Act. Converted from "Yes"/"No" in products.txt RLD column. Default 0.'),
        (N'IsRS', N'Reference Standard flag. 1 = product is the reference standard selected by FDA for ANDA bioequivalence studies. Converted from "Yes"/"No" in products.txt RS column. Default 0.');

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

    ---------------------------------------------------------------------------
    -- 3. OrangeBookPatent
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookPatent' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookPatent] table...';

        CREATE TABLE [dbo].[OrangeBookPatent] (
            -- Primary Key
            [OrangeBookPatentID] INT IDENTITY(1,1) NOT NULL,

            -- Product Reference
            [OrangeBookProductID] INT NULL,

            -- Natural Key (for import matching)
            [ApplType]   CHAR(1)    NOT NULL,
            [ApplNo]     VARCHAR(6) NOT NULL,
            [ProductNo]  CHAR(3)    NOT NULL,

            -- Patent Information
            [PatentNo]         VARCHAR(17) NOT NULL,
            [PatentExpireDate] DATE        NULL,
            [PatentUseCode]    VARCHAR(20) NULL,

            -- Patent Claim Flags
            [DrugSubstanceFlag] BIT NOT NULL DEFAULT 0,
            [DrugProductFlag]   BIT NOT NULL DEFAULT 0,

            -- Administrative
            [DelistFlag]     BIT  NOT NULL DEFAULT 0,
            [SubmissionDate] DATE NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookPatent] PRIMARY KEY CLUSTERED ([OrangeBookPatentID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookPatent]';
        PRINT '    - Skipping table creation.';
    END

    PRINT '';
    PRINT ' -> Creating indexes for [dbo].[OrangeBookPatent]...';

    -- Product lookup (filtered)
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_OrangeBookProductID' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_OrangeBookProductID]
            ON [dbo].[OrangeBookPatent]([OrangeBookProductID])
            INCLUDE ([PatentNo], [PatentExpireDate])
            WHERE [OrangeBookProductID] IS NOT NULL;
        PRINT '    - Created index: IX_OrangeBookPatent_OrangeBookProductID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookPatent_OrangeBookProductID';
    END

    -- Import matching via natural key
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_ApplType_ApplNo_ProductNo' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_ApplType_ApplNo_ProductNo]
            ON [dbo].[OrangeBookPatent]([ApplType], [ApplNo], [ProductNo])
            INCLUDE ([PatentNo]);
        PRINT '    - Created index: IX_OrangeBookPatent_ApplType_ApplNo_ProductNo';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookPatent_ApplType_ApplNo_ProductNo';
    END

    -- Patent number search
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookPatent_PatentNo' AND object_id = OBJECT_ID('dbo.OrangeBookPatent'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookPatent_PatentNo]
            ON [dbo].[OrangeBookPatent]([PatentNo])
            INCLUDE ([OrangeBookProductID], [PatentExpireDate]);
        PRINT '    - Created index: IX_OrangeBookPatent_PatentNo';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookPatent_PatentNo';
    END

    -- Extended properties for OrangeBookPatent
    SET @TableName = N'OrangeBookPatent';

    SET @PropValue = N'Stores patent records associated with Orange Book products. Sourced from patent.txt. One product can have multiple patents. Natural keys (ApplType, ApplNo, ProductNo) are preserved for import matching before surrogate FK resolution.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookPatentID', N'Surrogate primary key (auto-increment)'),
        (N'OrangeBookProductID', N'References OrangeBookProduct. Resolved during import by matching ApplType + ApplNo + ProductNo. No foreign key constraint enforced.'),
        (N'ApplType', N'Application type, matches the parent product. Preserved for import matching. Maps to Appl_Type in patent.txt.'),
        (N'ApplNo', N'FDA application number. Preserved for import matching. Maps to Appl_No in patent.txt.'),
        (N'ProductNo', N'Product number within the application. Preserved for import matching. Maps to Product_No in patent.txt.'),
        (N'PatentNo', N'U.S. patent number (7 to 11 digits) as submitted by the applicant holder. Maps to Patent_No in patent.txt.'),
        (N'PatentExpireDate', N'Patent expiration date including applicable extensions. Parsed from Patent_Expire_Date_Text (MMM DD, YYYY format) in patent.txt.'),
        (N'PatentUseCode', N'Code designating a use patent covering the approved indication (e.g., "U-141", "U-2261"). Maps to Patent_Use_Code in patent.txt.'),
        (N'DrugSubstanceFlag', N'Indicates patent covers the drug substance (listed after Aug 18, 2003). Converted from "Y"/NULL in Drug_Substance_Flag. Default 0.'),
        (N'DrugProductFlag', N'Indicates patent covers the drug product formulation (listed after Aug 18, 2003). Converted from "Y"/NULL in Drug_Product_Flag. Default 0.'),
        (N'DelistFlag', N'Sponsor has requested patent be delisted per Section 505(j)(5)(D)(i). Converted from "Y"/NULL in Delist_Flag. Default 0.'),
        (N'SubmissionDate', N'Date the FDA received patent information from the NDA holder. Parsed from Submission_Date (MMM DD, YYYY format) in patent.txt.');

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

    ---------------------------------------------------------------------------
    -- 4. OrangeBookExclusivity
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookExclusivity' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookExclusivity] table...';

        CREATE TABLE [dbo].[OrangeBookExclusivity] (
            -- Primary Key
            [OrangeBookExclusivityID] INT IDENTITY(1,1) NOT NULL,

            -- Product Reference
            [OrangeBookProductID] INT NULL,

            -- Natural Key (for import matching)
            [ApplType]   CHAR(1)    NOT NULL,
            [ApplNo]     VARCHAR(6) NOT NULL,
            [ProductNo]  CHAR(3)    NOT NULL,

            -- Exclusivity Information
            [ExclusivityCode] VARCHAR(10) NOT NULL,
            [ExclusivityDate] DATE        NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookExclusivity] PRIMARY KEY CLUSTERED ([OrangeBookExclusivityID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookExclusivity]';
        PRINT '    - Skipping table creation.';
    END

    PRINT '';
    PRINT ' -> Creating indexes for [dbo].[OrangeBookExclusivity]...';

    -- Product lookup (filtered)
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookExclusivity_OrangeBookProductID' AND object_id = OBJECT_ID('dbo.OrangeBookExclusivity'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookExclusivity_OrangeBookProductID]
            ON [dbo].[OrangeBookExclusivity]([OrangeBookProductID])
            INCLUDE ([ExclusivityCode], [ExclusivityDate])
            WHERE [OrangeBookProductID] IS NOT NULL;
        PRINT '    - Created index: IX_OrangeBookExclusivity_OrangeBookProductID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookExclusivity_OrangeBookProductID';
    END

    -- Import matching via natural key
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookExclusivity_ApplType_ApplNo_ProductNo' AND object_id = OBJECT_ID('dbo.OrangeBookExclusivity'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookExclusivity_ApplType_ApplNo_ProductNo]
            ON [dbo].[OrangeBookExclusivity]([ApplType], [ApplNo], [ProductNo])
            INCLUDE ([ExclusivityCode]);
        PRINT '    - Created index: IX_OrangeBookExclusivity_ApplType_ApplNo_ProductNo';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookExclusivity_ApplType_ApplNo_ProductNo';
    END

    -- Exclusivity type analysis
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrangeBookExclusivity_ExclusivityCode' AND object_id = OBJECT_ID('dbo.OrangeBookExclusivity'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrangeBookExclusivity_ExclusivityCode]
            ON [dbo].[OrangeBookExclusivity]([ExclusivityCode])
            INCLUDE ([OrangeBookProductID], [ExclusivityDate]);
        PRINT '    - Created index: IX_OrangeBookExclusivity_ExclusivityCode';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OrangeBookExclusivity_ExclusivityCode';
    END

    -- Extended properties for OrangeBookExclusivity
    SET @TableName = N'OrangeBookExclusivity';

    SET @PropValue = N'Stores marketing exclusivity records associated with Orange Book products. Sourced from exclusivity.txt. One product can have multiple exclusivity periods with different codes. Common codes include NCE (New Chemical Entity), ODE (Orphan Drug), and RTO (Right of Reference).';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookExclusivityID', N'Surrogate primary key (auto-increment)'),
        (N'OrangeBookProductID', N'References OrangeBookProduct. Resolved during import by matching ApplType + ApplNo + ProductNo. No foreign key constraint enforced.'),
        (N'ApplType', N'Application type, matches the parent product. Preserved for import matching. Maps to Appl_Type in exclusivity.txt.'),
        (N'ApplNo', N'FDA application number. Preserved for import matching. Maps to Appl_No in exclusivity.txt.'),
        (N'ProductNo', N'Product number within the application. Preserved for import matching. Maps to Product_No in exclusivity.txt.'),
        (N'ExclusivityCode', N'Type of marketing exclusivity granted by the FDA (e.g., "NCE" = New Chemical Entity, "ODE" = Orphan Drug Exclusivity, "RTO" = Right of Reference, "M" = Biosimilar). Maps to Exclusivity_Code in exclusivity.txt.'),
        (N'ExclusivityDate', N'Expiration date of the exclusivity period. Parsed from Exclusivity_Date (MMM DD, YYYY format) in exclusivity.txt.');

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

    ---------------------------------------------------------------------------
    -- 5. OrangeBookProductMarketingCategory (Junction)
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookProductMarketingCategory' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookProductMarketingCategory] junction table...';

        CREATE TABLE [dbo].[OrangeBookProductMarketingCategory] (
            -- Primary Key
            [OrangeBookProductMarketingCategoryID] INT IDENTITY(1,1) NOT NULL,

            -- Junction References
            [OrangeBookProductID] INT NOT NULL,
            [MarketingCategoryID] INT NOT NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookProductMarketingCategory] PRIMARY KEY CLUSTERED ([OrangeBookProductMarketingCategoryID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookProductMarketingCategory]';
        PRINT '    - Skipping table creation.';
    END

    -- Indexes for OrangeBookProductMarketingCategory
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OBProductMarketingCategory_ProductID_MarketingCategoryID' AND object_id = OBJECT_ID('dbo.OrangeBookProductMarketingCategory'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_OBProductMarketingCategory_ProductID_MarketingCategoryID]
            ON [dbo].[OrangeBookProductMarketingCategory]([OrangeBookProductID], [MarketingCategoryID]);
        PRINT '    - Created index: UX_OBProductMarketingCategory_ProductID_MarketingCategoryID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: UX_OBProductMarketingCategory_ProductID_MarketingCategoryID';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OBProductMarketingCategory_MarketingCategoryID' AND object_id = OBJECT_ID('dbo.OrangeBookProductMarketingCategory'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OBProductMarketingCategory_MarketingCategoryID]
            ON [dbo].[OrangeBookProductMarketingCategory]([MarketingCategoryID]);
        PRINT '    - Created index: IX_OBProductMarketingCategory_MarketingCategoryID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OBProductMarketingCategory_MarketingCategoryID';
    END

    -- Extended properties for OrangeBookProductMarketingCategory
    SET @TableName = N'OrangeBookProductMarketingCategory';

    SET @PropValue = N'Junction table linking Orange Book products to SPL MarketingCategory records via application number. Enables cross-referencing Orange Book TE codes, patents, and exclusivities with full SPL label content. Populated by matching OrangeBookProduct.ApplNo to MarketingCategory.ApplicationOrMonographIDValue.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookProductMarketingCategoryID', N'Surrogate primary key (auto-increment)'),
        (N'OrangeBookProductID', N'References OrangeBookProduct. No foreign key constraint enforced; relationship managed by the import module.'),
        (N'MarketingCategoryID', N'References the existing MarketingCategory table (SPL data). No foreign key constraint enforced; relationship managed by the import module.');

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

    ---------------------------------------------------------------------------
    -- 6. OrangeBookProductIngredientSubstance (Junction)
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookProductIngredientSubstance' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookProductIngredientSubstance] junction table...';

        CREATE TABLE [dbo].[OrangeBookProductIngredientSubstance] (
            -- Primary Key
            [OrangeBookProductIngredientSubstanceID] INT IDENTITY(1,1) NOT NULL,

            -- Junction References
            [OrangeBookProductID]  INT NOT NULL,
            [IngredientSubstanceID] INT NOT NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookProductIngredientSubstance] PRIMARY KEY CLUSTERED ([OrangeBookProductIngredientSubstanceID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookProductIngredientSubstance]';
        PRINT '    - Skipping table creation.';
    END

    -- Indexes for OrangeBookProductIngredientSubstance
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OBProductIngredientSubstance_ProductID_SubstanceID' AND object_id = OBJECT_ID('dbo.OrangeBookProductIngredientSubstance'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_OBProductIngredientSubstance_ProductID_SubstanceID]
            ON [dbo].[OrangeBookProductIngredientSubstance]([OrangeBookProductID], [IngredientSubstanceID]);
        PRINT '    - Created index: UX_OBProductIngredientSubstance_ProductID_SubstanceID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: UX_OBProductIngredientSubstance_ProductID_SubstanceID';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OBProductIngredientSubstance_IngredientSubstanceID' AND object_id = OBJECT_ID('dbo.OrangeBookProductIngredientSubstance'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OBProductIngredientSubstance_IngredientSubstanceID]
            ON [dbo].[OrangeBookProductIngredientSubstance]([IngredientSubstanceID]);
        PRINT '    - Created index: IX_OBProductIngredientSubstance_IngredientSubstanceID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OBProductIngredientSubstance_IngredientSubstanceID';
    END

    -- Extended properties for OrangeBookProductIngredientSubstance
    SET @TableName = N'OrangeBookProductIngredientSubstance';

    SET @PropValue = N'Junction table linking Orange Book products to SPL IngredientSubstance records. Many-to-many: one OB product can contain multiple semicolon-delimited ingredients, and one IngredientSubstance can appear in many OB products. Populated by parsing individual ingredients from OrangeBookProduct.Ingredient and matching against IngredientSubstance.SubstanceName.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookProductIngredientSubstanceID', N'Surrogate primary key (auto-increment)'),
        (N'OrangeBookProductID', N'References OrangeBookProduct. No foreign key constraint enforced; relationship managed by the import module.'),
        (N'IngredientSubstanceID', N'References the existing IngredientSubstance table (SPL data). No foreign key constraint enforced; relationship managed by the import module.');

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

    ---------------------------------------------------------------------------
    -- 7. OrangeBookApplicantOrganization (Junction)
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookApplicantOrganization' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookApplicantOrganization] junction table...';

        CREATE TABLE [dbo].[OrangeBookApplicantOrganization] (
            -- Primary Key
            [OrangeBookApplicantOrganizationID] INT IDENTITY(1,1) NOT NULL,

            -- Junction References
            [OrangeBookApplicantID] INT NOT NULL,
            [OrganizationID]        INT NOT NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookApplicantOrganization] PRIMARY KEY CLUSTERED ([OrangeBookApplicantOrganizationID] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookApplicantOrganization]';
        PRINT '    - Skipping table creation.';
    END

    -- Indexes for OrangeBookApplicantOrganization
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_OBApplicantOrganization_ApplicantID_OrganizationID' AND object_id = OBJECT_ID('dbo.OrangeBookApplicantOrganization'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UX_OBApplicantOrganization_ApplicantID_OrganizationID]
            ON [dbo].[OrangeBookApplicantOrganization]([OrangeBookApplicantID], [OrganizationID]);
        PRINT '    - Created index: UX_OBApplicantOrganization_ApplicantID_OrganizationID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: UX_OBApplicantOrganization_ApplicantID_OrganizationID';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OBApplicantOrganization_OrganizationID' AND object_id = OBJECT_ID('dbo.OrangeBookApplicantOrganization'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OBApplicantOrganization_OrganizationID]
            ON [dbo].[OrangeBookApplicantOrganization]([OrganizationID]);
        PRINT '    - Created index: IX_OBApplicantOrganization_OrganizationID';
    END
    ELSE
    BEGIN
        PRINT '    - Index already exists: IX_OBApplicantOrganization_OrganizationID';
    END

    -- Extended properties for OrangeBookApplicantOrganization
    SET @TableName = N'OrangeBookApplicantOrganization';

    SET @PropValue = N'Junction table linking Orange Book applicant companies to SPL Organization records. Many-to-many: one OB applicant may map to multiple SPL organizations (name variations), and one Organization could match multiple OB applicant entries. Populated by matching OrangeBookApplicant.ApplicantFullName against Organization.OrganizationName.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'OrangeBookApplicantOrganizationID', N'Surrogate primary key (auto-increment)'),
        (N'OrangeBookApplicantID', N'References OrangeBookApplicant. No foreign key constraint enforced; relationship managed by the import module.'),
        (N'OrganizationID', N'References the existing Organization table (SPL data). No foreign key constraint enforced; relationship managed by the import module.');

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

    ---------------------------------------------------------------------------
    -- 8. OrangeBookPatentUseCode (Lookup)
    ---------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrangeBookPatentUseCode' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        PRINT ' -> Creating [dbo].[OrangeBookPatentUseCode] table...';

        CREATE TABLE [dbo].[OrangeBookPatentUseCode] (
            -- Natural Primary Key
            [PatentUseCode] VARCHAR(6)    NOT NULL,

            -- Definition
            [Definition]    VARCHAR(1000) NOT NULL,

            -- Constraints
            CONSTRAINT [PK_OrangeBookPatentUseCode] PRIMARY KEY CLUSTERED ([PatentUseCode] ASC)
        );

        PRINT '    - Table created successfully.';
    END
    ELSE
    BEGIN
        PRINT ' -> Table already exists: [dbo].[OrangeBookPatentUseCode]';
        PRINT '    - Skipping table creation.';
    END

    -- Extended properties for OrangeBookPatentUseCode
    SET @TableName = N'OrangeBookPatentUseCode';

    SET @PropValue = N'Lookup table for FDA Orange Book patent use code definitions. Maps each Patent_Use_Code value (e.g., "U-141") to its human-readable description of the approved indication covered by the patent. Populated from an embedded JSON resource in MedRecProImportClass. Data sourced from the FDA Orange Book website Patent Use Codes and Definitions page.';
    IF NOT EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', 'SCHEMA', @SchemaName, 'TABLE', @TableName, NULL, NULL))
    BEGIN
        EXEC sp_addextendedproperty
            @name = N'MS_Description',
            @value = @PropValue,
            @level0type = N'SCHEMA', @level0name = @SchemaName,
            @level1type = N'TABLE', @level1name = @TableName;
        PRINT '    - Added table description';
    END

    DELETE FROM @ColumnDescriptions;
    INSERT INTO @ColumnDescriptions (ColumnName, Description) VALUES
        (N'PatentUseCode', N'Patent use code identifier (e.g., "U-1", "U-141", "U-4412"). Serves as the natural primary key. Matches Patent_Use_Code values in patent.txt and the OrangeBookPatent.PatentUseCode column.'),
        (N'Definition', N'Human-readable description of the approved indication or method of use covered by the patent (e.g., "PREVENTION OF PREGNANCY", "TREATMENT OF HYPERTENSION"). Sourced from the FDA Patent Use Codes and Definitions publication.');

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

    ---------------------------------------------------------------------------
    -- Summary
    ---------------------------------------------------------------------------
    PRINT '';
    PRINT '========================================';
    PRINT 'Migration completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT ' - OrangeBookApplicant table created (if not exists)';
    PRINT ' - OrangeBookProduct table created (if not exists)';
    PRINT ' - OrangeBookPatent table created (if not exists)';
    PRINT ' - OrangeBookExclusivity table created (if not exists)';
    PRINT ' - OrangeBookProductMarketingCategory junction created (if not exists)';
    PRINT ' - OrangeBookProductIngredientSubstance junction created (if not exists)';
    PRINT ' - OrangeBookApplicantOrganization junction created (if not exists)';
    PRINT ' - OrangeBookPatentUseCode lookup created (if not exists)';
    PRINT ' - Indexes created for optimal query performance';
    PRINT ' - Extended properties added for documentation';
    PRINT ' - No foreign key constraints (managed by import module)';
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
PRINT 'Orange Book tables are ready for data import.';
PRINT 'Source files: products.txt, patent.txt, exclusivity.txt (tilde-delimited)';
PRINT '              + embedded JSON resource for patent use code definitions.';
PRINT '';
