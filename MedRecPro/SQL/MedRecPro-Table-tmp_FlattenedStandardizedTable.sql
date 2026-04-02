/*******************************************************************************
 * Create_tmp_FlattenedStandardizedTable.sql
 *
 * Idempotent DDL for the Stage 3 SPL Table Normalization output table.
 * Each row = one atomic observation from a parsed SPL table cell.
 *
 * Pipeline: Stage 2 (ReconstructedTable) → Parser → ParsedObservation →
 *           Orchestrator → tmp_FlattenedStandardizedTable
 *
 * Re-runnable: IF NOT EXISTS guard. Truncate-on-rerun via orchestrator.
 ******************************************************************************/
USE MedRecLocal
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tmp_FlattenedStandardizedTable')
BEGIN
    CREATE TABLE tmp_FlattenedStandardizedTable (
        -- Surrogate PK: required for EF Core change tracking (AddRange + SaveChangesAsync)
        tmp_FlattenedStandardizedTableID INT IDENTITY(1,1) PRIMARY KEY,

        -- Provenance (8): traces every value back to the exact source cell
        DocumentGUID            UNIQUEIDENTIFIER NULL,
        LabelerName             NVARCHAR(500)    NULL,
        ProductTitle            NVARCHAR(500)    NULL,
        VersionNumber           INT              NULL,
        TextTableID             INT              NULL,
        Caption                 NVARCHAR(MAX)    NULL,
        SourceRowSeq            INT              NULL,
        SourceCellSeq           INT              NULL,

        -- Classification (4): routes queries and groups comparable data
        TableCategory           NVARCHAR(100)    NULL,
        ParentSectionCode       NVARCHAR(50)     NULL,
        ParentSectionTitle      NVARCHAR(1000)   NULL,
        SectionTitle            NVARCHAR(1000)   NULL,

        -- Observation Context (9): what was measured, in whom, under what conditions
        ParameterName           NVARCHAR(1000)   NULL,
        ParameterCategory       NVARCHAR(500)    NULL,
        ParameterSubtype        NVARCHAR(500)    NULL,
        TreatmentArm            NVARCHAR(1000)   NULL,
        ArmN                    INT              NULL,
        StudyContext            NVARCHAR(1000)   NULL,
        DoseRegimen             NVARCHAR(1000)   NULL,
        [Population]            NVARCHAR(500)    NULL,
        Timepoint               NVARCHAR(500)    NULL,
        [Time]                  FLOAT            NULL,
        TimeUnit                NVARCHAR(50)     NULL,

        -- Decomposed Values (10): typed, queryable components of the raw cell text
        RawValue                NVARCHAR(2000)   NULL,
        PrimaryValue            FLOAT            NULL,
        PrimaryValueType        NVARCHAR(100)    NULL,
        SecondaryValue          FLOAT            NULL,
        SecondaryValueType      NVARCHAR(100)    NULL,
        LowerBound              FLOAT            NULL,
        UpperBound              FLOAT            NULL,
        BoundType               NVARCHAR(50)     NULL,
        PValue                  FLOAT            NULL,
        Unit                    NVARCHAR(500)    NULL,

        -- Validation (5): automated quality signals and confidence scores
        ParseConfidence         FLOAT            NULL,
        ParseRule               NVARCHAR(100)    NULL,
        FootnoteMarkers         NVARCHAR(500)    NULL,
        FootnoteText            NVARCHAR(MAX)    NULL,
        ValidationFlags         NVARCHAR(1000)   NULL
    );

    -- Nonclustered indexes for common query patterns
    CREATE NONCLUSTERED INDEX IX_FNT_TextTableID      ON tmp_FlattenedStandardizedTable(TextTableID);
    CREATE NONCLUSTERED INDEX IX_FNT_TableCategory     ON tmp_FlattenedStandardizedTable(TableCategory);
    CREATE NONCLUSTERED INDEX IX_FNT_DocumentGUID      ON tmp_FlattenedStandardizedTable(DocumentGUID);
    CREATE NONCLUSTERED INDEX IX_FNT_ParameterName     ON tmp_FlattenedStandardizedTable(ParameterName);
    CREATE NONCLUSTERED INDEX IX_FNT_ParentSectionCode ON tmp_FlattenedStandardizedTable(ParentSectionCode);
END
