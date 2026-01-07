

/*******************************************************************************/
/*                                                                             */
/*  MedRecPro SPL Label Database Indexes                                       */
/*  SQL Server 2012 Compatible                                                 */
/*                                                                             */
/*  Purpose: Creates standard indexes and full-text indexes for optimal        */
/*           query performance on pharmaceutical labeling data structures.     */
/*                                                                             */
/*  Naming Convention: IX_[Table]_[Col1]_[ColN]_on_[Key1]_on_[KeyN]           */
/*                                                                             */
/*  Author: Generated for MedRecPro                                            */
/*  Date: 2025-12-08                                                           */
/*                                                                             */
/*  Notes:                                                                     */
/*    - All scripts are idempotent and safe to run multiple times             */
/*    - No hard-coded foreign key constraints (performance optimization)       */
/*    - Extended properties added for documentation                            */
/*    - Full-text indexes require Full-Text Search feature enabled            */
/*                                                                             */
/*******************************************************************************/

SET NOCOUNT ON;
GO

/*******************************************************************************/
/*                                                                             */
/*  SECTION 1: FULL-TEXT CATALOG SETUP                                         */
/*  Creates the full-text catalog if it does not already exist.                */
/*                                                                             */
/*******************************************************************************/

-- Create full-text catalog for SPL content searchability
IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'FTC_MedRecPro_SPL')
BEGIN
    CREATE FULLTEXT CATALOG FTC_MedRecPro_SPL AS DEFAULT;
    
    PRINT 'Created full-text catalog: FTC_MedRecPro_SPL';
END
ELSE
BEGIN
    PRINT 'Full-text catalog FTC_MedRecPro_SPL already exists.';
END
GO

/*******************************************************************************/
/*                                                                             */
/*  SECTION 2: FULL-TEXT INDEXES                                               */
/*  Creates full-text indexes for columns requiring text search capabilities.  */
/*  Enables semantic search across pharmaceutical labeling content.            */
/*                                                                             */
/*  Uses dynamic SQL to lookup actual PK index names since they may be         */
/*  auto-generated (e.g., PK__TableNam__XXXX) rather than explicitly named.    */
/*                                                                             */
/*******************************************************************************/

--#region Full-Text Index Creation Procedure

/**************************************************************/
-- Helper procedure to create full-text indexes dynamically
-- Purpose: Looks up actual PK index name and creates full-text index
-- Note: This handles auto-generated PK constraint names
-- See also: Label classes

