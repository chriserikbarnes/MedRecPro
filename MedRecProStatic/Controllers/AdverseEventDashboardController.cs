using MedRecPro.Static.Services;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Static.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Controller for the adverse-event dashboard static application shell.
    /// </summary>
    /// <remarks>
    /// This controller serves the client-side dashboard page only. All adverse-event
    /// product, visualization, reverse lookup, interchange, and favorite data is
    /// retrieved directly by browser modules from the MedRecPro API.
    /// </remarks>
    /// <example>
    /// <code>
    /// GET /adverse-events
    /// </code>
    /// </example>
    /// <seealso cref="ContentService"/>
    public class AdverseEventDashboardController : Controller
    {
        #region fields

        /**************************************************************/
        /// <summary>
        /// Content and site configuration loader used by static app pages.
        /// </summary>
        private readonly ContentService _contentService;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AdverseEventDashboardController"/> class.
        /// </summary>
        /// <param name="contentService">Service for loading shared site configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentService"/> is null.</exception>
        /// <seealso cref="ContentService"/>
        public AdverseEventDashboardController(ContentService contentService)
        {
            #region implementation

            _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Displays the adverse-event dashboard client application.
        /// </summary>
        /// <returns>The dashboard view shell.</returns>
        /// <remarks>
        /// The view is a null-layout operational surface. Client-side JavaScript
        /// modules call the API virtual application directly and use shared browser
        /// URL helpers for local and production routing.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /adverse-events?view=triage
        /// </code>
        /// </example>
        /// <seealso cref="ContentService.GetConfig"/>
        [Route("adverse-events")]
        public IActionResult Index()
        {
            #region implementation

            var config = _contentService.GetConfig();
            ViewBag.Config = config;
            ViewBag.Title = "Adverse Events Dashboard";
            ViewBag.Version = config.Version;

            #endregion

            return View();
        }

        #endregion
    }
}
