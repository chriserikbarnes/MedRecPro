/*******************************************************************************/
/*                                                                             */
/*  MedRecPro SPL Label Navigation Views                                       */
/*  SQL Server 2012 Compatible                                                 */
/*                                                                             */
/*  Purpose: Creates lightweight views for navigation, cross-referencing,      */
/*           and discovery of pharmaceutical labeling data. Optimized for      */
/*           API consumption and AI-assisted query workflows.                  */
/*                                                                             */
/*  Information Flow:                                                          */
/*    User Input -> Claude API -> API Endpoints -> Views -> Results            */
/*    -> Claude Interpretation -> Report                                       */
/*                                                                             */
/*  Author: Generated for MedRecPro                                            */
/*  Date: 2025-12-08                                                           */
/*                                                                             */
/*  Notes:                                                                     */
/*    - All scripts are idempotent and safe to run multiple times             */
/*    - Views return lightweight objects with IDs/GUIDs for navigation        */
/*    - Designed to leverage indexes created in MedRecPro_Indexes.sql         */
/*    - Extended properties added for documentation                            */
/*                                                                             */
/*******************************************************************************/

SET NOCOUNT ON;
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*******************************************************************************/
/*                                                                             */
/*  SECTION 1: APPLICATION NUMBER NAVIGATION VIEWS                             */
/*  Views for locating products by regulatory application number               */
/*  (NDA, ANDA, BLA, etc.)                                                     */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductsByApplicationNumber

/**************************************************************/
-- View: vw_ProductsByApplicationNumber
-- Purpose: Locates all products sharing the same application number
--          (e.g., NDA014526, ANDA125654, BLA103948)
-- Usage: Find all label versions and products under a single approval
-- Returns: Lightweight navigation object with Product and Document IDs
-- Indexes Used: IX_ProductIdentifier_ProductID, IX_Document_DocumentGUID
-- See also: Label.MarketingCategory, Label.Product, Label.Document

