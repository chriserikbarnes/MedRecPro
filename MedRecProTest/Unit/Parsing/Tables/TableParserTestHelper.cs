using MedRecProImportClass.Service.TransformationServices;

namespace MedRecProTest.Unit.Parsing.Tables
{
    /**************************************************************/
    /// <summary>
    /// Creates the standard production parser set for table-parser test fixtures.
    /// </summary>
    /// <remarks>
    /// The test suite exercises the same ordered parser collection in router and
    /// orchestrator tests. Keeping that construction in one focused helper prevents
    /// fixture drift when a production parser is added or its order changes.
    /// </remarks>
    /// <seealso cref="ITableParser"/>
    /// <seealso cref="TableParserRouter"/>
    internal static class TableParserTestHelper
    {
        /**************************************************************/
        /// <summary>
        /// Creates one instance of every parser used by the production router.
        /// </summary>
        /// <returns>A new ordered parser collection suitable for one test fixture.</returns>
        /// <remarks>
        /// Each call returns new parser instances so mutable parser state cannot leak
        /// between tests or between router and orchestrator fixtures.
        /// </remarks>
        /// <seealso cref="PkTableParser"/>
        /// <seealso cref="SimpleArmTableParser"/>
        /// <seealso cref="MultilevelAeTableParser"/>
        /// <seealso cref="AeWithSocTableParser"/>
        /// <seealso cref="EfficacyMultilevelTableParser"/>
        internal static List<ITableParser> CreateProductionParsers()
        {
            #region implementation

            return new List<ITableParser>
            {
                new PkTableParser(),
                new SimpleArmTableParser(),
                new MultilevelAeTableParser(),
                new AeWithSocTableParser(),
                new EfficacyMultilevelTableParser()
            };

            #endregion
        }
    }
}
