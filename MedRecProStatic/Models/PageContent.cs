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
        /// Base URL for the API endpoints.
        /// </summary>
        [JsonPropertyName("apiUrl")]
        public string? ApiUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Base URL for authentication endpoints.
        /// </summary>
        [JsonPropertyName("authUrl")]
        public string? AuthUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// URL to the Swagger/OpenAPI documentation.
        /// </summary>
        [JsonPropertyName("swaggerUrl")]
        public string? SwaggerUrl { get; set; }

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
        [JsonPropertyName("terms")]
        public PageContent Terms { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Content for the Privacy Policy page.
        /// </summary>
        [JsonPropertyName("privacy")]
        public PageContent Privacy { get; set; } = new();
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
        /// API endpoint path (e.g., "/api/labels/section/{section}").
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
    /// </summary>
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
    }
}