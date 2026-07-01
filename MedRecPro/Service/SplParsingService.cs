using ImportSplXmlParser = MedRecProImportClass.Service.SplXmlParser;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Compatibility adapter for the import-library SPL XML parser.
    /// </summary>
    /// <remarks>
    /// Keeps legacy web-project references to <see cref="SplXmlParser"/> compiling while
    /// delegating parser behavior to <see cref="MedRecProImportClass.Service.SplXmlParser"/>.
    /// New import code should depend on the import-library parser directly.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.SplXmlParser"/>
    /// <seealso cref="SplImportService"/>
    public class SplXmlParser : ImportSplXmlParser
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Initializes a new adapter instance for the import-library SPL XML parser.
        /// </summary>
        /// <param name="serviceProvider">Service provider used by the import-library parser.</param>
        /// <param name="logger">Logger for import-library parser diagnostics.</param>
        /// <remarks>
        /// The constructor forwards dependencies directly to the import-library parser and
        /// contains no local parser orchestration.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddScoped&lt;SplXmlParser&gt;();
        /// </code>
        /// </example>
        /// <seealso cref="MedRecProImportClass.Service.SplXmlParser"/>
        public SplXmlParser(
            IServiceProvider serviceProvider,
            ILogger<ImportSplXmlParser> logger)
            : base(serviceProvider, logger)
        {
            #region implementation
            #endregion
        }

        #endregion
    }
}
