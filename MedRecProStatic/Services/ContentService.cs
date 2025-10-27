using System.Text.Json;
using MedRecPro.Static.Models;

namespace MedRecPro.Static.Services
{
    /**************************************************************/
    /// <summary>
    /// Service for loading and managing JSON-based content for static pages.
    /// </summary>
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
        public ContentService(IWebHostEnvironment env)
        {
            #region implementation

            // Configure JSON serializer options for case-insensitive deserialization
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Load configuration
            var configPath = Path.Combine(env.ContentRootPath, "Content", "config.json");
            var configJson = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<SiteConfig>(configJson, options)
                ?? throw new InvalidOperationException("Failed to load site configuration");

            // Load page content
            var pagesPath = Path.Combine(env.ContentRootPath, "Content", "pages.json");
            var pagesJson = File.ReadAllText(pagesPath);
            _pages = JsonSerializer.Deserialize<PagesData>(pagesJson, options)
                ?? throw new InvalidOperationException("Failed to load pages content");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the site configuration settings.
        /// </summary>
        public SiteConfig GetConfig() => _config;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the home page.
        /// </summary>
        public PageContent GetHomePage() => _pages.Home;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the Terms of Service page.
        /// </summary>
        public PageContent GetTermsPage() => _pages.Terms;

        /**************************************************************/
        /// <summary>
        /// Gets the content for the Privacy Policy page.
        /// </summary>
        public PageContent GetPrivacyPage() => _pages.Privacy;
    }
}