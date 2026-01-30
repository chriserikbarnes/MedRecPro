USE [MedRecLocal]
GO

/**************************************************************/
-- PharmClassDosageFormExclusion Table
-- 
-- Purpose: Defines business rules for illogical pharmacologic class and 
-- dosage form combinations. Used to filter out false-positive class 
-- associations when a substance appears as an active ingredient but the 
-- dosage form is incompatible with the therapeutic class's mechanism of action.
--
-- Example: Polyethylene Glycol (PEG) as "Osmotic Laxative" should only 
-- apply to oral formulations, not injectables where PEG serves as a 
-- solubilizer/excipient.
--
-- <seealso cref="vw_PharmacologicClassByActiveIngredient"/>
-- <seealso cref="PharmacologicClass"/>
-- <seealso cref="Product.FormCode"/>
/**************************************************************/

-- ============================================================
-- DROP EXISTING OBJECTS (safe for re-run)
-- ============================================================

-- Drop extended property if exists
IF EXISTS (
    SELECT 1 FROM sys.extended_properties ep
    INNER JOIN sys.tables t ON ep.major_id = t.object_id
    WHERE t.name = 'PharmClassDosageFormExclusion' 
      AND t.schema_id = SCHEMA_ID('dbo')
      AND ep.name = 'MS_Description'
)
BEGIN
    EXEC sys.sp_dropextendedproperty 
        @name = N'MS_Description', 
        @level0type = N'SCHEMA', @level0name = N'dbo', 
        @level1type = N'TABLE', @level1name = N'PharmClassDosageFormExclusion';
END
GO

-- Drop table if exists (indexes drop automatically with table)
IF OBJECT_ID('dbo.PharmClassDosageFormExclusion', 'U') IS NOT NULL
    DROP TABLE dbo.PharmClassDosageFormExclusion;
GO

-- ============================================================
-- CREATE TABLE
-- ============================================================

