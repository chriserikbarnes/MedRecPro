using System.Text.Json;
using MedRecPro.Static.Models;

namespace MedRecPro.Static.Services
{
    /**************************************************************/
    /// <summary>
    /// Service for loading and managing JSON-based content for static pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service loads content from JSON files at application startup and provides
    /// strongly-typed access to page content throughout the application.
    /// </para>
    /// <para>
    /// Content files are expected to be located in the Content directory:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Content/config.json - Site configuration settings</description></item>
    ///   <item><description>Content/pages.json - Page content for home, terms, and privacy pages</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SiteConfig"/>
    /// <seealso cref="PagesData"/>
    /// <seealso cref="PageContent"/>
    public class ContentService
    {
        #region fields

        private readonly SiteConfig _config;
        private readonly PagesData _pages;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ContentService and loads content from JSON files.
        /// </summary>
        /// <param name="env">The web host environment providing access to content root path.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when configuration or pages content fails to load or deserialize.
        /// </exception>
        /// <remarks>
        /// JSON deserialization is configured with case-insensitive property matching
        /// to support both camelCase JSON and PascalCase C# properties.
        /// </remarks>
        /// <seealso cref="SiteConfig"/>
        /// <seealso cref="PagesData"/>
        public ContentService(IWebHostEnvironment env)
        {
            #region implementation

            // Configure JSON serializer options for flexible deserialization
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // Allow reading numbers from JSON strings if needed
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };

            // Load site configuration
            var configPath = Path.Combine(env.ContentRootPath, "Content", "config.json");
            var configJson = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<SiteConfig>(configJson, options)
                ?? throw new InvalidOperationException("Failed to load site configuration from config.json");

            // Load page content (home, terms, privacy, and all subsections)
            var pagesPath = Path.Combine(env.ContentRootPath, "Content", "pages.json");
            var pagesJson = File.ReadAllText(pagesPath);
            _pages = JsonSerializer.Deserialize<PagesData>(pagesJson, options)
                ?? throw new InvalidOperationException("Failed to load pages content from pages.json");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the site configuration settings.
        /// </summary>
        /// <returns>The site configuration containing API URLs, branding, and contact information.</returns>
        /// <seealso cref="SiteConfig"/>
        public SiteConfig GetConfig() => _config;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the home page.
        /// </summary>
        /// <returns>
        /// Page content including hero section, features, stats, how-it-works,
        /// use cases, API showcase, and CTA sections.
        /// </returns>
        /// <remarks>
        /// The home page content structure supports the redesigned marketing page
        /// with multiple sections for comprehensive product presentation.
        /// </remarks>
        /// <seealso cref="PageContent"/>
        /// <seealso cref="HeroContent"/>
        /// <seealso cref="FeatureItem"/>
        /// <seealso cref="StatItem"/>
        /// <seealso cref="HowItWorksContent"/>
        /// <seealso cref="UseCasesContent"/>
        /// <seealso cref="ApiContent"/>
        /// <seealso cref="CtaContent"/>
        public PageContent GetHomePage() => _pages.Home;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the Terms of Service page.
        /// </summary>
        /// <returns>Page content containing legal sections for terms of service.</returns>
        /// <seealso cref="PageContent"/>
        /// <seealso cref="LegalSection"/>
        public PageContent GetTermsPage() => _pages.Terms;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the Privacy Policy page.
        /// </summary>
        /// <returns>Page content containing legal sections for privacy policy.</returns>
        /// <seealso cref="PageContent"/>
        /// <seealso cref="LegalSection"/>
        public PageContent GetPrivacyPage() => _pages.Privacy;
    }
}