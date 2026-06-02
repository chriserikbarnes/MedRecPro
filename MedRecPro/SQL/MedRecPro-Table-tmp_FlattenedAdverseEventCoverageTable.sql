/*******************************************************************************
 * Create_tmp_FlattenedAdverseEventCoverageTable.sql
 *
 * Idempotent DDL for the Stage 5 adverse-event coverage/audit companion table.
 * The RR-ready statistics table remains tmp_FlattenedAdverseEventTable; this
 * table records every standardized AE row's Stage 5 outcome, including source
 * eligibility exclusions, selected comparators, null-RR guard failures, and
 * RR-ready rows.
 *
 * Pipeline: tmp_FlattenedStandardizedTable (Stage 3 output)
 *             -> AdverseEventDenormalizationService
 *             -> tmp_FlattenedAdverseEventCoverageTable
 *
 * Re-runnable: IF NOT EXISTS guard. Truncate-on-rerun via Stage 5 service.
 ******************************************************************************/
/**************************************************************/
-- Target database: select the intended MedRecPro database before running.
-- This script intentionally does not issue USE so it cannot silently create
-- the Stage 5 AE coverage table in a different database than the view refresh
-- target.
/**************************************************************/
Use MedRecLocal
IF NOT EXISTS (SELECT * FROM sys.tables AS t INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id WHERE s.name = 'dbo' AND t.name = 'tmp_FlattenedAdverseEventCoverageTable')
BEGIN
    CREATE TABLE dbo.tmp_FlattenedAdverseEventCoverageTable (
        -- Surrogate PK: required for EF Core keyed mapping and stable row identity.
        tmp_FlattenedAdverseEventCoverageTableID INT IDENTITY(1,1) PRIMARY KEY,

        -- Source standardized row projection.
        tmp_FlattenedStandardizedTableID         INT              NOT NULL,
        TextTableID                              INT              NULL,
        DocumentGUID                             UNIQUEIDENTIFIER NULL,
        UNII                                     NVARCHAR(1000)   NULL,
        ParameterName                            NVARCHAR(1000)   NULL,
        ParameterCategory                        NVARCHAR(500)    NULL,
        TreatmentArm                             NVARCHAR(1000)   NULL,
        ArmN                                     INT              NULL,
        Dose                                     DECIMAL(18,6)    NULL,
        DoseUnit                                 NVARCHAR(50)     NULL,
        PrimaryValue                             FLOAT            NULL,
        PrimaryValueType                         NVARCHAR(100)    NULL,

        -- Selected comparator projection, when Stage 5 can choose one.
        ComparatorArm                            NVARCHAR(1000)   NULL,
        ComparatorN                              INT              NULL,
        ComparatorDose                           DECIMAL(18,6)    NULL,
        ComparatorDoseUnit                       NVARCHAR(50)     NULL,
        ComparatorPrimaryValue                   FLOAT            NULL,
        ComparatorPrimaryValueType               NVARCHAR(100)    NULL,
        IsPlaceboControlled                      BIT              NULL,
        RR                                       FLOAT            NULL,

        -- Durable audit/status fields.
        CoverageStatus                           NVARCHAR(100)    NULL,
        ExclusionReason                          NVARCHAR(100)    NULL,
        CoverageFlags                            NVARCHAR(1000)   NULL,
        CalculationFlags                         NVARCHAR(1000)   NULL,

        -- Population context copied from the standardized row or built AE entity.
        StudyContext                             NVARCHAR(1000)   NULL,
        [Population]                             NVARCHAR(500)    NULL,
        Subpopulation                            NVARCHAR(500)    NULL,

        -- Prefix keys keep common long-text filters under SQL Server's
        -- nonclustered-index key-width limit.
        UNII_IndexKey                            AS (CONVERT(NVARCHAR(450), LEFT(UNII, 450))) PERSISTED,
        ParameterName_IndexKey                   AS (CONVERT(NVARCHAR(450), LEFT(ParameterName, 450))) PERSISTED
    );

    CREATE NONCLUSTERED INDEX IX_FAEC_DocumentGUID        ON dbo.tmp_FlattenedAdverseEventCoverageTable(DocumentGUID);
    CREATE NONCLUSTERED INDEX IX_FAEC_TextTableID         ON dbo.tmp_FlattenedAdverseEventCoverageTable(TextTableID);
    CREATE NONCLUSTERED INDEX IX_FAEC_SourceID            ON dbo.tmp_FlattenedAdverseEventCoverageTable(tmp_FlattenedStandardizedTableID);
    CREATE NONCLUSTERED INDEX IX_FAEC_CoverageStatus      ON dbo.tmp_FlattenedAdverseEventCoverageTable(CoverageStatus, ExclusionReason);
    CREATE NONCLUSTERED INDEX IX_FAEC_RRReady             ON dbo.tmp_FlattenedAdverseEventCoverageTable(RR, CoverageStatus) INCLUDE (DocumentGUID, TextTableID, ParameterName);
    CREATE NONCLUSTERED INDEX IX_FAEC_UNII                ON dbo.tmp_FlattenedAdverseEventCoverageTable(UNII_IndexKey) INCLUDE (UNII);
    CREATE NONCLUSTERED INDEX IX_FAEC_ParameterName       ON dbo.tmp_FlattenedAdverseEventCoverageTable(ParameterName_IndexKey) INCLUDE (ParameterName);
