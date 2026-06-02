/*******************************************************************************
 * Create_tmp_FlattenedAdverseEventRiskTable.sql
 *
 * Idempotent DDL for the materialized adverse-event risk projection sourced from
 * dbo.vw_AeRisk. Stage 5 refreshes this table after
 * tmp_FlattenedAdverseEventTable has been rebuilt so dashboard queries can read
 * optional product/class context and number-needed estimates without
 * re-running the view.
 *
 * Pipeline: tmp_FlattenedStandardizedTable (Stage 3 output)
 *             -> AdverseEventDenormalizationService
 *             -> tmp_FlattenedAdverseEventTable
 *             -> dbo.vw_AeRisk
 *             -> tmp_FlattenedAdverseEventRiskTable
 *
 * Re-runnable: IF NOT EXISTS guard. Truncate-on-rerun via Stage 5 service.
 ******************************************************************************/
/**************************************************************/
-- Target database: select the intended MedRecPro database before running.
-- This script intentionally does not issue USE so it cannot silently create
-- the Stage 5 AE risk snapshot table in a different database than the view
-- refresh target.
/**************************************************************/
Use MedRecLocal
IF NOT EXISTS (SELECT * FROM sys.tables AS t INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'tmp_FlattenedAdverseEventRiskTable')
BEGIN
    CREATE TABLE dbo.tmp_FlattenedAdverseEventRiskTable (
        -- Surrogate PK: required for EF Core keyed mapping and stable row identity.
        tmp_FlattenedAdverseEventRiskTableID INT IDENTITY(1,1) PRIMARY KEY,

        -- Source and product/class identification from dbo.vw_AeRisk.
        DocumentGUID                         UNIQUEIDENTIFIER NULL,
        tmp_FlattenedAdverseEventTableID     INT              NOT NULL,
        tmp_FlattenedStandardizedTableID     INT              NOT NULL,
        ActiveMoietyID                       INT              NULL,
        IngredientSubstanceID                INT              NULL,
        PharmacologicClassID                 INT              NULL,
        ProductName                          NVARCHAR(500)    NULL,
        SubstanceName                        NVARCHAR(1000)   NULL,
        PharmClassCode                       NVARCHAR(50)     NULL,
        PharmClassName                       NVARCHAR(255)    NULL,

        -- AE signal classification.
        IsPlaceboControlled                  BIT              NOT NULL DEFAULT 0,
        ParameterName                        NVARCHAR(1000)   NULL,
        ParameterCategory                    NVARCHAR(500)    NULL,
        Significance                         NVARCHAR(50)     NULL,
        NumberNeededType                     NVARCHAR(10)     NULL,

        -- Denominators, event counts, and number-needed estimates.
        ArmN                                 INT              NULL,
        ComparatorN                          INT              NULL,
        EventsTreatment                      FLOAT            NULL,
        EventsComparator                     FLOAT            NULL,
        NumberNeeded                         FLOAT            NULL,
        NumberNeededLowerBound               FLOAT            NULL,
        NumberNeededUpperBound               FLOAT            NULL,

        -- RR point estimates and log-scale companions.
        RR                                   FLOAT            NULL,
        RRLowerBound                         FLOAT            NULL,
        RRUpperBound                         FLOAT            NULL,
        LogRR                                FLOAT            NULL,
        LogRRLowerBound                      FLOAT            NULL,
        LogRRUpperBound                      FLOAT            NULL,

        -- Ingredient/combo and provenance context.
        UNII                                 NVARCHAR(1000)   NULL,
        IsCombo                              BIT              NOT NULL DEFAULT 0,
        CalculationFlags                     NVARCHAR(500)    NULL,
        StudyContext                         NVARCHAR(1000)   NULL,
        [Population]                         NVARCHAR(500)    NULL,
        Subpopulation                        NVARCHAR(500)    NULL,
        Dose                                 DECIMAL(18,6)    NULL,
        DoseUnit                             NVARCHAR(50)     NULL,

        -- Prefix keys keep common long-text filters under SQL Server's
        -- nonclustered-index key-width limit.
        UNII_IndexKey                        AS (CONVERT(NVARCHAR(450), LEFT(UNII, 450))) PERSISTED,
        ParameterName_IndexKey               AS (CONVERT(NVARCHAR(450), LEFT(ParameterName, 450))) PERSISTED
    );

    CREATE NONCLUSTERED INDEX IX_FAER_DocumentGUID             ON dbo.tmp_FlattenedAdverseEventRiskTable(DocumentGUID);
    CREATE NONCLUSTERED INDEX IX_FAER_AdverseEventID           ON dbo.tmp_FlattenedAdverseEventRiskTable(tmp_FlattenedAdverseEventTableID);
    CREATE NONCLUSTERED INDEX IX_FAER_SourceID                 ON dbo.tmp_FlattenedAdverseEventRiskTable(tmp_FlattenedStandardizedTableID);
    CREATE NONCLUSTERED INDEX IX_FAER_PharmClassSignificance   ON dbo.tmp_FlattenedAdverseEventRiskTable(PharmacologicClassID, Significance) INCLUDE (ParameterName, NumberNeeded, RR);
    CREATE NONCLUSTERED INDEX IX_FAER_PlaceboSignificance      ON dbo.tmp_FlattenedAdverseEventRiskTable(IsPlaceboControlled, Significance) INCLUDE (RR, NumberNeeded);
    CREATE NONCLUSTERED INDEX IX_FAER_ParameterCategory        ON dbo.tmp_FlattenedAdverseEventRiskTable(ParameterCategory);
    CREATE NONCLUSTERED INDEX IX_FAER_UNII                     ON dbo.tmp_FlattenedAdverseEventRiskTable(UNII_IndexKey) INCLUDE (UNII);
    CREATE NONCLUSTERED INDEX IX_FAER_ParameterName            ON dbo.tmp_FlattenedAdverseEventRiskTable(ParameterName_IndexKey) INCLUDE (ParameterName);
