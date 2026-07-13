using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Exceptions
{
    /**************************************************************/
    /// <summary>
    /// Translates unhandled MedRecPro request failures into sanitized RFC 7807 problem details responses.
    /// </summary>
    /// <remarks>
    /// Authorization failures remain the responsibility of <see cref="MedRecPro.Filters.AuthorizationExceptionFilter"/>,
    /// which preserves their established response contract. This handler owns only exceptions that reach the global
    /// exception-handling boundary and deliberately omits exception details from the client response.
    /// </remarks>
    /// <seealso cref="IExceptionHandler"/>
    /// <seealso cref="IProblemDetailsService"/>
    public sealed class MedRecProExceptionHandler : IExceptionHandler
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Logger used to record unexpected request failures with request correlation data.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<MedRecProExceptionHandler> _logger;

        /**************************************************************/
        /// <summary>
        /// Service that writes the negotiated problem-details response.
        /// </summary>
        /// <seealso cref="IProblemDetailsService"/>
        private readonly IProblemDetailsService _problemDetailsService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="MedRecProExceptionHandler"/> class.
        /// </summary>
        /// <param name="logger">Logger used for the server-side exception record.</param>
        /// <param name="problemDetailsService">Service used to serialize the API error response.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="IExceptionHandler"/>
        public MedRecProExceptionHandler(
            ILogger<MedRecProExceptionHandler> logger,
            IProblemDetailsService problemDetailsService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _problemDetailsService = problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs an unexpected exception and writes a sanitized HTTP 500 problem-details response.
        /// </summary>
        /// <param name="httpContext">Current request context that receives the error response.</param>
        /// <param name="exception">Unhandled exception that escaped the request pipeline.</param>
        /// <param name="cancellationToken">Token used when writing the response is canceled.</param>
        /// <returns><see langword="true"/> after the exception has been handled.</returns>
        /// <remarks>
        /// The error payload includes the request path and a correlation identifier, but never the exception message or
        /// stack trace. Operators use the structured server-side log record to correlate that identifier with details.
        /// </remarks>
        /// <seealso cref="ProblemDetails"/>
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            #region implementation

            var traceId = RequestCorrelation.GetTraceId(httpContext);

            if (exception is OperationCanceledException
                && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Request {RequestMethod} {RequestPath} was canceled by the client with trace ID {TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    traceId);
                return true;
            }

            _logger.LogError(
                exception,
                "Unhandled exception while processing {RequestMethod} {RequestPath} with trace ID {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);

            await _problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
                    Instance = httpContext.Request.Path.Value
                }
            });

            return true;

            #endregion
        }

        #endregion
    }
}
