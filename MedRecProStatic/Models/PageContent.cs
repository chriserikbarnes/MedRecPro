namespace MedRecPro.Static.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the configuration settings for the MedRecPro static site.
    /// </summary>
    public class SiteConfig
    {
        public string SiteName { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string AuthUrl { get; set; } = string.Empty;
        public string SwaggerUrl { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Represents the content for a specific page.
    /// </summary>
    public class PageContent
    {
        public string Title { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
        public List<ContentSection> Sections { get; set; } = new();
        public HeroSection? Hero { get; set; }
        public List<Feature>? Features { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents a content section with a heading and body text.
    /// </summary>
    public class ContentSection
    {
        public string Heading { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Represents the hero section on the home page.
    /// </summary>
    public class HeroSection
    {
        public string Headline { get; set; } = string.Empty;
        public string Subheadline { get; set; } = string.Empty;
        public string CtaText { get; set; } = string.Empty;
        public string CtaLink { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Represents a feature displayed on the home page.
    /// </summary>
    public class Feature
    {
        public string Icon { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Root container for all page content data.
    /// </summary>
    public class PagesData
    {
        public PageContent Home { get; set; } = new();
        public PageContent Terms { get; set; } = new();
        public PageContent Privacy { get; set; } = new();
    }
}