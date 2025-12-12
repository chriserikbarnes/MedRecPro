using System.Text.Json.Serialization;

namespace MedRecPro.Static.Models
{
    // ============================================================
    // CONFIGURATION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Site-wide configuration settings loaded from config.json.
    /// </summary>
    /// <remarks>
    /// Contains branding, API URLs, and contact information used throughout the application.
    /// </remarks>
    /// <seealso cref="ContentService"/>
    /**************************************************************/
    /// <summary>
    /// Site-wide configuration settings loaded from config.json.
    /// </summary>
    /// <remarks>
    /// Contains branding, API URLs, authentication URLs, and contact information 
    /// used throughout the application. Includes both production and development
    /// URLs to support environment-aware configuration.
    /// </remarks>
    /// <seealso cref="ContentService"/>
    public class SiteConfig
    {
        /**************************************************************/
        /// <summary>
        /// The display name of the site (e.g., "MedRecPro").
        /// </summary>
        [JsonPropertyName("siteName")]
        public string? SiteName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Short tagline describing the application.
        /// </summary>
        [JsonPropertyName("tagline")]
        public string? Tagline { get; set; }

        /**************************************************************/
        /// <summary>
        /// Base URL for the production API endpoints.
        /// </summary>
        [JsonPropertyName("apiUrl")]
        public string? ApiUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Base URL for the development API endpoints.
        /// </summary>
        /// <remarks>
        /// Used when the site is accessed from localhost.
        /// Format: http://localhost:5093/api
        /// </remarks>
        [JsonPropertyName("apiUrlDev")]
        public string? ApiUrlDev { get; set; }

        /**************************************************************/
        /// <summary>
        /// Production OAuth login endpoint URL.
        /// </summary>
        /// <remarks>
        /// Format: https://medrecpro.com/api/auth/login/
        /// Append the provider name (e.g., /Google, /Microsoft) when constructing links.
        /// </remarks>
        [JsonPropertyName("authUrl")]
        public string? AuthUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Development OAuth login endpoint URL.
        /// </summary>
        /// <remarks>
        /// Used when the site is accessed from localhost.
        /// This ensures authentication cookies are set for the localhost
        /// domain, allowing them to be sent with subsequent API calls.
        /// Format: http://localhost:5093/api/auth/login
        /// </remarks>
        [JsonPropertyName("authUrlDev")]
        public string? AuthUrlDev { get; set; }

        /**************************************************************/
        /// <summary>
        /// Production post-authentication redirect URL.
        /// </summary>
        /// <remarks>
        /// Where to redirect the user after successful OAuth authentication
        /// in the production environment.
        /// Format: https://www.medrecpro.com/Home/Chat
        /// </remarks>
        [JsonPropertyName("returnUrl")]
        public string? ReturnUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Development post-authentication redirect URL.
        /// </summary>
        /// <remarks>
        /// Where to redirect the user after successful OAuth authentication
        /// in the local development environment. This ensures the user lands
        /// on the local static site where the auth cookie (set for localhost)
        /// will be sent with subsequent API requests.
        /// Format: http://localhost:5001/Home/Chat
        /// </remarks>
        [JsonPropertyName("returnUrlDev")]
        public string? ReturnUrlDev { get; set; }

        /**************************************************************/
        /// <summary>
        /// URL to the production Swagger/OpenAPI documentation.
        /// </summary>
        [JsonPropertyName("swaggerUrl")]
        public string? SwaggerUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// URL to the development Swagger/OpenAPI documentation.
        /// </summary>
        [JsonPropertyName("swaggerUrlDev")]
        public string? SwaggerUrlDev { get; set; }

