using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// SQL contract tests for the Stage 5 <c>dbo.vw_AeRisk</c> projection.
    /// </summary>
    /// <remarks>
    /// These tests inspect the checked-in view definition because the normal unit
    /// test providers do not execute SQL Server view DDL. They guard high-risk
    /// projection contracts that feed the materialized AE risk table.
    /// </remarks>
    [TestClass]
    public class AeRiskViewSqlTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies that product and substance context do not depend on pharmacologic-class enrichment.
        /// </summary>
        /// <remarks>
        /// Sampled Stage 5 risk rows exposed broad null product/substance fields
        /// whenever class context was absent. The view must resolve those fields
        /// from <c>vw_ProductsByIngredient</c> before applying optional class
        /// fan-out.
        /// </remarks>
        /// <example>
        /// <code>
        /// dotnet test MedRecProTest/MedRecProTest.csproj --filter AeRiskViewSqlTests
        /// </code>
        /// </example>
        /// <seealso cref="AeRiskViewSqlTests"/>
        [TestMethod]
        public void VwAeRisk_ProductAndSubstanceContext_UsesIngredientFallback()
        {
            #region implementation

            var sql = readViewsSql();

            StringAssert.Contains(sql, "FROM dbo.vw_ProductsByIngredient AS pbi");
            StringAssert.Contains(sql, "pbi.DocumentGUID = fae.DocumentGUID");
            StringAssert.Contains(sql, "pbi.IngredientClassCode <> 'IACT'");
            StringAssert.Contains(sql, "pbi.UNII = fae.UNII");
            StringAssert.Contains(sql, "COALESCE(pc.ProductName, productContext.ProductName) AS ProductName");
            StringAssert.Contains(sql, "COALESCE(pc.SubstanceName, productContext.SubstanceName) AS SubstanceName");
            StringAssert.Contains(sql, "COALESCE(pc.ActiveMoietyID, productContext.ActiveMoietyID) AS ActiveMoietyID");
            StringAssert.Contains(sql, "COALESCE(pc.IngredientSubstanceID, productContext.IngredientSubstanceID) AS IngredientSubstanceID");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that number-needed estimates are projected for every RR-ready row.
        /// </summary>
        /// <remarks>
        /// The AE risk table needs number-needed data for significant and
        /// non-significant rows. Significance classifies the confidence interval;
        /// it must not gate the point estimate projection.
        /// </remarks>
        /// <example>
        /// <code>
        /// dotnet test MedRecProTest/MedRecProTest.csproj --filter AeRiskViewSqlTests
        /// </code>
        /// </example>
        /// <seealso cref="AeRiskViewSqlTests"/>
        [TestMethod]
        public void VwAeRisk_NumberNeededProjection_IsNotSignificanceGated()
        {
            #region implementation

            var sql = readViewsSql();

            StringAssert.Contains(sql, "ABS(1.0 / NULLIF(r.RiskT - r.RiskC, 0)) AS NumberNeeded");
            StringAssert.Contains(sql, "WHEN r.RiskT > r.RiskC THEN 'NNH'");
            StringAssert.Contains(sql, "WHEN r.RiskT < r.RiskC THEN 'NNT'");
            Assert.IsFalse(sql.Contains("WHEN sig.IsSignificant = 1"),
                "Number-needed projection should not be gated by significance.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads the repository SQL view script from the test output folder.
        /// </summary>
        /// <returns>The full <c>MedRecPro_Views.sql</c> text.</returns>
        /// <remarks>
        /// Test execution starts under a build output directory, so the helper
        /// walks upward until it reaches the shared repository parent that
        /// contains the <c>MedRecPro</c> project folder.
        /// </remarks>
        /// <seealso cref="VwAeRisk_ProductAndSubstanceContext_UsesIngredientFallback"/>
        /// <seealso cref="VwAeRisk_NumberNeededProjection_IsNotSignificanceGated"/>
        private static string readViewsSql()
        {
            #region implementation

            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "MedRecPro", "SQL", "MedRecPro_Views.sql");
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            Assert.Fail("Unable to locate MedRecPro/SQL/MedRecPro_Views.sql from the test output directory.");
            return string.Empty;

            #endregion
        }
    }
}
