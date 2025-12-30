USE [MedRecLocal]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER VIEW [dbo].[vw_ProductsByPharmacologicClass]
AS
/**************************************************************/
-- Links products to their pharmacologic classes via UNII matching
-- Join path: IngredientSubstance.UNII → IdentifiedSubstance.SubstanceIdentifierValue 
--            (where SubjectType = 'ActiveMoiety')
--            → PharmacologicClass.IdentifiedSubstanceID
-- This approach is order-of-operation independent (no PharmacologicClassLink dependency)
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

FROM dbo.IngredientSubstance ins
    -- Step 1: Match UNII to IdentifiedSubstance (ActiveMoiety records only)
    INNER JOIN dbo.IdentifiedSubstance ids 
        ON ins.UNII = ids.SubstanceIdentifierValue
        AND ids.SubjectType = 'ActiveMoiety'
    -- Step 2: Get PharmacologicClass via IdentifiedSubstanceID
    INNER JOIN dbo.PharmacologicClass pc 
        ON ids.IdentifiedSubstanceID = pc.IdentifiedSubstanceID
    -- Link to ActiveMoiety for moiety details
    LEFT JOIN dbo.ActiveMoiety am 
        ON ins.IngredientSubstanceID = am.IngredientSubstanceID
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