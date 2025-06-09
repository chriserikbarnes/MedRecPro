
-- SQL Server Script for SPL Database based on ICH7 SPL Implementation Guide (December 2023)
-- Target Version: SQL Server 2016
-- Schema: dbo
-- Constraints: Primary Keys only (Integer IDENTITY), No Foreign Keys enforced.
-- Description: Creates tables for storing Structured Product Labeling data.

-- #############################################################################
-- Chunk 1: Core Document and Header Information
-- #############################################################################

PRINT 'Creating Core Document and Header Tables...';
GO

-- ============================================================================
-- Table: Document
-- Purpose: Stores the main metadata for each SPL document version. Based on Section 2.1.3.
-- ============================================================================
IF OBJECT_ID('dbo.Document', 'U') IS NOT NULL
    DROP TABLE dbo.Document;
GO

CREATE TABLE dbo.Document (
    DocumentID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentGUID UNIQUEIDENTIFIER NULL, -- Corresponds to <id root> 
    DocumentCode VARCHAR(50) NULL,      -- Corresponds to <code> code 
    DocumentCodeSystem VARCHAR(100) NULL, -- Corresponds to <code> codeSystem 
    DocumentDisplayName VARCHAR(255) NULL, -- Corresponds to <code> displayName 
    Title NVARCHAR(MAX) NULL,                 -- Corresponds to <title> 
    EffectiveTime DATETIME2(0) NULL,      -- Corresponds to <effectiveTime value> 
    SetGUID UNIQUEIDENTIFIER NULL,        -- Corresponds to <setId root> 
    VersionNumber INT  NULL,               -- Corresponds to <versionNumber value> 
    SubmissionFileName VARCHAR(255) NULL      -- Added for tracking the source XML file name 
);
GO

