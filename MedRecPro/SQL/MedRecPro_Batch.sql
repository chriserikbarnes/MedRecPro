-- =====================================================================
-- MedRecPro Batch Maintenance: Materialized View Refresh
-- =====================================================================
-- Purpose: Creates and refreshes temp tables from complex views that
--          cannot be optimized with indexes alone due to STRING_AGG,
--          ROW_NUMBER(), and UNION ALL optimization barriers.
--
-- Strategy: Instead of SELECT FROM view (which materializes the entire
--           database before any filtering), the view CTE logic is inlined
--           with DocumentID batch predicates injected at the base CTE level.
--           This allows SQL Server to use IX_Section_DocumentID for index
--           seeks, processing ~100 documents per batch instead of 10K+ at once.
--
-- Tables Created:
--   tmp_SectionContent        - From vw_SectionContent logic (~274K rows)
--   tmp_LabelSectionMarkdown  - From vw_LabelSectionMarkdown logic (~274K rows)
--   tmp_InventorySummary      - From vw_InventorySummary (~50 rows)
--
-- Usage:
--   EXEC dbo.usp_RefreshTempTables;
--
-- Schedule: Run after each data import batch or on a maintenance schedule.
--
-- Compatibility: Azure SQL Database (SQL Server 2017+ for STRING_AGG)
--
-- Version History:
--   2026-02-09: Initial creation
--   2026-02-09: Rewrite — inline CTEs with document batching to avoid timeout
-- =====================================================================

