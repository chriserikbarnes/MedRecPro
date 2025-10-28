USE [MedRecProDB]

GO
ALTER DATABASE [MedRecProDB] SET COMPATIBILITY_LEVEL = 150
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [MedRecProDB].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [MedRecProDB] SET ANSI_NULL_DEFAULT ON 
GO
ALTER DATABASE [MedRecProDB] SET ANSI_NULLS ON 
GO
ALTER DATABASE [MedRecProDB] SET ANSI_PADDING ON 
GO
ALTER DATABASE [MedRecProDB] SET ANSI_WARNINGS ON 
GO
ALTER DATABASE [MedRecProDB] SET ARITHABORT ON 
GO
ALTER DATABASE [MedRecProDB] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [MedRecProDB] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [MedRecProDB] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [MedRecProDB] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [MedRecProDB] SET CONCAT_NULL_YIELDS_NULL ON 
GO
ALTER DATABASE [MedRecProDB] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [MedRecProDB] SET QUOTED_IDENTIFIER ON 
GO
ALTER DATABASE [MedRecProDB] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [MedRecProDB] SET  DISABLE_BROKER 
GO
ALTER DATABASE [MedRecProDB] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [MedRecProDB] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [MedRecProDB] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [MedRecProDB] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [MedRecProDB] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [MedRecProDB] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [MedRecProDB] SET  MULTI_USER 
GO
ALTER DATABASE [MedRecProDB] SET DELAYED_DURABILITY = DISABLED  
GO
ALTER DATABASE [MedRecProDB] SET QUERY_STORE = OFF
GO
USE [MedRecProDB]
GO
/****** Object:  Table [dbo].[__EFMigrationsHistory]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ActiveMoiety]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ActiveMoiety](
	[ActiveMoietyID] [int] IDENTITY(1,1) NOT NULL,
	[IngredientSubstanceID] [int] NULL,
	[MoietyUNII] [char](10) NULL,
	[MoietyName] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[ActiveMoietyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdditionalIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdditionalIdentifier](
	[AdditionalIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[IdentifierTypeCode] [varchar](50) NULL,
	[IdentifierTypeCodeSystem] [varchar](100) NULL,
	[IdentifierTypeDisplayName] [varchar](255) NULL,
	[IdentifierValue] [varchar](255) NULL,
	[IdentifierRootOID] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[AdditionalIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Address]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Address](
	[AddressID] [int] IDENTITY(1,1) NOT NULL,
	[StreetAddressLine1] [varchar](500) NULL,
	[StreetAddressLine2] [varchar](500) NULL,
	[City] [varchar](100) NULL,
	[StateProvince] [varchar](100) NULL,
	[PostalCode] [varchar](20) NULL,
	[CountryCode] [char](3) NULL,
	[CountryName] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[AddressID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Analyte]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Analyte](
	[AnalyteID] [int] IDENTITY(1,1) NOT NULL,
	[SubstanceSpecificationID] [int] NULL,
	[AnalyteSubstanceID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[AnalyteID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ApplicationType]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApplicationType](
	[ApplicationTypeID] [int] IDENTITY(1,1) NOT NULL,
	[AppTypeCode] [varchar](50) NULL,
	[AppTypeCodeSystem] [varchar](100) NULL,
	[AppTypeDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[ApplicationTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetRoleClaims]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoleClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoleId] [bigint] NOT NULL,
	[ClaimType] [nvarchar](max) NULL,
	[ClaimValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetRoles]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoles](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](256) NULL,
	[NormalizedName] [nvarchar](256) NULL,
	[ConcurrencyStamp] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUserActivityLog]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserActivityLog](
	[ActivityLogId] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [bigint] NULL,
	[ActivityType] [nvarchar](100) NOT NULL,
	[ActivityTimestamp] [datetime2](7) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[IpAddress] [nvarchar](45) NULL,
	[UserAgent] [nvarchar](500) NULL,
	[RequestPath] [nvarchar](500) NULL,
	[ControllerName] [nvarchar](100) NULL,
	[ActionName] [nvarchar](100) NULL,
	[HttpMethod] [nvarchar](10) NULL,
	[RequestParameters] [nvarchar](max) NULL,
	[ResponseStatusCode] [int] NULL,
	[ExecutionTimeMs] [int] NULL,
	[Result] [nvarchar](50) NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[ExceptionType] [nvarchar](200) NULL,
	[StackTrace] [nvarchar](max) NULL,
	[SessionId] [nvarchar](100) NULL,
 CONSTRAINT [PK_AspNetUserActivityLog] PRIMARY KEY CLUSTERED 
(
	[ActivityLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUserClaims]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [bigint] NOT NULL,
	[ClaimType] [nvarchar](max) NULL,
	[ClaimValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUserLogins]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserLogins](
	[LoginProvider] [nvarchar](450) NOT NULL,
	[ProviderKey] [nvarchar](450) NOT NULL,
	[ProviderDisplayName] [nvarchar](max) NULL,
	[UserId] [bigint] NOT NULL,
 CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED 
(
	[LoginProvider] ASC,
	[ProviderKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUserRoles]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserRoles](
	[UserId] [bigint] NOT NULL,
	[RoleId] [bigint] NOT NULL,
 CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUsers]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUsers](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[CanonicalUsername] [nvarchar](256) NULL,
	[DisplayName] [nvarchar](256) NULL,
	[PrimaryEmail] [nvarchar](320) NOT NULL,
	[MfaEnabled] [bit] NOT NULL,
	[PasswordChangedAt] [datetime2](7) NULL,
	[FailedLoginCount] [int] NOT NULL,
	[LockoutUntil] [datetime2](7) NULL,
	[MfaSecret] [nvarchar](200) NULL,
	[UserRole] [nvarchar](100) NOT NULL,
	[UserPermissions] [nvarchar](4000) NULL,
	[Timezone] [nvarchar](100) NOT NULL,
	[Locale] [nvarchar](20) NOT NULL,
	[NotificationSettings] [nvarchar](4000) NULL,
	[UiTheme] [nvarchar](50) NULL,
	[TosVersionAccepted] [nvarchar](20) NULL,
	[TosAcceptedAt] [datetime2](7) NULL,
	[TosMarketingOptIn] [bit] NOT NULL,
	[TosEmailNotification] [bit] NOT NULL,
	[UserFollowing] [nvarchar](4000) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedByID] [bigint] NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [bigint] NULL,
	[DeletedAt] [datetime2](7) NULL,
	[SuspendedAt] [datetime2](7) NULL,
	[SuspensionReason] [nvarchar](500) NULL,
	[LastLoginAt] [datetime2](7) NULL,
	[LastActivityAt] [datetime2](7) NULL,
	[LastIpAddress] [nvarchar](45) NULL,
	[UserName] [nvarchar](256) NULL,
	[NormalizedUserName] [nvarchar](256) NULL,
	[Email] [nvarchar](256) NULL,
	[NormalizedEmail] [nvarchar](256) NULL,
	[EmailConfirmed] [bit] NOT NULL,
	[PasswordHash] [nvarchar](500) NULL,
	[SecurityStamp] [nvarchar](2000) NULL,
	[ConcurrencyStamp] [nvarchar](256) NULL,
	[PhoneNumber] [nvarchar](20) NULL,
	[PhoneNumberConfirmed] [bit] NOT NULL,
	[TwoFactorEnabled] [bit] NOT NULL,
	[LockoutEnd] [datetimeoffset](7) NULL,
	[LockoutEnabled] [bit] NOT NULL,
	[AccessFailedCount] [int] NOT NULL,
 CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AspNetUserTokens]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserTokens](
	[UserId] [bigint] NOT NULL,
	[LoginProvider] [nvarchar](450) NOT NULL,
	[Name] [nvarchar](450) NOT NULL,
	[Value] [nvarchar](max) NULL,
 CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[LoginProvider] ASC,
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AttachedDocument]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttachedDocument](
	[AttachedDocumentID] [int] IDENTITY(1,1) NOT NULL,
	[ParentEntityType] [varchar](50) NULL,
	[ParentEntityID] [int] NULL,
	[MediaType] [varchar](100) NULL,
	[FileName] [varchar](255) NULL,
	[DocumentIdRoot] [varchar](255) NULL,
	[Title] [varchar](max) NULL,
	[TitleReference] [varchar](255) NULL,
	[SectionID] [int] NULL,
	[ComplianceActionID] [int] NULL,
	[ProductID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[AttachedDocumentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BillingUnitIndex]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BillingUnitIndex](
	[BillingUnitIndexID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[PackageNDCValue] [varchar](20) NULL,
	[PackageNDCSystemOID] [varchar](100) NULL,
	[BillingUnitCode] [varchar](5) NULL,
	[BillingUnitCodeSystemOID] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[BillingUnitIndexID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessOperation]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessOperation](
	[BusinessOperationID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentRelationshipID] [int] NULL,
	[OperationCode] [varchar](50) NULL,
	[OperationCodeSystem] [varchar](100) NULL,
	[OperationDisplayName] [varchar](255) NULL,
	[PerformingOrganizationID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[BusinessOperationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessOperationProductLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessOperationProductLink](
	[BusinessOperationProductLinkID] [int] IDENTITY(1,1) NOT NULL,
	[BusinessOperationID] [int] NULL,
	[ProductID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[BusinessOperationProductLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BusinessOperationQualifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BusinessOperationQualifier](
	[BusinessOperationQualifierID] [int] IDENTITY(1,1) NOT NULL,
	[BusinessOperationID] [int] NULL,
	[QualifierCode] [varchar](50) NULL,
	[QualifierCodeSystem] [varchar](100) NULL,
	[QualifierDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[BusinessOperationQualifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CertificationProductLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CertificationProductLink](
	[CertificationProductLinkID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentRelationshipID] [int] NULL,
	[ProductIdentifierID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[CertificationProductLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Characteristic]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Characteristic](
	[CharacteristicID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[PackagingLevelID] [int] NULL,
	[CharacteristicCode] [varchar](50) NULL,
	[CharacteristicCodeSystem] [varchar](100) NULL,
	[ValueType] [varchar](10) NULL,
	[ValuePQ_Value] [decimal](18, 9) NULL,
	[ValuePQ_Unit] [varchar](50) NULL,
	[ValueINT] [int] NULL,
	[ValueCV_Code] [varchar](50) NULL,
	[ValueCV_CodeSystem] [varchar](100) NULL,
	[ValueCV_DisplayName] [varchar](255) NULL,
	[ValueST] [nvarchar](max) NULL,
	[ValueBL] [bit] NULL,
	[ValueIVLPQ_LowValue] [decimal](18, 9) NULL,
	[ValueIVLPQ_LowUnit] [varchar](50) NULL,
	[ValueIVLPQ_HighValue] [decimal](18, 9) NULL,
	[ValueIVLPQ_HighUnit] [varchar](50) NULL,
	[ValueED_MediaType] [varchar](50) NULL,
	[ValueED_FileName] [varchar](255) NULL,
	[ValueNullFlavor] [varchar](10) NULL,
	[MoietyID] [int] NULL,
	[ValueED_CDATAContent] [nvarchar](max) NULL,
	[OriginalText] [nvarchar](256) NULL,
PRIMARY KEY CLUSTERED 
(
	[CharacteristicID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Commodity]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Commodity](
	[CommodityID] [int] IDENTITY(1,1) NOT NULL,
	[CommodityCode] [varchar](50) NULL,
	[CommodityCodeSystem] [varchar](100) NULL,
	[CommodityDisplayName] [varchar](255) NULL,
	[CommodityName] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[CommodityID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ComplianceAction]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ComplianceAction](
	[ComplianceActionID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[PackageIdentifierID] [int] NULL,
	[DocumentRelationshipID] [int] NULL,
	[ActionCode] [varchar](50) NULL,
	[ActionCodeSystem] [varchar](100) NULL,
	[ActionDisplayName] [varchar](255) NULL,
	[EffectiveTimeLow] [date] NULL,
	[EffectiveTimeHigh] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[ComplianceActionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContactParty]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContactParty](
	[ContactPartyID] [int] IDENTITY(1,1) NOT NULL,
	[OrganizationID] [int] NULL,
	[AddressID] [int] NULL,
	[ContactPersonID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ContactPartyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContactPartyTelecom]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContactPartyTelecom](
	[ContactPartyTelecomID] [int] IDENTITY(1,1) NOT NULL,
	[ContactPartyID] [int] NULL,
	[TelecomID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ContactPartyTelecomID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContactPerson]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContactPerson](
	[ContactPersonID] [int] IDENTITY(1,1) NOT NULL,
	[ContactPersonName] [varchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[ContactPersonID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContributingFactor]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContributingFactor](
	[ContributingFactorID] [int] IDENTITY(1,1) NOT NULL,
	[InteractionIssueID] [int] NULL,
	[FactorSubstanceID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ContributingFactorID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DisciplinaryAction]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DisciplinaryAction](
	[DisciplinaryActionID] [int] IDENTITY(1,1) NOT NULL,
	[LicenseID] [int] NULL,
	[ActionCode] [varchar](50) NULL,
	[ActionCodeSystem] [varchar](100) NULL,
	[ActionDisplayName] [varchar](255) NULL,
	[EffectiveTime] [date] NULL,
	[ActionText] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[DisciplinaryActionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Document]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Document](
	[DocumentID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentGUID] [uniqueidentifier] NULL,
	[DocumentCode] [varchar](50) NULL,
	[DocumentCodeSystem] [varchar](100) NULL,
	[DocumentDisplayName] [varchar](255) NULL,
	[Title] [nvarchar](max) NULL,
	[EffectiveTime] [datetime2](0) NULL,
	[SetGUID] [uniqueidentifier] NULL,
	[VersionNumber] [int] NULL,
	[SubmissionFileName] [varchar](255) NULL,
	[DocumentCodeSystemName] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[DocumentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentAuthor]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentAuthor](
	[DocumentAuthorID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentID] [int] NULL,
	[OrganizationID] [int] NULL,
	[AuthorType] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[DocumentAuthorID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentRelationship]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentRelationship](
	[DocumentRelationshipID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentID] [int] NULL,
	[ParentOrganizationID] [int] NULL,
	[ChildOrganizationID] [int] NULL,
	[RelationshipType] [varchar](50) NULL,
	[RelationshipLevel] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[DocumentRelationshipID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentRelationshipIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentRelationshipIdentifier](
	[DocumentRelationshipIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentRelationshipID] [int] NULL,
	[OrganizationIdentifierID] [int] NULL,
 CONSTRAINT [PK_DocumentRelationshipIdentifier] PRIMARY KEY CLUSTERED 
(
	[DocumentRelationshipIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DosingSpecification]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DosingSpecification](
	[DosingSpecificationID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[RouteCode] [varchar](50) NULL,
	[RouteCodeSystem] [varchar](100) NULL,
	[RouteDisplayName] [varchar](255) NULL,
	[DoseQuantityValue] [decimal](18, 9) NULL,
	[DoseQuantityUnit] [varchar](50) NULL,
	[RouteNullFlavor] [nvarchar](128) NULL,
PRIMARY KEY CLUSTERED 
(
	[DosingSpecificationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EquivalentEntity]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EquivalentEntity](
	[EquivalentEntityID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[EquivalenceCode] [varchar](50) NULL,
	[EquivalenceCodeSystem] [varchar](100) NULL,
	[DefiningMaterialKindCode] [varchar](100) NULL,
	[DefiningMaterialKindSystem] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[EquivalentEntityID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FacilityProductLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FacilityProductLink](
	[FacilityProductLinkID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentRelationshipID] [int] NULL,
	[ProductID] [int] NULL,
	[ProductIdentifierID] [int] NULL,
	[ProductName] [nvarchar](500) NULL,
	[IsResolved] [bit] NULL,
PRIMARY KEY CLUSTERED 
(
	[FacilityProductLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GenericMedicine]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GenericMedicine](
	[GenericMedicineID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[GenericName] [nvarchar](512) NULL,
	[PhoneticName] [nvarchar](512) NULL,
PRIMARY KEY CLUSTERED 
(
	[GenericMedicineID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Holder]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Holder](
	[HolderID] [int] IDENTITY(1,1) NOT NULL,
	[MarketingCategoryID] [int] NULL,
	[HolderOrganizationID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[HolderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IdentifiedSubstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IdentifiedSubstance](
	[IdentifiedSubstanceID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[SubjectType] [varchar](50) NULL,
	[SubstanceIdentifierValue] [varchar](100) NULL,
	[SubstanceIdentifierSystemOID] [varchar](100) NULL,
	[IsDefinition] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdentifiedSubstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ingredient]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ingredient](
	[IngredientID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[IngredientSubstanceID] [int] NULL,
	[ClassCode] [varchar](10) NULL,
	[QuantityNumerator] [decimal](18, 9) NULL,
	[QuantityNumeratorUnit] [varchar](50) NULL,
	[QuantityDenominator] [decimal](18, 9) NULL,
	[QuantityDenominatorUnit] [varchar](50) NULL,
	[ReferenceSubstanceID] [int] NULL,
	[IsConfidential] [bit] NULL,
	[SequenceNumber] [int] NULL,
	[ProductConceptID] [int] NULL,
	[SpecifiedSubstanceID] [int] NULL,
	[NumeratorTranslationCode] [varchar](255) NULL,
	[NumeratorCodeSystem] [varchar](255) NULL,
	[NumeratorDisplayName] [varchar](255) NULL,
	[NumeratorValue] [varchar](255) NULL,
	[DenominatorTranslationCode] [varchar](255) NULL,
	[DenominatorCodeSystem] [varchar](255) NULL,
	[DenominatorDisplayName] [varchar](255) NULL,
	[DenominatorValue] [varchar](255) NULL,
	[DisplayName] [varchar](255) NULL,
	[OriginatingElement] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[IngredientID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IngredientInstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IngredientInstance](
	[IngredientInstanceID] [int] IDENTITY(1,1) NOT NULL,
	[FillLotInstanceID] [int] NULL,
	[IngredientSubstanceID] [int] NULL,
	[LotIdentifierID] [int] NULL,
	[ManufacturerOrganizationID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[IngredientInstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IngredientSourceProduct]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IngredientSourceProduct](
	[IngredientSourceProductID] [int] IDENTITY(1,1) NOT NULL,
	[IngredientID] [int] NULL,
	[SourceProductNDC] [varchar](20) NULL,
	[SourceProductNDCSysten] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[IngredientSourceProductID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IngredientSubstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IngredientSubstance](
	[IngredientSubstanceID] [int] IDENTITY(1,1) NOT NULL,
	[UNII] [char](10) NULL,
	[SubstanceName] [nvarchar](1000) NULL,
	[OriginatingElement] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[IngredientSubstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InteractionConsequence]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InteractionConsequence](
	[InteractionConsequenceID] [int] IDENTITY(1,1) NOT NULL,
	[InteractionIssueID] [int] NULL,
	[ConsequenceTypeCode] [varchar](50) NULL,
	[ConsequenceTypeCodeSystem] [varchar](100) NULL,
	[ConsequenceTypeDisplayName] [varchar](255) NULL,
	[ConsequenceValueCode] [varchar](50) NULL,
	[ConsequenceValueCodeSystem] [varchar](100) NULL,
	[ConsequenceValueDisplayName] [varchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[InteractionConsequenceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InteractionIssue]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InteractionIssue](
	[InteractionIssueID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[InteractionCode] [varchar](50) NULL,
	[InteractionCodeSystem] [varchar](100) NULL,
	[InteractionDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[InteractionIssueID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LegalAuthenticator]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LegalAuthenticator](
	[LegalAuthenticatorID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentID] [int] NULL,
	[NoteText] [nvarchar](max) NULL,
	[TimeValue] [datetime2](0) NULL,
	[SignatureText] [nvarchar](max) NULL,
	[AssignedPersonName] [varchar](500) NULL,
	[SignerOrganizationID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[LegalAuthenticatorID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[License]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[License](
	[LicenseID] [int] IDENTITY(1,1) NOT NULL,
	[BusinessOperationID] [int] NULL,
	[LicenseNumber] [varchar](100) NULL,
	[LicenseRootOID] [varchar](100) NULL,
	[LicenseTypeCode] [varchar](50) NULL,
	[LicenseTypeCodeSystem] [varchar](100) NULL,
	[LicenseTypeDisplayName] [varchar](255) NULL,
	[StatusCode] [varchar](20) NULL,
	[ExpirationDate] [date] NULL,
	[TerritorialAuthorityID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[LicenseID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LotHierarchy]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LotHierarchy](
	[LotHierarchyID] [int] IDENTITY(1,1) NOT NULL,
	[ParentInstanceID] [int] NULL,
	[ChildInstanceID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[LotHierarchyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LotIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LotIdentifier](
	[LotIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[LotNumber] [varchar](100) NULL,
	[LotRootOID] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[LotIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MarketingCategory]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MarketingCategory](
	[MarketingCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[CategoryCode] [varchar](50) NULL,
	[CategoryCodeSystem] [varchar](100) NULL,
	[CategoryDisplayName] [varchar](255) NULL,
	[ApplicationOrMonographIDValue] [varchar](100) NULL,
	[ApplicationOrMonographIDOID] [varchar](100) NULL,
	[ApprovalDate] [date] NULL,
	[TerritoryCode] [char](3) NULL,
	[ProductConceptID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MarketingCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MarketingStatus]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MarketingStatus](
	[MarketingStatusID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[PackagingLevelID] [int] NULL,
	[MarketingActCode] [varchar](50) NULL,
	[MarketingActCodeSystem] [varchar](100) NULL,
	[StatusCode] [varchar](20) NULL,
	[EffectiveStartDate] [date] NULL,
	[EffectiveEndDate] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[MarketingStatusID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Moiety]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Moiety](
	[MoietyID] [int] IDENTITY(1,1) NOT NULL,
	[IdentifiedSubstanceID] [int] NULL,
	[MoietyCode] [varchar](50) NULL,
	[MoietyCodeSystem] [varchar](100) NULL,
	[MoietyDisplayName] [varchar](255) NULL,
	[QuantityNumeratorLowValue] [decimal](18, 6) NULL,
	[QuantityNumeratorUnit] [varchar](50) NULL,
	[QuantityNumeratorInclusive] [bit] NULL,
	[QuantityDenominatorValue] [decimal](18, 6) NULL,
	[QuantityDenominatorUnit] [varchar](50) NULL,
	[SequenceNumber] [int] NULL,
 CONSTRAINT [PK_Moiety] PRIMARY KEY CLUSTERED 
(
	[MoietyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[NamedEntity]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NamedEntity](
	[NamedEntityID] [int] IDENTITY(1,1) NOT NULL,
	[OrganizationID] [int] NULL,
	[EntityTypeCode] [varchar](50) NULL,
	[EntityTypeCodeSystem] [varchar](100) NULL,
	[EntityTypeDisplayName] [varchar](255) NULL,
	[EntityName] [varchar](500) NULL,
	[EntitySuffix] [varchar](20) NULL,
PRIMARY KEY CLUSTERED 
(
	[NamedEntityID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[NCTLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NCTLink](
	[NCTLinkID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[NCTNumber] [varchar](20) NULL,
	[NCTRootOID] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[NCTLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ObservationCriterion]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ObservationCriterion](
	[ObservationCriterionID] [int] IDENTITY(1,1) NOT NULL,
	[SubstanceSpecificationID] [int] NULL,
	[ToleranceHighValue] [decimal](18, 9) NULL,
	[ToleranceHighUnit] [varchar](10) NULL,
	[CommodityID] [int] NULL,
	[ApplicationTypeID] [int] NULL,
	[ExpirationDate] [date] NULL,
	[TextNote] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[ObservationCriterionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ObservationMedia]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ObservationMedia](
	[ObservationMediaID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[MediaID] [varchar](100) NULL,
	[DescriptionText] [nvarchar](max) NULL,
	[MediaType] [varchar](50) NULL,
	[FileName] [varchar](255) NULL,
	[XsiType] [varchar](32) NULL,
	[DocumentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ObservationMediaID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Organization]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Organization](
	[OrganizationID] [int] IDENTITY(1,1) NOT NULL,
	[OrganizationName] [varchar](500) NULL,
	[IsConfidential] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[OrganizationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrganizationIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrganizationIdentifier](
	[OrganizationIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[OrganizationID] [int] NULL,
	[IdentifierValue] [varchar](100) NULL,
	[IdentifierSystemOID] [varchar](100) NULL,
	[IdentifierType] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[OrganizationIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrganizationTelecom]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrganizationTelecom](
	[OrganizationTelecomID] [int] IDENTITY(1,1) NOT NULL,
	[OrganizationID] [int] NULL,
	[TelecomID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[OrganizationTelecomID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackageIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackageIdentifier](
	[PackageIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[PackagingLevelID] [int] NULL,
	[IdentifierValue] [varchar](100) NULL,
	[IdentifierSystemOID] [varchar](100) NULL,
	[IdentifierType] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[PackageIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackagingHierarchy]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackagingHierarchy](
	[PackagingHierarchyID] [int] IDENTITY(1,1) NOT NULL,
	[OuterPackagingLevelID] [int] NULL,
	[InnerPackagingLevelID] [int] NULL,
	[SequenceNumber] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[PackagingHierarchyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackagingLevel]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackagingLevel](
	[PackagingLevelID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[PartProductID] [int] NULL,
	[QuantityNumerator] [decimal](18, 9) NULL,
	[QuantityNumeratorUnit] [varchar](50) NULL,
	[PackageFormCode] [varchar](50) NULL,
	[PackageFormCodeSystem] [varchar](100) NULL,
	[PackageFormDisplayName] [varchar](255) NULL,
	[ProductInstanceID] [int] NULL,
	[QuantityDenominator] [decimal](18, 6) NULL,
	[PackageCode] [varchar](64) NULL,
	[PackageCodeSystem] [varchar](64) NULL,
	[NumeratorTranslationCode] [nvarchar](64) NULL,
	[NumeratorTranslationCodeSystem] [nvarchar](64) NULL,
	[NumeratorTranslationDisplayName] [nvarchar](256) NULL,
PRIMARY KEY CLUSTERED 
(
	[PackagingLevelID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PartOfAssembly]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PartOfAssembly](
	[PartOfAssemblyID] [int] IDENTITY(1,1) NOT NULL,
	[PrimaryProductID] [int] NULL,
	[AccessoryProductID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[PartOfAssemblyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PharmacologicClass]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PharmacologicClass](
	[PharmacologicClassID] [int] IDENTITY(1,1) NOT NULL,
	[IdentifiedSubstanceID] [int] NULL,
	[ClassCode] [varchar](50) NULL,
	[ClassCodeSystem] [varchar](100) NULL,
	[ClassDisplayName] [varchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[PharmacologicClassID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PharmacologicClassHierarchy]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PharmacologicClassHierarchy](
	[PharmClassHierarchyID] [int] IDENTITY(1,1) NOT NULL,
	[ChildPharmacologicClassID] [int] NULL,
	[ParentPharmacologicClassID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[PharmClassHierarchyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PharmacologicClassLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PharmacologicClassLink](
	[PharmClassLinkID] [int] IDENTITY(1,1) NOT NULL,
	[ActiveMoietySubstanceID] [int] NULL,
	[PharmacologicClassID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[PharmClassLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PharmacologicClassName]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PharmacologicClassName](
	[PharmClassNameID] [int] IDENTITY(1,1) NOT NULL,
	[PharmacologicClassID] [int] NULL,
	[NameValue] [nvarchar](1000) NULL,
	[NameUse] [char](1) NULL,
PRIMARY KEY CLUSTERED 
(
	[PharmClassNameID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Policy]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Policy](
	[PolicyID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[PolicyClassCode] [varchar](50) NULL,
	[PolicyCode] [varchar](50) NULL,
	[PolicyCodeSystem] [varchar](100) NULL,
	[PolicyDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[PolicyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Product]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Product](
	[ProductID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ProductName] [nvarchar](500) NULL,
	[ProductSuffix] [nvarchar](100) NULL,
	[FormCode] [varchar](50) NULL,
	[FormCodeSystem] [varchar](100) NULL,
	[FormDisplayName] [varchar](255) NULL,
	[DescriptionText] [nvarchar](512) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductConcept]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductConcept](
	[ProductConceptID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ConceptCode] [varchar](36) NULL,
	[ConceptCodeSystem] [varchar](100) NULL,
	[ConceptType] [varchar](20) NULL,
	[FormCode] [varchar](50) NULL,
	[FormCodeSystem] [varchar](100) NULL,
	[FormDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductConceptID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductConceptEquivalence]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductConceptEquivalence](
	[ProductConceptEquivalenceID] [int] IDENTITY(1,1) NOT NULL,
	[ApplicationProductConceptID] [int] NULL,
	[AbstractProductConceptID] [int] NULL,
	[EquivalenceCode] [varchar](10) NULL,
	[EquivalenceCodeSystem] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductConceptEquivalenceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductEvent]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductEvent](
	[ProductEventID] [int] IDENTITY(1,1) NOT NULL,
	[PackagingLevelID] [int] NULL,
	[EventCode] [varchar](50) NULL,
	[EventCodeSystem] [varchar](100) NULL,
	[EventDisplayName] [varchar](255) NULL,
	[QuantityValue] [int] NULL,
	[QuantityUnit] [varchar](50) NULL,
	[EffectiveTimeLow] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductEventID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductIdentifier]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductIdentifier](
	[ProductIdentifierID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[IdentifierValue] [varchar](100) NULL,
	[IdentifierSystemOID] [varchar](100) NULL,
	[IdentifierType] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductInstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductInstance](
	[ProductInstanceID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[InstanceType] [varchar](20) NULL,
	[LotIdentifierID] [int] NULL,
	[ExpirationDate] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductInstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductPart]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductPart](
	[ProductPartID] [int] IDENTITY(1,1) NOT NULL,
	[KitProductID] [int] NULL,
	[PartProductID] [int] NULL,
	[PartQuantityNumerator] [decimal](18, 9) NULL,
	[PartQuantityNumeratorUnit] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductPartID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductRouteOfAdministration]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductRouteOfAdministration](
	[ProductRouteOfAdministrationID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[RouteCode] [varchar](50) NULL,
	[RouteCodeSystem] [varchar](100) NULL,
	[RouteDisplayName] [varchar](255) NULL,
	[RouteNullFlavor] [varchar](10) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductRouteOfAdministrationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductWebLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductWebLink](
	[ProductWebLinkID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[WebURL] [varchar](2048) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProductWebLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Protocol]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Protocol](
	[ProtocolID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ProtocolCode] [varchar](50) NULL,
	[ProtocolCodeSystem] [varchar](100) NULL,
	[ProtocolDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[ProtocolID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ReferenceSubstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ReferenceSubstance](
	[ReferenceSubstanceID] [int] IDENTITY(1,1) NOT NULL,
	[IngredientSubstanceID] [int] NULL,
	[RefSubstanceUNII] [char](10) NULL,
	[RefSubstanceName] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[ReferenceSubstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RelatedDocument]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RelatedDocument](
	[RelatedDocumentID] [int] IDENTITY(1,1) NOT NULL,
	[SourceDocumentID] [int] NULL,
	[RelationshipTypeCode] [varchar](10) NULL,
	[ReferencedSetGUID] [uniqueidentifier] NULL,
	[ReferencedDocumentGUID] [uniqueidentifier] NULL,
	[ReferencedVersionNumber] [int] NULL,
	[ReferencedDocumentCode] [varchar](50) NULL,
	[ReferencedDocumentCodeSystem] [varchar](100) NULL,
	[ReferencedDocumentDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[RelatedDocumentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[REMSApproval]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[REMSApproval](
	[REMSApprovalID] [int] IDENTITY(1,1) NOT NULL,
	[ProtocolID] [int] NULL,
	[ApprovalCode] [varchar](50) NULL,
	[ApprovalCodeSystem] [varchar](100) NULL,
	[ApprovalDisplayName] [varchar](255) NULL,
	[ApprovalDate] [date] NULL,
	[TerritoryCode] [char](3) NULL,
PRIMARY KEY CLUSTERED 
(
	[REMSApprovalID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[REMSElectronicResource]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[REMSElectronicResource](
	[REMSElectronicResourceID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ResourceDocumentGUID] [uniqueidentifier] NULL,
	[Title] [nvarchar](max) NULL,
	[TitleReference] [varchar](100) NULL,
	[ResourceReferenceValue] [varchar](2048) NULL,
PRIMARY KEY CLUSTERED 
(
	[REMSElectronicResourceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[REMSMaterial]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[REMSMaterial](
	[REMSMaterialID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[MaterialDocumentGUID] [uniqueidentifier] NULL,
	[Title] [nvarchar](max) NULL,
	[TitleReference] [varchar](100) NULL,
	[AttachedDocumentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[REMSMaterialID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RenderedMedia]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RenderedMedia](
	[RenderedMediaID] [int] IDENTITY(1,1) NOT NULL,
	[SectionTextContentID] [int] NULL,
	[ObservationMediaID] [int] NULL,
	[SequenceInContent] [int] NULL,
	[IsInline] [bit] NOT NULL,
	[DocumentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[RenderedMediaID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Requirement]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Requirement](
	[RequirementID] [int] IDENTITY(1,1) NOT NULL,
	[ProtocolID] [int] NULL,
	[RequirementSequenceNumber] [int] NULL,
	[IsMonitoringObservation] [bit] NOT NULL,
	[PauseQuantityValue] [decimal](18, 9) NULL,
	[PauseQuantityUnit] [varchar](50) NULL,
	[RequirementCode] [varchar](50) NULL,
	[RequirementCodeSystem] [varchar](100) NULL,
	[RequirementDisplayName] [varchar](500) NULL,
	[OriginalTextReference] [varchar](100) NULL,
	[PeriodValue] [decimal](18, 9) NULL,
	[PeriodUnit] [varchar](50) NULL,
	[StakeholderID] [int] NULL,
	[REMSMaterialID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[RequirementID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ResponsiblePersonLink]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ResponsiblePersonLink](
	[ResponsiblePersonLinkID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[ResponsiblePersonOrgID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ResponsiblePersonLinkID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Section]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Section](
	[SectionID] [int] IDENTITY(1,1) NOT NULL,
	[StructuredBodyID] [int] NULL,
	[SectionGUID] [uniqueidentifier] NULL,
	[SectionCode] [varchar](50) NULL,
	[SectionCodeSystem] [varchar](100) NULL,
	[SectionDisplayName] [varchar](255) NULL,
	[Title] [nvarchar](max) NULL,
	[EffectiveTime] [datetime2](0) NULL,
	[EffectiveTimeLow] [datetime] NULL,
	[EffectiveTimeHigh] [datetime] NULL,
	[SectionLinkGUID] [nvarchar](255) NULL,
	[SectionCodeSystemName] [nvarchar](255) NULL,
	[DocumentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SectionExcerptHighlight]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SectionExcerptHighlight](
	[SectionExcerptHighlightID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[HighlightText] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[SectionExcerptHighlightID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SectionHierarchy]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SectionHierarchy](
	[SectionHierarchyID] [int] IDENTITY(1,1) NOT NULL,
	[ParentSectionID] [int] NULL,
	[ChildSectionID] [int] NULL,
	[SequenceNumber] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SectionHierarchyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SectionTextContent]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SectionTextContent](
	[SectionTextContentID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ContentType] [varchar](20) NULL,
	[SequenceNumber] [int] NULL,
	[ContentText] [nvarchar](max) NULL,
	[StyleCode] [varchar](64) NULL,
	[ParentSectionTextContentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SectionTextContentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SpecializedKind]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SpecializedKind](
	[SpecializedKindID] [int] IDENTITY(1,1) NOT NULL,
	[ProductID] [int] NULL,
	[KindCode] [varchar](50) NULL,
	[KindCodeSystem] [varchar](100) NULL,
	[KindDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[SpecializedKindID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SpecifiedSubstance]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SpecifiedSubstance](
	[SpecifiedSubstanceID] [int] IDENTITY(1,1) NOT NULL,
	[SubstanceCode] [varchar](100) NULL,
	[SubstanceCodeSystem] [varchar](100) NULL,
	[SubstanceCodeSystemName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[SpecifiedSubstanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SplData]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SplData](
	[SplDataID] [bigint] IDENTITY(1,1) NOT NULL,
	[AspNetUsersID] [bigint] NULL,
	[SplDataGUID] [uniqueidentifier] NOT NULL,
	[SplXML] [nvarchar](max) NOT NULL,
	[Archive] [bit] NULL,
	[LogDate] [datetime] NULL,
	[SplXMLHash] [char](64) NULL,
 CONSTRAINT [PK_SplData] PRIMARY KEY CLUSTERED 
(
	[SplDataID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Stakeholder]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Stakeholder](
	[StakeholderID] [int] IDENTITY(1,1) NOT NULL,
	[StakeholderCode] [varchar](50) NULL,
	[StakeholderCodeSystem] [varchar](100) NULL,
	[StakeholderDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[StakeholderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[StakeholderCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StructuredBody]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StructuredBody](
	[StructuredBodyID] [int] IDENTITY(1,1) NOT NULL,
	[DocumentID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[StructuredBodyID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SubstanceSpecification]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SubstanceSpecification](
	[SubstanceSpecificationID] [int] IDENTITY(1,1) NOT NULL,
	[IdentifiedSubstanceID] [int] NULL,
	[SpecCode] [varchar](100) NULL,
	[SpecCodeSystem] [varchar](100) NULL,
	[EnforcementMethodCode] [varchar](50) NULL,
	[EnforcementMethodCodeSystem] [varchar](100) NULL,
	[EnforcementMethodDisplayName] [varchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[SubstanceSpecificationID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Telecom]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Telecom](
	[TelecomID] [int] IDENTITY(1,1) NOT NULL,
	[TelecomType] [varchar](10) NULL,
	[TelecomValue] [varchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[TelecomID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TerritorialAuthority]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TerritorialAuthority](
	[TerritorialAuthorityID] [int] IDENTITY(1,1) NOT NULL,
	[TerritoryCode] [varchar](10) NULL,
	[TerritoryCodeSystem] [varchar](50) NULL,
	[GoverningAgencyIdExtension] [nvarchar](64) NULL,
	[GoverningAgencyIdRoot] [nvarchar](64) NULL,
	[GoverningAgencyName] [nvarchar](256) NULL,
PRIMARY KEY CLUSTERED 
(
	[TerritorialAuthorityID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextList]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextList](
	[TextListID] [int] IDENTITY(1,1) NOT NULL,
	[SectionTextContentID] [int] NULL,
	[ListType] [varchar](20) NULL,
	[StyleCode] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[TextListID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextListItem]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextListItem](
	[TextListItemID] [int] IDENTITY(1,1) NOT NULL,
	[TextListID] [int] NULL,
	[SequenceNumber] [int] NULL,
	[ItemCaption] [nvarchar](100) NULL,
	[ItemText] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[TextListItemID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextTable]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextTable](
	[TextTableID] [int] IDENTITY(1,1) NOT NULL,
	[SectionTextContentID] [int] NULL,
	[Width] [varchar](20) NULL,
	[HasHeader] [bit] NOT NULL,
	[HasFooter] [bit] NOT NULL,
	[Caption] [nvarchar](512) NULL,
	[SectionTableLink] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED 
(
	[TextTableID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextTableCell]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextTableCell](
	[TextTableCellID] [int] IDENTITY(1,1) NOT NULL,
	[TextTableRowID] [int] NULL,
	[CellType] [varchar](5) NULL,
	[SequenceNumber] [int] NULL,
	[CellText] [nvarchar](max) NULL,
	[RowSpan] [int] NULL,
	[ColSpan] [int] NULL,
	[StyleCode] [varchar](100) NULL,
	[Align] [varchar](10) NULL,
	[VAlign] [varchar](10) NULL,
PRIMARY KEY CLUSTERED 
(
	[TextTableCellID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextTableColumn]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextTableColumn](
	[TextTableColumnID] [int] IDENTITY(1,1) NOT NULL,
	[TextTableID] [int] NULL,
	[SequenceNumber] [int] NULL,
	[Width] [nvarchar](50) NULL,
	[Align] [nvarchar](50) NULL,
	[VAlign] [nvarchar](50) NULL,
	[StyleCode] [nvarchar](256) NULL,
	[ColGroupSequenceNumber] [int] NULL,
	[ColGroupStyleCode] [nvarchar](256) NULL,
	[ColGroupAlign] [nvarchar](50) NULL,
	[ColGroupVAlign] [nvarchar](50) NULL,
 CONSTRAINT [PK_TextTableColumn] PRIMARY KEY CLUSTERED 
(
	[TextTableColumnID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TextTableRow]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TextTableRow](
	[TextTableRowID] [int] IDENTITY(1,1) NOT NULL,
	[TextTableID] [int] NULL,
	[RowGroupType] [varchar](10) NULL,
	[SequenceNumber] [int] NULL,
	[StyleCode] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[TextTableRowID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WarningLetterDate]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WarningLetterDate](
	[WarningLetterDateID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[AlertIssueDate] [date] NULL,
	[ResolutionDate] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[WarningLetterDateID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WarningLetterProductInfo]    Script Date: 10/24/2025 2:32:30 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WarningLetterProductInfo](
	[WarningLetterProductInfoID] [int] IDENTITY(1,1) NOT NULL,
	[SectionID] [int] NULL,
	[ProductName] [nvarchar](500) NULL,
	[GenericName] [nvarchar](512) NULL,
	[FormCode] [varchar](50) NULL,
	[FormCodeSystem] [varchar](100) NULL,
	[FormDisplayName] [varchar](255) NULL,
	[StrengthText] [nvarchar](1000) NULL,
	[ItemCodesText] [nvarchar](1000) NULL,
PRIMARY KEY CLUSTERED 
(
	[WarningLetterProductInfoID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_AspNetRoleClaims_RoleId]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims]
(
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [RoleNameIndex]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles]
(
	[NormalizedName] ASC
)
WHERE ([NormalizedName] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ActivityLog_ActivityType]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_ActivityType] ON [dbo].[AspNetUserActivityLog]
(
	[ActivityType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ActivityLog_Controller_Action]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_Controller_Action] ON [dbo].[AspNetUserActivityLog]
(
	[ControllerName] ASC,
	[ActionName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ActivityLog_ExecutionTime]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_ExecutionTime] ON [dbo].[AspNetUserActivityLog]
(
	[ExecutionTimeMs] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ActivityLog_ResponseStatus]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_ResponseStatus] ON [dbo].[AspNetUserActivityLog]
(
	[ResponseStatusCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ActivityLog_Timestamp]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_Timestamp] ON [dbo].[AspNetUserActivityLog]
(
	[ActivityTimestamp] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ActivityLog_UserId]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_ActivityLog_UserId] ON [dbo].[AspNetUserActivityLog]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AspNetUserClaims_UserId]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AspNetUserLogins_UserId]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AspNetUserRoles_RoleId]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles]
(
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [EmailIndex]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [EmailIndex] ON [dbo].[AspNetUsers]
(
	[NormalizedEmail] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Users_LastActivityAt]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_Users_LastActivityAt] ON [dbo].[AspNetUsers]
(
	[LastActivityAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UserNameIndex]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers]
(
	[NormalizedUserName] ASC
)
WHERE ([NormalizedUserName] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Users_CanonicalUsername_Active]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_CanonicalUsername_Active] ON [dbo].[AspNetUsers]
(
	[CanonicalUsername] ASC
)
WHERE ([DeletedAt] IS NULL AND [CanonicalUsername] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_Users_PrimaryEmail_Active]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Users_PrimaryEmail_Active] ON [dbo].[AspNetUsers]
(
	[PrimaryEmail] ASC
)
WHERE ([DeletedAt] IS NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_DocumentRelationshipIdentifier_DocumentRelationshipID]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_DocumentRelationshipID] ON [dbo].[DocumentRelationshipIdentifier]
(
	[DocumentRelationshipID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_DocumentRelationshipIdentifier_OrganizationIdentifierID]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_DocumentRelationshipIdentifier_OrganizationIdentifierID] ON [dbo].[DocumentRelationshipIdentifier]
(
	[OrganizationIdentifierID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_DocumentRelationshipIdentifier_Unique]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_DocumentRelationshipIdentifier_Unique] ON [dbo].[DocumentRelationshipIdentifier]
(
	[DocumentRelationshipID] ASC,
	[OrganizationIdentifierID] ASC
)
WHERE ([DocumentRelationshipID] IS NOT NULL AND [OrganizationIdentifierID] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TextTableColumn_ColGroupSequenceNumber]    Script Date: 10/24/2025 2:32:30 PM ******/
CREATE NONCLUSTERED INDEX [IX_TextTableColumn_ColGroupSequenceNumber] ON [dbo].[TextTableColumn]
(
	[TextTableID] ASC,
	[ColGroupSequenceNumber] ASC
)
INCLUDE([SequenceNumber]) 
WHERE ([ColGroupSequenceNumber] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AspNetUserActivityLog] ADD  DEFAULT (getutcdate()) FOR [ActivityTimestamp]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (CONVERT([bit],(0))) FOR [MfaEnabled]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT ((0)) FOR [FailedLoginCount]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (N'User') FOR [UserRole]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (N'UTC') FOR [Timezone]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (N'en-US') FOR [Locale]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (CONVERT([bit],(0))) FOR [TosMarketingOptIn]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (CONVERT([bit],(0))) FOR [TosEmailNotification]
GO
ALTER TABLE [dbo].[AspNetUsers] ADD  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[BillingUnitIndex] ADD  DEFAULT ('2.16.840.1.113883.6.69') FOR [PackageNDCSystemOID]
GO
ALTER TABLE [dbo].[BillingUnitIndex] ADD  DEFAULT ('2.16.840.1.113883.2.13') FOR [BillingUnitCodeSystemOID]
GO
ALTER TABLE [dbo].[DocumentAuthor] ADD  DEFAULT ('Labeler') FOR [AuthorType]
GO
ALTER TABLE [dbo].[FacilityProductLink] ADD  CONSTRAINT [DF_FacilityProductLink_IsResolved]  DEFAULT ((0)) FOR [IsResolved]
GO
ALTER TABLE [dbo].[IdentifiedSubstance] ADD  DEFAULT ((0)) FOR [IsDefinition]
GO
ALTER TABLE [dbo].[Ingredient] ADD  DEFAULT ((0)) FOR [IsConfidential]
GO
ALTER TABLE [dbo].[IngredientSourceProduct] ADD  DEFAULT ('2.16.840.1.113883.6.69') FOR [SourceProductNDCSysten]
GO
ALTER TABLE [dbo].[NCTLink] ADD  DEFAULT ('2.16.840.1.113883.3.1077') FOR [NCTRootOID]
GO
ALTER TABLE [dbo].[ObservationCriterion] ADD  DEFAULT ('[ppm]') FOR [ToleranceHighUnit]
GO
ALTER TABLE [dbo].[Organization] ADD  DEFAULT ((0)) FOR [IsConfidential]
GO
ALTER TABLE [dbo].[ProductConcept] ADD  DEFAULT ('2.16.840.1.113883.3.3389') FOR [ConceptCodeSystem]
GO
ALTER TABLE [dbo].[ProductConceptEquivalence] ADD  DEFAULT ('2.16.840.1.113883.3.2964') FOR [EquivalenceCodeSystem]
GO
ALTER TABLE [dbo].[Requirement] ADD  DEFAULT ((0)) FOR [IsMonitoringObservation]
GO
ALTER TABLE [dbo].[SplData] ADD  CONSTRAINT [DF_SplData_Archive]  DEFAULT ((0)) FOR [Archive]
GO
ALTER TABLE [dbo].[SplData] ADD  CONSTRAINT [DF_SplData_LogDate]  DEFAULT (getutcdate()) FOR [LogDate]
GO
ALTER TABLE [dbo].[TextTable] ADD  DEFAULT ((0)) FOR [HasHeader]
GO
ALTER TABLE [dbo].[TextTable] ADD  DEFAULT ((0)) FOR [HasFooter]
GO
ALTER TABLE [dbo].[AspNetRoleClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetRoleClaims] CHECK CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
GO
ALTER TABLE [dbo].[AspNetUserActivityLog]  WITH CHECK ADD  CONSTRAINT [FK_ActivityLog_AspNetUsers] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserActivityLog] CHECK CONSTRAINT [FK_ActivityLog_AspNetUsers]
GO
ALTER TABLE [dbo].[AspNetUserClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserClaims] CHECK CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserLogins]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserLogins] CHECK CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserRoles] CHECK CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId]
GO
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserRoles] CHECK CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserTokens]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserTokens] CHECK CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[ComplianceAction]  WITH CHECK ADD  CONSTRAINT [CK_ComplianceAction_Target] CHECK  (([PackageIdentifierID] IS NOT NULL AND [DocumentRelationshipID] IS NULL OR [PackageIdentifierID] IS NULL AND [DocumentRelationshipID] IS NOT NULL))
GO
ALTER TABLE [dbo].[ComplianceAction] CHECK CONSTRAINT [CK_ComplianceAction_Target]
GO
ALTER TABLE [dbo].[FacilityProductLink]  WITH CHECK ADD  CONSTRAINT [CK_FacilityProductLink_Target] CHECK  (([ProductID] IS NOT NULL OR [ProductIdentifierID] IS NOT NULL OR [ProductName] IS NOT NULL))
GO
ALTER TABLE [dbo].[FacilityProductLink] CHECK CONSTRAINT [CK_FacilityProductLink_Target]
GO
ALTER TABLE [dbo].[Ingredient]  WITH CHECK ADD  CONSTRAINT [CK_Ingredient_ProductOrConcept] CHECK  (([ProductID] IS NOT NULL AND [ProductConceptID] IS NULL OR [ProductID] IS NULL AND [ProductConceptID] IS NOT NULL))
GO
ALTER TABLE [dbo].[Ingredient] CHECK CONSTRAINT [CK_Ingredient_ProductOrConcept]
GO
ALTER TABLE [dbo].[MarketingCategory]  WITH CHECK ADD  CONSTRAINT [CK_MarketingCategory_ProductOrConcept] CHECK  (([ProductID] IS NOT NULL AND [ProductConceptID] IS NULL OR [ProductID] IS NULL AND [ProductConceptID] IS NOT NULL))
GO
ALTER TABLE [dbo].[MarketingCategory] CHECK CONSTRAINT [CK_MarketingCategory_ProductOrConcept]
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UNII code of the active moiety (<activeMoiety><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyUNII'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 76, Para 3.2.4.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyUNII'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the active moiety (<activeMoiety><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 76, Para 3.2.4.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety', @level2type=N'COLUMN',@level2name=N'MoietyName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores active moiety details linked to an IngredientSubstance. Based on Section 3.1.4, 3.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, 76' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ActiveMoiety'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the type of identifier (e.g., C99286 Model Number, C99285 Catalog Number, C99287 Reference Number).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43 (Table 3), Page 90 (Para 3.3.2.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The actual identifier value (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, Page 90 (Para 3.3.2.6)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID associated with the identifier (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, Page 90 (Para 3.3.2.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores additional product identifiers like Model Number, Catalog Number, Reference Number. Based on Section 3.1.3, 3.3.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 42, 90' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AdditionalIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'First line of the street address (<streetAddressLine>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine1'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine1'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Second line of the street address (<streetAddressLine>), optional.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine2'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StreetAddressLine2'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'City name (<city>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'City'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'City'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'State or province (<state>), required if country is USA.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StateProvince'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, Para 2.1.6.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'StateProvince'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Postal or ZIP code (<postalCode>), required if country is USA.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'PostalCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, Para 2.1.6.4, 2.1.6.5, 2.1.6.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'PostalCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ISO 3166-1 alpha-3 country code (<country code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Full country name (<country> name).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address', @level2type=N'COLUMN',@level2name=N'CountryName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores address information for organizations or contact parties. Based on Section 2.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Address'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Substance Specification to the analyte(s) being measured (<analyte><identifiedSubstance>). Based on Section 19.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Analyte'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 189, 190 (Para 19.2.3.13)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Analyte'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the application type (e.g., General Tolerance).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType', @level2type=N'COLUMN',@level2name=N'AppTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType', @level2type=N'COLUMN',@level2name=N'AppTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores application type details referenced in tolerance specifications (<subjectOf><approval><code>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ApplicationType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for the activity log entry (auto-increment)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ActivityLogId'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to AspNetUsers. Identifies the user who performed the activity. NULL for anonymous/unauthenticated users.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'UserId'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of activity performed (Login, Logout, Create, Read, Update, Delete, Other)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ActivityType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp when the activity occurred' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ActivityTimestamp'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Human-readable description of the activity' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'Description'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'IP address of the client (IPv4 or IPv6, supports X-Forwarded-For)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'IpAddress'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User-Agent string from the HTTP request header' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'UserAgent'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'URL path of the request (without query string)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'RequestPath'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the controller that handled the request' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ControllerName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the action method that was executed' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ActionName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'HTTP method used (GET, POST, PUT, PATCH, DELETE, etc.)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'HttpMethod'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Action parameters serialized as JSON (sensitive data excluded)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'RequestParameters'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'HTTP response status code (200, 400, 404, 500, etc.)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ResponseStatusCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Execution time of the action in milliseconds' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ExecutionTimeMs'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Overall result status (Success, Error, Warning)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'Result'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Error message if an exception occurred' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ErrorMessage'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type name of the exception that occurred' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'ExceptionType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Full stack trace for debugging (populated on errors)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'StackTrace'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Session identifier for correlating requests within the same session' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog', @level2type=N'COLUMN',@level2name=N'SessionId'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores comprehensive activity logs for user actions and controller executions. Tracks request details, performance metrics, and error information for auditing and analysis.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUserActivityLog'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Surrogate primary key (BIGINT IDENTITY) for the user.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'Id'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Lower-/case-folded username used for uniqueness checks and login. Typically mirrors NormalizedUserName.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'CanonicalUsername'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Friendly name displayed in the UI; can be non-unique and user-modifiable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'DisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary email address (RFC 5322 max length 320 chars). Often mirrors the Email field.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'PrimaryEmail'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Boolean flag (0/1) indicating whether multi-factor authentication is currently active for the user. Typically mirrors TwoFactorEnabled.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'MfaEnabled'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp of the most recent password change or reset. Useful for security auditing and policy enforcement.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'PasswordChangedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Custom field tracking consecutive failed login attempts since the last successful login; reset on success. Used for lockout policy. Note: ASP.NET Identity uses AccessFailedCount by default.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'FailedLoginCount'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Custom field for UTC timestamp until which the account is locked out due to repeated failed login attempts. Note: ASP.NET Identity uses LockoutEnd (DATETIMEOFFSET) by default.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LockoutUntil'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Encrypted Time-based One-Time Password (TOTP) seed or other MFA credential (e.g., WebAuthn ID) used to validate MFA challenges.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'MfaSecret'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Coarse-grained role assignment for Role-Based Access Control (RBAC) (e.g., ''User'', ''Admin''). May be extended via a separate roles table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UserRole'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'JSON blob or delimited string representing fine-grained permissions granted to the user, complementing UserRole for detailed authorization.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UserPermissions'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'IANA timezone identifier (e.g., ''America/New_York'', ''Europe/London'') to localize dates and times displayed to the user.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'Timezone'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Locale/region code (e.g., ''en-US'', ''fr-FR'') for content localization, affecting language, date formats, and number formats.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'Locale'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'JSON-encoded set of user preferences for various types of notifications (e.g., email, SMS, push notifications).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'NotificationSettings'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User''s preferred UI theme (e.g., ''Dark'', ''Light'', ''SystemDefault'', ''HighContrast'') for personalization.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UiTheme'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Version string or identifier of the Terms of Service (ToS) that the user has accepted (e.g., ''v3.2-20250115'').' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'TosVersionAccepted'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp when the user accepted the current or a specific version of the Terms of Service.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'TosAcceptedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Boolean flag (0/1) indicating whether the user has opted-in to receive marketing communications, relevant for GDPR and other privacy regulations.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'TosMarketingOptIn'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Boolean flag (0/1) indicating whether the user has agreed to receive email notifications (transactional, system alerts, etc.).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'TosEmailNotification'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'JSON blob or delimited string storing IDs of entities (e.g., other users, topics, documents) that this user is following, for social or notification features.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UserFollowing'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Record creation timestamp (UTC), indicating when the user account was first created.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'CreatedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier (e.g., UserID of an admin or system process ID) of the entity that created this user record; NULL for self-registration.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'CreatedByID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp of the most recent update to the user''s profile or settings.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UpdatedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier (e.g., UserID or system process ID) of the entity that performed the most recent update.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UpdatedBy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Soft-delete marker (UTC timestamp); non-NULL rows are considered deleted and typically excluded from normal queries. Essential for GDPR ''right to be forgotten'' compliance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'DeletedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp when the account was administratively suspended, distinct from soft deletion (e.g., for temporary deactivation).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'SuspendedAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Reason provided for administrative suspension (e.g., policy violation, security concern, fraud investigation).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'SuspensionReason'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp of the last successful login, used for activity tracking and identifying dormant accounts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LastLoginAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp of the user''s most recent detected activity within the application (API call, UI interaction, etc.).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LastActivityAt'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'IP address (IPv4 or IPv6) recorded during the user''s last login or significant activity; consider anonymization or truncation based on privacy policies.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LastIpAddress'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User''s chosen username, often used for login.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'UserName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Normalized (e.g., uppercase) version of UserName, used for efficient lookups and uniqueness checks.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'NormalizedUserName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User''s email address, may be used for login and communication.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'Email'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Normalized (e.g., uppercase) version of Email, used for efficient lookups.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'NormalizedEmail'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the user''s email address has been verified/confirmed (1 if true, 0 if false).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'EmailConfirmed'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Password hash produced by a strong one-way hashing algorithm (e.g., PBKDF2, bcrypt, Argon2id); plaintext passwords are never stored.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'PasswordHash'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'A random GUID or string that is regenerated when security-sensitive information (like password or MFA settings) changes, used to invalidate existing sessions/tokens.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'SecurityStamp'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'A random value that changes whenever a user is persisted to the store, used for optimistic concurrency control.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'ConcurrencyStamp'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User''s phone number, optionally used for multi-factor authentication (MFA) or account recovery.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'PhoneNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the user''s phone number has been verified/confirmed (1 if true, 0 if false).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'PhoneNumberConfirmed'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if two-factor authentication is enabled for the user (1 if true, 0 if false).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'TwoFactorEnabled'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC date and time until which the user is locked out, if lockout is enabled and triggered. This is a DATETIMEOFFSET.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LockoutEnd'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if account lockout is enabled for this user (1 if true, 0 if false).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'LockoutEnabled'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The number of consecutive failed login attempts for the user; reset on successful login. This is the standard Identity field for this purpose.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers', @level2type=N'COLUMN',@level2name=N'AccessFailedCount'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores user account information for the application, including credentials, contact details, security settings, preferences, and audit trails, compatible with ASP.NET Identity.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AspNetUsers'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the AttachedDocument table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'AttachedDocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'(Legacy) Identifies the type of the parent element containing the document reference (e.g., "DisciplinaryAction").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'ParentEntityType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'(Legacy) Foreign key to the parent table (e.g., DisciplinaryActionID).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'ParentEntityID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'MIME type of the attached document (e.g., "application/pdf").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'MediaType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (Para 18.1.7.16), Page 218 (Para 23.2.9.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'MediaType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'File name of the attached document.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'FileName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (Para 18.1.7.17), Page 218 (Para 23.2.9.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'FileName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root identifier of the document from the <id> element, required for REMS materials (SPL IG 23.2.9.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'DocumentIdRoot'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The title of the document reference (SPL IG 23.2.9.2).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'Title'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The ID referenced within the document''s title, linking it to content in the section text (SPL IG 23.2.9.3).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'TitleReference'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to the Section where this document is referenced. Can be null.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'SectionID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to a ComplianceAction, if the document is part of a drug listing or establishment inactivation. Can be null.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'ComplianceActionID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to a Product, if the document is related to a specific product (e.g., REMS material). Can be null.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument', @level2type=N'COLUMN',@level2name=N'ProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to attached documents (e.g., PDFs for Disciplinary Actions, REMS Materials). Based on SPL IG 18.1.7 and 23.2.9.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185 (18.1.7), Page 218 (23.2.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'AttachedDocument'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The NDC Package Code being linked (<containerPackagedProduct><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'PackageNDCValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146, Para 12.2.2.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'PackageNDCValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The NCPDP Billing Unit Code associated with the NDC package (GM, ML, or EA).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146 (Para 12.2.3), Page 147 (Para 12.2.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system OID for the NCPDP Billing Unit Code (2.16.840.1.113883.2.13).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCodeSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 146 (Para 12.2.3), Page 147 (Para 12.2.3.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex', @level2type=N'COLUMN',@level2name=N'BillingUnitCodeSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the link between an NDC Package Code and its NCPDP Billing Unit, from Indexing - Billing Unit (71446-9) documents. Based on Section 12.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 145' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BillingUnitIndex'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the business operation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the operation code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the operation code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 108 (Para 4.1.4.7), Page 121 (Para 5.1.5.4), Page 129 (Para 6.1.6.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'OperationDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N' Organization performing the operation ([performance][actDefinition][code code="code"])' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation', @level2type=N'COLUMN',@level2name=N'PerformingOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores business operation details for an establishment or labeler (<performance><actDefinition>). Based on Section 4.1.4, 5.1.5, 6.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 107, 120, 129' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperation'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Business Operation performed by an establishment to a specific product (<actDefinition><product>). Based on Section 4.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 111' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code qualifying the business operation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.8), Page 130 (Para 6.1.7.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the qualifier code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.9), Page 130 (Para 6.1.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the qualifier code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121 (Para 5.1.5.10), Page 130 (Para 6.1.7.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier', @level2type=N'COLUMN',@level2name=N'QualifierDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores qualifier details for a specific Business Operation (<actDefinition><subjectOf><approval><code>). Based on Section 5.1.5, 6.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 121, 130' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'BusinessOperationQualifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an establishment (within a Blanket No Changes Certification doc) to a product being certified (<performance><actDefinition><product>). Based on Section 28.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'CertificationProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 226' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'CertificationProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the characteristic property. Traditional: SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS. Enhanced for substance indexing: C103240 for Chemical Structure characteristics.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'CharacteristicCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 64, 66, 67, 68, 84, 95, 104, 259' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'CharacteristicCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates the XML Schema instance type of the <value> element (e.g., PQ, INT, CV, ST, BL, IVL_PQ, ED).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'ValueType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 65, 67' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'ValueType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to Moiety table (if characteristic applies to a chemical moiety). Used for substance indexing to link chemical structure data to specific molecular components within a substance definition per ISO/FDIS 11238. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'MoietyID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Raw CDATA content for ED type chemical structure characteristics. Contains molecular structure data in format specified by ValueED_MediaType (MOLFILE, InChI, InChI-Key). Preserves exact formatting for scientific integrity per FDA Substance Registration System requirements.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'ValueED_CDATAContent'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional original color description or flavor text' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic', @level2type=N'COLUMN',@level2name=N'OriginalText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores characteristics of products, packages, or substance moieties ([subjectOf][characteristic]). Enhanced for FDA Substance Indexing to support chemical structure data including MOLFILE format and InChI identifiers per ISO/FDIS 11238.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 64' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Characteristic'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the commodity.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity', @level2type=N'COLUMN',@level2name=N'CommodityCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity', @level2type=N'COLUMN',@level2name=N'CommodityCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores commodity details referenced in tolerance specifications (<subject><presentSubstance><presentSubstance>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Commodity'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the specific package NDC being inactivated/reactivated (Section 30).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'PackageIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 237' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'PackageIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the DocumentRelationship representing the establishment being inactivated/reactivated (Section 31).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 241' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the compliance action (e.g., C162847 Inactivated).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'ActionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.2), Page 242 (Para 31.1.4.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'ActionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the inactivation begins.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.3), Page 242 (Para 31.1.4.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the inactivation ends (reactivation date), if applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeHigh'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238 (Para 30.2.3.1), Page 242 (Para 31.1.4.1)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction', @level2type=N'COLUMN',@level2name=N'EffectiveTimeHigh'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores FDA-initiated inactivation/reactivation status for Drug Listings (linked via PackageIdentifierID) or Establishment Registrations (linked via DocumentRelationshipID). Based on Section 30.2.3, 31.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 238, 242' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ComplianceAction'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the Organization this contact party belongs to.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'OrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'OrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the Address for this contact party.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'AddressID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'AddressID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key linking to the ContactPerson for this contact party.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'ContactPersonID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty', @level2type=N'COLUMN',@level2name=N'ContactPersonID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the <contactParty> element, linking Organization, Address, Telecom, and ContactPerson. Based on Section 2.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactParty'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Junction table to link ContactParty with multiple Telecom entries (typically tel and mailto ).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPartyTelecom'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPartyTelecom'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the contact person (<contactPerson><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson', @level2type=N'COLUMN',@level2name=N'ContactPersonName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, Para 2.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson', @level2type=N'COLUMN',@level2name=N'ContactPersonName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores contact person details. Based on Section 2.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactPerson'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the IdentifiedSubstance representing the drug or pharmacologic class that is the contributing factor.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor', @level2type=N'COLUMN',@level2name=N'FactorSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 248, Para 32.2.4.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor', @level2type=N'COLUMN',@level2name=N'FactorSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an InteractionIssue to the contributing substance/class (<issue><subject><substanceAdministrationCriterion>). Based on Section 32.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContributingFactor'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the disciplinary action type (e.g., suspension, revocation, activation).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the disciplinary action became effective.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description used when the action code is ''other''.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 185, Para 18.1.7.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction', @level2type=N'COLUMN',@level2name=N'ActionText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores disciplinary action details related to a License (<approval><subjectOf><action>). Based on Section 18.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 184' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DisciplinaryAction'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Globally Unique Identifier for this specific document version (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LOINC code identifying the document type (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the document type code (<code> codeSystem), typically 2.16.840.1.113883.6.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the document type code (<code> displayName).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document title (<title>), if provided.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'Title'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'Title'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date reference for the SPL version (<effectiveTime value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.11' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Globally Unique Identifier for the document set, constant across versions (<setId root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SetGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.3.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SetGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sequential integer for the document version (<versionNumber value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'VersionNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 13, Para 2.1.3.15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'VersionNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the submitted XML file (e.g., DocumentGUID.xml).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SubmissionFileName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12, Para 2.1.2.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'SubmissionFileName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the code system used for document classification. Identifies the coding standard or vocabulary used to categorize and classify the document type or content.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document', @level2type=N'COLUMN',@level2name=N'DocumentCodeSystemName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the main metadata for each SPL document version. Based on Section 2.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Document'
GO
EXEC sys.sp_addextendedproperty @name=N'AuthorType', @value=N'Identifies the type or role of the author, e.g., Labeler (4.1.2 ), FDA (8.1.2, 15.1.2, 20.1.2, 21.1.2, 30.1.2, 31.1.2, 32.1.2, 33.1.2 ), NCPDP (12.1.2 ).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor', @level2type=N'COLUMN',@level2name=N'AuthorType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links Documents to Authoring Organizations (typically the Labeler). Based on Section 2.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentAuthor'
GO
EXEC sys.sp_addextendedproperty @name=N'RelationshipType', @value=N'Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3), RegistrantToEstablishment (4.1.4), EstablishmentToUSagent (6.1.4), EstablishmentToImporter (6.1.5), LabelerToDetails (5.1.3), FacilityToParentCompany (35.1.6), LabelerToParentCompany (36.1.2.5), DocumentToBulkLotManufacturer (16.1.3).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship', @level2type=N'COLUMN',@level2name=N'RelationshipType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines hierarchical relationships between organizations within a document header (e.g., Labeler -> Registrant -> Establishment).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationship'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the DocumentRelationshipIdentifier table. Auto-incrementing identity column.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationshipIdentifier', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to DocumentRelationship table. Identifies which document relationship (hierarchy level) this identifier was used in. Logical relationship only - no database FK constraint. Example: links to the registrant-to-establishment relationship where DUNS 830995189 appeared.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationshipIdentifier', @level2type=N'COLUMN',@level2name=N'DocumentRelationshipID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to OrganizationIdentifier table. Identifies which specific identifier (DUNS, FEI, etc.) appeared at this hierarchy level in the original XML. Logical relationship only - no database FK constraint. Example: links to the OrganizationIdentifier record containing DUNS 830995189 for Henry Schein.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationshipIdentifier', @level2type=N'COLUMN',@level2name=N'OrganizationIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links organization identifiers to specific document relationships, preserving which identifier (e.g., DUNS number) was used at which hierarchy level in the SPL author section. Enables accurate rendering that matches the original XML structure. No database FK constraints for performance; referential integrity maintained in application code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DocumentRelationshipIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Route of administration associated with the dose.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'RouteCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, Para 16.2.4.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'RouteCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit representing a single dose.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'DoseQuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, Para 16.2.4.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'DoseQuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'NullFlavor attribute for route code when the specific route is unknown or not applicable. Allows for flexible handling of route specifications in SPL documents.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification', @level2type=N'COLUMN',@level2name=N'RouteNullFlavor'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores dose-related specifications for a product (e.g., doseQuantity, rateQuantity, routeCode).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DosingSpecification'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of equivalence relationship, e.g., C64637 (Same), pending (Predecessor).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'EquivalenceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 41, Table 2 & Para 3.1.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'EquivalenceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Item code of the equivalent product (e.g., source NDC product code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, 41 (Para 3.1.2.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the equivalent product''s item code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity', @level2type=N'COLUMN',@level2name=N'DefiningMaterialKindSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores relationships indicating equivalence to other products (e.g., product source, predecessor). Based on Section 3.1.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, 41' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'EquivalentEntity'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link via Cosmetic Listing Number.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductIdentifierID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link via Product Name (used if CLN not yet assigned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Facility (in Registration or Listing docs) to a Cosmetic Product (<performance><actDefinition><product>). Link via ProductID, ProductIdentifierID (CLN), or ProductName. Based on Section 35.2.2, 36.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 260, 267' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FacilityProductLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Non-proprietary name of the product (<genericMedicine><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'GenericName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 69, Para 3.2.1.24' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'GenericName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Phonetic spelling of the generic name (<name use="PHON">), optional.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'PhoneticName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 70, Para 3.2.1.28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine', @level2type=N'COLUMN',@level2name=N'PhoneticName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores non-proprietary (generic) medicine names associated with a Product. Based on Section 3.1.1, 3.2.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 69' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'GenericMedicine'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the Organization table for the Application Holder.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder', @level2type=N'COLUMN',@level2name=N'HolderOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 208, Para 23.2.3.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder', @level2type=N'COLUMN',@level2name=N'HolderOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the Application Holder organization linked to a Marketing Category for REMS products (<holder><role><playingOrganization>). Based on Section 23.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 208' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Holder'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates whether the identified substance represents an Active Moiety (8.2.2) or a Pharmacologic Class being defined (8.2.3).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubjectType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier value - UNII for Active Moiety, MED-RT/MeSH code for Pharm Class.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135 (Para 8.2.2.2), Page 136 (Para 8.2.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier system OID - UNII (2.16.840.1.113883.4.9), MED-RT (2.16.840.1.113883.6.345), or MeSH (2.16.840.1.113883.6.177).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135 (Para 8.2.2.3), Page 136 (Para 8.2.2.11)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceIdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if this row defines the substance/class (8.2.3) or references it (8.2.2).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance', @level2type=N'COLUMN',@level2name=N'IsDefinition'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores substance details (e.g., active moiety, pharmacologic class identifier) used in Indexing contexts (<subject><identifiedSubstance>). Based on Section 8.2.2, 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135, 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IdentifiedSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the Ingredient table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IngredientID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to Product or Product representing a Part. Null if linked via ProductConceptID.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to IngredientSubstance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, Page 45 (Para 3.1.4.1), Page 74 (3.2.3), Page 77 (3.2.6), Page 91 (3.3.4), Page 101 (3.4.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Strength expressed as numerator/denominator value and unit (<quantity>). Null for CNTM unless zero numerator.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, Page 45 (Para 3.1.4.2, 3.1.4.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Corresponds to <quantity><numerator unit>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityNumeratorUnit'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Corresponds to <quantity><denominator value>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityDenominator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Corresponds to <quantity><denominator unit>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'QuantityDenominatorUnit'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ReferenceSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ReferenceSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the inactive ingredient information is confidential (<confidentialityCode code="B">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IsConfidential'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'IsConfidential'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Order of the ingredient as listed in the SPL file (important for cosmetics).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 101' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product. Null if linked via ProductID.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ProductConceptID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 156' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'ProductConceptID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key for the SpecifiedSubstance table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'SpecifiedSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation code="C28253")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'NumeratorTranslationCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'NumeratorCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'NumeratorDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation value="50")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'NumeratorValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation code="C28253")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'DenominatorTranslationCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'DenominatorCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'DenominatorDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Translation attribute for the numerator (e.g., translation value="50")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'DenominatorValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name (displayName="MILLIGRAM").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'DisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N' The name of the XML element this ingredient was parsed from (e.g., "ingredient", "activeIngredient").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient', @level2type=N'COLUMN',@level2name=N'OriginatingElement'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents an ingredient instance within a product, part, or product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.4, 15.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43, 156' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Ingredient'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Reference to the substance constituting the bulk lot.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 170, Para 16.2.6.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Reference to the Organization that manufactured the bulk lot.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'ManufacturerOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 170, Para 16.2.6.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance', @level2type=N'COLUMN',@level2name=N'ManufacturerOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents Bulk Lot information in Lot Distribution Reports (<productInstance><ingredient>). Based on Section 16.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 169' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientInstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'NDC Product Code of the source product used for the ingredient.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct', @level2type=N'COLUMN',@level2name=N'SourceProductNDC'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44, Para 3.1.4.12, 3.1.4.14' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct', @level2type=N'COLUMN',@level2name=N'SourceProductNDC'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an Ingredient to its source product NDC (used in compounded drugs). Based on Section 3.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSourceProduct'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the IngredientSubstance table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'IngredientSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique Ingredient Identifier (<code code=> where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'UNII'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 45, Para 3.1.4.7, 3.1.4.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'UNII'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the substance (name).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 45, Para 3.1.4.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The name of the XML element this ingredient was parsed from (e.g., "inactiveIngredientSubstance").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance', @level2type=N'COLUMN',@level2name=N'OriginatingElement'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details about a unique substance (identified primarily by UNII). Based on Section 3.1.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 43' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'IngredientSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of consequence: Pharmacokinetic effect (C54386) or Medical problem (44100-6).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the specific pharmacokinetic effect or medical problem.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the value code (NCI Thesaurus 2.16.840.1.113883.3.26.1.1 or SNOMED CT 2.16.840.1.113883.6.96).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 249, Para 32.2.5.6, 32.2.5.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence', @level2type=N'COLUMN',@level2name=N'ConsequenceValueCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the consequence (pharmacokinetic effect or medical problem) of an InteractionIssue (<risk><consequenceObservation>). Based on Section 32.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 248' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionConsequence'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying an interaction issue (C54708).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue', @level2type=N'COLUMN',@level2name=N'InteractionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247, Para 32.2.3.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue', @level2type=N'COLUMN',@level2name=N'InteractionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a drug interaction issue within a specific section (<subjectOf><issue>). Based on Section 32.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 247' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'InteractionIssue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional signing statement provided in <noteText>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'NoteText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.2), Page 268 (Para 36.1.7.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'NoteText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Timestamp of the signature (<time value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'TimeValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.9), Page 269 (Para 36.1.7.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'TimeValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The electronic signature text (<signatureText>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignatureText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.3), Page 269 (Para 36.1.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignatureText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the person signing (<assignedPerson><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'AssignedPersonName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 256 (Para 35.1.3.6), Page 269 (Para 36.1.7.6)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'AssignedPersonName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the signing Organization, used for FDA signers in Labeler Code Inactivation (Sec 5.1.6).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignerOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 122' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator', @level2type=N'COLUMN',@level2name=N'SignerOrganizationID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores legal authenticator (signature) information for a document (<legalAuthenticator>). Based on Section 5.1.6, 35.1.3, 36.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 122, 256, 268' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LegalAuthenticator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The license number string.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID identifying the issuing authority and context.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 182 (Para 18.1.5.8), Page 183 (Para 18.1.5.16-22, 18.1.5.27)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of approval/license (e.g., C118777 licensing).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'LicenseTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Status of the license: active, suspended, aborted (revoked), completed (expired).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'StatusCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 182, Para 18.1.5.9' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'StatusCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Expiration date of the license.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180, Para 18.1.5.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores license information for WDD/3PL facilities (<subjectOf><approval>). Based on Section 18.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 180' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'License'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the relationship between Fill/Package Lots and Label Lots (<productInstance><member><memberProductInstance>). Based on Section 16.2.7, 16.2.11.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 171, 175' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The lot number string.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168 (Para 16.2.5.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The computed globally unique root OID for the lot number.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 169 (Para 16.2.5.5), Page 170 (Para 16.2.6.3), Page 171 (Para 16.2.7.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier', @level2type=N'COLUMN',@level2name=N'LotRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores Lot Number and its associated globally unique root OID. Based on Section 16.2.5, 16.2.6, 16.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, 170, 171' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LotIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the marketing category (e.g., NDA, ANDA, OTC Monograph Drug).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'CategoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54, Para 3.1.7.2, 3.1.7.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'CategoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Application number, monograph ID, or citation (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Root OID for the application number or monograph ID system (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 56, Para 3.1.7.7, 3.1.7.28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApplicationOrMonographIDOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date of application approval, if applicable (<effectiveTime><low value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApprovalDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 58, Para 3.1.7.33' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ApprovalDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Territory code, typically USA (<territory><code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'TerritoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 55, Para 3.1.7.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'TerritoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductConcept, used when the marketing category applies to an Application Product Concept instead of a concrete Product.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ProductConceptID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 162' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory', @level2type=N'COLUMN',@level2name=N'ProductConceptID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores marketing category and application/monograph information for a product, part, or application product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.7, 15.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 54, 162' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingCategory'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the marketing activity (e.g., C53292 Marketing, C96974 Drug Sample).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'MarketingActCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 61, Para 3.1.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'MarketingActCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Status code: active, completed, new, cancelled.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'StatusCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 61, Para 3.1.8.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'StatusCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Marketing start date (<effectiveTime><low value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveStartDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveStartDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Marketing end date (<effectiveTime><high value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveEndDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus', @level2type=N'COLUMN',@level2name=N'EffectiveEndDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores marketing status information for a product or package (<subjectOf><marketingAct>). Based on Section 3.1.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 59' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MarketingStatus'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the Moiety table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'MoietyID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to IdentifiedSubstance (The substance this moiety helps define). Links this molecular component to its parent substance record. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'IdentifiedSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the type or role of this moiety within the substance definition. Typically indicates whether this is a mixture component or other structural element. Example: "C103243" for "mixture component".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'MoietyCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system OID for the moiety code, typically NCI Thesaurus. Standard value: "2.16.840.1.113883.3.26.1.1".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'MoietyCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Human-readable name for the moiety code. Example: "mixture component".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'MoietyDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Lower bound value for the quantity numerator in mixture ratios. Used to specify ranges or minimum quantities for this moiety component.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'QuantityNumeratorLowValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unit of measure for the quantity numerator. Typically "1" for dimensionless ratios in mixture specifications.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'QuantityNumeratorUnit'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates whether the numerator low value boundary is inclusive in range specifications. False typically indicates "greater than" rather than "greater than or equal to".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'QuantityNumeratorInclusive'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Denominator value for quantity ratios in mixture specifications. Provides the base for calculating relative proportions of mixture components.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'QuantityDenominatorValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unit of measure for the quantity denominator. Typically "1" for dimensionless ratios in mixture specifications.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'QuantityDenominatorUnit'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sequence number to distinguish between multiple moieties with the same type code within a single substance. Critical for substances with multiple chemical components of the same type but different molecular structures (e.g., peptides with multiple mixture components).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a chemical moiety within an identified substance, containing molecular structure and quantity information that defines part of a substance''s identity. Based on FDA Substance Registration System standards and ISO/FDIS 11238.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Moiety'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the type of named entity, e.g., C117113 for "doing business as".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20 (Para 2.1.9.1), Page 179 (Para 18.1.3.10)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The name of the entity, e.g., the DBA name.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20 (Para 2.1.9.2), Page 179 (Para 18.1.3.11)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntityName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional suffix used with DBA names in WDD/3PL reports to indicate business type ([WDD] or [3PL]).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntitySuffix'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 179, Para 18.1.3.12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity', @level2type=N'COLUMN',@level2name=N'EntitySuffix'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores "Doing Business As" (DBA) names or other named entity types associated with an Organization (<asNamedEntity>). Based on Section 2.1.9, 18.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 19, 178' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NamedEntity'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The National Clinical Trials number (id extension).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 251, Para 33.2.2.1, 33.2.2.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The root OID for NCT numbers (id root).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 252, Para 33.2.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink', @level2type=N'COLUMN',@level2name=N'NCTRootOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the link between an indexing section and a National Clinical Trials number (<protocol><id>). Based on Section 33.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 251' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'NCTLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The upper limit of the tolerance range in ppm.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ToleranceHighValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.4, 19.2.4.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ToleranceHighValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional link to the specific commodity the tolerance applies to.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'CommodityID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191, Para 19.2.4.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'CommodityID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the type of application associated with this tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ApplicationTypeID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ApplicationTypeID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional expiration or revocation date for the tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.18' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional text annotation about the tolerance.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'TextNote'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 192, Para 19.2.4.22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion', @level2type=N'COLUMN',@level2name=N'TextNote'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the tolerance range and related details (<referenceRange><observationCriterion>). Based on Section 19.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 191' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationCriterion'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifier for the media object (<observationMedia ID=>), referenced by <renderMultimedia>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the image (<text> child of observationMedia), used by screen readers.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'DescriptionText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30-31' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'DescriptionText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Media type of the file (<value mediaType=>), e.g., image/jpeg.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 31, Para 2.2.3.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'MediaType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'File name of the image (<reference value=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'FileName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30, Para 2.2.3.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'FileName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Xsi type of the file ([value xsi:type=]), e.g., "ED".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'XsiType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to Document. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia', @level2type=N'COLUMN',@level2name=N'DocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores metadata for images ([observationMedia]). Based on Section 2.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ObservationMedia'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the organization (<name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'OrganizationName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 16, Para 2.1.5.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'OrganizationName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Flag indicating if the organization information is confidential (<confidentialityCode code="B">).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'IsConfidential'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 11, Para 2.1.1.10, 2.1.1.11; Page 77 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization', @level2type=N'COLUMN',@level2name=N'IsConfidential'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores information about organizations (e.g., labelers, registrants, establishments). Identifiers (DUNS, FEI, Labeler Code etc) stored in OrganizationIdentifier table. Based on Section 2.1.4, 2.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 14, 15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Organization'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The identifier value (<id extension>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15, 106, 118, 126, 164, 180' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the identifier system (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15 (1.3.6.1.4.1.519.1, 2.16.840.1.113883.4.82), Page 118 (2.16.840.1.113883.6.69), Page 164 (1.3.6.1.4.1.32366.1.3.1.2), Page 180 (1.3.6.1.4.1.32366.4.840.x)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type classification of the identifier based on the OID and context.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores various identifiers associated with an Organization (DUNS, FEI, Labeler Code, License Number, etc.).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 15 (DUNS, FEI), Page 106 (Labeler DUNS), Page 118 (Labeler DUNS & Labeler Code), Page 126 (Est FEI), Page 164 (Manuf License), Page 180 (State License)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Junction table to link Organizations directly with Telecom entries (e.g., for US Agents or facility phones without a full ContactParty).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationTelecom'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'OrganizationTelecom'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The package item code value (<containerPackagedProduct><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.12' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the package identifier system (<containerPackagedProduct><code> codeSystem).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 48, Para 3.1.5.27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores identifiers (NDC Package Code, etc.) for a specific packaging level. Based on Section 3.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, 48' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackageIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the nested structure of packaging levels. Links an outer package to the inner package(s) it contains.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46 (implied by nested <asContent>)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to Product table if this packaging directly contains the base manufactured product.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to Product table (representing a part) if this packaging contains a part of a kit.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PartProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46, 49' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PartProductID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit of the item contained within this package level (<quantity>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'QuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.3, 3.1.5.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'QuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Package type code, system, and display name (<containerPackagedProduct><formCode>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageFormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 47, Para 3.1.5.9, 3.1.5.10, 3.1.5.11' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageFormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'FK to ProductInstance, used when the packaging details describe a container linked to a specific Label Lot instance (Lot Distribution).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductInstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 172' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'ProductInstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Corresponds to <quantity><denominator value> for packaging units.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'QuantityDenominator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The package item code value (<containerPackagedProduct><code code=.../>). For example, NDC package code or other item code for the package.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The code system OID for the package item code (<containerPackagedProduct><code codeSystem=.../>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'PackageCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The translation code for the numerator quantity (<quantity><numerator><translation code=.../>). Provides an alternative coded representation of the packaging form or unit type (e.g., C43168 for BLISTER PACK).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'NumeratorTranslationCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The code system OID for the numerator translation code (<quantity><numerator><translation codeSystem=.../>). Typically 2.16.840.1.113883.3.26.1.1 for FDA form codes used in SPL packaging specifications.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'NumeratorTranslationCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The human-readable display name for the numerator translation code (<quantity><numerator><translation displayName=.../>). Provides the descriptive text for the packaging form or unit type (e.g., "BLISTER PACK", "PACKAGE", "TABLET").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel', @level2type=N'COLUMN',@level2name=N'NumeratorTranslationDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a level of packaging (asContent/containerPackagedProduct). Links to ProductID/PartProductID for definitions OR ProductInstanceID for lot distribution container data.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 46' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PackagingLevel'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links products sold separately but intended for use together (<asPartOfAssembly>). Based on Section 3.1.6, 3.3.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PartOfAssembly'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 53, 92' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PartOfAssembly'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The MED-RT or MeSH code for the pharmacologic class.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The display name for the class code, including the type suffix like [EPC] or [CS].' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.2.15, 8.2.2.16' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass', @level2type=N'COLUMN',@level2name=N'ClassDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the definition of a pharmacologic class concept, identified by its code. Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClass'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the hierarchy between Pharmacologic Classes (<asSpecializedKind> under class definition). Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 137, Para 8.2.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an active moiety (IdentifiedSubstance) to its associated Pharmacologic Class (<asSpecializedKind> under moiety). Based on Section 8.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 135' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The text of the preferred or alternate name.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the name is preferred (L) or alternate (A).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameUse'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136, Para 8.2.3.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName', @level2type=N'COLUMN',@level2name=N'NameUse'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores preferred (L) and alternate (A) names for a Pharmacologic Class (<identifiedSubstance><name use=>). Based on Section 8.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 136' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PharmacologicClassName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Class code for the policy, e.g., DEADrugSchedule.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyClassCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code representing the specific policy value (e.g., DEA Schedule C-II).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the policy code (e.g., 2.16.840.1.113883.3.26.1.1 for DEA schedule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the policy code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83, Para 3.2.11.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy', @level2type=N'COLUMN',@level2name=N'PolicyDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores policy information related to a product, like DEA Schedule (<subjectOf><policy>). Based on Section 3.2.11.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 83' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Policy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Proprietary name or product name (<name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, Para 3.1.1.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Suffix to the proprietary name (<suffix>), e.g., "XR".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductSuffix'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'ProductSuffix'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage form code, system, and display name (<formCode>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 39, 70 (3.2.1.20)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Brief description of the product (<desc>), mainly used for devices.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'DescriptionText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product', @level2type=N'COLUMN',@level2name=N'DescriptionText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores core product information (<manufacturedProduct>). Based on Section 3.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Product'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The computed MD5 hash code identifying the product concept (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 155 (Para 15.2.2.1), Page 157 (15.2.4), Page 161 (Para 15.2.6.1), Page 162 (15.2.8)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Distinguishes Abstract Product/Kit concepts from Application-specific Product/Kit concepts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'ConceptType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage Form details, applicable only for Abstract Product concepts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 156, Para 15.2.2.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the definition of an abstract or application-specific product/kit concept. Based on Section 15.2.2, 15.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 155, 161' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConcept'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the relationship type between Application and Abstract concepts (A, B, OTC, N).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence', @level2type=N'COLUMN',@level2name=N'EquivalenceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 161, Para 15.2.6.6, 15.2.6.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence', @level2type=N'COLUMN',@level2name=N'EquivalenceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links an Application Product Concept to its corresponding Abstract Product Concept (<asEquivalentEntity>). Based on Section 15.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 161' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductConceptEquivalence'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the type of event (e.g., C106325 Distributed, C106328 Returned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EventCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173 (Para 16.2.9.5), Page 174 (Para 16.2.10.1)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EventCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Integer quantity associated with the event (e.g., number of containers distributed/returned).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'QuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173 (Para 16.2.9.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'QuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Effective date (low value), used for Initial Distribution Date.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 174 (Para 16.2.9.9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores product events like distribution or return quantities (<subjectOf><productEvent>). Based on Section 16.2.9, 16.2.10.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 173, 174' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductEvent'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The item code value (<code code=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 39, Para 3.1.1.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'OID for the identifier system (<code codeSystem=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, Para 3.1.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierSystemOID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type classification of the identifier based on the OID.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 40, Para 3.1.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier', @level2type=N'COLUMN',@level2name=N'IdentifierType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores various types of identifiers associated with a product (Item Codes like NDC, GTIN, etc.). Based on Section 3.1.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 39' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductIdentifier'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of lot instance: FillLot, LabelLot, or PackageLot (for kits).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'InstanceType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Expiration date, typically for Label Lots.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 171 (Para 16.2.7.5)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance', @level2type=N'COLUMN',@level2name=N'ExpirationDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents an instance of a product (Fill Lot, Label Lot, Package Lot, Salvaged Lot) in Lot Distribution or Salvage Reports. Based on Section 16.2.5, 16.2.7, 16.2.11, 29.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 168, 171, 175' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductInstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Quantity and unit of this part contained within the parent kit product (<part><quantity>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart', @level2type=N'COLUMN',@level2name=N'PartQuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 49, 54 (Para 3.1.6.2, 3.1.6.3, 3.1.6.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart', @level2type=N'COLUMN',@level2name=N'PartQuantityNumerator'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines the parts comprising a kit product. Links a Kit Product to its constituent Part Products. Based on Section 3.1.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 49' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductPart'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the route of administration.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the route code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteNullFlavor'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88, Para 3.2.20.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration', @level2type=N'COLUMN',@level2name=N'RouteNullFlavor'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a product (or part) to its route(s) of administration (<consumedIn><substanceAdministration>). Based on Section 3.2.20.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 88' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductRouteOfAdministration'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Absolute URL for the product web page, starting with http:// or https://.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink', @level2type=N'COLUMN',@level2name=N'WebURL'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 103, Para 3.4.7.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink', @level2type=N'COLUMN',@level2name=N'WebURL'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the web page link for a cosmetic product (<subjectOf><document><text><reference value=>). Based on Section 3.4.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 103' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ProductWebLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the REMS protocol type.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol', @level2type=N'COLUMN',@level2name=N'ProtocolCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 211, Para 23.2.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol', @level2type=N'COLUMN',@level2name=N'ProtocolCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a REMS protocol defined within a section (<protocol> element). Based on Section 23.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 211' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Protocol'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UNII code of the reference substance (<definingSubstance><code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceUNII'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceUNII'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the reference substance (<definingSubstance><name>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 77, Para 3.2.5.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance', @level2type=N'COLUMN',@level2name=N'RefSubstanceName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores reference substance details linked to an IngredientSubstance (used when BasisOfStrength=''ReferenceIngredient''). Based on Section 3.1.4, 3.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 44, 77' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ReferenceSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code indicating the type of relationship (e.g., APND for core doc, RPLC for predecessor, DRIV for reference labeling, SUBJ for subject, XCRPT for excerpt).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'RelationshipTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21, Page 154, Page 193, Page 199, Page 205, Page 236, Page 242, Page 245, Page 250 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'RelationshipTypeCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Set GUID of the related/referenced document (<setId root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedSetGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21, Page 154, Page 193, Page 199, Page 205, Page 236, Page 242, Page 245, Page 250 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedSetGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document GUID of the related/referenced document (<id root>), used for RPLC relationship.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 21, Para 2.1.11.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Version number of the related/referenced document (<versionNumber value>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedVersionNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, Page 21 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedVersionNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Document type code, system, and display name of the related/referenced document (<code>), used for RPLC relationship.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 21 ' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument', @level2type=N'COLUMN',@level2name=N'ReferencedDocumentCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to other documents (e.g., Core Document, Predecessor, Reference Labeling[cite: 1031, 1298, 1332, 1608, 1644], Subject [cite: 1363, 1556, 1599]). Based on Sections 2.1.10, 2.1.11, 15.1.3, 20.1.3, 21.1.3, 23.1.3, 30.1.3, 31.1.5, 32.1.3, 33.1.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 20, 21, 154, 193, 199, 205, 235, 242, 245, 250' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RelatedDocument'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for REMS Approval (C128899).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217, Para 23.2.8.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date of the initial REMS program approval.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217, Para 23.2.8.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval', @level2type=N'COLUMN',@level2name=N'ApprovalDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the REMS approval details associated with the first protocol mention (<subjectOf><approval>). Based on Section 23.2.8.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSApproval'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for this specific electronic resource reference.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219, Para 23.2.10.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The URI (URL or URN) of the electronic resource.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceReferenceValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219, Para 23.2.10.4, 23.2.10.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource', @level2type=N'COLUMN',@level2name=N'ResourceReferenceValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to REMS electronic resources (URLs or URNs) (<subjectOf><document>). Based on Section 23.2.10.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 219' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSElectronicResource'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for this specific material document reference.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'MaterialDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'MaterialDocumentGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Internal link ID (#...) embedded within the title, potentially linking to descriptive text.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'TitleReference'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'TitleReference'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the AttachedDocument table if the material is provided as an attachment (e.g., PDF).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'AttachedDocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 218, Para 23.2.9.4' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial', @level2type=N'COLUMN',@level2name=N'AttachedDocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores references to REMS materials, linking to attached documents if applicable (<subjectOf><document>). Based on Section 23.2.9.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 217' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'REMSMaterial'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the ObservationMedia containing the image details, via the referencedObject attribute.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'ObservationMediaID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'ObservationMediaID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the image is inline (within a paragraph) or block level (direct child of <text>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'IsInline'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 31' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'IsInline'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to Document. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia', @level2type=N'COLUMN',@level2name=N'DocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the <renderMultimedia> tag, linking text content to an ObservationMedia entry. Based on Section 2.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 30' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'RenderedMedia'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sequence number relative to the substance administration step (fixed at 2). 1=Before, 2=During/Concurrent, 3=After.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementSequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 213, Para 23.2.7.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementSequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional delay (pause) relative to the start/end of the previous step.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PauseQuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 213, Para 23.2.7.3, 23.2.7.4, 23.2.7.5, 23.2.7.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PauseQuantityValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the specific requirement or monitoring observation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 215, Para 23.2.7.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'RequirementCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link ID (#...) pointing to the corresponding text description in the REMS Summary or REMS Participant Requirements section.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'OriginalTextReference'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212, Page 215 (Para 23.2.7.12)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'OriginalTextReference'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional repetition period for the requirement/observation.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PeriodValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 215, Para 23.2.7.15' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'PeriodValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Link to the stakeholder responsible for fulfilling the requirement.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'StakeholderID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.17' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'StakeholderID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional link to a REMS Material document referenced by the requirement.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'REMSMaterialID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement', @level2type=N'COLUMN',@level2name=N'REMSMaterialID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents a REMS requirement or monitoring observation within a protocol (<component><requirement> or <monitoringObservation>). Based on Section 23.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Requirement'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Links a Cosmetic Product (in Facility Reg doc) to its Responsible Person organization (<manufacturerOrganization>). Based on Section 35.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ResponsiblePersonLink'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 261' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ResponsiblePersonLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identifier for the section (<id root>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LOINC code for the section type (<code> code).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22, Para 2.2.1.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the section code (<code> codeSystem), typically 2.16.840.1.113883.6.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.7' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the section code (<code> displayName).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24, Para 2.2.1.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Title of the section (<title>), may include numbering.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'Title'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'Title'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Effective time for the section (<effectiveTime value>). For Compounded Drug Labels (Sec 4.2.2), low/high represent the reporting period.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 24 (Para 2.2.1.9), Page 114 (Para 4.2.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTime'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Low boundary of the effective time period for the section ([effectiveTime][low value]). Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTimeLow'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'High boundary of the effective time period for the section ([effectiveTime][high value]). Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'EffectiveTimeHigh'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Attribute identifying the section link ([section][ID]), used for cross-references within the document e.g. [section ID="ID_1dc7080f-1d52-4bf7-b353-3c13ec291810"].' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionLinkGUID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'LOINC code name for the section type ([code] codeSystemName).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'SectionCodeSystemName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to Document. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section', @level2type=N'COLUMN',@level2name=N'DocumentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Sections of the document that group related text and metadata such as titles and codes.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Section'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content from <excerpt><highlight><text>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight', @level2type=N'COLUMN',@level2name=N'HighlightText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 32' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight', @level2type=N'COLUMN',@level2name=N'HighlightText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the highlight text within an excerpt for specific sections (e.g., Boxed Warning, Indications). Based on Section 2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 32' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionExcerptHighlight'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Manages the nested structure (parent-child relationships) of sections using <component><section>. Based on Section 2.2.1.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 23' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionHierarchy'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of content block: Paragraph, List, Table, BlockImage (for <renderMultimedia> as direct child of <text>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Order of this content block within the parent section''s <text> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22, Para referencing order' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Actual text for Paragraphs. For List/Table types, details are in related tables. Inline markup (bold, italic, links etc) handled separately.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ContentText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The values for [styleCode] indicate font effects such as bold, italics, underline, or emphasis to aid accessibility for visually impaired users.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent SectionTextContent for hierarchy (e.g., a paragraph inside a highlight inside an excerpt)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent', @level2type=N'COLUMN',@level2name=N'ParentSectionTextContentID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Textual content of sections within a structured document. Supports style codes for font formatting.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 25' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SectionTextContent'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the specialized kind (e.g., device product classification, cosmetic category).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.9), Page 97 (Para 3.4.3.3)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the specialized kind code (typically 2.16.840.1.113883.6.303).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.8), Page 97 (Para 3.4.3.2)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display name matching the specialized kind code.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 89 (Para 3.3.1.10), Page 97 (Para 3.4.3.4)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind', @level2type=N'COLUMN',@level2name=N'KindDisplayName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores specialized kind information, like device product classification or cosmetic category. Based on Section 3.1.1, 3.3.1, 3.4.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 37, 89, 97' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecializedKind'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the SpecifiedSubstance table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SpecifiedSubstanceID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The code assigned to the specified substance.(Atribute code="70097M6I30")' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system for the specified substance code (Atribute codeSystem="2.16.840.1.113883.4.9").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code name for the specified substance code (Atribute codeSystemName="FDA SRS").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystemName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 198, Para 20.2.6.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance', @level2type=N'COLUMN',@level2name=N'SubstanceCodeSystemName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the specified substance code and name linked to an ingredient in Biologic/Drug Substance Indexing documents. Based on Section 20.2.6.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 197' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SpecifiedSubstance'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Hexadecimal hash value (64 characters) representing the cryptographic hash of the SplXML content. Used for data integrity verification, duplicate detection, and caching optimization. Typically contains SHA-256 hash in hexadecimal format. NULL values indicate hash has not been computed.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SplData', @level2type=N'COLUMN',@level2name=N'SplXMLHash'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores SPL (Structured Product Labeling) data records with XML content and metadata. Enhanced with content hash support for data integrity verification and caching optimization.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SplData'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code identifying the stakeholder role (e.g., prescriber, patient).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder', @level2type=N'COLUMN',@level2name=N'StakeholderCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 216, Para 23.2.7.19, 23.2.7.20' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder', @level2type=N'COLUMN',@level2name=N'StakeholderCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Lookup table for REMS stakeholder types (<stakeholder>). Based on Section 23.2.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 212, 216' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Stakeholder'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the main <structuredBody> container within a Document. Based on Section 2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructuredBody'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 22' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructuredBody'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Specification code, format 40-CFR-...' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'SpecCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 190, Para 19.2.3.8' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'SpecCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code for the Enforcement Analytical Method used (<observation><code>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'EnforcementMethodCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 190, Para 19.2.3.10' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification', @level2type=N'COLUMN',@level2name=N'EnforcementMethodCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores substance specification details for tolerance documents (<subjectOf><substanceSpecification>). Based on Section 19.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 189' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SubstanceSpecification'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Type of telecommunication: "tel", "mailto", or "fax".' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, 18, 19, Para 2.1.7.2, 2.1.7.11, 2.1.7.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The telecommunication value, prefixed with type (e.g., "tel:+1-...", "mailto:...", "fax:+1-...").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomValue'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17, 18, 19, Para 2.1.7.2, 2.1.7.11, 2.1.7.13' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom', @level2type=N'COLUMN',@level2name=N'TelecomValue'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores telecommunication details (phone, email, fax) for organizations or contact parties. Based on Section 2.1.7.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 17' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Telecom'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA). Used to identify the territorial scope of the licensing authority.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'TerritoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 181, Para 18.1.5.5, 18.1.5.23' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'TerritoryCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code system OID for the territory code (e.g., ''1.0.3166.2'' for state, ''1.0.3166.1.2.3'' for country).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'TerritoryCodeSystem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'DUNS number of the federal governing agency (e.g., "004234790" for DEA). Required when territory code is "USA", prohibited otherwise.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'GoverningAgencyIdExtension'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Root OID for governing agency identification ("1.3.6.1.4.1.519.1").' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'GoverningAgencyIdRoot'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of the federal governing agency (e.g., "DEA"). Required when territory code is "USA", prohibited otherwise.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority', @level2type=N'COLUMN',@level2name=N'GoverningAgencyName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Represents the issuing authority (State or Federal Agency like DEA) for licenses ([author][territorialAuthority]). Based on Section 18.1.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 181' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TerritorialAuthority'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Attribute identifying the list as ordered or unordered (<list listType=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'ListType'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'ListType'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional style code for numbering/bullet style (<list styleCode=>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details specific to <list> elements. Based on Section 2.2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextList'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional custom marker specified using <caption> within <item>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemCaption'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27-28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemCaption'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content of the list item <item>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem', @level2type=N'COLUMN',@level2name=N'ItemText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <item> elements within a <list>. Based on Section 2.2.2.4.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 27' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextListItem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional width attribute specified on the <table> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'Width'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'Width'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the table included a <thead> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasHeader'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasHeader'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Indicates if the table included a <tfoot> element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasFooter'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'HasFooter'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional caption text for the table, may contain formatted content.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'Caption'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional ID attribute on the [table] element for cross-referencing.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable', @level2type=N'COLUMN',@level2name=N'SectionTableLink'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores details specific to <table> elements. Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTable'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text content of the table cell (<td> or <th>).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'CellText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'CellText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional rowspan attribute on <td> or <th>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'RowSpan'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'RowSpan'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional colspan attribute on <td> or <th>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'ColSpan'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'ColSpan'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute for cell rules (Lrule, Rrule, Toprule, Botrule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional align attribute for horizontal alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'Align'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'Align'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional valign attribute for vertical alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'VAlign'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 29' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell', @level2type=N'COLUMN',@level2name=N'VAlign'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <td> or <th> elements within a <tr>. Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableCell'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary key for the TextTableColumn table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'TextTableColumnID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Foreign key to TextTable. No database constraint - managed by ApplicationDbContext.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'TextTableID'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Order of the column within the table.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'SequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional width attribute on [col] element.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'Width'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional align attribute on [col] for horizontal alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'Align'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional valign attribute on [col] for vertical alignment.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'VAlign'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute on [col] for formatting rules.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Identifies which colgroup this column belongs to (if any). Null indicates a standalone [col] element not within a [colgroup]. Multiple columns with the same ColGroupSequenceNumber belong to the same [colgroup].' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'ColGroupSequenceNumber'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no styleCode. Individual [col] styleCode attributes take precedence over colgroup-level styles.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'ColGroupStyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional align attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no align. Individual [col] align attributes take precedence. Valid values: left, center, right, justify, char.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'ColGroupAlign'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional valign attribute from the parent [colgroup] element. Null if column is not within a [colgroup] or if [colgroup] has no valign. Individual [col] valign attributes take precedence.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn', @level2type=N'COLUMN',@level2name=N'ColGroupVAlign'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual [col] elements within a [table]. Based on Section 2.2.2.5. Column definitions specify default formatting and alignment for table columns. Supports both standalone [col] elements and [col] elements nested within [colgroup].' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableColumn'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional styleCode attribute on <tr> (e.g., Botrule).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow', @level2type=N'COLUMN',@level2name=N'StyleCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores individual <tr> elements within a <table> (header, body, or footer). Based on Section 2.2.2.5.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 28' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TextTableRow'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the warning letter alert was issued.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'AlertIssueDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202, Para 21.2.3.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'AlertIssueDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the issue described in the warning letter was resolved, if applicable.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'ResolutionDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202, Para 21.2.3.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate', @level2type=N'COLUMN',@level2name=N'ResolutionDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores the issue date and optional resolution date for a warning letter alert. Based on Section 21.2.3.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 202' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterDate'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Proprietary name of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.1' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ProductName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Generic name of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'GenericName'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.2' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'GenericName'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Dosage form code of the product referenced in the warning letter.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.3' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'FormCode'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the ingredient strength(s).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'StrengthText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.5' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'StrengthText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Text description of the product item code(s) (e.g., NDC).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ItemCodesText'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201, Para 21.2.2.6' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo', @level2type=N'COLUMN',@level2name=N'ItemCodesText'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Stores key product identification details referenced in a Warning Letter Alert Indexing document. Based on Section 21.2.2.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo'
GO
EXEC sys.sp_addextendedproperty @name=N'SPL_Reference', @value=N'Page 201' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'WarningLetterProductInfo'
GO
ALTER DATABASE [MedRecProDB] SET  READ_WRITE 
GO