-- Add Comments and Extended Properties for Document table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the main metadata for each SPL document version. Based on Section 2.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document';
GO
-- DocumentGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Globally Unique Identifier for this specific document version (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentGUID';
GO
-- DocumentCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LOINC code identifying the document type (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCode';
GO
-- DocumentCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the document type code (<code> codeSystem), typically 2.16.840.1.113883.6.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCodeSystem';
GO
-- DocumentDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the document type code (<code> displayName).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentDisplayName';
GO
-- Title
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document title (<title>), if provided.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'Title';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'Title';
GO
-- EffectiveTime
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date reference for the SPL version (<effectiveTime value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.11' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
-- SetGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Globally Unique Identifier for the document set, constant across versions (<setId root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SetGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SetGUID';
GO
-- VersionNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sequential integer for the document version (<versionNumber value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'VersionNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 13, Para 2.1.3.15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'VersionNumber';
GO
-- SubmissionFileName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the submitted XML file (e.g., DocumentGUID.xml).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SubmissionFileName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.2.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SubmissionFileName';
GO

-- ============================================================================
-- Table: Organization
-- Purpose: Stores information about organizations (e.g., labelers, registrants, establishments). Based on Section 2.1.4, 2.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.Organization', 'U') IS NOT NULL
    DROP TABLE dbo.Organization;
GO

CREATE TABLE dbo.Organization (
    OrganizationID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationName VARCHAR(500) NULL, -- Corresponds to <name> 
    DUNSNumber VARCHAR(9) NULL,        -- Corresponds to <id extension> where root is DUNS OID 
    FEINumber VARCHAR(10) NULL,        -- Corresponds to <id extension> where root is FEI OID 
    IsConfidential BIT NOT NULL DEFAULT 0 -- Corresponds to <confidentialityCode> [cite: 49, 608]
);
GO

-- Add Comments and Extended Properties for Organization table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores information about organizations (e.g., labelers, registrants, establishments). Based on Section 2.1.4, 2.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14, 15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization';
GO
-- OrganizationName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the organization (<name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'OrganizationName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.5.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'OrganizationName';
GO
-- DUNSNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dun and Bradstreet Number (<id extension="DUNS Number" root="1.3.6.1.4.1.519.1">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'DUNSNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15, Para 2.1.5.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'DUNSNumber';
GO
-- FEINumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FDA Establishment Identifier (<id extension="FDA establishment identifier" root="2.16.840.1.113883.4.82">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'FEINumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'FEINumber';
GO
-- IsConfidential
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the organization information is confidential (<confidentialityCode code="B">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'IsConfidential';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 11, Para 2.1.1.10, 2.1.1.11; Page 77 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'IsConfidential';
GO


-- ============================================================================
-- Table: Address
-- Purpose: Stores address information for organizations or contact parties. Based on Section 2.1.6.
-- ============================================================================
IF OBJECT_ID('dbo.Address', 'U') IS NOT NULL
    DROP TABLE dbo.Address;
GO

CREATE TABLE dbo.Address (
    AddressID INT IDENTITY(1,1) PRIMARY KEY,
    StreetAddressLine1 VARCHAR(500) NULL, -- Corresponds to <streetAddressLine> 
    StreetAddressLine2 VARCHAR(500) NULL,    -- Corresponds to optional second <streetAddressLine> 
    City VARCHAR(100) NULL,              -- Corresponds to <city> 
    StateProvince VARCHAR(100) NULL,          -- Corresponds to <state>, required for USA 
    PostalCode VARCHAR(20) NULL,             -- Corresponds to <postalCode>, required for USA 
    CountryCode CHAR(3) NULL,            -- Corresponds to <country code> (ISO 3-letter) 
    CountryName VARCHAR(100) NULL        -- Corresponds to <country> name 
);
GO

-- Add Comments and Extended Properties for Address table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores address information for organizations or contact parties. Based on Section 2.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address';
GO
-- StreetAddressLine1
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'First line of the street address (<streetAddressLine>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine1';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine1';
GO
-- StreetAddressLine2
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Second line of the street address (<streetAddressLine>), optional.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine2';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine2';
GO
-- City
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'City name (<city>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'City';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'City';
GO
-- StateProvince
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'State or province (<state>), required if country is USA.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StateProvince';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, Para 2.1.6.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StateProvince';
GO
-- PostalCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Postal or ZIP code (<postalCode>), required if country is USA.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'PostalCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, Para 2.1.6.4, 2.1.6.5, 2.1.6.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'PostalCode';
GO
-- CountryCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ISO 3166-1 alpha-3 country code (<country code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryCode';
GO
-- CountryName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Full country name (<country> name).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryName';
GO

-- ============================================================================
-- Table: Telecom
-- Purpose: Stores telecommunication details (phone, email, fax) for organizations or contact parties. Based on Section 2.1.7.
-- Note: Storing different types in one table for simplicity, linked via junction tables later.
-- ============================================================================
IF OBJECT_ID('dbo.Telecom', 'U') IS NOT NULL
    DROP TABLE dbo.Telecom;
GO

CREATE TABLE dbo.Telecom (
    TelecomID INT IDENTITY(1,1) PRIMARY KEY,
    TelecomType VARCHAR(10) NULL, -- 'tel', 'mailto', 'fax' [cite: 86, 89]
    TelecomValue VARCHAR(500) NULL -- The actual phone number, email address, or fax number [cite: 86, 89]
);
GO

-- Add Comments and Extended Properties for Telecom table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores telecommunication details (phone, email, fax) for organizations or contact parties. Based on Section 2.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom';
GO
-- TelecomType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of telecommunication: "tel", "mailto", or "fax".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, 18, 19, Para 2.1.7.2, 2.1.7.11, 2.1.7.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomType';
GO
-- TelecomValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The telecommunication value, prefixed with type (e.g., "tel:+1-...", "mailto:...", "fax:+1-...").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, 18, 19, Para 2.1.7.2, 2.1.7.11, 2.1.7.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomValue';
GO

-- ============================================================================
-- Table: ContactPerson
-- Purpose: Stores contact person details. Based on Section 2.1.8.
-- ============================================================================
IF OBJECT_ID('dbo.ContactPerson', 'U') IS NOT NULL
    DROP TABLE dbo.ContactPerson;
GO

CREATE TABLE dbo.ContactPerson (
    ContactPersonID INT IDENTITY(1,1) PRIMARY KEY,
    ContactPersonName VARCHAR(500) NULL -- Corresponds to <contactPerson><name> 
);
GO

-- Add Comments and Extended Properties for ContactPerson table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores contact person details. Based on Section 2.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson';
GO
-- ContactPersonName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the contact person (<contactPerson><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson', @level2type=N'COLUMN',@level2name=N'ContactPersonName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson', @level2type=N'COLUMN',@level2name=N'ContactPersonName';
GO


-- ============================================================================
-- Table: ContactParty
-- Purpose: Represents the <contactParty> element, linking Organization, Address, Telecom, and ContactPerson. Based on Section 2.1.8.
-- ============================================================================
IF OBJECT_ID('dbo.ContactParty', 'U') IS NOT NULL
    DROP TABLE dbo.ContactParty;
GO

CREATE TABLE dbo.ContactParty (
    ContactPartyID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationID INT NULL,       -- Link to the Organization this contact party belongs to
    AddressID INT NULL,          -- Link to the Address table 
    ContactPersonID INT NULL     -- Link to the ContactPerson table 
    -- Telecom links will be in a junction table: ContactPartyTelecom
);
GO

-- Add Comments and Extended Properties for ContactParty table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the <contactParty> element, linking Organization, Address, Telecom, and ContactPerson. Based on Section 2.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty';
GO
-- OrganizationID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the Organization this contact party belongs to.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'OrganizationID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'OrganizationID';
GO
-- AddressID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the Address for this contact party.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'AddressID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'AddressID';
GO
-- ContactPersonID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the ContactPerson for this contact party.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'ContactPersonID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'ContactPersonID';
GO

-- ============================================================================
-- Table: ContactPartyTelecom
-- Purpose: Junction table to link ContactParty with multiple Telecom entries.
-- ============================================================================
IF OBJECT_ID('dbo.ContactPartyTelecom', 'U') IS NOT NULL
    DROP TABLE dbo.ContactPartyTelecom;
GO

CREATE TABLE dbo.ContactPartyTelecom (
    ContactPartyTelecomID INT IDENTITY(1,1) PRIMARY KEY,
    ContactPartyID INT  NULL, -- FK to ContactParty
    TelecomID INT  NULL       -- FK to Telecom
);
GO

-- Add Comments and Extended Properties for ContactPartyTelecom table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Junction table to link ContactParty with multiple Telecom entries (typically tel and mailto ).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPartyTelecom';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPartyTelecom';
GO

-- ============================================================================
-- Table: OrganizationTelecom
-- Purpose: Junction table to link Organizations directly with Telecom entries (e.g., US Agent ).
-- ============================================================================
IF OBJECT_ID('dbo.OrganizationTelecom', 'U') IS NOT NULL
    DROP TABLE dbo.OrganizationTelecom;
GO

CREATE TABLE dbo.OrganizationTelecom (
    OrganizationTelecomID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationID INT  NULL, -- FK to Organization
    TelecomID INT  NULL       -- FK to Telecom
);
GO

-- Add Comments and Extended Properties for OrganizationTelecom table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Junction table to link Organizations directly with Telecom entries (e.g., for US Agents or facility phones without a full ContactParty).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationTelecom';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationTelecom';
GO


-- ============================================================================
-- Table: DocumentAuthor
-- Purpose: Links Documents to Authoring Organizations (typically the Labeler). Based on Section 2.1.4.
-- ============================================================================
IF OBJECT_ID('dbo.DocumentAuthor', 'U') IS NOT NULL
    DROP TABLE dbo.DocumentAuthor;
GO

CREATE TABLE dbo.DocumentAuthor (
    DocumentAuthorID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentID INT  NULL,     -- FK to Document
    OrganizationID INT  NULL, -- FK to Organization (the authoring org, e.g., Labeler )
    AuthorType VARCHAR(50) NULL DEFAULT 'Labeler' -- e.g., 'Labeler', 'FDA', 'NCPDP' [cite: 786, 946, 995]
);
GO

-- Add Comments and Extended Properties for DocumentAuthor table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links Documents to Authoring Organizations (typically the Labeler). Based on Section 2.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor';
GO
EXEC sys.sp_addextendedproperty @name=N'AuthorType', @value=N'Identifies the type or role of the author, e.g., Labeler (4.1.2 ), FDA (8.1.2, 15.1.2, 20.1.2, 21.1.2, 30.1.2, 31.1.2, 32.1.2, 33.1.2 ), NCPDP (12.1.2 ).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor', @level2type=N'COLUMN',@level2name=N'AuthorType';
GO

-- ============================================================================
-- Table: RelatedDocument
-- Purpose: Stores references to other documents (e.g., Core Document, Predecessor, Reference Labeling). Based on Section 2.1.10, 2.1.11, 15.1.3, 20.1.3, 21.1.3, 23.1.3, 30.1.3, 31.1.5, 32.1.3, 33.1.3.
-- ============================================================================
IF OBJECT_ID('dbo.RelatedDocument', 'U') IS NOT NULL
    DROP TABLE dbo.RelatedDocument;
GO

CREATE TABLE dbo.RelatedDocument (
    RelatedDocumentID INT IDENTITY(1,1) PRIMARY KEY,
    SourceDocumentID INT  NULL,           -- FK to Document (The document containing the reference)
    RelationshipTypeCode VARCHAR(10) NULL, -- e.g., 'APND', 'RPLC', 'DRIV', 'XCRPT', 'SUBJ' [cite: 111, 122, 1032, 1299, 1333, 1365, 1557, 1601, 1609, 1645]
    ReferencedSetGUID UNIQUEIDENTIFIER NULL, -- Corresponds to <setId root> of the referenced document [cite: 112, 121, 1032, 1299, 1333, 1366, 1558, 1602, 1609, 1645]
    ReferencedDocumentGUID UNIQUEIDENTIFIER NULL, -- Corresponds to <id root> of the referenced document (used in RPLC) 
    ReferencedVersionNumber INT NULL,          -- Corresponds to <versionNumber value> of the referenced document [cite: 114, 121]
    ReferencedDocumentCode VARCHAR(50) NULL,   -- Corresponds to <code> code of the referenced document (used in RPLC) 
    ReferencedDocumentCodeSystem VARCHAR(100) NULL, -- Corresponds to <code> codeSystem of the referenced document (used in RPLC) 
    ReferencedDocumentDisplayName VARCHAR(255) NULL -- Corresponds to <code> displayName of the referenced document (used in RPLC) 
);
GO

-- Add Comments and Extended Properties for RelatedDocument table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to other documents (e.g., Core Document, Predecessor, Reference Labeling[cite: 1031, 1298, 1332, 1608, 1644], Subject [cite: 1363, 1556, 1599]). Based on Sections 2.1.10, 2.1.11, 15.1.3, 20.1.3, 21.1.3, 23.1.3, 30.1.3, 31.1.5, 32.1.3, 33.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, 21, 154, 193, 199, 205, 235, 242, 245, 250' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument';
GO
-- RelationshipTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of relationship (e.g., APND for core doc, RPLC for predecessor, DRIV for reference labeling, SUBJ for subject, XCRPT for excerpt).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'RelationshipTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21, Page 154, Page 193, Page 199, Page 205, Page 236, Page 242, Page 245, Page 250 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'RelationshipTypeCode';
GO
-- ReferencedSetGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Set GUID of the related/referenced document (<setId root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedSetGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21, Page 154, Page 193, Page 199, Page 205, Page 236, Page 242, Page 245, Page 250 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedSetGUID';
GO
-- ReferencedDocumentGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document GUID of the related/referenced document (<id root>), used for RPLC relationship.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 21, Para 2.1.11.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentGUID';
GO
-- ReferencedVersionNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Version number of the related/referenced document (<versionNumber value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedVersionNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedVersionNumber';
GO
-- ReferencedDocumentCode, ReferencedDocumentCodeSystem, ReferencedDocumentDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document type code, system, and display name of the related/referenced document (<code>), used for RPLC relationship.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 21 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentCode';
GO

-- ============================================================================
-- Table: DocumentRelationship
-- Purpose: Defines relationships between organizations within a document header (e.g., Labeler -> Registrant -> Establishment). Based on Section 2.1.4, 4.1.2, 4.1.3, 4.1.4, 5.1.3, 6.1.2, 6.1.3, 13.1.2, 13.1.3, 18.1.2, 18.1.3, 35.1.4, 36.1.3, 36.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.DocumentRelationship', 'U') IS NOT NULL
    DROP TABLE dbo.DocumentRelationship;
GO

CREATE TABLE dbo.DocumentRelationship (
    DocumentRelationshipID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentID INT  NULL,         -- FK to Document
    ParentOrganizationID INT NULL,   -- FK to Organization (e.g., Labeler)
    ChildOrganizationID INT  NULL,    -- FK to Organization (e.g., Registrant or Establishment)
    RelationshipType VARCHAR(50) NULL, -- e.g., 'LabelerToRegistrant', 'RegistrantToEstablishment', 'EstablishmentToUSagent', 'EstablishmentToImporter', 'LabelerToDetails', 'FacilityToParentCompany', 'LabelerToParentCompany'
    RelationshipLevel INT  NULL     -- Indicates the level in the hierarchy (e.g., 1 for Labeler, 2 for Registrant, 3 for Establishment)
);
GO

-- Add Comments and Extended Properties for DocumentRelationship table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines hierarchical relationships between organizations within a document header (e.g., Labeler -> Registrant -> Establishment).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship';
GO
EXEC sys.sp_addextendedproperty @name=N'RelationshipType', @value=N'Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3 ), RegistrantToEstablishment (4.1.4 ), EstablishmentToUSagent (6.1.4 ), EstablishmentToImporter (6.1.5 ), LabelerToDetails (5.1.3 ), FacilityToParentCompany (35.1.6 ), LabelerToParentCompany (36.1.2.5 ).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship', @level2type=N'COLUMN',@level2name=N'RelationshipType';
GO

PRINT 'Chunk 1 complete.';
GO
-- #############################################################################
-- Chunk 2: SPL Body, Sections, and Text Content
-- #############################################################################

PRINT 'Creating SPL Body, Section, and Text Content Tables...';
GO

-- ============================================================================
-- Table: StructuredBody
-- Purpose: Represents the main <structuredBody> container within a Document. Based on Section 2.2.
-- ============================================================================
IF OBJECT_ID('dbo.StructuredBody', 'U') IS NOT NULL
    DROP TABLE dbo.StructuredBody;
GO

CREATE TABLE dbo.StructuredBody (
    StructuredBodyID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentID INT  NULL -- FK to Document
);
GO

-- Add Comments and Extended Properties for StructuredBody table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the main <structuredBody> container within a Document. Based on Section 2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructuredBody';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructuredBody';
GO

-- ============================================================================
-- Table: Section
-- Purpose: Stores details for each <section> within the StructuredBody. Based on Section 2.2.1.
-- ============================================================================
IF OBJECT_ID('dbo.Section', 'U') IS NOT NULL
    DROP TABLE dbo.Section;
GO

CREATE TABLE dbo.Section (
    SectionID INT IDENTITY(1,1) PRIMARY KEY,
    StructuredBodyID INT  NULL,            -- FK to StructuredBody (for top-level sections)
    SectionGUID UNIQUEIDENTIFIER NULL,    -- Corresponds to <id root>
    SectionCode VARCHAR(50) NULL,         -- Corresponds to <code> code (LOINC)
    SectionCodeSystem VARCHAR(100) NULL,  -- Corresponds to <code> codeSystem
    SectionDisplayName VARCHAR(255) NULL, -- Corresponds to <code> displayName
    Title NVARCHAR(MAX) NULL,                 -- Corresponds to <title>
    EffectiveTime DATETIME2(0) NULL       -- Corresponds to <effectiveTime value>
);
GO

-- Add Comments and Extended Properties for Section table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details for each <section> within the StructuredBody. Based on Section 2.2.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section';
GO
-- SectionGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for the section (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionGUID';
GO
-- SectionCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LOINC code for the section type (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22, Para 2.2.1.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCode';
GO
-- SectionCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the section code (<code> codeSystem), typically 2.16.840.1.113883.6.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCodeSystem';
GO
-- SectionDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the section code (<code> displayName).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionDisplayName';
GO
-- Title
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Title of the section (<title>), may include numbering.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'Title';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'Title';
GO
-- EffectiveTime
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Effective time for the section (<effectiveTime value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO

-- ============================================================================
-- Table: SectionHierarchy
-- Purpose: Manages the nested structure (parent-child relationships) of sections. Based on Section 2.2.1.
-- ============================================================================
IF OBJECT_ID('dbo.SectionHierarchy', 'U') IS NOT NULL
    DROP TABLE dbo.SectionHierarchy;
GO

CREATE TABLE dbo.SectionHierarchy (
    SectionHierarchyID INT IDENTITY(1,1) PRIMARY KEY,
    ParentSectionID INT  NULL, -- FK to Section (The parent section)
    ChildSectionID INT  NULL,  -- FK to Section (The child/nested section)
    SequenceNumber INT  NULL   -- Order of the child section within the parent
);
GO

-- Add Comments and Extended Properties for SectionHierarchy table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Manages the nested structure (parent-child relationships) of sections using <component><section>. Based on Section 2.2.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionHierarchy';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 23' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionHierarchy';
GO

-- ============================================================================
-- Table: SectionTextContent
-- Purpose: Stores the main content blocks (<paragraph>, <list>, <table>) within a section's <text> element. Based on Section 2.2.2.
-- ============================================================================
IF OBJECT_ID('dbo.SectionTextContent', 'U') IS NOT NULL
    DROP TABLE dbo.SectionTextContent;
GO

CREATE TABLE dbo.SectionTextContent (
    SectionTextContentID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,             -- FK to Section
    ContentType VARCHAR(20) NULL,   -- 'Paragraph', 'List', 'Table', 'BlockImage'
    SequenceNumber INT  NULL,        -- Order of content block within the section <text>
    ContentText NVARCHAR(MAX) NULL      -- Stores paragraph text directly. List/Table details in separate tables.
);
GO

-- Add Comments and Extended Properties for SectionTextContent table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the main content blocks (<paragraph>, <list>, <table>, block <renderMultimedia>) within a section''s <text> element. Based on Section 2.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent';
GO
-- ContentType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of content block: Paragraph, List, Table, BlockImage (for <renderMultimedia> as direct child of <text>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentType';
GO
-- SequenceNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Order of this content block within the parent section''s <text> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'SequenceNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22, Para referencing order' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'SequenceNumber';
GO
-- ContentText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Actual text for Paragraphs. For List/Table types, details are in related tables. Inline markup (bold, italic, links etc) handled separately.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentText';
GO


-- ============================================================================
-- Table: TextList
-- Purpose: Stores details specific to <list> elements. Based on Section 2.2.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.TextList', 'U') IS NOT NULL
    DROP TABLE dbo.TextList;
GO

CREATE TABLE dbo.TextList (
    TextListID INT IDENTITY(1,1) PRIMARY KEY,
    SectionTextContentID INT  NULL, -- FK to SectionTextContent (where ContentType='List')
    ListType VARCHAR(20) NULL,   -- 'ordered' or 'unordered'
    StyleCode VARCHAR(50) NULL     -- e.g., 'BigRoman', 'LittleAlpha', 'Disc', 'Circle', 'Square'
);
GO

-- Add Comments and Extended Properties for TextList table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details specific to <list> elements. Based on Section 2.2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList';
GO
-- ListType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Attribute identifying the list as ordered or unordered (<list listType=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'ListType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'ListType';
GO
-- StyleCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional style code for numbering/bullet style (<list styleCode=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO

-- ============================================================================
-- Table: TextListItem
-- Purpose: Stores individual <item> elements within a <list>. Based on Section 2.2.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.TextListItem', 'U') IS NOT NULL
    DROP TABLE dbo.TextListItem;
GO

CREATE TABLE dbo.TextListItem (
    TextListItemID INT IDENTITY(1,1) PRIMARY KEY,
    TextListID INT  NULL,        -- FK to TextList
    SequenceNumber INT  NULL,    -- Order of the item within the list
    ItemCaption NVARCHAR(100) NULL, -- Optional custom marker from <caption> within <item>
    ItemText NVARCHAR(MAX) NULL -- Text content of the list item
);
GO

-- Add Comments and Extended Properties for TextListItem table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <item> elements within a <list>. Based on Section 2.2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem';
GO
-- ItemCaption
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional custom marker specified using <caption> within <item>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemCaption';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27-28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemCaption';
GO
-- ItemText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content of the list item <item>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemText';
GO

-- ============================================================================
-- Table: TextTable
-- Purpose: Stores details specific to <table> elements. Based on Section 2.2.2.5.
-- ============================================================================
IF OBJECT_ID('dbo.TextTable', 'U') IS NOT NULL
    DROP TABLE dbo.TextTable;
GO

CREATE TABLE dbo.TextTable (
    TextTableID INT IDENTITY(1,1) PRIMARY KEY,
    SectionTextContentID INT  NULL, -- FK to SectionTextContent (where ContentType='Table')
    Width VARCHAR(20) NULL,           -- Optional width attribute on <table>
    HasHeader BIT NOT NULL DEFAULT 0,   -- Flag indicating if <thead> exists
    HasFooter BIT NOT NULL DEFAULT 0    -- Flag indicating if <tfoot> exists
    -- Colgroup/Col details might need separate tables if complex formatting is required
);
GO

-- Add Comments and Extended Properties for TextTable table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details specific to <table> elements. Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable';
GO
-- Width
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional width attribute specified on the <table> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'Width';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'Width';
GO
-- HasHeader
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the table included a <thead> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasHeader';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasHeader';
GO
-- HasFooter
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the table included a <tfoot> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasFooter';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasFooter';
GO

-- ============================================================================
-- Table: TextTableRow
-- Purpose: Stores individual <tr> elements within a <table> (header, body, or footer). Based on Section 2.2.2.5.
-- ============================================================================
IF OBJECT_ID('dbo.TextTableRow', 'U') IS NOT NULL
    DROP TABLE dbo.TextTableRow;
GO

CREATE TABLE dbo.TextTableRow (
    TextTableRowID INT IDENTITY(1,1) PRIMARY KEY,
    TextTableID INT  NULL,        -- FK to TextTable
    RowGroupType VARCHAR(10) NULL, -- 'Header', 'Body', 'Footer' (corresponding to thead, tbody, tfoot)
    SequenceNumber INT  NULL,     -- Order of the row within its group (thead, tbody, tfoot)
    StyleCode VARCHAR(100) NULL      -- Optional styleCode attribute on <tr> (e.g., 'Botrule')
);
GO

-- Add Comments and Extended Properties for TextTableRow table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <tr> elements within a <table> (header, body, or footer). Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow';
GO
-- StyleCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute on <tr> (e.g., Botrule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO

-- ============================================================================
-- Table: TextTableCell
-- Purpose: Stores individual <td> or <th> elements within a <tr>. Based on Section 2.2.2.5.
-- ============================================================================
IF OBJECT_ID('dbo.TextTableCell', 'U') IS NOT NULL
    DROP TABLE dbo.TextTableCell;
GO

CREATE TABLE dbo.TextTableCell (
    TextTableCellID INT IDENTITY(1,1) PRIMARY KEY,
    TextTableRowID INT  NULL,        -- FK to TextTableRow
    CellType VARCHAR(5) NULL,     -- 'td' or 'th'
    SequenceNumber INT  NULL,      -- Order of the cell within the row (column number)
    CellText NVARCHAR(MAX) NULL,  -- Text content of the cell
    RowSpan INT NULL,                 -- Optional rowspan attribute
    ColSpan INT NULL,                 -- Optional colspan attribute
    StyleCode VARCHAR(100) NULL,      -- Optional styleCode attribute (e.g., 'Lrule', 'Rrule', 'Toprule', 'Botrule')
    Align VARCHAR(10) NULL,           -- Optional align attribute ('left', 'center', 'right', 'justify', 'char')
    VAlign VARCHAR(10) NULL           -- Optional valign attribute ('top', 'middle', 'bottom', 'baseline')
);
GO

-- Add Comments and Extended Properties for TextTableCell table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <td> or <th> elements within a <tr>. Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell';
GO
-- CellText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content of the table cell (<td> or <th>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'CellText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'CellText';
GO
-- RowSpan
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional rowspan attribute on <td> or <th>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'RowSpan';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'RowSpan';
GO
-- ColSpan
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional colspan attribute on <td> or <th>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'ColSpan';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'ColSpan';
GO
-- StyleCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute for cell rules (Lrule, Rrule, Toprule, Botrule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'StyleCode';
GO
-- Align
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional align attribute for horizontal alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'Align';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'Align';
GO
-- VAlign
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional valign attribute for vertical alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'VAlign';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'VAlign';
GO

-- ============================================================================
-- Table: ObservationMedia
-- Purpose: Stores metadata for images (<observationMedia>). Based on Section 2.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.ObservationMedia', 'U') IS NOT NULL
    DROP TABLE dbo.ObservationMedia;
GO

CREATE TABLE dbo.ObservationMedia (
    ObservationMediaID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,              -- FK to Section (where the observationMedia is defined)
    MediaID VARCHAR(100) NULL,       -- Corresponds to <observationMedia ID=> (used by renderMultimedia)
    DescriptionText NVARCHAR(MAX) NULL,  -- Corresponds to <text> child of observationMedia
    MediaType VARCHAR(50) NULL,      -- Corresponds to <value mediaType=> (e.g., 'image/jpeg')
    FileName VARCHAR(255) NULL       -- Corresponds to <reference value=>
);
GO
-- Add Comments and Extended Properties for ObservationMedia table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores metadata for images (<observationMedia>). Based on Section 2.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia';
GO
-- MediaID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier for the media object (<observationMedia ID=>), referenced by <renderMultimedia>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaID';
GO
-- DescriptionText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the image (<text> child of observationMedia), used by screen readers.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'DescriptionText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30-31' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'DescriptionText';
GO
-- MediaType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Media type of the file (<value mediaType=>), e.g., image/jpeg.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 31, Para 2.2.3.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaType';
GO
-- FileName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'File name of the image (<reference value=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'FileName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30, Para 2.2.3.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'FileName';
GO

-- ============================================================================
-- Table: RenderedMedia
-- Purpose: Represents the <renderMultimedia> tag, linking text content to an ObservationMedia entry. Based on Section 2.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.RenderedMedia', 'U') IS NOT NULL
    DROP TABLE dbo.RenderedMedia;
GO

CREATE TABLE dbo.RenderedMedia (
    RenderedMediaID INT IDENTITY(1,1) PRIMARY KEY,
    SectionTextContentID INT  NULL, -- FK to SectionTextContent (Paragraph or BlockImage)
    ObservationMediaID INT  NULL,   -- FK to ObservationMedia (The image to render)
    SequenceInContent INT  NULL,    -- Order if multiple images are in one content block
    IsInline BIT NOT NULL              -- True if <renderMultimedia> is child of <paragraph>, False if child of <text>
);
GO

-- Add Comments and Extended Properties for RenderedMedia table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the <renderMultimedia> tag, linking text content to an ObservationMedia entry. Based on Section 2.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia';
GO
-- ObservationMediaID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the ObservationMedia containing the image details, via the referencedObject attribute.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'ObservationMediaID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'ObservationMediaID';
GO
-- IsInline
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the image is inline (within a paragraph) or block level (direct child of <text>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'IsInline';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 31' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'IsInline';
GO

-- ============================================================================
-- Table: SectionExcerptHighlight
-- Purpose: Stores the highlight text within an excerpt for specific sections. Based on Section 2.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.SectionExcerptHighlight', 'U') IS NOT NULL
    DROP TABLE dbo.SectionExcerptHighlight;
GO

CREATE TABLE dbo.SectionExcerptHighlight (
    SectionExcerptHighlightID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,         -- FK to Section (The section containing the excerpt/highlight)
    HighlightText NVARCHAR(MAX) NULL -- Corresponds to the text within <excerpt><highlight><text>
);
GO

-- Add Comments and Extended Properties for SectionExcerptHighlight table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the highlight text within an excerpt for specific sections (e.g., Boxed Warning, Indications). Based on Section 2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 32' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight';
GO
-- HighlightText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content from <excerpt><highlight><text>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight', @level2type=N'COLUMN',@level2name=N'HighlightText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 32' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight', @level2type=N'COLUMN',@level2name=N'HighlightText';
GO


PRINT 'Chunk 2 complete.';
GO

-- #############################################################################
-- Chunk 3: General Product Data Elements (Section 3.1)
-- #############################################################################

PRINT 'Creating General Product Data Element Tables...';
GO

-- ============================================================================
-- Table: Product
-- Purpose: Stores core product information (<manufacturedProduct>). Based on Section 3.1.
-- ============================================================================
IF OBJECT_ID('dbo.Product', 'U') IS NOT NULL
    DROP TABLE dbo.Product;
GO

CREATE TABLE dbo.Product (
    ProductID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT NULL,               -- FK to Section (if product defined in a section)
    ProductName NVARCHAR(500) NULL,   -- Corresponds to <name> (proprietary name or product name)
    ProductSuffix NVARCHAR(100) NULL, -- Corresponds to <suffix>
    FormCode VARCHAR(50) NULL,        -- Corresponds to <formCode code> (Dosage Form or Kit)
    FormCodeSystem VARCHAR(100) NULL, -- Corresponds to <formCode codeSystem>
    FormDisplayName VARCHAR(255) NULL,-- Corresponds to <formCode displayName>
    DescriptionText NVARCHAR(512) NULL -- Corresponds to <desc> (mainly for devices)
);
GO

-- Add Comments and Extended Properties for Product table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores core product information (<manufacturedProduct>). Based on Section 3.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product';
GO
-- ProductName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Proprietary name or product name (<name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, Para 3.1.1.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductName';
GO
-- ProductSuffix
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Suffix to the proprietary name (<suffix>), e.g., "XR".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductSuffix';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductSuffix';
GO
-- FormCode, FormCodeSystem, FormDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage form code, system, and display name (<formCode>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'FormCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 39, 70 (3.2.1.20)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'FormCode';
GO
-- DescriptionText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Brief description of the product (<desc>), mainly used for devices.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'DescriptionText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'DescriptionText';
GO

-- ============================================================================
-- Table: ProductIdentifier
-- Purpose: Stores various types of identifiers associated with a product (Item Codes like NDC, GTIN, etc.). Based on Section 3.1.1.
-- ============================================================================
IF OBJECT_ID('dbo.ProductIdentifier', 'U') IS NOT NULL
    DROP TABLE dbo.ProductIdentifier;
GO

CREATE TABLE dbo.ProductIdentifier (
    ProductIdentifierID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,            -- FK to Product
    IdentifierValue VARCHAR(100) NULL, -- The actual code value (<code code=>)
    IdentifierSystemOID VARCHAR(100) NULL, -- OID for the code system (<code codeSystem=>)
    IdentifierType VARCHAR(50) NULL  -- e.g., 'NDCProduct', 'NHRICProduct', 'GS1', 'HIBCC', 'ISBTProduct', 'CosmeticListingNumber'
);
GO

-- Add Comments and Extended Properties for ProductIdentifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores various types of identifiers associated with a product (Item Codes like NDC, GTIN, etc.). Based on Section 3.1.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 39' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier';
GO
-- IdentifierValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The item code value (<code code=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 39, Para 3.1.1.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
-- IdentifierSystemOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the identifier system (<code codeSystem=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, Para 3.1.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO
-- IdentifierType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type classification of the identifier based on the OID.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, Para 3.1.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType';
GO

-- ============================================================================
-- Table: GenericMedicine
-- Purpose: Stores non-proprietary (generic) medicine names associated with a Product. Based on Section 3.1.1, 3.2.1.
-- ============================================================================
IF OBJECT_ID('dbo.GenericMedicine', 'U') IS NOT NULL
    DROP TABLE dbo.GenericMedicine;
GO

CREATE TABLE dbo.GenericMedicine (
    GenericMedicineID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                -- FK to Product
    GenericName NVARCHAR(512) NULL,    -- Corresponds to <genericMedicine><name>
    PhoneticName NVARCHAR(512) NULL        -- Corresponds to <genericMedicine><name use="PHON">
);
GO

-- Add Comments and Extended Properties for GenericMedicine table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores non-proprietary (generic) medicine names associated with a Product. Based on Section 3.1.1, 3.2.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 69' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine';
GO
-- GenericName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Non-proprietary name of the product (<genericMedicine><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'GenericName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 69, Para 3.2.1.24' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'GenericName';
GO
-- PhoneticName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Phonetic spelling of the generic name (<name use="PHON">), optional.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'PhoneticName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 70, Para 3.2.1.28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'PhoneticName';
GO


-- ============================================================================
-- Table: SpecializedKind
-- Purpose: Stores specialized kind information, like device classification or cosmetic category. Based on Section 3.1.1, 3.3.1, 3.4.3.
-- ============================================================================
IF OBJECT_ID('dbo.SpecializedKind', 'U') IS NOT NULL
    DROP TABLE dbo.SpecializedKind;
GO

CREATE TABLE dbo.SpecializedKind (
    SpecializedKindID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,             -- FK to Product
    KindCode VARCHAR(50) NULL,      -- Corresponds to <generalizedMaterialKind><code> code
    KindCodeSystem VARCHAR(100) NULL, -- Corresponds to <generalizedMaterialKind><code> codeSystem
    KindDisplayName VARCHAR(255) NULL -- Corresponds to <generalizedMaterialKind><code> displayName
);
GO

-- Add Comments and Extended Properties for SpecializedKind table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores specialized kind information, like device product classification or cosmetic category. Based on Section 3.1.1, 3.3.1, 3.4.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 89, 97' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind';
GO
-- KindCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the specialized kind (e.g., device product classification, cosmetic category).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.9), Page 97 (Para 3.4.3.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCode';
GO
-- KindCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the specialized kind code (typically 2.16.840.1.113883.6.303).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.8), Page 97 (Para 3.4.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCodeSystem';
GO
-- KindDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the specialized kind code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.10), Page 97 (Para 3.4.3.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindDisplayName';
GO

-- ============================================================================
-- Table: EquivalentEntity
-- Purpose: Stores relationships indicating equivalence to other products (e.g., product source, predecessor). Based on Section 3.1.2.
-- ============================================================================
IF OBJECT_ID('dbo.EquivalentEntity', 'U') IS NOT NULL
    DROP TABLE dbo.EquivalentEntity;
GO

CREATE TABLE dbo.EquivalentEntity (
    EquivalentEntityID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                     -- FK to Product (The product being described)
    EquivalenceCode VARCHAR(50) NULL,       -- Code indicating type of equivalence (e.g., 'C64637' for Same)
    EquivalenceCodeSystem VARCHAR(100) NULL,-- Code system for EquivalenceCode
    DefiningMaterialKindCode VARCHAR(100) NULL, -- Item code of the equivalent product (<definingMaterialKind><code> code)
    DefiningMaterialKindSystem VARCHAR(100) NULL -- Code system of the equivalent product code
);
GO

-- Add Comments and Extended Properties for EquivalentEntity table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores relationships indicating equivalence to other products (e.g., product source, predecessor). Based on Section 3.1.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, 41' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity';
GO
-- EquivalenceCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of equivalence relationship, e.g., C64637 (Same), pending (Predecessor).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'EquivalenceCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 41, Table 2 & Para 3.1.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'EquivalenceCode';
GO
-- DefiningMaterialKindCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Item code of the equivalent product (e.g., source NDC product code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, 41 (Para 3.1.2.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindCode';
GO
-- DefiningMaterialKindSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the equivalent product''s item code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindSystem';
GO


-- ============================================================================
-- Table: AdditionalIdentifier
-- Purpose: Stores additional product identifiers like Model Number, Catalog Number. Based on Section 3.1.3, 3.3.2.
-- ============================================================================
IF OBJECT_ID('dbo.AdditionalIdentifier', 'U') IS NOT NULL
    DROP TABLE dbo.AdditionalIdentifier;
GO

CREATE TABLE dbo.AdditionalIdentifier (
    AdditionalIdentifierID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                 -- FK to Product
    IdentifierTypeCode VARCHAR(50) NULL,   -- Code for the type of identifier (e.g., 'C99286' for Model Number)
    IdentifierTypeCodeSystem VARCHAR(100) NULL, -- Code system for IdentifierTypeCode
    IdentifierTypeDisplayName VARCHAR(255) NULL, -- Display name for IdentifierTypeCode
    IdentifierValue VARCHAR(255) NULL,   -- The actual identifier string (<id extension>)
    IdentifierRootOID VARCHAR(100) NULL    -- The root OID for the identifier (<id root>)
);
GO

-- Add Comments and Extended Properties for AdditionalIdentifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores additional product identifiers like Model Number, Catalog Number, Reference Number. Based on Section 3.1.3, 3.3.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, 90' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier';
GO
-- IdentifierTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the type of identifier (e.g., C99286 Model Number, C99285 Catalog Number, C99287 Reference Number).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43 (Table 3), Page 90 (Para 3.3.2.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierTypeCode';
GO
-- IdentifierValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The actual identifier value (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, Page 90 (Para 3.3.2.6)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
-- IdentifierRootOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID associated with the identifier (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierRootOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, Page 90 (Para 3.3.2.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierRootOID';
GO

-- ============================================================================
-- Table: IngredientSubstance
-- Purpose: Stores details about a unique substance (identified by UNII). Based on Section 3.1.4.
-- ============================================================================
IF OBJECT_ID('dbo.IngredientSubstance', 'U') IS NOT NULL
    DROP TABLE dbo.IngredientSubstance;
GO

CREATE TABLE dbo.IngredientSubstance (
    IngredientSubstanceID INT IDENTITY(1,1) PRIMARY KEY,
    UNII CHAR(10) NULL,                -- Unique Ingredient Identifier (<code code> where system is UNII)
    SubstanceName NVARCHAR(1000) NULL -- Name of the substance (<name>)
);
GO

-- Add Comments and Extended Properties for IngredientSubstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details about a unique substance (identified primarily by UNII). Based on Section 3.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance';
GO
-- UNII
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique Ingredient Identifier (<code code=> where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'UNII';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 45, Para 3.1.4.7, 3.1.4.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'UNII';
GO
-- SubstanceName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the substance (<name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 45, Para 3.1.4.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceName';
GO

-- ============================================================================
-- Table: ActiveMoiety
-- Purpose: Stores active moiety details linked to an IngredientSubstance. Based on Section 3.1.4, 3.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.ActiveMoiety', 'U') IS NOT NULL
    DROP TABLE dbo.ActiveMoiety;
GO

CREATE TABLE dbo.ActiveMoiety (
    ActiveMoietyID INT IDENTITY(1,1) PRIMARY KEY,
    IngredientSubstanceID INT  NULL, -- FK to IngredientSubstance (The parent substance)
    MoietyUNII CHAR(10) NULL,       -- UNII code of the active moiety (<activeMoiety><code> code)
    MoietyName NVARCHAR(1000) NULL  -- Name of the active moiety (<activeMoiety><name>)
);
GO

-- Add Comments and Extended Properties for ActiveMoiety table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores active moiety details linked to an IngredientSubstance. Based on Section 3.1.4, 3.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, 76' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety';
GO
-- MoietyUNII
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UNII code of the active moiety (<activeMoiety><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyUNII';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 76, Para 3.2.4.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyUNII';
GO
-- MoietyName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the active moiety (<activeMoiety><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 76, Para 3.2.4.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyName';
GO

-- ============================================================================
-- Table: ReferenceSubstance
-- Purpose: Stores reference substance details linked to an IngredientSubstance (used when BasisOfStrength='ReferenceIngredient'). Based on Section 3.1.4, 3.2.5.
-- ============================================================================
IF OBJECT_ID('dbo.ReferenceSubstance', 'U') IS NOT NULL
    DROP TABLE dbo.ReferenceSubstance;
GO

CREATE TABLE dbo.ReferenceSubstance (
    ReferenceSubstanceID INT IDENTITY(1,1) PRIMARY KEY,
    IngredientSubstanceID INT  NULL,     -- FK to IngredientSubstance (The parent substance)
    RefSubstanceUNII CHAR(10) NULL,     -- UNII code of the reference substance (<definingSubstance><code> code)
    RefSubstanceName NVARCHAR(1000) NULL -- Name of the reference substance (<definingSubstance><name>)
);
GO

-- Add Comments and Extended Properties for ReferenceSubstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores reference substance details linked to an IngredientSubstance (used when BasisOfStrength=''ReferenceIngredient''). Based on Section 3.1.4, 3.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44, 77' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance';
GO
-- RefSubstanceUNII
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UNII code of the reference substance (<definingSubstance><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceUNII';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceUNII';
GO
-- RefSubstanceName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the reference substance (<definingSubstance><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceName';
GO

-- ============================================================================
-- Table: Ingredient
-- Purpose: Represents an ingredient instance within a product or part. Based on Section 3.1.4.
-- ============================================================================
IF OBJECT_ID('dbo.Ingredient', 'U') IS NOT NULL
    DROP TABLE dbo.Ingredient;
GO

CREATE TABLE dbo.Ingredient (
    IngredientID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                  -- FK to Product or Product representing a Part
    IngredientSubstanceID INT  NULL,      -- FK to IngredientSubstance
    ClassCode VARCHAR(10) NULL,          -- e.g., 'ACTIB', 'ACTIM', 'ACTIR', 'IACT', 'INGR', 'COLR', 'CNTM', 'ADJV'
    QuantityNumerator DECIMAL(18, 9) NULL,   -- Corresponds to <quantity><numerator value>
    QuantityNumeratorUnit VARCHAR(50) NULL,  -- Corresponds to <quantity><numerator unit>
    QuantityDenominator DECIMAL(18, 9) NULL, -- Corresponds to <quantity><denominator value>
    QuantityDenominatorUnit VARCHAR(50) NULL,-- Corresponds to <quantity><denominator unit>
    ReferenceSubstanceID INT NULL,           -- FK to ReferenceSubstance (if BasisOfStrength='ReferenceIngredient')
    IsConfidential BIT NOT NULL DEFAULT 0,   -- Flag for inactive ingredients <confidentialityCode>
    SequenceNumber INT  NULL              -- Order of ingredient within the product/part
);
GO
-- Add Comments and Extended Properties for Ingredient table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents an ingredient instance within a product or part. Based on Section 3.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient';
GO
-- ClassCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ClassCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, Page 45 (Para 3.1.4.1), Page 74 (3.2.3), Page 77 (3.2.6), Page 91 (3.3.4), Page 101 (3.4.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ClassCode';
GO
-- QuantityNumerator, QuantityNumeratorUnit, QuantityDenominator, QuantityDenominatorUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Strength expressed as numerator/denominator value and unit (<quantity>). Null for CNTM unless zero numerator.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityNumerator';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, Page 45 (Para 3.1.4.2, 3.1.4.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityNumerator';
GO
-- ReferenceSubstanceID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ReferenceSubstanceID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ReferenceSubstanceID';
GO
-- IsConfidential
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the inactive ingredient information is confidential (<confidentialityCode code="B">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IsConfidential';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IsConfidential';
GO
-- SequenceNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Order of the ingredient as listed in the SPL file (important for cosmetics).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'SequenceNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 101' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'SequenceNumber';
GO

-- ============================================================================
-- Table: IngredientSourceProduct
-- Purpose: Links an Ingredient to its source product NDC (used in compounded drugs). Based on Section 3.1.4.
-- ============================================================================
IF OBJECT_ID('dbo.IngredientSourceProduct', 'U') IS NOT NULL
    DROP TABLE dbo.IngredientSourceProduct;
GO

CREATE TABLE dbo.IngredientSourceProduct (
    IngredientSourceProductID INT IDENTITY(1,1) PRIMARY KEY,
    IngredientID INT  NULL,              -- FK to Ingredient
    SourceProductNDC VARCHAR(20) NULL,  -- Source NDC Product Code (<substanceSpecification><code> code)
    SourceProductNDCSysten VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.6.69' -- Code system for Source NDC
);
GO

-- Add Comments and Extended Properties for IngredientSourceProduct table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an Ingredient to its source product NDC (used in compounded drugs). Based on Section 3.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct';
GO
-- SourceProductNDC
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'NDC Product Code of the source product used for the ingredient.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct', @level2type=N'COLUMN',@level2name=N'SourceProductNDC';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44, Para 3.1.4.12, 3.1.4.14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct', @level2type=N'COLUMN',@level2name=N'SourceProductNDC';
GO


PRINT 'Chunk 3 complete.';
GO

-- #############################################################################
-- Chunk 4: Packaging, Marketing, Characteristics, Parts (Section 3.1 continued)
-- #############################################################################

PRINT 'Creating Packaging, Marketing, Characteristics, and Parts Tables...';
GO

-- ============================================================================
-- Table: PackagingLevel
-- Purpose: Represents a level of packaging (<asContent>/<containerPackagedProduct>). Based on Section 3.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.PackagingLevel', 'U') IS NOT NULL
    DROP TABLE dbo.PackagingLevel;
GO

CREATE TABLE dbo.PackagingLevel (
    PackagingLevelID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT NULL,                       -- FK to Product (if this is the innermost package level for a base product)
    PartProductID INT NULL,                   -- FK to Product (if this is packaging for a Part)
    QuantityNumerator DECIMAL(18, 9) NULL,-- Corresponds to <quantity><numerator value> (amount of product/inner package)
    QuantityNumeratorUnit VARCHAR(50) NULL,-- Corresponds to <quantity><numerator unit>
    PackageFormCode VARCHAR(50) NULL,     -- Corresponds to <containerPackagedProduct><formCode code>
    PackageFormCodeSystem VARCHAR(100) NULL, -- Corresponds to <containerPackagedProduct><formCode codeSystem>
    PackageFormDisplayName VARCHAR(255) NULL -- Corresponds to <containerPackagedProduct><formCode displayName>
    -- Link to outer package via PackagingHierarchy table
);
GO

-- Add Comments and Extended Properties for PackagingLevel table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a level of packaging (<asContent>/<containerPackagedProduct>). Based on Section 3.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel';
GO
-- ProductID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to Product table if this packaging directly contains the base manufactured product.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductID';
GO
-- PartProductID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to Product table (representing a part) if this packaging contains a part of a kit.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PartProductID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46, 49' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PartProductID';
GO
-- QuantityNumerator, QuantityNumeratorUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit of the item contained within this package level (<quantity>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'QuantityNumerator';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.3, 3.1.5.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'QuantityNumerator';
GO
-- PackageFormCode, PackageFormCodeSystem, PackageFormDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Package type code, system, and display name (<containerPackagedProduct><formCode>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageFormCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.9, 3.1.5.10, 3.1.5.11' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageFormCode';
GO

-- ============================================================================
-- Table: PackageIdentifier
-- Purpose: Stores identifiers (NDC Package Code, etc.) for a specific packaging level. Based on Section 3.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.PackageIdentifier', 'U') IS NOT NULL
    DROP TABLE dbo.PackageIdentifier;
GO

CREATE TABLE dbo.PackageIdentifier (
    PackageIdentifierID INT IDENTITY(1,1) PRIMARY KEY,
    PackagingLevelID INT  NULL,         -- FK to PackagingLevel
    IdentifierValue VARCHAR(100) NULL, -- The actual code value (<code code=>)
    IdentifierSystemOID VARCHAR(100) NULL, -- OID for the code system (<code codeSystem=>)
    IdentifierType VARCHAR(50) NULL  -- e.g., 'NDCPackage', 'NHRICPackage', 'GS1Package', 'HIBCCPackage', 'ISBTPackage'
);
GO

-- Add Comments and Extended Properties for PackageIdentifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores identifiers (NDC Package Code, etc.) for a specific packaging level. Based on Section 3.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, 48' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier';
GO
-- IdentifierValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The package item code value (<containerPackagedProduct><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
-- IdentifierSystemOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the package identifier system (<containerPackagedProduct><code> codeSystem).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 48, Para 3.1.5.27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO

-- ============================================================================
-- Table: PackagingHierarchy
-- Purpose: Defines the nested structure of packaging levels.
-- ============================================================================
IF OBJECT_ID('dbo.PackagingHierarchy', 'U') IS NOT NULL
    DROP TABLE dbo.PackagingHierarchy;
GO

CREATE TABLE dbo.PackagingHierarchy (
    PackagingHierarchyID INT IDENTITY(1,1) PRIMARY KEY,
    OuterPackagingLevelID INT  NULL, -- FK to PackagingLevel (The containing package)
    InnerPackagingLevelID INT  NULL, -- FK to PackagingLevel (The contained package)
    SequenceNumber INT  NULL       -- Order of inner package within outer package (if multiple identical inner packages)
);
GO

-- Add Comments and Extended Properties for PackagingHierarchy table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the nested structure of packaging levels. Links an outer package to the inner package(s) it contains.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingHierarchy';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46 (implied by nested <asContent>)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingHierarchy';
GO

-- ============================================================================
-- Table: MarketingCategory
-- Purpose: Stores marketing category and application/monograph information for a product or part. Based on Section 3.1.7.
-- ============================================================================
IF OBJECT_ID('dbo.MarketingCategory', 'U') IS NOT NULL
    DROP TABLE dbo.MarketingCategory;
GO

CREATE TABLE dbo.MarketingCategory (
    MarketingCategoryID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                 -- FK to Product (or Product representing a Part)
    CategoryCode VARCHAR(50) NULL,      -- Marketing Category code (<approval><code> code)
    CategoryCodeSystem VARCHAR(100) NULL, -- Marketing Category code system (<approval><code> codeSystem)
    CategoryDisplayName VARCHAR(255) NULL, -- Marketing Category display name (<approval><code> displayName)
    ApplicationOrMonographIDValue VARCHAR(100) NULL, -- Application number or Monograph ID (<approval><id extension>)
    ApplicationOrMonographIDOID VARCHAR(100) NULL,   -- OID for App#/Monograph system (<approval><id root>)
    ApprovalDate DATE NULL,                 -- Approval date (<approval><effectiveTime><low value>)
    TerritoryCode CHAR(3) NULL                -- Usually 'USA' (<territory><code>)
);
GO

-- Add Comments and Extended Properties for MarketingCategory table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores marketing category and application/monograph information for a product or part (<subjectOf><approval>). Based on Section 3.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory';
GO
-- CategoryCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the marketing category (e.g., NDA, ANDA, OTC Monograph Drug).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'CategoryCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54, Para 3.1.7.2, 3.1.7.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'CategoryCode';
GO
-- ApplicationOrMonographIDValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Application number, monograph ID, or citation (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDValue';
GO
-- ApplicationOrMonographIDOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Root OID for the application number or monograph ID system (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 56, Para 3.1.7.7, 3.1.7.28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDOID';
GO
-- ApprovalDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date of application approval, if applicable (<effectiveTime><low value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApprovalDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 58, Para 3.1.7.33' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApprovalDate';
GO
-- TerritoryCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Territory code, typically USA (<territory><code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'TerritoryCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 55, Para 3.1.7.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'TerritoryCode';
GO

-- ============================================================================
-- Table: MarketingStatus
-- Purpose: Stores marketing status information for a product or package. Based on Section 3.1.8.
-- ============================================================================
IF OBJECT_ID('dbo.MarketingStatus', 'U') IS NOT NULL
    DROP TABLE dbo.MarketingStatus;
GO

CREATE TABLE dbo.MarketingStatus (
    MarketingStatusID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT NULL,                    -- FK to Product (if status applies to product)
    PackagingLevelID INT NULL,             -- FK to PackagingLevel (if status applies to a package)
    MarketingActCode VARCHAR(50) NULL,   -- Code for marketing activity (<marketingAct><code> code)
    MarketingActCodeSystem VARCHAR(100) NULL, -- Code system for MarketingActCode
    StatusCode VARCHAR(20) NULL,       -- e.g., 'active', 'completed', 'new', 'cancelled' (<statusCode code>)
    EffectiveStartDate DATE NULL,          -- Marketing start date (<effectiveTime><low value>)
    EffectiveEndDate DATE NULL             -- Marketing end date (<effectiveTime><high value>)
);
GO

-- Add Comments and Extended Properties for MarketingStatus table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores marketing status information for a product or package (<subjectOf><marketingAct>). Based on Section 3.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus';
GO
-- MarketingActCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the marketing activity (e.g., C53292 Marketing, C96974 Drug Sample).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'MarketingActCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 61, Para 3.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'MarketingActCode';
GO
-- StatusCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Status code: active, completed, new, cancelled.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'StatusCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 61, Para 3.1.8.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'StatusCode';
GO
-- EffectiveStartDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Marketing start date (<effectiveTime><low value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveStartDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveStartDate';
GO
-- EffectiveEndDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Marketing end date (<effectiveTime><high value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveEndDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveEndDate';
GO


-- ============================================================================
-- Table: Characteristic
-- Purpose: Stores characteristics of a product or package. Based on Section 3.1.9.
-- Note: Uses a wide structure to accommodate different value types (PQ, INT, CV, ST, BL, IVL_PQ, ED).
-- ============================================================================
IF OBJECT_ID('dbo.Characteristic', 'U') IS NOT NULL
    DROP TABLE dbo.Characteristic;
GO

CREATE TABLE dbo.Characteristic (
    CharacteristicID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT NULL,                     -- FK to Product (if characteristic applies to product)
    PackagingLevelID INT NULL,              -- FK to PackagingLevel (if characteristic applies to package)
    CharacteristicCode VARCHAR(50) NULL,    -- Code identifying the characteristic (<characteristic><code> code)
    CharacteristicCodeSystem VARCHAR(100) NULL, -- Code system for CharacteristicCode
    ValueType VARCHAR(10) NULL,         -- Data type of the value (PQ, INT, CV, ST, BL, IVL_PQ, ED)
    -- Value columns for different types (only one set relevant per row based on ValueType)
    ValuePQ_Value DECIMAL(18, 9) NULL,
    ValuePQ_Unit VARCHAR(50) NULL,
    ValueINT INT NULL,
    ValueCV_Code VARCHAR(50) NULL,
    ValueCV_CodeSystem VARCHAR(100) NULL,
    ValueCV_DisplayName VARCHAR(255) NULL,
    ValueST NVARCHAR(MAX) NULL,
    ValueBL BIT NULL,
    ValueIVLPQ_LowValue DECIMAL(18, 9) NULL,
    ValueIVLPQ_LowUnit VARCHAR(50) NULL,
    ValueIVLPQ_HighValue DECIMAL(18, 9) NULL,
    ValueIVLPQ_HighUnit VARCHAR(50) NULL,
    ValueED_MediaType VARCHAR(50) NULL,
    ValueED_FileName VARCHAR(255) NULL,
    ValueNullFlavor VARCHAR(10) NULL        -- Used for INT type with nullFlavor="PINF" (e.g., SPLUSE, SPLPRODUCTIONAMOUNT)
);
GO

-- Add Comments and Extended Properties for Characteristic table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores characteristics of a product or package (<subjectOf><characteristic>). Based on Section 3.1.9.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 64' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic';
GO
-- CharacteristicCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the characteristic property (e.g., SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'CharacteristicCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 64, 66, 67, 68, 84, 95, 104, 259' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'CharacteristicCode';
GO
-- ValueType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates the XML Schema instance type of the <value> element (e.g., PQ, INT, CV, ST, BL, IVL_PQ, ED).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'ValueType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 65, 67' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'ValueType';
GO


-- ============================================================================
-- Table: ProductPart
-- Purpose: Defines the parts comprising a kit product. Based on Section 3.1.6.
-- ============================================================================
IF OBJECT_ID('dbo.ProductPart', 'U') IS NOT NULL
    DROP TABLE dbo.ProductPart;
GO

CREATE TABLE dbo.ProductPart (
    ProductPartID INT IDENTITY(1,1) PRIMARY KEY,
    KitProductID INT  NULL,              -- FK to Product (The parent Kit product)
    PartProductID INT  NULL,             -- FK to Product (The product representing the part)
    PartQuantityNumerator DECIMAL(18, 9) NULL, -- Total amount/count of this part in the kit (<quantity><numerator value>)
    PartQuantityNumeratorUnit VARCHAR(50) NULL -- Unit for the part quantity (<quantity><numerator unit>)
);
GO

-- Add Comments and Extended Properties for ProductPart table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the parts comprising a kit product. Links a Kit Product to its constituent Part Products. Based on Section 3.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 49' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart';
GO
-- PartQuantityNumerator, PartQuantityNumeratorUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit of this part contained within the parent kit product (<part><quantity>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart', @level2type=N'COLUMN',@level2name=N'PartQuantityNumerator';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 49, 54 (Para 3.1.6.2, 3.1.6.3, 3.1.6.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart', @level2type=N'COLUMN',@level2name=N'PartQuantityNumerator';
GO

-- ============================================================================
-- Table: PartOfAssembly
-- Purpose: Links products sold separately but intended for use together. Based on Section 3.1.6, 3.3.8.
-- Note: Represents the <asPartOfAssembly> structure.
-- ============================================================================
IF OBJECT_ID('dbo.PartOfAssembly', 'U') IS NOT NULL
    DROP TABLE dbo.PartOfAssembly;
GO

CREATE TABLE dbo.PartOfAssembly (
    PartOfAssemblyID INT IDENTITY(1,1) PRIMARY KEY,
    PrimaryProductID INT  NULL,      -- FK to Product (The product being described that is part of the assembly)
    AccessoryProductID INT  NULL     -- FK to Product (The other product in the assembly, referenced via <part><partProduct> inside <asPartOfAssembly>)
    -- The <wholeProduct> element doesn't have its own identifier in the spec, so it's represented implicitly by the link.
);
GO

-- Add Comments and Extended Properties for PartOfAssembly table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links products sold separately but intended for use together (<asPartOfAssembly>). Based on Section 3.1.6, 3.3.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PartOfAssembly';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 53, 92' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PartOfAssembly';
GO


PRINT 'Chunk 4 complete.';
GO

-- #############################################################################
-- Chunk 5: Drug, Biologics, Dietary Supplement, Medical Food Specifics (Section 3.2)
-- #############################################################################

PRINT 'Creating Drug/Biologic/Supplement/Medical Food Specific Tables...';
GO

-- ============================================================================
-- Table: Policy
-- Purpose: Stores policy information related to a product, like DEA Schedule. Based on Section 3.2.11.
-- Note: Linked via subjectOf in the XML, associating with a Product.
-- ============================================================================
IF OBJECT_ID('dbo.Policy', 'U') IS NOT NULL
    DROP TABLE dbo.Policy;
GO

CREATE TABLE dbo.Policy (
    PolicyID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,               -- FK to Product
    PolicyClassCode VARCHAR(50) NULL, -- e.g., 'DEADrugSchedule'
    PolicyCode VARCHAR(50) NULL,      -- Code value (e.g., 'C48675' for CII)
    PolicyCodeSystem VARCHAR(100) NULL, -- Code system for PolicyCode
    PolicyDisplayName VARCHAR(255) NULL -- Display name for PolicyCode
);
GO

-- Add Comments and Extended Properties for Policy table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores policy information related to a product, like DEA Schedule (<subjectOf><policy>). Based on Section 3.2.11.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy';
GO
-- PolicyClassCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Class code for the policy, e.g., DEADrugSchedule.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyClassCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyClassCode';
GO
-- PolicyCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code representing the specific policy value (e.g., DEA Schedule C-II).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCode';
GO
-- PolicyCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the policy code (e.g., 2.16.840.1.113883.3.26.1.1 for DEA schedule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCodeSystem';
GO
-- PolicyDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the policy code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyDisplayName';
GO

-- ============================================================================
-- Table: ProductRouteOfAdministration
-- Purpose: Links a product (or part) to its route(s) of administration. Based on Section 3.2.20.
-- Note: Represents the <consumedIn><substanceAdministration> structure.
-- ============================================================================
IF OBJECT_ID('dbo.ProductRouteOfAdministration', 'U') IS NOT NULL
    DROP TABLE dbo.ProductRouteOfAdministration;
GO

CREATE TABLE dbo.ProductRouteOfAdministration (
    ProductRouteOfAdministrationID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                 -- FK to Product (or Product representing a Part)
    RouteCode VARCHAR(50) NULL,         -- Code for the route (<routeCode code>)
    RouteCodeSystem VARCHAR(100) NULL,  -- Code system for RouteCode (<routeCode codeSystem>)
    RouteDisplayName VARCHAR(255) NULL, -- Display name for RouteCode (<routeCode displayName>)
    RouteNullFlavor VARCHAR(10) NULL        -- Used for 'NA' nullFlavor in Bulk Ingredients etc.
);
GO

-- Add Comments and Extended Properties for ProductRouteOfAdministration table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a product (or part) to its route(s) of administration (<consumedIn><substanceAdministration>). Based on Section 3.2.20.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration';
GO
-- RouteCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the route of administration.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCode';
GO
-- RouteCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCodeSystem';
GO
-- RouteDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the route code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteDisplayName';
GO
-- RouteNullFlavor
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteNullFlavor';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteNullFlavor';
GO

-- Note: Specific Drug Product characteristics (Color, Shape, Size, Scoring, Imprint, Flavor, Image - Sections 3.2.12 to 3.2.19)
-- are handled using the general 'Characteristic' table created in Chunk 4, using the appropriate codes (SPLCOLOR, SPLSHAPE, etc.).

PRINT 'Chunk 5 complete.';
GO

-- #############################################################################
-- Chunk 6: Device and Cosmetic Product Specifics (Section 3.3, 3.4)
-- #############################################################################

PRINT 'Creating Device and Cosmetic Specific Tables...';
GO

-- Note: Device Product specifics (Section 3.3) like Item Code, Name, Additional Identifiers,
-- Ingredients, Parts, Assembly Info, Regulatory Identifiers (Marketing Category),
-- Marketing Status, and Characteristics (Reusability, Sterile Use) are generally
-- handled by the tables created in previous chunks (ProductIdentifier, Product,
-- AdditionalIdentifier, Ingredient, ProductPart, PartOfAssembly, MarketingCategory,
-- MarketingStatus, Characteristic) using device-specific codes and values where applicable.

-- Note: Cosmetic Product specifics (Section 3.4) like Item Code (CLN or GTIN), Name, Category,
-- Ingredients (INGR/COLR, optional UNII), Parts, Marketing Status, Professional Use,
-- and Label Image are generally handled by the tables created in previous chunks
-- (ProductIdentifier, Product, SpecializedKind, Ingredient, IngredientSubstance,
-- ProductPart, MarketingStatus, Characteristic) using cosmetic-specific codes and values.

-- ============================================================================
-- Table: ProductWebLink
-- Purpose: Stores the web page link for a cosmetic product. Based on Section 3.4.7.
-- Note: Linked via subjectOf in the XML.
-- ============================================================================
IF OBJECT_ID('dbo.ProductWebLink', 'U') IS NOT NULL
    DROP TABLE dbo.ProductWebLink;
GO

CREATE TABLE dbo.ProductWebLink (
    ProductWebLinkID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,           -- FK to Product
    WebURL VARCHAR(2048) NULL     -- The absolute URL (<reference value=>)
);
GO

-- Add Comments and Extended Properties for ProductWebLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the web page link for a cosmetic product (<subjectOf><document><text><reference value=>). Based on Section 3.4.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 103' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink';
GO
-- WebURL
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Absolute URL for the product web page, starting with http:// or https://.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink', @level2type=N'COLUMN',@level2name=N'WebURL';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 103, Para 3.4.7.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink', @level2type=N'COLUMN',@level2name=N'WebURL';
GO

-- Reminder: Specific characteristics mentioned in 3.3 and 3.4 are stored in the Characteristic table:
-- Device Reusability (3.3.12): CharacteristicCode='SPLUSE', ValueType='INT' or ValueNullFlavor='PINF'
-- Device Sterile Use (3.3.14): CharacteristicCode='SPLSTERILEUSE', ValueType='BL'
-- Device MRI Safety (Table 6, Page 95): CharacteristicCode='SPLMRISAFE', ValueType='BL'
-- Cosmetic Professional Use (3.4.8): CharacteristicCode='SPLPROFESSIONALUSE', ValueType='BL'
-- Cosmetic Label Image (3.4.9): CharacteristicCode='SPLIMAGE', ValueType='ED'

PRINT 'Chunk 6 complete.';
GO

-- #############################################################################
-- Chunk 7: Drug Listing & Labeler Code Request Specifics (Section 4, 5)
-- #############################################################################

PRINT 'Creating Drug Listing and Labeler Code Request Specific Tables...';
GO

-- Modify Organization table to remove direct identifier columns
-- Identifiers will now be stored in the OrganizationIdentifier table
PRINT 'Modifying Organization Table...';
GO
ALTER TABLE dbo.Organization DROP COLUMN DUNSNumber;
ALTER TABLE dbo.Organization DROP COLUMN FEINumber;
GO

-- Update Organization Comments/Properties after removing direct columns
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Stores information about organizations (e.g., labelers, registrants, establishments). Identifiers (DUNS, FEI, Labeler Code etc) stored in OrganizationIdentifier table. Based on Section 2.1.4, 2.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization';
GO

-- ============================================================================
-- Table: OrganizationIdentifier
-- Purpose: Stores various identifiers associated with an Organization (DUNS, FEI, Labeler Code, License #). Replaces direct columns in Organization table.
-- ============================================================================
IF OBJECT_ID('dbo.OrganizationIdentifier', 'U') IS NOT NULL
    DROP TABLE dbo.OrganizationIdentifier;
GO

CREATE TABLE dbo.OrganizationIdentifier (
    OrganizationIdentifierID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationID INT  NULL,           -- FK to Organization
    IdentifierValue VARCHAR(100) NULL, -- The actual code value (e.g., DUNS number, FEI number, Labeler Code)
    IdentifierSystemOID VARCHAR(100) NULL, -- OID for the code system (e.g., DUNS OID, FEI OID, NDC OID)
    IdentifierType VARCHAR(50) NULL  -- e.g., 'DUNS', 'FEI', 'NDCLabelerCode', 'ManufacturerLicenseNumber', 'StateLicense'
);
GO

-- Add Comments and Extended Properties for OrganizationIdentifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores various identifiers associated with an Organization (DUNS, FEI, Labeler Code, License Number, etc.).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15 (DUNS, FEI), Page 106 (Labeler DUNS), Page 118 (Labeler DUNS & Labeler Code), Page 126 (Est FEI), Page 164 (Manuf License), Page 180 (State License)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier';
GO
-- IdentifierValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The identifier value (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15, 106, 118, 126, 164, 180' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue';
GO
-- IdentifierSystemOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the identifier system (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15 (1.3.6.1.4.1.519.1, 2.16.840.1.113883.4.82), Page 118 (2.16.840.1.113883.6.69), Page 164 (1.3.6.1.4.1.32366.1.3.1.2), Page 180 (1.3.6.1.4.1.32366.4.840.x)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID';
GO
-- IdentifierType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type classification of the identifier based on the OID and context.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType';
GO

-- ============================================================================
-- Table: BusinessOperation
-- Purpose: Stores business operation details for an establishment or labeler. Based on Section 4.1.4, 5.1.5, 6.1.6.
-- Note: Linked via DocumentRelationship representing the Establishment or Labeler Detail org.
-- ============================================================================
IF OBJECT_ID('dbo.BusinessOperation', 'U') IS NOT NULL
    DROP TABLE dbo.BusinessOperation;
GO

CREATE TABLE dbo.BusinessOperation (
    BusinessOperationID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentRelationshipID INT  NULL,      -- FK to DocumentRelationship (linking to the Org performing the operation)
    OperationCode VARCHAR(50) NULL,      -- Business Operation code (e.g., 'C43360' for manufacture)
    OperationCodeSystem VARCHAR(100) NULL, -- Code system for OperationCode
    OperationDisplayName VARCHAR(255) NULL -- Display name for OperationCode
);
GO

-- Add Comments and Extended Properties for BusinessOperation table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores business operation details for an establishment or labeler (<performance><actDefinition>). Based on Section 4.1.4, 5.1.5, 6.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation';
GO
-- OperationCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the business operation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCode';
GO
-- OperationCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the operation code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCodeSystem';
GO
-- OperationDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the operation code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 108 (Para 4.1.4.7), Page 121 (Para 5.1.5.4), Page 129 (Para 6.1.6.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationDisplayName';
GO

-- ============================================================================
-- Table: BusinessOperationQualifier
-- Purpose: Stores qualifier details for a specific Business Operation. Based on Section 5.1.5, 6.1.7.
-- ============================================================================
IF OBJECT_ID('dbo.BusinessOperationQualifier', 'U') IS NOT NULL
    DROP TABLE dbo.BusinessOperationQualifier;
GO

CREATE TABLE dbo.BusinessOperationQualifier (
    BusinessOperationQualifierID INT IDENTITY(1,1) PRIMARY KEY,
    BusinessOperationID INT  NULL,          -- FK to BusinessOperation
    QualifierCode VARCHAR(50) NULL,        -- Qualifier code (<approval><code>)
    QualifierCodeSystem VARCHAR(100) NULL,   -- Code system for QualifierCode
    QualifierDisplayName VARCHAR(255) NULL   -- Display name for QualifierCode
);
GO

-- Add Comments and Extended Properties for BusinessOperationQualifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores qualifier details for a specific Business Operation (<actDefinition><subjectOf><approval><code>). Based on Section 5.1.5, 6.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121, 130' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier';
GO
-- QualifierCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code qualifying the business operation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.8), Page 130 (Para 6.1.7.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCode';
GO
-- QualifierCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the qualifier code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.9), Page 130 (Para 6.1.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCodeSystem';
GO
-- QualifierDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the qualifier code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.10), Page 130 (Para 6.1.7.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierDisplayName';
GO

-- ============================================================================
-- Table: BusinessOperationProductLink
-- Purpose: Links a Business Operation performed by an establishment to a specific product. Based on Section 4.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.BusinessOperationProductLink', 'U') IS NOT NULL
    DROP TABLE dbo.BusinessOperationProductLink;
GO

CREATE TABLE dbo.BusinessOperationProductLink (
    BusinessOperationProductLinkID INT IDENTITY(1,1) PRIMARY KEY,
    BusinessOperationID INT  NULL,    -- FK to BusinessOperation
    ProductID INT  NULL               -- FK to Product (The product linked to the operation)
);
GO

-- Add Comments and Extended Properties for BusinessOperationProductLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Business Operation performed by an establishment to a specific product (<actDefinition><product>). Based on Section 4.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationProductLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 111' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationProductLink';
GO


-- ============================================================================
-- Table: LegalAuthenticator
-- Purpose: Stores legal authenticator (signature) information for a document. Based on Section 5.1.6, 35.1.3, 36.1.7.
-- ============================================================================
IF OBJECT_ID('dbo.LegalAuthenticator', 'U') IS NOT NULL
    DROP TABLE dbo.LegalAuthenticator;
GO

CREATE TABLE dbo.LegalAuthenticator (
    LegalAuthenticatorID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentID INT  NULL,                -- FK to Document
    NoteText NVARCHAR(MAX) NULL,            -- Optional signing statement (<noteText>)
    TimeValue DATETIME2(0) NULL,        -- Signature timestamp (<time value>)
    SignatureText NVARCHAR(MAX) NULL,   -- Electronic signature text (<signatureText>)
    AssignedPersonName VARCHAR(500) NULL -- Name of the signing person (<assignedPerson><name>)
    -- representedOrganization is empty in spec examples 35.1.3, 36.1.7 but present for FDA signature in 5.1.6
    -- Add fields if representedOrganization details are needed for FDA signatures
    ,SignerOrganizationID INT NULL           -- Optional FK to Organization (Signer's org, used for FDA signers in 5.1.6)
);
GO

-- Add Comments and Extended Properties for LegalAuthenticator table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores legal authenticator (signature) information for a document (<legalAuthenticator>). Based on Section 5.1.6, 35.1.3, 36.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 122, 256, 268' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator';
GO
-- NoteText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional signing statement provided in <noteText>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'NoteText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.2), Page 268 (Para 36.1.7.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'NoteText';
GO
-- TimeValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Timestamp of the signature (<time value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'TimeValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.9), Page 269 (Para 36.1.7.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'TimeValue';
GO
-- SignatureText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The electronic signature text (<signatureText>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignatureText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.3), Page 269 (Para 36.1.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignatureText';
GO
-- AssignedPersonName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the person signing (<assignedPerson><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'AssignedPersonName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.6), Page 269 (Para 36.1.7.6)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'AssignedPersonName';
GO
-- SignerOrganizationID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the signing Organization, used for FDA signers in Labeler Code Inactivation (Sec 5.1.6).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignerOrganizationID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 122' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignerOrganizationID';
GO

-- Update Section table comment for Reporting Period usage
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Effective time for the section (<effectiveTime value>). For Compounded Drug Labels (Sec 4.2.2), low/high represent the reporting period.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
EXEC sys.sp_updateextendedproperty @name=N'SPL_Reference', @value=N'Page 24 (Para 2.2.1.9), Page 114 (Para 4.2.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO

PRINT 'Chunk 7 complete.';
GO

-- #############################################################################
-- Chunk 8: Establishment Reg, Out of Business, Pharm Class Indexing (Sec 6, 7, 8)
-- #############################################################################

PRINT 'Creating Establishment Registration, Out of Business, and Pharmacologic Class Indexing Support Tables...';
GO

-- Note: Establishment Registration (Section 6) structure relies heavily on existing tables:
-- Document (Type 51725-0, 70097-1, 53410-7)
-- DocumentRelationship (Registrant->Establishment, Establishment->USAgent, Establishment->Importer)
-- OrganizationIdentifier (DUNS, FEI)
-- ContactParty (Registrant, Establishment)
-- Address (Registrant, Establishment)
-- Telecom (Registrant, Establishment, USAgent, Importer)
-- BusinessOperation (linked to Establishment DocumentRelationship)
-- BusinessOperationQualifier (linked to BusinessOperation)
-- RelatedDocument (for RPLC references)
-- Extended properties within those tables reference Section 6 where applicable.

-- Note: Out of Business Notification (Section 7) structure relies on existing tables:
-- Document (Type 53411-5)
-- RelatedDocument (linking back to the registration being ended)
-- It has an empty body and minimal header info per the spec.

-- ============================================================================
-- Table: IdentifiedSubstance
-- Purpose: Stores substance details (e.g., active moiety, pharmacologic class identifier) used in Indexing contexts. Based on Section 8.2.2, 8.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.IdentifiedSubstance', 'U') IS NOT NULL
    DROP TABLE dbo.IdentifiedSubstance;
GO

CREATE TABLE dbo.IdentifiedSubstance (
    IdentifiedSubstanceID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                  -- FK to Section (The indexing section containing this substance)
    SubjectType VARCHAR(50) NULL,        -- e.g., 'ActiveMoiety', 'PharmacologicClass'
    SubstanceIdentifierValue VARCHAR(100) NULL, -- ID value (<id extension> or <code> code)
    SubstanceIdentifierSystemOID VARCHAR(100) NULL, -- ID system OID (<id root> or <code> codeSystem)
    IsDefinition BIT NOT NULL DEFAULT 0      -- Flag: True if this row represents the *definition* of the substance/class (8.2.3), False if it's a *reference* (8.2.2)
);
GO

-- Add Comments and Extended Properties for IdentifiedSubstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores substance details (e.g., active moiety, pharmacologic class identifier) used in Indexing contexts (<subject><identifiedSubstance>). Based on Section 8.2.2, 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135, 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance';
GO
-- SubjectType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates whether the identified substance represents an Active Moiety (8.2.2) or a Pharmacologic Class being defined (8.2.3).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubjectType';
GO
-- SubstanceIdentifierValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier value - UNII for Active Moiety, MED-RT/MeSH code for Pharm Class.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135 (Para 8.2.2.2), Page 136 (Para 8.2.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierValue';
GO
-- SubstanceIdentifierSystemOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier system OID - UNII (2.16.840.1.113883.4.9), MED-RT (2.16.840.1.113883.6.345), or MeSH (2.16.840.1.113883.6.177).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierSystemOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135 (Para 8.2.2.3), Page 136 (Para 8.2.2.11)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierSystemOID';
GO
-- IsDefinition
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if this row defines the substance/class (8.2.3) or references it (8.2.2).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'IsDefinition';
GO

-- ============================================================================
-- Table: PharmacologicClass
-- Purpose: Stores the definition of a pharmacologic class concept. Based on Section 8.2.3.
-- Note: This represents the <identifiedSubstance> when defining a class.
-- ============================================================================
IF OBJECT_ID('dbo.PharmacologicClass', 'U') IS NOT NULL
    DROP TABLE dbo.PharmacologicClass;
GO

CREATE TABLE dbo.PharmacologicClass (
    PharmacologicClassID INT IDENTITY(1,1) PRIMARY KEY,
    IdentifiedSubstanceID INT  NULL,      -- FK to IdentifiedSubstance (where IsDefinition=1 and SubjectType='PharmacologicClass')
    ClassCode VARCHAR(50) NULL,        -- MED-RT or MeSH code (<code> code)
    ClassCodeSystem VARCHAR(100) NULL, -- Code system (<code> codeSystem)
    ClassDisplayName VARCHAR(500) NULL -- Display name including type suffix (<code> displayName)
);
GO

-- Add Comments and Extended Properties for PharmacologicClass table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the definition of a pharmacologic class concept, identified by its code. Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass';
GO
-- ClassCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The MED-RT or MeSH code for the pharmacologic class.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassCode';
GO
-- ClassDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The display name for the class code, including the type suffix like [EPC] or [CS].' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.2.15, 8.2.2.16' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassDisplayName';
GO

-- ============================================================================
-- Table: PharmacologicClassName
-- Purpose: Stores preferred (L) and alternate (A) names for a Pharmacologic Class. Based on Section 8.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.PharmacologicClassName', 'U') IS NOT NULL
    DROP TABLE dbo.PharmacologicClassName;
GO

CREATE TABLE dbo.PharmacologicClassName (
    PharmClassNameID INT IDENTITY(1,1) PRIMARY KEY,
    PharmacologicClassID INT  NULL,    -- FK to PharmacologicClass
    NameValue NVARCHAR(1000) NULL,   -- The name string (<name>)
    NameUse CHAR(1) NULL             -- 'L' for preferred, 'A' for alternate (<name use=>)
);
GO

-- Add Comments and Extended Properties for PharmacologicClassName table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores preferred (L) and alternate (A) names for a Pharmacologic Class (<identifiedSubstance><name use=>). Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName';
GO
-- NameValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The text of the preferred or alternate name.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameValue';
GO
-- NameUse
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the name is preferred (L) or alternate (A).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameUse';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameUse';
GO

-- ============================================================================
-- Table: PharmacologicClassLink
-- Purpose: Links an active moiety (IdentifiedSubstance) to its associated Pharmacologic Class. Based on Section 8.2.2.
-- Note: Represents the <asSpecializedKind><generalizedMaterialKind> under the active moiety definition.
-- ============================================================================
IF OBJECT_ID('dbo.PharmacologicClassLink', 'U') IS NOT NULL
    DROP TABLE dbo.PharmacologicClassLink;
GO

CREATE TABLE dbo.PharmacologicClassLink (
    PharmClassLinkID INT IDENTITY(1,1) PRIMARY KEY,
    ActiveMoietySubstanceID INT  NULL, -- FK to IdentifiedSubstance (where SubjectType='ActiveMoiety')
    PharmacologicClassID INT  NULL     -- FK to PharmacologicClass
);
GO

-- Add Comments and Extended Properties for PharmacologicClassLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an active moiety (IdentifiedSubstance) to its associated Pharmacologic Class (<asSpecializedKind> under moiety). Based on Section 8.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassLink';
GO

-- ============================================================================
-- Table: PharmacologicClassHierarchy
-- Purpose: Defines the hierarchy between Pharmacologic Classes. Based on Section 8.2.3.
-- Note: Represents the <asSpecializedKind><generalizedMaterialKind> under the class definition.
-- ============================================================================
IF OBJECT_ID('dbo.PharmacologicClassHierarchy', 'U') IS NOT NULL
    DROP TABLE dbo.PharmacologicClassHierarchy;
GO

CREATE TABLE dbo.PharmacologicClassHierarchy (
    PharmClassHierarchyID INT IDENTITY(1,1) PRIMARY KEY,
    ChildPharmacologicClassID INT  NULL,  -- FK to PharmacologicClass (The class being defined)
    ParentPharmacologicClassID INT  NULL -- FK to PharmacologicClass (The super-class)
);
GO

-- Add Comments and Extended Properties for PharmacologicClassHierarchy table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the hierarchy between Pharmacologic Classes (<asSpecializedKind> under class definition). Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassHierarchy';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 137, Para 8.2.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassHierarchy';
GO

PRINT 'Chunk 8 complete.';
GO

-- #############################################################################
-- Chunk 9: Dietary Supp, Medical Food, Medical Device Labeling, Billing Unit Indexing (Sec 9, 10, 11, 12)
-- #############################################################################

PRINT 'Creating Dietary Supplement, Medical Food, Medical Device Labeling, and Billing Unit Indexing Support Tables...';
GO

-- Note: Dietary Supplement Labeling (Section 9) structure relies on existing tables:
-- Document (Type 58476-3)
-- DocumentAuthor (Labeler)
-- OrganizationIdentifier (DUNS)
-- DocumentRelationship (Labeler -> Registrant)
-- Section (Product Data Elements 48780-1, PDP 51945-4, Statement of Identity 69718-5, etc.)
-- Product, Ingredient, Packaging, MarketingCategory (C86952), MarketingStatus, etc.

-- Note: Medical Food Labeling (Section 10) structure relies on existing tables:
-- Document (Type 58475-5)
-- DocumentAuthor (Labeler)
-- OrganizationIdentifier (DUNS)
-- DocumentRelationship (Labeler -> Registrant -> Establishment)
-- BusinessOperation
-- Section (Product Data Elements 48780-1, PDP 51945-4)
-- Product, Ingredient, Packaging, MarketingCategory (C86964), MarketingStatus, etc.

-- Note: Medical Device Labeling (Section 11) structure relies on existing tables:
-- Document (Type 69403-4 OTC, 69404-2 Rx)
-- DocumentAuthor (Labeler)
-- OrganizationIdentifier (DUNS)
-- ContactParty (Labeler)
-- Section (Product Data Elements 48780-1, PDP 51945-4)
-- Product, ProductIdentifier, SpecializedKind, Packaging, MarketingCategory, MarketingStatus, etc.

-- ============================================================================
-- Table: BillingUnitIndex
-- Purpose: Stores the link between an NDC Package Code and its NCPDP Billing Unit. Based on Section 12.
-- Note: Captures the core data from the Indexing - Billing Unit document type.
-- ============================================================================
IF OBJECT_ID('dbo.BillingUnitIndex', 'U') IS NOT NULL
    DROP TABLE dbo.BillingUnitIndex;
GO

CREATE TABLE dbo.BillingUnitIndex (
    BillingUnitIndexID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                 -- FK to the Indexing Section (48779-3) in the Billing Unit Index document
    PackageNDCValue VARCHAR(20) NULL,   -- The specific NDC Package Code being indexed
    PackageNDCSystemOID VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.6.69', -- System for NDC
    BillingUnitCode VARCHAR(5) NULL,    -- NCPDP Billing Unit Code ('GM', 'ML', 'EA')
    BillingUnitCodeSystemOID VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.2.13' -- System for Billing Unit Code
);
GO

-- Add Comments and Extended Properties for BillingUnitIndex table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the link between an NDC Package Code and its NCPDP Billing Unit, from Indexing - Billing Unit (71446-9) documents. Based on Section 12.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 145' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex';
GO
-- PackageNDCValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The NDC Package Code being linked (<containerPackagedProduct><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'PackageNDCValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146, Para 12.2.2.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'PackageNDCValue';
GO
-- BillingUnitCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The NCPDP Billing Unit Code associated with the NDC package (GM, ML, or EA).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146 (Para 12.2.3), Page 147 (Para 12.2.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCode';
GO
-- BillingUnitCodeSystemOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system OID for the NCPDP Billing Unit Code (2.16.840.1.113883.2.13).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCodeSystemOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146 (Para 12.2.3), Page 147 (Para 12.2.3.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCodeSystemOID';
GO

PRINT 'Chunk 9 complete.';
GO

-- #############################################################################
-- Chunk 10: GDUFA ID, Product Concept Indexing, Lot Distribution (Sec 13, 15, 16)
-- #############################################################################

PRINT 'Creating GDUFA ID, Product Concept Indexing, and Lot Distribution Tables...';
GO

-- Note: Generic User Fee Facility Self-Identification (Section 13) structure relies on existing tables:
-- Document (Type 72090-4, 71743-9)
-- DocumentRelationship (Registrant->Facility)
-- OrganizationIdentifier (DUNS, FEI)
-- ContactParty (Registrant, Facility)
-- Address (Registrant, Facility)
-- Telecom (Registrant, Facility)
-- BusinessOperation (linked to Facility DocumentRelationship)
-- BusinessOperationQualifier (linked to BusinessOperation, specific codes C101886, C132491)

-- ============================================================================
-- Table: ProductConcept
-- Purpose: Stores the definition of an abstract product or kit concept. Based on Section 15.2.2, 15.2.6.
-- ============================================================================
IF OBJECT_ID('dbo.ProductConcept', 'U') IS NOT NULL
    DROP TABLE dbo.ProductConcept;
GO

CREATE TABLE dbo.ProductConcept (
    ProductConceptID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                 -- FK to the Indexing Section (48779-3)
    ConceptCode VARCHAR(36) NULL,       -- The computed MD5 hash code for the concept (8-4-4-4-12 format)
    ConceptCodeSystem VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.3.3389', -- OID for Product Concept Codes
    ConceptType VARCHAR(20) NULL,       -- 'AbstractProduct', 'AbstractKit', 'ApplicationProduct', 'ApplicationKit'
    -- FormCode info only needed for Abstract concepts
    FormCode VARCHAR(50) NULL,              -- Dosage Form code for Abstract concepts
    FormCodeSystem VARCHAR(100) NULL,       -- Code system for FormCode
    FormDisplayName VARCHAR(255) NULL       -- Display name for FormCode
);
GO

-- Add Comments and Extended Properties for ProductConcept table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the definition of an abstract or application-specific product/kit concept. Based on Section 15.2.2, 15.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 155, 161' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept';
GO
-- ConceptCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The computed MD5 hash code identifying the product concept (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 155 (Para 15.2.2.1), Page 157 (15.2.4), Page 161 (Para 15.2.6.1), Page 162 (15.2.8)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptCode';
GO
-- ConceptType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Distinguishes Abstract Product/Kit concepts from Application-specific Product/Kit concepts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptType';
GO
-- FormCode, FormCodeSystem, FormDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage Form details, applicable only for Abstract Product concepts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'FormCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 156, Para 15.2.2.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'FormCode';
GO

-- Modify Ingredient table to allow linking to ProductConceptID
PRINT 'Modifying Ingredient Table for Product Concept linkage...';
GO
ALTER TABLE dbo.Ingredient ADD ProductConceptID INT NULL;
GO
ALTER TABLE dbo.Ingredient ADD CONSTRAINT CK_Ingredient_ProductOrConcept CHECK (
    (ProductID IS NOT NULL AND ProductConceptID IS NULL) OR (ProductID IS NULL AND ProductConceptID IS NOT NULL)
);
GO
-- Update Ingredient comment/properties
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Represents an ingredient instance within a product, part, or product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.4, 15.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient';
GO
EXEC sys.sp_updateextendedproperty @name=N'SPL_Reference', @value=N'Page 43, 156' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient';
GO
-- Add property for ProductConceptID column
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ProductConceptID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 156' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ProductConceptID';
GO

-- Modify MarketingCategory table to allow linking to ProductConceptID
PRINT 'Modifying MarketingCategory Table for Product Concept linkage...';
GO
ALTER TABLE dbo.MarketingCategory ADD ProductConceptID INT NULL;
GO
ALTER TABLE dbo.MarketingCategory ADD CONSTRAINT CK_MarketingCategory_ProductOrConcept CHECK (
    (ProductID IS NOT NULL AND ProductConceptID IS NULL) OR (ProductID IS NULL AND ProductConceptID IS NOT NULL)
);
GO
-- Update MarketingCategory comment/properties
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Stores marketing category and application/monograph information for a product, part, or application product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.7, 15.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory';
GO
EXEC sys.sp_updateextendedproperty @name=N'SPL_Reference', @value=N'Page 54, 162' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory';
GO
-- Add property for ProductConceptID column
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductConcept, used when the marketing category applies to an Application Product Concept instead of a concrete Product.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ProductConceptID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 162' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ProductConceptID';
GO

-- ============================================================================
-- Table: ProductConceptEquivalence
-- Purpose: Links an Application Product Concept to its corresponding Abstract Product Concept. Based on Section 15.2.6.
-- ============================================================================
IF OBJECT_ID('dbo.ProductConceptEquivalence', 'U') IS NOT NULL
    DROP TABLE dbo.ProductConceptEquivalence;
GO

CREATE TABLE dbo.ProductConceptEquivalence (
    ProductConceptEquivalenceID INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationProductConceptID INT  NULL, -- FK to ProductConcept (The Application concept)
    AbstractProductConceptID INT  NULL,    -- FK to ProductConcept (The Abstract concept it derives from)
    EquivalenceCode VARCHAR(10) NULL,    -- Code: 'A', 'B', 'OTC', or 'N'
    EquivalenceCodeSystem VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.3.2964' -- OID for this code system
);
GO

-- Add Comments and Extended Properties for ProductConceptEquivalence table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an Application Product Concept to its corresponding Abstract Product Concept (<asEquivalentEntity>). Based on Section 15.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 161' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence';
GO
-- EquivalenceCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the relationship type between Application and Abstract concepts (A, B, OTC, N).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence', @level2type=N'COLUMN',@level2name=N'EquivalenceCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 161, Para 15.2.6.6, 15.2.6.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence', @level2type=N'COLUMN',@level2name=N'EquivalenceCode';
GO

-- Update DocumentRelationship comment/properties to include link to Bulk Lot Manufacturer
EXEC sys.sp_updateextendedproperty @name=N'RelationshipType', @value=N'Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3), RegistrantToEstablishment (4.1.4), EstablishmentToUSagent (6.1.4), EstablishmentToImporter (6.1.5), LabelerToDetails (5.1.3), FacilityToParentCompany (35.1.6), LabelerToParentCompany (36.1.2.5), DocumentToBulkLotManufacturer (16.1.3).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship', @level2type=N'COLUMN',@level2name=N'RelationshipType';
GO

-- ============================================================================
-- Table: LotIdentifier
-- Purpose: Stores Lot Number and its associated globally unique root OID. Used in Section 16.
-- ============================================================================
IF OBJECT_ID('dbo.LotIdentifier', 'U') IS NOT NULL
    DROP TABLE dbo.LotIdentifier;
GO

CREATE TABLE dbo.LotIdentifier (
    LotIdentifierID INT IDENTITY(1,1) PRIMARY KEY,
    LotNumber VARCHAR(100) NULL,  -- The alphanumeric lot number string (<id extension>)
    LotRootOID VARCHAR(100) NULL  -- The computed globally unique root OID (<id root>)
);
GO
-- Add Comments and Extended Properties for LotIdentifier table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores Lot Number and its associated globally unique root OID. Based on Section 16.2.5, 16.2.6, 16.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, 170, 171' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier';
GO
-- LotNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The lot number string.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168 (Para 16.2.5.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotNumber';
GO
-- LotRootOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The computed globally unique root OID for the lot number.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotRootOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 169 (Para 16.2.5.5), Page 170 (Para 16.2.6.3), Page 171 (Para 16.2.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotRootOID';
GO

-- ============================================================================
-- Table: ProductInstance
-- Purpose: Represents an instance of a product, specifically Fill Lots, Label Lots, or Package Lots in Lot Distribution Reports. Based on Section 16.2.5, 16.2.7, 16.2.11.
-- ============================================================================
IF OBJECT_ID('dbo.ProductInstance', 'U') IS NOT NULL
    DROP TABLE dbo.ProductInstance;
GO

CREATE TABLE dbo.ProductInstance (
    ProductInstanceID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                -- FK to Product (The product definition this is an instance of)
    InstanceType VARCHAR(20) NULL,     -- 'FillLot', 'LabelLot', 'PackageLot'
    LotIdentifierID INT  NULL,          -- FK to LotIdentifier
    ExpirationDate DATE NULL               -- Expiration date (<expirationTime><high value>), applicable mainly to LabelLot
);
GO
-- Add Comments and Extended Properties for ProductInstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents an instance of a product (Fill Lot, Label Lot, Package Lot) in Lot Distribution Reports. Based on Section 16.2.5, 16.2.7, 16.2.11.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, 171, 175' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance';
GO
-- InstanceType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of lot instance: FillLot, LabelLot, or PackageLot (for kits).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'InstanceType';
GO
-- ExpirationDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Expiration date, typically for Label Lots.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 171 (Para 16.2.7.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO


-- Modify PackagingLevel table to allow linking to ProductInstanceID for Lot Distribution Container Data
PRINT 'Modifying PackagingLevel Table for Product Instance linkage...';
GO
ALTER TABLE dbo.PackagingLevel ADD ProductInstanceID INT NULL; -- FK to ProductInstance (LabelLot)
GO
-- Update PackagingLevel comment/properties
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Represents a level of packaging (<asContent>/<containerPackagedProduct>). Links to ProductID/PartProductID for definitions OR ProductInstanceID for lot distribution container data (16.2.8).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel';
GO
-- Add property for ProductInstanceID column
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductInstance, used when the packaging details describe a container linked to a specific Label Lot instance (Lot Distribution).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductInstanceID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 172' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductInstanceID';
GO


-- ============================================================================
-- Table: DosingSpecification
-- Purpose: Stores dosing specification for Lot Distribution calculations. Based on Section 16.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.DosingSpecification', 'U') IS NOT NULL
    DROP TABLE dbo.DosingSpecification;
GO

CREATE TABLE dbo.DosingSpecification (
    DosingSpecificationID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,                 -- FK to Product
    RouteCode VARCHAR(50) NULL,             -- Route of Administration code
    RouteCodeSystem VARCHAR(100) NULL,      -- Code system for RouteCode
    RouteDisplayName VARCHAR(255) NULL,     -- Display name for RouteCode
    DoseQuantityValue DECIMAL(18, 9) NULL,  -- Dose quantity value (<doseQuantity value>)
    DoseQuantityUnit VARCHAR(50) NULL       -- Dose quantity unit (<doseQuantity unit>)
);
GO
-- Add Comments and Extended Properties for DosingSpecification table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores dosing specification for Lot Distribution calculations (<consumedIn><substanceAdministration1>). Based on Section 16.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification';
GO
-- RouteCode, RouteCodeSystem, RouteDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Route of administration associated with the dose.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'RouteCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, Para 16.2.4.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'RouteCode';
GO
-- DoseQuantityValue, DoseQuantityUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit representing a single dose.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'DoseQuantityValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, Para 16.2.4.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'DoseQuantityValue';
GO

-- ============================================================================
-- Table: IngredientInstance
-- Purpose: Represents Bulk Lot information in Lot Distribution Reports. Based on Section 16.2.6.
-- ============================================================================
IF OBJECT_ID('dbo.IngredientInstance', 'U') IS NOT NULL
    DROP TABLE dbo.IngredientInstance;
GO

CREATE TABLE dbo.IngredientInstance (
    IngredientInstanceID INT IDENTITY(1,1) PRIMARY KEY,
    FillLotInstanceID INT  NULL,          -- FK to ProductInstance (The Fill Lot this bulk lot contributes to)
    IngredientSubstanceID INT  NULL,      -- FK to IngredientSubstance (The substance of the bulk lot)
    LotIdentifierID INT  NULL,            -- FK to LotIdentifier (The Bulk Lot number)
    ManufacturerOrganizationID INT  NULL  -- FK to Organization (The manufacturer of the bulk lot)
);
GO
-- Add Comments and Extended Properties for IngredientInstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents Bulk Lot information in Lot Distribution Reports (<productInstance><ingredient>). Based on Section 16.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 169' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance';
GO
-- IngredientSubstanceID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Reference to the substance constituting the bulk lot.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 170, Para 16.2.6.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID';
GO
-- ManufacturerOrganizationID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Reference to the Organization that manufactured the bulk lot.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'ManufacturerOrganizationID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 170, Para 16.2.6.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'ManufacturerOrganizationID';
GO


-- ============================================================================
-- Table: LotHierarchy
-- Purpose: Defines the relationship between Fill Lots and Label Lots, or Package Lots and Label Lots. Based on Section 16.2.7, 16.2.11.
-- ============================================================================
IF OBJECT_ID('dbo.LotHierarchy', 'U') IS NOT NULL
    DROP TABLE dbo.LotHierarchy;
GO

CREATE TABLE dbo.LotHierarchy (
    LotHierarchyID INT IDENTITY(1,1) PRIMARY KEY,
    ParentInstanceID INT  NULL, -- FK to ProductInstance (The Fill Lot or Package Lot)
    ChildInstanceID INT  NULL   -- FK to ProductInstance (The Label Lot which is a member)
);
GO
-- Add Comments and Extended Properties for LotHierarchy table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the relationship between Fill/Package Lots and Label Lots (<productInstance><member><memberProductInstance>). Based on Section 16.2.7, 16.2.11.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotHierarchy';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 171, 175' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotHierarchy';
GO


-- ============================================================================
-- Table: ProductEvent
-- Purpose: Stores information about product events, like distribution or return quantities for Lot Distribution Reports. Based on Section 16.2.9, 16.2.10.
-- ============================================================================
IF OBJECT_ID('dbo.ProductEvent', 'U') IS NOT NULL
    DROP TABLE dbo.ProductEvent;
GO

CREATE TABLE dbo.ProductEvent (
    ProductEventID INT IDENTITY(1,1) PRIMARY KEY,
    PackagingLevelID INT  NULL,           -- FK to PackagingLevel (The container level the event applies to)
    EventCode VARCHAR(50) NULL,        -- Code for the event type (<productEvent><code> code)
    EventCodeSystem VARCHAR(100) NULL, -- Code system for EventCode
    EventDisplayName VARCHAR(255) NULL,-- Display name for EventCode
    QuantityValue INT  NULL,            -- Quantity associated with the event (<quantity value>)
    QuantityUnit VARCHAR(50) NULL,         -- Unit for quantity (usually '1' or null)
    EffectiveTimeLow DATE NULL             -- Start date/Initial date for the event (<effectiveTime><low value>), used for Distribution
);
GO
-- Add Comments and Extended Properties for ProductEvent table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores product events like distribution or return quantities (<subjectOf><productEvent>). Based on Section 16.2.9, 16.2.10.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173, 174' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent';
GO
-- EventCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the type of event (e.g., C106325 Distributed, C106328 Returned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EventCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173 (Para 16.2.9.5), Page 174 (Para 16.2.10.1)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EventCode';
GO
-- QuantityValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Integer quantity associated with the event (e.g., number of containers distributed/returned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'QuantityValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173 (Para 16.2.9.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'QuantityValue';
GO
-- EffectiveTimeLow
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Effective date (low value), used for Initial Distribution Date.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 174 (Para 16.2.9.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow';
GO

PRINT 'Chunk 10 complete.';
GO

-- #############################################################################
-- Chunk 11: WDD/3PL Reports, 40 CFR 180 Tolerance (Sec 18, 19)
-- #############################################################################

PRINT 'Creating WDD/3PL Report and 40 CFR 180 Tolerance Tables...';
GO

-- ============================================================================
-- Table: NamedEntity
-- Purpose: Stores "Doing Business As" (DBA) names for an Organization (Facility in WDD/3PL reports). Based on Section 18.1.3.
-- ============================================================================
IF OBJECT_ID('dbo.NamedEntity', 'U') IS NOT NULL
    DROP TABLE dbo.NamedEntity;
GO

CREATE TABLE dbo.NamedEntity (
    NamedEntityID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationID INT  NULL,             -- FK to Organization (The facility)
    EntityTypeCode VARCHAR(50) NULL,     -- Code for entity type (e.g., 'C117113' for DBA)
    EntityTypeCodeSystem VARCHAR(100) NULL,-- Code system for EntityTypeCode
    EntityTypeDisplayName VARCHAR(255) NULL,-- Display name for EntityTypeCode
    EntityName VARCHAR(500) NULL,        -- The DBA Name (<name>)
    EntitySuffix VARCHAR(20) NULL            -- Optional suffix, e.g., ' [WDD]', ' [3PL]'
);
GO

-- Add Comments and Extended Properties for NamedEntity table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores "Doing Business As" (DBA) names or other named entity types associated with an Organization (<asNamedEntity>). Based on Section 2.1.9, 18.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, 178' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity';
GO
-- EntityTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the type of named entity, e.g., C117113 for "doing business as".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20 (Para 2.1.9.1), Page 179 (Para 18.1.3.10)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityTypeCode';
GO
-- EntityName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The name of the entity, e.g., the DBA name.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20 (Para 2.1.9.2), Page 179 (Para 18.1.3.11)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityName';
GO
-- EntitySuffix
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional suffix used with DBA names in WDD/3PL reports to indicate business type ([WDD] or [3PL]).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntitySuffix';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 179, Para 18.1.3.12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntitySuffix';
GO


-- ============================================================================
-- Table: TerritorialAuthority
-- Purpose: Represents the issuing authority (State or Federal Agency like DEA) for licenses. Based on Section 18.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.TerritorialAuthority', 'U') IS NOT NULL
    DROP TABLE dbo.TerritorialAuthority;
GO

CREATE TABLE dbo.TerritorialAuthority (
    TerritorialAuthorityID INT IDENTITY(1,1) PRIMARY KEY,
    TerritoryCode VARCHAR(10) NULL,       -- State (e.g., 'US-MD') or Country ('USA') code (<territory><code>)
    TerritoryCodeSystem VARCHAR(50) NULL, -- Code system (e.g., '1.0.3166.2' for state, '1.0.3166.1.2.3' for country)
    GoverningAgencyOrgID INT NULL             -- Optional FK to Organization (if federal agency like DEA)
);
GO
-- Add Comments and Extended Properties for TerritorialAuthority table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the issuing authority (State or Federal Agency like DEA) for licenses (<author><territorialAuthority>). Based on Section 18.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 181' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority';
GO
-- TerritoryCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'TerritoryCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 181, Para 18.1.5.5, 18.1.5.23' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'TerritoryCode';
GO
-- GoverningAgencyOrgID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the Organization representing the federal governing agency, if applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'GoverningAgencyOrgID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 181, Para 18.1.5.24' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'GoverningAgencyOrgID';
GO

-- ============================================================================
-- Table: License
-- Purpose: Stores license information for WDD/3PL facilities. Based on Section 18.1.5.
-- ============================================================================
IF OBJECT_ID('dbo.License', 'U') IS NOT NULL
    DROP TABLE dbo.License;
GO

CREATE TABLE dbo.License (
    LicenseID INT IDENTITY(1,1) PRIMARY KEY,
    BusinessOperationID INT  NULL,         -- FK to BusinessOperation (The WDD/3PL operation being licensed)
    LicenseNumber VARCHAR(100) NULL,    -- License number (<id extension>)
    LicenseRootOID VARCHAR(100) NULL,   -- State/Agency OID (<id root>)
    LicenseTypeCode VARCHAR(50) NULL,   -- Code for license type (<approval><code> code)
    LicenseTypeCodeSystem VARCHAR(100) NULL, -- Code system for LicenseTypeCode
    LicenseTypeDisplayName VARCHAR(255) NULL,-- Display name for LicenseTypeCode
    StatusCode VARCHAR(20) NULL,        -- License status ('active', 'suspended', 'aborted', 'completed') (<statusCode code>)
    ExpirationDate DATE NULL,               -- License expiration date (<effectiveTime><high value>)
    TerritorialAuthorityID INT  NULL     -- FK to TerritorialAuthority (Issuing state/agency)
);
GO
-- Add Comments and Extended Properties for License table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores license information for WDD/3PL facilities (<subjectOf><approval>). Based on Section 18.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License';
GO
-- LicenseNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The license number string.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseNumber';
GO
-- LicenseRootOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID identifying the issuing authority and context.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseRootOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 182 (Para 18.1.5.8), Page 183 (Para 18.1.5.16-22, 18.1.5.27)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseRootOID';
GO
-- LicenseTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of approval/license (e.g., C118777 licensing).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseTypeCode';
GO
-- StatusCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Status of the license: active, suspended, aborted (revoked), completed (expired).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'StatusCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 182, Para 18.1.5.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'StatusCode';
GO
-- ExpirationDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Expiration date of the license.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO

-- ============================================================================
-- Table: AttachedDocument
-- Purpose: Stores references to attached documents (e.g., PDFs for Disciplinary Actions, REMS Materials).
-- ============================================================================
IF OBJECT_ID('dbo.AttachedDocument', 'U') IS NOT NULL
    DROP TABLE dbo.AttachedDocument;
GO

CREATE TABLE dbo.AttachedDocument (
    AttachedDocumentID INT IDENTITY(1,1) PRIMARY KEY,
    ParentEntityType VARCHAR(50) NULL, -- e.g., 'DisciplinaryAction', 'REMSMaterial'
    ParentEntityID INT  NULL,         -- FK to the parent table (e.g., DisciplinaryActionID)
    MediaType VARCHAR(100) NULL,     -- Media type (e.g., 'application/pdf')
    FileName VARCHAR(255) NULL       -- File name referenced in the SPL (<reference value=>)
);
GO
-- Add Comments and Extended Properties for AttachedDocument table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to attached documents (e.g., PDFs for Disciplinary Actions, REMS Materials).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (18.1.7), Page 218 (23.2.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument';
GO
-- ParentEntityType, ParentEntityID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifies the type and ID of the parent element containing the document reference.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'ParentEntityType';
GO
-- MediaType
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'MIME type of the attached document.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'MediaType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (Para 18.1.7.16), Page 218 (Para 23.2.9.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'MediaType';
GO
-- FileName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'File name of the attached document.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'FileName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (Para 18.1.7.17), Page 218 (Para 23.2.9.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'FileName';
GO

-- ============================================================================
-- Table: DisciplinaryAction
-- Purpose: Stores disciplinary action details related to a License. Based on Section 18.1.7.
-- ============================================================================
IF OBJECT_ID('dbo.DisciplinaryAction', 'U') IS NOT NULL
    DROP TABLE dbo.DisciplinaryAction;
GO

CREATE TABLE dbo.DisciplinaryAction (
    DisciplinaryActionID INT IDENTITY(1,1) PRIMARY KEY,
    LicenseID INT  NULL,                -- FK to License
    ActionCode VARCHAR(50) NULL,       -- Code for the action type (<action><code> code)
    ActionCodeSystem VARCHAR(100) NULL, -- Code system for ActionCode
    ActionDisplayName VARCHAR(255) NULL,-- Display name for ActionCode
    EffectiveTime DATE NULL,           -- Date the action took effect (<effectiveTime value>)
    ActionText NVARCHAR(MAX) NULL          -- Text description if action code is 'other' (<text xsi:type="ST">)
);
GO
-- Add Comments and Extended Properties for DisciplinaryAction table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores disciplinary action details related to a License (<approval><subjectOf><action>). Based on Section 18.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 184' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction';
GO
-- ActionCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the disciplinary action type (e.g., suspension, revocation, activation).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionCode';
GO
-- EffectiveTime
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the disciplinary action became effective.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'EffectiveTime';
GO
-- ActionText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description used when the action code is ''other''.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionText';
GO

-- ============================================================================
-- Table: SubstanceSpecification (for 40 CFR 180)
-- Purpose: Stores substance specification details for tolerance documents. Based on Section 19.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.SubstanceSpecification', 'U') IS NOT NULL
    DROP TABLE dbo.SubstanceSpecification;
GO

CREATE TABLE dbo.SubstanceSpecification (
    SubstanceSpecificationID INT IDENTITY(1,1) PRIMARY KEY,
    IdentifiedSubstanceID INT  NULL,      -- FK to IdentifiedSubstance (The substance subject to tolerance)
    SpecCode VARCHAR(100) NULL,        -- Specification code ('40-CFR-...' format)
    SpecCodeSystem VARCHAR(100) NULL, -- Code system (2.16.840.1.113883.3.149)
    EnforcementMethodCode VARCHAR(50) NULL, -- Optional code for Enforcement Analytical Method
    EnforcementMethodCodeSystem VARCHAR(100) NULL,
    EnforcementMethodDisplayName VARCHAR(255) NULL
);
GO
-- Add Comments and Extended Properties for SubstanceSpecification table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores substance specification details for tolerance documents (<subjectOf><substanceSpecification>). Based on Section 19.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 189' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification';
GO
-- SpecCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Specification code, format 40-CFR-...' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'SpecCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 190, Para 19.2.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'SpecCode';
GO
-- EnforcementMethodCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the Enforcement Analytical Method used (<observation><code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'EnforcementMethodCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 190, Para 19.2.3.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'EnforcementMethodCode';
GO

-- ============================================================================
-- Table: Analyte
-- Purpose: Links a Substance Specification to the analyte(s) being measured. Based on Section 19.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.Analyte', 'U') IS NOT NULL
    DROP TABLE dbo.Analyte;
GO

CREATE TABLE dbo.Analyte (
    AnalyteID INT IDENTITY(1,1) PRIMARY KEY,
    SubstanceSpecificationID INT  NULL, -- FK to SubstanceSpecification
    AnalyteSubstanceID INT  NULL      -- FK to IdentifiedSubstance (The substance being measured)
);
GO
-- Add Comments and Extended Properties for Analyte table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Substance Specification to the analyte(s) being measured (<analyte><identifiedSubstance>). Based on Section 19.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Analyte';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 189, 190 (Para 19.2.3.13)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Analyte';
GO

-- ============================================================================
-- Table: Commodity
-- Purpose: Stores commodity details referenced in tolerance specifications. Based on Section 19.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.Commodity', 'U') IS NOT NULL
    DROP TABLE dbo.Commodity;
GO

CREATE TABLE dbo.Commodity (
    CommodityID INT IDENTITY(1,1) PRIMARY KEY,
    CommodityCode VARCHAR(50) NULL,       -- Code for the commodity (<presentSubstance><code> code)
    CommodityCodeSystem VARCHAR(100) NULL,-- Code system for CommodityCode (2.16.840.1.113883.6.275.1)
    CommodityDisplayName VARCHAR(255) NULL,-- Display name for CommodityCode
    CommodityName NVARCHAR(1000) NULL       -- Optional name (<presentSubstance><name>)
);
GO
-- Add Comments and Extended Properties for Commodity table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores commodity details referenced in tolerance specifications (<subject><presentSubstance><presentSubstance>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity';
GO
-- CommodityCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the commodity.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity', @level2type=N'COLUMN',@level2name=N'CommodityCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity', @level2type=N'COLUMN',@level2name=N'CommodityCode';
GO

-- ============================================================================
-- Table: ApplicationType (for 40 CFR 180)
-- Purpose: Stores application type details referenced in tolerance specifications. Based on Section 19.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.ApplicationType', 'U') IS NOT NULL
    DROP TABLE dbo.ApplicationType;
GO

CREATE TABLE dbo.ApplicationType (
    ApplicationTypeID INT IDENTITY(1,1) PRIMARY KEY,
    AppTypeCode VARCHAR(50) NULL,       -- Code for the application type (<approval><code> code)
    AppTypeCodeSystem VARCHAR(100) NULL,-- Code system for AppTypeCode (2.16.840.1.113883.6.275.1)
    AppTypeDisplayName VARCHAR(255) NULL -- Display name for AppTypeCode
);
GO
-- Add Comments and Extended Properties for ApplicationType table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores application type details referenced in tolerance specifications (<subjectOf><approval><code>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType';
GO
-- AppTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the application type (e.g., General Tolerance).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType', @level2type=N'COLUMN',@level2name=N'AppTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType', @level2type=N'COLUMN',@level2name=N'AppTypeCode';
GO

-- ============================================================================
-- Table: ObservationCriterion (for 40 CFR 180)
-- Purpose: Stores the tolerance range and related details for a substance specification. Based on Section 19.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.ObservationCriterion', 'U') IS NOT NULL
    DROP TABLE dbo.ObservationCriterion;
GO

CREATE TABLE dbo.ObservationCriterion (
    ObservationCriterionID INT IDENTITY(1,1) PRIMARY KEY,
    SubstanceSpecificationID INT  NULL,    -- FK to SubstanceSpecification
    ToleranceHighValue DECIMAL(18, 9) NULL,-- Tolerance limit (<value><high value>)
    ToleranceHighUnit VARCHAR(10) NULL DEFAULT '[ppm]', -- Tolerance unit (<value><high unit>)
    CommodityID INT NULL,                  -- Optional FK to Commodity
    ApplicationTypeID INT  NULL,          -- FK to ApplicationType
    ExpirationDate DATE NULL,                -- Optional expiration/revocation date (<effectiveTime><high value>)
    TextNote NVARCHAR(MAX) NULL            -- Optional text note (<text>)
);
GO
-- Add Comments and Extended Properties for ObservationCriterion table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the tolerance range and related details (<referenceRange><observationCriterion>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion';
GO
-- ToleranceHighValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The upper limit of the tolerance range in ppm.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ToleranceHighValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.4, 19.2.4.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ToleranceHighValue';
GO
-- CommodityID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional link to the specific commodity the tolerance applies to.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'CommodityID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'CommodityID';
GO
-- ApplicationTypeID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the type of application associated with this tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ApplicationTypeID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ApplicationTypeID';
GO
-- ExpirationDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional expiration or revocation date for the tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.18' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ExpirationDate';
GO
-- TextNote
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional text annotation about the tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'TextNote';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'TextNote';
GO


PRINT 'Chunk 11 complete.';
GO

-- #############################################################################
-- Chunk 12: Biologic/Drug Substance Indexing, Warning Letter Indexing (Sec 20, 21)
-- #############################################################################

PRINT 'Creating Biologic/Drug Substance and Warning Letter Indexing Tables...';
GO

-- ============================================================================
-- Table: SpecifiedSubstance
-- Purpose: Stores the specified substance code and name linked to an ingredient in Biologic/Drug Substance Indexing docs. Based on Section 20.2.6.
-- Note: Represents the <subjectOf><substanceSpecification> element under <ingredient>.
-- ============================================================================
IF OBJECT_ID('dbo.SpecifiedSubstance', 'U') IS NOT NULL
    DROP TABLE dbo.SpecifiedSubstance;
GO

CREATE TABLE dbo.SpecifiedSubstance (
    SpecifiedSubstanceID INT IDENTITY(1,1) PRIMARY KEY,
    IngredientID INT  NULL,                 -- FK to Ingredient (The ingredient being specified)
    SubstanceCode VARCHAR(100) NULL,       -- The specified substance code (<code> code)
    SubstanceCodeSystem VARCHAR(100) NULL, -- Code system (2.16.840.1.113883.3.6277)
    SubstanceDisplayName VARCHAR(255) NULL -- Display name matching the code (<code> displayName)
);
GO

-- Add Comments and Extended Properties for SpecifiedSubstance table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the specified substance code and name linked to an ingredient in Biologic/Drug Substance Indexing documents. Based on Section 20.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 197' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance';
GO
-- SubstanceCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The code assigned to the specified substance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCode';
GO
-- SubstanceCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the specified substance code (2.16.840.1.113883.3.6277).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystem';
GO
-- SubstanceDisplayName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the specified substance code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceDisplayName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceDisplayName';
GO


-- ============================================================================
-- Table: WarningLetterProductInfo
-- Purpose: Stores key product identification details referenced in a Warning Letter Alert Indexing document. Based on Section 21.2.2.
-- ============================================================================
IF OBJECT_ID('dbo.WarningLetterProductInfo', 'U') IS NOT NULL
    DROP TABLE dbo.WarningLetterProductInfo;
GO

CREATE TABLE dbo.WarningLetterProductInfo (
    WarningLetterProductInfoID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                 -- FK to the Indexing Section (48779-3)
    ProductName NVARCHAR(500) NULL,         -- Proprietary name (<name>)
    GenericName NVARCHAR(512) NULL,     -- Non-proprietary name (<genericMedicine><name>)
    FormCode VARCHAR(50) NULL,          -- Dosage Form code (<formCode code>)
    FormCodeSystem VARCHAR(100) NULL,   -- Dosage Form code system
    FormDisplayName VARCHAR(255) NULL,  -- Dosage Form display name
    -- Storing strength and item codes as text for simplicity in this context
    StrengthText NVARCHAR(1000) NULL,       -- Text representation of strength(s)
    ItemCodesText NVARCHAR(1000) NULL       -- Text representation of relevant item code(s)
);
GO

-- Add Comments and Extended Properties for WarningLetterProductInfo table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores key product identification details referenced in a Warning Letter Alert Indexing document. Based on Section 21.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo';
GO
-- ProductName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Proprietary name of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ProductName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ProductName';
GO
-- GenericName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Generic name of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'GenericName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'GenericName';
GO
-- FormCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage form code of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'FormCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'FormCode';
GO
-- StrengthText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the ingredient strength(s).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'StrengthText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'StrengthText';
GO
-- ItemCodesText
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the product item code(s) (e.g., NDC).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ItemCodesText';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ItemCodesText';
GO


-- ============================================================================
-- Table: WarningLetterDate
-- Purpose: Stores the issue date and optional resolution date for a warning letter alert. Based on Section 21.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.WarningLetterDate', 'U') IS NOT NULL
    DROP TABLE dbo.WarningLetterDate;
GO

CREATE TABLE dbo.WarningLetterDate (
    WarningLetterDateID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,           -- FK to the Indexing Section (48779-3)
    AlertIssueDate DATE NULL,     -- Date the warning letter alert was issued (<effectiveTime><low value>)
    ResolutionDate DATE NULL          -- Optional date the issue was resolved (<effectiveTime><high value>)
);
GO

-- Add Comments and Extended Properties for WarningLetterDate table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the issue date and optional resolution date for a warning letter alert. Based on Section 21.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate';
GO
-- AlertIssueDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the warning letter alert was issued.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'AlertIssueDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202, Para 21.2.3.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'AlertIssueDate';
GO
-- ResolutionDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the issue described in the warning letter was resolved, if applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'ResolutionDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202, Para 21.2.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'ResolutionDate';
GO


PRINT 'Chunk 12 complete.';
GO

-- #############################################################################
-- Chunk 13: REMS Document Specifics (Section 23)
-- #############################################################################

PRINT 'Creating REMS Document Specific Tables...';
GO

-- Note: REMS Document Header elements (Type, Author, Related Document) are handled by existing tables.
-- Note: REMS Sections (Goals, Requirements, ETASU, Communication Plan, etc.) are handled by the Section table using specific LOINC codes.
-- Note: REMS Product details (Name, Generic Name) are stored in the Product table. Marketing Category/Application Number use the MarketingCategory table.

-- ============================================================================
-- Table: Holder
-- Purpose: Stores the Application Holder organization linked to a Marketing Category for REMS products. Based on Section 23.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.Holder', 'U') IS NOT NULL
    DROP TABLE dbo.Holder;
GO

CREATE TABLE dbo.Holder (
    HolderID INT IDENTITY(1,1) PRIMARY KEY,
    MarketingCategoryID INT  NULL, -- FK to MarketingCategory
    HolderOrganizationID INT  NULL -- FK to Organization (The Application Holder)
);
GO
-- Add Comments and Extended Properties for Holder table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the Application Holder organization linked to a Marketing Category for REMS products (<holder><role><playingOrganization>). Based on Section 23.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 208' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder';
GO
-- HolderOrganizationID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the Organization table for the Application Holder.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder', @level2type=N'COLUMN',@level2name=N'HolderOrganizationID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 208, Para 23.2.3.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder', @level2type=N'COLUMN',@level2name=N'HolderOrganizationID';
GO

-- ============================================================================
-- Table: Protocol
-- Purpose: Represents a REMS protocol defined within a section. Based on Section 23.2.6.
-- Note: Captures the <code> element under <protocol>.
-- ============================================================================
IF OBJECT_ID('dbo.Protocol', 'U') IS NOT NULL
    DROP TABLE dbo.Protocol;
GO

CREATE TABLE dbo.Protocol (
    ProtocolID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,               -- FK to the Section containing the protocol definition
    ProtocolCode VARCHAR(50) NULL,    -- Code identifying the protocol type
    ProtocolCodeSystem VARCHAR(100) NULL, -- Code system for ProtocolCode
    ProtocolDisplayName VARCHAR(255) NULL -- Display name for ProtocolCode
    -- The link to substanceAdministration is implicit via the Section. SequenceNumber 2 fixed per spec.
);
GO
-- Add Comments and Extended Properties for Protocol table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a REMS protocol defined within a section (<protocol> element). Based on Section 23.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 211' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol';
GO
-- ProtocolCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the REMS protocol type.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol', @level2type=N'COLUMN',@level2name=N'ProtocolCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 211, Para 23.2.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol', @level2type=N'COLUMN',@level2name=N'ProtocolCode';
GO

-- ============================================================================
-- Table: Stakeholder
-- Purpose: Lookup table for REMS stakeholder types. Based on Section 23.2.7.
-- ============================================================================
IF OBJECT_ID('dbo.Stakeholder', 'U') IS NOT NULL
    DROP TABLE dbo.Stakeholder;
GO

CREATE TABLE dbo.Stakeholder (
    StakeholderID INT IDENTITY(1,1) PRIMARY KEY,
    StakeholderCode VARCHAR(50) NULL UNIQUE, -- Code identifying the stakeholder type
    StakeholderCodeSystem VARCHAR(100) NULL, -- Code system for StakeholderCode
    StakeholderDisplayName VARCHAR(255) NULL -- Display name for StakeholderCode
);
GO
-- Add Comments and Extended Properties for Stakeholder table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Lookup table for REMS stakeholder types (<stakeholder>). Based on Section 23.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212, 216' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder';
GO
-- StakeholderCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the stakeholder role (e.g., prescriber, patient).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder', @level2type=N'COLUMN',@level2name=N'StakeholderCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.19, 23.2.7.20' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder', @level2type=N'COLUMN',@level2name=N'StakeholderCode';
GO

-- Pre-populate Stakeholder table if desired (example)
-- INSERT INTO dbo.Stakeholder (StakeholderCode, StakeholderCodeSystem, StakeholderDisplayName) VALUES ('C0SH01', '2.16.840.1.113883.3.26.1.1', 'prescriber');
-- INSERT INTO dbo.Stakeholder (StakeholderCode, StakeholderCodeSystem, StakeholderDisplayName) VALUES ('C0SH02', '2.16.840.1.113883.3.26.1.1', 'patient');
-- ... add other stakeholder codes from list ...
-- GO

-- ============================================================================
-- Table: REMSMaterial
-- Purpose: Stores references to REMS materials (e.g., PDFs). Based on Section 23.2.9.
-- ============================================================================
IF OBJECT_ID('dbo.REMSMaterial', 'U') IS NOT NULL
    DROP TABLE dbo.REMSMaterial;
GO

CREATE TABLE dbo.REMSMaterial (
    REMSMaterialID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,              -- FK to the REMS Material Section (82346-8)
    MaterialDocumentGUID UNIQUEIDENTIFIER NULL, -- Document ID for the material (<document><id root>)
    Title NVARCHAR(MAX) NULL,        -- Title of the material (<document><title>)
    TitleReference VARCHAR(100) NULL,    -- Link ID within title (<reference value=> pointing to text)
    AttachedDocumentID INT NULL          -- FK to AttachedDocument (if material is an attached file like PDF)
);
GO
-- Add Comments and Extended Properties for REMSMaterial table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to REMS materials, linking to attached documents if applicable (<subjectOf><document>). Based on Section 23.2.9.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial';
GO
-- MaterialDocumentGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for this specific material document reference.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'MaterialDocumentGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'MaterialDocumentGUID';
GO
-- TitleReference
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Internal link ID (#...) embedded within the title, potentially linking to descriptive text.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'TitleReference';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'TitleReference';
GO
-- AttachedDocumentID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the AttachedDocument table if the material is provided as an attachment (e.g., PDF).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'AttachedDocumentID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'AttachedDocumentID';
GO


-- ============================================================================
-- Table: Requirement
-- Purpose: Represents a REMS requirement or monitoring observation within a protocol. Based on Section 23.2.7.
-- ============================================================================
IF OBJECT_ID('dbo.Requirement', 'U') IS NOT NULL
    DROP TABLE dbo.Requirement;
GO

CREATE TABLE dbo.Requirement (
    RequirementID INT IDENTITY(1,1) PRIMARY KEY,
    ProtocolID INT  NULL,                -- FK to Protocol
    RequirementSequenceNumber INT  NULL, -- Sequence relative to substance admin (1=before, 2=during, 3=after)
    IsMonitoringObservation BIT NOT NULL DEFAULT 0, -- Flag: True if <monitoringObservation>, False if <requirement>
    PauseQuantityValue DECIMAL(18, 9) NULL,-- Optional delay value (<pauseQuantity value>)
    PauseQuantityUnit VARCHAR(50) NULL,   -- Optional delay unit (<pauseQuantity unit>)
    RequirementCode VARCHAR(50) NULL,   -- Code for the requirement/observation
    RequirementCodeSystem VARCHAR(100) NULL, -- Code system for RequirementCode
    RequirementDisplayName VARCHAR(500) NULL,-- Display name for RequirementCode
    OriginalTextReference VARCHAR(100) NULL,-- Link ID pointing to text description (#...)
    PeriodValue DECIMAL(18, 9) NULL,      -- Optional repetition period value (<effectiveTime><period value>)
    PeriodUnit VARCHAR(50) NULL,          -- Optional repetition period unit (<effectiveTime><period unit>)
    StakeholderID INT  NULL,             -- FK to Stakeholder
    REMSMaterialID INT NULL                 -- Optional FK to REMSMaterial (referenced document)
);
GO
-- Add Comments and Extended Properties for Requirement table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a REMS requirement or monitoring observation within a protocol (<component><requirement> or <monitoringObservation>). Based on Section 23.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement';
GO
-- RequirementSequenceNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sequence number relative to the substance administration step (fixed at 2). 1=Before, 2=During/Concurrent, 3=After.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementSequenceNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 213, Para 23.2.7.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementSequenceNumber';
GO
-- PauseQuantityValue, PauseQuantityUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional delay (pause) relative to the start/end of the previous step.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PauseQuantityValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 213, Para 23.2.7.3, 23.2.7.4, 23.2.7.5, 23.2.7.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PauseQuantityValue';
GO
-- RequirementCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the specific requirement or monitoring observation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 215, Para 23.2.7.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementCode';
GO
-- OriginalTextReference
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link ID (#...) pointing to the corresponding text description in the REMS Summary or REMS Participant Requirements section.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'OriginalTextReference';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212, Page 215 (Para 23.2.7.12)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'OriginalTextReference';
GO
-- PeriodValue, PeriodUnit
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional repetition period for the requirement/observation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PeriodValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 215, Para 23.2.7.15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PeriodValue';
GO
-- StakeholderID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the stakeholder responsible for fulfilling the requirement.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'StakeholderID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.17' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'StakeholderID';
GO
-- REMSMaterialID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional link to a REMS Material document referenced by the requirement.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'REMSMaterialID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'REMSMaterialID';
GO


-- ============================================================================
-- Table: REMSApproval
-- Purpose: Stores the REMS approval details associated with the first protocol mention. Based on Section 23.2.8.
-- ============================================================================
IF OBJECT_ID('dbo.REMSApproval', 'U') IS NOT NULL
    DROP TABLE dbo.REMSApproval;
GO

CREATE TABLE dbo.REMSApproval (
    REMSApprovalID INT IDENTITY(1,1) PRIMARY KEY,
    ProtocolID INT  NULL,                 -- FK to the first Protocol defined in the document
    ApprovalCode VARCHAR(50) NULL,       -- Code for REMS Approval ('C128899')
    ApprovalCodeSystem VARCHAR(100) NULL, -- Code system for ApprovalCode
    ApprovalDisplayName VARCHAR(255) NULL,-- Display name for ApprovalCode
    ApprovalDate DATE NULL,              -- Initial REMS approval date (<effectiveTime><low value>)
    TerritoryCode CHAR(3) NULL           -- Territory code ('USA')
);
GO
-- Add Comments and Extended Properties for REMSApproval table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the REMS approval details associated with the first protocol mention (<subjectOf><approval>). Based on Section 23.2.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval';
GO
-- ApprovalCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for REMS Approval (C128899).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217, Para 23.2.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalCode';
GO
-- ApprovalDate
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date of the initial REMS program approval.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalDate';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217, Para 23.2.8.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalDate';
GO

-- ============================================================================
-- Table: REMSElectronicResource
-- Purpose: Stores references to REMS electronic resources (URLs or URNs). Based on Section 23.2.10.
-- ============================================================================
IF OBJECT_ID('dbo.REMSElectronicResource', 'U') IS NOT NULL
    DROP TABLE dbo.REMSElectronicResource;
GO

CREATE TABLE dbo.REMSElectronicResource (
    REMSElectronicResourceID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                 -- FK to the REMS Material Section (82346-8) where resource is listed
    ResourceDocumentGUID UNIQUEIDENTIFIER NULL, -- Document ID for the resource reference (<document><id root>)
    Title NVARCHAR(MAX) NULL,           -- Title of the resource (<document><title>)
    TitleReference VARCHAR(100) NULL,       -- Link ID within title (<reference value=> pointing to text)
    ResourceReferenceValue VARCHAR(2048) NULL -- The URL or URN (<text><reference value=>)
);
GO
-- Add Comments and Extended Properties for REMSElectronicResource table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to REMS electronic resources (URLs or URNs) (<subjectOf><document>). Based on Section 23.2.10.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource';
GO
-- ResourceDocumentGUID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for this specific electronic resource reference.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceDocumentGUID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219, Para 23.2.10.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceDocumentGUID';
GO
-- ResourceReferenceValue
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The URI (URL or URN) of the electronic resource.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceReferenceValue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219, Para 23.2.10.4, 23.2.10.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceReferenceValue';
GO


PRINT 'Chunk 13 complete.';
GO

-- #############################################################################
-- Chunk 14: Certification, Salvage, Compliance Actions, Drug/NCT Indexing (Sec 28-33)
-- #############################################################################

PRINT 'Creating Certification, Salvage, Compliance Action, and Indexing Tables...';
GO

-- Note: Blanket No Changes Certification (Section 28) uses existing tables Document, OrganizationIdentifier, LegalAuthenticator.
-- It requires linking Establishments (via DocumentRelationship) to certified Products (via ProductIdentifier).

-- ============================================================================
-- Table: CertificationProductLink
-- Purpose: Links an establishment (within a Certification doc) to a product being certified. Based on Section 28.1.3.
-- ============================================================================
IF OBJECT_ID('dbo.CertificationProductLink', 'U') IS NOT NULL
    DROP TABLE dbo.CertificationProductLink;
GO

CREATE TABLE dbo.CertificationProductLink (
    CertificationProductLinkID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentRelationshipID INT  NULL, -- FK to DocumentRelationship (linking Doc to certified Establishment)
    ProductIdentifierID INT  NULL     -- FK to ProductIdentifier (NDC or ISBT code being certified)
);
GO
-- Add Comments and Extended Properties for CertificationProductLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an establishment (within a Blanket No Changes Certification doc) to a product being certified (<performance><actDefinition><product>). Based on Section 28.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'CertificationProductLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 226' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'CertificationProductLink';
GO

-- Note: Human and Animal Salvaged Drug Products (Section 29) uses existing tables Document, Product, PackagingLevel, etc.
-- The specific business operation 'SALVAGE' (C70827) is stored in BusinessOperation.
-- Lot Number and Container Data (29.2.2, 29.2.3) are handled by ProductInstance and PackagingLevel linked to ProductInstance.
-- Update ProductInstance description to include SalvagedLot type.
EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=N'Represents an instance of a product (Fill Lot, Label Lot, Package Lot, Salvaged Lot) in Lot Distribution or Salvage Reports. Based on Section 16.2.5, 16.2.7, 16.2.11, 29.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance';
GO
EXEC sys.sp_updateextendedproperty @name=N'InstanceType', @value=N'Type of lot instance: FillLot, LabelLot, PackageLot (for kits), SalvagedLot.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'InstanceType';
GO

-- ============================================================================
-- Table: ComplianceAction
-- Purpose: Stores FDA-initiated inactivation/reactivation status for Drug Listings or Establishment Registrations. Based on Section 30.2.3, 31.1.4.
-- ============================================================================
IF OBJECT_ID('dbo.ComplianceAction', 'U') IS NOT NULL
    DROP TABLE dbo.ComplianceAction;
GO

CREATE TABLE dbo.ComplianceAction (
    ComplianceActionID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT NULL,                   -- FK to Section (for Drug Listing Inactivation - Section 30)
    PackageIdentifierID INT NULL,         -- FK to PackageIdentifier (for Drug Listing Inactivation - Section 30)
    DocumentRelationshipID INT NULL,      -- FK to DocumentRelationship (for Estab Reg Inactivation - Section 31)
    ActionCode VARCHAR(50) NULL,      -- Action code (e.g., C162847 Inactivated)
    ActionCodeSystem VARCHAR(100) NULL, -- Code system for ActionCode
    ActionDisplayName VARCHAR(255) NULL,-- Display name for ActionCode
    EffectiveTimeLow DATE NULL,         -- Inactivation date (<effectiveTime><low value>)
    EffectiveTimeHigh DATE NULL,          -- Optional Reactivation date (<effectiveTime><high value>)
    CONSTRAINT CK_ComplianceAction_Target CHECK (
        (PackageIdentifierID IS NOT NULL AND DocumentRelationshipID IS NULL) -- Drug Listing Target
        OR
        (PackageIdentifierID IS NULL AND DocumentRelationshipID IS NOT NULL) -- Establishment Target
    )
);
GO
-- Add Comments and Extended Properties for ComplianceAction table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores FDA-initiated inactivation/reactivation status for Drug Listings (linked via PackageIdentifierID) or Establishment Registrations (linked via DocumentRelationshipID). Based on Section 30.2.3, 31.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238, 242' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction';
GO
-- PackageIdentifierID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the specific package NDC being inactivated/reactivated (Section 30).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'PackageIdentifierID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 237' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'PackageIdentifierID';
GO
-- DocumentRelationshipID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the DocumentRelationship representing the establishment being inactivated/reactivated (Section 31).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 241' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipID';
GO
-- ActionCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the compliance action (e.g., C162847 Inactivated).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'ActionCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.2), Page 242 (Para 31.1.4.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'ActionCode';
GO
-- EffectiveTimeLow
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the inactivation begins.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.3), Page 242 (Para 31.1.4.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow';
GO
-- EffectiveTimeHigh
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the inactivation ends (reactivation date), if applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeHigh';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.1), Page 242 (Para 31.1.4.1)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeHigh';
GO

-- ============================================================================
-- Table: InteractionIssue
-- Purpose: Represents a drug interaction issue within a specific section. Based on Section 32.2.3.
-- ============================================================================
IF OBJECT_ID('dbo.InteractionIssue', 'U') IS NOT NULL
    DROP TABLE dbo.InteractionIssue;
GO

CREATE TABLE dbo.InteractionIssue (
    InteractionIssueID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,                  -- FK to Section where the interaction is mentioned
    InteractionCode VARCHAR(50) NULL,    -- Code for interaction ('C54708')
    InteractionCodeSystem VARCHAR(100) NULL, -- Code system
    InteractionDisplayName VARCHAR(255) NULL -- Display name ('INTERACTION')
);
GO
-- Add Comments and Extended Properties for InteractionIssue table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a drug interaction issue within a specific section (<subjectOf><issue>). Based on Section 32.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue';
GO
-- InteractionCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying an interaction issue (C54708).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue', @level2type=N'COLUMN',@level2name=N'InteractionCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247, Para 32.2.3.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue', @level2type=N'COLUMN',@level2name=N'InteractionCode';
GO

-- ============================================================================
-- Table: ContributingFactor
-- Purpose: Links an InteractionIssue to the contributing substance/class. Based on Section 32.2.4.
-- ============================================================================
IF OBJECT_ID('dbo.ContributingFactor', 'U') IS NOT NULL
    DROP TABLE dbo.ContributingFactor;
GO

CREATE TABLE dbo.ContributingFactor (
    ContributingFactorID INT IDENTITY(1,1) PRIMARY KEY,
    InteractionIssueID INT  NULL,  -- FK to InteractionIssue
    FactorSubstanceID INT  NULL   -- FK to IdentifiedSubstance (The interacting drug/class)
);
GO
-- Add Comments and Extended Properties for ContributingFactor table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an InteractionIssue to the contributing substance/class (<issue><subject><substanceAdministrationCriterion>). Based on Section 32.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor';
GO
-- FactorSubstanceID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the IdentifiedSubstance representing the drug or pharmacologic class that is the contributing factor.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor', @level2type=N'COLUMN',@level2name=N'FactorSubstanceID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 248, Para 32.2.4.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor', @level2type=N'COLUMN',@level2name=N'FactorSubstanceID';
GO

-- ============================================================================
-- Table: InteractionConsequence
-- Purpose: Stores the consequence (pharmacokinetic effect or medical problem) of an InteractionIssue. Based on Section 32.2.5.
-- ============================================================================
IF OBJECT_ID('dbo.InteractionConsequence', 'U') IS NOT NULL
    DROP TABLE dbo.InteractionConsequence;
GO

CREATE TABLE dbo.InteractionConsequence (
    InteractionConsequenceID INT IDENTITY(1,1) PRIMARY KEY,
    InteractionIssueID INT  NULL,          -- FK to InteractionIssue
    ConsequenceTypeCode VARCHAR(50) NULL,  -- Code for type ('C54386' PK effect, '44100-6' Medical problem)
    ConsequenceTypeCodeSystem VARCHAR(100) NULL, -- Code system
    ConsequenceTypeDisplayName VARCHAR(255) NULL,-- Display name
    ConsequenceValueCode VARCHAR(50) NULL, -- Code for the specific effect/problem
    ConsequenceValueCodeSystem VARCHAR(100) NULL,-- Code system (NCI or SNOMED CT)
    ConsequenceValueDisplayName VARCHAR(500) NULL -- Display name for the value code
);
GO
-- Add Comments and Extended Properties for InteractionConsequence table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the consequence (pharmacokinetic effect or medical problem) of an InteractionIssue (<risk><consequenceObservation>). Based on Section 32.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 248' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence';
GO
-- ConsequenceTypeCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of consequence: Pharmacokinetic effect (C54386) or Medical problem (44100-6).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceTypeCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceTypeCode';
GO
-- ConsequenceValueCode
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the specific pharmacokinetic effect or medical problem.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCode';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCode';
GO
-- ConsequenceValueCodeSystem
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the value code (NCI Thesaurus 2.16.840.1.113883.3.26.1.1 or SNOMED CT 2.16.840.1.113883.6.96).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCodeSystem';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.6, 32.2.5.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCodeSystem';
GO


-- ============================================================================
-- Table: NCTLink
-- Purpose: Stores the link between an indexing section and a National Clinical Trials number. Based on Section 33.2.2.
-- ============================================================================
IF OBJECT_ID('dbo.NCTLink', 'U') IS NOT NULL
    DROP TABLE dbo.NCTLink;
GO

CREATE TABLE dbo.NCTLink (
    NCTLinkID INT IDENTITY(1,1) PRIMARY KEY,
    SectionID INT  NULL,            -- FK to the Indexing Section (48779-3)
    NCTNumber VARCHAR(20) NULL,    -- The NCT number ('NCT' + 8 digits)
    NCTRootOID VARCHAR(100) NULL DEFAULT '2.16.840.1.113883.3.1077' -- Root OID for NCT
);
GO
-- Add Comments and Extended Properties for NCTLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the link between an indexing section and a National Clinical Trials number (<protocol><id>). Based on Section 33.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 251' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink';
GO
-- NCTNumber
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The National Clinical Trials number (id extension).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTNumber';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 251, Para 33.2.2.1, 33.2.2.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTNumber';
GO
-- NCTRootOID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID for NCT numbers (id root).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTRootOID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 252, Para 33.2.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTRootOID';
GO


PRINT 'Chunk 14 complete.';
GO

-- #############################################################################
-- Chunk 15: Cosmetic Facility Registration, Cosmetic Product Listing (Sec 35, 36)
-- #############################################################################

PRINT 'Creating Cosmetic Facility Registration and Product Listing Tables...';
GO

-- Note: Cosmetic Facility Registration (Section 35) uses existing tables:
-- Document (Types 103573-2, X8888-1, X8888-2, X8888-3, X8888-4)
-- DocumentRelationship (linking Doc to AuthAgent, linking Registrant to Facility, linking Facility to USAgent, linking Facility to ParentCompany)
-- OrganizationIdentifier (FEI required, DUNS optional for Facility)
-- Address, ContactParty, OrganizationTelecom (for Facility, AuthAgent, USAgent)
-- LegalAuthenticator (Submitter Signature)
-- Characteristic (SPLSMALLBUSINESS for Facility)
-- Section (Product Data Elements 48780-1, optional)
-- Product (for listed products)
-- SpecializedKind (for listed product categories)

-- Note: Cosmetic Product Listing (Section 36) uses existing tables:
-- Document (Types 103572-4, X8888-5, X8888-6)
-- DocumentAuthor (Labeler/Responsible Person)
-- OrganizationIdentifier (DUNS optional for Labeler, FEI for registered Facilities)
-- OrganizationTelecom, ContactParty, Address (for Labeler, Facilities)
-- DocumentRelationship (linking Labeler to ParentCompany, linking Registrant to ResponsiblePersonDetails/Facilities)
-- BusinessOperation (for Responsible Person Type)
-- Characteristic (SPLSMALLBUSINESS for Responsible Person, Facilities)
-- LegalAuthenticator (Submitter Signature)
-- Section (Product Data Elements 48780-1)
-- Product, ProductIdentifier (CLN), SpecializedKind, Ingredient, ProductWebLink, Characteristic (ProUse, Image)

-- ============================================================================
-- Table: FacilityProductLink
-- Purpose: Links a Facility (in Reg or Listing docs) to a Cosmetic Product. Based on Section 35.2.2, 36.1.6.
-- ============================================================================
IF OBJECT_ID('dbo.FacilityProductLink', 'U') IS NOT NULL
    DROP TABLE dbo.FacilityProductLink;
GO

CREATE TABLE dbo.FacilityProductLink (
    FacilityProductLinkID INT IDENTITY(1,1) PRIMARY KEY,
    DocumentRelationshipID INT  NULL, -- FK to DocumentRelationship (linking Doc/Reg to Facility)
    ProductID INT NULL,              -- FK to Product (if linked by internal ProductID)
    ProductIdentifierID INT NULL,    -- FK to ProductIdentifier (if linked by CLN)
    ProductName NVARCHAR(500) NULL,  -- Product Name (if linked by name before CLN assigned)
    CONSTRAINT CK_FacilityProductLink_Target CHECK (
        ProductID IS NOT NULL OR ProductIdentifierID IS NOT NULL OR ProductName IS NOT NULL
    )
);
GO
-- Add Comments and Extended Properties for FacilityProductLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Facility (in Registration or Listing docs) to a Cosmetic Product (<performance><actDefinition><product>). Link via ProductID, ProductIdentifierID (CLN), or ProductName. Based on Section 35.2.2, 36.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 260, 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink';
GO
-- ProductIdentifierID
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link via Cosmetic Listing Number.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductIdentifierID';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductIdentifierID';
GO
-- ProductName
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link via Product Name (used if CLN not yet assigned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductName';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductName';
GO

-- ============================================================================
-- Table: ResponsiblePersonLink
-- Purpose: Links a Cosmetic Product (in Facility Reg doc) to its Responsible Person org. Based on Section 35.2.3.
-- Note: Represents the <manufacturerOrganization> element under <manufacturedProduct>.
-- ============================================================================
IF OBJECT_ID('dbo.ResponsiblePersonLink', 'U') IS NOT NULL
    DROP TABLE dbo.ResponsiblePersonLink;
GO

CREATE TABLE dbo.ResponsiblePersonLink (
    ResponsiblePersonLinkID INT IDENTITY(1,1) PRIMARY KEY,
    ProductID INT  NULL,          -- FK to Product (The cosmetic product listed in the Facility Reg doc)
    ResponsiblePersonOrgID INT  NULL -- FK to Organization (The responsible person organization)
);
GO
-- Add Comments and Extended Properties for ResponsiblePersonLink table
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Cosmetic Product (in Facility Reg doc) to its Responsible Person organization (<manufacturerOrganization>). Based on Section 35.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ResponsiblePersonLink';
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 261' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ResponsiblePersonLink';
GO

PRINT 'Chunk 15 complete.';
GO

PRINT '=============================================================='
PRINT ' SQL Script Generation Complete'
PRINT '=============================================================='
GO