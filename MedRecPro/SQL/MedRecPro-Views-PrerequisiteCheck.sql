/*******************************************************************************
 * MedRecPro_Views.sql prerequisite check
 *
 * Purpose:
 *   Reports missing table dependencies before rerunning MedRecPro_Views.sql.
 *   Run this script in the same target database where the views will be
 *   refreshed. It does not create or modify objects.
 *
 * Common remediation order:
 *   1. Patch/MedRecPro-Patch-PharmClassDosageFormDependency.sql
 *      Recommended when dbo.PharmClassDosageFormExclusion is missing or empty.
 *      MedRecPro_Views.sql can create an empty bootstrap table, but the patch
 *      seeds the dosage-form exclusion rules used by pharmacologic-class views.
 *
 *   2. MedRecPro-Table-tmp_FlattenedStandardizedTable.sql
 *      Stage 3 table; include when rebuilding the table-standardization stack.
 *
 *   3. MedRecPro-Table-tmp_FlattenedAdverseEventTable.sql
 *      Stage 5 AE statistics table required by dbo.vw_AeRisk.
 *
 *   4. MedRecPro-Table-tmp_FlattenedAdverseEventCoverageTable.sql
 *      Stage 5 AE coverage/audit companion table.
 *
 *   5. MedRecPro-Table-tmp_FlattenedAdverseEventRiskTable.sql
 *      Materialized AE risk table required by dbo.vw_AeDrugSummary.
 *
 *   6. MedRecPro-TableCreate-OrangeBook.sql
 *      Required only when Orange Book patent views should return patent data.
 *      MedRecPro_Views.sql creates an empty compatibility view when these
 *      optional tables are absent.
 ******************************************************************************/

SET NOCOUNT ON;
GO

PRINT 'Checking MedRecPro_Views.sql prerequisites in database: ' + DB_NAME();
PRINT '';
GO

DECLARE @MissingPrerequisites TABLE
(
    ObjectName SYSNAME NOT NULL,
    RequiredFor NVARCHAR(500) NOT NULL,
    RemediationScript NVARCHAR(500) NOT NULL,
    Notes NVARCHAR(1000) NULL
);

IF OBJECT_ID('dbo.PharmClassDosageFormExclusion', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.PharmClassDosageFormExclusion',
         N'Seeded pharmacologic-class dosage-form filtering',
         N'Patch/MedRecPro-Patch-PharmClassDosageFormDependency.sql',
         N'MedRecPro_Views.sql can create an empty bootstrap table; run this patch to seed the exclusion-rule data.');
END;
ELSE
BEGIN
    DECLARE @HasActivePharmClassExclusions BIT = 0;

    EXEC sys.sp_executesql
        N'SELECT @HasRows = CASE WHEN EXISTS (SELECT 1 FROM dbo.PharmClassDosageFormExclusion WHERE IsActive = 1) THEN 1 ELSE 0 END;',
        N'@HasRows BIT OUTPUT',
        @HasRows = @HasActivePharmClassExclusions OUTPUT;

    IF @HasActivePharmClassExclusions = 0
    BEGIN
        INSERT INTO @MissingPrerequisites
            (ObjectName, RequiredFor, RemediationScript, Notes)
        VALUES
            (N'dbo.PharmClassDosageFormExclusion (empty)',
             N'Seeded pharmacologic-class dosage-form filtering',
             N'Patch/MedRecPro-Patch-PharmClassDosageFormDependency.sql',
             N'The views will compile, but pharmacologic-class false-positive exclusions are inactive until this seed data is loaded.');
    END;
END;

IF OBJECT_ID('dbo.tmp_FlattenedAdverseEventTable', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.tmp_FlattenedAdverseEventTable',
         N'vw_AeRisk',
         N'MedRecPro-Table-tmp_FlattenedAdverseEventTable.sql',
         N'Create before rerunning MedRecPro_Views.sql; Stage 5 later populates this table.');
END;

IF OBJECT_ID('dbo.tmp_FlattenedAdverseEventRiskTable', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.tmp_FlattenedAdverseEventRiskTable',
         N'vw_AeDrugSummary',
         N'MedRecPro-Table-tmp_FlattenedAdverseEventRiskTable.sql',
         N'Create after dbo.vw_AeRisk can compile; Stage 5 materializes dbo.vw_AeRisk into this table.');
END;

IF OBJECT_ID('dbo.OrangeBookProduct', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.OrangeBookProduct',
         N'vw_OrangeBookPatent',
         N'MedRecPro-TableCreate-OrangeBook.sql',
         N'MedRecPro_Views.sql creates an empty compatibility view without this optional table.');
END;

IF OBJECT_ID('dbo.OrangeBookPatent', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.OrangeBookPatent',
         N'vw_OrangeBookPatent',
         N'MedRecPro-TableCreate-OrangeBook.sql',
         N'MedRecPro_Views.sql creates an empty compatibility view without this optional table.');
END;

IF OBJECT_ID('dbo.OrangeBookPatentUseCode', 'U') IS NULL
BEGIN
    INSERT INTO @MissingPrerequisites
        (ObjectName, RequiredFor, RemediationScript, Notes)
    VALUES
        (N'dbo.OrangeBookPatentUseCode',
         N'vw_OrangeBookPatent',
         N'MedRecPro-TableCreate-OrangeBook.sql',
         N'MedRecPro_Views.sql creates an empty compatibility view without this optional table.');
END;

IF EXISTS (SELECT 1 FROM @MissingPrerequisites)
BEGIN
    PRINT 'Prerequisite findings found. Run the listed remediation scripts in this database when you need the corresponding data surface.';
    PRINT '';

    SELECT
        ObjectName,
        RequiredFor,
        RemediationScript,
        Notes
    FROM @MissingPrerequisites
    ORDER BY
        CASE
            WHEN ObjectName LIKE N'dbo.PharmClassDosageFormExclusion%' THEN 1
            WHEN ObjectName = N'dbo.tmp_FlattenedAdverseEventTable' THEN 2
            WHEN ObjectName = N'dbo.tmp_FlattenedAdverseEventRiskTable' THEN 3
            WHEN ObjectName LIKE N'dbo.OrangeBook%' THEN 4
            ELSE 5
        END,
        ObjectName;

    IF EXISTS (
        SELECT 1
        FROM @MissingPrerequisites
        WHERE ObjectName IN (
            N'dbo.tmp_FlattenedAdverseEventTable',
            N'dbo.tmp_FlattenedAdverseEventRiskTable'
        )
    )
    BEGIN
        RAISERROR('Required AE table prerequisites are missing in the current database.', 16, 1);
    END
    ELSE
    BEGIN
        PRINT '';
        PRINT 'Only optional or bootstrap findings were found. MedRecPro_Views.sql can run, but the listed remediation scripts improve data completeness.';
    END
END
ELSE
BEGIN
    PRINT 'All checked prerequisites are present. You can rerun MedRecPro_Views.sql.';
END;
GO
