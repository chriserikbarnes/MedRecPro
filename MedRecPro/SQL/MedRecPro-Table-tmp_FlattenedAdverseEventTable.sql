/*******************************************************************************
 * Create_tmp_FlattenedAdverseEventTable.sql
 *
 * Idempotent DDL for the AE-only denormalized projection of
 * tmp_FlattenedStandardizedTable, produced by a Stage 5 post-standardization
 * service (Phase 2). Each row carries pre-computed Relative Risk (RR),
 * Dose-Normalized RR (DNRR), 95% CI bounds, and PERSISTED log-scale companions
 * so that real-time visualizations bind directly without runtime statistics.
 *
 * Pipeline: tmp_FlattenedStandardizedTable (Stage 3 output)
 *             -> AdverseEventDenormalizationService (Stage 5, Phase 2)
 *             -> tmp_FlattenedAdverseEventTable
 *
 * Stat method: Katz log-method for RR + 95% CI; Haldane-Anscombe continuity
 * correction on zero cells; log-linear DNRR with intra-study reference dose
 * (D_ref = MIN(Dose) WHERE Dose > 0 over the study group).
 *
 * Re-runnable: IF NOT EXISTS guard. Truncate-on-rerun via Phase 2 service.
 ******************************************************************************/
USE MedRecLocal
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tmp_FlattenedAdverseEventTable')
BEGIN
    CREATE TABLE tmp_FlattenedAdverseEventTable (
        -- Surrogate PK: required for EF Core change tracking (AddRange + SaveChangesAsync)
        tmp_FlattenedAdverseEventTableID    INT IDENTITY(1,1) PRIMARY KEY,

        -- Source linkage / projection (lifted verbatim from tmp_FlattenedStandardizedTable)
        tmp_FlattenedStandardizedTableID    INT              NOT NULL,
        DocumentGUID                        UNIQUEIDENTIFIER NULL,
        UNII                                NVARCHAR(1000)   NULL,    -- may be concatenated with '+'
        ParameterName                       NVARCHAR(1000)   NULL,    -- AE term (e.g. "Nausea")
        ParameterCategory                   NVARCHAR(500)    NULL,    -- SOC group (e.g. "Nervous System")
        ArmN                                INT              NULL,
        Dose                                DECIMAL(18,6)    NULL,
        DoseUnit                            NVARCHAR(50)     NULL,
        PrimaryValue                        FLOAT            NULL,
        PrimaryValueType                    NVARCHAR(100)    NULL,    -- copied verbatim, never derived

        -- Comparator metadata (identifies the row this RR is calculated against)
        TreatmentArm                        NVARCHAR(1000)   NULL,
        ComparatorArm                       NVARCHAR(1000)   NULL,
        ComparatorN                         INT              NULL,
        IsPlaceboControlled                 BIT              NOT NULL DEFAULT 0,
                                                                      -- Trial-design flag (Document-level):
                                                                      --   1 = drug arms + placebo only
                                                                      --   0 = active comparator present, stepped-dose
                                                                      --       only, single-arm, or placebo+active mix

        -- Derived event counts (intermediate; needed for CI math)
        EventsTreatment                     FLOAT            NULL,    -- a in 2x2
        EventsComparator                    FLOAT            NULL,    -- c in 2x2

        -- Risk statistics (point estimates)
        RR                                  FLOAT            NULL,    -- Relative Risk (Katz)
        DNRR                                FLOAT            NULL,    -- Dose-Normalized RR (log-linear)

        -- 95% CI bounds (linear scale)
        RRLowerBound                        FLOAT            NULL,
        RRUpperBound                        FLOAT            NULL,
        DNRRLowerBound                      FLOAT            NULL,
        DNRRUpperBound                      FLOAT            NULL,

        -- Log-scale companions: PERSISTED computed columns. Materialized on disk,
        -- auto-maintained by SQL Server, indexable. CASE guards prevent LOG(0)/LOG(NULL).
        LogRR                               AS (CASE WHEN RR             > 0 THEN LOG(RR)             END) PERSISTED,
        LogRRLowerBound                     AS (CASE WHEN RRLowerBound   > 0 THEN LOG(RRLowerBound)   END) PERSISTED,
        LogRRUpperBound                     AS (CASE WHEN RRUpperBound   > 0 THEN LOG(RRUpperBound)   END) PERSISTED,
        LogDNRR                             AS (CASE WHEN DNRR           > 0 THEN LOG(DNRR)           END) PERSISTED,
        LogDNRRLowerBound                   AS (CASE WHEN DNRRLowerBound > 0 THEN LOG(DNRRLowerBound) END) PERSISTED,
        LogDNRRUpperBound                   AS (CASE WHEN DNRRUpperBound > 0 THEN LOG(DNRRUpperBound) END) PERSISTED,

        -- Provenance metadata for the calculation pass (Phase 2 populates)
        CalculationMethod                   NVARCHAR(50)     NULL,    -- e.g. 'KATZ_LOG'
        CalculationFlags                    NVARCHAR(500)    NULL     -- e.g. 'ZERO_CELL_CORRECTED;PLACEBO_COMPARATOR'
    );

    -- Nonclustered indexes for common query patterns
    CREATE NONCLUSTERED INDEX IX_FAE_DocumentGUID      ON tmp_FlattenedAdverseEventTable(DocumentGUID);
    CREATE NONCLUSTERED INDEX IX_FAE_UNII              ON tmp_FlattenedAdverseEventTable(UNII);
    CREATE NONCLUSTERED INDEX IX_FAE_ParameterName     ON tmp_FlattenedAdverseEventTable(ParameterName);
    CREATE NONCLUSTERED INDEX IX_FAE_ParameterCategory ON tmp_FlattenedAdverseEventTable(ParameterCategory);
    CREATE NONCLUSTERED INDEX IX_FAE_SourceID          ON tmp_FlattenedAdverseEventTable(tmp_FlattenedStandardizedTableID);