END

-- Idempotent column additions for existing databases (forward-compat upgrades).
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'tmp_FlattenedStandardizedTableID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD tmp_FlattenedStandardizedTableID INT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'TextTableID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD TextTableID INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'DocumentGUID')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD DocumentGUID UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'UNII')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD UNII NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ParameterName')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ParameterName NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ParameterCategory')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ParameterCategory NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'TreatmentArm')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD TreatmentArm NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ArmN')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ArmN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'Dose')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD Dose DECIMAL(18,6) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'DoseUnit')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD DoseUnit NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'PrimaryValue')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD PrimaryValue FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'PrimaryValueType')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD PrimaryValueType NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorArm')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorArm NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorN')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorDose')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorDose DECIMAL(18,6) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorDoseUnit')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorDoseUnit NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorPrimaryValue')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorPrimaryValue FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ComparatorPrimaryValueType')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ComparatorPrimaryValueType NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IsPlaceboControlled')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD IsPlaceboControlled BIT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'RR')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD RR FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'CoverageStatus')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD CoverageStatus NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ExclusionReason')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ExclusionReason NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'CoverageFlags')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD CoverageFlags NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'CalculationFlags')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD CalculationFlags NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'StudyContext')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD StudyContext NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'Population')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD [Population] NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'Subpopulation')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD Subpopulation NVARCHAR(500) NULL;

-- Long-text computed key columns for filtered QA lookups.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'UNII_IndexKey')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD UNII_IndexKey AS (CONVERT(NVARCHAR(450), LEFT(UNII, 450))) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'ParameterName_IndexKey')
    ALTER TABLE dbo.tmp_FlattenedAdverseEventCoverageTable ADD ParameterName_IndexKey AS (CONVERT(NVARCHAR(450), LEFT(ParameterName, 450))) PERSISTED;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_DocumentGUID')
    CREATE NONCLUSTERED INDEX IX_FAEC_DocumentGUID        ON dbo.tmp_FlattenedAdverseEventCoverageTable(DocumentGUID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_TextTableID')
    CREATE NONCLUSTERED INDEX IX_FAEC_TextTableID         ON dbo.tmp_FlattenedAdverseEventCoverageTable(TextTableID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_SourceID')
    CREATE NONCLUSTERED INDEX IX_FAEC_SourceID            ON dbo.tmp_FlattenedAdverseEventCoverageTable(tmp_FlattenedStandardizedTableID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_CoverageStatus')
    CREATE NONCLUSTERED INDEX IX_FAEC_CoverageStatus      ON dbo.tmp_FlattenedAdverseEventCoverageTable(CoverageStatus, ExclusionReason);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_RRReady')
    CREATE NONCLUSTERED INDEX IX_FAEC_RRReady             ON dbo.tmp_FlattenedAdverseEventCoverageTable(RR, CoverageStatus) INCLUDE (DocumentGUID, TextTableID, ParameterName);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_UNII')
    CREATE NONCLUSTERED INDEX IX_FAEC_UNII                ON dbo.tmp_FlattenedAdverseEventCoverageTable(UNII_IndexKey) INCLUDE (UNII);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_FlattenedAdverseEventCoverageTable') AND name = 'IX_FAEC_ParameterName')
    CREATE NONCLUSTERED INDEX IX_FAEC_ParameterName       ON dbo.tmp_FlattenedAdverseEventCoverageTable(ParameterName_IndexKey) INCLUDE (ParameterName);
