using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Shares one deterministic MedRecPro integration-test host across the test assembly.
/// </summary>
/// <remarks>
/// Sharing avoids repeatedly reassigning process-wide <c>Util</c> and <c>User</c> configuration
/// state while the legacy static helpers remain. Host-backed test classes are marked non-parallel.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
public class MedRecProHostFixture
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the shared factory after assembly initialization.
    /// </summary>
    /// <remarks>
    /// Accessing <see cref="MedRecProWebApplicationFactory.Services"/> starts and validates the real host.
    /// </remarks>
    public static MedRecProWebApplicationFactory Factory { get; private set; } = null!;

    /**************************************************************/
    /// <summary>
    /// Creates the shared factory before MSTest runs host-backed test classes.
    /// </summary>
    /// <param name="testContext">MSTest context supplied at assembly initialization.</param>
    /// <seealso cref="Factory"/>
    [AssemblyInitialize]
    public static void StartHost(TestContext testContext)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(testContext);
        Factory = new MedRecProWebApplicationFactory();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Disposes the shared factory after all tests complete.
    /// </summary>
    /// <seealso cref="Factory"/>
    [AssemblyCleanup]
    public static void StopHost()
    {
        #region implementation

        Factory?.Dispose();

        #endregion
    }

    #endregion
}