END

-- Idempotent column additions for existing databases (forward-compat upgrades)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'tmp_FlattenedStandardizedTableID')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD tmp_FlattenedStandardizedTableID INT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'DocumentGUID')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD DocumentGUID UNIQUEIDENTIFIER NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'UNII')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD UNII NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'ParameterName')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD ParameterName NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'ParameterCategory')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD ParameterCategory NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'ArmN')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD ArmN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'Dose')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD Dose DECIMAL(18,6) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'DoseUnit')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD DoseUnit NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'PrimaryValue')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD PrimaryValue FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'PrimaryValueType')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD PrimaryValueType NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'TreatmentArm')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD TreatmentArm NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'ComparatorArm')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD ComparatorArm NVARCHAR(1000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'ComparatorN')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD ComparatorN INT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IsPlaceboControlled')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD IsPlaceboControlled BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'EventsTreatment')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD EventsTreatment FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'EventsComparator')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD EventsComparator FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'RR')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD RR FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'DNRR')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD DNRR FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'RRLowerBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD RRLowerBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'RRUpperBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD RRUpperBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'DNRRLowerBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD DNRRLowerBound FLOAT NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'DNRRUpperBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD DNRRUpperBound FLOAT NULL;

-- Computed columns must be added separately (cannot use a single ALTER ADD)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogRR')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogRR AS (CASE WHEN RR > 0 THEN LOG(RR) END) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogRRLowerBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogRRLowerBound AS (CASE WHEN RRLowerBound > 0 THEN LOG(RRLowerBound) END) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogRRUpperBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogRRUpperBound AS (CASE WHEN RRUpperBound > 0 THEN LOG(RRUpperBound) END) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogDNRR')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogDNRR AS (CASE WHEN DNRR > 0 THEN LOG(DNRR) END) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogDNRRLowerBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogDNRRLowerBound AS (CASE WHEN DNRRLowerBound > 0 THEN LOG(DNRRLowerBound) END) PERSISTED;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'LogDNRRUpperBound')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD LogDNRRUpperBound AS (CASE WHEN DNRRUpperBound > 0 THEN LOG(DNRRUpperBound) END) PERSISTED;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'CalculationMethod')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD CalculationMethod NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'CalculationFlags')
    ALTER TABLE tmp_FlattenedAdverseEventTable ADD CalculationFlags NVARCHAR(500) NULL;

-- Idempotent index creation for existing databases
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IX_FAE_DocumentGUID')
    CREATE NONCLUSTERED INDEX IX_FAE_DocumentGUID      ON tmp_FlattenedAdverseEventTable(DocumentGUID);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IX_FAE_UNII')
    CREATE NONCLUSTERED INDEX IX_FAE_UNII              ON tmp_FlattenedAdverseEventTable(UNII);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IX_FAE_ParameterName')
    CREATE NONCLUSTERED INDEX IX_FAE_ParameterName     ON tmp_FlattenedAdverseEventTable(ParameterName);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IX_FAE_ParameterCategory')
    CREATE NONCLUSTERED INDEX IX_FAE_ParameterCategory ON tmp_FlattenedAdverseEventTable(ParameterCategory);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('tmp_FlattenedAdverseEventTable') AND name = 'IX_FAE_SourceID')
    CREATE NONCLUSTERED INDEX IX_FAE_SourceID          ON tmp_FlattenedAdverseEventTable(tmp_FlattenedStandardizedTableID);