IF OBJECT_ID('dbo.vw_ProductsByApplicationNumber', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductsByApplicationNumber;
GO

CREATE VIEW dbo.vw_ProductsByApplicationNumber
AS
/**************************************************************/
-- Returns products grouped by application number for cross-referencing
-- Enables discovery of all labels sharing the same NDA/ANDA/BLA
/**************************************************************/
SELECT 
    -- Application identification
    mc.ApplicationOrMonographIDValue AS ApplicationNumber,
    mc.CategoryCode AS MarketingCategoryCode,
    mc.CategoryDisplayName AS MarketingCategoryName,
    mc.ApprovalDate,
    
    -- Product identification (lightweight for navigation)
    p.ProductID,
    p.ProductName,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    
    -- Document identification for full label retrieval
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.EffectiveTime AS LabelEffectiveDate,
    d.Title AS DocumentTitle,
    d.DocumentCode,
    d.DocumentDisplayName AS DocumentType,
    
    -- Section path for navigation
    s.SectionID,
    s.SectionCode,
    
    -- Labeler information
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName

FROM dbo.MarketingCategory mc
    INNER JOIN dbo.Product p ON mc.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID

WHERE mc.ApplicationOrMonographIDValue IS NOT NULL
GO

-- Add extended property for documentation
IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductsByApplicationNumber') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates all products sharing the same regulatory application number (NDA, ANDA, BLA). Enables cross-referencing of labels under a single approval. Returns lightweight navigation objects with IDs/GUIDs.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductsByApplicationNumber';
END
GO

PRINT 'Created view: vw_ProductsByApplicationNumber';
GO

--#endregion

--#region vw_ApplicationNumberSummary

/**************************************************************/
-- View: vw_ApplicationNumberSummary
-- Purpose: Aggregated summary of application numbers with counts
-- Usage: Quick lookup to see how many products/labels exist per application
-- Returns: Application number with product and label counts

IF OBJECT_ID('dbo.vw_ApplicationNumberSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ApplicationNumberSummary;
GO

CREATE VIEW dbo.vw_ApplicationNumberSummary
AS
/**************************************************************/
-- Provides counts of products and documents per application number
-- Useful for understanding the scope of an application
/**************************************************************/
SELECT 
    mc.ApplicationOrMonographIDValue AS ApplicationNumber,
    mc.CategoryCode AS MarketingCategoryCode,
    mc.CategoryDisplayName AS MarketingCategoryName,
    MIN(mc.ApprovalDate) AS EarliestApprovalDate,
    MAX(mc.ApprovalDate) AS LatestApprovalDate,
    COUNT(DISTINCT mc.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT d.SetGUID) AS LabelSetCount,
    MAX(d.EffectiveTime) AS MostRecentLabelDate

FROM dbo.MarketingCategory mc
    INNER JOIN dbo.Product p ON mc.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID

WHERE mc.ApplicationOrMonographIDValue IS NOT NULL

GROUP BY 
    mc.ApplicationOrMonographIDValue,
    mc.CategoryCode,
    mc.CategoryDisplayName
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ApplicationNumberSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Aggregated summary showing product and document counts per application number. Use to understand the scope of NDA/ANDA/BLA approvals.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ApplicationNumberSummary';
END
GO

PRINT 'Created view: vw_ApplicationNumberSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 2: PHARMACOLOGIC CLASS NAVIGATION VIEWS                            */
/*  Views for locating products by pharmacologic class                         */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductsByPharmacologicClass

/**************************************************************/
-- View: vw_ProductsByPharmacologicClass
-- Purpose: Locates all products within a specific pharmacologic class
-- Usage: Find all drugs in a therapeutic category (e.g., "Beta-Adrenergic Blockers")
-- Returns: Lightweight navigation object with Product, Class, and Document IDs
-- See also: Label.PharmacologicClass, Label.PharmacologicClassLink

IF OBJECT_ID('dbo.vw_ProductsByPharmacologicClass', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductsByPharmacologicClass;
GO

CREATE VIEW dbo.vw_ProductsByPharmacologicClass
AS
/**************************************************************/
-- Links products to their pharmacologic classes via active moieties
-- Enables therapeutic category-based drug discovery
/**************************************************************/
SELECT  
    -- Pharmacologic class identification
    pc.PharmacologicClassID,
    pc.ClassCode AS PharmClassCode,
    pc.ClassDisplayName AS PharmClassName,
    
    -- Active moiety/substance linkage
    am.ActiveMoietyID,
    am.MoietyUNII,
    am.MoietyName,
    
    -- Ingredient substance
    ins.IngredientSubstanceID,
    ins.UNII AS SubstanceUNII,
    ins.SubstanceName,
    
    -- Product identification
    p.ProductID,
    p.ProductName,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    
    -- Document identification for full label retrieval
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS LabelEffectiveDate

FROM dbo.PharmacologicClass pc
    -- Link pharm class to active moiety substance
    INNER JOIN dbo.PharmacologicClassLink pcl ON pc.PharmacologicClassID = pcl.PharmacologicClassID
    INNER JOIN dbo.IngredientSubstance ams ON pcl.ActiveMoietySubstanceID = ams.IngredientSubstanceID
    -- Link via active moiety
    INNER JOIN dbo.ActiveMoiety am ON ams.IngredientSubstanceID = am.IngredientSubstanceID
    -- Get the parent ingredient substance
    INNER JOIN dbo.IngredientSubstance ins ON am.IngredientSubstanceID = ins.IngredientSubstanceID
    -- Link to products via ingredients
    INNER JOIN dbo.Ingredient i 
        ON ins.IngredientSubstanceID = i.IngredientSubstanceID
    INNER JOIN dbo.Product p 
        ON i.ProductID = p.ProductID
    -- Navigate to document
    INNER JOIN dbo.Section s 
        ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb 
        ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d 
        ON sb.DocumentID = d.DocumentID

WHERE ins.UNII IS NOT NULL
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductsByPharmacologicClass') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates products by pharmacologic/therapeutic class via active moiety linkage. Enables drug discovery by therapeutic category.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductsByPharmacologicClass';
END
GO

PRINT 'Created view: vw_ProductsByPharmacologicClass';
GO

--#endregion

--#region vw_PharmacologicClassHierarchy

/**************************************************************/
-- View: vw_PharmacologicClassHierarchy
-- Purpose: Shows parent-child relationships between pharmacologic classes
-- Usage: Navigate the therapeutic classification hierarchy
-- Returns: Class hierarchy with names and codes

IF OBJECT_ID('dbo.vw_PharmacologicClassHierarchy', 'V') IS NOT NULL
    DROP VIEW dbo.vw_PharmacologicClassHierarchy;
GO

CREATE VIEW dbo.vw_PharmacologicClassHierarchy
AS
/**************************************************************/
-- Exposes the pharmacologic class hierarchy for navigation
-- Enables drill-down through therapeutic categories
/**************************************************************/
SELECT 
    -- Child class (more specific)
    child.PharmacologicClassID AS ChildClassID,
    child.ClassCode AS ChildClassCode,
    child.ClassDisplayName AS ChildClassName,
    
    -- Parent class (more general)
    parent.PharmacologicClassID AS ParentClassID,
    parent.ClassCode AS ParentClassCode,
    parent.ClassDisplayName AS ParentClassName,
    
    -- Hierarchy linkage
    pch.PharmClassHierarchyID

FROM dbo.PharmacologicClassHierarchy pch
    INNER JOIN dbo.PharmacologicClass child ON pch.ChildPharmacologicClassID = child.PharmacologicClassID
    INNER JOIN dbo.PharmacologicClass parent ON pch.ParentPharmacologicClassID = parent.PharmacologicClassID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_PharmacologicClassHierarchy') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Shows parent-child relationships in the pharmacologic class hierarchy. Use for navigating therapeutic classification levels.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_PharmacologicClassHierarchy';
END
GO

PRINT 'Created view: vw_PharmacologicClassHierarchy';
GO

--#endregion

--#region vw_PharmacologicClassSummary

/**************************************************************/
-- View: vw_PharmacologicClassSummary
-- Purpose: Summary of pharmacologic classes with product counts
-- Usage: Discover which therapeutic classes have the most products
-- Returns: Class info with aggregated counts

IF OBJECT_ID('dbo.vw_PharmacologicClassSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_PharmacologicClassSummary;
GO

CREATE VIEW dbo.vw_PharmacologicClassSummary
AS
/**************************************************************/
-- Aggregated view of pharmacologic classes with product counts
/**************************************************************/
SELECT 
    pc.PharmacologicClassID,
    pc.ClassCode AS PharmClassCode,
    pc.ClassDisplayName AS PharmClassName,
    COUNT(DISTINCT pcl.ActiveMoietySubstanceID) AS LinkedSubstanceCount,
    COUNT(DISTINCT i.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount

FROM dbo.PharmacologicClass pc
    LEFT JOIN dbo.PharmacologicClassLink pcl ON pc.PharmacologicClassID = pcl.PharmacologicClassID
    LEFT JOIN dbo.IngredientSubstance ins ON pcl.ActiveMoietySubstanceID = ins.IngredientSubstanceID
    LEFT JOIN dbo.ActiveMoiety am ON ins.IngredientSubstanceID = am.IngredientSubstanceID
    LEFT JOIN dbo.Ingredient i ON am.IngredientSubstanceID = i.IngredientSubstanceID
    LEFT JOIN dbo.Product p ON i.ProductID = p.ProductID
    LEFT JOIN dbo.Section s ON p.SectionID = s.SectionID
    LEFT JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    LEFT JOIN dbo.Document d ON sb.DocumentID = d.DocumentID

GROUP BY 
    pc.PharmacologicClassID,
    pc.ClassCode,
    pc.ClassDisplayName
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_PharmacologicClassSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Summary of pharmacologic classes with substance and product counts. Use for therapeutic category analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_PharmacologicClassSummary';
END
GO

PRINT 'Created view: vw_PharmacologicClassSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 3: INGREDIENT AND SUBSTANCE NAVIGATION VIEWS                       */
/*  Views for locating products by active ingredient, UNII, or substance       */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductsByIngredient

/**************************************************************/
-- View: vw_ProductsByIngredient
-- Purpose: Locates all products containing a specific ingredient
-- Usage: Find drugs by active ingredient (UNII or name)
-- Returns: Product and ingredient details with document navigation
-- Indexes Used: IX_Ingredient_IngredientSubstanceID, IX_IngredientSubstance_UNII

IF OBJECT_ID('dbo.vw_ProductsByIngredient', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductsByIngredient;
GO

CREATE VIEW dbo.vw_ProductsByIngredient
AS
/**************************************************************/
-- Links products to their ingredients for drug composition queries
-- Supports lookup by UNII code or substance name
/**************************************************************/
SELECT 
    -- Ingredient substance identification
    ins.IngredientSubstanceID,
    ins.UNII,
    ins.SubstanceName,
    ins.OriginatingElement AS IngredientType,
    
    -- Ingredient details
    i.IngredientID,
    i.ClassCode AS IngredientClassCode,
    i.QuantityNumerator,
    i.QuantityNumeratorUnit,
    i.QuantityDenominator,
    i.DisplayName AS StrengthDisplayName,
    i.SequenceNumber AS IngredientSequence,
    
    -- Active moiety (if applicable)
    am.ActiveMoietyID,
    am.MoietyUNII,
    am.MoietyName,
    
    -- Product identification
    p.ProductID,
    p.ProductName,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS LabelEffectiveDate,
    
    -- Labeler
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName

FROM dbo.IngredientSubstance ins
    INNER JOIN dbo.Ingredient i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
    INNER JOIN dbo.Product p ON i.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.ActiveMoiety am ON ins.IngredientSubstanceID = am.IngredientSubstanceID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductsByIngredient') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates products by active/inactive ingredient. Supports lookup by UNII code or substance name. Includes strength and active moiety information.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductsByIngredient';
END
GO

PRINT 'Created view: vw_ProductsByIngredient';
GO

--#endregion

--#region vw_IngredientSummary

/**************************************************************/
-- View: vw_IngredientSummary
-- Purpose: Summary of ingredients with product counts
-- Usage: Discover most common ingredients across products
-- Returns: Ingredient info with product counts

IF OBJECT_ID('dbo.vw_IngredientSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_IngredientSummary;
GO

CREATE VIEW dbo.vw_IngredientSummary
AS
/**************************************************************/
-- Aggregated view of ingredients with product and document counts
/**************************************************************/
SELECT 
    ins.IngredientSubstanceID,
    ins.UNII,
    ins.SubstanceName,
    ins.OriginatingElement AS IngredientType,
    COUNT(DISTINCT i.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT o.OrganizationID) AS LabelerCount

FROM dbo.IngredientSubstance ins
    LEFT JOIN dbo.Ingredient i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
    LEFT JOIN dbo.Product p ON i.ProductID = p.ProductID
    LEFT JOIN dbo.Section s ON p.SectionID = s.SectionID
    LEFT JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    LEFT JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID

GROUP BY 
    ins.IngredientSubstanceID,
    ins.UNII,
    ins.SubstanceName,
    ins.OriginatingElement
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_IngredientSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Summary of ingredients with product and labeler counts. Use for ingredient prevalence analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_IngredientSummary';
END
GO

PRINT 'Created view: vw_IngredientSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 4: PRODUCT IDENTIFIER NAVIGATION VIEWS                             */
/*  Views for locating products by NDC, GTIN, or other identifiers             */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductsByNDC

/**************************************************************/
-- View: vw_ProductsByNDC
-- Purpose: Locates products by NDC (National Drug Code) or other item codes
-- Usage: Quick lookup by NDC for pharmacy/dispensing systems
-- Returns: Product details with all associated identifiers
-- Indexes Used: IX_ProductIdentifier_IdentifierValue_on_IdentifierType

IF OBJECT_ID('dbo.vw_ProductsByNDC', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductsByNDC;
GO

CREATE VIEW dbo.vw_ProductsByNDC
AS
/**************************************************************/
-- Enables product lookup by NDC or other product codes
-- Critical for pharmacy system integration
/**************************************************************/
SELECT 
    -- Product identifier (NDC, GTIN, etc.)
    pi.ProductIdentifierID,
    pi.IdentifierValue AS ProductCode,
    pi.IdentifierType AS CodeType,
    pi.IdentifierSystemOID AS CodeSystemOID,
    
    -- Product identification
    p.ProductID,
    p.ProductName,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    
    -- Generic name
    gm.GenericMedicineID,
    gm.GenericName,
    
    -- Marketing category
    mc.CategoryCode AS MarketingCategoryCode,
    mc.CategoryDisplayName AS MarketingCategoryName,
    mc.ApplicationOrMonographIDValue AS ApplicationNumber,
    
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS LabelEffectiveDate,
    
    -- Labeler
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName

FROM dbo.ProductIdentifier pi
    INNER JOIN dbo.Product p ON pi.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.GenericMedicine gm ON p.ProductID = gm.ProductID
    LEFT JOIN dbo.MarketingCategory mc ON p.ProductID = mc.ProductID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductsByNDC') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates products by NDC, GTIN, or other product codes. Critical for pharmacy system integration and product lookup.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductsByNDC';
END
GO

PRINT 'Created view: vw_ProductsByNDC';
GO

--#endregion

--#region vw_PackageByNDC

/**************************************************************/
-- View: vw_PackageByNDC
-- Purpose: Locates package configurations by NDC package code
-- Usage: Find specific package sizes/configurations
-- Returns: Package details with product navigation

IF OBJECT_ID('dbo.vw_PackageByNDC', 'V') IS NOT NULL
    DROP VIEW dbo.vw_PackageByNDC;
GO

CREATE VIEW dbo.vw_PackageByNDC
AS
/**************************************************************/
-- Enables package lookup by NDC package code
-- Shows packaging hierarchy and quantities
/**************************************************************/
SELECT 
    -- Package identifier
    pki.PackageIdentifierID,
    pki.IdentifierValue AS PackageCode,
    pki.IdentifierType AS CodeType,
    
    -- Packaging level details
    pl.PackagingLevelID,
    pl.PackageCode AS PackageItemCode,
    pl.PackageFormCode,
    pl.PackageFormDisplayName AS PackageType,
    pl.QuantityNumerator,
    pl.QuantityNumeratorUnit,
    
    -- Product identification
    p.ProductID,
    p.ProductName,
    
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID

FROM dbo.PackageIdentifier pki
    INNER JOIN dbo.PackagingLevel pl ON pki.PackagingLevelID = pl.PackagingLevelID
    INNER JOIN dbo.Product p ON pl.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_PackageByNDC') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates package configurations by NDC package code. Shows packaging hierarchy and quantities.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_PackageByNDC';
END
GO

PRINT 'Created view: vw_PackageByNDC';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 5: ORGANIZATION NAVIGATION VIEWS                                   */
/*  Views for locating products by labeler, manufacturer, or other orgs        */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductsByLabeler

/**************************************************************/
-- View: vw_ProductsByLabeler
-- Purpose: Locates all products marketed by a specific labeler
-- Usage: Find product portfolio for an organization
-- Returns: Products grouped by labeler organization
-- Indexes Used: IX_OrganizationIdentifier_IdentifierValue_on_IdentifierType

IF OBJECT_ID('dbo.vw_ProductsByLabeler', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductsByLabeler;
GO

CREATE VIEW dbo.vw_ProductsByLabeler
AS
/**************************************************************/
-- Lists products by labeler/marketing organization
-- Supports lookup by organization name or identifier (DUNS, Labeler Code)
/**************************************************************/
SELECT 
    -- Labeler organization
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName,
    
    -- Organization identifiers (DUNS, Labeler Code, etc.)
    oi.OrganizationIdentifierID,
    oi.IdentifierValue AS OrgIdentifierValue,
    oi.IdentifierType AS OrgIdentifierType,
    
    -- Product identification
    p.ProductID,
    p.ProductName,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    
    -- Generic name
    gm.GenericName,
    
    -- Marketing info
    mc.ApplicationOrMonographIDValue AS ApplicationNumber,
    mc.CategoryDisplayName AS MarketingCategory,
    
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS LabelEffectiveDate

FROM dbo.Organization o
    INNER JOIN dbo.DocumentAuthor da ON o.OrganizationID = da.OrganizationID
    INNER JOIN dbo.Document d ON da.DocumentID = d.DocumentID
    INNER JOIN dbo.StructuredBody sb ON d.DocumentID = sb.DocumentID
    INNER JOIN dbo.Section s ON sb.StructuredBodyID = s.StructuredBodyID
    INNER JOIN dbo.Product p ON s.SectionID = p.SectionID
    LEFT JOIN dbo.OrganizationIdentifier oi ON o.OrganizationID = oi.OrganizationID
    LEFT JOIN dbo.GenericMedicine gm ON p.ProductID = gm.ProductID
    LEFT JOIN dbo.MarketingCategory mc ON p.ProductID = mc.ProductID

WHERE da.AuthorType = 'Labeler'
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductsByLabeler') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Locates products by labeler organization. Supports lookup by name or identifier (DUNS, Labeler Code).',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductsByLabeler';
END
GO

PRINT 'Created view: vw_ProductsByLabeler';
GO

--#endregion

--#region vw_LabelerSummary

/**************************************************************/
-- View: vw_LabelerSummary
-- Purpose: Summary of labelers with product counts
-- Usage: Discover labelers by portfolio size
-- Returns: Labeler info with product counts

IF OBJECT_ID('dbo.vw_LabelerSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_LabelerSummary;
GO

CREATE VIEW dbo.vw_LabelerSummary
AS
/**************************************************************/
-- Aggregated view of labelers with portfolio statistics
/**************************************************************/
SELECT 
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName,
    COUNT(DISTINCT p.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT d.SetGUID) AS LabelSetCount,
    MIN(d.EffectiveTime) AS EarliestLabelDate,
    MAX(d.EffectiveTime) AS MostRecentLabelDate

FROM dbo.Organization o
    INNER JOIN dbo.DocumentAuthor da ON o.OrganizationID = da.OrganizationID
    INNER JOIN dbo.Document d ON da.DocumentID = d.DocumentID
    INNER JOIN dbo.StructuredBody sb ON d.DocumentID = sb.DocumentID
    INNER JOIN dbo.Section s ON sb.StructuredBodyID = s.StructuredBodyID
    LEFT JOIN dbo.Product p ON s.SectionID = p.SectionID

WHERE da.AuthorType = 'Labeler'

GROUP BY 
    o.OrganizationID,
    o.OrganizationName
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_LabelerSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Summary of labelers with product and document counts. Use for labeler portfolio analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_LabelerSummary';
END
GO

PRINT 'Created view: vw_LabelerSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 6: DOCUMENT NAVIGATION VIEWS                                       */
/*  Views for document discovery and version tracking                          */
/*                                                                             */
/*******************************************************************************/

--#region vw_DocumentNavigation

/**************************************************************/
-- View: vw_DocumentNavigation
-- Purpose: Lightweight document index for navigation
-- Usage: Quick document listing and version tracking
-- Returns: Document metadata for navigation
-- Indexes Used: IX_Document_DocumentGUID, IX_Document_SetGUID

IF OBJECT_ID('dbo.vw_DocumentNavigation', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DocumentNavigation;
GO

CREATE VIEW dbo.vw_DocumentNavigation
AS
/**************************************************************/
-- Lightweight document index with essential metadata
-- Use for document listings and version navigation
/**************************************************************/
SELECT 
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    
    -- Document metadata
    d.DocumentCode,
    d.DocumentDisplayName AS DocumentType,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS EffectiveDate,
    
    -- Labeler
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName,
    
    -- Product count for this document
    (SELECT COUNT(DISTINCT p.ProductID) 
     FROM dbo.StructuredBody sb2
     INNER JOIN dbo.Section s2 ON sb2.StructuredBodyID = s2.StructuredBodyID
     INNER JOIN dbo.Product p ON s2.SectionID = p.SectionID
     WHERE sb2.DocumentID = d.DocumentID) AS ProductCount,
    
    -- Version info
    (SELECT COUNT(*) FROM dbo.Document d2 WHERE d2.SetGUID = d.SetGUID) AS TotalVersions,
    
    -- Is this the latest version?
    CASE WHEN d.VersionNumber = (
        SELECT MAX(d3.VersionNumber) 
        FROM dbo.Document d3 
        WHERE d3.SetGUID = d.SetGUID
    ) THEN 1 ELSE 0 END AS IsLatestVersion

FROM dbo.Document d
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_DocumentNavigation') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Lightweight document index for navigation. Shows version info, product counts, and latest version flag.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_DocumentNavigation';
END
GO

PRINT 'Created view: vw_DocumentNavigation';
GO

--#endregion

--#region vw_DocumentVersionHistory

/**************************************************************/
-- View: vw_DocumentVersionHistory
-- Purpose: Shows all versions of a label set
-- Usage: Track label revision history
-- Returns: Version timeline for a label set

IF OBJECT_ID('dbo.vw_DocumentVersionHistory', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DocumentVersionHistory;
GO

CREATE VIEW dbo.vw_DocumentVersionHistory
AS
/**************************************************************/
-- Shows version history for label sets
-- Enables revision tracking and comparison
/**************************************************************/
SELECT 
    d.SetGUID,
    d.DocumentID,
    d.DocumentGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS EffectiveDate,
    d.DocumentCode,
    d.DocumentDisplayName AS DocumentType,
    
    -- Previous version reference
    rd.ReferencedDocumentGUID AS PredecessorDocGUID,
    rd.ReferencedVersionNumber AS PredecessorVersion,
    
    -- Labeler
    o.OrganizationName AS LabelerName

FROM dbo.Document d
    LEFT JOIN dbo.RelatedDocument rd ON d.DocumentID = rd.SourceDocumentID 
        AND rd.RelationshipTypeCode = 'RPLC'
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_DocumentVersionHistory') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Shows version history for label sets. Includes predecessor document references for revision tracking.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_DocumentVersionHistory';
END
GO

PRINT 'Created view: vw_DocumentVersionHistory';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 7: SECTION CONTENT NAVIGATION VIEWS                                */
/*  Views for navigating section content within documents                      */
/*                                                                             */
/*******************************************************************************/

--#region vw_SectionNavigation

/**************************************************************/
-- View: vw_SectionNavigation
-- Purpose: Section index for document navigation
-- Usage: Navigate to specific sections by LOINC code
-- Returns: Section metadata with document context
-- Indexes Used: IX_Section_SectionCode_on_DocumentID

IF OBJECT_ID('dbo.vw_SectionNavigation', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SectionNavigation;
GO

CREATE VIEW dbo.vw_SectionNavigation
AS
/**************************************************************/
-- Section index for navigating document structure
-- Supports lookup by LOINC code (e.g., find all Boxed Warnings)
/**************************************************************/
SELECT 
    -- Section identification
    s.SectionID,
    s.SectionGUID,
    s.SectionCode,
    s.SectionDisplayName AS SectionType,
    s.Title AS SectionTitle,
    
    -- Document context
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.Title AS DocumentTitle,
    d.VersionNumber,
    
    -- Hierarchy info
    sh.ParentSectionID,
    ps.SectionCode AS ParentSectionCode,
    ps.Title AS ParentSectionTitle,
    
    -- Content summary
    (SELECT COUNT(*) FROM dbo.SectionTextContent stc 
     WHERE stc.SectionID = s.SectionID) AS ContentBlockCount,
    
    -- Labeler
    o.OrganizationName AS LabelerName

FROM dbo.Section s
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.SectionHierarchy sh ON s.SectionID = sh.ChildSectionID
    LEFT JOIN dbo.Section ps ON sh.ParentSectionID = ps.SectionID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_SectionNavigation') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Section index for document navigation. Supports lookup by LOINC code to find specific section types across documents.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_SectionNavigation';
END
GO

PRINT 'Created view: vw_SectionNavigation';
GO

--#endregion

--#region vw_SectionTypeSummary

/**************************************************************/
-- View: vw_SectionTypeSummary
-- Purpose: Summary of section types across all documents
-- Usage: Discover available section types and their prevalence
-- Returns: Section type with document counts

IF OBJECT_ID('dbo.vw_SectionTypeSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SectionTypeSummary;
GO

CREATE VIEW dbo.vw_SectionTypeSummary
AS
/**************************************************************/
-- Summary of section types (LOINC codes) across documents
/**************************************************************/
SELECT 
    s.SectionCode,
    s.SectionDisplayName AS SectionType,
    COUNT(*) AS SectionCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT d.SetGUID) AS LabelSetCount

FROM dbo.Section s
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID

WHERE s.SectionCode IS NOT NULL

GROUP BY 
    s.SectionCode,
    s.SectionDisplayName
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_SectionTypeSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Summary of section types (LOINC codes) with document counts. Use to understand section type prevalence.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_SectionTypeSummary';
END
GO

PRINT 'Created view: vw_SectionTypeSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 8: DRUG INTERACTION AND SAFETY VIEWS                               */
/*  Views for drug interaction checking and safety lookups                     */
/*                                                                             */
/*******************************************************************************/

--#region vw_DrugInteractionLookup

/**************************************************************/
-- View: vw_DrugInteractionLookup
-- Purpose: Provides ingredient data for drug interaction checking
-- Usage: Input for drug interaction analysis systems
-- Returns: Product ingredients with UNII codes for interaction databases

IF OBJECT_ID('dbo.vw_DrugInteractionLookup', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DrugInteractionLookup;
GO

CREATE VIEW dbo.vw_DrugInteractionLookup
AS
/**************************************************************/
-- Drug interaction lookup data
-- Provides active ingredients for interaction checking systems
/**************************************************************/
SELECT 
    -- Product identification
    p.ProductID,
    p.ProductName,
    pi.IdentifierValue AS NDC,
    
    -- Active ingredient (for interaction checking)
    ins.IngredientSubstanceID,
    ins.UNII AS IngredientUNII,
    ins.SubstanceName AS IngredientName,
    
    -- Active moiety (often used for interaction databases)
    am.ActiveMoietyID,
    am.MoietyUNII,
    am.MoietyName,
    
    -- Ingredient class
    i.ClassCode AS IngredientClassCode,
    
    -- Pharmacologic class (if available)
    pc.PharmacologicClassID,
    pc.ClassCode AS PharmClassCode,
    pc.ClassDisplayName AS PharmClassName,
    
    -- Document for label reference
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID

FROM dbo.Product p
    INNER JOIN dbo.Ingredient i ON p.ProductID = i.ProductID
    INNER JOIN dbo.IngredientSubstance ins ON i.IngredientSubstanceID = ins.IngredientSubstanceID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.ProductIdentifier pi ON p.ProductID = pi.ProductID 
        AND pi.IdentifierType = 'NDC'
    LEFT JOIN dbo.ActiveMoiety am ON ins.IngredientSubstanceID = am.IngredientSubstanceID
    LEFT JOIN dbo.PharmacologicClassLink pcl ON ins.IngredientSubstanceID = pcl.ActiveMoietySubstanceID
    LEFT JOIN dbo.PharmacologicClass pc ON pcl.PharmacologicClassID = pc.PharmacologicClassID

WHERE i.ClassCode IN ('ACTIB', 'ACTIM', 'ACTIR')  -- Active ingredient class codes
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_DrugInteractionLookup') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Drug interaction lookup data with active ingredients and moieties. Use for drug interaction checking systems.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_DrugInteractionLookup';
END
GO

PRINT 'Created view: vw_DrugInteractionLookup';
GO

--#endregion

--#region vw_DEAScheduleLookup

/**************************************************************/
-- View: vw_DEAScheduleLookup
-- Purpose: Shows DEA controlled substance schedule for products
-- Usage: Regulatory compliance and dispensing restrictions
-- Returns: Products with DEA schedule classification

IF OBJECT_ID('dbo.vw_DEAScheduleLookup', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DEAScheduleLookup;
GO

CREATE VIEW dbo.vw_DEAScheduleLookup
AS
/**************************************************************/
-- DEA controlled substance schedule lookup
-- Critical for pharmacy dispensing compliance
/**************************************************************/
SELECT 
    -- Product identification
    p.ProductID,
    p.ProductName,
    pi.IdentifierValue AS NDC,
    
    -- DEA Schedule
    pol.PolicyCode AS DEAScheduleCode,
    pol.PolicyDisplayName AS DEASchedule,
    
    -- Generic name
    gm.GenericName,
    
    -- Document reference
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    
    -- Labeler
    o.OrganizationName AS LabelerName

FROM dbo.Policy pol
    INNER JOIN dbo.Product p ON pol.ProductID = p.ProductID
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.ProductIdentifier pi ON p.ProductID = pi.ProductID 
        AND pi.IdentifierType = 'NDC'
    LEFT JOIN dbo.GenericMedicine gm ON p.ProductID = gm.ProductID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID

WHERE pol.PolicyClassCode = 'DEADrugSchedule'
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_DEAScheduleLookup') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'DEA controlled substance schedule lookup. Critical for pharmacy dispensing compliance.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_DEAScheduleLookup';
END
GO

PRINT 'Created view: vw_DEAScheduleLookup';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 9: PRODUCT SUMMARY VIEWS                                           */
/*  Consolidated product information for comprehensive lookups                 */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductSummary

/**************************************************************/
-- View: vw_ProductSummary
-- Purpose: Comprehensive product summary with key attributes
-- Usage: Full product profile for API responses
-- Returns: Consolidated product information

IF OBJECT_ID('dbo.vw_ProductSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductSummary;
GO

CREATE VIEW dbo.vw_ProductSummary
AS
/**************************************************************/
-- Comprehensive product summary consolidating key attributes
-- Primary view for product profile API responses
/**************************************************************/
SELECT 
    -- Product identification
    p.ProductID,
    p.ProductName,
    p.ProductSuffix,
    p.FormCode AS DosageFormCode,
    p.FormDisplayName AS DosageFormName,
    p.DescriptionText,
    
    -- Generic name (first one if multiple)
    (SELECT TOP 1 gm.GenericName 
     FROM dbo.GenericMedicine gm 
     WHERE gm.ProductID = p.ProductID) AS GenericName,
    
    -- Primary NDC
    (SELECT TOP 1 pi.IdentifierValue 
     FROM dbo.ProductIdentifier pi 
     WHERE pi.ProductID = p.ProductID 
       AND pi.IdentifierType = 'NDC') AS PrimaryNDC,
    
    -- Marketing category
    mc.CategoryCode AS MarketingCategoryCode,
    mc.CategoryDisplayName AS MarketingCategory,
    mc.ApplicationOrMonographIDValue AS ApplicationNumber,
    mc.ApprovalDate,
    
    -- Route of administration (first one if multiple)
    (SELECT TOP 1 pra.RouteDisplayName 
     FROM dbo.ProductRouteOfAdministration pra 
     WHERE pra.ProductID = p.ProductID) AS RouteOfAdministration,
    
    -- DEA Schedule (if controlled)
    (SELECT TOP 1 pol.PolicyDisplayName 
     FROM dbo.Policy pol 
     WHERE pol.ProductID = p.ProductID 
       AND pol.PolicyClassCode = 'DEADrugSchedule') AS DEASchedule,
    
    -- Active ingredient count
    (SELECT COUNT(*) 
     FROM dbo.Ingredient i 
     WHERE i.ProductID = p.ProductID 
       AND i.ClassCode IN ('ACTIB', 'ACTIM', 'ACTIR')) AS ActiveIngredientCount,
    
    -- Document identification
    d.DocumentID,
    d.DocumentGUID,
    d.SetGUID,
    d.VersionNumber,
    d.Title AS DocumentTitle,
    d.EffectiveTime AS LabelEffectiveDate,
    d.DocumentCode,
    d.DocumentDisplayName AS DocumentType,
    
    -- Labeler
    o.OrganizationID AS LabelerOrgID,
    o.OrganizationName AS LabelerName,
    
    -- Labeler Code
    (SELECT TOP 1 oi.IdentifierValue 
     FROM dbo.OrganizationIdentifier oi 
     WHERE oi.OrganizationID = o.OrganizationID 
       AND oi.IdentifierType = 'LabelerCode') AS LabelerCode

FROM dbo.Product p
    INNER JOIN dbo.Section s ON p.SectionID = s.SectionID
    INNER JOIN dbo.StructuredBody sb ON s.StructuredBodyID = sb.StructuredBodyID
    INNER JOIN dbo.Document d ON sb.DocumentID = d.DocumentID
    LEFT JOIN dbo.MarketingCategory mc ON p.ProductID = mc.ProductID
    LEFT JOIN dbo.DocumentAuthor da ON d.DocumentID = da.DocumentID 
        AND da.AuthorType = 'Labeler'
    LEFT JOIN dbo.Organization o ON da.OrganizationID = o.OrganizationID
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_ProductSummary') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Comprehensive product summary consolidating key attributes. Primary view for product profile API responses.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductSummary';
END
GO

PRINT 'Created view: vw_ProductSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 10: CROSS-REFERENCE AND DISCOVERY VIEWS                            */
/*  Views for discovering relationships and related items                      */
/*                                                                             */
/*******************************************************************************/

--#region vw_RelatedProducts

/**************************************************************/
-- View: vw_RelatedProducts
-- Purpose: Finds products related by shared attributes
-- Usage: Discover alternatives, generics, or related drugs
-- Returns: Product relationships by various criteria

IF OBJECT_ID('dbo.vw_RelatedProducts', 'V') IS NOT NULL
    DROP VIEW dbo.vw_RelatedProducts;
GO

CREATE VIEW dbo.vw_RelatedProducts
AS
/**************************************************************/
-- Identifies related products by shared attributes
-- Useful for finding alternatives, generics, or similar drugs
-- Note: A unique product is identified by (ProductName, SectionID) combination
--       since the same product name can have multiple ProductID entries (one per NDC)
/**************************************************************/
SELECT
    -- Source product
    p1.ProductID AS SourceProductID,
    p1.ProductName AS SourceProductName,
    d1.DocumentGUID AS SourceDocumentGUID,

    -- Related product
    p2.ProductID AS RelatedProductID,
    p2.ProductName AS RelatedProductName,
    d2.DocumentGUID AS RelatedDocumentGUID,

    -- Relationship type
    'SameApplicationNumber' AS RelationshipType,
    mc1.ApplicationOrMonographIDValue AS SharedValue

FROM dbo.Product p1
    INNER JOIN dbo.MarketingCategory mc1 ON p1.ProductID = mc1.ProductID
    INNER JOIN dbo.MarketingCategory mc2 ON mc1.ApplicationOrMonographIDValue = mc2.ApplicationOrMonographIDValue
        AND mc1.ProductID <> mc2.ProductID
    INNER JOIN dbo.Product p2 ON mc2.ProductID = p2.ProductID
    INNER JOIN dbo.Section s1 ON p1.SectionID = s1.SectionID
    INNER JOIN dbo.StructuredBody sb1 ON s1.StructuredBodyID = sb1.StructuredBodyID
    INNER JOIN dbo.Document d1 ON sb1.DocumentID = d1.DocumentID
    INNER JOIN dbo.Section s2 ON p2.SectionID = s2.SectionID
    INNER JOIN dbo.StructuredBody sb2 ON s2.StructuredBodyID = sb2.StructuredBodyID
    INNER JOIN dbo.Document d2 ON sb2.DocumentID = d2.DocumentID

WHERE mc1.ApplicationOrMonographIDValue IS NOT NULL
    -- Exclude self-relationships: products with same name in the same section are the same logical product
    AND NOT (p1.ProductName = p2.ProductName AND p1.SectionID = p2.SectionID)

UNION ALL

-- Products sharing same active ingredient (by UNII)
SELECT
    p1.ProductID AS SourceProductID,
    p1.ProductName AS SourceProductName,
    d1.DocumentGUID AS SourceDocumentGUID,

    p2.ProductID AS RelatedProductID,
    p2.ProductName AS RelatedProductName,
    d2.DocumentGUID AS RelatedDocumentGUID,

    'SameActiveIngredient' AS RelationshipType,
    ins.UNII AS SharedValue

FROM dbo.Product p1
    INNER JOIN dbo.Ingredient i1 ON p1.ProductID = i1.ProductID
    INNER JOIN dbo.IngredientSubstance ins ON i1.IngredientSubstanceID = ins.IngredientSubstanceID
    INNER JOIN dbo.Ingredient i2 ON ins.IngredientSubstanceID = i2.IngredientSubstanceID
        AND i1.ProductID <> i2.ProductID
    INNER JOIN dbo.Product p2 ON i2.ProductID = p2.ProductID
    INNER JOIN dbo.Section s1 ON p1.SectionID = s1.SectionID
    INNER JOIN dbo.StructuredBody sb1 ON s1.StructuredBodyID = sb1.StructuredBodyID
    INNER JOIN dbo.Document d1 ON sb1.DocumentID = d1.DocumentID
    INNER JOIN dbo.Section s2 ON p2.SectionID = s2.SectionID
    INNER JOIN dbo.StructuredBody sb2 ON s2.StructuredBodyID = sb2.StructuredBodyID
    INNER JOIN dbo.Document d2 ON sb2.DocumentID = d2.DocumentID

WHERE ins.UNII IS NOT NULL
    AND i1.ClassCode IN ('ACTIB', 'ACTIM', 'ACTIR')
    AND i2.ClassCode IN ('ACTIB', 'ACTIM', 'ACTIR')
    -- Exclude self-relationships: products with same name in the same section are the same logical product
    AND NOT (p1.ProductName = p2.ProductName AND p1.SectionID = p2.SectionID)
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_RelatedProducts') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Identifies related products by shared application number or active ingredient. Use for finding alternatives or similar drugs.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_RelatedProducts';
END
GO

PRINT 'Created view: vw_RelatedProducts';
GO

--#endregion

--#region vw_APIEndpointGuide

/**************************************************************/
-- View: vw_APIEndpointGuide
-- Purpose: Metadata view describing available navigation views
-- Usage: Claude API can query this to discover available endpoints
-- Returns: View names and descriptions for AI-assisted navigation

IF OBJECT_ID('dbo.vw_APIEndpointGuide', 'V') IS NOT NULL
    DROP VIEW dbo.vw_APIEndpointGuide;
GO

CREATE VIEW dbo.vw_APIEndpointGuide
AS
/**************************************************************/
-- Metadata view for AI-assisted API endpoint discovery
-- Claude API can query this to understand available data access patterns
/**************************************************************/
SELECT 
    v.name AS ViewName,
    REPLACE(v.name, 'vw_', '') AS EndpointName,
    ISNULL(ep.value, 'No description available') AS Description,
    
    -- Categorize views for easier discovery
    CASE 
        WHEN v.name LIKE '%Application%' THEN 'Application/Regulatory'
        WHEN v.name LIKE '%Pharmacologic%' THEN 'Pharmacologic Class'
        WHEN v.name LIKE '%Ingredient%' THEN 'Ingredient/Substance'
        WHEN v.name LIKE '%NDC%' OR v.name LIKE '%Package%' THEN 'Product Codes'
        WHEN v.name LIKE '%Labeler%' OR v.name LIKE '%Organization%' THEN 'Organization'
        WHEN v.name LIKE '%Document%' OR v.name LIKE '%Version%' THEN 'Document Navigation'
        WHEN v.name LIKE '%Section%' THEN 'Section Content'
        WHEN v.name LIKE '%Drug%' OR v.name LIKE '%DEA%' THEN 'Drug Safety'
        WHEN v.name LIKE '%Product%' THEN 'Product Information'
        WHEN v.name LIKE '%Related%' THEN 'Cross-Reference'
        ELSE 'General'
    END AS Category,
    
    -- Usage hints for Claude API
    CASE 
        WHEN v.name = 'vw_ProductsByApplicationNumber' THEN 
            'Query with: WHERE ApplicationNumber = ''NDA014526'''
        WHEN v.name = 'vw_ProductsByPharmacologicClass' THEN 
            'Query with: WHERE PharmClassName LIKE ''%Beta-Blocker%'''
        WHEN v.name = 'vw_ProductsByIngredient' THEN 
            'Query with: WHERE UNII = ''ABC123'' OR SubstanceName LIKE ''%aspirin%'''
        WHEN v.name = 'vw_ProductsByNDC' THEN 
            'Query with: WHERE ProductCode = ''12345-678-90'''
        WHEN v.name = 'vw_ProductsByLabeler' THEN 
            'Query with: WHERE LabelerName LIKE ''%Pfizer%'''
        WHEN v.name = 'vw_DocumentNavigation' THEN 
            'Query with: WHERE IsLatestVersion = 1'
        WHEN v.name = 'vw_SectionNavigation' THEN 
            'Query with: WHERE SectionCode = ''34066-1'' (Boxed Warning)'
        WHEN v.name = 'vw_DrugInteractionLookup' THEN 
            'Query with: WHERE IngredientUNII IN (''UNII1'', ''UNII2'')'
        WHEN v.name = 'vw_DEAScheduleLookup' THEN 
            'Query with: WHERE DEAScheduleCode IS NOT NULL'
        WHEN v.name = 'vw_ProductSummary' THEN 
            'Query with: WHERE ProductID = 123 OR ProductName LIKE ''%Lipitor%'''
        WHEN v.name = 'vw_RelatedProducts' THEN 
            'Query with: WHERE SourceProductID = 123'
        ELSE 'See view definition for query patterns'
    END AS UsageHint

FROM sys.views v
    LEFT JOIN sys.extended_properties ep ON v.object_id = ep.major_id 
        AND ep.name = 'MS_Description' 
        AND ep.minor_id = 0

WHERE v.schema_id = SCHEMA_ID('dbo')
    AND v.name LIKE 'vw_%'
    AND v.name <> 'vw_APIEndpointGuide'
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties 
    WHERE major_id = OBJECT_ID('dbo.vw_APIEndpointGuide') 
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty 
        @name = N'MS_Description',
        @value = N'Metadata view for AI-assisted API endpoint discovery. Claude API queries this to understand available navigation views and usage patterns.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_APIEndpointGuide';
END
GO

PRINT 'Created view: vw_APIEndpointGuide';
GO

--#endregion



/*******************************************************************************/
/*                                                                             */
/*  SECTION 12: CONSOLIDATED VIEWS FROM ADDITIONAL FILES                       */
/*  Views consolidated from MedRecPro_Views_*.sql files                        */
/*  Date Added: 2025-12-30                                                     */
/*                                                                             */
/*******************************************************************************/

--#region vw_SectionContent (from MedRecPro_Views_SectionContent.sql)

/**************************************************************/
-- View: vw_SectionContent
-- Purpose: Section content with text for full-text search
-- Usage: Retrieve section text content for document display
-- Returns: Section content with document context

IF OBJECT_ID('dbo.vw_SectionContent', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SectionContent;
GO

CREATE VIEW dbo.vw_SectionContent
AS
SELECT
    dbo.[Document].DocumentID,
    dbo.Section.SectionID,
    dbo.[Document].DocumentGUID,
    dbo.[Document].SetGUID,
    dbo.Section.SectionGUID,
    dbo.[Document].VersionNumber,
    dbo.[Document].DocumentDisplayName,
    dbo.[Document].Title AS DocumentTitle,
    dbo.Section.SectionCode,
    dbo.Section.SectionDisplayName,
    dbo.Section.Title AS SectionTitle,
    dbo.SectionTextContent.ContentText,
    dbo.SectionTextContent.SequenceNumber,
    dbo.SectionTextContent.ContentType,
    dbo.Section.SectionCodeSystem
FROM dbo.[Document]
    INNER JOIN dbo.Section ON dbo.[Document].DocumentID = dbo.Section.DocumentID
    INNER JOIN dbo.SectionTextContent ON dbo.Section.SectionID = dbo.SectionTextContent.SectionID
WHERE (dbo.SectionTextContent.ContentText IS NOT NULL)
    AND (LEN(dbo.SectionTextContent.ContentText) > 3)
    AND (dbo.Section.Title IS NOT NULL)
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_SectionContent')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Section content with text for full-text search and document display. Filters empty or minimal content.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_SectionContent';
END
GO

PRINT 'Created view: vw_SectionContent';
GO

--#endregion

--#region vw_IngredientActiveSummary (from MedRecPro_Views_ActiveIngredient.sql)

/**************************************************************/
-- View: vw_IngredientActiveSummary
-- Purpose: Summary of active ingredients with product counts
-- Usage: Discover active ingredients by prevalence
-- Returns: Active ingredient info with product counts (excludes IACT)

IF OBJECT_ID('dbo.vw_IngredientActiveSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_IngredientActiveSummary;
GO

CREATE VIEW dbo.vw_IngredientActiveSummary
AS
SELECT
    ins.IngredientSubstanceID,
    ins.UNII,
    ins.SubstanceName,
    ins.OriginatingElement AS IngredientType,
    COUNT(DISTINCT i.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT o.OrganizationID) AS LabelerCount
FROM dbo.IngredientSubstance AS ins
    LEFT OUTER JOIN dbo.Ingredient AS i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
    LEFT OUTER JOIN dbo.Product AS p ON i.ProductID = p.ProductID
    LEFT OUTER JOIN dbo.Section AS s ON p.SectionID = s.SectionID
    LEFT OUTER JOIN dbo.StructuredBody AS sb ON s.StructuredBodyID = sb.StructuredBodyID
    LEFT OUTER JOIN dbo.[Document] AS d ON sb.DocumentID = d.DocumentID
    LEFT OUTER JOIN dbo.DocumentAuthor AS da ON d.DocumentID = da.DocumentID AND da.AuthorType = 'Labeler'
    LEFT OUTER JOIN dbo.Organization AS o ON da.OrganizationID = o.OrganizationID
GROUP BY ins.IngredientSubstanceID, ins.UNII, ins.SubstanceName, ins.OriginatingElement, i.ClassCode
HAVING (i.ClassCode <> 'IACT')
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_IngredientActiveSummary')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Summary of active ingredients (excluding IACT class) with product and labeler counts. Use for active ingredient prevalence analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_IngredientActiveSummary';
END
GO

PRINT 'Created view: vw_IngredientActiveSummary';
GO

--#endregion

--#region vw_IngredientInactiveSummary (from MedRecPro_Views_InactiveIngredient.sql)

/**************************************************************/
-- View: vw_IngredientInactiveSummary
-- Purpose: Summary of inactive ingredients with product counts
-- Usage: Discover inactive ingredients by prevalence
-- Returns: Inactive ingredient info with product counts (IACT only)

IF OBJECT_ID('dbo.vw_IngredientInactiveSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_IngredientInactiveSummary;
GO

CREATE VIEW dbo.vw_IngredientInactiveSummary
AS
SELECT
    ins.IngredientSubstanceID,
    ins.UNII,
    ins.SubstanceName,
    ins.OriginatingElement AS IngredientType,
    COUNT(DISTINCT i.ProductID) AS ProductCount,
    COUNT(DISTINCT d.DocumentID) AS DocumentCount,
    COUNT(DISTINCT o.OrganizationID) AS LabelerCount
FROM dbo.IngredientSubstance AS ins
    LEFT OUTER JOIN dbo.Ingredient AS i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
    LEFT OUTER JOIN dbo.Product AS p ON i.ProductID = p.ProductID
    LEFT OUTER JOIN dbo.Section AS s ON p.SectionID = s.SectionID
    LEFT OUTER JOIN dbo.StructuredBody AS sb ON s.StructuredBodyID = sb.StructuredBodyID
    LEFT OUTER JOIN dbo.[Document] AS d ON sb.DocumentID = d.DocumentID
    LEFT OUTER JOIN dbo.DocumentAuthor AS da ON d.DocumentID = da.DocumentID AND da.AuthorType = 'Labeler'
    LEFT OUTER JOIN dbo.Organization AS o ON da.OrganizationID = o.OrganizationID
GROUP BY ins.IngredientSubstanceID, ins.UNII, ins.SubstanceName, ins.OriginatingElement, i.ClassCode
HAVING (i.ClassCode = 'IACT')
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_IngredientInactiveSummary')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Summary of inactive ingredients (IACT class only) with product and labeler counts. Use for inactive ingredient prevalence analysis.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_IngredientInactiveSummary';
END
GO

PRINT 'Created view: vw_IngredientInactiveSummary';
GO

--#endregion

/*******************************************************************************/
/*                                                                             */
/*  SECTION 13: INGREDIENT NAVIGATION VIEWS                                    */
/*  Views for ingredient lookup with application number normalization          */
/*  Date Added: 2025-12-30                                                     */
/*                                                                             */
/*******************************************************************************/

--#region vw_Ingredients

/**************************************************************/
-- View: vw_Ingredients
-- Purpose: All ingredients with normalized application numbers
-- Usage: Find all ingredients with product and document context
-- Returns: Ingredient details with ApplicationType and normalized ApplicationNumber
-- Note: ApplicationNumber has prefix stripped (IND, NDA, BLA, ANDA, DMF, OTC, sANDA, sNDA, sBLA, PMA)

IF OBJECT_ID('dbo.vw_Ingredients', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Ingredients;
GO

CREATE VIEW dbo.vw_Ingredients
AS
SELECT
    dbo.[Document].DocumentGUID,
    dbo.[Document].SetGUID,
    dbo.Section.SectionGUID,
    dbo.Ingredient.IngredientID,
    dbo.Ingredient.ProductID,
    dbo.Ingredient.IngredientSubstanceID,
    dbo.MarketingCategory.MarketingCategoryID,
    dbo.Section.SectionID,
    dbo.Section.DocumentID,
    dbo.Ingredient.ClassCode,
    dbo.Product.ProductName,
    dbo.IngredientSubstance.SubstanceName,
    dbo.IngredientSubstance.UNII,
    dbo.MarketingCategory.CategoryDisplayName AS ApplicationType,
    CASE
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 6, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sNDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sBLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'ANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'NDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'BLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'DMF%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'OTC%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'PMA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'IND%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        ELSE dbo.MarketingCategory.ApplicationOrMonographIDValue
    END AS ApplicationNumber
    FROM  dbo.Section INNER JOIN
             dbo.[Document] ON dbo.Section.DocumentID = dbo.[Document].DocumentID RIGHT OUTER JOIN
             dbo.Product ON dbo.Section.SectionID = dbo.Product.SectionID LEFT OUTER JOIN
             dbo.MarketingCategory ON dbo.Product.ProductID = dbo.MarketingCategory.ProductID LEFT OUTER JOIN
             dbo.Ingredient LEFT OUTER JOIN
             dbo.IngredientSubstance 
                ON dbo.Ingredient.IngredientSubstanceID = dbo.IngredientSubstance.IngredientSubstanceID 
                ON dbo.Product.ProductID = dbo.Ingredient.ProductID

GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_Ingredients')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'All ingredients with normalized application numbers. Strips application type prefixes (IND, NDA, BLA, ANDA, DMF, OTC, sANDA, sNDA, sBLA, PMA) from ApplicationNumber.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_Ingredients';
END
GO

PRINT 'Created view: vw_Ingredients';
GO

--#endregion

--#region vw_InactiveIngredients

/**************************************************************/
-- View: vw_InactiveIngredients
-- Purpose: Inactive ingredients (IACT) with normalized application numbers
-- Usage: Find inactive ingredients with product and document context
-- Returns: Inactive ingredient details with ApplicationType and normalized ApplicationNumber
-- Filter: ClassCode = 'IACT'

IF OBJECT_ID('dbo.vw_InactiveIngredients', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InactiveIngredients;
GO

CREATE VIEW dbo.vw_InactiveIngredients
AS
SELECT
    dbo.[Document].DocumentGUID,
    dbo.[Document].SetGUID,
    dbo.Section.SectionGUID,
    dbo.Ingredient.IngredientID,
    dbo.Ingredient.ProductID,
    dbo.Ingredient.IngredientSubstanceID,
    dbo.MarketingCategory.MarketingCategoryID,
    dbo.Section.SectionID,
    dbo.Section.DocumentID,
    dbo.Ingredient.ClassCode,
    dbo.Product.ProductName,
    dbo.IngredientSubstance.SubstanceName,
    dbo.IngredientSubstance.UNII,
    dbo.MarketingCategory.CategoryDisplayName AS ApplicationType,
    CASE
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 6, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sNDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sBLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'ANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'NDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'BLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'DMF%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'OTC%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'PMA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'IND%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        ELSE dbo.MarketingCategory.ApplicationOrMonographIDValue
    END AS ApplicationNumber
    FROM  dbo.Section INNER JOIN
                 dbo.[Document] ON dbo.Section.DocumentID = dbo.[Document].DocumentID RIGHT OUTER JOIN
                 dbo.Product ON dbo.Section.SectionID = dbo.Product.SectionID LEFT OUTER JOIN
                 dbo.MarketingCategory ON dbo.Product.ProductID = dbo.MarketingCategory.ProductID LEFT OUTER JOIN
                 dbo.Ingredient LEFT OUTER JOIN
                 dbo.IngredientSubstance 
                    ON dbo.Ingredient.IngredientSubstanceID = dbo.IngredientSubstance.IngredientSubstanceID 
                    ON dbo.Product.ProductID = dbo.Ingredient.ProductID
WHERE (dbo.Ingredient.ClassCode = 'IACT')
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_InactiveIngredients')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Inactive ingredients (IACT class) with normalized application numbers. Strips application type prefixes from ApplicationNumber.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_InactiveIngredients';
END
GO

PRINT 'Created view: vw_InactiveIngredients';
GO

--#endregion

--#region vw_ActiveIngredients

/**************************************************************/
-- View: vw_ActiveIngredients
-- Purpose: Active ingredients (non-IACT) with normalized application numbers
-- Usage: Find active ingredients with product and document context
-- Returns: Active ingredient details with ApplicationType and normalized ApplicationNumber
-- Filter: ClassCode <> 'IACT'

IF OBJECT_ID('dbo.vw_ActiveIngredients', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ActiveIngredients;
GO

CREATE VIEW dbo.vw_ActiveIngredients
AS
SELECT
    dbo.[Document].DocumentGUID,
    dbo.[Document].SetGUID,
    dbo.Section.SectionGUID,
    dbo.Ingredient.IngredientID,
    dbo.Ingredient.ProductID,
    dbo.Ingredient.IngredientSubstanceID,
    dbo.MarketingCategory.MarketingCategoryID,
    dbo.Section.SectionID,
    dbo.Section.DocumentID,
    dbo.Ingredient.ClassCode,
    dbo.Product.ProductName,
    dbo.IngredientSubstance.SubstanceName,
    dbo.IngredientSubstance.UNII,
    dbo.MarketingCategory.CategoryDisplayName AS ApplicationType,
    CASE
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 6, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sNDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'sBLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'ANDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 5, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'NDA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'BLA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'DMF%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'OTC%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'PMA%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        WHEN dbo.MarketingCategory.ApplicationOrMonographIDValue LIKE 'IND%'
            THEN SUBSTRING(dbo.MarketingCategory.ApplicationOrMonographIDValue, 4, LEN(dbo.MarketingCategory.ApplicationOrMonographIDValue))
        ELSE dbo.MarketingCategory.ApplicationOrMonographIDValue
    END AS ApplicationNumber
    FROM  dbo.Section INNER JOIN
            dbo.[Document] ON dbo.Section.DocumentID = dbo.[Document].DocumentID RIGHT OUTER JOIN
            dbo.Product ON dbo.Section.SectionID = dbo.Product.SectionID LEFT OUTER JOIN
            dbo.MarketingCategory ON dbo.Product.ProductID = dbo.MarketingCategory.ProductID LEFT OUTER JOIN
            dbo.Ingredient LEFT OUTER JOIN
            dbo.IngredientSubstance 
            ON dbo.Ingredient.IngredientSubstanceID = dbo.IngredientSubstance.IngredientSubstanceID 
            ON dbo.Product.ProductID = dbo.Ingredient.ProductID
WHERE (dbo.Ingredient.ClassCode <> 'IACT')
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_ActiveIngredients')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Active ingredients (non-IACT classes) with normalized application numbers. Strips application type prefixes from ApplicationNumber.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ActiveIngredients';
END
GO

PRINT 'Created view: vw_ActiveIngredients';
GO

--#endregion


/*******************************************************************************/
/*                                                                             */
/*  SECTION: LATEST LABEL NAVIGATION VIEWS                                     */
/*  Views for locating the most recent label for products/ingredients          */
/*  Date Added: 2026-01-07                                                     */
/*                                                                             */
/*******************************************************************************/

--#region vw_ProductLatestLabel

/**************************************************************/
-- View: vw_ProductLatestLabel
-- Purpose: Returns the single most recent label (document) for each
--          UNII/ProductName combination based on EffectiveTime
-- Usage: Find the latest label information for a product or active ingredient
-- Returns: One row per UNII/ProductName with the most recent DocumentGUID
-- Indexes Used: IX_Document_EffectiveTime_LatestLabel,
--               IX_Ingredient_IngredientSubstanceID,
--               IX_IngredientSubstance_UNII
-- See also: vw_IngredientActiveSummary, vw_ProductsByIngredient

IF OBJECT_ID('dbo.vw_ProductLatestLabel', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductLatestLabel;
GO

CREATE VIEW dbo.vw_ProductLatestLabel
AS
/**************************************************************/
-- Returns the latest label for each UNII/ProductName combination
-- Uses ROW_NUMBER() partitioned by UNII and ProductName, ordered by EffectiveTime DESC
-- Only returns active ingredients (excludes IACT class)
/**************************************************************/
SELECT
    ProductName,
    ActiveIngredient,
    UNII,
    DocumentGUID
FROM (
    SELECT
        dbo.vw_IngredientActiveSummary.SubstanceName AS ActiveIngredient,
        dbo.vw_IngredientActiveSummary.UNII,
        dbo.vw_ProductsByIngredient.ProductName,
        dbo.[Document].DocumentGUID,
        ROW_NUMBER() OVER (
            PARTITION BY dbo.vw_IngredientActiveSummary.UNII,
                         dbo.vw_ProductsByIngredient.ProductName
            ORDER BY dbo.[Document].EffectiveTime DESC
        ) AS RowNum
    FROM dbo.vw_IngredientActiveSummary
    INNER JOIN dbo.vw_ProductsByIngredient
        ON dbo.vw_IngredientActiveSummary.IngredientSubstanceID = dbo.vw_ProductsByIngredient.IngredientSubstanceID
        AND dbo.vw_IngredientActiveSummary.UNII = dbo.vw_ProductsByIngredient.UNII
    INNER JOIN dbo.[Document]
        ON dbo.vw_ProductsByIngredient.DocumentID = dbo.[Document].DocumentID
    WHERE dbo.vw_ProductsByIngredient.MoietyUNII IS NOT NULL
        AND dbo.vw_ProductsByIngredient.IngredientClassCode <> 'IACT'
) AS RankedDocs
WHERE RowNum = 1;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_ProductLatestLabel')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Returns the single most recent label (document) for each UNII/ProductName combination. Use to find the latest label when searching by product or active ingredient.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductLatestLabel';
END
GO

PRINT 'Created view: vw_ProductLatestLabel';
GO

--#endregion

--#region vw_ProductIndications View

/**************************************************************/
-- View: vw_ProductIndications
-- Purpose: Returns product indication text combined with ingredients
-- Usage: Searching and displaying product indications by active ingredient
-- Related tables: vw_SectionNavigation, vw_Ingredients, SectionTextContent, TextList, TextListItem
-- Notes: Filters to INDICATION sections only and excludes inactive ingredients (IACT)
--        Combines ContentText and ItemText into single ContentText column

IF OBJECT_ID('dbo.vw_ProductIndications', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductIndications;
GO

CREATE VIEW dbo.vw_ProductIndications
AS
SELECT DISTINCT
    dbo.vw_Ingredients.ProductName,
    dbo.vw_Ingredients.SubstanceName,
    dbo.vw_Ingredients.UNII,
    dbo.vw_SectionNavigation.DocumentGUID,
    COALESCE(dbo.SectionTextContent.ContentText, '')
        + CASE WHEN dbo.TextListItem.ItemText IS NOT NULL
               THEN ' ' + dbo.TextListItem.ItemText
               ELSE '' END AS ContentText
FROM dbo.vw_SectionNavigation
INNER JOIN dbo.vw_Ingredients
    ON dbo.vw_SectionNavigation.DocumentID = dbo.vw_Ingredients.DocumentID
INNER JOIN dbo.SectionTextContent
    ON dbo.vw_SectionNavigation.SectionID = dbo.SectionTextContent.SectionID
LEFT OUTER JOIN dbo.TextListItem
    INNER JOIN dbo.TextList
        ON dbo.TextListItem.TextListID = dbo.TextList.TextListID
    ON dbo.SectionTextContent.SectionTextContentID = dbo.TextList.SectionTextContentID
WHERE dbo.SectionTextContent.SectionID IN
        (SELECT SectionID
         FROM dbo.vw_SectionNavigation AS vw_SectionNavigation_1
         WHERE SectionType LIKE 'INDICATION%')
    AND dbo.vw_Ingredients.ClassCode <> 'IACT'
    AND LTRIM(RTRIM(COALESCE(dbo.SectionTextContent.ContentText, '')
        + CASE WHEN dbo.TextListItem.ItemText IS NOT NULL
               THEN ' ' + dbo.TextListItem.ItemText
               ELSE '' END)) <> '';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_ProductIndications')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Returns product indication text combined with active ingredients. Filters to INDICATION sections and excludes inactive ingredients. Combines ContentText and ItemText into a single column.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_ProductIndications';
END
GO

PRINT 'Created view: vw_ProductIndications';
GO

--#endregion

/**************************************************************/
-- File: MedRecPro_Views_LabelSectionMarkdown.sql
-- Purpose: Creates vw_LabelSectionMarkdown view for aggregating section content
--          into markdown-formatted text blocks for LLM/API consumption
--
-- Dependencies:
--   - dbo.vw_SectionContent (must exist)
--   - SQL Server 2017+ (requires STRING_AGG function)
--
-- Execution: Run this script against MedRecLocal database
--   sqlcmd -S .\SQLEXPRESS -d MedRecLocal -i MedRecPro_Views_LabelSectionMarkdown.sql
--
-- Author: Auto-generated for LLM summarization optimization
-- Date: 2026-01-09
/**************************************************************/




PRINT '';
PRINT '=================================================================';
PRINT 'Creating vw_LabelSectionMarkdown View';
PRINT '=================================================================';
PRINT '';
GO

/**************************************************************/
-- View: vw_LabelSectionMarkdown
-- Purpose: Aggregates all ContentText for each document section into a single
--          markdown-formatted text block, optimized for LLM/API consumption
--          and label summarization workflows.
--
-- Design Rationale:
--   This view addresses the need for complete, contiguous section text that can
--   be efficiently consumed by AI/LLM APIs for label summarization. The existing
--   vw_SectionContent view returns individual content rows which requires
--   complex application-side aggregation and produces inconsistent results
--   when used with AI summarization skills.
--
-- Key Features:
--   1. STRING_AGG aggregation: Concatenates all ContentText rows for a section
--      in SequenceNumber order, separated by paragraph breaks
--   2. Markdown conversion: Transforms HTML-style SPL content tags to markdown:
--      - <content styleCode="bold"> → **text**
--      - <content styleCode="italics"> → *text*
--      - <content styleCode="underline"> → _text_ (markdown convention)
--   3. Section identification: Uses COALESCE to handle NULL SectionCode values
--      and generates a unique SectionKey for grouping
--   4. Markdown section headers: Prepends section title as ## header
--
-- Output Columns:
--   - DocumentGUID: Unique identifier for the label document
--   - SetGUID: Document set identifier (for version tracking)
--   - DocumentTitle: Full document title
--   - SectionCode: LOINC section code (may be NULL for some sections)
--   - SectionTitle: Human-readable section title (e.g., "ADVERSE REACTIONS")
--   - SectionKey: Computed unique key combining DocumentGUID + SectionCode/Title
--   - FullSectionText: Complete markdown-formatted section text with header
--   - ContentBlockCount: Number of content blocks aggregated (for diagnostics)
--
-- Usage Examples:
--   -- Get full markdown for all sections of a specific document:
--   SELECT SectionTitle, FullSectionText
--   FROM vw_LabelSectionMarkdown
--   WHERE DocumentGUID = '052493C7-89A3-452E-8140-04DD95F0D9E2'
--   ORDER BY SectionCode;
--
--   -- Get all ADVERSE REACTIONS sections for batch processing:
--   SELECT DocumentGUID, FullSectionText
--   FROM vw_LabelSectionMarkdown
--   WHERE SectionTitle = 'ADVERSE REACTIONS';
--
--   -- Combine all sections for a complete document markdown:
--   SELECT STRING_AGG(FullSectionText, CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10))
--          WITHIN GROUP (ORDER BY SectionCode)
--   FROM vw_LabelSectionMarkdown
--   WHERE DocumentGUID = '052493C7-89A3-452E-8140-04DD95F0D9E2';
--
-- Performance Considerations:
--   - Uses STRING_AGG (SQL Server 2017+) for efficient text aggregation
--   - Groups by DocumentGUID and SectionTitle to minimize row count
--   - Relies on existing indexes: IX_SectionTextContent_SectionID
--   - For very large result sets, consider filtering by DocumentGUID
--
-- Related Views:
--   - vw_SectionContent: Source view with individual content rows
--   - vw_SectionNavigation: Section hierarchy and navigation
--   - vw_ProductIndications: Filtered view for indication sections only
--
-- Markdown Conversion Notes:
--   The SPL XML format uses <content styleCode="X"> tags for formatting.
--   This view converts common styleCode values to markdown equivalents:
--   - "bold" → **bold**
--   - "italics" → *italics*
--   - "underline" → _underline_
--   - Nested tags are processed in sequence (innermost first in REPLACE chain)
--   - Tags with unrecognized styleCodes are stripped, preserving inner text
--
-- Version History:
--   2026-01-09: Initial creation for LLM summarization optimization

IF OBJECT_ID('dbo.vw_LabelSectionMarkdown', 'V') IS NOT NULL
    DROP VIEW dbo.vw_LabelSectionMarkdown;
GO

CREATE VIEW dbo.vw_LabelSectionMarkdown
AS
WITH ContentWithMarkdown AS (
    -- Step 1: Convert HTML-style content tags to Markdown
    -- Process each ContentText row, replacing SPL formatting with markdown
    SELECT
        sc.DocumentGUID,
        sc.SetGUID,
        sc.DocumentTitle,
        sc.SectionCode,
        sc.SectionTitle,
        sc.SequenceNumber,
        -- Create unique section key for grouping (handles NULL SectionCode)
        CAST(sc.DocumentGUID AS VARCHAR(36)) + '|' +
            COALESCE(sc.SectionCode, 'NULL') + '|' +
            COALESCE(sc.SectionTitle, '') AS SectionKey,
        -- Convert SPL content tags to Markdown
        -- Order matters: process specific patterns before generic cleanup
        REPLACE(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(
                            REPLACE(
                                REPLACE(
                                    REPLACE(
                                        REPLACE(
                                            REPLACE(sc.ContentText,
                                                -- Bold: <content styleCode="bold">text</content> → **text**
                                                '<content styleCode="bold">', '**'),
                                            '</content>', ''), -- First pass: close tags after bold
                                        '<content styleCode="italics">', '*'),
                                    -- Handle closing tags that remain
                                    '</content>', ''),
                                '<content styleCode="underline">', '_'),
                            '</content>', ''),
                        -- Clean up any remaining content tags with other styleCodes
                        '<content styleCode=', ''),
                    '">', ''),
                -- Handle self-closing variations
                '/>', ''),
            -- Clean up orphaned closing tags
            '</content>', '') AS ContentMarkdown
    FROM dbo.vw_SectionContent sc
    WHERE sc.ContentText IS NOT NULL
      AND LEN(LTRIM(RTRIM(sc.ContentText))) > 0
),
AggregatedSections AS (
    -- Step 2: Aggregate all content for each section
    SELECT
        DocumentGUID,
        SetGUID,
        DocumentTitle,
        SectionCode,
        SectionTitle,
        SectionKey,
        -- Aggregate content in sequence order, separated by double newlines
        STRING_AGG(
            CAST(ContentMarkdown AS NVARCHAR(MAX)),
            CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10)  -- Paragraph separator (CRLF + blank line)
        ) WITHIN GROUP (ORDER BY SequenceNumber) AS AggregatedText,
        COUNT(*) AS ContentBlockCount
    FROM ContentWithMarkdown
    GROUP BY
        DocumentGUID,
        SetGUID,
        DocumentTitle,
        SectionCode,
        SectionTitle,
        SectionKey
)
-- Step 3: Add markdown section header and final formatting
SELECT
    DocumentGUID,
    SetGUID,
    DocumentTitle,
    SectionCode,
    SectionTitle,
    SectionKey,
    -- Prepend section title as markdown header
    '## ' + COALESCE(SectionTitle, 'Untitled Section') +
        CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
        COALESCE(AggregatedText, '') AS FullSectionText,
    ContentBlockCount
FROM AggregatedSections;
GO

-- Add extended property description
IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_LabelSectionMarkdown')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Aggregates section content into markdown-formatted text blocks for LLM/API consumption. Converts SPL HTML-style tags to markdown and concatenates all ContentText by DocumentGUID and SectionTitle.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_LabelSectionMarkdown';
END
GO

PRINT 'Created view: vw_LabelSectionMarkdown';
GO

/**************************************************************/
-- Verification Queries
-- Uncomment and run to test the view after creation
/**************************************************************/

-- Test 1: Check view exists and has expected columns
/*
SELECT
    c.name AS ColumnName,
    t.name AS DataType
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.vw_LabelSectionMarkdown')
ORDER BY c.column_id;
*/

-- Test 2: Sample a single document's sections
/*
SELECT TOP 5
    SectionTitle,
    ContentBlockCount,
    LEN(FullSectionText) AS TextLength,
    LEFT(FullSectionText, 200) AS TextPreview
FROM vw_LabelSectionMarkdown
WHERE DocumentGUID = (SELECT TOP 1 DocumentGUID FROM vw_LabelSectionMarkdown)
ORDER BY SectionCode;
*/

-- Test 3: Compare row counts (should be significantly fewer rows than source)
/*
SELECT
    'vw_SectionContent' AS ViewName,
    COUNT(*) AS RowCount
FROM vw_SectionContent
UNION ALL
SELECT
    'vw_LabelSectionMarkdown' AS ViewName,
    COUNT(*) AS RowCount
FROM vw_LabelSectionMarkdown;
*/

-- Test 4: Get complete document markdown for a single document
/*
SELECT STRING_AGG(FullSectionText, CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10))
       WITHIN GROUP (ORDER BY SectionCode) AS CompleteDocumentMarkdown
FROM vw_LabelSectionMarkdown
WHERE DocumentGUID = '052493C7-89A3-452E-8140-04DD95F0D9E2';
*/

PRINT '';
PRINT '=================================================================';
PRINT 'vw_LabelSectionMarkdown Creation Complete';
PRINT '=================================================================';
PRINT '';
PRINT 'View Usage:';
PRINT '  - Query by DocumentGUID to get all sections for a label';
PRINT '  - Query by SectionTitle for cross-label analysis';
PRINT '  - Use STRING_AGG on FullSectionText for complete document markdown';
PRINT '';
PRINT 'Example Query:';
PRINT '  SELECT SectionTitle, FullSectionText';
PRINT '  FROM vw_LabelSectionMarkdown';
PRINT '  WHERE DocumentGUID = ''your-guid-here''';
PRINT '  ORDER BY SectionCode;';
PRINT '';
GO


/*******************************************************************************/
/*                                                                             */
/*  SECTION FINAL SUMMARY                                                      */
/*                                                                             */
/*******************************************************************************/

-- Summary of all views created
PRINT '';
PRINT '=================================================================';
PRINT 'MedRecPro SPL Label Navigation Views Creation Complete';
PRINT '=================================================================';
PRINT '';

-- Count total views created
SELECT 
    'Total Navigation Views Created' AS Summary,
    COUNT(*) AS ViewCount
FROM sys.views v
WHERE v.schema_id = SCHEMA_ID('dbo')
    AND v.name LIKE 'vw_%';

-- List all views with categories
SELECT 
    v.name AS ViewName,
    CASE 
        WHEN v.name LIKE '%Application%' THEN 'Application/Regulatory'
        WHEN v.name LIKE '%Pharmacologic%' THEN 'Pharmacologic Class'
        WHEN v.name LIKE '%Ingredient%' THEN 'Ingredient/Substance'
        WHEN v.name LIKE '%NDC%' OR v.name LIKE '%Package%' THEN 'Product Codes'
        WHEN v.name LIKE '%Labeler%' THEN 'Organization'
        WHEN v.name LIKE '%Document%' OR v.name LIKE '%Version%' THEN 'Document Navigation'
        WHEN v.name LIKE '%Section%' THEN 'Section Content'
        WHEN v.name LIKE '%Drug%' OR v.name LIKE '%DEA%' THEN 'Drug Safety'
        WHEN v.name LIKE '%Product%' THEN 'Product Information'
        WHEN v.name LIKE '%Related%' THEN 'Cross-Reference'
        WHEN v.name LIKE '%API%' THEN 'API Metadata'
        ELSE 'General'
    END AS Category
FROM sys.views v
WHERE v.schema_id = SCHEMA_ID('dbo')
    AND v.name LIKE 'vw_%'
ORDER BY Category, v.name;

GO

PRINT '';
PRINT 'View creation script completed successfully.';
PRINT '';
PRINT 'Available Navigation Patterns:';
PRINT '  - By Application Number: vw_ProductsByApplicationNumber, vw_ApplicationNumberSummary';
PRINT '  - By Pharmacologic Class: vw_ProductsByPharmacologicClass, vw_PharmacologicClassHierarchy';
PRINT '  - By Ingredient/UNII: vw_ProductsByIngredient, vw_IngredientSummary';
PRINT '  - By NDC/Product Code: vw_ProductsByNDC, vw_PackageByNDC';
PRINT '  - By Labeler: vw_ProductsByLabeler, vw_LabelerSummary';
PRINT '  - Document Navigation: vw_DocumentNavigation, vw_DocumentVersionHistory';
PRINT '  - Section Navigation: vw_SectionNavigation, vw_SectionTypeSummary';
PRINT '  - Drug Safety: vw_DrugInteractionLookup, vw_DEAScheduleLookup';
PRINT '  - Product Summary: vw_ProductSummary';
PRINT '  - Cross-Reference: vw_RelatedProducts';
PRINT '  - API Discovery: vw_APIEndpointGuide';
PRINT '';
GO



/*******************************************************************************/
/*                                                                             */
/*  SECTION: INVENTORY SUMMARY VIEW                                            */
/*  Comprehensive inventory summary for AI-driven discovery                    */
/*                                                                             */
/*******************************************************************************/

--#region vw_InventorySummary

/**************************************************************/
-- View: vw_InventorySummary
-- Purpose: Comprehensive inventory summary for answering "what products do you have"
--          Provides counts across multiple dimensions in a compact format
-- Usage: Quick discovery of database scope and top entities
-- Returns: ~50 rows covering documents, products, labelers, ingredients, etc.
-- Indexes Used: Various primary key and foreign key indexes
-- See also: vw_ProductSummary, vw_LabelerSummary, vw_IngredientSummary

IF OBJECT_ID('dbo.vw_InventorySummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InventorySummary;
GO

CREATE VIEW dbo.vw_InventorySummary
AS
/**************************************************************/
-- Comprehensive inventory summary for answering "what products do you have"
-- Provides counts across multiple dimensions in a compact format
-- Target: ~50 rows covering documents, products, labelers, ingredients, etc.
/**************************************************************/

-- Section 1: Top-level entity counts
SELECT
    'TOTALS' AS Category,
    'Documents' AS Dimension,
    NULL AS DimensionValue,
    COUNT(*) AS ItemCount,
    1 AS SortOrder
FROM dbo.Document

UNION ALL

SELECT
    'TOTALS' AS Category,
    'Products' AS Dimension,
    NULL AS DimensionValue,
    COUNT(*) AS ItemCount,
    2 AS SortOrder
FROM dbo.Product

UNION ALL

SELECT
    'TOTALS' AS Category,
    'Labelers' AS Dimension,
    NULL AS DimensionValue,
    COUNT(DISTINCT o.OrganizationID) AS ItemCount,
    3 AS SortOrder
FROM dbo.Organization o
INNER JOIN dbo.DocumentAuthor da ON o.OrganizationID = da.OrganizationID
WHERE da.AuthorType = 'Labeler'

UNION ALL

SELECT
    'TOTALS' AS Category,
    'Active Ingredients (UNII)' AS Dimension,
    NULL AS DimensionValue,
    COUNT(DISTINCT ins.UNII) AS ItemCount,
    4 AS SortOrder
FROM dbo.IngredientSubstance ins
INNER JOIN dbo.Ingredient i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
WHERE i.ClassCode <> 'IACT'

UNION ALL

SELECT
    'TOTALS' AS Category,
    'Pharmacologic Classes' AS Dimension,
    NULL AS DimensionValue,
    COUNT(DISTINCT PharmacologicClassID) AS ItemCount,
    5 AS SortOrder
FROM dbo.PharmacologicClass

UNION ALL

SELECT
    'TOTALS' AS Category,
    'NDCs' AS Dimension,
    NULL AS DimensionValue,
    COUNT(*) AS ItemCount,
    6 AS SortOrder
FROM dbo.ProductIdentifier
WHERE IdentifierType = 'NDC'

UNION ALL

-- Section 2: Products by Marketing Category (typically 5-10 rows)
SELECT
    'BY_MARKETING_CATEGORY' AS Category,
    'Marketing Category' AS Dimension,
    COALESCE(mc.CategoryDisplayName, '(No Category)') AS DimensionValue,
    COUNT(DISTINCT p.ProductID) AS ItemCount,
    100 + ROW_NUMBER() OVER (ORDER BY COUNT(DISTINCT p.ProductID) DESC) AS SortOrder
FROM dbo.Product p
LEFT JOIN dbo.MarketingCategory mc ON p.ProductID = mc.ProductID
GROUP BY mc.CategoryDisplayName

UNION ALL

-- Section 3: Products by Dosage Form (top 15)
SELECT
    'BY_DOSAGE_FORM' AS Category,
    'Dosage Form' AS Dimension,
    COALESCE(p.FormDisplayName, '(Unknown)') AS DimensionValue,
    COUNT(*) AS ItemCount,
    200 + ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) AS SortOrder
FROM dbo.Product p
GROUP BY p.FormDisplayName
HAVING COUNT(*) >= (
    SELECT MIN(cnt) FROM (
        SELECT TOP 15 COUNT(*) as cnt
        FROM dbo.Product
        GROUP BY FormDisplayName
        ORDER BY COUNT(*) DESC
    ) t
)

UNION ALL

-- Section 4: Top 10 Labelers by product count
SELECT
    'TOP_LABELERS' AS Category,
    'Labeler' AS Dimension,
    o.OrganizationName AS DimensionValue,
    COUNT(DISTINCT p.ProductID) AS ItemCount,
    300 + ROW_NUMBER() OVER (ORDER BY COUNT(DISTINCT p.ProductID) DESC) AS SortOrder
FROM dbo.Organization o
INNER JOIN dbo.DocumentAuthor da ON o.OrganizationID = da.OrganizationID
INNER JOIN dbo.Document d ON da.DocumentID = d.DocumentID
INNER JOIN dbo.StructuredBody sb ON d.DocumentID = sb.DocumentID
INNER JOIN dbo.Section s ON sb.StructuredBodyID = s.StructuredBodyID
INNER JOIN dbo.Product p ON s.SectionID = p.SectionID
WHERE da.AuthorType = 'Labeler'
GROUP BY o.OrganizationID, o.OrganizationName
HAVING COUNT(DISTINCT p.ProductID) >= (
    SELECT MIN(cnt) FROM (
        SELECT TOP 10 COUNT(DISTINCT p2.ProductID) as cnt
        FROM dbo.Organization o2
        INNER JOIN dbo.DocumentAuthor da2 ON o2.OrganizationID = da2.OrganizationID
        INNER JOIN dbo.Document d2 ON da2.DocumentID = d2.DocumentID
        INNER JOIN dbo.StructuredBody sb2 ON d2.DocumentID = sb2.DocumentID
        INNER JOIN dbo.Section s2 ON sb2.StructuredBodyID = s2.StructuredBodyID
        INNER JOIN dbo.Product p2 ON s2.SectionID = p2.SectionID
        WHERE da2.AuthorType = 'Labeler'
        GROUP BY o2.OrganizationID
        ORDER BY COUNT(DISTINCT p2.ProductID) DESC
    ) t
)

UNION ALL

-- Section 5: Top 10 Pharmacologic Classes by product count
SELECT
    'TOP_PHARM_CLASSES' AS Category,
    'Pharmacologic Class' AS Dimension,
    pc.ClassDisplayName AS DimensionValue,
    COUNT(DISTINCT p.ProductID) AS ItemCount,
    400 + ROW_NUMBER() OVER (ORDER BY COUNT(DISTINCT p.ProductID) DESC) AS SortOrder
FROM dbo.PharmacologicClass pc
INNER JOIN dbo.PharmacologicClassLink pcl ON pc.PharmacologicClassID = pcl.PharmacologicClassID
INNER JOIN dbo.IngredientSubstance ams ON pcl.ActiveMoietySubstanceID = ams.IngredientSubstanceID
INNER JOIN dbo.ActiveMoiety am ON ams.IngredientSubstanceID = am.IngredientSubstanceID
INNER JOIN dbo.Ingredient i ON am.IngredientSubstanceID = i.IngredientSubstanceID
INNER JOIN dbo.Product p ON i.ProductID = p.ProductID
GROUP BY pc.PharmacologicClassID, pc.ClassDisplayName
HAVING COUNT(DISTINCT p.ProductID) >= (
    SELECT MIN(cnt) FROM (
        SELECT TOP 10 COUNT(DISTINCT p2.ProductID) as cnt
        FROM dbo.PharmacologicClass pc2
        INNER JOIN dbo.PharmacologicClassLink pcl2 ON pc2.PharmacologicClassID = pcl2.PharmacologicClassID
        INNER JOIN dbo.IngredientSubstance ams2 ON pcl2.ActiveMoietySubstanceID = ams2.IngredientSubstanceID
        INNER JOIN dbo.ActiveMoiety am2 ON ams2.IngredientSubstanceID = am2.IngredientSubstanceID
        INNER JOIN dbo.Ingredient i2 ON am2.IngredientSubstanceID = i2.IngredientSubstanceID
        INNER JOIN dbo.Product p2 ON i2.ProductID = p2.ProductID
        GROUP BY pc2.PharmacologicClassID
        ORDER BY COUNT(DISTINCT p2.ProductID) DESC
    ) t
)

UNION ALL

-- Section 6: Top 10 Active Ingredients by product count
SELECT
    'TOP_INGREDIENTS' AS Category,
    'Active Ingredient' AS Dimension,
    ins.SubstanceName AS DimensionValue,
    COUNT(DISTINCT p.ProductID) AS ItemCount,
    500 + ROW_NUMBER() OVER (ORDER BY COUNT(DISTINCT p.ProductID) DESC) AS SortOrder
FROM dbo.IngredientSubstance ins
INNER JOIN dbo.Ingredient i ON ins.IngredientSubstanceID = i.IngredientSubstanceID
INNER JOIN dbo.Product p ON i.ProductID = p.ProductID
WHERE i.ClassCode <> 'IACT'
GROUP BY ins.IngredientSubstanceID, ins.SubstanceName
HAVING COUNT(DISTINCT p.ProductID) >= (
    SELECT MIN(cnt) FROM (
        SELECT TOP 10 COUNT(DISTINCT p2.ProductID) as cnt
        FROM dbo.IngredientSubstance ins2
        INNER JOIN dbo.Ingredient i2 ON ins2.IngredientSubstanceID = i2.IngredientSubstanceID
        INNER JOIN dbo.Product p2 ON i2.ProductID = p2.ProductID
        WHERE i2.ClassCode <> 'IACT'
        GROUP BY ins2.IngredientSubstanceID
        ORDER BY COUNT(DISTINCT p2.ProductID) DESC
    ) t
)

GO

-- Add extended property for documentation
IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE major_id = OBJECT_ID('dbo.vw_InventorySummary')
    AND name = 'MS_Description'
)
BEGIN
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'Comprehensive inventory summary for answering "what products do you have". Provides counts across multiple dimensions (totals, marketing categories, dosage forms, top labelers, pharmacologic classes, active ingredients) in a compact ~50 row format.',
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'VIEW', @level1name = N'vw_InventorySummary';
END
GO

PRINT 'Created view: vw_InventorySummary';
GO

--#endregion

PRINT '';
PRINT '=================================================================';
PRINT 'Additional Views and Indexes Creation Complete';
PRINT '=================================================================';
PRINT '';
PRINT 'New Views Added:';
PRINT '  - vw_SectionContent: Section text content for full-text search';
PRINT '  - vw_IngredientActiveSummary: Active ingredient summary';
PRINT '  - vw_IngredientInactiveSummary: Inactive ingredient summary';
PRINT '  - vw_Ingredients: All ingredients with normalized application numbers';
PRINT '  - vw_InactiveIngredients: Inactive ingredients (IACT) with normalized app numbers';
PRINT '  - vw_ActiveIngredients: Active ingredients (non-IACT) with normalized app numbers';
PRINT '  - vw_ProductLatestLabel: Latest label per UNII/ProductName combination';
PRINT '  - vw_InventorySummary: Comprehensive inventory summary for AI discovery';
PRINT '';

GO
