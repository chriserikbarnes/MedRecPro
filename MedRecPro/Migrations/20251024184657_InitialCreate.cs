using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedRecPro.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveMoiety",
                columns: table => new
                {
                    ActiveMoietyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientSubstanceID = table.Column<int>(type: "int", nullable: true),
                    MoietyUNII = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoietyName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveMoiety", x => x.ActiveMoietyID);
                });

            migrationBuilder.CreateTable(
                name: "AdditionalIdentifier",
                columns: table => new
                {
                    AdditionalIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    IdentifierTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierTypeCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierTypeDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierRootOID = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalIdentifier", x => x.AdditionalIdentifierID);
                });

            migrationBuilder.CreateTable(
                name: "Address",
                columns: table => new
                {
                    AddressID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StreetAddressLine1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StreetAddressLine2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StateProvince = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CountryName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Address", x => x.AddressID);
                });

            migrationBuilder.CreateTable(
                name: "Analyte",
                columns: table => new
                {
                    AnalyteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubstanceSpecificationID = table.Column<int>(type: "int", nullable: true),
                    AnalyteSubstanceID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analyte", x => x.AnalyteID);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationType",
                columns: table => new
                {
                    ApplicationTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppTypeCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppTypeDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationType", x => x.ApplicationTypeID);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CanonicalUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PrimaryEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PasswordChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LockoutUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MfaSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "User"),
                    UserPermissions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    Locale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "en-US"),
                    NotificationSettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UiTheme = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TosVersionAccepted = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TosAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TosMarketingOptIn = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TosEmailNotification = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UserFollowing = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByID = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttachedDocument",
                columns: table => new
                {
                    AttachedDocumentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ComplianceActionID = table.Column<int>(type: "int", nullable: true),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    ParentEntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentEntityID = table.Column<int>(type: "int", nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentIdRoot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleReference = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachedDocument", x => x.AttachedDocumentID);
                });

            migrationBuilder.CreateTable(
                name: "BillingUnitIndex",
                columns: table => new
                {
                    BillingUnitIndexID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    PackageNDCValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageNDCSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillingUnitCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillingUnitCodeSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingUnitIndex", x => x.BillingUnitIndexID);
                });

            migrationBuilder.CreateTable(
                name: "BusinessOperationProductLink",
                columns: table => new
                {
                    BusinessOperationProductLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessOperationID = table.Column<int>(type: "int", nullable: true),
                    ProductID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOperationProductLink", x => x.BusinessOperationProductLinkID);
                });

            migrationBuilder.CreateTable(
                name: "BusinessOperationQualifier",
                columns: table => new
                {
                    BusinessOperationQualifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessOperationID = table.Column<int>(type: "int", nullable: true),
                    QualifierCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualifierCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualifierDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOperationQualifier", x => x.BusinessOperationQualifierID);
                });

            migrationBuilder.CreateTable(
                name: "CertificationProductLink",
                columns: table => new
                {
                    CertificationProductLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: true),
                    ProductIdentifierID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationProductLink", x => x.CertificationProductLinkID);
                });

            migrationBuilder.CreateTable(
                name: "Commodity",
                columns: table => new
                {
                    CommodityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommodityCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommodityCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommodityDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommodityName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commodity", x => x.CommodityID);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAction",
                columns: table => new
                {
                    ComplianceActionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    PackageIdentifierID = table.Column<int>(type: "int", nullable: true),
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: true),
                    ActionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveTimeLow = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTimeHigh = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAction", x => x.ComplianceActionID);
                });

            migrationBuilder.CreateTable(
                name: "ContactParty",
                columns: table => new
                {
                    ContactPartyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationID = table.Column<int>(type: "int", nullable: true),
                    AddressID = table.Column<int>(type: "int", nullable: true),
                    ContactPersonID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactParty", x => x.ContactPartyID);
                });

            migrationBuilder.CreateTable(
                name: "ContactPartyTelecom",
                columns: table => new
                {
                    ContactPartyTelecomID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactPartyID = table.Column<int>(type: "int", nullable: true),
                    TelecomID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPartyTelecom", x => x.ContactPartyTelecomID);
                });

            migrationBuilder.CreateTable(
                name: "ContactPerson",
                columns: table => new
                {
                    ContactPersonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactPersonName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPerson", x => x.ContactPersonID);
                });

            migrationBuilder.CreateTable(
                name: "ContributingFactor",
                columns: table => new
                {
                    ContributingFactorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InteractionIssueID = table.Column<int>(type: "int", nullable: true),
                    FactorSubstanceID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributingFactor", x => x.ContributingFactorID);
                });

            migrationBuilder.CreateTable(
                name: "DisciplinaryAction",
                columns: table => new
                {
                    DisciplinaryActionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseID = table.Column<int>(type: "int", nullable: true),
                    ActionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinaryAction", x => x.DisciplinaryActionID);
                });

            migrationBuilder.CreateTable(
                name: "Document",
                columns: table => new
                {
                    DocumentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentCodeSystemName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SetGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: true),
                    SubmissionFileName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document", x => x.DocumentID);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRelationship",
                columns: table => new
                {
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    ParentOrganizationID = table.Column<int>(type: "int", nullable: true),
                    ChildOrganizationID = table.Column<int>(type: "int", nullable: true),
                    RelationshipType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelationshipLevel = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRelationship", x => x.DocumentRelationshipID);
                });

            migrationBuilder.CreateTable(
                name: "DosingSpecification",
                columns: table => new
                {
                    DosingSpecificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    RouteCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoseQuantityValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DoseQuantityUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RouteNullFlavor = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DosingSpecification", x => x.DosingSpecificationID);
                });

            migrationBuilder.CreateTable(
                name: "EquivalentEntity",
                columns: table => new
                {
                    EquivalentEntityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    EquivalenceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquivalenceCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefiningMaterialKindCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefiningMaterialKindSystem = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquivalentEntity", x => x.EquivalentEntityID);
                });

            migrationBuilder.CreateTable(
                name: "GenericMedicine",
                columns: table => new
                {
                    GenericMedicineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    GenericName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneticName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericMedicine", x => x.GenericMedicineID);
                });

            migrationBuilder.CreateTable(
                name: "Holder",
                columns: table => new
                {
                    HolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketingCategoryID = table.Column<int>(type: "int", nullable: true),
                    HolderOrganizationID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holder", x => x.HolderID);
                });

            migrationBuilder.CreateTable(
                name: "IdentifiedSubstance",
                columns: table => new
                {
                    IdentifiedSubstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    SubjectType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstanceIdentifierValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstanceIdentifierSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefinition = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentifiedSubstance", x => x.IdentifiedSubstanceID);
                });

            migrationBuilder.CreateTable(
                name: "Ingredient",
                columns: table => new
                {
                    IngredientID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    IngredientSubstanceID = table.Column<int>(type: "int", nullable: true),
                    SpecifiedSubstanceID = table.Column<int>(type: "int", nullable: true),
                    ReferenceSubstanceID = table.Column<int>(type: "int", nullable: true),
                    ProductConceptID = table.Column<int>(type: "int", nullable: true),
                    ClassCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityNumerator = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityNumeratorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorTranslationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityDenominator = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DenominatorTranslationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenominatorCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenominatorDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DenominatorValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityDenominatorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConfidential = table.Column<bool>(type: "bit", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatingElement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredient", x => x.IngredientID);
                });

            migrationBuilder.CreateTable(
                name: "IngredientInstance",
                columns: table => new
                {
                    IngredientInstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FillLotInstanceID = table.Column<int>(type: "int", nullable: true),
                    IngredientSubstanceID = table.Column<int>(type: "int", nullable: true),
                    LotIdentifierID = table.Column<int>(type: "int", nullable: true),
                    ManufacturerOrganizationID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientInstance", x => x.IngredientInstanceID);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSourceProduct",
                columns: table => new
                {
                    IngredientSourceProductID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientID = table.Column<int>(type: "int", nullable: true),
                    SourceProductNDC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceProductNDCSysten = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSourceProduct", x => x.IngredientSourceProductID);
                });

            migrationBuilder.CreateTable(
                name: "IngredientSubstance",
                columns: table => new
                {
                    IngredientSubstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UNII = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstanceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginatingElement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientSubstance", x => x.IngredientSubstanceID);
                });

            migrationBuilder.CreateTable(
                name: "InteractionConsequence",
                columns: table => new
                {
                    InteractionConsequenceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InteractionIssueID = table.Column<int>(type: "int", nullable: true),
                    ConsequenceTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsequenceTypeCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsequenceTypeDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsequenceValueCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsequenceValueCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsequenceValueDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteractionConsequence", x => x.InteractionConsequenceID);
                });

            migrationBuilder.CreateTable(
                name: "InteractionIssue",
                columns: table => new
                {
                    InteractionIssueID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    InteractionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InteractionCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InteractionDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteractionIssue", x => x.InteractionIssueID);
                });

            migrationBuilder.CreateTable(
                name: "LegalAuthenticator",
                columns: table => new
                {
                    LegalAuthenticatorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    NoteText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeValue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedPersonName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignerOrganizationID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalAuthenticator", x => x.LegalAuthenticatorID);
                });

            migrationBuilder.CreateTable(
                name: "License",
                columns: table => new
                {
                    LicenseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessOperationID = table.Column<int>(type: "int", nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseRootOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseTypeCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LicenseTypeDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerritorialAuthorityID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_License", x => x.LicenseID);
                });

            migrationBuilder.CreateTable(
                name: "LotHierarchy",
                columns: table => new
                {
                    LotHierarchyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentInstanceID = table.Column<int>(type: "int", nullable: true),
                    ChildInstanceID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotHierarchy", x => x.LotHierarchyID);
                });

            migrationBuilder.CreateTable(
                name: "LotIdentifier",
                columns: table => new
                {
                    LotIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LotRootOID = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotIdentifier", x => x.LotIdentifierID);
                });

            migrationBuilder.CreateTable(
                name: "MarketingCategory",
                columns: table => new
                {
                    MarketingCategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    CategoryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicationOrMonographIDValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApplicationOrMonographIDOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerritoryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductConceptID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCategory", x => x.MarketingCategoryID);
                });

            migrationBuilder.CreateTable(
                name: "MarketingStatus",
                columns: table => new
                {
                    MarketingStatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    PackagingLevelID = table.Column<int>(type: "int", nullable: true),
                    MarketingActCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MarketingActCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveEndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingStatus", x => x.MarketingStatusID);
                });

            migrationBuilder.CreateTable(
                name: "NamedEntity",
                columns: table => new
                {
                    NamedEntityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationID = table.Column<int>(type: "int", nullable: true),
                    EntityTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityTypeCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityTypeDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntitySuffix = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamedEntity", x => x.NamedEntityID);
                });

            migrationBuilder.CreateTable(
                name: "NCTLink",
                columns: table => new
                {
                    NCTLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    NCTNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NCTRootOID = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NCTLink", x => x.NCTLinkID);
                });

            migrationBuilder.CreateTable(
                name: "ObservationCriterion",
                columns: table => new
                {
                    ObservationCriterionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubstanceSpecificationID = table.Column<int>(type: "int", nullable: true),
                    ToleranceHighValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ToleranceHighUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommodityID = table.Column<int>(type: "int", nullable: true),
                    ApplicationTypeID = table.Column<int>(type: "int", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TextNote = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationCriterion", x => x.ObservationCriterionID);
                });

            migrationBuilder.CreateTable(
                name: "ObservationMedia",
                columns: table => new
                {
                    ObservationMediaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    MediaID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    XsiType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationMedia", x => x.ObservationMediaID);
                });

            migrationBuilder.CreateTable(
                name: "Organization",
                columns: table => new
                {
                    OrganizationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConfidential = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organization", x => x.OrganizationID);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationTelecom",
                columns: table => new
                {
                    OrganizationTelecomID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationID = table.Column<int>(type: "int", nullable: true),
                    TelecomID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationTelecom", x => x.OrganizationTelecomID);
                });

            migrationBuilder.CreateTable(
                name: "PackageIdentifier",
                columns: table => new
                {
                    PackageIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackagingLevelID = table.Column<int>(type: "int", nullable: true),
                    IdentifierValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageIdentifier", x => x.PackageIdentifierID);
                });

            migrationBuilder.CreateTable(
                name: "PackagingHierarchy",
                columns: table => new
                {
                    PackagingHierarchyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OuterPackagingLevelID = table.Column<int>(type: "int", nullable: true),
                    InnerPackagingLevelID = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagingHierarchy", x => x.PackagingHierarchyID);
                });

            migrationBuilder.CreateTable(
                name: "PackagingLevel",
                columns: table => new
                {
                    PackagingLevelID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    PartProductID = table.Column<int>(type: "int", nullable: true),
                    QuantityNumerator = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NumeratorTranslationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorTranslationCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeratorTranslationDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityDenominator = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityNumeratorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageFormCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageFormCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageFormDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductInstanceID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagingLevel", x => x.PackagingLevelID);
                });

            migrationBuilder.CreateTable(
                name: "PartOfAssembly",
                columns: table => new
                {
                    PartOfAssemblyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryProductID = table.Column<int>(type: "int", nullable: true),
                    AccessoryProductID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartOfAssembly", x => x.PartOfAssemblyID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacologicClass",
                columns: table => new
                {
                    PharmacologicClassID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentifiedSubstanceID = table.Column<int>(type: "int", nullable: true),
                    ClassCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacologicClass", x => x.PharmacologicClassID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacologicClassHierarchy",
                columns: table => new
                {
                    PharmClassHierarchyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildPharmacologicClassID = table.Column<int>(type: "int", nullable: true),
                    ParentPharmacologicClassID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacologicClassHierarchy", x => x.PharmClassHierarchyID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacologicClassLink",
                columns: table => new
                {
                    PharmClassLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActiveMoietySubstanceID = table.Column<int>(type: "int", nullable: true),
                    PharmacologicClassID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacologicClassLink", x => x.PharmClassLinkID);
                });

            migrationBuilder.CreateTable(
                name: "PharmacologicClassName",
                columns: table => new
                {
                    PharmClassNameID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PharmacologicClassID = table.Column<int>(type: "int", nullable: true),
                    NameValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameUse = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacologicClassName", x => x.PharmClassNameID);
                });

            migrationBuilder.CreateTable(
                name: "Policy",
                columns: table => new
                {
                    PolicyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    PolicyClassCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PolicyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PolicyCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PolicyDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policy", x => x.PolicyID);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                columns: table => new
                {
                    ProductID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductSuffix = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.ProductID);
                });

            migrationBuilder.CreateTable(
                name: "ProductConcept",
                columns: table => new
                {
                    ProductConceptID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ConceptCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConceptCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConceptType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductConcept", x => x.ProductConceptID);
                });

            migrationBuilder.CreateTable(
                name: "ProductConceptEquivalence",
                columns: table => new
                {
                    ProductConceptEquivalenceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationProductConceptID = table.Column<int>(type: "int", nullable: true),
                    AbstractProductConceptID = table.Column<int>(type: "int", nullable: true),
                    EquivalenceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EquivalenceCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductConceptEquivalence", x => x.ProductConceptEquivalenceID);
                });

            migrationBuilder.CreateTable(
                name: "ProductEvent",
                columns: table => new
                {
                    ProductEventID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackagingLevelID = table.Column<int>(type: "int", nullable: false),
                    EventCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityValue = table.Column<int>(type: "int", nullable: true),
                    QuantityUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveTimeLow = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductEvent", x => x.ProductEventID);
                });

            migrationBuilder.CreateTable(
                name: "ProductIdentifier",
                columns: table => new
                {
                    ProductIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    IdentifierValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIdentifier", x => x.ProductIdentifierID);
                });

            migrationBuilder.CreateTable(
                name: "ProductInstance",
                columns: table => new
                {
                    ProductInstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    InstanceType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LotIdentifierID = table.Column<int>(type: "int", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInstance", x => x.ProductInstanceID);
                });

            migrationBuilder.CreateTable(
                name: "ProductPart",
                columns: table => new
                {
                    ProductPartID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KitProductID = table.Column<int>(type: "int", nullable: true),
                    PartProductID = table.Column<int>(type: "int", nullable: true),
                    PartQuantityNumerator = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartQuantityNumeratorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPart", x => x.ProductPartID);
                });

            migrationBuilder.CreateTable(
                name: "ProductRouteOfAdministration",
                columns: table => new
                {
                    ProductRouteOfAdministrationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    RouteCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RouteCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RouteDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RouteNullFlavor = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRouteOfAdministration", x => x.ProductRouteOfAdministrationID);
                });

            migrationBuilder.CreateTable(
                name: "ProductWebLink",
                columns: table => new
                {
                    ProductWebLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    WebURL = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductWebLink", x => x.ProductWebLinkID);
                });

            migrationBuilder.CreateTable(
                name: "Protocol",
                columns: table => new
                {
                    ProtocolID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ProtocolCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProtocolCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProtocolDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Protocol", x => x.ProtocolID);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceSubstance",
                columns: table => new
                {
                    ReferenceSubstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientSubstanceID = table.Column<int>(type: "int", nullable: true),
                    RefSubstanceUNII = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefSubstanceName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceSubstance", x => x.ReferenceSubstanceID);
                });

            migrationBuilder.CreateTable(
                name: "RelatedDocument",
                columns: table => new
                {
                    RelatedDocumentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceDocumentID = table.Column<int>(type: "int", nullable: true),
                    RelationshipTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencedSetGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferencedDocumentGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReferencedVersionNumber = table.Column<int>(type: "int", nullable: true),
                    ReferencedDocumentCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencedDocumentCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferencedDocumentDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelatedDocument", x => x.RelatedDocumentID);
                });

            migrationBuilder.CreateTable(
                name: "REMSApproval",
                columns: table => new
                {
                    REMSApprovalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtocolID = table.Column<int>(type: "int", nullable: true),
                    ApprovalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerritoryCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSApproval", x => x.REMSApprovalID);
                });

            migrationBuilder.CreateTable(
                name: "REMSElectronicResource",
                columns: table => new
                {
                    REMSElectronicResourceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ResourceDocumentGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResourceReferenceValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSElectronicResource", x => x.REMSElectronicResourceID);
                });

            migrationBuilder.CreateTable(
                name: "REMSMaterial",
                columns: table => new
                {
                    REMSMaterialID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    MaterialDocumentGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachedDocumentID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSMaterial", x => x.REMSMaterialID);
                });

            migrationBuilder.CreateTable(
                name: "RenderedMedia",
                columns: table => new
                {
                    RenderedMediaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionTextContentID = table.Column<int>(type: "int", nullable: true),
                    ObservationMediaID = table.Column<int>(type: "int", nullable: true),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    SequenceInContent = table.Column<int>(type: "int", nullable: true),
                    IsInline = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderedMedia", x => x.RenderedMediaID);
                });

            migrationBuilder.CreateTable(
                name: "Requirement",
                columns: table => new
                {
                    RequirementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtocolID = table.Column<int>(type: "int", nullable: true),
                    RequirementSequenceNumber = table.Column<int>(type: "int", nullable: true),
                    IsMonitoringObservation = table.Column<bool>(type: "bit", nullable: true),
                    PauseQuantityValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PauseQuantityUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequirementCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequirementCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequirementDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalTextReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PeriodUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StakeholderID = table.Column<int>(type: "int", nullable: true),
                    REMSMaterialID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirement", x => x.RequirementID);
                });

            migrationBuilder.CreateTable(
                name: "ResponsiblePersonLink",
                columns: table => new
                {
                    ResponsiblePersonLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    ResponsiblePersonOrgID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponsiblePersonLink", x => x.ResponsiblePersonLinkID);
                });

            migrationBuilder.CreateTable(
                name: "Section",
                columns: table => new
                {
                    SectionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StructuredBodyID = table.Column<int>(type: "int", nullable: true),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    SectionLinkGUID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionCodeSystemName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SectionDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTimeLow = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTimeHigh = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Section", x => x.SectionID);
                });

            migrationBuilder.CreateTable(
                name: "SectionExcerptHighlight",
                columns: table => new
                {
                    SectionExcerptHighlightID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    HighlightText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionExcerptHighlight", x => x.SectionExcerptHighlightID);
                });

            migrationBuilder.CreateTable(
                name: "SectionHierarchy",
                columns: table => new
                {
                    SectionHierarchyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentSectionID = table.Column<int>(type: "int", nullable: true),
                    ChildSectionID = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionHierarchy", x => x.SectionHierarchyID);
                });

            migrationBuilder.CreateTable(
                name: "SectionTextContent",
                columns: table => new
                {
                    SectionTextContentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ParentSectionTextContentID = table.Column<int>(type: "int", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionTextContent", x => x.SectionTextContentID);
                });

            migrationBuilder.CreateTable(
                name: "SpecializedKind",
                columns: table => new
                {
                    SpecializedKindID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    KindCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KindCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KindDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecializedKind", x => x.SpecializedKindID);
                });

            migrationBuilder.CreateTable(
                name: "SpecifiedSubstance",
                columns: table => new
                {
                    SpecifiedSubstanceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubstanceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstanceCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstanceCodeSystemName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecifiedSubstance", x => x.SpecifiedSubstanceID);
                });

            migrationBuilder.CreateTable(
                name: "SplData",
                columns: table => new
                {
                    SplDataID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AspNetUsersID = table.Column<long>(type: "bigint", nullable: true),
                    SplDataGUID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SplXML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Archive = table.Column<bool>(type: "bit", nullable: true),
                    LogDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SplXMLHash = table.Column<string>(type: "char(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplData", x => x.SplDataID);
                });

            migrationBuilder.CreateTable(
                name: "Stakeholder",
                columns: table => new
                {
                    StakeholderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StakeholderCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StakeholderCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StakeholderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stakeholder", x => x.StakeholderID);
                });

            migrationBuilder.CreateTable(
                name: "StructuredBody",
                columns: table => new
                {
                    StructuredBodyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredBody", x => x.StructuredBodyID);
                });

            migrationBuilder.CreateTable(
                name: "SubstanceSpecification",
                columns: table => new
                {
                    SubstanceSpecificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentifiedSubstanceID = table.Column<int>(type: "int", nullable: true),
                    SpecCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnforcementMethodCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnforcementMethodCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnforcementMethodDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubstanceSpecification", x => x.SubstanceSpecificationID);
                });

            migrationBuilder.CreateTable(
                name: "Telecom",
                columns: table => new
                {
                    TelecomID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TelecomType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TelecomValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Telecom", x => x.TelecomID);
                });

            migrationBuilder.CreateTable(
                name: "TerritorialAuthority",
                columns: table => new
                {
                    TerritorialAuthorityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerritoryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TerritoryCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoverningAgencyIdExtension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoverningAgencyIdRoot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoverningAgencyName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritorialAuthority", x => x.TerritorialAuthorityID);
                });

            migrationBuilder.CreateTable(
                name: "TextList",
                columns: table => new
                {
                    TextListID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionTextContentID = table.Column<int>(type: "int", nullable: true),
                    ListType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextList", x => x.TextListID);
                });

            migrationBuilder.CreateTable(
                name: "TextListItem",
                columns: table => new
                {
                    TextListItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextListID = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    ItemCaption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextListItem", x => x.TextListItemID);
                });

            migrationBuilder.CreateTable(
                name: "TextTable",
                columns: table => new
                {
                    TextTableID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionTextContentID = table.Column<int>(type: "int", nullable: true),
                    SectionTableLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasHeader = table.Column<bool>(type: "bit", nullable: true),
                    HasFooter = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextTable", x => x.TextTableID);
                });

            migrationBuilder.CreateTable(
                name: "TextTableCell",
                columns: table => new
                {
                    TextTableCellID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextTableRowID = table.Column<int>(type: "int", nullable: true),
                    CellType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    CellText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowSpan = table.Column<int>(type: "int", nullable: true),
                    ColSpan = table.Column<int>(type: "int", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Align = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VAlign = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextTableCell", x => x.TextTableCellID);
                });

            migrationBuilder.CreateTable(
                name: "TextTableColumn",
                columns: table => new
                {
                    TextTableColumnID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextTableID = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    ColGroupSequenceNumber = table.Column<int>(type: "int", nullable: true),
                    ColGroupStyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColGroupAlign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColGroupVAlign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Align = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VAlign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextTableColumn", x => x.TextTableColumnID);
                });

            migrationBuilder.CreateTable(
                name: "TextTableRow",
                columns: table => new
                {
                    TextTableRowID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextTableID = table.Column<int>(type: "int", nullable: true),
                    RowGroupType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    StyleCode = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextTableRow", x => x.TextTableRowID);
                });

            migrationBuilder.CreateTable(
                name: "WarningLetterDate",
                columns: table => new
                {
                    WarningLetterDateID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    AlertIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningLetterDate", x => x.WarningLetterDateID);
                });

            migrationBuilder.CreateTable(
                name: "WarningLetterProductInfo",
                columns: table => new
                {
                    WarningLetterProductInfoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StrengthText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemCodesText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningLetterProductInfo", x => x.WarningLetterProductInfoID);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserActivityLog",
                columns: table => new
                {
                    ActivityLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    ActivityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActivityTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ControllerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequestParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ExecutionTimeMs = table.Column<int>(type: "int", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserActivityLog", x => x.ActivityLogId);
                    table.ForeignKey(
                        name: "FK_AspNetUserActivityLog_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityProductLink",
                columns: table => new
                {
                    FacilityProductLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: true),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    ProductIdentifierID = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityProductLink", x => x.FacilityProductLinkID);
                    table.ForeignKey(
                        name: "FK_FacilityProductLink_DocumentRelationship_DocumentRelationshipID",
                        column: x => x.DocumentRelationshipID,
                        principalTable: "DocumentRelationship",
                        principalColumn: "DocumentRelationshipID");
                });

            migrationBuilder.CreateTable(
                name: "Moiety",
                columns: table => new
                {
                    MoietyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentifiedSubstanceID = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<int>(type: "int", nullable: true),
                    MoietyCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoietyCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoietyDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityNumeratorLowValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityNumeratorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuantityNumeratorInclusive = table.Column<bool>(type: "bit", nullable: true),
                    QuantityDenominatorValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    QuantityDenominatorUnit = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moiety", x => x.MoietyID);
                    table.ForeignKey(
                        name: "FK_Moiety_IdentifiedSubstance_IdentifiedSubstanceID",
                        column: x => x.IdentifiedSubstanceID,
                        principalTable: "IdentifiedSubstance",
                        principalColumn: "IdentifiedSubstanceID");
                });

            migrationBuilder.CreateTable(
                name: "BusinessOperation",
                columns: table => new
                {
                    BusinessOperationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: true),
                    PerformingOrganizationID = table.Column<int>(type: "int", nullable: true),
                    OperationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessOperation", x => x.BusinessOperationID);
                    table.ForeignKey(
                        name: "FK_BusinessOperation_Organization_PerformingOrganizationID",
                        column: x => x.PerformingOrganizationID,
                        principalTable: "Organization",
                        principalColumn: "OrganizationID");
                });

            migrationBuilder.CreateTable(
                name: "DocumentAuthor",
                columns: table => new
                {
                    DocumentAuthorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentID = table.Column<int>(type: "int", nullable: true),
                    OrganizationID = table.Column<int>(type: "int", nullable: true),
                    AuthorType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAuthor", x => x.DocumentAuthorID);
                    table.ForeignKey(
                        name: "FK_DocumentAuthor_Organization_OrganizationID",
                        column: x => x.OrganizationID,
                        principalTable: "Organization",
                        principalColumn: "OrganizationID");
                });

            migrationBuilder.CreateTable(
                name: "OrganizationIdentifier",
                columns: table => new
                {
                    OrganizationIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationID = table.Column<int>(type: "int", nullable: true),
                    IdentifierValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierSystemOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationIdentifier", x => x.OrganizationIdentifierID);
                    table.ForeignKey(
                        name: "FK_OrganizationIdentifier_Organization_OrganizationID",
                        column: x => x.OrganizationID,
                        principalTable: "Organization",
                        principalColumn: "OrganizationID");
                });

            migrationBuilder.CreateTable(
                name: "Characteristic",
                columns: table => new
                {
                    CharacteristicID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductID = table.Column<int>(type: "int", nullable: true),
                    PackagingLevelID = table.Column<int>(type: "int", nullable: true),
                    MoietyID = table.Column<int>(type: "int", nullable: true),
                    CharacteristicCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CharacteristicCodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValuePQ_Value = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValuePQ_Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueINT = table.Column<int>(type: "int", nullable: true),
                    ValueCV_Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueCV_CodeSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueCV_DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueST = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueBL = table.Column<bool>(type: "bit", nullable: true),
                    ValueIVLPQ_LowValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValueIVLPQ_LowUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueIVLPQ_HighValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValueIVLPQ_HighUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueED_MediaType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueED_FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueED_CDATAContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueNullFlavor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characteristic", x => x.CharacteristicID);
                    table.ForeignKey(
                        name: "FK_Characteristic_Moiety_MoietyID",
                        column: x => x.MoietyID,
                        principalTable: "Moiety",
                        principalColumn: "MoietyID");
                });

            migrationBuilder.CreateTable(
                name: "DocumentRelationshipIdentifier",
                columns: table => new
                {
                    DocumentRelationshipIdentifierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentRelationshipID = table.Column<int>(type: "int", nullable: true),
                    OrganizationIdentifierID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRelationshipIdentifier", x => x.DocumentRelationshipIdentifierID);
                    table.ForeignKey(
                        name: "FK_DocumentRelationshipIdentifier_DocumentRelationship_DocumentRelationshipID",
                        column: x => x.DocumentRelationshipID,
                        principalTable: "DocumentRelationship",
                        principalColumn: "DocumentRelationshipID");
                    table.ForeignKey(
                        name: "FK_DocumentRelationshipIdentifier_OrganizationIdentifier_OrganizationIdentifierID",
                        column: x => x.OrganizationIdentifierID,
                        principalTable: "OrganizationIdentifier",
                        principalColumn: "OrganizationIdentifierID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserActivityLog_UserId",
                table: "AspNetUserActivityLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastActivityAt",
                table: "AspNetUsers",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_CanonicalUsername_Active",
                table: "AspNetUsers",
                column: "CanonicalUsername",
                unique: true,
                filter: "[DeletedAt] IS NULL AND [CanonicalUsername] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Users_PrimaryEmail_Active",
                table: "AspNetUsers",
                column: "PrimaryEmail",
                unique: true,
                filter: "[DeletedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessOperation_PerformingOrganizationID",
                table: "BusinessOperation",
                column: "PerformingOrganizationID");

            migrationBuilder.CreateIndex(
                name: "IX_Characteristic_MoietyID",
                table: "Characteristic",
                column: "MoietyID");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuthor_OrganizationID",
                table: "DocumentAuthor",
                column: "OrganizationID");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRelationshipIdentifier_DocumentRelationshipID",
                table: "DocumentRelationshipIdentifier",
                column: "DocumentRelationshipID");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRelationshipIdentifier_OrganizationIdentifierID",
                table: "DocumentRelationshipIdentifier",
                column: "OrganizationIdentifierID");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentRelationshipIdentifier_Unique",
                table: "DocumentRelationshipIdentifier",
                columns: new[] { "DocumentRelationshipID", "OrganizationIdentifierID" },
                unique: true,
                filter: "[DocumentRelationshipID] IS NOT NULL AND [OrganizationIdentifierID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityProductLink_DocumentRelationshipID",
                table: "FacilityProductLink",
                column: "DocumentRelationshipID");

            migrationBuilder.CreateIndex(
                name: "IX_Moiety_IdentifiedSubstanceID",
                table: "Moiety",
                column: "IdentifiedSubstanceID");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationIdentifier_OrganizationID",
                table: "OrganizationIdentifier",
                column: "OrganizationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveMoiety");

            migrationBuilder.DropTable(
                name: "AdditionalIdentifier");

            migrationBuilder.DropTable(
                name: "Address");

            migrationBuilder.DropTable(
                name: "Analyte");

            migrationBuilder.DropTable(
                name: "ApplicationType");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserActivityLog");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AttachedDocument");

            migrationBuilder.DropTable(
                name: "BillingUnitIndex");

            migrationBuilder.DropTable(
                name: "BusinessOperation");

            migrationBuilder.DropTable(
                name: "BusinessOperationProductLink");

            migrationBuilder.DropTable(
                name: "BusinessOperationQualifier");

            migrationBuilder.DropTable(
                name: "CertificationProductLink");

            migrationBuilder.DropTable(
                name: "Characteristic");

            migrationBuilder.DropTable(
                name: "Commodity");

            migrationBuilder.DropTable(
                name: "ComplianceAction");

            migrationBuilder.DropTable(
                name: "ContactParty");

            migrationBuilder.DropTable(
                name: "ContactPartyTelecom");

            migrationBuilder.DropTable(
                name: "ContactPerson");

            migrationBuilder.DropTable(
                name: "ContributingFactor");

            migrationBuilder.DropTable(
                name: "DisciplinaryAction");

            migrationBuilder.DropTable(
                name: "Document");

            migrationBuilder.DropTable(
                name: "DocumentAuthor");

            migrationBuilder.DropTable(
                name: "DocumentRelationshipIdentifier");

            migrationBuilder.DropTable(
                name: "DosingSpecification");

            migrationBuilder.DropTable(
                name: "EquivalentEntity");

            migrationBuilder.DropTable(
                name: "FacilityProductLink");

            migrationBuilder.DropTable(
                name: "GenericMedicine");

            migrationBuilder.DropTable(
                name: "Holder");

            migrationBuilder.DropTable(
                name: "Ingredient");

            migrationBuilder.DropTable(
                name: "IngredientInstance");

            migrationBuilder.DropTable(
                name: "IngredientSourceProduct");

            migrationBuilder.DropTable(
                name: "IngredientSubstance");

            migrationBuilder.DropTable(
                name: "InteractionConsequence");

            migrationBuilder.DropTable(
                name: "InteractionIssue");

            migrationBuilder.DropTable(
                name: "LegalAuthenticator");

            migrationBuilder.DropTable(
                name: "License");

            migrationBuilder.DropTable(
                name: "LotHierarchy");

            migrationBuilder.DropTable(
                name: "LotIdentifier");

            migrationBuilder.DropTable(
                name: "MarketingCategory");

            migrationBuilder.DropTable(
                name: "MarketingStatus");

            migrationBuilder.DropTable(
                name: "NamedEntity");

            migrationBuilder.DropTable(
                name: "NCTLink");

            migrationBuilder.DropTable(
                name: "ObservationCriterion");

            migrationBuilder.DropTable(
                name: "ObservationMedia");

            migrationBuilder.DropTable(
                name: "OrganizationTelecom");

            migrationBuilder.DropTable(
                name: "PackageIdentifier");

            migrationBuilder.DropTable(
                name: "PackagingHierarchy");

            migrationBuilder.DropTable(
                name: "PackagingLevel");

            migrationBuilder.DropTable(
                name: "PartOfAssembly");

            migrationBuilder.DropTable(
                name: "PharmacologicClass");

            migrationBuilder.DropTable(
                name: "PharmacologicClassHierarchy");

            migrationBuilder.DropTable(
                name: "PharmacologicClassLink");

            migrationBuilder.DropTable(
                name: "PharmacologicClassName");

            migrationBuilder.DropTable(
                name: "Policy");

            migrationBuilder.DropTable(
                name: "Product");

            migrationBuilder.DropTable(
                name: "ProductConcept");

            migrationBuilder.DropTable(
                name: "ProductConceptEquivalence");

            migrationBuilder.DropTable(
                name: "ProductEvent");

            migrationBuilder.DropTable(
                name: "ProductIdentifier");

            migrationBuilder.DropTable(
                name: "ProductInstance");

            migrationBuilder.DropTable(
                name: "ProductPart");

            migrationBuilder.DropTable(
                name: "ProductRouteOfAdministration");

            migrationBuilder.DropTable(
                name: "ProductWebLink");

            migrationBuilder.DropTable(
                name: "Protocol");

            migrationBuilder.DropTable(
                name: "ReferenceSubstance");

            migrationBuilder.DropTable(
                name: "RelatedDocument");

            migrationBuilder.DropTable(
                name: "REMSApproval");

            migrationBuilder.DropTable(
                name: "REMSElectronicResource");

            migrationBuilder.DropTable(
                name: "REMSMaterial");

            migrationBuilder.DropTable(
                name: "RenderedMedia");

            migrationBuilder.DropTable(
                name: "Requirement");

            migrationBuilder.DropTable(
                name: "ResponsiblePersonLink");

            migrationBuilder.DropTable(
                name: "Section");

            migrationBuilder.DropTable(
                name: "SectionExcerptHighlight");

            migrationBuilder.DropTable(
                name: "SectionHierarchy");

            migrationBuilder.DropTable(
                name: "SectionTextContent");

            migrationBuilder.DropTable(
                name: "SpecializedKind");

            migrationBuilder.DropTable(
                name: "SpecifiedSubstance");

            migrationBuilder.DropTable(
                name: "SplData");

            migrationBuilder.DropTable(
                name: "Stakeholder");

            migrationBuilder.DropTable(
                name: "StructuredBody");

            migrationBuilder.DropTable(
                name: "SubstanceSpecification");

            migrationBuilder.DropTable(
                name: "Telecom");

            migrationBuilder.DropTable(
                name: "TerritorialAuthority");

            migrationBuilder.DropTable(
                name: "TextList");

            migrationBuilder.DropTable(
                name: "TextListItem");

            migrationBuilder.DropTable(
                name: "TextTable");

            migrationBuilder.DropTable(
                name: "TextTableCell");

            migrationBuilder.DropTable(
                name: "TextTableColumn");

            migrationBuilder.DropTable(
                name: "TextTableRow");

            migrationBuilder.DropTable(
                name: "WarningLetterDate");

            migrationBuilder.DropTable(
                name: "WarningLetterProductInfo");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Moiety");

            migrationBuilder.DropTable(
                name: "OrganizationIdentifier");

            migrationBuilder.DropTable(
                name: "DocumentRelationship");

            migrationBuilder.DropTable(
                name: "IdentifiedSubstance");

            migrationBuilder.DropTable(
                name: "Organization");
        }
    }
}
