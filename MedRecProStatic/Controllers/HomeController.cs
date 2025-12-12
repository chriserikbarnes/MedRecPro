using Microsoft.AspNetCore.Mvc;
using MedRecPro.Static.Services;

namespace MedRecPro.Static.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Controller for handling static content pages including home, terms, privacy, and AI chat interface.
    /// </summary>
    /// <remarks>
    /// This controller serves the public-facing pages for MedRecPro.
    /// All content is loaded from JSON files via the ContentService.
    /// The AI Chat interface provides a conversational UI for interacting with the MedRecPro API.
    /// </remarks>
    /// <seealso cref="ContentService"/>
    public class HomeController : Controller
    {
        #region fields

        private readonly ContentService _contentService;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the HomeController.
        /// </summary>
        /// <param name="contentService">Service for loading page content from JSON files.</param>
        /// <seealso cref="ContentService"/>
        public HomeController(ContentService contentService)
        {
            _contentService = contentService;
        }

        /**************************************************************/
        /// <summary>
        /// Displays the home page with marketing information and features.
        /// </summary>
        /// <returns>View with home page content.</returns>
        /// <remarks>
        /// This is the default landing page for the MedRecPro static site.
        /// It displays the hero section, feature list, and call-to-action buttons.
        /// </remarks>
        public IActionResult Index()
        {
            var content = _contentService.GetHomePage();
            ViewBag.Config = _contentService.GetConfig();
            return View(content);
        }

        /**************************************************************/
        /// <summary>
        /// Displays the Terms of Service page.
        /// </summary>
        /// <returns>View with terms of service content.</returns>
        /// <remarks>
        /// This page is required for Azure App Registration compliance.
        /// Content is loaded from the pages.json file.
        /// </remarks>
        public IActionResult Terms()
        {
            var content = _contentService.GetTermsPage();
            ViewBag.Config = _contentService.GetConfig();
            return View(content);
        }

        /**************************************************************/
        /// <summary>
        /// Displays the Privacy Policy page.
        /// </summary>
        /// <returns>View with privacy policy content.</returns>
        /// <remarks>
        /// This page is required for Azure App Registration compliance.
        /// It informs users about data collection and usage practices.
        /// Content is loaded from the pages.json file.
        /// </remarks>
        public IActionResult Privacy()
        {
            var content = _contentService.GetPrivacyPage();
            ViewBag.Config = _contentService.GetConfig();
            return View(content);
        }

        /**************************************************************/
        /// <summary>
        /// Displays the AI Chat interface for natural language interaction with MedRecPro.
        /// </summary>
        /// <returns>View with the AI chat interface.</returns>
        /// <remarks>
        /// <para>
        /// This page provides a conversational interface for querying the MedRecPro system
        /// using natural language. The interface communicates with the AI API endpoints:
        /// </para>
        /// 
        /// <list type="bullet">
        ///   <item>
        ///     <term>GET /api/Ai/context</term>
        ///     <description>Retrieves system context including authentication status and capabilities.</description>
        ///   </item>
        ///   <item>
        ///     <term>POST /api/Ai/interpret</term>
        ///     <description>Interprets natural language queries into API endpoint specifications.</description>
        ///   </item>
        ///   <item>
        ///     <term>POST /api/Ai/synthesize</term>
        ///     <description>Synthesizes API results into human-readable responses.</description>
        ///   </item>
        ///   <item>
        ///     <term>GET /api/Ai/chat</term>
        ///     <description>Convenience endpoint for simple queries.</description>
        ///   </item>
        /// </list>
        /// 
        /// <para>
        /// The interface supports:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Streaming responses with real-time text display</description></item>
        ///   <item><description>Thinking process visualization (collapsible blocks)</description></item>
        ///   <item><description>Drag and drop file upload for ZIP files</description></item>
        ///   <item><description>Markdown rendering with syntax highlighting</description></item>
        ///   <item><description>Conversation history management</description></item>
        ///   <item><description>Request cancellation support</description></item>
        /// </list>
        /// 
        /// <example>
        /// Example URL: /Home/Chat
        /// </example>
        /// </remarks>
        /// <seealso cref="ContentService"/>
        public IActionResult Chat()
        {
            #region implementation

            // Load configuration for API base URL and branding
            ViewBag.Config = _contentService.GetConfig();

            // Set page title for the chat interface
            ViewBag.Title = "AI Assistant";
            ViewBag.Version = _contentService.GetConfig().Version;

            #endregion

            return View();
        }
    }
}