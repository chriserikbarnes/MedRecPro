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
        /// Content for the MCP Documentation page.
        /// </summary>
        /// <seealso cref="McpDocsPageContent"/>
        [JsonPropertyName("mcpDocs")]
        public McpDocsPageContent McpDocs { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Content for the MCP Getting Started page.
        /// </summary>
        /// <seealso cref="McpSetupPageContent"/>
        [JsonPropertyName("mcpSetup")]
        public McpSetupPageContent McpSetup { get; set; } = new();

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
        /// Feature showcase items displayed as alternating image+text rows.
        /// </summary>
        /// <remarks>
        /// Each item pairs a screenshot with descriptive text and mini-stats,
        /// creating visual rhythm inspired by Inspinia's landing page pattern.
        /// Odd-indexed items display image on the right, even on the left.
        /// </remarks>
        /// <seealso cref="FeatureShowcaseItem"/>
        [JsonPropertyName("featureShowcase")]
        public List<FeatureShowcaseItem>? FeatureShowcase { get; set; }

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

        /**************************************************************/
        /// <summary>
        /// Path to the hero illustration image displayed below the CTAs.
        /// </summary>
        /// <remarks>
        /// Typically a screenshot wrapped in a browser-chrome frame.
        /// Uses ASP.NET ~/ path format (e.g., "~/images/screenshots/Swagger-UI.PNG").
        /// </remarks>
        [JsonPropertyName("heroImage")]
        public string? HeroImage { get; set; }

        /**************************************************************/
        /// <summary>
        /// Alt text for the hero illustration image.
        /// </summary>
        [JsonPropertyName("heroImageAlt")]
        public string? HeroImageAlt { get; set; }
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
    // FEATURE SHOWCASE MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Feature showcase item displayed as an alternating image+text row.
    /// </summary>
    /// <remarks>
    /// Pairs a product screenshot with descriptive content and mini-statistics.
    /// Inspired by Inspinia's alternating feature rows pattern.
    /// </remarks>
    /// <seealso cref="MiniStat"/>
    public class FeatureShowcaseItem
    {
        /**************************************************************/
        /// <summary>
        /// Feature title/heading.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Primary description of the feature.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Secondary detail text providing additional context.
        /// </summary>
        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        /**************************************************************/
        /// <summary>
        /// Path to the feature screenshot image.
        /// </summary>
        /// <remarks>
        /// Uses ASP.NET ~/ path format (e.g., "~/images/screenshots/MCP-Indication-Example.PNG").
        /// </remarks>
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        /**************************************************************/
        /// <summary>
        /// Alt text for the feature screenshot image.
        /// </summary>
        [JsonPropertyName("imageAlt")]
        public string? ImageAlt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional CTA button text (e.g., "Try It Now").
        /// </summary>
        [JsonPropertyName("ctaText")]
        public string? CtaText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional CTA button link URL.
        /// </summary>
        [JsonPropertyName("ctaLink")]
        public string? CtaLink { get; set; }

        /**************************************************************/
        /// <summary>
        /// Mini-statistics displayed below the feature description.
        /// </summary>
        /// <seealso cref="MiniStat"/>
        [JsonPropertyName("stats")]
        public List<MiniStat>? Stats { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Mini-statistic displayed within a feature showcase row.
    /// </summary>
    /// <remarks>
    /// Compact stat display with a value and label, used to reinforce
    /// feature credibility with concrete numbers.
    /// </remarks>
    /// <seealso cref="FeatureShowcaseItem"/>
    public class MiniStat
    {
        /**************************************************************/
        /// <summary>
        /// The statistic value (e.g., "20+", "OAuth 2.1").
        /// </summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        /**************************************************************/
        /// <summary>
        /// Label describing the statistic (e.g., "AI Skills", "Protocol").
        /// </summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }
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

    // ============================================================
    // MCP DOCUMENTATION PAGE MODELS
    // ============================================================

    /**************************************************************/
    /// <summary>
    /// Content model for the MCP Documentation page.
    /// </summary>
    /// <remarks>
    /// Contains architecture, authentication, endpoints, tool documentation,
    /// LOINC codes, and tool selection guide information.
    /// </remarks>
    /// <seealso cref="McpToolCategory"/>
    /// <seealso cref="McpEndpointInfo"/>
    /// <seealso cref="McpLoincCode"/>
    public class McpDocsPageContent
    {
        /**************************************************************/
        /// <summary>
        /// Page title (e.g., "MedRecPro MCP Server").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Server description text.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Runtime information (e.g., "ASP.NET Core 8.0").
        /// </summary>
        [JsonPropertyName("runtime")]
        public string? Runtime { get; set; }

        /**************************************************************/
        /// <summary>
        /// Production URL for the MCP server.
        /// </summary>
        [JsonPropertyName("productionUrl")]
        public string? ProductionUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Architecture section description text.
        /// </summary>
        [JsonPropertyName("architectureDescription")]
        public string? ArchitectureDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// ASCII architecture diagram text.
        /// </summary>
        [JsonPropertyName("architectureDiagram")]
        public string? ArchitectureDiagram { get; set; }

        /**************************************************************/
        /// <summary>
        /// Virtual application path mapping table rows.
        /// </summary>
        [JsonPropertyName("virtualPaths")]
        public List<McpVirtualPath>? VirtualPaths { get; set; }

        /**************************************************************/
        /// <summary>
        /// Claude Desktop connection configuration JSON string.
        /// </summary>
        [JsonPropertyName("connectionConfig")]
        public string? ConnectionConfig { get; set; }

        /**************************************************************/
        /// <summary>
        /// Authentication description text.
        /// </summary>
        [JsonPropertyName("authDescription")]
        public string? AuthDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ordered list of authentication flow steps.
        /// </summary>
        [JsonPropertyName("authSteps")]
        public List<string>? AuthSteps { get; set; }

        /**************************************************************/
        /// <summary>
        /// Supported OAuth scopes.
        /// </summary>
        [JsonPropertyName("oauthScopes")]
        public string? OAuthScopes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Description for the endpoints section.
        /// </summary>
        [JsonPropertyName("endpointsDescription")]
        public string? EndpointsDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of available MCP endpoints.
        /// </summary>
        /// <seealso cref="McpEndpointInfo"/>
        [JsonPropertyName("endpoints")]
        public List<McpEndpointInfo>? Endpoints { get; set; }

        /**************************************************************/
        /// <summary>
        /// Note about endpoint behavior (e.g., GET returns 405).
        /// </summary>
        [JsonPropertyName("endpointsNote")]
        public string? EndpointsNote { get; set; }

        /**************************************************************/
        /// <summary>
        /// Description for the tools section.
        /// </summary>
        [JsonPropertyName("toolsDescription")]
        public string? ToolsDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tool categories with their tools.
        /// </summary>
        /// <seealso cref="McpToolCategory"/>
        [JsonPropertyName("toolCategories")]
        public List<McpToolCategory>? ToolCategories { get; set; }

        /**************************************************************/
        /// <summary>
        /// LOINC section codes for label filtering.
        /// </summary>
        /// <seealso cref="McpLoincCode"/>
        [JsonPropertyName("loincCodes")]
        public List<McpLoincCode>? LoincCodes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Note about LOINC section fallback behavior.
        /// </summary>
        [JsonPropertyName("loincNote")]
        public string? LoincNote { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tool selection guide entries mapping user intent to tools.
        /// </summary>
        /// <seealso cref="McpToolGuideEntry"/>
        [JsonPropertyName("toolSelectionGuide")]
        public List<McpToolGuideEntry>? ToolSelectionGuide { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Content model for the MCP Getting Started page.
    /// </summary>
    /// <remarks>
    /// Contains hero content, features, setup steps, authentication info,
    /// usage examples with screenshots, and tool reference tables.
    /// </remarks>
    /// <seealso cref="McpFeatureCard"/>
    /// <seealso cref="McpExampleCard"/>
    public class McpSetupPageContent
    {
        /**************************************************************/
        /// <summary>
        /// Page title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Hero section description text.
        /// </summary>
        [JsonPropertyName("heroText")]
        public string? HeroText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Extended description of capabilities.
        /// </summary>
        [JsonPropertyName("capabilitiesDescription")]
        public string? CapabilitiesDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// Feature cards displayed in a grid.
        /// </summary>
        /// <seealso cref="McpFeatureCard"/>
        [JsonPropertyName("features")]
        public List<McpFeatureCard>? Features { get; set; }

        /**************************************************************/
        /// <summary>
        /// Getting started steps.
        /// </summary>
        /// <seealso cref="McpStep"/>
        [JsonPropertyName("gettingStartedSteps")]
        public List<McpStep>? GettingStartedSteps { get; set; }

        /**************************************************************/
        /// <summary>
        /// Authentication information text.
        /// </summary>
        [JsonPropertyName("authInfo")]
        public string? AuthInfo { get; set; }

        /**************************************************************/
        /// <summary>
        /// Usage examples with screenshots.
        /// </summary>
        /// <seealso cref="McpExampleCard"/>
        [JsonPropertyName("examples")]
        public List<McpExampleCard>? Examples { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tool categories for reference tables.
        /// </summary>
        /// <seealso cref="McpToolCategory"/>
        [JsonPropertyName("toolCategories")]
        public List<McpToolCategory>? ToolCategories { get; set; }

        /**************************************************************/
        /// <summary>
        /// Data source disclaimer note.
        /// </summary>
        [JsonPropertyName("disclaimer")]
        public string? Disclaimer { get; set; }

        /**************************************************************/
        /// <summary>
        /// Support contact information.
        /// </summary>
        /// <seealso cref="McpSupportInfo"/>
        [JsonPropertyName("support")]
        public McpSupportInfo? Support { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Virtual path mapping for the IIS architecture table.
    /// </summary>
    public class McpVirtualPath
    {
        /**************************************************************/
        /// <summary>
        /// Virtual path (e.g., "/mcp").
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /**************************************************************/
        /// <summary>
        /// Physical path on disk (e.g., "site\wwwroot\mcp").
        /// </summary>
        [JsonPropertyName("physicalPath")]
        public string? PhysicalPath { get; set; }

        /**************************************************************/
        /// <summary>
        /// Project name (e.g., "MedRecProMCP").
        /// </summary>
        [JsonPropertyName("project")]
        public string? Project { get; set; }

        /**************************************************************/
        /// <summary>
        /// Purpose description.
        /// </summary>
        [JsonPropertyName("purpose")]
        public string? Purpose { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A category of MCP tools (e.g., "Drug Label Tools", "User Tools").
    /// </summary>
    /// <seealso cref="McpToolInfo"/>
    public class McpToolCategory
    {
        /**************************************************************/
        /// <summary>
        /// Category display name.
        /// </summary>
        [JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Category description text.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Workflow diagram text for this category.
        /// </summary>
        [JsonPropertyName("workflowDiagram")]
        public string? WorkflowDiagram { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tools in this category.
        /// </summary>
        /// <seealso cref="McpToolInfo"/>
        [JsonPropertyName("tools")]
        public List<McpToolInfo>? Tools { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Information about a single MCP tool.
    /// </summary>
    /// <seealso cref="McpToolParam"/>
    public class McpToolInfo
    {
        /**************************************************************/
        /// <summary>
        /// Tool name in snake_case (e.g., "search_drug_labels").
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /**************************************************************/
        /// <summary>
        /// Brief description of what the tool does.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Detailed usage description explaining when to use the tool.
        /// </summary>
        [JsonPropertyName("usageDescription")]
        public string? UsageDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// When to use this tool (short phrase for tables).
        /// </summary>
        [JsonPropertyName("whenToUse")]
        public string? WhenToUse { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tool parameters.
        /// </summary>
        /// <seealso cref="McpToolParam"/>
        [JsonPropertyName("parameters")]
        public List<McpToolParam>? Parameters { get; set; }

        /**************************************************************/
        /// <summary>
        /// Description of what the tool returns.
        /// </summary>
        [JsonPropertyName("returnsDescription")]
        public string? ReturnsDescription { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional workflow steps for multi-step tools.
        /// </summary>
        [JsonPropertyName("steps")]
        public List<McpToolStep>? Steps { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A parameter for an MCP tool.
    /// </summary>
    public class McpToolParam
    {
        /**************************************************************/
        /// <summary>
        /// Parameter name in camelCase.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /**************************************************************/
        /// <summary>
        /// Parameter data type (e.g., "string", "int").
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /**************************************************************/
        /// <summary>
        /// Whether the parameter is required.
        /// </summary>
        [JsonPropertyName("required")]
        public bool Required { get; set; }

        /**************************************************************/
        /// <summary>
        /// Description of the parameter including examples.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A step in a multi-step tool workflow (e.g., export_drug_label_markdown).
    /// </summary>
    public class McpToolStep
    {
        /**************************************************************/
        /// <summary>
        /// Step label (e.g., "Step 1 — Product Search").
        /// </summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        /**************************************************************/
        /// <summary>
        /// Step description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Parameters specific to this step.
        /// </summary>
        [JsonPropertyName("parameters")]
        public List<McpToolParam>? Parameters { get; set; }

        /**************************************************************/
        /// <summary>
        /// What this step returns.
        /// </summary>
        [JsonPropertyName("returnsDescription")]
        public string? ReturnsDescription { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// An MCP server endpoint.
    /// </summary>
    public class McpEndpointInfo
    {
        /**************************************************************/
        /// <summary>
        /// Endpoint path (e.g., "/mcp").
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        /**************************************************************/
        /// <summary>
        /// HTTP method (e.g., "POST", "GET").
        /// </summary>
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        /**************************************************************/
        /// <summary>
        /// Endpoint description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A LOINC section code used for drug label filtering.
    /// </summary>
    public class McpLoincCode
    {
        /**************************************************************/
        /// <summary>
        /// LOINC code (e.g., "34067-9").
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section name (e.g., "Indications and Usage").
        /// </summary>
        [JsonPropertyName("sectionName")]
        public string? SectionName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Typical user question that maps to this section.
        /// </summary>
        [JsonPropertyName("typicalQuestion")]
        public string? TypicalQuestion { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A tool selection guide entry mapping user intent to the appropriate tool.
    /// </summary>
    public class McpToolGuideEntry
    {
        /**************************************************************/
        /// <summary>
        /// User intent or question (e.g., "What are the side effects of X?").
        /// </summary>
        [JsonPropertyName("userIntent")]
        public string? UserIntent { get; set; }

        /**************************************************************/
        /// <summary>
        /// Recommended tool name.
        /// </summary>
        [JsonPropertyName("tool")]
        public string? Tool { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A feature card for the MCP Getting Started page.
    /// </summary>
    public class McpFeatureCard
    {
        /**************************************************************/
        /// <summary>
        /// Feature title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Feature description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// A getting started step.
    /// </summary>
    public class McpStep
    {
        /**************************************************************/
        /// <summary>
        /// Step text description.
        /// </summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// An example usage card with screenshot.
    /// </summary>
    public class McpExampleCard
    {
        /**************************************************************/
        /// <summary>
        /// Example title (e.g., "Look Up Side Effects and Warnings").
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Example prompt the user would type.
        /// </summary>
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Description of what Claude does in response.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Path to screenshot image.
        /// </summary>
        [JsonPropertyName("screenshot")]
        public string? Screenshot { get; set; }

        /**************************************************************/
        /// <summary>
        /// Screenshot alt text.
        /// </summary>
        [JsonPropertyName("screenshotAlt")]
        public string? ScreenshotAlt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Caption text below the screenshot.
        /// </summary>
        [JsonPropertyName("caption")]
        public string? Caption { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Support contact information for the MCP Getting Started page.
    /// </summary>
    public class McpSupportInfo
    {
        /**************************************************************/
        /// <summary>
        /// Support email address.
        /// </summary>
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        /**************************************************************/
        /// <summary>
        /// Technical documentation URL.
        /// </summary>
        [JsonPropertyName("docsUrl")]
        public string? DocsUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// GitHub issues URL.
        /// </summary>
        [JsonPropertyName("githubUrl")]
        public string? GithubUrl { get; set; }
    }
}