IF OBJECT_ID('dbo.usp_CreateFullTextIndex', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_CreateFullTextIndex;
GO

CREATE PROCEDURE dbo.usp_CreateFullTextIndex
    @TableName NVARCHAR(128),
    @ColumnList NVARCHAR(MAX),
    @CatalogName NVARCHAR(128) = 'FTC_MedRecPro_SPL'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @PKIndexName NVARCHAR(128);
    DECLARE @SQL NVARCHAR(MAX);
    DECLARE @ObjectId INT;
    
    -- Get the object ID for the table
    SET @ObjectId = OBJECT_ID(@TableName);
    
    IF @ObjectId IS NULL
    BEGIN
        PRINT 'WARNING: Table ' + @TableName + ' does not exist.';
        RETURN;
    END
    
    -- Check if full-text index already exists on this table
    IF EXISTS (
        SELECT 1 FROM sys.fulltext_indexes 
        WHERE object_id = @ObjectId
    )
    BEGIN
        PRINT 'Full-text index on ' + @TableName + ' already exists.';
        RETURN;
    END
    
    -- Find the primary key index name (handles auto-generated names)
    SELECT @PKIndexName = i.name
    FROM sys.indexes i
    WHERE i.object_id = @ObjectId
      AND i.is_primary_key = 1;
    
    IF @PKIndexName IS NULL
    BEGIN
        PRINT 'WARNING: ' + @TableName + ' does not have a primary key index.';
        RETURN;
    END
    
    -- Build and execute the CREATE FULLTEXT INDEX statement
    SET @SQL = 'CREATE FULLTEXT INDEX ON ' + QUOTENAME(@TableName) + '(' + @ColumnList + ') '
             + 'KEY INDEX ' + QUOTENAME(@PKIndexName) + ' '
             + 'ON ' + QUOTENAME(@CatalogName) + ' '
             + 'WITH CHANGE_TRACKING AUTO;';
    
    BEGIN TRY
        EXEC sp_executesql @SQL;
        PRINT 'Created full-text index on ' + @TableName + '(' + @ColumnList + ') using key index ' + @PKIndexName;
    END TRY
    BEGIN CATCH
        PRINT 'ERROR creating full-text index on ' + @TableName + ': ' + ERROR_MESSAGE();
    END CATCH
END
GO

--#endregion

--#region Full-Text Index: SectionTextContent.ContentText

/**************************************************************/
-- Full-Text Index on SectionTextContent.ContentText
-- Purpose: Enables full-text search across paragraph content, 
--          list descriptions, and table text within SPL sections.
-- Usage: Primary search interface for labeling content queries
-- See also: Label.SectionTextContent

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'SectionTextContent', 
    @ColumnList = 'ContentText';
GO

--#endregion

--#region Full-Text Index: Section.Title

/**************************************************************/
-- Full-Text Index on Section.Title
-- Purpose: Enables full-text search across section titles
--          for navigation and content discovery
-- Usage: Section lookup by title, document navigation
-- See also: Label.Section

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'Section', 
    @ColumnList = 'Title';
GO

--#endregion

--#region Full-Text Index: TextListItem.ItemText

/**************************************************************/
-- Full-Text Index on TextListItem.ItemText
-- Purpose: Enables full-text search across list item content
--          (bullet points, numbered lists in labeling)
-- Usage: Search within structured list content
-- See also: Label.TextListItem

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'TextListItem', 
    @ColumnList = 'ItemText';
GO

--#endregion

--#region Full-Text Index: Document.Title

/**************************************************************/
-- Full-Text Index on Document.Title
-- Purpose: Enables full-text search across SPL document titles
-- Usage: Document discovery and listing queries
-- See also: Label.Document

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'Document', 
    @ColumnList = 'Title';
GO

--#endregion

--#region Full-Text Index: Product.ProductName and DescriptionText

/**************************************************************/
-- Full-Text Index on Product.ProductName and DescriptionText
-- Purpose: Enables full-text search across proprietary drug names
--          and product descriptions
-- Usage: Product search by brand name or description
-- See also: Label.Product

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'Product', 
    @ColumnList = 'ProductName, DescriptionText';
GO

--#endregion

--#region Full-Text Index: GenericMedicine.GenericName and PhoneticName

/**************************************************************/
-- Full-Text Index on GenericMedicine.GenericName and PhoneticName
-- Purpose: Enables full-text search across generic drug names
-- Usage: Product search by non-proprietary name
-- See also: Label.GenericMedicine

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'GenericMedicine', 
    @ColumnList = 'GenericName, PhoneticName';
GO

--#endregion

--#region Full-Text Index: IngredientSubstance.SubstanceName

/**************************************************************/
-- Full-Text Index on IngredientSubstance.SubstanceName
-- Purpose: Enables full-text search across active/inactive ingredient names
-- Usage: Ingredient lookup, drug interaction research
-- See also: Label.IngredientSubstance

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'IngredientSubstance', 
    @ColumnList = 'SubstanceName';
GO

--#endregion

--#region Full-Text Index: Organization.OrganizationName

/**************************************************************/
-- Full-Text Index on Organization.OrganizationName
-- Purpose: Enables full-text search across labeler, manufacturer,
--          registrant, and establishment organization names
-- Usage: Organization search, regulatory lookup
-- See also: Label.Organization

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'Organization', 
    @ColumnList = 'OrganizationName';
GO

--#endregion

--#region Full-Text Index: TextTableCell.CellText

/**************************************************************/
-- Full-Text Index on TextTableCell.CellText
-- Purpose: Enables full-text search across table cell content
--          (clinical data tables, dosage tables, etc.)
-- Usage: Search within structured table data in labeling
-- See also: Label.TextTableCell

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'TextTableCell', 
    @ColumnList = 'CellText';
GO

--#endregion

--#region Full-Text Index: SectionExcerptHighlight.HighlightText

/**************************************************************/
-- Full-Text Index on SectionExcerptHighlight.HighlightText
-- Purpose: Enables full-text search across highlight/excerpt content
--          (Boxed Warnings, Indications highlights, etc.)
-- Usage: Search critical safety and regulatory highlight text
-- See also: Label.SectionExcerptHighlight

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'SectionExcerptHighlight', 
    @ColumnList = 'HighlightText';
GO

--#endregion

--#region Full-Text Index: ActiveMoiety.MoietyName

/**************************************************************/
-- Full-Text Index on ActiveMoiety.MoietyName
-- Purpose: Enables full-text search across active moiety names
-- Usage: Drug component search, pharmacological research
-- See also: Label.ActiveMoiety

EXEC dbo.usp_CreateFullTextIndex 
    @TableName = 'ActiveMoiety', 
    @ColumnList = 'MoietyName';
GO

--#endregion

--#region Cleanup Helper Procedure (Optional)

/**************************************************************/
-- Optionally drop the helper procedure after use
-- Uncomment the following lines if you want to remove it:

IF OBJECT_ID('dbo.usp_CreateFullTextIndex', 'P') IS NOT NULL
   DROP PROCEDURE dbo.usp_CreateFullTextIndex;
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 3: DOCUMENT & STRUCTURED BODY INDEXES                              */
/*  Core document hierarchy indexes for efficient document traversal.          */
/*                                                                             */
/*******************************************************************************/

--#region Document Table Indexes

/**************************************************************/
-- Index on Document.DocumentGUID
-- Purpose: Fast lookup by document unique identifier
-- Usage: API document retrieval by GUID
-- See also: Label.Document

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_DocumentGUID' AND object_id = OBJECT_ID('Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_DocumentGUID
    ON Document(DocumentGUID)
    WHERE DocumentGUID IS NOT NULL;
    
    PRINT 'Created index: IX_Document_DocumentGUID';
END
GO

-- Add extended property for documentation
IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('Document') 
    AND name = 'IX_Document_DocumentGUID_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_Document_DocumentGUID_Description',
        @value = N'Enables fast document lookup by globally unique identifier. Critical for API document retrieval operations.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Document',
        @level2type = N'INDEX', @level2name = N'IX_Document_DocumentGUID';
END
GO

/**************************************************************/
-- Index on Document.SetGUID
-- Purpose: Fast lookup by document set (all versions of a label)
-- Usage: Version history queries, document family lookup
-- See also: Label.Document.SetGUID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_SetGUID' AND object_id = OBJECT_ID('Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_SetGUID
    ON Document(SetGUID)
    WHERE SetGUID IS NOT NULL;
    
    PRINT 'Created index: IX_Document_SetGUID';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('Document') 
    AND name = 'IX_Document_SetGUID_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_Document_SetGUID_Description',
        @value = N'Enables fast lookup of all document versions within a set. Used for version history and document family queries.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Document',
        @level2type = N'INDEX', @level2name = N'IX_Document_SetGUID';
END
GO

/**************************************************************/
-- Composite Index on Document.DocumentCode with EffectiveTime
-- Purpose: Filter documents by type and date for regulatory queries
-- Usage: Document type filtering, chronological lookups
-- See also: Label.Document.DocumentCode, Label.Document.EffectiveTime

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Document_DocumentCode_EffectiveTime' AND object_id = OBJECT_ID('Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_DocumentCode_EffectiveTime
    ON Document(DocumentCode, EffectiveTime DESC)
    INCLUDE (DocumentGUID, Title, VersionNumber);
    
    PRINT 'Created index: IX_Document_DocumentCode_EffectiveTime';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('Document') 
    AND name = 'IX_Document_DocumentCode_EffectiveTime_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_Document_DocumentCode_EffectiveTime_Description',
        @value = N'Enables efficient filtering by document type code and effective date. Covers common regulatory compliance queries.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Document',
        @level2type = N'INDEX', @level2name = N'IX_Document_DocumentCode_EffectiveTime';
END
GO

--#endregion

--#region StructuredBody Table Indexes

/**************************************************************/
-- Index on StructuredBody.DocumentID
-- Purpose: Fast retrieval of structured bodies for a document
-- Usage: Document content loading
-- See also: Label.StructuredBody

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_StructuredBody_DocumentID' AND object_id = OBJECT_ID('StructuredBody'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_StructuredBody_DocumentID
    ON StructuredBody(DocumentID)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_StructuredBody_DocumentID';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('StructuredBody') 
    AND name = 'IX_StructuredBody_DocumentID_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_StructuredBody_DocumentID_Description',
        @value = N'Enables fast retrieval of structured body content for document rendering. Used in DTO building operations.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'StructuredBody',
        @level2type = N'INDEX', @level2name = N'IX_StructuredBody_DocumentID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 4: SECTION HIERARCHY INDEXES                                       */
/*  Indexes for efficient section navigation and content retrieval.            */
/*                                                                             */
/*******************************************************************************/

--#region Section Table Indexes

/**************************************************************/
-- Index on Section.StructuredBodyID
-- Purpose: Fast retrieval of sections for a structured body
-- Usage: Document content hierarchy building
-- See also: Label.Section

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Section_StructuredBodyID' AND object_id = OBJECT_ID('Section'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Section_StructuredBodyID
    ON Section(StructuredBodyID)
    INCLUDE (SectionCode, Title)
    WHERE StructuredBodyID IS NOT NULL;
    
    PRINT 'Created index: IX_Section_StructuredBodyID';
END
GO

/**************************************************************/
-- Index on Section.DocumentID
-- Purpose: Fast retrieval of all sections for a document
-- Usage: Direct section queries bypassing structured body
-- See also: Label.Section

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Section_DocumentID' AND object_id = OBJECT_ID('Section'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Section_DocumentID
    ON Section(DocumentID)
    INCLUDE (SectionCode, Title)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_Section_DocumentID';
END
GO

/**************************************************************/
-- Index on Section.SectionCode
-- Purpose: Fast lookup of sections by LOINC code
-- Usage: Regulatory section queries (e.g., find all Boxed Warnings)
-- See also: Label.Section.SectionCode

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Section_SectionCode_on_DocumentID' AND object_id = OBJECT_ID('Section'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Section_SectionCode_on_DocumentID
    ON Section(SectionCode, DocumentID)
    INCLUDE (Title, SectionGUID);
    
    PRINT 'Created index: IX_Section_SectionCode_on_DocumentID';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('Section') 
    AND name = 'IX_Section_SectionCode_on_DocumentID_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_Section_SectionCode_on_DocumentID_Description',
        @value = N'Enables fast section lookup by LOINC code across documents. Critical for regulatory compliance reporting.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Section',
        @level2type = N'INDEX', @level2name = N'IX_Section_SectionCode_on_DocumentID';
END
GO

/**************************************************************/
-- Index on Section.SectionGUID
-- Purpose: Fast lookup by section unique identifier
-- Usage: Cross-reference resolution within documents
-- See also: Label.Section.SectionGUID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Section_SectionGUID' AND object_id = OBJECT_ID('Section'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Section_SectionGUID
    ON Section(SectionGUID)
    WHERE SectionGUID IS NOT NULL;
    
    PRINT 'Created index: IX_Section_SectionGUID';
END
GO

--#endregion

--#region SectionHierarchy Table Indexes

/**************************************************************/
-- Index on SectionHierarchy.ParentSectionID
-- Purpose: Fast retrieval of child sections for a parent
-- Usage: Section tree traversal (top-down)
-- See also: Label.SectionHierarchy

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionHierarchy_ParentSectionID' AND object_id = OBJECT_ID('SectionHierarchy'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionHierarchy_ParentSectionID
    ON SectionHierarchy(ParentSectionID)
    INCLUDE (ChildSectionID, SequenceNumber)
    WHERE ParentSectionID IS NOT NULL;
    
    PRINT 'Created index: IX_SectionHierarchy_ParentSectionID';
END
GO

/**************************************************************/
-- Index on SectionHierarchy.ChildSectionID
-- Purpose: Fast retrieval of parent section for a child
-- Usage: Section tree traversal (bottom-up)
-- See also: Label.SectionHierarchy

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionHierarchy_ChildSectionID' AND object_id = OBJECT_ID('SectionHierarchy'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionHierarchy_ChildSectionID
    ON SectionHierarchy(ChildSectionID)
    INCLUDE (ParentSectionID, SequenceNumber)
    WHERE ChildSectionID IS NOT NULL;
    
    PRINT 'Created index: IX_SectionHierarchy_ChildSectionID';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('SectionHierarchy') 
    AND name = 'IX_SectionHierarchy_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_SectionHierarchy_Description',
        @value = N'Parent and child indexes enable bidirectional section tree traversal for document structure rendering.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'SectionHierarchy';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 5: SECTION TEXT CONTENT INDEXES                                    */
/*  Indexes for efficient content retrieval and rendering.                     */
/*                                                                             */
/*******************************************************************************/

--#region SectionTextContent Table Indexes

/**************************************************************/
-- Index on SectionTextContent.SectionID
-- Purpose: Fast retrieval of text content for a section
-- Usage: Section content rendering
-- See also: Label.SectionTextContent

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionTextContent_SectionID' AND object_id = OBJECT_ID('SectionTextContent'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionTextContent_SectionID
    ON SectionTextContent(SectionID)
    INCLUDE (ContentType, SequenceNumber, StyleCode)
    WHERE SectionID IS NOT NULL;
    
    PRINT 'Created index: IX_SectionTextContent_SectionID';
END
GO

/**************************************************************/
-- Index on SectionTextContent.ParentSectionTextContentID
-- Purpose: Fast retrieval of nested content blocks
-- Usage: Hierarchical content rendering (excerpts, highlights)
-- See also: Label.SectionTextContent.ParentSectionTextContentID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionTextContent_ParentSectionTextContentID' AND object_id = OBJECT_ID('SectionTextContent'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionTextContent_ParentSectionTextContentID
    ON SectionTextContent(ParentSectionTextContentID)
    INCLUDE (ContentType, SequenceNumber)
    WHERE ParentSectionTextContentID IS NOT NULL;
    
    PRINT 'Created index: IX_SectionTextContent_ParentSectionTextContentID';
END
GO

/**************************************************************/
-- Index on SectionTextContent.ContentType
-- Purpose: Filter content by type (Paragraph, List, Table, BlockImage)
-- Usage: Content type filtering for rendering optimization
-- See also: Label.SectionTextContent.ContentType

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionTextContent_ContentType_on_SectionID' AND object_id = OBJECT_ID('SectionTextContent'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionTextContent_ContentType_on_SectionID
    ON SectionTextContent(ContentType, SectionID)
    INCLUDE (SequenceNumber);

    PRINT 'Created index: IX_SectionTextContent_ContentType_on_SectionID';
END
GO

/**************************************************************/
-- Index on SectionTextContent.SectionID with ContentText
-- Purpose: Optimized for vw_ProductIndications view
-- Usage: Fast retrieval of section content text for indication queries
-- See also: vw_ProductIndications

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionTextContent_SectionID_ContentText' AND object_id = OBJECT_ID('SectionTextContent'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionTextContent_SectionID_ContentText
    ON SectionTextContent(SectionID)
    INCLUDE (SectionTextContentID, ContentText)
    WHERE SectionID IS NOT NULL;

    PRINT 'Created index: IX_SectionTextContent_SectionID_ContentText';
END
GO

--#endregion

--#region TextList Table Indexes

/**************************************************************/
-- Index on TextList.SectionTextContentID
-- Purpose: Fast retrieval of list definitions for content blocks
-- Usage: List rendering
-- See also: Label.TextList

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextList_SectionTextContentID' AND object_id = OBJECT_ID('TextList'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextList_SectionTextContentID
    ON TextList(SectionTextContentID)
    INCLUDE (ListType, StyleCode)
    WHERE SectionTextContentID IS NOT NULL;
    
    PRINT 'Created index: IX_TextList_SectionTextContentID';
END
GO

--#endregion

--#region TextListItem Table Indexes

/**************************************************************/
-- Index on TextListItem.TextListID
-- Purpose: Fast retrieval of list items for a list
-- Usage: List item rendering with ordering
-- See also: Label.TextListItem

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextListItem_TextListID_SequenceNumber' AND object_id = OBJECT_ID('TextListItem'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextListItem_TextListID_SequenceNumber
    ON TextListItem(TextListID, SequenceNumber)
    WHERE TextListID IS NOT NULL;
    
    PRINT 'Created index: IX_TextListItem_TextListID_SequenceNumber';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 6: TABLE CONTENT INDEXES                                           */
/*  Indexes for efficient table structure rendering.                           */
/*                                                                             */
/*******************************************************************************/

--#region TextTable Table Indexes

/**************************************************************/
-- Index on TextTable.SectionTextContentID
-- Purpose: Fast retrieval of table definitions for content blocks
-- Usage: Table rendering
-- See also: Label.TextTable

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextTable_SectionTextContentID' AND object_id = OBJECT_ID('TextTable'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextTable_SectionTextContentID
    ON TextTable(SectionTextContentID)
    WHERE SectionTextContentID IS NOT NULL;
    
    PRINT 'Created index: IX_TextTable_SectionTextContentID';
END
GO

--#endregion

--#region TextTableColumn Table Indexes

/**************************************************************/
-- Index on TextTableColumn.TextTableID
-- Purpose: Fast retrieval of column definitions for a table
-- Usage: Table column structure rendering
-- See also: Label.TextTableColumn

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextTableColumn_TextTableID_SequenceNumber' AND object_id = OBJECT_ID('TextTableColumn'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextTableColumn_TextTableID_SequenceNumber
    ON TextTableColumn(TextTableID, SequenceNumber)
    WHERE TextTableID IS NOT NULL;
    
    PRINT 'Created index: IX_TextTableColumn_TextTableID_SequenceNumber';
END
GO

--#endregion

--#region TextTableRow Table Indexes

/**************************************************************/
-- Index on TextTableRow.TextTableID
-- Purpose: Fast retrieval of rows for a table
-- Usage: Table row rendering
-- See also: Label.TextTableRow

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextTableRow_TextTableID_SequenceNumber' AND object_id = OBJECT_ID('TextTableRow'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextTableRow_TextTableID_SequenceNumber
    ON TextTableRow(TextTableID, SequenceNumber)
    INCLUDE (RowGroupType, StyleCode)
    WHERE TextTableID IS NOT NULL;
    
    PRINT 'Created index: IX_TextTableRow_TextTableID_SequenceNumber';
END
GO

--#endregion

--#region TextTableCell Table Indexes

/**************************************************************/
-- Index on TextTableCell.TextTableRowID
-- Purpose: Fast retrieval of cells for a row
-- Usage: Table cell rendering
-- See also: Label.TextTableCell

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TextTableCell_TextTableRowID_SequenceNumber' AND object_id = OBJECT_ID('TextTableCell'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TextTableCell_TextTableRowID_SequenceNumber
    ON TextTableCell(TextTableRowID, SequenceNumber)
    INCLUDE (CellType, RowSpan, ColSpan)
    WHERE TextTableRowID IS NOT NULL;
    
    PRINT 'Created index: IX_TextTableCell_TextTableRowID_SequenceNumber';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 7: ORGANIZATION INDEXES                                            */
/*  Indexes for organization hierarchy and identifier lookups.                 */
/*                                                                             */
/*******************************************************************************/

--#region Organization Table Indexes

/**************************************************************/
-- Index on Organization.OrganizationName
-- Purpose: Fast organization name lookup (non-full-text)
-- Usage: Exact match or prefix searches
-- See also: Label.Organization

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Organization_OrganizationName' AND object_id = OBJECT_ID('Organization'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Organization_OrganizationName
    ON Organization(OrganizationName)
    INCLUDE (IsConfidential);
    
    PRINT 'Created index: IX_Organization_OrganizationName';
END
GO

--#endregion

--#region OrganizationIdentifier Table Indexes

/**************************************************************/
-- Index on OrganizationIdentifier.OrganizationID
-- Purpose: Fast retrieval of identifiers for an organization
-- Usage: Organization identifier listing
-- See also: Label.OrganizationIdentifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizationIdentifier_OrganizationID' AND object_id = OBJECT_ID('OrganizationIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrganizationIdentifier_OrganizationID
    ON OrganizationIdentifier(OrganizationID)
    INCLUDE (IdentifierValue, IdentifierType)
    WHERE OrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_OrganizationIdentifier_OrganizationID';
END
GO

/**************************************************************/
-- Index on OrganizationIdentifier.IdentifierValue
-- Purpose: Fast lookup by identifier value (DUNS, FEI, Labeler Code)
-- Usage: Organization search by regulatory identifier
-- See also: Label.OrganizationIdentifier.IdentifierValue

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType' AND object_id = OBJECT_ID('OrganizationIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType
    ON OrganizationIdentifier(IdentifierValue, IdentifierType)
    INCLUDE (OrganizationID);
    
    PRINT 'Created index: IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('OrganizationIdentifier') 
    AND name = 'IX_OrganizationIdentifier_IdentifierValue_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_OrganizationIdentifier_IdentifierValue_Description',
        @value = N'Enables fast organization lookup by DUNS, FEI, Labeler Code, or other regulatory identifiers.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'OrganizationIdentifier',
        @level2type = N'INDEX', @level2name = N'IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 8: DOCUMENT RELATIONSHIP INDEXES                                   */
/*  Indexes for document authorship and organizational hierarchies.            */
/*                                                                             */
/*******************************************************************************/

--#region DocumentAuthor Table Indexes

/**************************************************************/
-- Index on DocumentAuthor.DocumentID
-- Purpose: Fast retrieval of authors for a document
-- Usage: Document author listing
-- See also: Label.DocumentAuthor

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentAuthor_DocumentID' AND object_id = OBJECT_ID('DocumentAuthor'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentAuthor_DocumentID
    ON DocumentAuthor(DocumentID)
    INCLUDE (OrganizationID, AuthorType)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentAuthor_DocumentID';
END
GO

/**************************************************************/
-- Index on DocumentAuthor.OrganizationID
-- Purpose: Fast retrieval of documents authored by an organization
-- Usage: Organization document history
-- See also: Label.DocumentAuthor.OrganizationID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentAuthor_OrganizationID' AND object_id = OBJECT_ID('DocumentAuthor'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentAuthor_OrganizationID
    ON DocumentAuthor(OrganizationID)
    INCLUDE (DocumentID, AuthorType)
    WHERE OrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentAuthor_OrganizationID';
END
GO

--#endregion

--#region RelatedDocument Table Indexes

/**************************************************************/
-- Index on RelatedDocument.SourceDocumentID
-- Purpose: Fast retrieval of related documents for a source
-- Usage: Document relationship navigation
-- See also: Label.RelatedDocument

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RelatedDocument_SourceDocumentID' AND object_id = OBJECT_ID('RelatedDocument'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RelatedDocument_SourceDocumentID
    ON RelatedDocument(SourceDocumentID)
    INCLUDE (RelationshipTypeCode, ReferencedSetGUID, ReferencedDocumentGUID)
    WHERE SourceDocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_RelatedDocument_SourceDocumentID';
END
GO

/**************************************************************/
-- Index on RelatedDocument.ReferencedSetGUID
-- Purpose: Fast lookup of documents referencing a specific set
-- Usage: Document version tracking
-- See also: Label.RelatedDocument.ReferencedSetGUID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RelatedDocument_ReferencedSetGUID' AND object_id = OBJECT_ID('RelatedDocument'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RelatedDocument_ReferencedSetGUID
    ON RelatedDocument(ReferencedSetGUID)
    INCLUDE (SourceDocumentID, RelationshipTypeCode)
    WHERE ReferencedSetGUID IS NOT NULL;
    
    PRINT 'Created index: IX_RelatedDocument_ReferencedSetGUID';
END
GO

/**************************************************************/
-- Index on RelatedDocument.ReferencedDocumentGUID
-- Purpose: Fast lookup of documents referencing a specific document
-- Usage: Document cross-reference resolution
-- See also: Label.RelatedDocument.ReferencedDocumentGUID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RelatedDocument_ReferencedDocumentGUID' AND object_id = OBJECT_ID('RelatedDocument'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RelatedDocument_ReferencedDocumentGUID
    ON RelatedDocument(ReferencedDocumentGUID)
    INCLUDE (SourceDocumentID, RelationshipTypeCode)
    WHERE ReferencedDocumentGUID IS NOT NULL;
    
    PRINT 'Created index: IX_RelatedDocument_ReferencedDocumentGUID';
END
GO

--#endregion

--#region DocumentRelationship Table Indexes

/**************************************************************/
-- Index on DocumentRelationship.DocumentID
-- Purpose: Fast retrieval of organizational relationships for a document
-- Usage: Document hierarchy building
-- See also: Label.DocumentRelationship

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentRelationship_DocumentID' AND object_id = OBJECT_ID('DocumentRelationship'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentRelationship_DocumentID
    ON DocumentRelationship(DocumentID)
    INCLUDE (ParentOrganizationID, ChildOrganizationID, RelationshipType, RelationshipLevel)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentRelationship_DocumentID';
END
GO

/**************************************************************/
-- Index on DocumentRelationship.ChildOrganizationID
-- Purpose: Fast lookup by child organization (e.g., find all establishments)
-- Usage: Organization relationship queries
-- See also: Label.DocumentRelationship.ChildOrganizationID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentRelationship_ChildOrganizationID' AND object_id = OBJECT_ID('DocumentRelationship'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentRelationship_ChildOrganizationID
    ON DocumentRelationship(ChildOrganizationID)
    INCLUDE (DocumentID, ParentOrganizationID, RelationshipType)
    WHERE ChildOrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentRelationship_ChildOrganizationID';
END
GO

/**************************************************************/
-- Index on DocumentRelationship.ParentOrganizationID
-- Purpose: Fast lookup by parent organization (e.g., find all registrants)
-- Usage: Organization hierarchy queries
-- See also: Label.DocumentRelationship.ParentOrganizationID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentRelationship_ParentOrganizationID' AND object_id = OBJECT_ID('DocumentRelationship'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentRelationship_ParentOrganizationID
    ON DocumentRelationship(ParentOrganizationID)
    INCLUDE (DocumentID, ChildOrganizationID, RelationshipType)
    WHERE ParentOrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentRelationship_ParentOrganizationID';
END
GO

--#endregion

--#region DocumentRelationshipIdentifier Table Indexes

/**************************************************************/
-- Index on DocumentRelationshipIdentifier.DocumentRelationshipID
-- Purpose: Fast retrieval of identifiers used in a relationship
-- Usage: Relationship identifier display
-- See also: Label.DocumentRelationshipIdentifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentRelationshipIdentifier_DocumentRelationshipID_OrganizationIdentifierID' AND object_id = OBJECT_ID('DocumentRelationshipIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentRelationshipIdentifier_DocumentRelationshipID_OrganizationIdentifierID
    ON DocumentRelationshipIdentifier(DocumentRelationshipID)
    INCLUDE (OrganizationIdentifierID)
    WHERE DocumentRelationshipID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentRelationshipIdentifier_DocumentRelationshipID';
END
GO

/**************************************************************/
-- Index on DocumentRelationshipIdentifier.OrganizationIdentifierID
-- Purpose: Fast lookup of relationships using a specific identifier
-- Usage: Identifier usage tracking
-- See also: Label.DocumentRelationshipIdentifier.OrganizationIdentifierID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentRelationshipIdentifier_OrganizationIdentifierID_DocumentRelationshipID' AND object_id = OBJECT_ID('DocumentRelationshipIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DocumentRelationshipIdentifier_OrganizationIdentifierID_DocumentRelationshipID
    ON DocumentRelationshipIdentifier(OrganizationIdentifierID)
    INCLUDE (DocumentRelationshipID)
    WHERE OrganizationIdentifierID IS NOT NULL;
    
    PRINT 'Created index: IX_DocumentRelationshipIdentifier_OrganizationIdentifierID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 9: PRODUCT INDEXES                                                 */
/*  Indexes for product, ingredient, and packaging lookups.                    */
/*                                                                             */
/*******************************************************************************/

--#region Product Table Indexes

/**************************************************************/
-- Index on Product.SectionID
-- Purpose: Fast retrieval of products defined in a section
-- Usage: Product listing for a document section
-- See also: Label.Product

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_SectionID' AND object_id = OBJECT_ID('Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_SectionID
    ON Product(SectionID)
    INCLUDE (ProductName, FormCode, FormDisplayName)
    WHERE SectionID IS NOT NULL;
    
    PRINT 'Created index: IX_Product_SectionID';
END
GO

/**************************************************************/
-- Index on Product.ProductName
-- Purpose: Fast product name lookup (non-full-text)
-- Usage: Exact match or prefix searches
-- See also: Label.Product.ProductName

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Product_ProductName' AND object_id = OBJECT_ID('Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_ProductName
    ON Product(ProductName)
    INCLUDE (FormCode, FormDisplayName);
    
    PRINT 'Created index: IX_Product_ProductName';
END
GO

--#endregion

--#region ProductIdentifier Table Indexes

/**************************************************************/
-- Index on ProductIdentifier.ProductID
-- Purpose: Fast retrieval of identifiers for a product
-- Usage: Product identifier listing (NDC, GTIN, etc.)
-- See also: Label.ProductIdentifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductIdentifier_ProductID' AND object_id = OBJECT_ID('ProductIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductIdentifier_ProductID
    ON ProductIdentifier(ProductID)
    INCLUDE (IdentifierValue, IdentifierType)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_ProductIdentifier_ProductID';
END
GO

/**************************************************************/
-- Index on ProductIdentifier.IdentifierValue
-- Purpose: Fast lookup by identifier value (NDC, GTIN)
-- Usage: Product search by NDC or other item code
-- See also: Label.ProductIdentifier.IdentifierValue

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductIdentifier_IdentifierValue_on_IdentifierType' AND object_id = OBJECT_ID('ProductIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductIdentifier_IdentifierValue_on_IdentifierType
    ON ProductIdentifier(IdentifierValue, IdentifierType)
    INCLUDE (ProductID, IdentifierSystemOID);
    
    PRINT 'Created index: IX_ProductIdentifier_IdentifierValue_on_IdentifierType';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('ProductIdentifier') 
    AND name = 'IX_ProductIdentifier_IdentifierValue_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_ProductIdentifier_IdentifierValue_Description',
        @value = N'Enables fast product lookup by NDC, GTIN, UPC, or other product codes. Critical for pharmaceutical inventory and dispensing systems.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'ProductIdentifier',
        @level2type = N'INDEX', @level2name = N'IX_ProductIdentifier_IdentifierValue_on_IdentifierType';
END
GO

--#endregion

--#region GenericMedicine Table Indexes

/**************************************************************/
-- Index on GenericMedicine.ProductID
-- Purpose: Fast retrieval of generic names for a product
-- Usage: Product generic name display
-- See also: Label.GenericMedicine

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GenericMedicine_ProductID' AND object_id = OBJECT_ID('GenericMedicine'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_GenericMedicine_ProductID
    ON GenericMedicine(ProductID)
    INCLUDE (GenericName, PhoneticName)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_GenericMedicine_ProductID';
END
GO

--#endregion

--#region IngredientSubstance Table Indexes

/**************************************************************/
-- Index on IngredientSubstance.UNII
-- Purpose: Fast lookup by Unique Ingredient Identifier
-- Usage: Substance search by UNII code
-- See also: Label.IngredientSubstance.UNII

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_IngredientSubstance_UNII' AND object_id = OBJECT_ID('IngredientSubstance'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_IngredientSubstance_UNII
    ON IngredientSubstance(UNII)
    INCLUDE (SubstanceName)
    WHERE UNII IS NOT NULL;
    
    PRINT 'Created index: IX_IngredientSubstance_UNII';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('IngredientSubstance') 
    AND name = 'IX_IngredientSubstance_UNII_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_IngredientSubstance_UNII_Description',
        @value = N'Enables fast ingredient lookup by FDA Unique Ingredient Identifier (UNII). Essential for drug interaction and composition analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'IngredientSubstance',
        @level2type = N'INDEX', @level2name = N'IX_IngredientSubstance_UNII';
END
GO

--#endregion

--#region Ingredient Table Indexes

/**************************************************************/
-- Index on Ingredient.ProductID
-- Purpose: Fast retrieval of ingredients for a product
-- Usage: Product composition listing
-- See also: Label.Ingredient

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingredient_ProductID' AND object_id = OBJECT_ID('Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Ingredient_ProductID
    ON Ingredient(ProductID)
    INCLUDE (IngredientSubstanceID, SequenceNumber)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_Ingredient_ProductID';
END
GO

/**************************************************************/
-- Index on Ingredient.IngredientSubstanceID
-- Purpose: Fast lookup of products containing a substance
-- Usage: Drug interaction queries, ingredient search
-- See also: Label.Ingredient.IngredientSubstanceID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Ingredient_IngredientSubstanceID' AND object_id = OBJECT_ID('Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Ingredient_IngredientSubstanceID
    ON Ingredient(IngredientSubstanceID)
    INCLUDE (ProductID)
    WHERE IngredientSubstanceID IS NOT NULL;
    
    PRINT 'Created index: IX_Ingredient_IngredientSubstanceID';
END
GO

--#endregion

--#region ActiveMoiety Table Indexes

/**************************************************************/
-- Index on ActiveMoiety.IngredientSubstanceID
-- Purpose: Fast retrieval of active moieties for a substance
-- Usage: Pharmacological component listing
-- See also: Label.ActiveMoiety

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ActiveMoiety_IngredientSubstanceID' AND object_id = OBJECT_ID('ActiveMoiety'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ActiveMoiety_IngredientSubstanceID
    ON ActiveMoiety(IngredientSubstanceID)
    INCLUDE (MoietyUNII, MoietyName)
    WHERE IngredientSubstanceID IS NOT NULL;
    
    PRINT 'Created index: IX_ActiveMoiety_IngredientSubstanceID';
END
GO

/**************************************************************/
-- Index on ActiveMoiety.MoietyUNII
-- Purpose: Fast lookup by moiety UNII code
-- Usage: Active moiety search
-- See also: Label.ActiveMoiety.MoietyUNII

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ActiveMoiety_MoietyUNII' AND object_id = OBJECT_ID('ActiveMoiety'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ActiveMoiety_MoietyUNII
    ON ActiveMoiety(MoietyUNII)
    INCLUDE (IngredientSubstanceID, MoietyName)
    WHERE MoietyUNII IS NOT NULL;
    
    PRINT 'Created index: IX_ActiveMoiety_MoietyUNII';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 10: PACKAGING INDEXES                                              */
/*  Indexes for packaging hierarchy and identifier lookups.                    */
/*                                                                             */
/*******************************************************************************/

--#region PackagingLevel Table Indexes

/**************************************************************/
-- Index on PackagingLevel.ProductID
-- Purpose: Fast retrieval of packaging for a product
-- Usage: Product packaging hierarchy display
-- See also: Label.PackagingLevel

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PackagingLevel_ProductID' AND object_id = OBJECT_ID('PackagingLevel'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PackagingLevel_ProductID
    ON PackagingLevel(ProductID)
    INCLUDE (PackageCode, PackageFormCode, PackageFormDisplayName)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_PackagingLevel_ProductID';
END
GO

/**************************************************************/
-- Index on PackagingLevel.PackageCode
-- Purpose: Fast lookup by package code
-- Usage: Package search by NDC package code
-- See also: Label.PackagingLevel.PackageCode

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PackagingLevel_PackageCode' AND object_id = OBJECT_ID('PackagingLevel'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PackagingLevel_PackageCode
    ON PackagingLevel(PackageCode)
    INCLUDE (ProductID, PackageFormDisplayName)
    WHERE PackageCode IS NOT NULL;
    
    PRINT 'Created index: IX_PackagingLevel_PackageCode';
END
GO

/**************************************************************/
-- Index on PackagingLevel.ProductInstanceID
-- Purpose: Fast retrieval of packaging for lot distribution
-- Usage: Lot tracking queries
-- See also: Label.PackagingLevel.ProductInstanceID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PackagingLevel_ProductInstanceID' AND object_id = OBJECT_ID('PackagingLevel'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PackagingLevel_ProductInstanceID
    ON PackagingLevel(ProductInstanceID)
    INCLUDE (ProductID, PackageCode)
    WHERE ProductInstanceID IS NOT NULL;
    
    PRINT 'Created index: IX_PackagingLevel_ProductInstanceID';
END
GO

--#endregion

--#region PackageIdentifier Table Indexes

/**************************************************************/
-- Index on PackageIdentifier.PackagingLevelID
-- Purpose: Fast retrieval of identifiers for a packaging level
-- Usage: Package identifier listing
-- See also: Label.PackageIdentifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PackageIdentifier_PackagingLevelID' AND object_id = OBJECT_ID('PackageIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PackageIdentifier_PackagingLevelID
    ON PackageIdentifier(PackagingLevelID)
    WHERE PackagingLevelID IS NOT NULL;
    
    PRINT 'Created index: IX_PackageIdentifier_PackagingLevelID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 11: CONTACT INFORMATION INDEXES                                    */
/*  Indexes for address, telecom, and contact party lookups.                   */
/*                                                                             */
/*******************************************************************************/

--#region ContactParty Table Indexes

/**************************************************************/
-- Index on ContactParty.OrganizationID
-- Purpose: Fast retrieval of contact parties for an organization
-- Usage: Organization contact listing
-- See also: Label.ContactParty

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContactParty_OrganizationID' AND object_id = OBJECT_ID('ContactParty'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContactParty_OrganizationID
    ON ContactParty(OrganizationID)
    INCLUDE (AddressID, ContactPersonID)
    WHERE OrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_ContactParty_OrganizationID';
END
GO

/**************************************************************/
-- Index on ContactParty.AddressID
-- Purpose: Fast lookup of contact parties at an address
-- Usage: Address-based contact queries
-- See also: Label.ContactParty.AddressID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContactParty_AddressID' AND object_id = OBJECT_ID('ContactParty'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContactParty_AddressID
    ON ContactParty(AddressID)
    INCLUDE (OrganizationID, ContactPersonID)
    WHERE AddressID IS NOT NULL;
    
    PRINT 'Created index: IX_ContactParty_AddressID';
END
GO

--#endregion

--#region ContactPartyTelecom Table Indexes

/**************************************************************/
-- Index on ContactPartyTelecom.ContactPartyID
-- Purpose: Fast retrieval of telecom entries for a contact party
-- Usage: Contact telecom listing
-- See also: Label.ContactPartyTelecom

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContactPartyTelecom_ContactPartyID' AND object_id = OBJECT_ID('ContactPartyTelecom'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ContactPartyTelecom_ContactPartyID
    ON ContactPartyTelecom(ContactPartyID)
    INCLUDE (TelecomID)
    WHERE ContactPartyID IS NOT NULL;
    
    PRINT 'Created index: IX_ContactPartyTelecom_ContactPartyID';
END
GO

--#endregion

--#region OrganizationTelecom Table Indexes

/**************************************************************/
-- Index on OrganizationTelecom.OrganizationID
-- Purpose: Fast retrieval of telecom entries for an organization
-- Usage: Organization telecom listing
-- See also: Label.OrganizationTelecom

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizationTelecom_OrganizationID' AND object_id = OBJECT_ID('OrganizationTelecom'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrganizationTelecom_OrganizationID
    ON OrganizationTelecom(OrganizationID)
    INCLUDE (TelecomID)
    WHERE OrganizationID IS NOT NULL;
    
    PRINT 'Created index: IX_OrganizationTelecom_OrganizationID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 12: MEDIA AND EXCERPT INDEXES                                      */
/*  Indexes for observation media and section excerpt lookups.                 */
/*                                                                             */
/*******************************************************************************/

--#region ObservationMedia Table Indexes

/**************************************************************/
-- Index on ObservationMedia.SectionID
-- Purpose: Fast retrieval of media for a section
-- Usage: Section media rendering
-- See also: Label.ObservationMedia

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ObservationMedia_SectionID' AND object_id = OBJECT_ID('ObservationMedia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ObservationMedia_SectionID
    ON ObservationMedia(SectionID)
    WHERE SectionID IS NOT NULL;
    
    PRINT 'Created index: IX_ObservationMedia_SectionID';
END
GO

--#endregion

--#region RenderedMedia Table Indexes

/**************************************************************/
-- Index on RenderedMedia.SectionTextContentID
-- Purpose: Fast retrieval of rendered media for content blocks
-- Usage: Content media rendering
-- See also: Label.RenderedMedia

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RenderedMedia_SectionTextContentID' AND object_id = OBJECT_ID('RenderedMedia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RenderedMedia_SectionTextContentID
    ON RenderedMedia(SectionTextContentID)
    INCLUDE (ObservationMediaID, IsInline, SequenceInContent)
    WHERE SectionTextContentID IS NOT NULL;
    
    PRINT 'Created index: IX_RenderedMedia_SectionTextContentID';
END
GO

/**************************************************************/
-- Index on RenderedMedia.ObservationMediaID
-- Purpose: Fast lookup of content using specific media
-- Usage: Media usage tracking
-- See also: Label.RenderedMedia.ObservationMediaID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RenderedMedia_ObservationMediaID' AND object_id = OBJECT_ID('RenderedMedia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RenderedMedia_ObservationMediaID
    ON RenderedMedia(ObservationMediaID)
    INCLUDE (SectionTextContentID, DocumentID)
    WHERE ObservationMediaID IS NOT NULL;
    
    PRINT 'Created index: IX_RenderedMedia_ObservationMediaID';
END
GO

/**************************************************************/
-- Index on RenderedMedia.DocumentID
-- Purpose: Fast retrieval of all media references for a document
-- Usage: Document media inventory
-- See also: Label.RenderedMedia.DocumentID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RenderedMedia_DocumentID' AND object_id = OBJECT_ID('RenderedMedia'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RenderedMedia_DocumentID
    ON RenderedMedia(DocumentID)
    INCLUDE (ObservationMediaID, SectionTextContentID)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_RenderedMedia_DocumentID';
END
GO

--#endregion

--#region SectionExcerptHighlight Table Indexes

/**************************************************************/
-- Index on SectionExcerptHighlight.SectionID
-- Purpose: Fast retrieval of highlights for a section
-- Usage: Boxed warning, indication highlight display
-- See also: Label.SectionExcerptHighlight

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SectionExcerptHighlight_SectionID' AND object_id = OBJECT_ID('SectionExcerptHighlight'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SectionExcerptHighlight_SectionID
    ON SectionExcerptHighlight(SectionID)
    WHERE SectionID IS NOT NULL;
    
    PRINT 'Created index: IX_SectionExcerptHighlight_SectionID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 13: BUSINESS OPERATION INDEXES                                     */
/*  Indexes for establishment operations and compliance tracking.              */
/*                                                                             */
/*******************************************************************************/

--#region BusinessOperation Table Indexes

/**************************************************************/
-- Index on BusinessOperation.DocumentRelationshipID
-- Purpose: Fast retrieval of business operations for a relationship
-- Usage: Establishment operations listing
-- See also: Label.BusinessOperation

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BusinessOperation_DocumentRelationshipID' AND object_id = OBJECT_ID('BusinessOperation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BusinessOperation_DocumentRelationshipID
    ON BusinessOperation(DocumentRelationshipID)
    WHERE DocumentRelationshipID IS NOT NULL;
    
    PRINT 'Created index: IX_BusinessOperation_DocumentRelationshipID';
END
GO

--#endregion

--#region BusinessOperationQualifier Table Indexes

/**************************************************************/
-- Index on BusinessOperationQualifier.BusinessOperationID
-- Purpose: Fast retrieval of qualifiers for a business operation
-- Usage: Operation qualifier listing
-- See also: Label.BusinessOperationQualifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BusinessOperationQualifier_BusinessOperationID' AND object_id = OBJECT_ID('BusinessOperationQualifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BusinessOperationQualifier_BusinessOperationID
    ON BusinessOperationQualifier(BusinessOperationID)
    WHERE BusinessOperationID IS NOT NULL;
    
    PRINT 'Created index: IX_BusinessOperationQualifier_BusinessOperationID';
END
GO

--#endregion

--#region ComplianceAction Table Indexes

/**************************************************************/
-- Index on ComplianceAction.DocumentRelationshipID
-- Purpose: Fast retrieval of compliance actions for a relationship
-- Usage: FDA compliance action tracking
-- See also: Label.ComplianceAction

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ComplianceAction_DocumentRelationshipID' AND object_id = OBJECT_ID('ComplianceAction'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ComplianceAction_DocumentRelationshipID
    ON ComplianceAction(DocumentRelationshipID)
    WHERE DocumentRelationshipID IS NOT NULL;
    
    PRINT 'Created index: IX_ComplianceAction_DocumentRelationshipID';
END
GO

--#endregion

--#region CertificationProductLink Table Indexes

/**************************************************************/
-- Index on CertificationProductLink.DocumentRelationshipID
-- Purpose: Fast retrieval of certification links for a relationship
-- Usage: Blanket certification product linking
-- See also: Label.CertificationProductLink

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CertificationProductLink_DocumentRelationshipID' AND object_id = OBJECT_ID('CertificationProductLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CertificationProductLink_DocumentRelationshipID
    ON CertificationProductLink(DocumentRelationshipID)
    WHERE DocumentRelationshipID IS NOT NULL;
    
    PRINT 'Created index: IX_CertificationProductLink_DocumentRelationshipID';
END
GO

--#endregion

--#region FacilityProductLink Table Indexes

/**************************************************************/
-- Index on FacilityProductLink.DocumentRelationshipID
-- Purpose: Fast retrieval of facility-product links for a relationship
-- Usage: Facility registration product listing
-- See also: Label.FacilityProductLink

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FacilityProductLink_DocumentRelationshipID' AND object_id = OBJECT_ID('FacilityProductLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_FacilityProductLink_DocumentRelationshipID
    ON FacilityProductLink(DocumentRelationshipID)
    INCLUDE (ProductID, ProductIdentifierID, ProductName, IsResolved)
    WHERE DocumentRelationshipID IS NOT NULL;
    
    PRINT 'Created index: IX_FacilityProductLink_DocumentRelationshipID';
END
GO

/**************************************************************/
-- Index on FacilityProductLink.ProductID
-- Purpose: Fast lookup of facility links by product
-- Usage: Product facility assignment queries
-- See also: Label.FacilityProductLink.ProductID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_FacilityProductLink_ProductID' AND object_id = OBJECT_ID('FacilityProductLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_FacilityProductLink_ProductID
    ON FacilityProductLink(ProductID)
    INCLUDE (DocumentRelationshipID)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_FacilityProductLink_ProductID';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 14: SPECIALIZED INDEXES                                            */
/*  Additional indexes for specialized queries and edge cases.                 */
/*                                                                             */
/*******************************************************************************/

--#region LegalAuthenticator Table Indexes

/**************************************************************/
-- Index on LegalAuthenticator.DocumentID
-- Purpose: Fast retrieval of authenticators for a document
-- Usage: Document signature validation
-- See also: Label.LegalAuthenticator

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LegalAuthenticator_DocumentID' AND object_id = OBJECT_ID('LegalAuthenticator'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_LegalAuthenticator_DocumentID
    ON LegalAuthenticator(DocumentID)
    WHERE DocumentID IS NOT NULL;
    
    PRINT 'Created index: IX_LegalAuthenticator_DocumentID';
END
GO

--#endregion

--#region SpecializedKind Table Indexes

/**************************************************************/
-- Index on SpecializedKind.ProductID
-- Purpose: Fast retrieval of specialized kinds for a product
-- Usage: Device classification, cosmetic category lookup
-- See also: Label.SpecializedKind

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SpecializedKind_ProductID' AND object_id = OBJECT_ID('SpecializedKind'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_SpecializedKind_ProductID
    ON SpecializedKind(ProductID)
    INCLUDE (KindCode, KindDisplayName)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_SpecializedKind_ProductID';
END
GO

--#endregion

--#region AdditionalIdentifier Table Indexes

/**************************************************************/
-- Index on AdditionalIdentifier.ProductID
-- Purpose: Fast retrieval of additional identifiers for a product
-- Usage: Model/Catalog/Reference number lookup
-- See also: Label.AdditionalIdentifier

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdditionalIdentifier_ProductID' AND object_id = OBJECT_ID('AdditionalIdentifier'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AdditionalIdentifier_ProductID
    ON AdditionalIdentifier(ProductID)
    INCLUDE (IdentifierValue, IdentifierTypeCode, IdentifierTypeDisplayName)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_AdditionalIdentifier_ProductID';
END
GO

--#endregion

--#region EquivalentEntity Table Indexes

/**************************************************************/
-- Index on EquivalentEntity.ProductID
-- Purpose: Fast retrieval of equivalent entities for a product
-- Usage: Product source/predecessor lookup
-- See also: Label.EquivalentEntity

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EquivalentEntity_ProductID' AND object_id = OBJECT_ID('EquivalentEntity'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EquivalentEntity_ProductID
    ON EquivalentEntity(ProductID)
    INCLUDE (EquivalenceCode, DefiningMaterialKindCode)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_EquivalentEntity_ProductID';
END
GO

--#endregion

--#region NCTLink Table Indexes

/**************************************************************/
-- Index on NCTLink.SectionID
-- Purpose: Fast retrieval of NCT links for a section
-- Usage: Clinical trial reference lookup
-- See also: Label.NCTLink

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NCTLink_SectionID' AND object_id = OBJECT_ID('NCTLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_NCTLink_SectionID
    ON NCTLink(SectionID)
    INCLUDE (NCTNumber)
    WHERE SectionID IS NOT NULL;
    
    PRINT 'Created index: IX_NCTLink_SectionID';
END
GO

/**************************************************************/
-- Index on NCTLink.NCTNumber
-- Purpose: Fast lookup by National Clinical Trials number
-- Usage: Clinical trial document search
-- See also: Label.NCTLink.NCTNumber

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_NCTLink_NCTNumber' AND object_id = OBJECT_ID('NCTLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_NCTLink_NCTNumber
    ON NCTLink(NCTNumber)
    INCLUDE (SectionID)
    WHERE NCTNumber IS NOT NULL;
    
    PRINT 'Created index: IX_NCTLink_NCTNumber';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('NCTLink') 
    AND name = 'IX_NCTLink_NCTNumber_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'IX_NCTLink_NCTNumber_Description',
        @value = N'Enables fast document lookup by ClinicalTrials.gov NCT number. Used for regulatory research and compliance verification.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'NCTLink',
        @level2type = N'INDEX', @level2name = N'IX_NCTLink_NCTNumber';
END
GO

--#endregion

--#region ResponsiblePersonLink Table Indexes

/**************************************************************/
-- Index on ResponsiblePersonLink.ProductID
-- Purpose: Fast retrieval of responsible person links for a product
-- Usage: Cosmetic responsible person lookup
-- See also: Label.ResponsiblePersonLink

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ResponsiblePersonLink_ProductID' AND object_id = OBJECT_ID('ResponsiblePersonLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ResponsiblePersonLink_ProductID
    ON ResponsiblePersonLink(ProductID)
    INCLUDE (ResponsiblePersonOrgID)
    WHERE ProductID IS NOT NULL;
    
    PRINT 'Created index: IX_ResponsiblePersonLink_ProductID';
END
GO

/**************************************************************/
-- Index on ResponsiblePersonLink.ResponsiblePersonOrgID
-- Purpose: Fast lookup of products by responsible person org
-- Usage: Organization product portfolio
-- See also: Label.ResponsiblePersonLink.ResponsiblePersonOrgID

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ResponsiblePersonLink_ResponsiblePersonOrgID' AND object_id = OBJECT_ID('ResponsiblePersonLink'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ResponsiblePersonLink_ResponsiblePersonOrgID
    ON ResponsiblePersonLink(ResponsiblePersonOrgID)
    INCLUDE (ProductID)
    WHERE ResponsiblePersonOrgID IS NOT NULL;
    
    PRINT 'Created index: IX_ResponsiblePersonLink_ResponsiblePersonOrgID';
END
GO

--#endregion


/*******************************************************************************/
/*                                                                             */
/*  SECTION 14: INDEXES FOR INGREDIENT VIEWS                                   */
/*  Non-clustered indexes to support search on ingredient views                */
/*  Date Added: 2025-12-30                                                     */
/*                                                                             */
/*******************************************************************************/

--#region Ingredient View Supporting Indexes

/**************************************************************/
-- Indexes to support vw_Ingredients, vw_ActiveIngredients, vw_InactiveIngredients
-- These indexes optimize searches on:
--   - SubstanceName (ingredient name)
--   - ProductName
--   - UNII
--   - DocumentGUID, SetGUID, SectionGUID
--   - ApplicationOrMonographIDValue (for ApplicationNumber derivation)
/**************************************************************/

-- Index on IngredientSubstance.SubstanceName for ingredient name searches
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IngredientSubstance_SubstanceName' AND object_id = OBJECT_ID('dbo.IngredientSubstance'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_IngredientSubstance_SubstanceName
    ON dbo.IngredientSubstance (SubstanceName)
    INCLUDE (IngredientSubstanceID, UNII);
    PRINT 'Created index: IX_IngredientSubstance_SubstanceName';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_IngredientSubstance_SubstanceName';
END
GO

-- Index on IngredientSubstance.UNII for UNII lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IngredientSubstance_UNII' AND object_id = OBJECT_ID('dbo.IngredientSubstance'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_IngredientSubstance_UNII
    ON dbo.IngredientSubstance (UNII)
    INCLUDE (IngredientSubstanceID, SubstanceName);
    PRINT 'Created index: IX_IngredientSubstance_UNII';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_IngredientSubstance_UNII';
END
GO

-- Index on Product.ProductName for product name searches
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_ProductName' AND object_id = OBJECT_ID('dbo.Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_ProductName
    ON dbo.Product (ProductName)
    INCLUDE (ProductID, SectionID);
    PRINT 'Created index: IX_Product_ProductName';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Product_ProductName';
END
GO

-- Index on Ingredient.ClassCode for active/inactive filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ingredient_ClassCode' AND object_id = OBJECT_ID('dbo.Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Ingredient_ClassCode
    ON dbo.Ingredient (ClassCode)
    INCLUDE (IngredientID, ProductID, IngredientSubstanceID);
    PRINT 'Created index: IX_Ingredient_ClassCode';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Ingredient_ClassCode';
END
GO

-- Index on MarketingCategory.ApplicationOrMonographIDValue for application number searches
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MarketingCategory_ApplicationOrMonographIDValue' AND object_id = OBJECT_ID('dbo.MarketingCategory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MarketingCategory_ApplicationOrMonographIDValue
    ON dbo.MarketingCategory (ApplicationOrMonographIDValue)
    INCLUDE (MarketingCategoryID, ProductID, CategoryDisplayName);
    PRINT 'Created index: IX_MarketingCategory_ApplicationOrMonographIDValue';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_MarketingCategory_ApplicationOrMonographIDValue';
END
GO

-- Index on Document.DocumentGUID for GUID lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Document_DocumentGUID_Ingredient' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_DocumentGUID_Ingredient
    ON dbo.[Document] (DocumentGUID)
    INCLUDE (DocumentID, SetGUID);
    PRINT 'Created index: IX_Document_DocumentGUID_Ingredient';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Document_DocumentGUID_Ingredient';
END
GO

-- Index on Document.SetGUID for set GUID lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Document_SetGUID_Ingredient' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_SetGUID_Ingredient
    ON dbo.[Document] (SetGUID)
    INCLUDE (DocumentID, DocumentGUID);
    PRINT 'Created index: IX_Document_SetGUID_Ingredient';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Document_SetGUID_Ingredient';
END
GO

-- Index on Section.SectionGUID for section GUID lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Section_SectionGUID_Ingredient' AND object_id = OBJECT_ID('dbo.Section'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Section_SectionGUID_Ingredient
    ON dbo.Section (SectionGUID)
    INCLUDE (SectionID, DocumentID);
    PRINT 'Created index: IX_Section_SectionGUID_Ingredient';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Section_SectionGUID_Ingredient';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 15: LATEST LABEL VIEW INDEXES                                      */
/*  Non-clustered indexes to support vw_ProductLatestLabel view                */
/*  Date Added: 2026-01-07                                                     */
/*                                                                             */
/*******************************************************************************/

--#region Latest Label View Supporting Indexes

/**************************************************************/
-- Indexes to support vw_ProductLatestLabel
-- These indexes optimize:
--   - ROW_NUMBER() OVER (PARTITION BY UNII, ProductName ORDER BY EffectiveTime DESC)
--   - Joins between IngredientSubstance, Product, and Document tables
/**************************************************************/

/**************************************************************/
-- Index on Document.EffectiveTime with DocumentID and DocumentGUID
-- Purpose: Supports the ORDER BY EffectiveTime DESC in ROW_NUMBER()
-- Usage: Latest label lookup by effective date
-- See also: vw_ProductLatestLabel

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Document_EffectiveTime_LatestLabel' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_EffectiveTime_LatestLabel
    ON dbo.[Document] (EffectiveTime DESC)
    INCLUDE (DocumentID, DocumentGUID);
    PRINT 'Created index: IX_Document_EffectiveTime_LatestLabel';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Document_EffectiveTime_LatestLabel';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('Document')
    AND name = 'IX_Document_EffectiveTime_LatestLabel_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'IX_Document_EffectiveTime_LatestLabel_Description',
        @value = N'Supports ROW_NUMBER() ordering by EffectiveTime DESC for latest label queries. Critical for vw_ProductLatestLabel view performance.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Document',
        @level2type = N'INDEX', @level2name = N'IX_Document_EffectiveTime_LatestLabel';
END
GO

/**************************************************************/
-- Composite Index on Document for DocumentID lookup with EffectiveTime
-- Purpose: Covers the join on DocumentID and the ORDER BY on EffectiveTime
-- Usage: Efficient document retrieval with date ordering
-- See also: vw_ProductLatestLabel, vw_ProductsByIngredient

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Document_DocumentID_EffectiveTime' AND object_id = OBJECT_ID('dbo.Document'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Document_DocumentID_EffectiveTime
    ON dbo.[Document] (DocumentID, EffectiveTime DESC)
    INCLUDE (DocumentGUID);
    PRINT 'Created index: IX_Document_DocumentID_EffectiveTime';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Document_DocumentID_EffectiveTime';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('Document')
    AND name = 'IX_Document_DocumentID_EffectiveTime_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'IX_Document_DocumentID_EffectiveTime_Description',
        @value = N'Covers DocumentID join with EffectiveTime ordering. Optimizes latest label queries joining on DocumentID.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Document',
        @level2type = N'INDEX', @level2name = N'IX_Document_DocumentID_EffectiveTime';
END
GO

/**************************************************************/
-- Index on ActiveMoiety for MoietyUNII filtering
-- Purpose: Supports WHERE MoietyUNII IS NOT NULL filter
-- Usage: Active moiety filtering in latest label queries
-- See also: vw_ProductLatestLabel

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveMoiety_MoietyUNII_LatestLabel' AND object_id = OBJECT_ID('dbo.ActiveMoiety'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ActiveMoiety_MoietyUNII_LatestLabel
    ON dbo.ActiveMoiety (MoietyUNII, IngredientSubstanceID)
    WHERE MoietyUNII IS NOT NULL;
    PRINT 'Created index: IX_ActiveMoiety_MoietyUNII_LatestLabel';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_ActiveMoiety_MoietyUNII_LatestLabel';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('ActiveMoiety')
    AND name = 'IX_ActiveMoiety_MoietyUNII_LatestLabel_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'IX_ActiveMoiety_MoietyUNII_LatestLabel_Description',
        @value = N'Filtered index on MoietyUNII for active moiety lookups. Supports WHERE MoietyUNII IS NOT NULL filter in latest label queries.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'ActiveMoiety',
        @level2type = N'INDEX', @level2name = N'IX_ActiveMoiety_MoietyUNII_LatestLabel';
END
GO

/**************************************************************/
-- Composite Index on Ingredient for ClassCode filtering with joins
-- Purpose: Supports WHERE ClassCode <> 'IACT' filter and joins
-- Usage: Active ingredient filtering in latest label queries
-- See also: vw_ProductLatestLabel

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Ingredient_ClassCode_LatestLabel' AND object_id = OBJECT_ID('dbo.Ingredient'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Ingredient_ClassCode_LatestLabel
    ON dbo.Ingredient (ClassCode, IngredientSubstanceID, ProductID)
    WHERE ClassCode <> 'IACT';
    PRINT 'Created index: IX_Ingredient_ClassCode_LatestLabel';
END
ELSE
BEGIN
    PRINT 'Index already exists: IX_Ingredient_ClassCode_LatestLabel';
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('Ingredient')
    AND name = 'IX_Ingredient_ClassCode_LatestLabel_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'IX_Ingredient_ClassCode_LatestLabel_Description',
        @value = N'Filtered index excluding IACT class ingredients. Optimizes active ingredient queries in latest label view.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'Ingredient',
        @level2type = N'INDEX', @level2name = N'IX_Ingredient_ClassCode_LatestLabel';
END
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION: INDEX STATISTICS AND SUMMARY                                      */
/*  Reports on created indexes for verification.                               */
/*                                                                             */
/*******************************************************************************/

-- Summary of all indexes created
PRINT '';
PRINT '=================================================================';
PRINT 'MedRecPro SPL Label Index Creation Complete';
PRINT '=================================================================';
PRINT '';

-- Count total indexes created
SELECT 
    'Total Indexes Created' AS Summary,
    COUNT(*) AS IndexCount
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name LIKE 'IX_%'
AND t.is_ms_shipped = 0;

-- Count full-text indexes
SELECT 
    'Full-Text Indexes Created' AS Summary,
    COUNT(*) AS IndexCount
FROM sys.fulltext_indexes fi
INNER JOIN sys.tables t ON fi.object_id = t.object_id
WHERE t.is_ms_shipped = 0;

GO

PRINT '';
PRINT 'Index creation script completed successfully.';
PRINT 'Run DBCC SHOW_STATISTICS to verify index usage over time.';
PRINT '';
GO