CREATE TABLE dbo.PharmClassDosageFormExclusion (
    /**************************************************************/
    -- ExclusionID: Primary key, auto-incremented
    /**************************************************************/
    ExclusionID INT IDENTITY(1,1) PRIMARY KEY,
    
    /**************************************************************/
    -- ClassCode: NCI Thesaurus code for the pharmacologic class (e.g., N0000175811)
    -- References PharmacologicClass.ClassCode
    /**************************************************************/
    ClassCode NVARCHAR(50) NOT NULL,
    
    /**************************************************************/
    -- ClassDisplayName: Human-readable name for documentation/auditing
    /**************************************************************/
    ClassDisplayName NVARCHAR(255) NOT NULL,
    
    /**************************************************************/
    -- ExcludedFormCode: FDA SPL dosage form code to exclude
    -- References Product.FormCode
    -- Increased to NVARCHAR(100) to accommodate long form names like
    -- 'INJECTION, POWDER, FOR SUSPENSION, EXTENDED RELEASE'
    /**************************************************************/
    ExcludedFormCode NVARCHAR(100) NOT NULL,
    
    /**************************************************************/
    -- ExcludedFormDisplayName: Human-readable dosage form name
    -- Increased to NVARCHAR(100) for consistency with ExcludedFormCode
    /**************************************************************/
    ExcludedFormDisplayName NVARCHAR(100) NOT NULL,
    
    /**************************************************************/
    -- ExclusionCategory: Logical grouping for maintenance
    -- Values: 'GI_ORAL_ONLY', 'INJECTABLE_ONLY', 'TOPICAL_ONLY', 
    --         'INHALATION_ONLY', 'DEVICE_SPECIFIC', 'OPHTHALMIC_ONLY'
    /**************************************************************/
    ExclusionCategory NVARCHAR(50) NOT NULL,
    
    /**************************************************************/
    -- Reason: Business rule explanation for documentation
    /**************************************************************/
    Reason NVARCHAR(500) NOT NULL,
    
    /**************************************************************/
    -- IsActive: Soft delete flag for audit trail
    /**************************************************************/
    IsActive BIT NOT NULL DEFAULT 1,
    
    /**************************************************************/
    -- CreatedDate: Audit timestamp
    /**************************************************************/
    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    
    /**************************************************************/
    -- ModifiedDate: Audit timestamp
    /**************************************************************/
    ModifiedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Create index for common query pattern
CREATE NONCLUSTERED INDEX IX_PharmClassDosageFormExclusion_ClassCode_FormCode
ON dbo.PharmClassDosageFormExclusion (ClassCode, ExcludedFormCode)
WHERE IsActive = 1;
GO

-- Create index for category-based maintenance queries
CREATE NONCLUSTERED INDEX IX_PharmClassDosageFormExclusion_Category
ON dbo.PharmClassDosageFormExclusion (ExclusionCategory)
WHERE IsActive = 1;
GO

/**************************************************************/
-- Extended property for table documentation
/**************************************************************/
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Defines exclusion rules for pharmacologic class/dosage form combinations. 
               When a substance appears as an active ingredient but the dosage form is 
               incompatible with the class mechanism of action, the class association 
               should be excluded. Example: Osmotic laxatives require oral administration 
               and should not be associated with injectable formulations.', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'PharmClassDosageFormExclusion';
GO


/**************************************************************/
-- SEED DATA
-- 
-- Organized by ExclusionCategory for maintainability
-- 
-- Categories:
-- 1. GI_ORAL_ONLY - GI tract agents that require oral administration
-- 2. INJECTABLE_ONLY - Agents that must be injected (vaccines, biologics)
-- 3. TOPICAL_ONLY - Agents requiring topical/local application
-- 4. INHALATION_ONLY - Respiratory agents requiring inhalation
-- 5. DEVICE_SPECIFIC - Device-dependent formulations (IUDs, implants)
-- 6. OPHTHALMIC_ONLY - Ocular agents
/**************************************************************/

-- ============================================================
-- CATEGORY 1: GI_ORAL_ONLY
-- GI tract-specific classes that require oral/enteral routes
-- These cannot be injectables, topicals, inhalants, etc.
-- ============================================================

-- Define all injectable forms to exclude for GI-oral-only classes
DECLARE @InjectableForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @InjectableForms VALUES
    ('INJECTABLE FOAM', 'INJECTABLE FOAM'),
    ('INJECTION', 'INJECTION'),
    ('INJECTION, EMULSION', 'INJECTION, EMULSION'),
    ('INJECTION, LIPID COMPLEX', 'INJECTION, LIPID COMPLEX'),
    ('INJECTION, POWDER, FOR SOLUTION', 'INJECTION, POWDER, FOR SOLUTION'),
    ('INJECTION, POWDER, FOR SUSPENSION', 'INJECTION, POWDER, FOR SUSPENSION'),
    ('INJECTION, POWDER, FOR SUSPENSION, EXTENDED RELEASE', 'INJECTION, POWDER, FOR SUSPENSION, EXTENDED RELEASE'),
    ('INJECTION, POWDER, LYOPHILIZED, FOR SOLUTION', 'INJECTION, POWDER, LYOPHILIZED, FOR SOLUTION'),
    ('INJECTION, POWDER, LYOPHILIZED, FOR SUSPENSION', 'INJECTION, POWDER, LYOPHILIZED, FOR SUSPENSION'),
    ('INJECTION, SOLUTION', 'INJECTION, SOLUTION'),
    ('INJECTION, SOLUTION, CONCENTRATE', 'INJECTION, SOLUTION, CONCENTRATE'),
    ('INJECTION, SUSPENSION', 'INJECTION, SUSPENSION'),
    ('INJECTION, SUSPENSION, EXTENDED RELEASE', 'INJECTION, SUSPENSION, EXTENDED RELEASE'),
    ('INJECTION, SUSPENSION, LIPOSOMAL', 'INJECTION, SUSPENSION, LIPOSOMAL'),
    ('IMPLANT', 'IMPLANT'),
    ('PELLET', 'PELLET');

-- Define topical forms to exclude for GI-oral-only classes
DECLARE @TopicalForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @TopicalForms VALUES
    ('AEROSOL', 'AEROSOL'),
    ('AEROSOL, FOAM', 'AEROSOL, FOAM'),
    ('AEROSOL, METERED', 'AEROSOL, METERED'),
    ('AEROSOL, POWDER', 'AEROSOL, POWDER'),
    ('CLOTH', 'CLOTH'),
    ('CREAM', 'CREAM'),
    ('CREAM, AUGMENTED', 'CREAM, AUGMENTED'),
    ('GEL', 'GEL'),
    ('GEL, DENTIFRICE', 'GEL, DENTIFRICE'),
    ('GEL, METERED', 'GEL, METERED'),
    ('INHALANT', 'INHALANT'),
    ('LOTION', 'LOTION'),
    ('LOTION, AUGMENTED', 'LOTION, AUGMENTED'),
    ('OIL', 'OIL'),
    ('OINTMENT', 'OINTMENT'),
    ('OINTMENT, AUGMENTED', 'OINTMENT, AUGMENTED'),
    ('PASTE', 'PASTE'),
    ('PASTE, DENTIFRICE', 'PASTE, DENTIFRICE'),
    ('PATCH', 'PATCH'),
    ('PATCH, EXTENDED RELEASE', 'PATCH, EXTENDED RELEASE'),
    ('POWDER', 'POWDER'),
    ('POWDER, METERED', 'POWDER, METERED'),
    ('RING', 'RING'),
    ('SHAMPOO', 'SHAMPOO'),
    ('SHAMPOO, SUSPENSION', 'SHAMPOO, SUSPENSION'),
    ('SPRAY', 'SPRAY'),
    ('SPRAY, METERED', 'SPRAY, METERED'),
    ('STICK', 'STICK'),
    ('SWAB', 'SWAB');

-- Define ophthalmic/otic forms to exclude for GI-oral-only classes
DECLARE @OphthalmicOticForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @OphthalmicOticForms VALUES
    ('SOLUTION, GEL FORMING / DROPS', 'SOLUTION, GEL FORMING / DROPS'),
    ('SOLUTION/ DROPS', 'SOLUTION/ DROPS'),
    ('SUSPENSION/ DROPS', 'SUSPENSION/ DROPS'),
    ('INSERT', 'INSERT'),
    ('INSERT, EXTENDED RELEASE', 'INSERT, EXTENDED RELEASE'),
    ('IRRIGANT', 'IRRIGANT');

-- Define device forms to exclude for GI-oral-only classes
DECLARE @DeviceForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @DeviceForms VALUES
    ('INTRAUTERINE DEVICE', 'INTRAUTERINE DEVICE'),
    ('SYSTEM', 'SYSTEM');

-- Define oral solid forms to exclude for injectable-only classes (and Osmotic Activity)
DECLARE @OralSolidForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @OralSolidForms VALUES
    ('CAPSULE', 'CAPSULE'),
    ('CAPSULE, COATED PELLETS', 'CAPSULE, COATED PELLETS'),
    ('CAPSULE, COATED, EXTENDED RELEASE', 'CAPSULE, COATED, EXTENDED RELEASE'),
    ('CAPSULE, DELAYED RELEASE', 'CAPSULE, DELAYED RELEASE'),
    ('CAPSULE, DELAYED RELEASE PELLETS', 'CAPSULE, DELAYED RELEASE PELLETS'),
    ('CAPSULE, EXTENDED RELEASE', 'CAPSULE, EXTENDED RELEASE'),
    ('CAPSULE, GELATIN COATED', 'CAPSULE, GELATIN COATED'),
    ('CAPSULE, LIQUID FILLED', 'CAPSULE, LIQUID FILLED'),
    ('GRANULE', 'GRANULE'),
    ('GRANULE, DELAYED RELEASE', 'GRANULE, DELAYED RELEASE'),
    ('GRANULE, FOR SOLUTION', 'GRANULE, FOR SOLUTION'),
    ('GRANULE, FOR SUSPENSION', 'GRANULE, FOR SUSPENSION'),
    ('TABLET', 'TABLET'),
    ('TABLET, CHEWABLE', 'TABLET, CHEWABLE'),
    ('TABLET, CHEWABLE, EXTENDED RELEASE', 'TABLET, CHEWABLE, EXTENDED RELEASE'),
    ('TABLET, COATED', 'TABLET, COATED'),
    ('TABLET, DELAYED RELEASE', 'TABLET, DELAYED RELEASE'),
    ('TABLET, EXTENDED RELEASE', 'TABLET, EXTENDED RELEASE'),
    ('TABLET, FILM COATED', 'TABLET, FILM COATED'),
    ('TABLET, FILM COATED, EXTENDED RELEASE', 'TABLET, FILM COATED, EXTENDED RELEASE'),
    ('TABLET, FOR SUSPENSION', 'TABLET, FOR SUSPENSION'),
    ('TABLET, MULTILAYER', 'TABLET, MULTILAYER'),
    ('TABLET, MULTILAYER, EXTENDED RELEASE', 'TABLET, MULTILAYER, EXTENDED RELEASE'),
    ('TABLET, ORALLY DISINTEGRATING', 'TABLET, ORALLY DISINTEGRATING'),
    ('TABLET, ORALLY DISINTEGRATING, DELAYED RELEASE', 'TABLET, ORALLY DISINTEGRATING, DELAYED RELEASE'),
    ('TABLET, SUGAR COATED', 'TABLET, SUGAR COATED');

-- Define oral liquid forms to exclude for injectable-only classes
DECLARE @OralLiquidForms TABLE (FormCode NVARCHAR(100), FormName NVARCHAR(100));
INSERT INTO @OralLiquidForms VALUES
    ('ELIXIR', 'ELIXIR'),
    ('EMULSION', 'EMULSION'),
    ('SOLUTION', 'SOLUTION'),
    ('SOLUTION, CONCENTRATE', 'SOLUTION, CONCENTRATE'),
    ('SUSPENSION', 'SUSPENSION'),
    ('SUSPENSION, EXTENDED RELEASE', 'SUSPENSION, EXTENDED RELEASE'),
    ('SYRUP', 'SYRUP'),
    ('TINCTURE', 'TINCTURE'),
    ('LIQUID', 'LIQUID'),
    ('CONCENTRATE', 'CONCENTRATE'),
    ('FOR SOLUTION', 'FOR SOLUTION'),
    ('FOR SUSPENSION', 'FOR SUSPENSION'),
    ('FOR SUSPENSION, EXTENDED RELEASE', 'FOR SUSPENSION, EXTENDED RELEASE'),
    ('POWDER, FOR SOLUTION', 'POWDER, FOR SOLUTION'),
    ('POWDER, FOR SUSPENSION', 'POWDER, FOR SUSPENSION');


/**************************************************************/
-- OSMOTIC LAXATIVE [EPC] - N0000175811
-- Mechanism: Draws water into intestinal lumen via osmosis
-- Requires: Oral administration with high-volume solutions (PEG 3350, GoLYTELY)
-- Cannot work via: Injection, topical, inhalation, ophthalmic, oral solids
-- Note: Oral solid dosage forms (tablets/capsules) of osmotically active 
--       substances like KCl are for electrolyte supplementation, not laxative effect
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175811', 
    'Osmotic Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic laxatives require oral administration to reach the intestinal lumen where osmotic water retention occurs. Injectable forms use the same substance (e.g., PEG) as an excipient, not for laxative effect.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175811', 
    'Osmotic Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic laxatives require oral administration. Topical forms cannot deliver the mechanism of action to the intestinal tract.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175811', 
    'Osmotic Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic laxatives require oral administration. Ophthalmic/otic forms cannot deliver the mechanism of action to the intestinal tract.'
FROM @OphthalmicOticForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175811', 
    'Osmotic Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic laxatives require oral administration. Device-based delivery systems are incompatible with GI tract mechanism.'
FROM @DeviceForms;

-- Oral solid dosage forms - KCl tablets/capsules are for electrolyte replacement, not osmotic laxative effect
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175811', 
    'Osmotic Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic laxatives require high-volume oral solutions (e.g., PEG 3350, GoLYTELY). Oral solid dosage forms (tablets, capsules) of osmotically active substances like KCl are used for electrolyte supplementation, not osmotic laxative effect.'
FROM @OralSolidForms;


/**************************************************************/
-- OSMOTIC ACTIVITY [MoA] - N0000010288
-- Mechanism: General osmotic activity (draws water via osmosis)
-- Context: When used therapeutically for laxative effect, requires oral route
--          with specific formulations (solutions, powders for reconstitution)
-- Problem: Osmotically active substances (KCl, NaCl, etc.) appear in:
--          - IV fluids for electrolyte replacement
--          - Oral tablets/capsules for electrolyte supplementation
--          - Topical products for tonicity
--          None of these are for osmotic laxative effect
-- Valid forms: SOLUTION, POWDER FOR SOLUTION (oral laxative preps like MiraLAX, GoLYTELY)
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000010288', 
    'Osmotic Activity [MoA]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic activity as a laxative mechanism requires oral/rectal administration to the GI tract. Injectable forms use osmotically active substances (e.g., KCl, NaCl) for electrolyte replacement or tonicity adjustment, not for osmotic laxative effect.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000010288', 
    'Osmotic Activity [MoA]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic activity for laxative effect requires GI tract delivery. Topical forms use osmotically active substances for different purposes (e.g., wound care, tonicity).'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000010288', 
    'Osmotic Activity [MoA]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic activity for laxative effect requires GI tract delivery. Ophthalmic/otic forms use osmotically active substances for tonicity adjustment, not laxative effect.'
FROM @OphthalmicOticForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000010288', 
    'Osmotic Activity [MoA]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic activity for laxative effect requires GI tract delivery. Device-based delivery systems are incompatible.'
FROM @DeviceForms;

-- Oral solid dosage forms - KCl tablets/capsules are for electrolyte replacement, not osmotic laxative effect
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000010288', 
    'Osmotic Activity [MoA]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Osmotic activity for laxative effect requires high-volume oral solutions (e.g., PEG 3350, GoLYTELY). Oral solid dosage forms (tablets, capsules) of osmotically active substances like KCl are used for electrolyte supplementation, not osmotic laxative effect.'
FROM @OralSolidForms;


/**************************************************************/
-- STIMULANT LAXATIVE [EPC] - N0000175812
-- Mechanism: Stimulates intestinal motility and secretion
-- Requires: Oral or rectal administration
-- Cannot work via: Injection, topical, inhalation, ophthalmic
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175812', 
    'Stimulant Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Stimulant laxatives require oral or rectal administration to directly stimulate colonic motility and secretion.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175812', 
    'Stimulant Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Stimulant laxatives require GI tract contact. Topical forms cannot reach the colon to exert their effect.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175812', 
    'Stimulant Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Stimulant laxatives require GI tract contact. Ophthalmic/otic delivery cannot reach the colon.'
FROM @OphthalmicOticForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175812', 
    'Stimulant Laxative [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Stimulant laxatives require GI tract contact. Device-based delivery systems are incompatible.'
FROM @DeviceForms;


/**************************************************************/
-- INTESTINAL LIPASE INHIBITOR [EPC] - N0000175591
-- Mechanism: Inhibits pancreatic/gastric lipase in GI tract
-- Requires: Oral administration to reach GI tract enzymes
-- Cannot work via: Injection, topical, inhalation, ophthalmic
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175591', 
    'Intestinal Lipase Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Intestinal lipase inhibitors must be taken orally to reach the intestinal lumen where dietary fat absorption occurs.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175591', 
    'Intestinal Lipase Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Intestinal lipase inhibitors require oral administration. Topical forms cannot reach GI enzymes.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175591', 
    'Intestinal Lipase Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Intestinal lipase inhibitors require oral administration. Ophthalmic/otic forms cannot reach GI tract.'
FROM @OphthalmicOticForms;


/**************************************************************/
-- ALPHA-GLUCOSIDASE INHIBITOR [EPC] - N0000175559
-- Mechanism: Inhibits intestinal alpha-glucosidase enzymes
-- Requires: Oral administration to reach brush border enzymes
-- Cannot work via: Injection, topical, inhalation, ophthalmic
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175559', 
    'Alpha-Glucosidase Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Alpha-glucosidase inhibitors must be taken orally to reach intestinal brush border enzymes where carbohydrate digestion occurs.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175559', 
    'Alpha-Glucosidase Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Alpha-glucosidase inhibitors require oral administration. Topical forms cannot reach intestinal enzymes.'
FROM @TopicalForms;


/**************************************************************/
-- ANTIDIARRHEAL [EPC] - N0000178374
-- Mechanism: Reduces GI motility or absorbs excess fluid in GI tract
-- Requires: Oral administration
-- Cannot work via: Injection (systemic), topical, inhalation
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000178374', 
    'Antidiarrheal [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Antidiarrheals require oral administration to act within the GI tract lumen or on GI smooth muscle.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000178374', 
    'Antidiarrheal [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Antidiarrheals require oral administration. Ophthalmic/otic delivery cannot reach the GI tract.'
FROM @OphthalmicOticForms;


/**************************************************************/
-- GASTROINTESTINAL MOTILITY INHIBITOR [EPC] - N0000190853
-- Mechanism: Reduces GI smooth muscle motility
-- Requires: Oral or injectable (for acute treatment)
-- Cannot work via: Topical, ophthalmic
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000190853', 
    'Gastrointestinal Motility Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'GI motility inhibitors require oral or parenteral administration to reach GI smooth muscle receptors.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000190853', 
    'Gastrointestinal Motility Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'GI motility inhibitors require systemic or enteral administration. Ophthalmic/otic forms are incompatible.'
FROM @OphthalmicOticForms;


/**************************************************************/
-- PROTON PUMP INHIBITOR [EPC] - N0000175525
-- Mechanism: Inhibits gastric H+/K+ ATPase
-- Requires: Oral or IV administration
-- Cannot work via: Topical, ophthalmic, inhalation
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175525', 
    'Proton Pump Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Proton pump inhibitors require systemic absorption to reach gastric parietal cells. Topical forms cannot achieve this.'
FROM @TopicalForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175525', 
    'Proton Pump Inhibitor [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Proton pump inhibitors require systemic absorption. Ophthalmic/otic delivery is incompatible.'
FROM @OphthalmicOticForms;


/**************************************************************/
-- DEMULCENT [EPC] - N0000175535
-- Mechanism: Forms protective coating on mucous membranes
-- Requires: Direct contact with affected mucosa (oral, topical)
-- Cannot work via: Injection (systemic)
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175535', 
    'Demulcent [EPC]', 
    FormCode, 
    FormName,
    'GI_ORAL_ONLY',
    'Demulcents require direct contact with mucous membranes. Injectable forms cannot provide the local coating effect.'
FROM @InjectableForms;


-- ============================================================
-- CATEGORY 2: INJECTABLE_ONLY
-- Classes that require parenteral administration
-- These cannot be oral tablets, topical, etc.
-- ============================================================

/**************************************************************/
-- PARENTERAL IRON REPLACEMENT [EPC] - N0000177913
-- Mechanism: Direct iron delivery to reticuloendothelial system
-- Requires: IV/IM injection for direct systemic delivery
-- Cannot work via: Oral (different mechanism - GI absorption)
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000177913', 
    'Parenteral Iron Replacement [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Parenteral iron formulations are specifically designed for IV/IM administration to bypass GI absorption limitations. Oral iron is a separate therapeutic class.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000177913', 
    'Parenteral Iron Replacement [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Parenteral iron requires injection. Oral liquid forms cannot deliver the parenteral mechanism.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000177913', 
    'Parenteral Iron Replacement [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Parenteral iron requires injection. Topical forms cannot achieve systemic iron delivery.'
FROM @TopicalForms;


-- ============================================================
-- CATEGORY 3: DEVICE_SPECIFIC
-- Classes that are inherently tied to specific device types
-- ============================================================

/**************************************************************/
-- COPPER-CONTAINING INTRAUTERINE DEVICE [EPC] - N0000175831
-- Mechanism: Local copper ion release for contraception
-- Requires: Intrauterine device form factor
-- Cannot be: Any other dosage form
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175831', 
    'Copper-containing Intrauterine Device [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Copper IUD class is device-specific. The mechanism requires intrauterine copper release which cannot be achieved with oral forms.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175831', 
    'Copper-containing Intrauterine Device [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Copper IUD class is device-specific. Oral liquids cannot deliver intrauterine copper.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175831', 
    'Copper-containing Intrauterine Device [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Copper IUD class is device-specific. Injectable forms cannot replicate the intrauterine mechanism.'
FROM @InjectableForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175831', 
    'Copper-containing Intrauterine Device [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Copper IUD class is device-specific. Topical forms cannot achieve intrauterine delivery.'
FROM @TopicalForms;


/**************************************************************/
-- PROGESTIN-CONTAINING INTRAUTERINE SYSTEM [EPC] - N0000175832
-- Mechanism: Local progestin release in uterus
-- Requires: Intrauterine device form factor
-- Cannot be: Oral, injectable (different class), topical
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175832', 
    'Progestin-containing Intrauterine System [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Progestin IUS class is device-specific. Oral progestins are classified differently (systemic progestins).'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175832', 
    'Progestin-containing Intrauterine System [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Progestin IUS class is device-specific. Oral liquids would be classified as systemic progestins.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175832', 
    'Progestin-containing Intrauterine System [EPC]', 
    FormCode, 
    FormName,
    'DEVICE_SPECIFIC',
    'Progestin IUS class is device-specific. Injectable progestins are classified separately.'
FROM @InjectableForms;


-- ============================================================
-- CATEGORY 4: INHALATION_ONLY
-- Classes that require inhalation delivery
-- ============================================================

/**************************************************************/
-- INHALATION DIAGNOSTIC AGENT [EPC] - N0000175866
-- Mechanism: Diagnostic agent delivered directly to airways
-- Requires: Inhalation delivery
-- Cannot be: Oral, injectable, topical
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175866', 
    'Inhalation Diagnostic Agent [EPC]', 
    FormCode, 
    FormName,
    'INHALATION_ONLY',
    'Inhalation diagnostic agents must be delivered to the airways via inhalation. Oral forms cannot achieve pulmonary delivery.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175866', 
    'Inhalation Diagnostic Agent [EPC]', 
    FormCode, 
    FormName,
    'INHALATION_ONLY',
    'Inhalation diagnostic agents require pulmonary delivery. Oral liquids enter the GI tract, not airways.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175866', 
    'Inhalation Diagnostic Agent [EPC]', 
    FormCode, 
    FormName,
    'INHALATION_ONLY',
    'Inhalation diagnostic agents require direct airway delivery. Injectable forms bypass the pulmonary target.'
FROM @InjectableForms;


-- ============================================================
-- CATEGORY 5: TOPICAL_ONLY
-- Classes requiring topical/local application
-- ============================================================

/**************************************************************/
-- SKIN LIGHTENING AGENT [EPC] - N0000175855
-- Mechanism: Local inhibition of melanin production
-- Requires: Topical application to skin
-- Cannot be: Oral, injectable (systemic exposure = toxicity)
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175855', 
    'Skin Lightening Agent [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Skin lightening agents require direct topical application to the affected skin. Oral forms would cause systemic toxicity.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175855', 
    'Skin Lightening Agent [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Skin lightening agents require direct topical application. Oral liquids would cause systemic toxicity.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175855', 
    'Skin Lightening Agent [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Skin lightening agents require direct skin application. Injectable forms would cause systemic toxicity.'
FROM @InjectableForms;


/**************************************************************/
-- PSORALEN [EPC] - N0000175879
-- Mechanism: Photosensitizer for PUVA therapy
-- Requires: Oral or topical (for localized treatment)
-- Note: Some psoralens are oral, so only topical-only restriction applies to certain forms
-- This is a nuanced case - psoralens can be oral or topical depending on treatment area
/**************************************************************/
-- Psoralens can be oral or topical, so we only exclude injectables and ophthalmics
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175879', 
    'Psoralen [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Psoralens are used for phototherapy (PUVA) and require skin presence during UV exposure. Injectable forms would not provide appropriate tissue distribution.'
FROM @InjectableForms;


/**************************************************************/
-- ALPHA-HYDROXY ACID [EPC] - N0000175842
-- Mechanism: Chemical exfoliation of stratum corneum
-- Requires: Topical application
-- Cannot be: Oral, injectable
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175842', 
    'Alpha-Hydroxy Acid [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Alpha-hydroxy acids require direct skin contact for chemical exfoliation. Oral administration would cause GI irritation without therapeutic benefit.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175842', 
    'Alpha-Hydroxy Acid [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Alpha-hydroxy acids require direct skin contact. Oral liquids would irritate the GI tract.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175842', 
    'Alpha-Hydroxy Acid [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Alpha-hydroxy acids require direct skin contact. Injectable administration is incompatible with the mechanism.'
FROM @InjectableForms;


/**************************************************************/
-- ANTISEPTIC [EPC] - N0000175486
-- Mechanism: Kills or inhibits microorganisms on living tissue
-- Requires: Topical application to affected area
-- Cannot be: Oral, injectable (systemic toxicity)
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175486', 
    'Antiseptic [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Antiseptics require direct application to tissue surfaces. Oral ingestion would cause GI toxicity.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175486', 
    'Antiseptic [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Antiseptics require direct tissue contact. Oral liquids would cause GI toxicity.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000175486', 
    'Antiseptic [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Antiseptics require direct tissue contact. Injectable administration would cause systemic toxicity.'
FROM @InjectableForms;


-- ============================================================
-- CATEGORY 6: SPECIAL CASES
-- Vaccines, Skin Tests, and other specialized exclusions
-- ============================================================

/**************************************************************/
-- SKIN TEST ANTIGEN [EPC] - N0000184316
-- Mechanism: Intradermal injection to assess immune response
-- Requires: Intradermal injection
-- Cannot be: Oral, topical
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184316', 
    'Skin Test Antigen [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Skin test antigens require intradermal injection to assess cell-mediated immunity. Oral forms cannot deliver the test mechanism.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184316', 
    'Skin Test Antigen [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Skin test antigens require intradermal injection. Oral liquids cannot elicit the required local immune response.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184316', 
    'Skin Test Antigen [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Skin test antigens require intradermal injection. Topical application cannot assess cell-mediated immunity.'
FROM @TopicalForms;


/**************************************************************/
-- TUBERCULOSIS SKIN TEST [EPC] - N0000184315
-- Mechanism: Intradermal injection of tuberculin PPD
-- Requires: Intradermal injection (Mantoux method)
-- Cannot be: Oral, topical
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184315', 
    'Tuberculosis Skin Test [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Tuberculosis skin tests (Mantoux) require intradermal injection. Oral forms cannot elicit the delayed hypersensitivity response.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184315', 
    'Tuberculosis Skin Test [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Tuberculosis skin tests require intradermal injection. Oral liquids cannot perform diagnostic assessment.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000184315', 
    'Tuberculosis Skin Test [EPC]', 
    FormCode, 
    FormName,
    'INJECTABLE_ONLY',
    'Tuberculosis skin tests require intradermal injection. Topical application cannot assess TB immunity.'
FROM @TopicalForms;


/**************************************************************/
-- ECTOPARASITICIDE [EPC] - N0000194050
-- Mechanism: Kills ectoparasites (lice, scabies) on skin/hair
-- Requires: Topical application
-- Cannot be: Oral, injectable
/**************************************************************/
INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000194050', 
    'Ectoparasiticide [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Ectoparasiticides must be applied topically to kill parasites on skin/hair surface. Oral forms cannot reach external parasites.'
FROM @OralSolidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000194050', 
    'Ectoparasiticide [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Ectoparasiticides must contact external parasites directly. Oral liquids cannot reach skin surface parasites.'
FROM @OralLiquidForms;

INSERT INTO dbo.PharmClassDosageFormExclusion 
    (ClassCode, ClassDisplayName, ExcludedFormCode, ExcludedFormDisplayName, ExclusionCategory, Reason)
SELECT 
    'N0000194050', 
    'Ectoparasiticide [EPC]', 
    FormCode, 
    FormName,
    'TOPICAL_ONLY',
    'Ectoparasiticides require topical contact with parasites. Injectable forms cannot eliminate external parasites.'
FROM @InjectableForms;


-- ============================================================
-- VERIFY DATA LOAD
-- ============================================================
SELECT 
    ExclusionCategory,
    COUNT(*) AS ExclusionCount
FROM dbo.PharmClassDosageFormExclusion
GROUP BY ExclusionCategory
ORDER BY ExclusionCategory;

SELECT COUNT(*) AS TotalExclusions FROM dbo.PharmClassDosageFormExclusion;

GO


/**************************************************************/
-- UPDATED VIEW: vw_PharmacologicClassByActiveIngredient
-- 
-- Now includes exclusion logic based on dosage form compatibility
-- 
-- Changes from original:
-- 1. REMOVED: The global NOT EXISTS check against vw_InactiveIngredients
-- 2. RETAINED: The i.ClassCode <> 'IACT' filter (per-product active check)
-- 3. ADDED: NOT EXISTS check against PharmClassDosageFormExclusion
/**************************************************************/

-- Drop extended property if exists
IF EXISTS (
    SELECT 1 FROM sys.extended_properties ep
    INNER JOIN sys.views v ON ep.major_id = v.object_id
    WHERE v.name = 'vw_PharmacologicClassByActiveIngredient' 
      AND v.schema_id = SCHEMA_ID('dbo')
      AND ep.name = 'MS_Description'
)
BEGIN
    EXEC sys.sp_dropextendedproperty 
        @name = N'MS_Description', 
        @level0type = N'SCHEMA', @level0name = N'dbo', 
        @level1type = N'VIEW', @level1name = N'vw_PharmacologicClassByActiveIngredient';
END
GO

-- Drop view if exists
IF OBJECT_ID('dbo.vw_PharmacologicClassByActiveIngredient', 'V') IS NOT NULL
    DROP VIEW dbo.vw_PharmacologicClassByActiveIngredient;
GO

CREATE VIEW [dbo].[vw_PharmacologicClassByActiveIngredient]
AS
SELECT 
    pc.PharmacologicClassID, 
    pc.ClassCode AS PharmClassCode, 
    pc.ClassDisplayName AS PharmClassName, 
    am.ActiveMoietyID, 
    am.MoietyUNII, 
    am.MoietyName, 
    ins.IngredientSubstanceID, 
    ins.UNII AS SubstanceUNII, 
    ins.SubstanceName, 
    p.ProductID, 
    p.ProductName, 
    p.FormCode AS DosageFormCode, 
    p.FormDisplayName AS DosageFormName, 
    i.ClassCode AS IngredientClassCode,
    d.DocumentID, 
    d.DocumentGUID, 
    d.SetGUID, 
    d.VersionNumber, 
    d.Title AS DocumentTitle, 
    d.EffectiveTime AS LabelEffectiveDate
FROM dbo.PharmacologicClass AS pc 
INNER JOIN dbo.PharmacologicClassLink AS pcl 
    ON pc.PharmacologicClassID = pcl.PharmacologicClassID 
INNER JOIN dbo.IngredientSubstance AS ams 
    ON pcl.ActiveMoietySubstanceID = ams.IngredientSubstanceID 
INNER JOIN dbo.ActiveMoiety AS am 
    ON ams.IngredientSubstanceID = am.IngredientSubstanceID 
INNER JOIN dbo.IngredientSubstance AS ins 
    ON am.IngredientSubstanceID = ins.IngredientSubstanceID 
INNER JOIN dbo.Ingredient AS i 
    ON ins.IngredientSubstanceID = i.IngredientSubstanceID 
    AND i.ClassCode <> 'IACT'  -- KEY: Filters to active ingredients for THIS product
INNER JOIN dbo.Product AS p 
    ON i.ProductID = p.ProductID 
INNER JOIN dbo.Section AS s 
    ON p.SectionID = s.SectionID 
INNER JOIN dbo.StructuredBody AS sb 
    ON s.StructuredBodyID = sb.StructuredBodyID 
INNER JOIN dbo.[Document] AS d 
    ON sb.DocumentID = d.DocumentID
WHERE 
    -- Exclude pharmacologic class/dosage form combinations that are illogical
    NOT EXISTS (
        SELECT 1 
        FROM dbo.PharmClassDosageFormExclusion AS ex
        WHERE ex.ClassCode = pc.ClassCode 
          AND ex.ExcludedFormCode = p.FormDisplayName  -- Using FormDisplayName to match exclusion data
          AND ex.IsActive = 1
    );
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Pharmacologic classes linked to products via active ingredients only, with 
               dosage form compatibility filtering. Excludes class associations where the 
               dosage form is incompatible with the class mechanism of action (e.g., 
               osmotic laxative class excluded from injectable formulations).', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'VIEW', @level1name = N'vw_PharmacologicClassByActiveIngredient';
GO