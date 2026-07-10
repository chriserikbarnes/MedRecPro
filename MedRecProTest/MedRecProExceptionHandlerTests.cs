using MedRecPro.Configuration;
using MedRecPro.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests the cross-cutting RFC 7807 exception and validation response boundary.
    /// </summary>
    /// <remarks>
    /// These tests exercise the handler and MVC registrations without starting the production host, keeping failure
    /// response assertions independent of database, user-secret, and route configuration prerequisites.
    /// </remarks>
    /// <seealso cref="MedRecProExceptionHandler"/>
    /// <seealso cref="MedRecProMvcExtensions"/>
    [TestClass]
    public class MedRecProExceptionHandlerTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies an unexpected exception is logged server-side and converted to a sanitized HTTP 500 problem response.
        /// </summary>
        /// <seealso cref="MedRecProExceptionHandler.TryHandleAsync(HttpContext, Exception, CancellationToken)"/>
        [TestMethod]
        public async Task TryHandleAsync_UnexpectedException_WritesSanitizedInternalServerErrorProblemDetails()
        {
            #region implementation

            var httpContext = new DefaultHttpContext
            {
                TraceIdentifier = "phase8-trace-id"
            };
            httpContext.Request.Method = HttpMethods.Get;
            httpContext.Request.Path = "/Label/markdown/export/example";

            var problemDetailsService = new CapturingProblemDetailsService();
            var handler = new MedRecProExceptionHandler(
                NullLogger<MedRecProExceptionHandler>.Instance,
                problemDetailsService);
            var exception = new InvalidOperationException("Sensitive exception detail");

            var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

            Assert.IsTrue(handled);
            Assert.IsNotNull(problemDetailsService.LastContext);
            Assert.AreSame(exception, problemDetailsService.LastContext.Exception);
            Assert.AreEqual(StatusCodes.Status500InternalServerError, problemDetailsService.LastContext.ProblemDetails.Status);
            Assert.AreEqual("An unexpected error occurred.", problemDetailsService.LastContext.ProblemDetails.Title);
            Assert.AreEqual("/Label/markdown/export/example", problemDetailsService.LastContext.ProblemDetails.Instance);
            Assert.IsNull(problemDetailsService.LastContext.ProblemDetails.Detail);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the normal MVC registration installs the global exception handler and problem-details services.
        /// </summary>
        /// <seealso cref="MedRecProMvcExtensions.AddMedRecProApiControllers(IServiceCollection, IConfiguration)"/>
        [TestMethod]
        public void AddMedRecProApiControllers_DefaultRegistration_RegistersExceptionAndProblemDetailsServices()
        {
            #region implementation

            var services = createApiServiceCollection();

            using var provider = services.BuildServiceProvider();
            Assert.IsNotNull(provider.GetService<IProblemDetailsService>());
            Assert.IsTrue(provider.GetServices<IExceptionHandler>().OfType<MedRecProExceptionHandler>().Any());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies invalid MVC model state returns a consistent RFC 7807 HTTP 400 response with correlation data.
        /// </summary>
        /// <seealso cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/>
        [TestMethod]
        public void AddMedRecProApiControllers_InvalidModelState_ReturnsValidationProblemDetails()
        {
            #region implementation

            var services = createApiServiceCollection();

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
            var httpContext = new DefaultHttpContext
            {
                TraceIdentifier = "phase8-validation-trace-id"
            };
            httpContext.Request.Path = "/Label/markdown/export/example";

            var modelState = new ModelStateDictionary();
            modelState.AddModelError("documentGuid", "The document GUID is invalid.");
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);

            var response = options.InvalidModelStateResponseFactory(actionContext);
            var badRequest = response as BadRequestObjectResult;
            var problemDetails = badRequest?.Value as ValidationProblemDetails;

            Assert.IsNotNull(badRequest);
            Assert.IsNotNull(problemDetails);
            Assert.AreEqual(StatusCodes.Status400BadRequest, problemDetails.Status);
            Assert.AreEqual("One or more validation errors occurred.", problemDetails.Title);
            Assert.AreEqual("/Label/markdown/export/example", problemDetails.Instance);
            Assert.AreEqual("phase8-validation-trace-id", problemDetails.Extensions["traceId"]);
            CollectionAssert.Contains(badRequest.ContentTypes, "application/problem+json");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the minimal service collection required to exercise the MedRecPro MVC registration boundary.
        /// </summary>
        /// <returns>Service collection configured with logging, in-memory configuration, and MedRecPro API MVC services.</returns>
        /// <seealso cref="MedRecProMvcExtensions"/>
        private static IServiceCollection createApiServiceCollection()
        {
            #region implementation

            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["IgnoreEmptyObjectsWhenSerializing"] = "false"
                })
                .Build();

            services.AddLogging();
            services.AddMedRecProApiControllers(configuration);
            return services;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captures the problem-details context supplied by the exception handler without requiring an HTTP host.
        /// </summary>
        /// <seealso cref="IProblemDetailsService"/>
        private sealed class CapturingProblemDetailsService : IProblemDetailsService
        {
            #region implementation

            /**************************************************************/
            /// <summary>
            /// Gets the most recent problem-details context written by the handler.
            /// </summary>
            public ProblemDetailsContext? LastContext { get; private set; }

            /**************************************************************/
            /// <summary>
            /// Records the supplied problem-details context for later test assertions.
            /// </summary>
            /// <param name="context">Problem-details context generated by the handler.</param>
            /// <returns>A completed asynchronous operation.</returns>
            /// <seealso cref="IProblemDetailsService.WriteAsync(ProblemDetailsContext)"/>
            public ValueTask WriteAsync(ProblemDetailsContext context)
            {
                #region implementation

                LastContext = context;
                return ValueTask.CompletedTask;

                #endregion
            }

            #endregion
        }

        #endregion
    }
}
