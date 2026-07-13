using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Adds an explicit test-only request probe after the production application pipeline.
/// </summary>
/// <remarks>
/// The probe is registered only by <see cref="MedRecProWebApplicationFactory"/> when a contract test opts in.
/// It makes an otherwise ordinary request throw after the real exception middleware is installed, proving that
/// the production exception handler returns a sanitized ProblemDetails payload without adding an application route.
/// </remarks>
/// <seealso cref="IStartupFilter"/>
/// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
internal sealed class TestExceptionThrowingStartupFilter : IStartupFilter
{
    #region implementation

    /// <summary>
    /// Gets the unadvertised test-only path that triggers the pipeline exception.
    /// </summary>
    /// <remarks>
    /// No production endpoint maps this path; the startup filter examines it only after the production pipeline
    /// has been configured by the application entry point.
    /// </remarks>
    public const string ThrowPath = "/test-host/throw";

    /**************************************************************/
    /// <summary>
    /// Adds the probe after the application's configured middleware and endpoint mappings.
    /// </summary>
    /// <param name="next">The application pipeline configuration supplied by the hosting runtime.</param>
    /// <returns>A configuration delegate that appends the test-only probe.</returns>
    /// <seealso cref="UseExtensions.Use"/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        #region implementation

        return application =>
        {
            next(application);
            application.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == ThrowPath)
                {
                    throw new InvalidOperationException("Phase 3 test-only exception detail must not reach clients.");
                }

                await nextMiddleware();
            });
        };

        #endregion
    }

    #endregion
}
