/*******************************************************************************
 * Create_tmp_AeDashboardProductCatalog.sql
 *
 * Idempotent DDL for the materialized AE dashboard product catalog. Stage 5
 * refreshes this table after tmp_FlattenedAdverseEventRiskTable so product
 * picker requests can search, sort, and page one row per DocumentGUID without
 * rebuilding the vw_AeDrugSummary collapse on each request.
 *
 * Pipeline: tmp_FlattenedAdverseEventRiskTable
 *             -> vw_AeDashboardProductCatalog
 *             -> AdverseEventDenormalizationService
 *             -> tmp_AeDashboardProductCatalog
 *
 * Re-runnable: IF NOT EXISTS guard. Truncate-on-rerun via Stage 5 service.
 ******************************************************************************/
/**************************************************************/
-- Target database: select the intended MedRecPro database before running.
-- This script intentionally mirrors the Stage 5 table scripts in this folder.
/**************************************************************/
Use MedRecLocal
IF NOT EXISTS (SELECT * FROM sys.tables AS t INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'tmp_AeDashboardProductCatalog')
BEGIN
    CREATE TABLE dbo.tmp_AeDashboardProductCatalog (
        -- Surrogate PK: required for EF Core keyed mapping and stable row identity.
        AeDashboardProductCatalogID          INT IDENTITY(1,1) PRIMARY KEY,

        -- Product identity and preferred display fields.
        DocumentGUID                         UNIQUEIDENTIFIER NOT NULL,
        ProductName                          NVARCHAR(500)    NULL,
        PrimarySubstanceName                 NVARCHAR(1000)   NULL,
        PrimaryUNII                          NVARCHAR(1000)   NULL,
        PrimaryPharmClassCode                NVARCHAR(50)     NULL,
        PrimaryPharmClassName                NVARCHAR(255)    NULL,
        ActiveIngredientsJson                NVARCHAR(MAX)    NULL,
        ActiveMoietyID                       INT              NULL,
        IngredientSubstanceID                INT              NULL,
        PharmacologicClassID                 INT              NULL,

        -- Picker and detail header metrics.
        ArmN                                 INT              NULL,
        ComparatorN                          INT              NULL,
        [RowCount]                           INT              NOT NULL DEFAULT 0,
        SignificantCount                     INT              NOT NULL DEFAULT 0,
        SignificantProtectiveCount           INT              NOT NULL DEFAULT 0,
        SignificantElevatedCount             INT              NOT NULL DEFAULT 0,
        PlaceboCoverage                      BIT              NOT NULL DEFAULT 0,
        ActiveCoverage                       BIT              NOT NULL DEFAULT 0,
        DoseCoverage                         FLOAT            NOT NULL DEFAULT 0,
        SocBreadth                           INT              NOT NULL DEFAULT 0,
        SocTotal                             INT              NOT NULL DEFAULT 17,
        MonoComboMix                         NVARCHAR(20)     NULL,
        Score                                INT              NULL,
        ScoreReason                          NVARCHAR(500)    NULL,

        -- Provider-side sort and search support.
        SortSignificantElevatedCount         INT              NOT NULL DEFAULT 0,
        SortProductName                      NVARCHAR(500)    NULL,
        SearchText                           NVARCHAR(4000)   NULL,
        SearchText_IndexKey                  AS (CONVERT(NVARCHAR(450), LEFT(SearchText, 450))) PERSISTED,
        RefreshedAt                          DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_AEDPC_DocumentGUID
        ON dbo.tmp_AeDashboardProductCatalog(DocumentGUID);

    CREATE NONCLUSTERED INDEX IX_AEDPC_Sort
        ON dbo.tmp_AeDashboardProductCatalog(SortSignificantElevatedCount DESC, SortProductName, DocumentGUID)
        INCLUDE (ProductName, PrimarySubstanceName, PrimaryUNII, PrimaryPharmClassName, MonoComboMix, Score, PlaceboCoverage, ActiveCoverage);

    CREATE NONCLUSTERED INDEX IX_AEDPC_SearchText
        ON dbo.tmp_AeDashboardProductCatalog(SearchText_IndexKey)
        INCLUDE (DocumentGUID, ProductName, PrimarySubstanceName, PrimaryUNII, PrimaryPharmClassName, SortSignificantElevatedCount, SortProductName);
END

-- Idempotent index additions for existing databases.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_AeDashboardProductCatalog') AND name = 'UX_AEDPC_DocumentGUID')
    CREATE UNIQUE NONCLUSTERED INDEX UX_AEDPC_DocumentGUID
        ON dbo.tmp_AeDashboardProductCatalog(DocumentGUID);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_AeDashboardProductCatalog') AND name = 'IX_AEDPC_Sort')
    CREATE NONCLUSTERED INDEX IX_AEDPC_Sort
        ON dbo.tmp_AeDashboardProductCatalog(SortSignificantElevatedCount DESC, SortProductName, DocumentGUID)
        INCLUDE (ProductName, PrimarySubstanceName, PrimaryUNII, PrimaryPharmClassName, MonoComboMix, Score, PlaceboCoverage, ActiveCoverage);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_AeDashboardProductCatalog') AND name = 'IX_AEDPC_SearchText')
    CREATE NONCLUSTERED INDEX IX_AEDPC_SearchText
        ON dbo.tmp_AeDashboardProductCatalog(SearchText_IndexKey)
        INCLUDE (DocumentGUID, ProductName, PrimarySubstanceName, PrimaryUNII, PrimaryPharmClassName, SortSignificantElevatedCount, SortProductName);