        /**************************************************************/
        /// <summary>
        /// Contact email address for support inquiries.
        /// </summary>
        [JsonPropertyName("contactEmail")]
        public string? ContactEmail { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the content was last updated.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Version for the app
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Container for all page content loaded from pages.json.
    /// </summary>
    /// <remarks>
    /// This class serves as the root deserialization target for the pages.json file,
    /// containing content for the home, terms, and privacy pages.
    /// </remarks>
    /// <seealso cref="PageContent"/>
    /// <seealso cref="TermsPageContent"/>
    /// <seealso cref="PrivacyPageContent"/>
    /// <seealso cref="ContentService"/>
    public class PagesData
    {
        /**************************************************************/
        /// <summary>
        /// Content for the home/landing page.
        /// </summary>
        [JsonPropertyName("home")]
        public PageContent Home { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Content for the Terms of Service page.
        /// </summary>
        /// <remarks>
        /// Uses the expanded TermsPageContent model with support for
        /// summary table, subsections, and contact information.
        /// </remarks>
        /// <seealso cref="TermsPageContent"/>
        [JsonPropertyName("terms")]
        public TermsPageContent Terms { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Content for the Privacy Policy page.
        /// </summary>
        /// <remarks>
        /// Uses the expanded PrivacyPageContent model with support for
        /// introduction, subsections, and subprocessors list.
        /// </remarks>
        /// <seealso cref="PrivacyPageContent"/>
        [JsonPropertyName("privacy")]
        public PrivacyPageContent Privacy { get; set; } = new();
    }

    // ============================================================
    // ADD: New classes for Terms of Service support
    // Add these after the existing legal classes (around line 920)
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Summary item for Terms of Service quick reference table.
    /// </summary>
    /// <remarks>
    /// Provides a brief overview of each section for user convenience.
    /// This summary is non-binding and for reference only.
    /// </remarks>
    /// <seealso cref="LegalSummary"/>
    public class LegalSummaryItem
    {
        /**************************************************************/
        /// <summary>
        /// Section identifier (e.g., "A. Definitions").
        /// </summary>
        [JsonPropertyName("section")]
        public string? Section { get; set; }

        /**************************************************************/
        /// <summary>
        /// Brief description of what the section covers.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Summary section for legal documents providing a quick reference.
    /// </summary>
    /// <remarks>
    /// Contains a non-binding overview of document sections.
    /// Commonly used in Terms of Service to help users navigate the document.
    /// </remarks>
    /// <seealso cref="LegalSummaryItem"/>
    public class LegalSummary
    {
        /**************************************************************/
        /// <summary>
        /// Descriptive text explaining the summary's purpose.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Collection of summary items, one per section.
        /// </summary>
        /// <seealso cref="LegalSummaryItem"/>
        [JsonPropertyName("items")]
        public List<LegalSummaryItem>? Items { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Extended page content model for Terms of Service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This model supports the expanded Terms of Service schema including:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Effective date and last updated date</description></item>
    ///   <item><description>Introduction section</description></item>
    ///   <item><description>Summary table for quick reference</description></item>
    ///   <item><description>Main sections with nested subsections and items</description></item>
    ///   <item><description>Contact information</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="LegalSection"/>
    /// <seealso cref="LegalIntroduction"/>
    /// <seealso cref="LegalSummary"/>
    public class TermsPageContent
    {
        /**************************************************************/
        /// <summary>
        /// Page title (e.g., "MedRecPro Terms of Service").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the Terms become effective.
        /// </summary>
        [JsonPropertyName("effectiveDate")]
        public string? EffectiveDate { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the Terms were last updated.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Introduction section displayed before main content.
        /// </summary>
        /// <seealso cref="LegalIntroduction"/>
        [JsonPropertyName("introduction")]
        public LegalIntroduction? Introduction { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional summary table for quick reference.
        /// </summary>
        /// <remarks>
        /// The summary provides a non-binding overview of document sections
        /// to help users navigate the Terms of Service.
        /// </remarks>
        /// <seealso cref="LegalSummary"/>
        [JsonPropertyName("summary")]
        public LegalSummary? Summary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Collection of legal sections comprising the main content.
        /// </summary>
        /// <seealso cref="LegalSection"/>
        [JsonPropertyName("sections")]
        public List<LegalSection>? Sections { get; set; }
    }

    // ============================================================
    // PAGE CONTENT MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Unified page content model supporting both marketing and legal pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This model supports multiple page types:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>Home Page</term>
    ///     <description>Uses Hero, Stats, Features, HowItWorks, UseCases, Api, and Cta sections</description>
    ///   </item>
    ///   <item>
    ///     <term>Legal Pages (Terms, Privacy)</term>
    ///     <description>Uses Title, LastUpdated, and Sections properties</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <seealso cref="HeroContent"/>
    /// <seealso cref="StatItem"/>
    /// <seealso cref="FeatureItem"/>
    /// <seealso cref="HowItWorksContent"/>
    /// <seealso cref="UseCasesContent"/>
    /// <seealso cref="ApiContent"/>
    /// <seealso cref="CtaContent"/>
    /// <seealso cref="LegalSection"/>
    public class PageContent
    {
        #region common properties

        /**************************************************************/
        /// <summary>
        /// Page title used in the browser tab and page header.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the page content was last updated (primarily for legal pages).
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        #endregion

        #region home page properties

        /**************************************************************/
        /// <summary>
        /// Hero section content with headline, subheadline, and CTAs.
        /// </summary>
        [JsonPropertyName("hero")]
        public HeroContent? Hero { get; set; }

        /**************************************************************/
        /// <summary>
        /// Statistics displayed below the hero section.
        /// </summary>
        [JsonPropertyName("stats")]
        public List<StatItem>? Stats { get; set; }

        /**************************************************************/
        /// <summary>
        /// Feature cards highlighting key capabilities.
        /// </summary>
        [JsonPropertyName("features")]
        public List<FeatureItem>? Features { get; set; }

        /**************************************************************/
        /// <summary>
        /// How-it-works section with step-by-step process visualization.
        /// </summary>
        [JsonPropertyName("howItWorks")]
        public HowItWorksContent? HowItWorks { get; set; }

        /**************************************************************/
        /// <summary>
        /// Use cases section showcasing application capabilities.
        /// </summary>
        [JsonPropertyName("useCases")]
        public UseCasesContent? UseCases { get; set; }

        /**************************************************************/
        /// <summary>
        /// API showcase section with terminal-style endpoint display.
        /// </summary>
        [JsonPropertyName("api")]
        public ApiContent? Api { get; set; }

        /**************************************************************/
        /// <summary>
        /// Call-to-action section with authentication buttons.
        /// </summary>
        [JsonPropertyName("cta")]
        public CtaContent? Cta { get; set; }

        #endregion

        #region legal page properties

        /**************************************************************/
        /// <summary>
        /// Legal sections for terms/privacy pages (heading + content pairs).
        /// </summary>
        [JsonPropertyName("sections")]
        public List<LegalSection>? Sections { get; set; }

        #endregion
    }

    // ============================================================
    // HERO SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Hero section content with headline, calls-to-action, and supporting text.
    /// </summary>
    /// <remarks>
    /// Supports both primary and secondary CTA buttons, plus an eyebrow badge.
    /// </remarks>
    public class HeroContent
    {
        /**************************************************************/
        /// <summary>
        /// Small text badge displayed above the headline (e.g., "Open Source").
        /// </summary>
        [JsonPropertyName("eyebrow")]
        public string? Eyebrow { get; set; }

        /**************************************************************/
        /// <summary>
        /// Main headline text with large typography.
        /// </summary>
        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        /**************************************************************/
        /// <summary>
        /// Supporting text below the headline.
        /// </summary>
        [JsonPropertyName("subheadline")]
        public string? Subheadline { get; set; }

        /**************************************************************/
        /// <summary>
        /// Primary call-to-action button text.
        /// </summary>
        [JsonPropertyName("ctaText")]
        public string? CtaText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Primary call-to-action button link URL.
        /// </summary>
        [JsonPropertyName("ctaLink")]
        public string? CtaLink { get; set; }

        /**************************************************************/
        /// <summary>
        /// Secondary call-to-action button text.
        /// </summary>
        [JsonPropertyName("secondaryCtaText")]
        public string? SecondaryCtaText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Secondary call-to-action button link URL.
        /// </summary>
        [JsonPropertyName("secondaryCtaLink")]
        public string? SecondaryCtaLink { get; set; }
    }

    // ============================================================
    // STATS SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Individual statistic item displayed in the stats section.
    /// </summary>
    /// <example>
    /// {
    ///   "value": "HL7",
    ///   "label": "Standards Compliant",
    ///   "description": "FDA SPL Implementation Guide (Dec 2023)"
    /// }
    /// </example>
    public class StatItem
    {
        /**************************************************************/
        /// <summary>
        /// The main value or metric (e.g., "HL7", "REST", "AI").
        /// </summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        /**************************************************************/
        /// <summary>
        /// Short label for the stat (e.g., "Standards Compliant").
        /// </summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        /**************************************************************/
        /// <summary>
        /// Longer description providing context for the stat.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    // ============================================================
    // FEATURES SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Feature card item displayed in the features grid.
    /// </summary>
    /// <remarks>
    /// The <see cref="Color"/> property determines the background color variant
    /// (blue, green, yellow, red, purple, teal).
    /// </remarks>
    public class FeatureItem
    {
        /**************************************************************/
        /// <summary>
        /// Emoji or icon character for the feature.
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        /**************************************************************/
        /// <summary>
        /// Feature title/heading.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Feature description text.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Color theme for the feature card background.
        /// </summary>
        /// <remarks>
        /// Valid values: blue, green, yellow, red, purple, teal.
        /// Maps to CSS class feature-card--{color}.
        /// </remarks>
        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    // ============================================================
    // HOW IT WORKS SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Container for the how-it-works section content.
    /// </summary>
    /// <seealso cref="StepItem"/>
    public class HowItWorksContent
    {
        /**************************************************************/
        /// <summary>
        /// Section title (e.g., "How MedRecPro Works").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section subtitle/description.
        /// </summary>
        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of process steps.
        /// </summary>
        [JsonPropertyName("steps")]
        public List<StepItem>? Steps { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Individual step in the how-it-works process visualization.
    /// </summary>
    public class StepItem
    {
        /**************************************************************/
        /// <summary>
        /// Step number displayed in the badge (e.g., "01", "02").
        /// </summary>
        [JsonPropertyName("number")]
        public string? Number { get; set; }

        /**************************************************************/
        /// <summary>
        /// Step title/heading.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Step description text.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Emoji or icon character for the step.
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }
    }

    // ============================================================
    // USE CASES SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Container for the use cases section content.
    /// </summary>
    /// <seealso cref="UseCaseItem"/>
    public class UseCasesContent
    {
        /**************************************************************/
        /// <summary>
        /// Section title (e.g., "Built for Pharmaceutical Professionals").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section subtitle/description.
        /// </summary>
        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of use case cards.
        /// </summary>
        [JsonPropertyName("cases")]
        public List<UseCaseItem>? Cases { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Individual use case card item.
    /// </summary>
    /// <remarks>
    /// The <see cref="Color"/> property determines the background color variant.
    /// </remarks>
    public class UseCaseItem
    {
        /**************************************************************/
        /// <summary>
        /// Use case title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Use case description text.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Color variant for the card background.
        /// </summary>
        /// <remarks>
        /// Valid values: light-red, light-yellow, light-blue, light-green, light-purple, light-teal.
        /// Maps to CSS class use-case-card--{color}.
        /// </remarks>
        [JsonPropertyName("color")]
        public string? Color { get; set; }

        /**************************************************************/
        /// <summary>
        /// Emoji or icon character for the use case.
        /// </summary>
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }
    }

    // ============================================================
    // API SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Container for the API showcase section content.
    /// </summary>
    /// <seealso cref="ApiEndpointItem"/>
    public class ApiContent
    {
        /**************************************************************/
        /// <summary>
        /// Section title (e.g., "Comprehensive REST API").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section subtitle/description.
        /// </summary>
        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of featured API endpoints.
        /// </summary>
        [JsonPropertyName("endpoints")]
        public List<ApiEndpointItem>? Endpoints { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// API endpoint item displayed in the terminal-style showcase.
    /// </summary>
    public class ApiEndpointItem
    {
        /**************************************************************/
        /// <summary>
        /// HTTP method (GET, POST, PUT, DELETE).
        /// </summary>
        /// <remarks>
        /// Used to apply color styling via CSS class api-method--{method.ToLower()}.
        /// </remarks>
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        /**************************************************************/
        /// <summary>
        /// API endpoint path (e.g., "/api/label/section/{section}").
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /**************************************************************/
        /// <summary>
        /// Brief description of the endpoint's purpose.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    // ============================================================
    // CTA SECTION MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Call-to-action section content with title and authentication prompt.
    /// </summary>
    public class CtaContent
    {
        /**************************************************************/
        /// <summary>
        /// CTA section title (e.g., "Ready to Simplify Your SPL Workflow?").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// CTA section subtitle/description.
        /// </summary>
        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Additional note displayed below the CTA buttons.
        /// </summary>
        /// <remarks>
        /// Typically used for license information or secondary messaging.
        /// </remarks>
        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    // ============================================================
    // LEGAL PAGE MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Legal section item for terms of service and privacy policy pages.
    /// Supports nested subsections, items, and contact information.
    /// </summary>
    /// <remarks>
    /// This model supports the expanded privacy policy schema with
    /// hierarchical content structure including subsections and itemized lists.
    /// </remarks>
    /// <seealso cref="LegalSubsection"/>
    /// <seealso cref="LegalContactInfo"/>
    public class LegalSection
    {
        /**************************************************************/
        /// <summary>
        /// Section heading (e.g., "1. Acceptance of Terms").
        /// </summary>
        [JsonPropertyName("heading")]
        public string? Heading { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section content/body text.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional collection of subsections within this section.
        /// </summary>
        /// <remarks>
        /// Subsections provide additional detail and may contain their own
        /// itemized lists for structured content like rights or data categories.
        /// </remarks>
        /// <seealso cref="LegalSubsection"/>
        [JsonPropertyName("subsections")]
        public List<LegalSubsection>? Subsections { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional contact information for sections like "Contact Us".
        /// </summary>
        /// <seealso cref="LegalContactInfo"/>
        [JsonPropertyName("contactInfo")]
        public LegalContactInfo? ContactInfo { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents a subsection within a legal section.
    /// </summary>
    /// <remarks>
    /// Subsections provide hierarchical organization for complex legal content,
    /// such as breaking down "Personal Data We Collect" into categories like
    /// "Information You Provide" and "Information Collected Automatically".
    /// </remarks>
    /// <seealso cref="LegalSection"/>
    /// <seealso cref="LegalItem"/>
    public class LegalSubsection
    {
        /**************************************************************/
        /// <summary>
        /// Subsection heading (e.g., "2.1 Information You Provide").
        /// </summary>
        [JsonPropertyName("subheading")]
        public string? Subheading { get; set; }

        /**************************************************************/
        /// <summary>
        /// Subsection content/body text.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional collection of items within this subsection.
        /// </summary>
        /// <remarks>
        /// Items provide structured lists of specific data types, rights,
        /// or other enumerable content within the subsection.
        /// </remarks>
        /// <example>
        /// Items might include "Account Data", "Profile Information", etc.
        /// under the "Information You Provide" subsection.
        /// </example>
        /// <seealso cref="LegalItem"/>
        [JsonPropertyName("items")]
        public List<LegalItem>? Items { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents an individual item within a legal subsection.
    /// </summary>
    /// <remarks>
    /// Items are used for definition-list style content where each item
    /// has a name/title and a corresponding description.
    /// </remarks>
    /// <example>
    /// Item: "Account Data"
    /// Description: "When you create an account, we collect your name, email address..."
    /// </example>
    /// <seealso cref="LegalSubsection"/>
    public class LegalItem
    {
        /**************************************************************/
        /// <summary>
        /// Item name or title (e.g., "Account Data", "Right to Access").
        /// </summary>
        [JsonPropertyName("item")]
        public string? Item { get; set; }

        /**************************************************************/
        /// <summary>
        /// Item description explaining the item in detail.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Contact information for legal pages.
    /// </summary>
    /// <remarks>
    /// Provides structured contact details for privacy and support inquiries.
    /// </remarks>
    /// <seealso cref="LegalSection"/>
    public class LegalContactInfo
    {
        /**************************************************************/
        /// <summary>
        /// Privacy-related email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /**************************************************************/
        /// <summary>
        /// General support email address.
        /// </summary>
        [JsonPropertyName("support")]
        public string? Support { get; set; }

        /**************************************************************/
        /// <summary>
        /// Website URL for contact form or additional information.
        /// </summary>
        [JsonPropertyName("website")]
        public string? Website { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Introduction section for legal pages.
    /// </summary>
    /// <remarks>
    /// Provides introductory content that appears before the main sections.
    /// </remarks>
    public class LegalIntroduction
    {
        /**************************************************************/
        /// <summary>
        /// Introduction content/body text.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents a third-party subprocessor that processes personal data.
    /// </summary>
    /// <remarks>
    /// Subprocessors are third-party services that process personal data
    /// on behalf of MedRecPro, such as cloud hosting providers or
    /// authentication services.
    /// </remarks>
    /// <seealso cref="SubprocessorInfo"/>
    public class Subprocessor
    {
        /**************************************************************/
        /// <summary>
        /// Name of the subprocessor (e.g., "Microsoft Azure").
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /**************************************************************/
        /// <summary>
        /// Purpose of data processing by this subprocessor.
        /// </summary>
        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }

        /**************************************************************/
        /// <summary>
        /// Geographic location(s) where data is processed.
        /// </summary>
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Container for subprocessor information including description and list.
    /// </summary>
    /// <remarks>
    /// Provides transparency about third-party data processors as required
    /// by GDPR and other privacy regulations.
    /// </remarks>
    /// <seealso cref="Subprocessor"/>
    public class SubprocessorInfo
    {
        /**************************************************************/
        /// <summary>
        /// Descriptive text explaining the subprocessor list.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Collection of subprocessors.
        /// </summary>
        /// <seealso cref="Subprocessor"/>
        [JsonPropertyName("list")]
        public List<Subprocessor>? List { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Extended page content model for privacy policy with additional schema support.
    /// </summary>
    /// <remarks>
    /// This model extends the base page content to support the expanded privacy
    /// policy schema including effective date, introduction, and subprocessors.
    /// </remarks>
    /// <seealso cref="LegalSection"/>
    /// <seealso cref="LegalIntroduction"/>
    /// <seealso cref="SubprocessorInfo"/>
    public class PrivacyPageContent
    {
        /**************************************************************/
        /// <summary>
        /// Page title (e.g., "MedRecPro Privacy Policy").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the policy becomes effective.
        /// </summary>
        [JsonPropertyName("effectiveDate")]
        public string? EffectiveDate { get; set; }

        /**************************************************************/
        /// <summary>
        /// Date when the policy was last updated.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public string? LastUpdated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Introduction section displayed before main content.
        /// </summary>
        /// <seealso cref="LegalIntroduction"/>
        [JsonPropertyName("introduction")]
        public LegalIntroduction? Introduction { get; set; }

        /**************************************************************/
        /// <summary>
        /// Collection of legal sections comprising the main content.
        /// </summary>
        /// <seealso cref="LegalSection"/>
        [JsonPropertyName("sections")]
        public List<LegalSection>? Sections { get; set; }

        /**************************************************************/
        /// <summary>
        /// Information about third-party subprocessors.
        /// </summary>
        /// <seealso cref="SubprocessorInfo"/>
        [JsonPropertyName("subprocessors")]
        public SubprocessorInfo? Subprocessors { get; set; }
    }
}