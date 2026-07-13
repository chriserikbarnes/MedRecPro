using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace MedRecPro.Exceptions
{
    /**************************************************************/
    /// <summary>
    /// Provides the one request correlation identifier shared by API responses and server logs.
    /// </summary>
    /// <remarks>
    /// Prefer the hosting activity identifier when one exists so distributed traces remain connected. The HTTP trace
    /// identifier remains the deterministic fallback for requests that do not create an activity.
    /// </remarks>
    /// <seealso cref="MedRecProExceptionHandler"/>
    internal static class RequestCorrelation
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the correlation identifier for the supplied HTTP request.
        /// </summary>
        /// <param name="httpContext">Current HTTP request context.</param>
        /// <returns>Activity identifier when available; otherwise the HTTP trace identifier.</returns>
        internal static string GetTraceId(HttpContext httpContext)
        {
            #region implementation

            return Activity.Current?.Id ?? httpContext.TraceIdentifier;

            #endregion
        }

        #endregion
    }
}