IF OBJECT_ID('dbo.usp_RefreshTempTables', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_RefreshTempTables;
GO

CREATE PROCEDURE dbo.usp_RefreshTempTables
AS
BEGIN
    SET NOCOUNT ON;
    SET LOCK_TIMEOUT 0;                    -- no lock wait timeout
    SET QUERY_GOVERNOR_COST_LIMIT 0;       -- no cost-based query governor

    DECLARE @RowCount int;
    DECLARE @TotalRows int;
    DECLARE @StartTime datetime2 = SYSDATETIME();
    DECLARE @StepStart datetime2;
    DECLARE @ErrorMessage nvarchar(4000);
    DECLARE @ErrorSeverity int;
    DECLARE @ErrorState int;
    DECLARE @Msg nvarchar(500);

    -- Batch processing variables
    DECLARE @BatchSize int = 100;
    DECLARE @MinDocID int, @MaxDocID int;
    DECLARE @BatchStart int, @BatchEnd int;
    DECLARE @BatchNum int, @TotalBatches int;
    DECLARE @ElapsedSec int;

    RAISERROR('=================================================================', 0, 1) WITH NOWAIT;
    RAISERROR('usp_RefreshTempTables: Starting materialized view refresh', 0, 1) WITH NOWAIT;
    SET @Msg = 'Started at: ' + CONVERT(varchar(30), @StartTime, 121);
    RAISERROR(@Msg, 0, 1) WITH NOWAIT;
    RAISERROR('=================================================================', 0, 1) WITH NOWAIT;
    RAISERROR(' ', 0, 1) WITH NOWAIT;

    -- Get document ID range for batching
    SELECT @MinDocID = MIN(DocumentID), @MaxDocID = MAX(DocumentID) FROM dbo.Document;

    IF @MinDocID IS NULL
    BEGIN
        RAISERROR('No documents found. Exiting.', 0, 1) WITH NOWAIT;
        RETURN;
    END

    SET @TotalBatches = CEILING(CAST(@MaxDocID - @MinDocID + 1 AS float) / @BatchSize);
    RAISERROR('Document range: %d to %d', 0, 1, @MinDocID, @MaxDocID) WITH NOWAIT;
    RAISERROR('Batch size: %d documents (%d batches)', 0, 1, @BatchSize, @TotalBatches) WITH NOWAIT;
    RAISERROR(' ', 0, 1) WITH NOWAIT;

    -- =================================================================
    -- 1. tmp_SectionContent
    --    Inlined from vw_SectionContent with DocumentID batch predicate
    -- =================================================================
    BEGIN TRY
        SET @StepStart = SYSDATETIME();
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;
        RAISERROR('1/3: tmp_SectionContent', 0, 1) WITH NOWAIT;
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;

        -- Create table if it does not exist; truncate if it does
        IF OBJECT_ID('dbo.tmp_SectionContent', 'U') IS NULL
        BEGIN
            RAISERROR('  Creating table dbo.tmp_SectionContent...', 0, 1) WITH NOWAIT;

            CREATE TABLE dbo.tmp_SectionContent (
                DocumentID          int              NULL,
                SectionID           int              NULL,
                DocumentGUID        uniqueidentifier NULL,
                SetGUID             uniqueidentifier NULL,
                SectionGUID         uniqueidentifier NULL,
                VersionNumber       int              NULL,
                DocumentDisplayName varchar(255)     NULL,
                DocumentTitle       nvarchar(max)    NULL,
                SectionCode         varchar(50)      NULL,
                SectionDisplayName  varchar(255)     NULL,
                SectionTitle        nvarchar(max)    NULL,
                ContentText         nvarchar(max)    NULL,
                SequenceNumber      int              NULL,
                ContentType         varchar(20)      NULL,
                SectionCodeSystem   varchar(100)     NULL
            ) WITH (DATA_COMPRESSION = PAGE);

            RAISERROR('  Table created with PAGE compression.', 0, 1) WITH NOWAIT;
        END
        ELSE
        BEGIN
            RAISERROR('  Truncating existing table...', 0, 1) WITH NOWAIT;
            TRUNCATE TABLE dbo.tmp_SectionContent;

            -- Self-heal: narrow SequenceNumber to int if previously created as bigint
            IF EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'tmp_SectionContent'
                  AND COLUMN_NAME = 'SequenceNumber'
                  AND DATA_TYPE = 'bigint'
            )
            BEGIN
                RAISERROR('  Narrowing SequenceNumber to int (dropping/recreating dependent index)...', 0, 1) WITH NOWAIT;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.tmp_SectionContent') AND name = 'IX_tmp_SectionContent_DocumentGUID')
                    DROP INDEX IX_tmp_SectionContent_DocumentGUID ON dbo.tmp_SectionContent;

                ALTER TABLE dbo.tmp_SectionContent ALTER COLUMN SequenceNumber int NULL;

                CREATE NONCLUSTERED INDEX IX_tmp_SectionContent_DocumentGUID
                    ON dbo.tmp_SectionContent (DocumentGUID)
                    INCLUDE (SectionID, SectionCode, SequenceNumber, DocumentID, SetGUID, VersionNumber, SectionGUID)
                    WITH (DATA_COMPRESSION = PAGE);
            END
        END

        -- Populate in batches using inlined CTE logic
        -- The DocumentID predicate is injected into the SectionToParent CTE
        -- so SQL Server can use IX_Section_DocumentID for index seeks
        RAISERROR('  Populating (batched by DocumentID)...', 0, 1) WITH NOWAIT;

        SET @TotalRows = 0;
        SET @BatchNum = 0;
        SET @BatchStart = @MinDocID;

        WHILE @BatchStart <= @MaxDocID
        BEGIN
            SET @BatchEnd = @BatchStart + @BatchSize - 1;
            SET @BatchNum = @BatchNum + 1;

            ;WITH
            -- Map content sections to their display parent (FILTERED by batch)
            SectionToParent AS (
                SELECT
                    s.SectionID AS ContentSectionID,
                    s.SectionID AS DisplaySectionID,
                    s.DocumentID,
                    0 AS HierarchySequence
                FROM dbo.Section s
                WHERE s.Title IS NOT NULL
                  AND s.DocumentID BETWEEN @BatchStart AND @BatchEnd

                UNION ALL

                SELECT
                    child.SectionID AS ContentSectionID,
                    parent.SectionID AS DisplaySectionID,
                    parent.DocumentID,
                    sh.SequenceNumber AS HierarchySequence
                FROM dbo.SectionHierarchy sh
                INNER JOIN dbo.Section child ON sh.ChildSectionID = child.SectionID
                INNER JOIN dbo.Section parent ON sh.ParentSectionID = parent.SectionID
                WHERE child.Title IS NULL
                  AND parent.Title IS NOT NULL
                  AND parent.DocumentID BETWEEN @BatchStart AND @BatchEnd
            ),

            -- Get all lists with their items (raw text, no formatting)
            -- Not filtered directly — scoped by join to SectionToParent via AllSectionContent
            ListContent AS (
                SELECT
                    tl.SectionTextContentID,
                    STRING_AGG(
                        CAST(
                            COALESCE(
                                CASE WHEN tli.ItemCaption IS NOT NULL AND LEN(tli.ItemCaption) > 0
                                     THEN tli.ItemCaption + ' '
                                     ELSE ''
                                END, ''
                            ) +
                            COALESCE(tli.ItemText, '')
                        AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (ORDER BY tli.SequenceNumber) AS ListText
                FROM dbo.TextList tl
                INNER JOIN dbo.TextListItem tli ON tl.TextListID = tli.TextListID
                WHERE tli.ItemText IS NOT NULL OR tli.ItemCaption IS NOT NULL
                GROUP BY tl.SectionTextContentID, tl.TextListID
            ),

            -- Aggregate table cells into rows (raw text, no formatting)
            TableCellsPerRow AS (
                SELECT
                    ttr.TextTableRowID,
                    ttr.TextTableID,
                    ttr.RowGroupType,
                    ttr.SequenceNumber AS RowSequence,
                    STRING_AGG(
                        CAST(COALESCE(ttc.CellText, '') AS NVARCHAR(MAX)),
                        ' '
                    ) WITHIN GROUP (ORDER BY ttc.SequenceNumber) AS RowText
                FROM dbo.TextTableRow ttr
                INNER JOIN dbo.TextTableCell ttc ON ttr.TextTableRowID = ttc.TextTableRowID
                GROUP BY ttr.TextTableRowID, ttr.TextTableID, ttr.RowGroupType, ttr.SequenceNumber
            ),

            -- Aggregate rows into complete tables (raw text, no formatting)
            TableContent AS (
                SELECT
                    tt.SectionTextContentID,
                    COALESCE(
                        CASE WHEN tt.Caption IS NOT NULL AND LEN(tt.Caption) > 0
                             THEN tt.Caption + CHAR(13) + CHAR(10)
                             ELSE ''
                        END, ''
                    ) +
                    STRING_AGG(
                        CAST(tcpr.RowText AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (
                        ORDER BY
                            CASE tcpr.RowGroupType
                                WHEN 'thead' THEN 1
                                WHEN 'tbody' THEN 2
                                WHEN 'tfoot' THEN 3
                                ELSE 2
                            END,
                            tcpr.RowSequence
                    ) AS TableText
                FROM dbo.TextTable tt
                INNER JOIN TableCellsPerRow tcpr ON tt.TextTableID = tcpr.TextTableID
                GROUP BY tt.SectionTextContentID, tt.TextTableID, tt.Caption
            ),

            -- Combine all content types with proper sequencing
            AllSectionContent AS (
                -- Regular text paragraphs (raw content text)
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.DocumentID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    0 AS SubSequence,
                    stc.ContentText AS RawContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                WHERE stc.ContentText IS NOT NULL
                  AND LEN(LTRIM(RTRIM(stc.ContentText))) > 0

                UNION ALL

                -- List content (attached to SectionTextContent)
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.DocumentID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    1 AS SubSequence,
                    lc.ListText AS RawContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                INNER JOIN ListContent lc ON stc.SectionTextContentID = lc.SectionTextContentID

                UNION ALL

                -- Table content (attached to SectionTextContent)
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.DocumentID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    2 AS SubSequence,
                    tc.TableText AS RawContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                INNER JOIN TableContent tc ON stc.SectionTextContentID = tc.SectionTextContentID
            ),

            -- Aggregate all content per section
            AggregatedContent AS (
                SELECT
                    SectionID,
                    DocumentID,
                    STRING_AGG(
                        CAST(RawContent AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (ORDER BY HierarchySequence, SequenceNumber, SubSequence) AS AggregatedText,
                    MIN(SequenceNumber) AS MinSequenceNumber
                FROM AllSectionContent
                WHERE RawContent IS NOT NULL
                  AND LEN(LTRIM(RTRIM(RawContent))) > 0
                GROUP BY SectionID, DocumentID
            )

            INSERT INTO dbo.tmp_SectionContent (
                DocumentID, SectionID, DocumentGUID, SetGUID, SectionGUID,
                VersionNumber, DocumentDisplayName, DocumentTitle,
                SectionCode, SectionDisplayName, SectionTitle,
                ContentText, SequenceNumber, ContentType, SectionCodeSystem
            )
            SELECT
                d.DocumentID,
                s.SectionID,
                d.DocumentGUID,
                d.SetGUID,
                s.SectionGUID,
                d.VersionNumber,
                d.DocumentDisplayName,
                d.Title AS DocumentTitle,
                COALESCE(NULLIF(s.SectionCode, ''), NULLIF(ps.SectionCode, '')) AS SectionCode,
                COALESCE(NULLIF(s.SectionDisplayName, ''), NULLIF(ps.SectionDisplayName, '')) AS SectionDisplayName,
                s.Title AS SectionTitle,
                ac.AggregatedText AS ContentText,
                CAST(ROW_NUMBER() OVER (PARTITION BY d.DocumentID ORDER BY s.SectionID) AS int) AS SequenceNumber,
                'Aggregated' AS ContentType,
                COALESCE(NULLIF(s.SectionCodeSystem, ''), NULLIF(ps.SectionCodeSystem, '')) AS SectionCodeSystem
            FROM dbo.[Document] d
            INNER JOIN dbo.Section s ON d.DocumentID = s.DocumentID
            INNER JOIN AggregatedContent ac ON s.SectionID = ac.SectionID AND ac.DocumentID = d.DocumentID
            LEFT JOIN dbo.SectionHierarchy sh_parent ON s.SectionID = sh_parent.ChildSectionID
            LEFT JOIN dbo.Section ps ON sh_parent.ParentSectionID = ps.SectionID
            WHERE s.Title IS NOT NULL
              AND ac.AggregatedText IS NOT NULL
              AND LEN(ac.AggregatedText) > 3
              AND d.DocumentID BETWEEN @BatchStart AND @BatchEnd;

            SET @RowCount = @@ROWCOUNT;
            SET @TotalRows = @TotalRows + @RowCount;

            IF @BatchNum % 10 = 0 OR @BatchStart + @BatchSize > @MaxDocID
                RAISERROR('  Batch %d/%d (DocID %d-%d) — %d rows so far', 0, 1,
                    @BatchNum, @TotalBatches, @BatchStart, @BatchEnd, @TotalRows) WITH NOWAIT;

            SET @BatchStart = @BatchEnd + 1;
        END

        RAISERROR('  Total inserted: %d rows.', 0, 1, @TotalRows) WITH NOWAIT;

        -- Create indexes (check existence first, do not drop/create)
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_SectionContent')
              AND name = 'CIX_tmp_SectionContent'
        )
        BEGIN
            RAISERROR('  Creating clustered index CIX_tmp_SectionContent...', 0, 1) WITH NOWAIT;
            CREATE CLUSTERED INDEX CIX_tmp_SectionContent
                ON dbo.tmp_SectionContent (DocumentID, SectionID)
                WITH (DATA_COMPRESSION = PAGE);
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_SectionContent')
              AND name = 'IX_tmp_SectionContent_DocumentGUID'
        )
        BEGIN
            RAISERROR('  Creating index IX_tmp_SectionContent_DocumentGUID...', 0, 1) WITH NOWAIT;
            CREATE NONCLUSTERED INDEX IX_tmp_SectionContent_DocumentGUID
                ON dbo.tmp_SectionContent (DocumentGUID)
                INCLUDE (SectionID, SectionCode, SequenceNumber, DocumentID, SetGUID, VersionNumber, SectionGUID)
                WITH (DATA_COMPRESSION = PAGE);
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_SectionContent')
              AND name = 'IX_tmp_SectionContent_SectionGUID'
        )
        BEGIN
            RAISERROR('  Creating index IX_tmp_SectionContent_SectionGUID...', 0, 1) WITH NOWAIT;
            CREATE NONCLUSTERED INDEX IX_tmp_SectionContent_SectionGUID
                ON dbo.tmp_SectionContent (SectionGUID)
                WITH (DATA_COMPRESSION = PAGE);
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_SectionContent')
              AND name = 'IX_tmp_SectionContent_SectionCode'
        )
        BEGIN
            RAISERROR('  Creating index IX_tmp_SectionContent_SectionCode...', 0, 1) WITH NOWAIT;
            CREATE NONCLUSTERED INDEX IX_tmp_SectionContent_SectionCode
                ON dbo.tmp_SectionContent (SectionCode)
                INCLUDE (DocumentGUID)
                WITH (DATA_COMPRESSION = PAGE);
        END

        SET @ElapsedSec = DATEDIFF(SECOND, @StepStart, SYSDATETIME());
        RAISERROR('  Completed in %d seconds.', 0, 1, @ElapsedSec) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        SET @ErrorSeverity = ERROR_SEVERITY();
        SET @ErrorState = ERROR_STATE();
        SET @Msg = '  ERROR refreshing tmp_SectionContent: ' + @ErrorMessage;
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        RAISERROR('  (Failed at batch %d, DocID range %d-%d)', 0, 1, @BatchNum, @BatchStart, @BatchEnd) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END CATCH

    -- =================================================================
    -- 2. tmp_LabelSectionMarkdown
    --    Inlined from vw_LabelSectionMarkdown with DocumentID batch predicate
    -- =================================================================
    BEGIN TRY
        SET @StepStart = SYSDATETIME();
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;
        RAISERROR('2/3: tmp_LabelSectionMarkdown', 0, 1) WITH NOWAIT;
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;

        -- Create table if it does not exist; truncate if it does
        IF OBJECT_ID('dbo.tmp_LabelSectionMarkdown', 'U') IS NULL
        BEGIN
            RAISERROR('  Creating table dbo.tmp_LabelSectionMarkdown...', 0, 1) WITH NOWAIT;

            CREATE TABLE dbo.tmp_LabelSectionMarkdown (
                DocumentGUID      uniqueidentifier NULL,
                SetGUID           uniqueidentifier NULL,
                DocumentTitle     nvarchar(max)    NULL,
                SectionCode       varchar(50)      NULL,
                SectionTitle      nvarchar(max)    NULL,
                SectionKey        nvarchar(max)    NULL,
                FullSectionText   nvarchar(max)    NULL,
                ContentBlockCount int              NULL
            ) WITH (DATA_COMPRESSION = PAGE);

            RAISERROR('  Table created with PAGE compression.', 0, 1) WITH NOWAIT;
        END
        ELSE
        BEGIN
            RAISERROR('  Truncating existing table...', 0, 1) WITH NOWAIT;
            TRUNCATE TABLE dbo.tmp_LabelSectionMarkdown;

            -- Self-heal: widen SectionKey if previously created with bounded size
            IF EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'tmp_LabelSectionMarkdown'
                  AND COLUMN_NAME = 'SectionKey'
                  AND CHARACTER_MAXIMUM_LENGTH <> -1  -- -1 = max
            )
            BEGIN
                RAISERROR('  Widening SectionKey to nvarchar(max)...', 0, 1) WITH NOWAIT;
                ALTER TABLE dbo.tmp_LabelSectionMarkdown ALTER COLUMN SectionKey nvarchar(max) NULL;
            END
        END

        -- Populate in batches using inlined CTE logic
        RAISERROR('  Populating (batched by DocumentID)...', 0, 1) WITH NOWAIT;

        SET @TotalRows = 0;
        SET @BatchNum = 0;
        SET @BatchStart = @MinDocID;

        WHILE @BatchStart <= @MaxDocID
        BEGIN
            SET @BatchEnd = @BatchStart + @BatchSize - 1;
            SET @BatchNum = @BatchNum + 1;

            ;WITH
            -- Convert SPL content tags to Markdown
            -- Not filtered directly — scoped by join chain through SectionToParent
            TextToMarkdown AS (
                SELECT
                    stc.SectionTextContentID,
                    stc.SectionID,
                    stc.SequenceNumber,
                    stc.ContentType,
                    stc.ParentSectionTextContentID,
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                REPLACE(
                                                    REPLACE(
                                                        REPLACE(stc.ContentText,
                                                            '<content styleCode="bold">', '**'),
                                                        '</content>', ''),
                                                    '<content styleCode="italics">', '*'),
                                                '</content>', ''),
                                            '<content styleCode="underline">', '_'),
                                        '</content>', ''),
                                    '<content styleCode=', ''),
                                '">', ''),
                            '/>', ''),
                        '</content>', '') AS ContentMarkdown
                FROM dbo.SectionTextContent stc
                WHERE stc.ContentText IS NOT NULL
                  AND LEN(LTRIM(RTRIM(stc.ContentText))) > 0
            ),

            -- Get all lists with their items converted to markdown
            ListMarkdown AS (
                SELECT
                    tl.SectionTextContentID,
                    tl.ListType,
                    STRING_AGG(
                        CAST(
                            CASE
                                WHEN tl.ListType = 'ordered'
                                THEN CAST(tli.SequenceNumber AS VARCHAR(10)) + '. '
                                ELSE '- '
                            END +
                            COALESCE(
                                CASE WHEN tli.ItemCaption IS NOT NULL AND LEN(tli.ItemCaption) > 0
                                     THEN '**' + tli.ItemCaption + '** '
                                     ELSE ''
                                END, ''
                            ) +
                            COALESCE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                REPLACE(
                                                    REPLACE(
                                                        REPLACE(
                                                            REPLACE(
                                                                REPLACE(
                                                                    REPLACE(tli.ItemText,
                                                                        '<content styleCode="bold">', '**'),
                                                                    '</content>', ''),
                                                                '<content styleCode="italics">', '*'),
                                                            '</content>', ''),
                                                        '<content styleCode="underline">', '_'),
                                                    '</content>', ''),
                                                '<content styleCode=', ''),
                                            '">', ''),
                                        '/>', ''),
                                    '</content>', ''),
                                ''
                            )
                        AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (ORDER BY tli.SequenceNumber) AS ListMarkdownText
                FROM dbo.TextList tl
                INNER JOIN dbo.TextListItem tli ON tl.TextListID = tli.TextListID
                WHERE tli.ItemText IS NOT NULL OR tli.ItemCaption IS NOT NULL
                GROUP BY tl.SectionTextContentID, tl.TextListID, tl.ListType
            ),

            -- Aggregate table cells into rows with markdown formatting
            TableCellsPerRow AS (
                SELECT
                    ttr.TextTableRowID,
                    ttr.TextTableID,
                    ttr.RowGroupType,
                    ttr.SequenceNumber AS RowSequence,
                    STRING_AGG(
                        CAST(
                            COALESCE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(
                                                REPLACE(
                                                    REPLACE(
                                                        REPLACE(
                                                            REPLACE(
                                                                REPLACE(
                                                                    REPLACE(ttc.CellText,
                                                                        '<content styleCode="bold">', '**'),
                                                                    '</content>', ''),
                                                                '<content styleCode="italics">', '*'),
                                                            '</content>', ''),
                                                        '<content styleCode="underline">', '_'),
                                                    '</content>', ''),
                                                '<content styleCode=', ''),
                                            '">', ''),
                                        '/>', ''),
                                    '</content>', ''),
                                ''
                            )
                        AS NVARCHAR(MAX)),
                        ' | '
                    ) WITHIN GROUP (ORDER BY ttc.SequenceNumber) AS RowCells,
                    COUNT(*) AS CellCount
                FROM dbo.TextTableRow ttr
                INNER JOIN dbo.TextTableCell ttc ON ttr.TextTableRowID = ttc.TextTableRowID
                GROUP BY ttr.TextTableRowID, ttr.TextTableID, ttr.RowGroupType, ttr.SequenceNumber
            ),

            -- Aggregate rows into complete tables with markdown formatting
            TableMarkdown AS (
                SELECT
                    tt.SectionTextContentID,
                    COALESCE(
                        CASE WHEN tt.Caption IS NOT NULL AND LEN(tt.Caption) > 0
                             THEN '**' + tt.Caption + '**' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10)
                             ELSE ''
                        END, ''
                    ) +
                    STRING_AGG(
                        CAST(
                            '| ' + tcpr.RowCells + ' |' +
                            CASE
                                WHEN tcpr.RowGroupType = 'thead' OR (tt.HasHeader = 1 AND tcpr.RowSequence = 1)
                                THEN CHAR(13) + CHAR(10) + '|' + REPLICATE(' --- |', tcpr.CellCount)
                                ELSE ''
                            END
                        AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (
                        ORDER BY
                            CASE tcpr.RowGroupType
                                WHEN 'thead' THEN 1
                                WHEN 'tbody' THEN 2
                                WHEN 'tfoot' THEN 3
                                ELSE 2
                            END,
                            tcpr.RowSequence
                    ) AS TableMarkdownText
                FROM dbo.TextTable tt
                INNER JOIN TableCellsPerRow tcpr ON tt.TextTableID = tcpr.TextTableID
                GROUP BY tt.SectionTextContentID, tt.TextTableID, tt.Caption, tt.HasHeader
            ),

            -- All sections with titles (FILTERED by batch)
            AllTitledSections AS (
                SELECT
                    s.SectionID,
                    s.SectionCode,
                    s.Title AS SectionTitle,
                    d.DocumentID,
                    d.DocumentGUID,
                    d.SetGUID,
                    d.Title AS DocumentTitle
                FROM dbo.Section s
                INNER JOIN dbo.[Document] d ON s.DocumentID = d.DocumentID
                WHERE s.Title IS NOT NULL
                  AND s.DocumentID BETWEEN @BatchStart AND @BatchEnd
            ),

            -- Map content sections to their display parent (FILTERED by batch)
            SectionToParent AS (
                SELECT
                    s.SectionID AS ContentSectionID,
                    s.SectionID AS DisplaySectionID,
                    0 AS HierarchySequence
                FROM dbo.Section s
                WHERE s.Title IS NOT NULL
                  AND s.DocumentID BETWEEN @BatchStart AND @BatchEnd

                UNION ALL

                SELECT
                    child.SectionID AS ContentSectionID,
                    parent.SectionID AS DisplaySectionID,
                    sh.SequenceNumber AS HierarchySequence
                FROM dbo.SectionHierarchy sh
                INNER JOIN dbo.Section child ON sh.ChildSectionID = child.SectionID
                INNER JOIN dbo.Section parent ON sh.ParentSectionID = parent.SectionID
                WHERE child.Title IS NULL
                  AND parent.Title IS NOT NULL
                  AND parent.DocumentID BETWEEN @BatchStart AND @BatchEnd
            ),

            -- Combine all content types with proper sequencing
            AllSectionContent AS (
                -- Regular text paragraphs
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    0 AS SubSequence,
                    tm.ContentMarkdown AS MarkdownContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                INNER JOIN TextToMarkdown tm ON stc.SectionTextContentID = tm.SectionTextContentID
                WHERE stc.ContentText IS NOT NULL
                  AND LEN(LTRIM(RTRIM(stc.ContentText))) > 0

                UNION ALL

                -- List content
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    1 AS SubSequence,
                    lm.ListMarkdownText AS MarkdownContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                INNER JOIN ListMarkdown lm ON stc.SectionTextContentID = lm.SectionTextContentID

                UNION ALL

                -- Table content
                SELECT
                    sp.DisplaySectionID AS SectionID,
                    sp.HierarchySequence,
                    stc.SequenceNumber,
                    2 AS SubSequence,
                    tbm.TableMarkdownText AS MarkdownContent
                FROM SectionToParent sp
                INNER JOIN dbo.SectionTextContent stc ON sp.ContentSectionID = stc.SectionID
                INNER JOIN TableMarkdown tbm ON stc.SectionTextContentID = tbm.SectionTextContentID
            ),

            -- Aggregate content per section
            AggregatedContent AS (
                SELECT
                    SectionID,
                    STRING_AGG(
                        CAST(MarkdownContent AS NVARCHAR(MAX)),
                        CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10)
                    ) WITHIN GROUP (ORDER BY HierarchySequence, SequenceNumber, SubSequence) AS AggregatedText,
                    COUNT(*) AS ContentBlockCount
                FROM AllSectionContent
                WHERE MarkdownContent IS NOT NULL
                  AND LEN(LTRIM(RTRIM(MarkdownContent))) > 0
                GROUP BY SectionID
            )

            INSERT INTO dbo.tmp_LabelSectionMarkdown (
                DocumentGUID, SetGUID, DocumentTitle, SectionCode,
                SectionTitle, SectionKey, FullSectionText, ContentBlockCount
            )
            SELECT
                ats.DocumentGUID,
                ats.SetGUID,
                ats.DocumentTitle,
                ats.SectionCode,
                ats.SectionTitle,
                CAST(ats.DocumentGUID AS VARCHAR(36)) + '|' +
                    COALESCE(ats.SectionCode, 'NULL') + '|' +
                    COALESCE(ats.SectionTitle, '') AS SectionKey,
                '## ' + ats.SectionTitle +
                    CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
                    COALESCE(ac.AggregatedText, '') AS FullSectionText,
                COALESCE(ac.ContentBlockCount, 0) AS ContentBlockCount
            FROM AllTitledSections ats
            LEFT JOIN AggregatedContent ac ON ats.SectionID = ac.SectionID;

            SET @RowCount = @@ROWCOUNT;
            SET @TotalRows = @TotalRows + @RowCount;

            IF @BatchNum % 10 = 0 OR @BatchStart + @BatchSize > @MaxDocID
                RAISERROR('  Batch %d/%d (DocID %d-%d) — %d rows so far', 0, 1,
                    @BatchNum, @TotalBatches, @BatchStart, @BatchEnd, @TotalRows) WITH NOWAIT;

            SET @BatchStart = @BatchEnd + 1;
        END

        RAISERROR('  Total inserted: %d rows.', 0, 1, @TotalRows) WITH NOWAIT;

        -- Create indexes (check existence first, do not drop/create)
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_LabelSectionMarkdown')
              AND name = 'CIX_tmp_LabelSectionMarkdown'
        )
        BEGIN
            RAISERROR('  Creating clustered index CIX_tmp_LabelSectionMarkdown...', 0, 1) WITH NOWAIT;
            CREATE CLUSTERED INDEX CIX_tmp_LabelSectionMarkdown
                ON dbo.tmp_LabelSectionMarkdown (DocumentGUID, SectionCode)
                WITH (DATA_COMPRESSION = PAGE);
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_LabelSectionMarkdown')
              AND name = 'IX_tmp_LabelSectionMarkdown_SetGUID'
        )
        BEGIN
            RAISERROR('  Creating index IX_tmp_LabelSectionMarkdown_SetGUID...', 0, 1) WITH NOWAIT;
            CREATE NONCLUSTERED INDEX IX_tmp_LabelSectionMarkdown_SetGUID
                ON dbo.tmp_LabelSectionMarkdown (SetGUID)
                WITH (DATA_COMPRESSION = PAGE);
        END

        SET @ElapsedSec = DATEDIFF(SECOND, @StepStart, SYSDATETIME());
        RAISERROR('  Completed in %d seconds.', 0, 1, @ElapsedSec) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        SET @ErrorSeverity = ERROR_SEVERITY();
        SET @ErrorState = ERROR_STATE();
        SET @Msg = '  ERROR refreshing tmp_LabelSectionMarkdown: ' + @ErrorMessage;
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        RAISERROR('  (Failed at batch %d, DocID range %d-%d)', 0, 1, @BatchNum, @BatchStart, @BatchEnd) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END CATCH

    -- =================================================================
    -- 3. tmp_InventorySummary (from vw_InventorySummary)
    --    Simple SELECT — only ~50 rows, no batching needed
    -- =================================================================
    BEGIN TRY
        SET @StepStart = SYSDATETIME();
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;
        RAISERROR('3/3: tmp_InventorySummary', 0, 1) WITH NOWAIT;
        RAISERROR('-----------------------------------------------------------------', 0, 1) WITH NOWAIT;

        -- Create table if it does not exist; truncate if it does
        IF OBJECT_ID('dbo.tmp_InventorySummary', 'U') IS NULL
        BEGIN
            RAISERROR('  Creating table dbo.tmp_InventorySummary...', 0, 1) WITH NOWAIT;

            CREATE TABLE dbo.tmp_InventorySummary (
                Category       varchar(30)   NULL,
                Dimension      varchar(30)   NULL,
                DimensionValue nvarchar(255) NULL,
                ItemCount      int           NULL,
                SortOrder      int           NULL
            );

            RAISERROR('  Table created.', 0, 1) WITH NOWAIT;
        END
        ELSE
        BEGIN
            RAISERROR('  Truncating existing table...', 0, 1) WITH NOWAIT;
            TRUNCATE TABLE dbo.tmp_InventorySummary;
        END

        -- Populate from view (simple — ~50 rows)
        RAISERROR('  Populating from vw_InventorySummary...', 0, 1) WITH NOWAIT;

        INSERT INTO dbo.tmp_InventorySummary (
            Category, Dimension, DimensionValue, ItemCount, SortOrder
        )
        SELECT
            Category, Dimension, DimensionValue, ItemCount, SortOrder
        FROM dbo.vw_InventorySummary;

        SET @RowCount = @@ROWCOUNT;
        RAISERROR('  Inserted %d rows.', 0, 1, @RowCount) WITH NOWAIT;

        -- Create index (check existence first)
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID('dbo.tmp_InventorySummary')
              AND name = 'CIX_tmp_InventorySummary'
        )
        BEGIN
            RAISERROR('  Creating clustered index CIX_tmp_InventorySummary...', 0, 1) WITH NOWAIT;
            CREATE CLUSTERED INDEX CIX_tmp_InventorySummary
                ON dbo.tmp_InventorySummary (Category, SortOrder);
        END

        SET @ElapsedSec = DATEDIFF(SECOND, @StepStart, SYSDATETIME());
        RAISERROR('  Completed in %d seconds.', 0, 1, @ElapsedSec) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        SET @ErrorSeverity = ERROR_SEVERITY();
        SET @ErrorState = ERROR_STATE();
        SET @Msg = '  ERROR refreshing tmp_InventorySummary: ' + @ErrorMessage;
        RAISERROR(@Msg, 0, 1) WITH NOWAIT;
        RAISERROR(' ', 0, 1) WITH NOWAIT;
    END CATCH

    -- =================================================================
    -- Summary
    -- =================================================================
    SET @ElapsedSec = DATEDIFF(SECOND, @StartTime, SYSDATETIME());
    RAISERROR('=================================================================', 0, 1) WITH NOWAIT;
    RAISERROR('usp_RefreshTempTables: Complete', 0, 1) WITH NOWAIT;
    RAISERROR('Total elapsed: %d seconds.', 0, 1, @ElapsedSec) WITH NOWAIT;
    RAISERROR('=================================================================', 0, 1) WITH NOWAIT;

END
GO

PRINT 'Created procedure: dbo.usp_RefreshTempTables';
GO