END

-- Idempotent column additions for existing databases (forward-compat upgrades).
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'DocumentGUID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD DocumentGUID UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'tmp_FlattenedAdverseEventTableID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD tmp_FlattenedAdverseEventTableID INT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'tmp_FlattenedStandardizedTableID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD tmp_FlattenedStandardizedTableID INT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ActiveMoietyID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ActiveMoietyID INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IngredientSubstanceID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD IngredientSubstanceID INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'PharmacologicClassID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD PharmacologicClassID INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ProductName')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ProductName NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'SubstanceName')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD SubstanceName NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'PharmClassCode')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD PharmClassCode NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'PharmClassName')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD PharmClassName NVARCHAR(255) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IsPlaceboControlled')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD IsPlaceboControlled BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ParameterName')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ParameterName NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ParameterCategory')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ParameterCategory NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'Significance')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD Significance NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'NumberNeededType')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD NumberNeededType NVARCHAR(10) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ArmN')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ArmN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ComparatorN')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ComparatorN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'EventsTreatment')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD EventsTreatment FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'EventsComparator')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD EventsComparator FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'NumberNeeded')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD NumberNeeded FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'NumberNeededLowerBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD NumberNeededLowerBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'NumberNeededUpperBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD NumberNeededUpperBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'RR')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD RR FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'RRLowerBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD RRLowerBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'RRUpperBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD RRUpperBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'LogRR')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD LogRR FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'LogRRLowerBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD LogRRLowerBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'LogRRUpperBound')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD LogRRUpperBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'UNII')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD UNII NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IsCombo')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD IsCombo BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'CalculationFlags')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD CalculationFlags NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'StudyContext')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD StudyContext NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'Population')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD [Population] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'Subpopulation')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD Subpopulation NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'Dose')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD Dose DECIMAL(18,6) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'DoseUnit')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD DoseUnit NVARCHAR(50) NULL;

-- Long-text computed key columns for filtered dashboard lookups.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'UNII_IndexKey')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD UNII_IndexKey AS (CONVERT(NVARCHAR(450), LEFT(UNII, 450))) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'ParameterName_IndexKey')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventRiskTable ADD ParameterName_IndexKey AS (CONVERT(NVARCHAR(450), LEFT(ParameterName, 450))) PERSISTED;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_DocumentGUID')
    CREATE NONCLUSTERED INDEX IX_FAER_DocumentGUID             ON dbo.tmp_FlattenedAdverseEventRiskTable(DocumentGUID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_AdverseEventID')
    CREATE NONCLUSTERED INDEX IX_FAER_AdverseEventID           ON dbo.tmp_FlattenedAdverseEventRiskTable(tmp_FlattenedAdverseEventTableID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_SourceID')
    CREATE NONCLUSTERED INDEX IX_FAER_SourceID                 ON dbo.tmp_FlattenedAdverseEventRiskTable(tmp_FlattenedStandardizedTableID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_PharmClassSignificance')
    CREATE NONCLUSTERED INDEX IX_FAER_PharmClassSignificance   ON dbo.tmp_FlattenedAdverseEventRiskTable(PharmacologicClassID, Significance) INCLUDE (ParameterName, NumberNeeded, RR);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_PlaceboSignificance')
    CREATE NONCLUSTERED INDEX IX_FAER_PlaceboSignificance      ON dbo.tmp_FlattenedAdverseEventRiskTable(IsPlaceboControlled, Significance) INCLUDE (RR, NumberNeeded);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_ParameterCategory')
    CREATE NONCLUSTERED INDEX IX_FAER_ParameterCategory        ON dbo.tmp_FlattenedAdverseEventRiskTable(ParameterCategory);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_UNII')
    CREATE NONCLUSTERED INDEX IX_FAER_UNII                     ON dbo.tmp_FlattenedAdverseEventRiskTable(UNII_IndexKey) INCLUDE (UNII);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable') AND name = 'IX_FAER_ParameterName')
    CREATE NONCLUSTERED INDEX IX_FAER_ParameterName            ON dbo.tmp_FlattenedAdverseEventRiskTable(ParameterName_IndexKey) INCLUDE (ParameterName);
