using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Verifies AE dashboard read-model mappings on the web application context.
    /// </summary>
    /// <remarks>
    /// These tests guard the feature-owned EF Core configuration classes used by
    /// <see cref="ApplicationDbContext"/> after the Phase 7 mapping-boundary cleanup.
    /// </remarks>
    /// <seealso cref="ApplicationDbContext"/>
    /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
    [TestClass]
    public class ApplicationDbContextAeDashboardMappingTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies the keyed AE dashboard table mappings remain table-backed.
        /// </summary>
        /// <seealso cref="LabelView.FlattenedAdverseEventTable"/>
        /// <seealso cref="LabelView.AeDashboardProductCatalog"/>
        [TestMethod]
        public void ApplicationDbContext_AeDashboardKeyedTables_UseFeatureConfigurations()
        {
            #region implementation

            using var context = createContext();

            assertKeyedTable<LabelView.FlattenedAdverseEventTable>(
                context,
                "tmp_FlattenedAdverseEventTable",
                nameof(LabelView.FlattenedAdverseEventTable.Id),
                "tmp_FlattenedAdverseEventTableID");
            assertDecimalColumn<LabelView.FlattenedAdverseEventTable>(
                context,
                nameof(LabelView.FlattenedAdverseEventTable.Dose));

            assertKeyedTable<LabelView.FlattenedAdverseEventCoverageTable>(
                context,
                "tmp_FlattenedAdverseEventCoverageTable",
                nameof(LabelView.FlattenedAdverseEventCoverageTable.Id),
                "tmp_FlattenedAdverseEventCoverageTableID");
            assertDecimalColumn<LabelView.FlattenedAdverseEventCoverageTable>(
                context,
                nameof(LabelView.FlattenedAdverseEventCoverageTable.Dose));
            assertDecimalColumn<LabelView.FlattenedAdverseEventCoverageTable>(
                context,
                nameof(LabelView.FlattenedAdverseEventCoverageTable.ComparatorDose));

            assertKeyedTable<LabelView.FlattenedAdverseEventRiskTable>(
                context,
                "tmp_FlattenedAdverseEventRiskTable",
                nameof(LabelView.FlattenedAdverseEventRiskTable.Id),
                "tmp_FlattenedAdverseEventRiskTableID");
            assertDecimalColumn<LabelView.FlattenedAdverseEventRiskTable>(
                context,
                nameof(LabelView.FlattenedAdverseEventRiskTable.Dose));

            assertKeyedTable<LabelView.AeDashboardProductCatalog>(
                context,
                "tmp_AeDashboardProductCatalog",
                nameof(LabelView.AeDashboardProductCatalog.Id),
                "AeDashboardProductCatalogID");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AE dashboard drug summary remains a keyless read-only view.
        /// </summary>
        /// <seealso cref="LabelView.AeDrugSummary"/>
        [TestMethod]
        public void ApplicationDbContext_AeDrugSummary_MapsAsKeylessView()
        {
            #region implementation

            using var context = createContext();
            var entityType = context.Model.FindEntityType(typeof(LabelView.AeDrugSummary));

            Assert.IsNotNull(entityType);
            Assert.IsNull(entityType!.FindPrimaryKey());
            Assert.AreEqual("vw_AeDrugSummary", entityType.GetViewName());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an isolated application context for metadata assertions.
        /// </summary>
        /// <returns>An application context backed by an isolated in-memory database name.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        private static ApplicationDbContext createContext()
        {
            #region implementation

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ApplicationDbContextAeDashboardMappingTests_{Guid.NewGuid():N}")
                .Options;

            return new ApplicationDbContext(options);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts an entity is configured as a keyed table with the expected key column.
        /// </summary>
        /// <typeparam name="TEntity">Entity type to inspect.</typeparam>
        /// <param name="context">Application context containing the model metadata.</param>
        /// <param name="tableName">Expected relational table name.</param>
        /// <param name="keyPropertyName">CLR key property name.</param>
        /// <param name="keyColumnName">Expected relational key column name.</param>
        /// <seealso cref="ApplicationDbContext"/>
        private static void assertKeyedTable<TEntity>(
            ApplicationDbContext context,
            string tableName,
            string keyPropertyName,
            string keyColumnName)
        {
            #region implementation

            var entityType = context.Model.FindEntityType(typeof(TEntity));
            Assert.IsNotNull(entityType);
            Assert.AreEqual(tableName, entityType!.GetTableName());
            Assert.IsNotNull(entityType.FindPrimaryKey());

            var keyProperty = entityType.FindProperty(keyPropertyName);
            Assert.IsNotNull(keyProperty);
            Assert.AreEqual(keyColumnName, keyProperty!.GetColumnName());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts an entity property keeps the Stage 5 decimal precision.
        /// </summary>
        /// <typeparam name="TEntity">Entity type to inspect.</typeparam>
        /// <param name="context">Application context containing the model metadata.</param>
        /// <param name="propertyName">CLR property name to inspect.</param>
        /// <seealso cref="ApplicationDbContext"/>
        private static void assertDecimalColumn<TEntity>(
            ApplicationDbContext context,
            string propertyName)
        {
            #region implementation

            var entityType = context.Model.FindEntityType(typeof(TEntity));
            Assert.IsNotNull(entityType);

            var property = entityType!.FindProperty(propertyName);
            Assert.IsNotNull(property);
            Assert.AreEqual("decimal(18, 6)", property!.FindAnnotation("Relational:ColumnType")?.Value);

            #endregion
        }

        #endregion
    }
}
