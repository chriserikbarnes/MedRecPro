using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Static.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Serves the adverse-event dashboard React island from the static MVC host.
    /// </summary>
    /// <remarks>
    /// This controller owns only the static route and view orchestration for
    /// <c>/adverse-events</c>. Product search, KPI metrics, favorites, and later
    /// visualization data are loaded by the compiled React bundle through the
    /// live <c>/api/AdverseEvent</c> endpoints.
    /// </remarks>
    /// <example>
    /// <code>
    /// GET /adverse-events
    /// </code>
    /// </example>
    /// <seealso cref="HomeController"/>
    public class AdverseEventDashboardController : Controller
    {
        #region public dashboard route

        /**************************************************************/
        /// <summary>
        /// Displays the standalone adverse-event dashboard page.
        /// </summary>
        /// <returns>The MVC view that mounts the React dashboard bundle.</returns>
        /// <remarks>
        /// The view intentionally uses <c>Layout = null</c> so the dashboard can
        /// own its CSS and JavaScript without changing existing MedRecProStatic
        /// pages such as Home, Chat, legal pages, or MCP documentation.
        /// </remarks>
        /// <example>
        /// <code>
        /// /adverse-events?product=11111111-1111-1111-1111-111111111111
        /// </code>
        /// </example>
        /// <seealso cref="Index"/>
        [HttpGet("adverse-events")]
        public IActionResult Index()
        {
            #region implementation

            // The React bundle reads query-string state directly from the browser.
            return View();

            #endregion
        }

        #endregion public dashboard route
    }
}
