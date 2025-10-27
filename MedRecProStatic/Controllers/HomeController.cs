using Microsoft.AspNetCore.Mvc;
using MedRecPro.Static.Services;

namespace MedRecPro.Static.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Controller for handling static content pages including home, terms, and privacy.
    /// </summary>
    /// <remarks>
    /// This controller serves the public-facing pages for MedRecPro.
    /// All content is loaded from JSON files via the ContentService.
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
    }
}