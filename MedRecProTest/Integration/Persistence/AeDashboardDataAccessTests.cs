using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// SQLite-backed tests for AE dashboard read data-access methods.
    /// </summary>
    /// <remarks>
    /// View entities are seeded through <see cref="DtoLabelAccessTestHelper"/> raw
    /// SQL helpers because EF Core keyless views cannot be added through DbSet.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="LabelView.AeDrugSummary"/>
    /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
    [TestClass]
    [TestCategory("Integration")]
    public partial class AeDashboardDataAccessTests
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Encryption secret used by dashboard data-access tests.
        /// </summary>
        private const string PkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        #endregion constants

        #region initialization

        /**************************************************************/
        /// <summary>
        /// Clears static caches and encryption state before each test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();

            #endregion
        }

        #endregion initialization

    }
}
